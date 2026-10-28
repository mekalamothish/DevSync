namespace DevSync.Domain.Tasks;

public sealed class TaskBoard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = "";
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TaskColumn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BoardId { get; set; }
    public Guid OwnerId { get; set; }
    public string Key { get; set; } = "todo";
    public string Name { get; set; } = "To do";
    public int Position { get; set; }
}

public sealed class WorkTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid BoardId { get; set; }
    public Guid ColumnId { get; set; }
    public int Position { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "medium";
    public DateOnly? DueDate { get; set; }
    public Guid? AssignedUserId { get; set; }
    public Guid? LinkedNoteId { get; set; }
    public Guid? LinkedPostId { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TaskChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public int Position { get; set; }
    public string Text { get; set; } = "";
    public bool IsCompleted { get; set; }
}

public sealed class TaskActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public Guid OwnerId { get; set; }
    public string Description { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
