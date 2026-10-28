using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Infrastructure.Identity;

public sealed class RoleInitializer(AppDbContext db)
{
    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in ApplicationRoles.All)
        {
            // The unique normalized-name index makes this safe across concurrent registrations.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
                VALUES ({Guid.NewGuid()}, {role}, {role.ToUpperInvariant()}, {Guid.NewGuid().ToString()})
                ON CONFLICT ("NormalizedName") DO NOTHING
                """, cancellationToken);
        }
    }
}
