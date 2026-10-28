using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace DevSync.Tests;

public sealed class WebApplicationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public WebApplicationTests(WebApplicationFactory<Program> factory)
    {
        client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = "" }));
        }).CreateClient();
    }

    [Fact]
    public async Task Home_resolves_feature_view_and_shared_layout_without_a_database()
    {
        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Your next idea starts here.", html);
        Assert.Contains("Workspace navigation", html);
        Assert.Contains("/css/site.css", html);
    }

    [Fact]
    public async Task Liveness_does_not_require_database_connectivity()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task Readiness_returns_unavailable_when_database_is_unconfigured()
    {
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("DefaultConnection", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_route_renders_error_view_and_preserves_404()
    {
        var response = await client.GetAsync("/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Page not found", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/account/sign-in")]
    [InlineData("/account/register")]
    [InlineData("/account/forgot-password")]
    [InlineData("/account/resend-verification")]
    public async Task Public_account_forms_render_without_database_access(string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("__RequestVerificationToken", await response.Content.ReadAsStringAsync());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Account_write_without_antiforgery_token_is_rejected_before_database_access()
    {
        var response = await client.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Account_request_limit_returns_429_after_allowance_is_used()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Account:RequestsPerMinute"] = "1", ["ConnectionStrings:DefaultConnection"] = ""
            }));
        });
        using var limitedClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await limitedClient.GetAsync("/account/sign-in")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await limitedClient.GetAsync("/account/register")).StatusCode);
    }

    [Fact]
    public async Task Mutation_limit_rejects_excess_writes_without_blocking_reads()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Writes:RequestsPerMinute"] = "1", ["ConnectionStrings:DefaultConnection"] = ""
            }));
        });
        using var limitedClient = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await limitedClient.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await limitedClient.PostAsync("/account/register", new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await limitedClient.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await limitedClient.GetAsync("/health/live")).StatusCode);
    }
}
