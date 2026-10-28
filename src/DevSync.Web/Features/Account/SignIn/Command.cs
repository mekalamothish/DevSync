using System.ComponentModel.DataAnnotations;

namespace DevSync.Web.Features.Account.SignIn;

public sealed class Command
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
    [Required, StringLength(128), DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
