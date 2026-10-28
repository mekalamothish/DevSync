using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace DevSync.Infrastructure.Email;

public sealed class SmtpAccountEmailSender(IConfiguration configuration) : IAccountEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var host = configuration["Email:Smtp:Host"];
        var sender = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender))
            throw new InvalidOperationException("Configure Email:Smtp:Host and Email:From for production email.");

        using var client = new SmtpClient(host, configuration.GetValue("Email:Smtp:Port", 587))
        {
            EnableSsl = true,
            UseDefaultCredentials = false
        };
        var username = configuration["Email:Smtp:Username"];
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, configuration["Email:Smtp:Password"]);
        using var message = new MailMessage(sender, recipient, subject, body);
        message.From = new MailAddress(sender, configuration["Email:FromName"] ?? "DevSync");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try { await client.SendMailAsync(message, timeout.Token); }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("SMTP delivery timed out."); }
    }
}
