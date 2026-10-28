namespace DevSync.Domain.Revision;

public sealed class RevisionCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid NoteId { get; set; }
    public required string Prompt { get; set; }
    public required string Answer { get; set; }
    public DateTimeOffset DueAt { get; set; } = DateTimeOffset.UtcNow;
    public int IntervalDays { get; set; }
    public int ReviewCount { get; set; }
    public int LapseCount { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum RevisionGrade { Again, Hard, Good, Easy }

public sealed class RevisionReview
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid CardId { get; init; }
    public Guid OwnerId { get; init; }
    public Guid SubmissionId { get; init; }
    public Guid SubmittedVersion { get; init; }
    public DateTimeOffset ReviewedAt { get; init; }
    public RevisionGrade Grade { get; init; }
    public DateTimeOffset PreviousDueAt { get; init; }
    public DateTimeOffset NextDueAt { get; init; }
    public int IntervalDays { get; init; }
}
