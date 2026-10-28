using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DevSync.Web.Features.Notes.Edit;

public sealed record NotebookChoice(Guid Id, string Name);
public sealed record ArticleChoice(Guid Id, string Title, string Url, bool IsDraft);

public sealed class BlockCommand
{
    public Guid Id { get; set; }
    [Required, StringLength(12)]
    public string Kind { get; set; } = "paragraph";
    [StringLength(60000)]
    public string? Content { get; set; } = "";
    [StringLength(30)]
    public string? Language { get; set; }
    public Guid? LinkedPostId { get; set; }
}

public sealed class Command
{
    public Guid Id { get; set; }
    public Guid Version { get; set; }
    [Required, StringLength(160)]
    public string Title { get; set; } = "";
    public Guid? NotebookId { get; set; }
    [StringLength(154)]
    public string? Tags { get; set; }
    [MaxLength(50)]
    public List<BlockCommand> Blocks { get; set; } = [];
    [BindNever] public IReadOnlyList<NotebookChoice> Notebooks { get; set; } = [];
    [BindNever] public IReadOnlyList<ArticleChoice> Articles { get; set; } = [];
    [BindNever] public bool IsArchived { get; set; }
    [BindNever] public DateTimeOffset UpdatedAt { get; set; }
}
