using System.ComponentModel.DataAnnotations;

namespace DevSync.Web.Features.Account.Register;

public sealed class Command
{
    [Required, StringLength(100), Display(Name = "Display name")]
    public string DisplayName { get; set; } = "";
    [Required, StringLength(40, MinimumLength = 3), RegularExpression("^[a-zA-Z][a-zA-Z0-9_]{2,39}$", ErrorMessage = "Use 3–40 letters, numbers, or underscores, starting with a letter.")]
    public string Handle { get; set; } = "";
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12), DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [Required, Compare(nameof(Password)), DataType(DataType.Password), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
}
