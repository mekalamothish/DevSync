using DevSync.Infrastructure.Email;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Teams.Invitations;

public sealed class InvitationMail(AppDbContext db,IAccountEmailSender sender,IConfiguration configuration,
    IHostEnvironment environment,ILogger<InvitationMail> logger)
{
    public async Task<string> SendAsync(Guid invitationId,string token,CancellationToken ct)
    {
        try
        {
            var invite=await (from i in db.TeamInvitations.AsNoTracking() join t in db.Teams.AsNoTracking() on i.TeamId equals t.Id
                where i.Id==invitationId && i.RevokedAt==null && i.ConsumedAt==null
                select new {i.Email,i.Role,i.ExpiresAt,TeamName=t.Name}).SingleAsync(ct);
            var origin=configuration["Account:PublicBaseUrl"];
            if(!Uri.TryCreate(origin,UriKind.Absolute,out var baseUri) || baseUri.UserInfo.Length!=0 || baseUri.Query.Length!=0 || baseUri.Fragment.Length!=0 ||
                (baseUri.Scheme!="https" && !(environment.IsDevelopment() && baseUri.Scheme=="http")))
                throw new InvalidOperationException("Configure Account:PublicBaseUrl with an HTTPS application URL.");
            var link=new Uri(baseUri,"teams/invitations/accept")+"?token="+Uri.EscapeDataString(token);
            await sender.SendAsync(invite.Email,"You're invited to a DevSync team",
                $"You have been invited to {invite.TeamName} as a {invite.Role}.\n\nOpen this invitation:\n{link}\n\n"+
                $"Sign in with this email address to accept. If you do not have an account, register and verify this email address, then open the invitation again.\n\n"+
                $"This invitation expires {invite.ExpiresAt:yyyy-MM-dd HH:mm} UTC. If you did not expect it, you can ignore this email.",ct);
            return EmailDelivery.UsesSmtp(configuration,environment)
                ? "Invitation email submitted for delivery. You can also share the link below."
                : "Invitation created. Local email mode saves the message without sending it; share the link below.";
        }
        catch(Exception exception) when(exception is not OperationCanceledException)
        {
            logger.LogWarning("Team invitation email failed for invitation {InvitationId}; delivery type {FailureType}.",invitationId,exception.GetType().Name);
            return "Invitation created, but the email could not be sent. Share the link below or create a new invitation to retry.";
        }
    }
}
