using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.HttpOverrides;

namespace DevSync.Web.Common.Hosting;

public static class ReleaseConfiguration
{
    public static void ConfigureRelease(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        if (!builder.Environment.IsDevelopment())
        {
            builder.Services.AddHostedService<ReleaseValidation>();
            builder.Services.AddDataProtection().SetApplicationName("DevSync");
            builder.Services.AddOptions<KeyManagementOptions>().Configure<IConfiguration, ILoggerFactory>((options, current, logger) =>
            {
                Validate(current);
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(current["DataProtection:KeyPath"]!), logger);
            });
            builder.Services.ConfigureApplicationCookie(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
            builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
            builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
        }

        builder.Services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((options, current) =>
        {
            var proxies = current.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
            if (proxies.Any(proxy => !IPAddress.TryParse(proxy, out _)))
                throw new InvalidOperationException("ReverseProxy:KnownProxies must contain IP addresses.");
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            foreach (var proxy in proxies) options.KnownProxies.Add(IPAddress.Parse(proxy));
            // No configured proxy means forwarded headers are disabled, not globally trusted.
            if (proxies.Length == 0) options.ForwardedHeaders = ForwardedHeaders.None;
            options.ForwardLimit = 1;
        });
    }

    private sealed class ReleaseValidation(IConfiguration configuration) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) { Validate(configuration); return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public static void Validate(IConfiguration configuration)
    {
        if (!Path.IsPathFullyQualified(configuration["DataProtection:KeyPath"] ?? ""))
            throw new InvalidOperationException("Configure DataProtection:KeyPath as an absolute durable directory.");
        var hosts = configuration["AllowedHosts"]?.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (hosts is null || hosts.Length == 0 || hosts.Any(host => host.Contains('*')))
            throw new InvalidOperationException("Configure explicit AllowedHosts for production.");
        if (!Uri.TryCreate(configuration["Account:PublicBaseUrl"], UriKind.Absolute, out var origin)
            || origin.Scheme != "https" || origin.UserInfo.Length != 0 || origin.Query.Length != 0
            || origin.Fragment.Length != 0 || origin.AbsolutePath != "/"
            || !hosts.Contains(origin.Host, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Account:PublicBaseUrl must be an HTTPS origin included in AllowedHosts.");
        if (string.IsNullOrWhiteSpace(configuration["Email:Smtp:Host"]) || string.IsNullOrWhiteSpace(configuration["Email:From"]))
            throw new InvalidOperationException("Configure Email:Smtp:Host and Email:From for production.");
    }
}
