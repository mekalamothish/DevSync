using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using DevSync.Infrastructure.Email;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using System.Security.Claims;

namespace DevSync.Tests;

[CollectionDefinition("PostgreSQL", DisableParallelization = true)]
public sealed class PostgreSqlCollection;

public sealed record CapturedMail(string Recipient, string Subject, string Body);

public sealed class CaptureMailbox : IAccountEmailSender
{
    public ConcurrentQueue<CapturedMail> Messages { get; } = new();
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        Messages.Enqueue(new(recipient, subject, body));
        return Task.CompletedTask;
    }
}

public sealed class AccountFactory(string connectionString, CaptureMailbox mailbox) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString,
            ["Account:PublicBaseUrl"] = "http://localhost:5180/",
            ["Account:RequestsPerMinute"] = "1000"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAccountEmailSender>();
            services.AddSingleton<IAccountEmailSender>(mailbox);
            services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        });
    }
}

public sealed class AccountFixture : IAsyncLifetime
{
    public string RunId { get; } = "run" + Guid.NewGuid().ToString("N")[..10];
    public CaptureMailbox Mailbox { get; } = new();
    public AccountFactory Factory { get; }

    public AccountFixture()
    {
        var connection = Environment.GetEnvironmentVariable("DEVSYNC_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set DEVSYNC_TEST_CONNECTION to a dedicated test database.");
        if (!new NpgsqlConnectionStringBuilder(connection).Database!.EndsWith("_tests", StringComparison.Ordinal))
            throw new InvalidOperationException("Account tests require a database ending in _tests.");
        Factory = new AccountFactory(connection, Mailbox);
    }

    public async Task InitializeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<RoleInitializer>().EnsureAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            using var scope = Factory.Services.CreateScope();
            var cleanupDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cleanupIds = await cleanupDb.Users.Where(user => user.Email != null && user.Email.StartsWith(RunId) && user.Email.EndsWith("@devsync-test.invalid")).Select(user => user.Id).ToArrayAsync();
            await cleanupDb.GovernanceAudits.Where(a => a.ActorId != null && cleanupIds.Contains(a.ActorId.Value)).ExecuteDeleteAsync();
            await cleanupDb.Users
                .Where(user => user.Email != null && user.Email.StartsWith(RunId) && user.Email.EndsWith("@devsync-test.invalid"))
                .ExecuteDeleteAsync();
        }
        finally { await Factory.DisposeAsync(); }
    }

    public HttpClient Client() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    public string Email(string suffix) => $"{RunId}-{suffix}@devsync-test.invalid";
}

[Collection("PostgreSQL")]
[Trait("Category", "Database")]
public sealed class AccountFlowTests(AccountFixture fixture) : IClassFixture<AccountFixture>
{
    private const string Password = "Initial-Test-Pass42!";

    private static string Field(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success, $"Missing form field: {name}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> FormAsync(HttpClient client, string getPath, string postPath,
        Dictionary<string, string> fields)
    {
        var form = await client.GetAsync(getPath);
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        fields["__RequestVerificationToken"] = Field(await form.Content.ReadAsStringAsync(), "__RequestVerificationToken");
        return await client.PostAsync(postPath, new FormUrlEncodedContent(fields));
    }

