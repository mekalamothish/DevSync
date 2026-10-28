namespace DevSync.Web.Features.Home.Index;

public sealed record ViewModel(string Heading, string Description, IReadOnlyList<WorkspaceArea> Areas)
{
    public IReadOnlyList<DevSync.Web.Features.Blogs.Explore.ArticleCard> Articles { get; init; } = [];
    public int? Drafts { get; init; }
    public int? Published { get; init; }
    public int? ActiveNotes { get; init; }
    public int? DueReviews { get; init; }
    public int? OpenTasks { get; init; }
    public int? OverdueTasks { get; init; }
}
public sealed record WorkspaceArea(string Number, string Title, string Description, string Label, string? Url = null);
