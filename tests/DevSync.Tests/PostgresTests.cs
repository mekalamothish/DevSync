using DevSync.Domain.Profiles;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DevSync.Tests;

[Collection("PostgreSQL")]
public sealed class PostgresTests
{
    [Fact]
    [Trait("Category", "Database")]
    public async Task Migrations_and_profile_constraints_work_on_postgresql()
    {
        var connectionString = Environment.GetEnvironmentVariable("DEVSYNC_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set DEVSYNC_TEST_CONNECTION to a dedicated test database.");
        if (!new NpgsqlConnectionStringBuilder(connectionString).Database!.EndsWith("_tests", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Database tests require a database name ending in _tests.");
        }
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "foundation-test" };
        var profile = new DeveloperProfile
        {
            UserId = user.Id,
            Handle = "test-" + Guid.NewGuid().ToString("N"),
            DisplayName = "Foundation Test"
        };
        db.Users.Add(user);
        db.DeveloperProfiles.Add(profile);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var persisted = await db.DeveloperProfiles.SingleAsync(item => item.Id == profile.Id);
        Assert.Equal(user.Id, persisted.UserId);
        Assert.Equal("Foundation Test", persisted.DisplayName);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        db.DeveloperProfiles.Add(new DeveloperProfile
        {
            UserId = user.Id,
            Handle = profile.Handle,
            DisplayName = "Duplicate profile"
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await transaction.RollbackAsync();
    }
}
