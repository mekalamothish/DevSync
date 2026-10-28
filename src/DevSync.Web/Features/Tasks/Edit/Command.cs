using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DevSync.Web.Features.Tasks.Edit;

public sealed record BoardChoice(Guid Id, string Name, Guid Version);
public sealed record ColumnChoice(Guid Id, string Name, string Key);
public sealed record LinkChoice(Guid Id, string Title, string Url, bool IsDraft = false);
public sealed class ChecklistCommand
{
    public Guid Id { get; set; }
    [StringLength(300)] public string? Text { get; set; } = "";
    public bool IsCompleted { get; set; }
}
public sealed class Command
{
    public Guid Id { get; set; }
    public Guid BoardId { get; set; }
    public Guid Version { get; set; }
    public Guid BoardVersion { get; set; }
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [StringLength(4000)] public string? Description { get; set; }
    [Required, StringLength(12)] public string Priority { get; set; } = "medium";
    public DateOnly? DueDate { get; set; }
    public bool AssignedToMe { get; set; }
    public Guid ColumnId { get; set; }
    public Guid? LinkedNoteId { get; set; }
    public Guid? LinkedPostId { get; set; }
    [MaxLength(30)] public List<ChecklistCommand> Checklist { get; set; } = [];
    [BindNever] public IReadOnlyList<BoardChoice> Boards { get; set; } = [];
    [BindNever] public IReadOnlyList<ColumnChoice> Columns { get; set; } = [];
    [BindNever] public IReadOnlyList<LinkChoice> Notes { get; set; } = [];
    [BindNever] public IReadOnlyList<LinkChoice> Articles { get; set; } = [];
    [BindNever] public bool IsNew { get; set; }
    [BindNever] public bool IsArchived { get; set; }
    [BindNever] public string BoardName { get; set; } = "";
}
