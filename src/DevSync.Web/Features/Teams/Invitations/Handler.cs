using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using DevSync.Domain.Teams;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DevSync.Web.Features.Teams.Invitations;

public sealed record ViewModel(Guid Id, string TeamName, string Role, DateTimeOffset ExpiresAt, string Token);
public sealed class Handler(AppDbContext db, TimeProvider clock)
{
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public async Task<Result> InviteAsync(Guid id, Guid actor, Guid version, string? email, string? role, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (team, actorRole) = await Access.LockAsync(db, id, actor, ct);
        if (team is null) return new(404);
        if (!Access.Manage(actorRole!)) return new(403, "Only an owner or admin can invite members.");
        if (team.Version != version) return new(409, "This team changed. Reload before inviting.");
        email = (email ?? "").Trim();
        if (email.Length > 256 || !new EmailAddressAttribute().IsValid(email) || role is not ("member" or "viewer")) return new(422, "Enter a valid email and choose Member or Viewer.");
        var normalized = email.ToUpperInvariant(); var now = clock.GetUtcNow().ToUniversalTime();
        if (await db.TeamMemberships.Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m.TeamId, u.NormalizedEmail }).AnyAsync(x => x.TeamId == id && x.NormalizedEmail == normalized, ct))
            return new(422, "That email already belongs to this team.");
        if (await db.TeamInvitations.CountAsync(i => i.TeamId == id && i.ConsumedAt == null && i.RevokedAt == null && i.ExpiresAt > now, ct) >= 100)
            return new(422, "This team has 100 pending invitations. Revoke an invitation first.");
        var previous = await db.TeamInvitations.Where(i => i.TeamId == id && i.NormalizedEmail == normalized && i.ConsumedAt == null && i.RevokedAt == null).ToListAsync(ct);
        foreach (var invite in previous) invite.RevokedAt = now;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var invitation = new TeamInvitation { TeamId = id, Email = email, NormalizedEmail = normalized, Role = role, TokenHash = Hash(token), ExpiresAt = now.AddDays(7) };
        db.TeamInvitations.Add(invitation); Access.Record(db, team, actor, "An invitation was created.", clock);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(200, Id: invitation.Id, Token: token);
    }
    public async Task<ViewModel?> LoadAsync(string? token, Guid actor, CancellationToken ct)
    {
        if (token is null || token.Length != 64 || !token.All(Uri.IsHexDigit)) return null;
        var hash = Hash(token); var now = clock.GetUtcNow();
        var email = await db.Users.Where(u => u.Id == actor && u.EmailConfirmed).Select(u => u.NormalizedEmail).SingleOrDefaultAsync(ct);
        if (email is null) return null;
        return await (from i in db.TeamInvitations.AsNoTracking() join t in db.Teams.AsNoTracking() on i.TeamId equals t.Id
            where i.TokenHash == hash && i.NormalizedEmail == email && i.RevokedAt == null && i.ConsumedAt == null && i.ExpiresAt > now
            select new ViewModel(i.Id, t.Name, i.Role, i.ExpiresAt, token)).SingleOrDefaultAsync(ct);
    }
    public async Task<Result> AcceptAsync(string? token, Guid actor, CancellationToken ct)
    {
        if (token is null || token.Length != 64 || !token.All(Uri.IsHexDigit)) return new(404);
        var hash = Hash(token);
        var teamId = await db.TeamInvitations.Where(i => i.TokenHash == hash).Select(i => (Guid?)i.TeamId).SingleOrDefaultAsync(ct);
        if (teamId is null) return new(404);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // The team lock serializes invitation acceptance with role changes and revocation.
        var team = await db.Teams.FromSqlInterpolated($"SELECT * FROM \"Teams\" WHERE \"Id\" = {teamId.Value} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (team is null) return new(404);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {actor} FOR UPDATE").SingleOrDefaultAsync(ct);
        var invite = await db.TeamInvitations.SingleOrDefaultAsync(i => i.TokenHash == hash, ct);
        var now = clock.GetUtcNow().ToUniversalTime();
        if (user is null || !user.EmailConfirmed || invite is null || invite.NormalizedEmail != user.NormalizedEmail || invite.ConsumedAt != null || invite.RevokedAt != null || invite.ExpiresAt <= now) return new(404);
        if (await db.TeamMemberships.AnyAsync(m => m.TeamId == team.Id && m.UserId == actor, ct)) return new(422, "You already belong to this team.");
        if (await db.TeamMemberships.CountAsync(m => m.TeamId == team.Id, ct) >= 50 || await db.TeamMemberships.CountAsync(m => m.UserId == actor, ct) >= 20)
            return new(422, "Team or account membership limit reached.");
        invite.ConsumedAt = now; db.TeamMemberships.Add(new TeamMembership { TeamId = team.Id, UserId = actor, Role = invite.Role, JoinedAt = now });
        Access.Record(db, team, actor, "An invited member joined the team.", clock);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(200, Id: team.Id);
    }
    public async Task<Result> RevokeAsync(Guid id, Guid inviteId, Guid actor, Guid version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (team, role) = await Access.LockAsync(db, id, actor, ct);
        if (team is null) return new(404);
        if (!Access.Manage(role!)) return new(403, "Only an owner or admin can revoke invitations.");
        if (team.Version != version) return new(409, "This team changed. Reload before revoking.");
        var invite = await db.TeamInvitations.SingleOrDefaultAsync(i => i.Id == inviteId && i.TeamId == id && i.ConsumedAt == null && i.RevokedAt == null, ct);
        if (invite is null) return new(404);
        invite.RevokedAt = clock.GetUtcNow().ToUniversalTime(); Access.Record(db, team, actor, "An invitation was revoked.", clock);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(200, Id: id);
    }
}
