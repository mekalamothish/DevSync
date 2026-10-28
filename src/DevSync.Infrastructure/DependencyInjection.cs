using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using DevSync.Infrastructure.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevSync.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Configure ConnectionStrings:DefaultConnection with user secrets or an environment variable.");
            }

            options.UseNpgsql(connectionString);
        });

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.SignIn.RequireConfirmedEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(2));
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.FromMinutes(5));
        services.AddScoped<RoleInitializer>();
        services.AddScoped<IAccountEmailSender>(provider =>
            EmailDelivery.UsesSmtp(configuration,provider.GetRequiredService<IHostEnvironment>())
                ? new SmtpAccountEmailSender(configuration)
                : new FileAccountEmailSender(provider.GetRequiredService<IHostEnvironment>()));

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "DevSync.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.LoginPath = "/account/sign-in";
            options.AccessDeniedPath = "/account/access-denied";
            options.SlidingExpiration = true;
        });

        return services;
    }
}
