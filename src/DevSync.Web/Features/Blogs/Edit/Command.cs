using System.ComponentModel.DataAnnotations;

namespace DevSync.Web.Features.Blogs.Edit;

public sealed class Command
{
    public Guid Id { get; set; }
    public Guid Version { get; set; }
    [Required, StringLength(160)]
    public string Title { get; set; } = "";
    [StringLength(500)]
    public string? Summary { get; set; }
    [StringLength(60000), Display(Name = "Article content")]
    public string? Markdown { get; set; }
    [Required, StringLength(12)] public string EditorFormat { get; set; } = "markdown";
    [StringLength(60000)] public string? RichText { get; set; }
    [MaxLength(50)] public List<DevSync.Web.Features.Notes.Edit.BlockCommand> Blocks { get; set; } = [];
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever] public IReadOnlyList<DevSync.Web.Features.Notes.Edit.ArticleChoice> Articles { get; set; } = [];
    [StringLength(154)]
    public string? Tags { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever] public bool IsHidden { get; set; }
    public string? PublishedUrl { get; set; }
}
