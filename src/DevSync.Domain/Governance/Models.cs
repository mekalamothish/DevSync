namespace DevSync.Domain.Governance;

public sealed class AccountPreferences
{
    public Guid UserId { get; set; }
    public bool PublicProfile { get; set; }
    public bool ShowBio { get; set; }
    public bool ReduceMotion { get; set; }
    public int ArticlesPageSize { get; set; } = 12;
    public Guid Version { get; set; } = Guid.NewGuid();
}
public sealed class PublisherVerification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string EvidenceUrl { get; set; } = "";
    public string Statement { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string DecisionReason { get; set; } = "";
    public Guid? ReviewerId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
public sealed class ContentReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterId { get; set; }
    public Guid PostId { get; set; }
    public Guid? CommentId { get; set; }
    public Guid TargetId { get; set; }
    public string Kind { get; set; } = "post";
    public string Category { get; set; } = "other";
    public string Details { get; set; } = "";
    public string Excerpt { get; set; } = "";
    public string Status { get; set; } = "pending";
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class GovernanceAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public string Kind { get; set; } = "";
    public Guid TargetId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
