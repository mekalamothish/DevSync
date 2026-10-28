using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevSync.Web.Features.Profiles.Edit;

public sealed class Handler(AppDbContext db)
{
    public async Task<Command?> LoadAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.DeveloperProfiles.AsNoTracking().Where(profile => profile.UserId == userId)
            .Select(profile => new Command
            {
                DisplayName = profile.DisplayName, Handle = profile.Handle, Bio = profile.Bio, Version = profile.Version
            }).SingleOrDefaultAsync(cancellationToken);

    public async Task<string?> HandleAsync(Guid userId, Command command, CancellationToken cancellationToken)
    {
        var profile = await db.DeveloperProfiles.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (profile is null) return "Your profile could not be found.";
        if (profile.Version != command.Version) return "Your profile changed in another session. Review your changes before saving again.";
        var handle = command.Handle.Trim().ToLowerInvariant();
        if (await db.DeveloperProfiles.AnyAsync(item => item.Handle == handle && item.UserId != userId, cancellationToken))
            return "This handle is already in use.";
        profile.DisplayName = command.DisplayName.Trim();
        profile.Handle = handle;
        profile.Bio = command.Bio?.Trim();
        profile.Version = Guid.NewGuid();
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return "Your profile changed in another session. Review your changes before saving again."; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return "This handle is already in use."; }
        return null;
    }
}