    private async Task RegisterAsync(HttpClient client, string suffix, string? handle = null)
    {
        var response = await FormAsync(client, "/account/register", "/account/register", new()
        {
            ["DisplayName"] = "Developer " + suffix,
            ["Handle"] = handle ?? "Dev_" + fixture.RunId + "_" + suffix,
            ["Email"] = fixture.Email(suffix), ["Password"] = Password, ["ConfirmPassword"] = Password
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/check-email", response.Headers.Location!.OriginalString);
    }

    private string MailLink(string email, string subject)
    {
        var mail = fixture.Mailbox.Messages.Last(item => item.Recipient == email && item.Subject == subject);
        return new Uri(Regex.Match(mail.Body, "http[^\\s]+").Value).PathAndQuery;
    }

    private async Task ConfirmAsync(HttpClient client, string suffix)
    {
        var response = await client.GetAsync(MailLink(fixture.Email(suffix), "Verify your DevSync email"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Email verified", await response.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> SignInAsync(HttpClient client, string suffix, string password = Password, string returnUrl = "/") =>
        FormAsync(client, "/account/sign-in", "/account/sign-in", new()
        {
            ["Email"] = fixture.Email(suffix), ["Password"] = password, ["ReturnUrl"] = returnUrl
        });

    [Fact]
    public async Task Registration_confirmation_profile_edit_conflict_and_sign_out_work_end_to_end()
    {
        using var client = fixture.Client();
        await RegisterAsync(client, "alice");
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "alice")).StatusCode);
        await ConfirmAsync(client, "alice");
        var signedIn = await SignInAsync(client, "alice", returnUrl: "https://attacker.invalid/");
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal("/", signedIn.Headers.Location!.OriginalString);
        Assert.Contains(signedIn.Headers.GetValues("Set-Cookie"), value => value.StartsWith("DevSync.Session=") && value.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        var profile = await client.GetStringAsync("/account/profile");
        var version = Field(profile, "Version");
        var fields = new Dictionary<string, string>
        {
            ["DisplayName"] = "Alice Updated", ["Handle"] = "ALICE_" + fixture.RunId,
            ["Bio"] = "<script>plain text</script>", ["Version"] = version,
            ["UserId"] = Guid.NewGuid().ToString()
        };
        var saved = await FormAsync(client, "/account/profile", "/account/profile", fields);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var stale = await FormAsync(client, "/account/profile", "/account/profile", fields);
        Assert.Contains("changed in another session", await stale.Content.ReadAsStringAsync());
        var updated = await client.GetStringAsync("/account/profile");
        Assert.Contains("Alice Updated", updated);
        Assert.Contains("&lt;script&gt;", updated);
        Assert.DoesNotContain("<script>plain text</script>", updated);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(item => item.Email == fixture.Email("alice"));
            var persisted = await db.DeveloperProfiles.SingleAsync(item => item.UserId == user.Id);
            Assert.Equal(("ALICE_" + fixture.RunId).ToLowerInvariant(), persisted.Handle);
            Assert.Equal("Alice Updated", persisted.DisplayName);
            Assert.Contains(ApplicationRoles.Member, await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().GetRolesAsync(user));
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/account/sign-out", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.GetAsync("/account/sign-out")).StatusCode);
        var signedOut = await FormAsync(client, "/", "/account/sign-out", new());
        Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
        var denied = await client.GetAsync("/account/profile");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Equal("/account/sign-in", denied.Headers.Location!.AbsolutePath);
        Assert.Equal("localhost", denied.Headers.Location.Host);
    }

    [Fact]
    public async Task Recovery_is_generic_token_is_single_use_and_password_reset_revokes_session()
    {
        using var client = fixture.Client();
        await RegisterAsync(client, "bob");
        await ConfirmAsync(client, "bob");
        Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(client, "bob")).StatusCode);
        var known = await FormAsync(client, "/account/forgot-password", "/account/forgot-password", new() { ["Email"] = fixture.Email("bob") });
        var count = fixture.Mailbox.Messages.Count;
        var unknown = await FormAsync(client, "/account/forgot-password", "/account/forgot-password", new() { ["Email"] = fixture.Email("missing") });
        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(known.Headers.Location, unknown.Headers.Location);
        Assert.Equal(count, fixture.Mailbox.Messages.Count);
        var link = MailLink(fixture.Email("bob"), "Reset your DevSync password");
        var form = await client.GetStringAsync(link);
        var fields = new Dictionary<string, string>
        {
            ["UserId"] = Field(form, "UserId"), ["Token"] = Field(form, "Token"),
            ["Password"] = "New-Test-Password73!", ["ConfirmPassword"] = "New-Test-Password73!"
        };
        var changed = await FormAsync(client, link, "/account/reset-password", fields);
        Assert.Contains("Password updated", await changed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/account/profile")).StatusCode);
        var replay = await FormAsync(client, link, "/account/reset-password", fields);
        Assert.DoesNotContain("Password updated", await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "bob", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await SignInAsync(client, "bob", "New-Test-Password73!")).StatusCode);
    }

    [Fact]
    public async Task Case_insensitive_handles_are_unique_and_failed_registration_leaves_no_user()
    {
        using var client = fixture.Client();
        var handle = "Unique_" + fixture.RunId;
        await RegisterAsync(client, "unique", handle);
        var duplicate = await FormAsync(client, "/account/register", "/account/register", new()
        {
            ["DisplayName"] = "Duplicate", ["Handle"] = handle.ToLowerInvariant(),
            ["Email"] = fixture.Email("duplicate"), ["Password"] = Password, ["ConfirmPassword"] = Password
        });
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Contains("already in use", await duplicate.Content.ReadAsStringAsync());
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(user => user.Email == fixture.Email("duplicate")));
    }

    [Fact]
    public async Task Five_failed_passwords_lock_account_and_member_cannot_moderate()
    {
        using var client = fixture.Client();
        await RegisterAsync(client, "lockout");
        await ConfirmAsync(client, "lockout");
        for (var index = 0; index < 5; index++)
            Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "lockout", "Wrong-Password-42!")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "lockout")).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(fixture.Email("lockout"));
        Assert.True(user!.LockoutEnd > DateTimeOffset.UtcNow);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, ApplicationRoles.Member)], "test"));
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        Assert.True((await authorization.AuthorizeAsync(principal, "Member")).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(principal, "Moderator")).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(principal, "Administrator")).Succeeded);
        await scope.ServiceProvider.GetRequiredService<RoleInitializer>().EnsureAsync();
        Assert.Equal(3, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Roles.CountAsync(role => ApplicationRoles.All.Contains(role.Name!)));
    }

    [Fact]
    public async Task Invalid_identity_password_creates_no_user_and_is_not_echoed_in_html()
    {
        using var client = fixture.Client();
        const string invalidPassword = "lowercasepasswordonly";
        var response = await FormAsync(client, "/account/register", "/account/register", new()
        {
            ["DisplayName"] = "Invalid Password", ["Handle"] = "invalid_" + fixture.RunId,
            ["Email"] = fixture.Email("invalid"), ["Password"] = invalidPassword, ["ConfirmPassword"] = invalidPassword
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(invalidPassword, await response.Content.ReadAsStringAsync());
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(user => user.Email == fixture.Email("invalid")));
    }

    [Fact]
    public async Task Duplicate_normalized_email_and_invalid_confirmation_are_rejected()
    {
        using var client = fixture.Client();
        await RegisterAsync(client, "email");
        var response = await FormAsync(client, "/account/register", "/account/register", new()
        {
            ["DisplayName"] = "Duplicate Email", ["Handle"] = "other_" + fixture.RunId,
            ["Email"] = fixture.Email("email").ToUpperInvariant(), ["Password"] = Password, ["ConfirmPassword"] = Password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(user => user.NormalizedEmail == fixture.Email("email").ToUpperInvariant()));
        Assert.False(await db.DeveloperProfiles.AnyAsync(profile => profile.Handle == "other_" + fixture.RunId));
        var invalid = await client.GetAsync("/account/confirm-email?userId=" + Guid.NewGuid() + "&token=invalid!");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("Unable to verify email", await invalid.Content.ReadAsStringAsync());
    }
}
