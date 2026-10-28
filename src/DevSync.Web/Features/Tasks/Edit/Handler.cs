using DevSync.Domain.Tasks;
using DevSync.Infrastructure.Persistence;
using DevSync.Web.Features.Tasks.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevSync.Web.Features.Tasks.Edit;

public sealed record Result(int Status, string? Error, Guid? Id = null, Guid Version = default, Guid BoardVersion = default);

public sealed class Handler(AppDbContext db, TimeProvider clock)
{
    private const string Conflict = "This task changed in another tab. Copy your work before reloading the latest version.";
    private const string BoardConflict = "This board changed in another tab. Reload it before moving the task.";
    private static readonly string[] Priorities = ["low", "medium", "high", "urgent"];

    public async Task<Command?> NewAsync(Guid ownerId, Guid? boardId, CancellationToken ct)
    {
        var boards = await Browse.BoardsAsync(db, ownerId, ct);
        var board = boardId is null ? boards.FirstOrDefault() : boards.SingleOrDefault(b => b.Id == boardId);
        if (board is null) return null;
        var command = new Command { IsNew = true, BoardId = board.Id, BoardVersion = board.Version, BoardName = board.Name };
        await PopulateChoicesAsync(command, ownerId, ct);
        command.ColumnId = command.Columns.Single(c => c.Key == "todo").Id;
        return command;
    }
    public async Task<Command?> LoadAsync(Guid id, Guid ownerId, CancellationToken ct)
    {
        var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && t.OwnerId == ownerId, ct);
        if (task is null) return null;
        var command = new Command
        {
            Id = task.Id, BoardId = task.BoardId, Version = task.Version, Title = task.Title, Description = task.Description,
            Priority = task.Priority, DueDate = task.DueDate, AssignedToMe = task.AssignedUserId == ownerId, ColumnId = task.ColumnId,
            LinkedNoteId = task.LinkedNoteId, LinkedPostId = task.LinkedPostId, IsArchived = task.IsArchived,
            Checklist = await db.TaskChecklistItems.AsNoTracking().Where(i => i.TaskId == id).OrderBy(i => i.Position)
                .Select(i => new ChecklistCommand { Id = i.Id, Text = i.Text, IsCompleted = i.IsCompleted }).ToListAsync(ct)
        };
        await PopulateChoicesAsync(command, ownerId, ct);
        return command;
    }
    public async Task PopulateChoicesAsync(Command command, Guid ownerId, CancellationToken ct)
    {
        command.Boards = await Browse.BoardsAsync(db, ownerId, ct);
        var board = command.Boards.SingleOrDefault(b => b.Id == command.BoardId);
        if (board is not null) { command.BoardName = board.Name; command.BoardVersion = board.Version; }
        command.Columns = await db.TaskColumns.AsNoTracking().Where(c => c.BoardId == command.BoardId && c.OwnerId == ownerId)
            .OrderBy(c => c.Position).Select(c => new ColumnChoice(c.Id, c.Name, c.Key)).ToListAsync(ct);
        command.Notes = await db.StudyNotes.AsNoTracking().Where(n => n.OwnerId == ownerId && !n.IsArchived)
            .OrderByDescending(n => n.UpdatedAt).ThenBy(n => n.Id).Take(200).Select(n => new LinkChoice(n.Id, n.Title, "/notes/" + n.Id, false)).ToListAsync(ct);
        if (command.LinkedNoteId is not null && !command.Notes.Any(n => n.Id == command.LinkedNoteId))
        {
            var extra = await db.StudyNotes.AsNoTracking().Where(n => n.Id == command.LinkedNoteId && n.OwnerId == ownerId && !n.IsArchived)
                .Select(n => new LinkChoice(n.Id, n.Title, "/notes/" + n.Id, false)).SingleOrDefaultAsync(ct);
            command.Notes = command.Notes.Append(extra ?? new LinkChoice(command.LinkedNoteId.Value, "Unavailable note — choose no note to unlink", "", false)).ToList();
        }
        command.Articles = await db.BlogPosts.AsNoTracking().Where(p => p.AuthorId == ownerId || (p.PublishedAt != null && !p.IsHidden))
            .OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Id).Take(200)
            .Select(p => new LinkChoice(p.Id, p.AuthorId == ownerId ? p.Title : p.PublishedTitle!, p.AuthorId == ownerId ? "/blogs/" + p.Id + "/preview" : "/blogs/" + p.Slug, p.AuthorId == ownerId)).ToListAsync(ct);
        if (command.LinkedPostId is not null && !command.Articles.Any(p => p.Id == command.LinkedPostId))
        {
            var extra = await db.BlogPosts.AsNoTracking().Where(p => p.Id == command.LinkedPostId && (p.AuthorId == ownerId || (p.PublishedAt != null && !p.IsHidden)))
                .Select(p => new LinkChoice(p.Id, p.AuthorId == ownerId ? p.Title : p.PublishedTitle!, p.AuthorId == ownerId ? "/blogs/" + p.Id + "/preview" : "/blogs/" + p.Slug, p.AuthorId == ownerId)).SingleOrDefaultAsync(ct);
            command.Articles = command.Articles.Append(extra ?? new LinkChoice(command.LinkedPostId.Value, "Unavailable article — choose no article to unlink", "", false)).ToList();
        }
    }
    public async Task<Result> CreateBoardAsync(Guid ownerId, string? name, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (name.Length is < 1 or > 80) return new(422, "Name the board using 1–80 characters.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {ownerId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
        if (user is null) return new(404, "Account not found.");
        if (await db.TaskBoards.CountAsync(b => b.OwnerId == ownerId, ct) >= 20) return new(422, "You can create up to 20 personal boards.");
        var now = Now; var board = new TaskBoard { OwnerId = ownerId, Name = name, CreatedAt = now, UpdatedAt = now };
        db.TaskBoards.Add(board);
        db.TaskColumns.AddRange(new TaskColumn { BoardId = board.Id, OwnerId = ownerId, Key = "todo", Name = "To do", Position = 0 },
            new TaskColumn { BoardId = board.Id, OwnerId = ownerId, Key = "in-progress", Name = "In progress", Position = 1 },
            new TaskColumn { BoardId = board.Id, OwnerId = ownerId, Key = "done", Name = "Done", Position = 2 });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(200, null, board.Id, BoardVersion: board.Version);
    }
    public async Task<Result> CreateAsync(Guid ownerId, Command command, string? operation, Guid? itemId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var board = await LockBoardAsync(command.BoardId, ownerId, ct);
        if (board is null) return new(404, "Board not found.");
        if (await db.WorkTasks.CountAsync(t => t.BoardId == board.Id && !t.IsArchived, ct) >= 200)
            return new(422, "This board has 200 active tasks. Archive a task before adding another.");
        var checklist = command.Checklist.ToList(); var placeholder = ApplyChecklistOperation(checklist, operation, itemId, out var actionError);
        if (actionError is not null) return new(422, actionError);
        var error = await ValidateAsync(command, ownerId, board.Id, checklist, placeholder, ct);
        if (error is not null) return new(422, error);
        var now = Now;
        var column = await db.TaskColumns.SingleAsync(c => c.Id == command.ColumnId && c.BoardId == board.Id && c.OwnerId == ownerId, ct);
        var task = new WorkTask { OwnerId = ownerId, BoardId = board.Id, ColumnId = column.Id, Position = await db.WorkTasks.CountAsync(t => t.ColumnId == column.Id && !t.IsArchived, ct),
            CreatedAt = now, UpdatedAt = now, CompletedAt = column.Key == "done" ? now : null };
        ApplyFields(task, command, ownerId); db.WorkTasks.Add(task);
        var checklistError = await ApplyChecklistAsync(task.Id, checklist, ct);
        if (checklistError is not null) return new(422, checklistError);
        Touch(board); AddActivity(task, "Task created.");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return new(422, "Each checklist item must belong to this task."); }
        await transaction.CommitAsync(ct);
        return new(200, null, task.Id, task.Version, board.Version);
    }
    public async Task<Result> SaveAsync(Guid id, Guid ownerId, Command command, string? operation, Guid? itemId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (board, task) = await LockTaskAsync(id, ownerId, ct);
        if (board is null || task is null) return new(404, "Task not found.");
        if (task.Version != command.Version) return new(409, Conflict, id, task.Version, board.Version);
        if (task.IsArchived) return new(422, "Restore this archived task before editing it.", id, task.Version, board.Version);
        var checklist = command.Checklist.ToList(); var placeholder = ApplyChecklistOperation(checklist, operation, itemId, out var actionError);
        if (actionError is not null) return new(422, actionError);
        var error = await ValidateAsync(command, ownerId, board.Id, checklist, placeholder, ct);
        if (error is not null) return new(422, error, id, task.Version, board.Version);
        var checklistError = await ApplyChecklistAsync(task.Id, checklist, ct);
        if (checklistError is not null) return new(422, checklistError, id, task.Version, board.Version);
        if (task.ColumnId != command.ColumnId) await PlaceAsync(task, command.ColumnId, null, ct);
        ApplyFields(task, command, ownerId); Touch(task); Touch(board); AddActivity(task, "Task updated.");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return new(409, Conflict, id); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { return new(422, "Each checklist item must belong to this task.", id); }
        await transaction.CommitAsync(ct);
        return new(200, null, id, task.Version, board.Version);
    }
    public async Task<Result> MoveAsync(Guid id, Guid ownerId, Guid version, Guid boardVersion, Guid columnId, int? targetPosition, string? direction, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (board, task) = await LockTaskAsync(id, ownerId, ct);
        if (board is null || task is null) return new(404, "Task not found.");
        if (task.Version != version) return new(409, Conflict, id, task.Version, board.Version);
        if (board.Version != boardVersion) return new(409, BoardConflict, id, task.Version, board.Version);
        if (task.IsArchived) return new(422, "Restore this archived task before moving it.", id, task.Version, board.Version);
        if (!await db.TaskColumns.AnyAsync(c => c.Id == columnId && c.BoardId == board.Id && c.OwnerId == ownerId, ct)) return new(404, "Column not found.");
        if (direction is not null)
        {
            if (columnId != task.ColumnId || direction is not ("up" or "down")) return new(422, "Choose a valid reorder action.", id);
            targetPosition = task.Position + (direction == "up" ? -1 : 1);
        }
        var count = await db.WorkTasks.CountAsync(t => t.ColumnId == columnId && !t.IsArchived && t.Id != id, ct);
        if (targetPosition is not null && (targetPosition < 0 || targetPosition > count)) return new(422, "Choose a position inside the destination column.", id);
        var originalColumnId = task.ColumnId;
        await PlaceAsync(task, columnId, targetPosition, ct); Touch(task); Touch(board);
        var columnName = await db.TaskColumns.Where(c => c.Id == columnId).Select(c => c.Name).SingleAsync(ct);
        AddActivity(task, columnId == originalColumnId ? "Task reordered." : "Task moved to " + columnName + ".");
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(200, null, id, task.Version, board.Version);
    }
    public async Task<Result> StateAsync(Guid id, Guid ownerId, Guid version, string operation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (board, task) = await LockTaskAsync(id, ownerId, ct);
        if (board is null || task is null) return new(404, "Task not found.");
        if (task.Version != version) return new(409, Conflict, id, task.Version, board.Version);
        if (operation is "archive" or "restore")
        {
            var archived = operation == "archive";
            if (task.IsArchived == archived) return new(200, null, id, task.Version, board.Version);
            if (!archived && await db.WorkTasks.CountAsync(t => t.BoardId == board.Id && !t.IsArchived, ct) >= 200)
                return new(422, "This board has 200 active tasks. Archive another task before restoring this one.", id);
            if (archived)
            {
                task.IsArchived = true; task.Position = -1;
                var remaining = await db.WorkTasks.Where(t => t.ColumnId == task.ColumnId && !t.IsArchived && t.Id != id).OrderBy(t => t.Position).ThenBy(t => t.Id).ToListAsync(ct);
                Reindex(remaining);
            }
            else
            {
                task.Position = await db.WorkTasks.CountAsync(t => t.ColumnId == task.ColumnId && !t.IsArchived, ct);
                task.IsArchived = false;
            }
            AddActivity(task, archived ? "Task archived." : "Task restored.");
        }
        else
        {
            if (task.IsArchived) return new(422, "Restore this archived task before changing its status.", id);
            var key = operation == "complete" ? "done" : "todo";
            var columnId = await db.TaskColumns.Where(c => c.BoardId == board.Id && c.OwnerId == ownerId && c.Key == key).Select(c => c.Id).SingleAsync(ct);
            await PlaceAsync(task, columnId, null, ct); AddActivity(task, operation == "complete" ? "Task completed." : "Task reopened.");
        }
        Touch(task); Touch(board); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(200, null, id, task.Version, board.Version);
    }
    public async Task<Result> ToggleAsync(Guid id, Guid ownerId, Guid version, Guid itemId, bool completed, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (board, task) = await LockTaskAsync(id, ownerId, ct);
        if (board is null || task is null) return new(404, "Task not found.");
        if (task.Version != version) return new(409, Conflict, id, task.Version, board.Version);
        if (task.IsArchived) return new(422, "Restore this archived task before changing its checklist.", id);
        var item = await db.TaskChecklistItems.SingleOrDefaultAsync(i => i.Id == itemId && i.TaskId == id, ct);
        if (item is null) return new(404, "Checklist item not found.");
        item.IsCompleted = completed; Touch(task); Touch(board); AddActivity(task, completed ? "Checklist item completed." : "Checklist item reopened.");
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(200, null, id, task.Version, board.Version);
    }
    private Task<TaskBoard?> LockBoardAsync(Guid boardId, Guid ownerId, CancellationToken ct) => db.TaskBoards
        .FromSqlInterpolated($"SELECT * FROM \"TaskBoards\" WHERE \"Id\" = {boardId} AND \"OwnerId\" = {ownerId} FOR UPDATE").SingleOrDefaultAsync(ct);
    private async Task<(TaskBoard?, WorkTask?)> LockTaskAsync(Guid id, Guid ownerId, CancellationToken ct)
    {
        var boardId = await db.WorkTasks.AsNoTracking().Where(t => t.Id == id && t.OwnerId == ownerId).Select(t => (Guid?)t.BoardId).SingleOrDefaultAsync(ct);
        if (boardId is null) return (null, null);
        var board = await LockBoardAsync(boardId.Value, ownerId, ct);
        if (board is null) return (null, null);
        var task = await db.WorkTasks.FromSqlInterpolated($"SELECT * FROM \"WorkTasks\" WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE").SingleOrDefaultAsync(ct);
        return (board, task);
    }
    private async Task<string?> ValidateAsync(Command command, Guid ownerId, Guid boardId, List<ChecklistCommand> checklist, Guid? placeholder, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Title) || command.Title.Length > 160 || (command.Description?.Length ?? 0) > 4000)
            return "Use a title between 1 and 160 characters and a description up to 4,000 characters.";
        if (!Priorities.Contains(command.Priority)) return "Choose low, medium, high, or urgent priority.";
        if (!await db.TaskColumns.AnyAsync(c => c.Id == command.ColumnId && c.BoardId == boardId && c.OwnerId == ownerId, ct)) return "Choose a column from this board.";
        if (checklist.Count > 30 || checklist.Any(i => (i.Text?.Length ?? 0) > 300 || (i.Id != placeholder && string.IsNullOrWhiteSpace(i.Text))))
            return "Use up to 30 checklist items, each between 1 and 300 characters.";
        if (command.LinkedNoteId is not null)
        {
            // Keep a chosen note active until this task mutation commits.
            var note = await db.StudyNotes.FromSqlInterpolated($"SELECT * FROM \"StudyNotes\" WHERE \"Id\" = {command.LinkedNoteId.Value} AND \"OwnerId\" = {ownerId} AND NOT \"IsArchived\" FOR SHARE").AsNoTracking().SingleOrDefaultAsync(ct);
            if (note is null) return "Link one of your active personal notes.";
        }
        if (command.LinkedPostId is not null && !await db.BlogPosts.AnyAsync(p => p.Id == command.LinkedPostId && (p.AuthorId == ownerId || (p.PublishedAt != null && !p.IsHidden)), ct))
            return "Link a published article or one of your own drafts.";
        return null;
    }
    private static Guid? ApplyChecklistOperation(List<ChecklistCommand> items, string? operation, Guid? itemId, out string? error)
    {
        error = null;
        if (operation is null) return null;
        var index = items.FindIndex(i => i.Id == itemId);
        switch (operation)
        {
            case "add" when items.Count < 30:
                var id = Guid.NewGuid(); items.Add(new ChecklistCommand { Id = id }); return id;
            case "remove" when index >= 0: items.RemoveAt(index); return null;
            case "up" when index > 0: (items[index - 1], items[index]) = (items[index], items[index - 1]); return null;
            case "down" when index >= 0 && index + 1 < items.Count: (items[index + 1], items[index]) = (items[index], items[index + 1]); return null;
            default: error = "Choose a valid checklist action. Tasks can contain up to 30 items."; return null;
        }
    }
    private async Task<string?> ApplyChecklistAsync(Guid taskId, List<ChecklistCommand> checklist, CancellationToken ct)
    {
        var existing = await db.TaskChecklistItems.Where(i => i.TaskId == taskId).ToListAsync(ct);
        foreach (var item in checklist.Where(i => i.Id == Guid.Empty)) item.Id = Guid.NewGuid();
        var ids = checklist.Select(i => i.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length || await db.TaskChecklistItems.AnyAsync(i => i.TaskId != taskId && ids.Contains(i.Id), ct))
            return "Each checklist item must belong to this task and appear only once.";
        db.TaskChecklistItems.RemoveRange(existing.Where(i => !ids.Contains(i.Id)));
        for (var index = 0; index < checklist.Count; index++)
        {
            var source = checklist[index]; var item = existing.SingleOrDefault(i => i.Id == source.Id);
            if (item is null) { item = new TaskChecklistItem { Id = source.Id, TaskId = taskId }; db.TaskChecklistItems.Add(item); }
            item.Text = source.Text?.Trim() ?? ""; item.IsCompleted = source.IsCompleted; item.Position = index;
        }
        return null;
    }
    private async Task PlaceAsync(WorkTask task, Guid columnId, int? position, CancellationToken ct)
    {
        var oldColumnId = task.ColumnId;
        var source = await db.WorkTasks.Where(t => t.BoardId == task.BoardId && t.ColumnId == oldColumnId && !t.IsArchived && t.Id != task.Id)
            .OrderBy(t => t.Position).ThenBy(t => t.Id).ToListAsync(ct);
        var destination = oldColumnId == columnId ? source : await db.WorkTasks.Where(t => t.BoardId == task.BoardId && t.ColumnId == columnId && !t.IsArchived && t.Id != task.Id)
            .OrderBy(t => t.Position).ThenBy(t => t.Id).ToListAsync(ct);
        destination.Insert(position ?? destination.Count, task); task.ColumnId = columnId;
        if (oldColumnId != columnId) Reindex(source);
        Reindex(destination);
        var done = await db.TaskColumns.Where(c => c.Id == columnId).Select(c => c.Key == "done").SingleAsync(ct);
        task.CompletedAt = done ? task.CompletedAt ?? Now : null;
    }
    private void Reindex(List<WorkTask> tasks)
    {
        for (var i = 0; i < tasks.Count; i++) if (tasks[i].Position != i) { tasks[i].Position = i; Touch(tasks[i]); }
    }
    private DateTimeOffset Now => clock.GetUtcNow().ToUniversalTime();
    private void Touch(WorkTask task) { task.Version = Guid.NewGuid(); task.UpdatedAt = Now; }
    private void Touch(TaskBoard board) { board.Version = Guid.NewGuid(); board.UpdatedAt = Now; }
    private void AddActivity(WorkTask task, string description) => db.TaskActivities.Add(new TaskActivity { TaskId = task.Id, OwnerId = task.OwnerId, Description = description, CreatedAt = Now });
    private static void ApplyFields(WorkTask task, Command command, Guid ownerId)
    {
        task.Title = command.Title.Trim(); task.Description = command.Description?.Trim() ?? ""; task.Priority = command.Priority;
        task.DueDate = command.DueDate; task.AssignedUserId = command.AssignedToMe ? ownerId : null;
        task.LinkedNoteId = command.LinkedNoteId; task.LinkedPostId = command.LinkedPostId;
    }
}
