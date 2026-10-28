using System.Text;
using DevSync.Infrastructure.Email;
using DevSync.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace DevSync.Web.Features.Account.Common;

public sealed class AccountMail(UserManager<ApplicationUser> users, IAccountEmailSender sender,
    IConfiguration configuration, IHostEnvironment environment, ILogger<AccountMail> logger)
{
    public async Task SendAsync(ApplicationUser user, bool resetPassword, CancellationToken cancellationToken)
    {
        var origin = configuration["Account:PublicBaseUrl"];
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != "https" && !(environment.IsDevelopment() && baseUri.Scheme == "http")) ||
            !string.IsNullOrEmpty(baseUri.UserInfo) || !string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
            throw new InvalidOperationException("Configure Account:PublicBaseUrl with an HTTPS application URL.");

        var token = resetPassword ? await users.GeneratePasswordResetTokenAsync(user)
            : await users.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var path = resetPassword ? "account/reset-password" : "account/confirm-email";
        var link = new Uri(baseUri, path) + $"?userId={user.Id}&token={encoded}";
        var subject = resetPassword ? "Reset your DevSync password" : "Verify your DevSync email";
        try
        {
            await sender.SendAsync(user.Email!, subject,
                $"{subject}\n\nOpen this link within two hours:\n{link}\n\nIf you didn't request this, you can ignore this message.", cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Never log tokens, email bodies, or SMTP credentials.
            logger.LogWarning("Account email delivery failed for user {UserId}; delivery type {FailureType}.",
                user.Id, exception.GetType().Name);
        }
    }

    public static string? Decode(string token)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)); }
        catch (FormatException) { return null; }
    }
}
