using DevSync.Domain.Profiles;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using DevSync.Web.Features.Account.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevSync.Web.Features.Account.Register;

public sealed class Handler(AppDbContext db, UserManager<ApplicationUser> users, RoleInitializer roles, AccountMail mail)
{
    public async Task<IReadOnlyList<string>> HandleAsync(Command command, CancellationToken cancellationToken)
    {
        var handle = command.Handle.Trim().ToLowerInvariant();
        if (await db.DeveloperProfiles.AnyAsync(profile => profile.Handle == handle, cancellationToken))
            return ["This handle is already in use."];
        await roles.EnsureAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser { UserName = command.Email.Trim(), Email = command.Email.Trim() };
        try
        {
            var created = await users.CreateAsync(user, command.Password);
            if (!created.Succeeded) return created.Errors.Select(error => error.Description).ToArray();
            var assigned = await users.AddToRoleAsync(user, ApplicationRoles.Member);
            if (!assigned.Succeeded) return ["Unable to finish creating your account. Please try again."];
            db.DeveloperProfiles.Add(new DeveloperProfile
            {
                UserId = user.Id, Handle = handle, DisplayName = command.DisplayName.Trim()
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ["An account with these details already exists. Try another handle or sign in."];
        }
        await mail.SendAsync(user, false, cancellationToken);
        return [];
    }
}
