using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DevSync.Web.Features.Teams.List;
public sealed record TeamRow(Guid Id, string Name, string Role, int Members, int Items);
public sealed class Handler(AppDbContext db)
{
    public Task<TeamRow[]> HandleAsync(Guid userId, CancellationToken ct) =>
        (from m in db.TeamMemberships.AsNoTracking() join t in db.Teams.AsNoTracking() on m.TeamId equals t.Id
         where m.UserId == userId orderby t.Name, t.Id
         select new TeamRow(t.Id, t.Name, m.Role, db.TeamMemberships.Count(x => x.TeamId == t.Id),
             db.TeamWorkspaceItems.Count(x => x.TeamId == t.Id && !x.IsArchived))).Take(100).ToArrayAsync(ct);
}
