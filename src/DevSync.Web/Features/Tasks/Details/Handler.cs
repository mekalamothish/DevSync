using DevSync.Infrastructure.Persistence;
using DevSync.Web.Features.Tasks.Common;
using DevSync.Web.Features.Tasks.Edit;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Tasks.Details;

public sealed class Handler(AppDbContext db)
{
    public async Task<ViewModel?> HandleAsync(Guid id, Guid ownerId, int activityPage, CancellationToken ct)
    {
        var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == ownerId, ct);
        if (task is null) return null;
        var board = await db.TaskBoards.AsNoTracking().SingleAsync(b => b.Id == task.BoardId && b.OwnerId == ownerId, ct);
        var columns = await db.TaskColumns.AsNoTracking().Where(c => c.BoardId == board.Id && c.OwnerId == ownerId).OrderBy(c => c.Position)
            .Select(c => new ColumnChoice(c.Id, c.Name, c.Key)).ToListAsync(ct);
        var column = columns.Single(c => c.Id == task.ColumnId);
        var checklist = await db.TaskChecklistItems.AsNoTracking().Where(i => i.TaskId == id).OrderBy(i => i.Position).Select(i => new ChecklistItem(i.Id, i.Text, i.IsCompleted, i.Position)).ToListAsync(ct);
        LinkChoice? note = null; LinkChoice? article = null;
        if (task.LinkedNoteId is not null) note = await db.StudyNotes.AsNoTracking().Where(n => n.Id == task.LinkedNoteId && n.OwnerId == ownerId && !n.IsArchived)
            .Select(n => new LinkChoice(n.Id, n.Title, "/notes/" + n.Id, false)).SingleOrDefaultAsync(ct);
        if (task.LinkedPostId is not null) article = await db.BlogPosts.AsNoTracking().Where(p => p.Id == task.LinkedPostId && (p.AuthorId == ownerId || (p.PublishedAt != null && !p.IsHidden)))
            .Select(p => new LinkChoice(p.Id, p.AuthorId == ownerId ? p.Title : p.PublishedTitle!, p.AuthorId == ownerId ? "/blogs/" + p.Id + "/preview" : "/blogs/" + p.Slug, p.AuthorId == ownerId)).SingleOrDefaultAsync(ct);
        var activity = db.TaskActivities.AsNoTracking().Where(a => a.TaskId == id && a.OwnerId == ownerId);
        var total = await activity.CountAsync(ct); var pages = Math.Max(1, (total + 19) / 20); activityPage = Math.Clamp(activityPage, 1, pages);
        return new(task.Id, task.Version, task.BoardId, board.Name, board.Version, task.ColumnId, column.Name, column.Key, task.Position,
            task.Title, task.Description, task.Priority, task.DueDate, task.AssignedUserId == ownerId, task.IsArchived, task.CompletedAt, task.UpdatedAt,
            columns, checklist, note, article, await activity.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).Skip((activityPage - 1) * 20).Take(20)
                .Select(a => new ActivityItem(a.Id, a.Description, a.CreatedAt)).ToListAsync(ct), activityPage, pages, total);
    }
}
