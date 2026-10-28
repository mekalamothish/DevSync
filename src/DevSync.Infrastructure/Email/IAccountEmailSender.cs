namespace DevSync.Infrastructure.Email;

public interface IAccountEmailSender
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken);
}
