using System.ComponentModel.DataAnnotations;

namespace DevSync.Web.Features.Revision.Create;

public sealed class Command
{
    public Guid NoteId { get; set; }
    public string NoteTitle { get; set; } = "";
    [Required, StringLength(500), Display(Name = "Question or prompt")]
    public string Prompt { get; set; } = "";
    [Required, StringLength(4000)]
    public string Answer { get; set; } = "";
}
