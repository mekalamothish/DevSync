using System.ComponentModel.DataAnnotations;
using DevSync.Domain.Governance;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DevSync.Web.Features.Verification;
public sealed class Command
{
    public Guid Version { get; set; }
    [Required,StringLength(2048)] public string EvidenceUrl { get; set; }="";
    [Required,StringLength(2000)] public string Statement { get; set; }="";
}
public sealed record RequestRow(Guid Id,Guid Version,string EvidenceUrl,string Statement,string Status,string Reason,DateTimeOffset SubmittedAt);
public sealed record ViewModel(Command Command,RequestRow[] Requests,bool CanSubmit);
public sealed class Handler(AppDbContext db,TimeProvider clock)
{
    public async Task<ViewModel> LoadAsync(Guid userId,CancellationToken ct)
    {
        var rows=await db.PublisherVerifications.AsNoTracking().Where(v=>v.UserId==userId).OrderByDescending(v=>v.SubmittedAt).ThenByDescending(v=>v.Id).Take(20)
            .Select(v=>new RequestRow(v.Id,v.Version,v.EvidenceUrl,v.Statement,v.Status,v.DecisionReason,v.SubmittedAt)).ToArrayAsync(ct);
        var active=await db.PublisherVerifications.AnyAsync(v=>v.UserId==userId && (v.Status=="pending" || v.Status=="approved"),ct);
        return new(new Command{Version=rows.FirstOrDefault()?.Version??Guid.Empty},rows,!active);
    }
    public async Task<(int Status,string? Error)> SubmitAsync(Guid userId,Command c,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var user=await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if(user is null)return(404,"Account not found.");
        var latest=await db.PublisherVerifications.Where(v=>v.UserId==userId).OrderByDescending(v=>v.SubmittedAt).ThenByDescending(v=>v.Id).FirstOrDefaultAsync(ct);
        if((latest?.Version??Guid.Empty)!=c.Version)return(409,"Verification changed. Reload before submitting.");
        if(await db.PublisherVerifications.AnyAsync(v=>v.UserId==userId && (v.Status=="pending" || v.Status=="approved"),ct))return(422,"You already have a pending request or an approved badge.");
        if(!Uri.TryCreate(c.EvidenceUrl,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.AbsoluteUri.Length>2048 || uri.UserInfo.Length>0 || c.EvidenceUrl.Length>2048
            || uri.HostNameType!=UriHostNameType.Dns || !uri.Host.Contains('.') || uri.Host.EndsWith(".local",StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".localhost",StringComparison.OrdinalIgnoreCase))return(422,"Use a public HTTPS portfolio or professional-profile link without credentials.");
        if(string.IsNullOrWhiteSpace(c.Statement) || c.Statement.Length>2000)return(422,"Explain your publishing experience using 1–2,000 characters.");
        var request=new PublisherVerification{UserId=userId,EvidenceUrl=uri.AbsoluteUri,Statement=c.Statement.Trim(),SubmittedAt=clock.GetUtcNow().ToUniversalTime()};
        db.PublisherVerifications.Add(request);
        db.GovernanceAudits.Add(new GovernanceAudit{ActorId=userId,Kind="verification",TargetId=request.Id,Action="submitted",Reason="Publisher verification requested.",CreatedAt=request.SubmittedAt});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return(200,null);
    }
}
