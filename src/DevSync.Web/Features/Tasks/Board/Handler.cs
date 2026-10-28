using DevSync.Infrastructure.Persistence;
using DevSync.Web.Features.Tasks.Common;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Tasks.Board;

public sealed class Handler(AppDbContext db, TimeProvider clock)
{
    public async Task<ViewModel?> HandleAsync(Guid ownerId, Guid? boardId, string? search, string? priority, string? state,
        string? assignment, string? due, string? sort, CancellationToken ct)
    {
        var boards = await Browse.BoardsAsync(db, ownerId, ct);
        var board = boardId is null ? boards.FirstOrDefault() : boards.SingleOrDefault(b => b.Id == boardId);
        if (boardId is not null && board is null) return null;
        var filters = Filters.Normalize(search, priority, state, assignment, due, sort, true);
        if (board is null) return new(null, "", Guid.Empty, boards, [], filters.Search, filters.Priority, filters.State, filters.Assignment, filters.Due, filters.Sort, filters.IsFiltered);
        var columns = await db.TaskColumns.AsNoTracking().Where(c => c.BoardId == board.Id && c.OwnerId == ownerId).OrderBy(c => c.Position).ToListAsync(ct);
        var tasks = await Browse.ItemsAsync(db, Browse.Query(db, ownerId, board.Id, filters, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)), ownerId, ct);
        return new(board.Id, board.Name, board.Version, boards, columns.Select(c => new Column(c.Id, c.Name, c.Key, tasks.Where(t => t.ColumnId == c.Id).ToList())).ToList(),
            filters.Search, filters.Priority, filters.State, filters.Assignment, filters.Due, filters.Sort, filters.IsFiltered);
    }
}
