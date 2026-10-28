using System.ComponentModel.DataAnnotations;

namespace DevSync.Web.Features.Account.ResetPassword;

public sealed class Command
{
    public Guid UserId { get; set; }
    [Required, StringLength(4096)]
    public string Token { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12), DataType(DataType.Password), Display(Name = "New password")]
    public string Password { get; set; } = "";
    [Required, Compare(nameof(Password)), DataType(DataType.Password), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";
}
