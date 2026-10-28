namespace DevSync.Web.Features.Tasks.Common;

public sealed record TaskItem(Guid Id, Guid Version, Guid BoardId, string BoardName, Guid ColumnId, string ColumnName,
    string ColumnKey, int Position, string Title, string Priority, DateOnly? DueDate, bool AssignedToMe,
    bool IsArchived, DateTimeOffset? CompletedAt, DateTimeOffset CreatedAt, int ChecklistCompleted, int ChecklistTotal);
public sealed record ChecklistItem(Guid Id, string Text, bool IsCompleted, int Position);
public sealed record ActivityItem(Guid Id, string Description, DateTimeOffset CreatedAt);
