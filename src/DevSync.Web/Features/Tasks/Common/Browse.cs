using DevSync.Domain.Tasks;
using DevSync.Infrastructure.Persistence;
using DevSync.Web.Features.Tasks.Edit;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Tasks.Common;

public sealed record Filters(string Search, string Priority, string State, string Assignment, string Due, string Sort)
{
    public bool IsFiltered => Search.Length > 0 || Priority != "all" || State != "all" || Assignment != "all" || Due != "all" || Sort != "position";
    public static Filters Normalize(string? search, string? priority, string? state, string? assignment, string? due, string? sort, bool board = false)
    {
        search = (search ?? "").Trim(); search = search[..Math.Min(search.Length, 200)];
        static string Pick(string? value, string[] allowed, string fallback) => allowed.Contains(value) ? value! : fallback;
        return new(search, Pick(priority, ["all", "low", "medium", "high", "urgent"], "all"),
            Pick(state, board ? ["all", "open", "completed"] : ["all", "open", "completed", "archived"], "all"),
            Pick(assignment, ["all", "me", "unassigned"], "all"), Pick(due, ["all", "overdue", "today", "upcoming", "none"], "all"),
            Pick(sort, ["position", "due", "priority", "newest"], "position"));
    }
}

public static class Browse
{
    public static Task<List<BoardChoice>> BoardsAsync(AppDbContext db, Guid ownerId, CancellationToken ct) =>
        db.TaskBoards.AsNoTracking().Where(b => b.OwnerId == ownerId).OrderBy(b => b.CreatedAt).ThenBy(b => b.Id)
            .Select(b => new BoardChoice(b.Id, b.Name, b.Version)).ToListAsync(ct);

    public static IQueryable<WorkTask> Query(AppDbContext db, Guid ownerId, Guid? boardId, Filters filters, DateOnly today)
    {
        var query = db.WorkTasks.AsNoTracking().Where(t => t.OwnerId == ownerId);
        if (boardId is not null) query = query.Where(t => t.BoardId == boardId);
        query = filters.State == "archived" ? query.Where(t => t.IsArchived) : query.Where(t => !t.IsArchived);
        if (filters.State == "open") query = query.Where(t => t.CompletedAt == null);
        if (filters.State == "completed") query = query.Where(t => t.CompletedAt != null);
        if (filters.Priority != "all") query = query.Where(t => t.Priority == filters.Priority);
        if (filters.Assignment == "me") query = query.Where(t => t.AssignedUserId == ownerId);
        if (filters.Assignment == "unassigned") query = query.Where(t => t.AssignedUserId == null);
        if (filters.Due == "overdue") query = query.Where(t => t.DueDate < today && t.CompletedAt == null);
        if (filters.Due == "today") query = query.Where(t => t.DueDate == today);
        if (filters.Due == "upcoming") query = query.Where(t => t.DueDate > today);
        if (filters.Due == "none") query = query.Where(t => t.DueDate == null);
        if (filters.Search.Length > 0)
        {
            var needle = filters.Search.ToLowerInvariant();
            query = query.Where(t => t.Title.ToLower().Contains(needle) || t.Description.ToLower().Contains(needle));
        }
        return filters.Sort switch
        {
            "due" => query.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenBy(t => t.Id),
            "priority" => query.OrderByDescending(t => t.Priority == "urgent" ? 4 : t.Priority == "high" ? 3 : t.Priority == "medium" ? 2 : 1).ThenBy(t => t.Id),
            "newest" => query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id),
            _ => query.OrderBy(t => t.BoardId).ThenBy(t => db.TaskColumns.Where(c => c.Id == t.ColumnId).Select(c => c.Position).First()).ThenBy(t => t.Position).ThenBy(t => t.Id)
        };
    }
    public static async Task<IReadOnlyList<TaskItem>> ItemsAsync(AppDbContext db, IQueryable<WorkTask> query, Guid ownerId, CancellationToken ct) =>
        await (from t in query join b in db.TaskBoards on t.BoardId equals b.Id join c in db.TaskColumns on t.ColumnId equals c.Id
            select new TaskItem(t.Id, t.Version, t.BoardId, b.Name, t.ColumnId, c.Name, c.Key, t.Position, t.Title, t.Priority,
                t.DueDate, t.AssignedUserId == ownerId, t.IsArchived, t.CompletedAt, t.CreatedAt,
                db.TaskChecklistItems.Count(i => i.TaskId == t.Id && i.IsCompleted), db.TaskChecklistItems.Count(i => i.TaskId == t.Id))).ToListAsync(ct);
}
