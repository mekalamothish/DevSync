using DevSync.Infrastructure.Identity;
using DevSync.Web.Features.Account.Common;
using Microsoft.AspNetCore.Identity;

namespace DevSync.Web.Features.Account.RequestEmail;

public sealed class Handler(UserManager<ApplicationUser> users, AccountMail mail)
{
    public async Task HandleAsync(Command command, bool resetPassword, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(command.Email.Trim());
        if (user is not null && user.EmailConfirmed == resetPassword)
            await mail.SendAsync(user, resetPassword, cancellationToken);
    }
}
