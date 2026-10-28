using DevSync.Infrastructure.Identity;
using DevSync.Web.Features.Account.Common;
using Microsoft.AspNetCore.Identity;

namespace DevSync.Web.Features.Account.ConfirmEmail;

public sealed class Handler(UserManager<ApplicationUser> users)
{
    public async Task<bool> HandleAsync(Guid userId, string token)
    {
        var decoded = AccountMail.Decode(token);
        if (decoded is null) return false;
        var user = await users.FindByIdAsync(userId.ToString());
        return user is not null && (await users.ConfirmEmailAsync(user, decoded)).Succeeded;
    }
}
