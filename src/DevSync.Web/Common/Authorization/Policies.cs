using DevSync.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;

namespace DevSync.Web.Common.Authorization;

public static class Policies
{
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy("Member", policy => policy.RequireAuthenticatedUser()
            .RequireRole(ApplicationRoles.Member, ApplicationRoles.Moderator, ApplicationRoles.Administrator));
        options.AddPolicy("Moderator", policy => policy.RequireAuthenticatedUser()
            .RequireRole(ApplicationRoles.Moderator, ApplicationRoles.Administrator));
        options.AddPolicy("Administrator", policy => policy.RequireAuthenticatedUser().RequireRole(ApplicationRoles.Administrator));
    }
}
