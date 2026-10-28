using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DevSync.Infrastructure.Email;

public static class EmailDelivery
{
    public static bool UsesSmtp(IConfiguration configuration, IHostEnvironment environment)
    {
        var mode=(configuration["Email:Delivery"] ?? "Auto").Trim().ToLowerInvariant();
        return mode switch
        {
            "auto" => !environment.IsDevelopment(),
            "smtp" => true,
            "file" when environment.IsDevelopment() => false,
            _ => throw new InvalidOperationException("Email:Delivery must be Auto or Smtp. File delivery is allowed only in Development.")
        };
    }
}
