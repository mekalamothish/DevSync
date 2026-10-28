using DevSync.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace DevSync.Web.Features.Account.SignIn;

public sealed class Handler(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn)
{
    public async Task<bool> HandleAsync(Command command)
    {
        var user = await users.FindByEmailAsync(command.Email.Trim());
        if (user is null) return false;
        return (await signIn.PasswordSignInAsync(user, command.Password, command.RememberMe, lockoutOnFailure: true)).Succeeded;
    }
}
