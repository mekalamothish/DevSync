using DevSync.Infrastructure.Persistence;
using DevSync.Web.Features.Tasks.Common;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Tasks.List;

public sealed class Handler(AppDbContext db, TimeProvider clock)
{
    public async Task<ViewModel?> HandleAsync(Guid ownerId, Guid? boardId, string? search, string? priority, string? state,
        string? assignment, string? due, string? sort, int page, CancellationToken ct)
    {
        var boards = await Browse.BoardsAsync(db, ownerId, ct);
        if (boardId is not null && !boards.Any(b => b.Id == boardId)) return null;
        var filters = Filters.Normalize(search, priority, state, assignment, due, sort);
        var query = Browse.Query(db, ownerId, boardId, filters, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        var total = await query.CountAsync(ct); var pages = Math.Max(1, (total + 19) / 20); page = Math.Clamp(page, 1, pages);
        var all = db.WorkTasks.AsNoTracking().Where(t => t.OwnerId == ownerId && (boardId == null || t.BoardId == boardId));
        return new(await Browse.ItemsAsync(db, query.Skip((page - 1) * 20).Take(20), ownerId, ct), boards, page, pages, total,
            await all.CountAsync(t => !t.IsArchived && t.CompletedAt == null, ct), await all.CountAsync(t => !t.IsArchived && t.CompletedAt != null, ct),
            await all.CountAsync(t => t.IsArchived, ct), boardId, filters.Search, filters.Priority, filters.State, filters.Assignment, filters.Due, filters.Sort);
    }
}
