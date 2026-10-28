namespace DevSync.Domain.StudyNotes;

public sealed class StudyNotebook
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class StudyNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid? NotebookId { get; set; }
    public string Title { get; set; } = "Untitled note";
    public bool IsArchived { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class StudyNoteBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid NoteId { get; set; }
    public int Position { get; set; }
    public string Kind { get; set; } = "paragraph";
    public string Content { get; set; } = "";
    public string? Language { get; set; }
    public Guid? LinkedPostId { get; set; }
}

public sealed class StudyNoteTag
{
    public Guid NoteId { get; set; }
    public string Name { get; set; } = "";
}
