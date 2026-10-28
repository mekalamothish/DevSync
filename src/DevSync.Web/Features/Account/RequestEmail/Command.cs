using System.ComponentModel.DataAnnotations;

namespace DevSync.Web.Features.Account.RequestEmail;

public sealed class Command
{
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";
}
