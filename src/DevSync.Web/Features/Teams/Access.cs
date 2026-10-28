using DevSync.Domain.Teams;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Teams;

public sealed record Result(int Status, string? Error = null, Guid? Id = null, string? Token = null);
public static class Access
{
    public static bool Manage(string role) => role is "owner" or "admin";
    public static bool Write(string role) => role is "owner" or "admin" or "member";
    public static async Task<(Team? Team, string? Role)> LockAsync(AppDbContext db, Guid teamId, Guid userId, CancellationToken ct)
    {
        var team = await db.Teams.FromSqlInterpolated($"SELECT * FROM \"Teams\" WHERE \"Id\" = {teamId} FOR UPDATE").SingleOrDefaultAsync(ct);
        var role = team is null ? null : await db.TeamMemberships.Where(m => m.TeamId == teamId && m.UserId == userId).Select(m => m.Role).SingleOrDefaultAsync(ct);
        return role is null ? (null, null) : (team, role);
    }
    public static void Record(AppDbContext db, Team team, Guid actorId, string description, TimeProvider clock)
    {
        team.Version = Guid.NewGuid();
        db.TeamActivities.Add(new TeamActivity { TeamId = team.Id, ActorId = actorId, Description = description,
            CreatedAt = clock.GetUtcNow().ToUniversalTime() });
    }
}
