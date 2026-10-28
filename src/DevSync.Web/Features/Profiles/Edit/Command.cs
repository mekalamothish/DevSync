using System.ComponentModel.DataAnnotations;

namespace DevSync.Web.Features.Profiles.Edit;

public sealed class Command
{
    [Required, StringLength(100), Display(Name = "Display name")]
    public string DisplayName { get; set; } = "";
    [Required, StringLength(40, MinimumLength = 3), RegularExpression("^[a-zA-Z][a-zA-Z0-9_]{2,39}$", ErrorMessage = "Use 3–40 letters, numbers, or underscores, starting with a letter.")]
    public string Handle { get; set; } = "";
    [StringLength(1000), Display(Name = "About you")]
    public string? Bio { get; set; }
    public Guid Version { get; set; }
}
