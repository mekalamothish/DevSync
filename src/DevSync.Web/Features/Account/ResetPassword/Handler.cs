using DevSync.Infrastructure.Identity;
using DevSync.Web.Features.Account.Common;
using Microsoft.AspNetCore.Identity;

namespace DevSync.Web.Features.Account.ResetPassword;

public sealed class Handler(UserManager<ApplicationUser> users)
{
    public async Task<IReadOnlyList<string>> HandleAsync(Command command)
    {
        var decoded = AccountMail.Decode(command.Token);
        var user = await users.FindByIdAsync(command.UserId.ToString());
        if (decoded is null || user is null) return ["This recovery link is invalid or expired. Request another link."];
        var result = await users.ResetPasswordAsync(user, decoded, command.Password);
        return result.Succeeded ? [] : result.Errors.Select(error => error.Description).ToArray();
    }
}
