using DevSync.Infrastructure;
using DevSync.Web.Common.Health;
using DevSync.Web.Features.Home.Index;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using DevSync.Web.Common.Authorization;
using DevSync.Web.Features.Account.Common;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DevSync.Web.Common.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var seedRoles = args.Contains("--seed-roles", StringComparer.Ordinal);
var initializeDatabase = args.Contains("--initialize-database", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(argument =>
    argument is not "--seed-roles" and not "--initialize-database").ToArray());
builder.Configuration["ConnectionStrings:DefaultConnection"] = DatabaseConnection.Resolve(builder.Configuration);

// Keep your existing builder and service registrations.

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
        resource.AddService("Devsync.Api"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
        tracing.AddSource("Devsync.Api");
        tracing.AddOtlpExporter();
    });


builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddRazorOptions(options =>
    {
        options.ViewLocationFormats.Insert(0, "/Features/{1}/{0}/View.cshtml");
        options.ViewLocationFormats.Insert(1, "/Features/{1}/{0}.cshtml");
    });

builder.Services.AddHttpClient();

builder.Services.AddScoped<Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Account.Register.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Account.SignIn.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Account.ConfirmEmail.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Account.RequestEmail.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Account.ResetPassword.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Profiles.Edit.Handler>();
builder.Services.AddScoped<AccountMail>();
builder.Services.AddScoped<DevSync.Web.Features.Blogs.Create.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Blogs.Edit.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Blogs.Explore.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Blogs.MyBlogs.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Blogs.Read.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Blogs.Interact.Handler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<DevSync.Web.Features.Revision.Dashboard.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Revision.Create.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Revision.Review.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Notes.List.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Notes.Edit.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Notes.Preview.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Tasks.List.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Tasks.Board.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Tasks.Edit.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Tasks.Details.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Teams.List.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Teams.Manage.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Teams.Invitations.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Teams.Invitations.InvitationMail>();
builder.Services.AddScoped<DevSync.Web.Features.Teams.Workspace.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Settings.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Images.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Profiles.Public.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Verification.Handler>();
builder.Services.AddScoped<DevSync.Web.Features.Moderation.Handler>();
builder.Services.AddAuthorization(Policies.Configure);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method))
            return RateLimitPartition.GetNoLimiter("read");
        // Status-code page re-execution must not charge the same HTTP write twice.
        if (context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>() is not null)
            return RateLimitPartition.GetNoLimiter("reexecute");
        var account = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var partition = account is null ? "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "local") : "user:" + account;
        return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("Writes:RequestsPerMinute", 120),
            Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        });
    });
    options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "local",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("Account:RequestsPerMinute", 30),
            Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection().PersistKeysToFileSystem(
        new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".local", "keys")));
}
builder.ConfigureRelease();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"]);

var app = builder.Build();

if (seedRoles || initializeDatabase)
{
    using var scope = app.Services.CreateScope();
    // Explicit one-shot command, used by Compose before the web service starts.
    // Serving HTTP never applies schema changes automatically.
    if (initializeDatabase)
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<RoleInitializer>().EnsureAsync();
    return;
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/errors/500");
    app.UseHsts();
    app.UseWhen(context => context.Request.Path != "/health/live" && context.Request.Path != "/health/ready",
        branch => branch.UseHttpsRedirection());
}

app.UseStatusCodePagesWithReExecute("/errors/{0}");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapControllers();
app.Run();

public partial class Program;
