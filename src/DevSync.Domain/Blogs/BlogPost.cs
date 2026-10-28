namespace DevSync.Domain.Blogs;

public sealed class BlogPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuthorId { get; set; }
    public bool IsHidden { get; set; }
    public string Title { get; set; } = "Untitled article";
    public string Summary { get; set; } = "";
    public string Markdown { get; set; } = "";
    public string EditorFormat { get; set; } = "markdown";
    public string BlocksJson { get; set; } = "[]";
    public string RichText { get; set; } = "";
    public string DraftTags { get; set; } = "";
    public string? Slug { get; set; }
    public string? PublishedTitle { get; set; }
    public string? PublishedSummary { get; set; }
    public string? PublishedMarkdown { get; set; }
    public string PublishedEditorFormat { get; set; } = "markdown";
    public string PublishedBlocksJson { get; set; } = "[]";
    public string PublishedRichText { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid Version { get; set; } = Guid.NewGuid();
}

public sealed class BlogTag
{
    public Guid PostId { get; set; }
    public required string Name { get; set; }
}

public sealed class BlogBookmark
{
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
}

public sealed class BlogReaction
{
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
}

public sealed class BlogComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public required string Body { get; set; }
    public bool IsHidden { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
