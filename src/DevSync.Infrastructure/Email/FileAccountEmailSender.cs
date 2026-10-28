using System.Net.Mail;
using Microsoft.Extensions.Hosting;

namespace DevSync.Infrastructure.Email;

public sealed class FileAccountEmailSender(IHostEnvironment environment) : IAccountEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(environment.ContentRootPath, ".local", "mail");
        Directory.CreateDirectory(directory);
        var address = new MailAddress(recipient).Address;
        var content = $"To: {address}\r\nSubject: {subject}\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n{body}";
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.eml");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = new FileStream(path, options);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }
}
