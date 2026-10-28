using DevSync.Domain.Teams;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DevSync.Web.Features.Teams.Manage;

public sealed record Member(Guid Id, string Name, string Role);
public sealed record Invitation(Guid Id, string Email, string Role, DateTimeOffset ExpiresAt);
public sealed record Activity(string Description, DateTimeOffset At);
public sealed record ViewModel(Guid Id, string Name, Guid Version, string Role, Member[] Members, Invitation[] Invitations, Activity[] Activities);
public sealed class Handler(AppDbContext db, TimeProvider clock)
{
    public async Task<ViewModel?> LoadAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var team = await (from t in db.Teams.AsNoTracking() join m in db.TeamMemberships.AsNoTracking() on t.Id equals m.TeamId
            where t.Id == id && m.UserId == userId select new { t.Id, t.Name, t.Version, m.Role }).SingleOrDefaultAsync(ct);
        if (team is null) return null;
        var members = await (from m in db.TeamMemberships.AsNoTracking() join p in db.DeveloperProfiles.AsNoTracking() on m.UserId equals p.UserId into profiles
            from p in profiles.DefaultIfEmpty() where m.TeamId == id orderby m.JoinedAt, m.UserId
            select new Member(m.UserId, p == null ? "Team member" : p.DisplayName, m.Role)).ToArrayAsync(ct);
        var invites = Access.Manage(team.Role) ? await db.TeamInvitations.AsNoTracking().Where(i => i.TeamId == id && i.ConsumedAt == null && i.RevokedAt == null && i.ExpiresAt > clock.GetUtcNow())
            .OrderBy(i => i.ExpiresAt).Select(i => new Invitation(i.Id, i.Email, i.Role, i.ExpiresAt)).ToArrayAsync(ct) : [];
        var activity = await db.TeamActivities.AsNoTracking().Where(a => a.TeamId == id).OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Take(30).Select(a => new Activity(a.Description, a.CreatedAt)).ToArrayAsync(ct);
        return new(team.Id, team.Name, team.Version, team.Role, members, invites, activity);
    }
    public async Task<Result> CreateAsync(Guid userId, string? name, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 80) return new(422, "Use a team name of 1–80 characters.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (user is null) return new(404);
        if (await db.TeamMemberships.CountAsync(m => m.UserId == userId, ct) >= 20) return new(422, "You can belong to up to 20 teams.");
        var team = new Team { OwnerId = userId, Name = name, CreatedAt = clock.GetUtcNow().ToUniversalTime() };
        db.Teams.Add(team); db.TeamMemberships.Add(new TeamMembership { TeamId = team.Id, UserId = userId, Role = "owner", JoinedAt = team.CreatedAt });
        Access.Record(db, team, userId, "Team created.", clock);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(200, Id: team.Id);
    }
    public async Task<Result> ChangeAsync(Guid id, Guid actor, Guid version, string operation, Guid? memberId, string? role, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (team, actorRole) = await Access.LockAsync(db, id, actor, ct);
        if (team is null) return new(404);
        if (operation != "leave" && !Access.Manage(actorRole!)) return new(403, "Only a team owner or admin can manage members.");
        if (team.Version != version) return new(409, "This team changed. Reload before trying again.");
        var targetId = operation == "leave" ? actor : memberId;
        var target = await db.TeamMemberships.SingleOrDefaultAsync(m => m.TeamId == id && m.UserId == targetId, ct);
        if (target is null) return new(404);
        if (operation == "transfer")
        {
            if (actorRole != "owner") return new(403, "Only the owner can transfer ownership.");
            if (target.UserId == actor || target.Role == "viewer") return new(422, "Choose another admin or member as owner.");
            var previous = await db.TeamMemberships.SingleAsync(m => m.TeamId == id && m.UserId == actor, ct);
            previous.Role = "admin";
            // Release the filtered unique owner index before assigning the successor.
            await db.SaveChangesAsync(ct);
            target.Role = "owner"; team.OwnerId = target.UserId;
            Access.Record(db, team, actor, "Team ownership transferred.", clock);
        }
        else if (operation is "remove" or "leave")
        {
            if (target.Role == "owner") return new(422, "Transfer ownership before the owner leaves.");
            if (operation == "remove" && (target.UserId == actor || (actorRole != "owner" && target.Role == "admin")))
                return new(403, "Use Leave for yourself; only the owner can remove an admin.");
            db.TeamMemberships.Remove(target);
            var assigned = await db.TeamWorkspaceItems.Where(x => x.TeamId == id && x.AssignedUserId == target.UserId).ToListAsync(ct);
            foreach (var item in assigned) { item.AssignedUserId = null; item.Version = Guid.NewGuid(); item.UpdatedAt = clock.GetUtcNow().ToUniversalTime(); }
            Access.Record(db, team, actor, operation == "leave" ? "A member left the team; their assignments were cleared." : "A member was removed; their assignments were cleared.", clock);
        }
        else if (operation == "role")
        {
            if (target.Role == "owner" || target.UserId == actor || role is not ("admin" or "member" or "viewer")) return new(422, "Choose a valid role for another member.");
            if (actorRole != "owner" && (target.Role == "admin" || role == "admin")) return new(403, "Only the owner can manage admins.");
            target.Role = role;
            if (role == "viewer")
            {
                var assigned = await db.TeamWorkspaceItems.Where(x => x.TeamId == id && x.AssignedUserId == target.UserId).ToListAsync(ct);
                foreach (var item in assigned) { item.AssignedUserId = null; item.Version = Guid.NewGuid(); item.UpdatedAt = clock.GetUtcNow().ToUniversalTime(); }
            }
            Access.Record(db, team, actor, "A member role changed to " + role + ".", clock);
        }
        else return new(422, "Unknown team operation.");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(200, Id: id);
    }
}
