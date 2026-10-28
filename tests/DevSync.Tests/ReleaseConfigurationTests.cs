using DevSync.Web.Common.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace DevSync.Tests;

public sealed class ReleaseConfigurationTests
{
    private static Dictionary<string,string?> Valid() => new()
    {
        ["AllowedHosts"] = "app.example.com",
        ["Account:PublicBaseUrl"] = "https://app.example.com",
        ["DataProtection:KeyPath"] = "/var/lib/devsync/keys",
        ["Email:Smtp:Host"] = "smtp.example.com",
        ["Email:From"] = "devsync@example.com"
    };

    [Fact]
    public void Explicit_release_configuration_is_accepted() =>
        ReleaseConfiguration.Validate(new ConfigurationBuilder().AddInMemoryCollection(Valid()).Build());

    [Theory]
    [InlineData("AllowedHosts", "*")]
    [InlineData("DataProtection:KeyPath", "relative/keys")]
    [InlineData("Account:PublicBaseUrl", "http://app.example.com")]
    [InlineData("Account:PublicBaseUrl", "https://other.example.com")]
    [InlineData("Account:PublicBaseUrl", "https://app.example.com/subpath")]
    [InlineData("Account:PublicBaseUrl", "https://user:pass@app.example.com")]
    [InlineData("Email:Smtp:Host", "")]
    public void Invalid_release_configuration_fails_before_serving_requests(string key,string value)
    {
        var settings=Valid();settings[key]=value;
        Assert.Throws<InvalidOperationException>(()=>ReleaseConfiguration.Validate(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));
    }

    [Fact]
    public async Task Production_uses_secure_forms_and_reuses_durable_keys_across_hosts()
    {
        var path=Path.Combine(Path.GetTempPath(),"devsync-release-"+Guid.NewGuid());
        try
        {
            Guid[] keys;
            await using (var first=Production(path))
            {
                using var client=first.CreateClient(new(){BaseAddress=new Uri("https://app.example.com"),AllowAutoRedirect=false});
                var response=await client.GetAsync("/account/sign-in");
                Assert.Equal(HttpStatusCode.OK,response.StatusCode);
                Assert.Contains("__RequestVerificationToken",await response.Content.ReadAsStringAsync());
                Assert.Contains(response.Headers.GetValues("Set-Cookie"),cookie=>cookie.Contains("secure",StringComparison.OrdinalIgnoreCase));
                Assert.True(response.Headers.Contains("Strict-Transport-Security"));
                keys=first.Services.GetRequiredService<IKeyManager>().GetAllKeys().Select(key=>key.KeyId).ToArray();
                Assert.NotEmpty(keys);
            }
            await using var second=Production(path);
            using var next=second.CreateClient(new(){BaseAddress=new Uri("https://app.example.com")});
            Assert.Equal(HttpStatusCode.OK,(await next.GetAsync("/account/sign-in")).StatusCode);
            Assert.Equal(keys.Order(),second.Services.GetRequiredService<IKeyManager>().GetAllKeys().Select(key=>key.KeyId).Order());
        }
        finally { if(Directory.Exists(path))Directory.Delete(path,true); }
    }

    [Fact]
    public async Task Untrusted_forwarded_scheme_cannot_bypass_production_https_redirect()
    {
        var path=Path.Combine(Path.GetTempPath(),"devsync-proxy-"+Guid.NewGuid());
        try
        {
            await using var factory=Production(path);
            using var client=factory.CreateClient(new(){BaseAddress=new Uri("http://app.example.com"),AllowAutoRedirect=false});
            using var request=new HttpRequestMessage(HttpMethod.Get,"/account/sign-in");
            request.Headers.Add("X-Forwarded-Proto","https");
            var response=await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.TemporaryRedirect,response.StatusCode);
            Assert.Equal("https",response.Headers.Location!.Scheme);
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/live")).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync("/health/ready")).StatusCode);
        }
        finally { if(Directory.Exists(path))Directory.Delete(path,true); }
    }

    private static WebApplicationFactory<Program> Production(string path)=>new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder=>
        {
            builder.UseEnvironment("Production");
            var settings=Valid();settings["DataProtection:KeyPath"]=path;
            settings["ConnectionStrings:DefaultConnection"]="";
            builder.ConfigureAppConfiguration((_,configuration)=>configuration.AddInMemoryCollection(settings));
        });
}
