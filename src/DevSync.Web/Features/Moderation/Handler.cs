using DevSync.Domain.Governance;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace DevSync.Web.Features.Moderation;
public sealed record ReportFormModel(Guid PostId,Guid? CommentId=null);
public sealed record Result(int Status,string? Error=null);
public sealed record ReportRow(Guid Id,Guid Version,Guid PostId,Guid? CommentId,Guid TargetId,string Kind,string Category,string Details,string Excerpt,string Status,DateTimeOffset Date);
public sealed record AuditRow(string Action,string Reason,DateTimeOffset At);
public sealed record VerificationModel(DevSync.Web.Features.Verification.RequestRow Request,AuditRow[] Audits);
public sealed record ReportModel(ReportRow Report,Guid TargetVersion,bool Hidden,bool TargetExists,AuditRow[] Audits);
public sealed record QueueModel(ReportRow[] Reports,DevSync.Web.Features.Verification.RequestRow[] Verifications,string Status,int Page,int Pages);
public sealed class Handler(AppDbContext db,TimeProvider clock)
{
    private DateTimeOffset Now=>clock.GetUtcNow().ToUniversalTime();
    private void Audit(Guid actor,string kind,Guid target,string action,string reason)=>db.GovernanceAudits.Add(new GovernanceAudit{ActorId=actor,Kind=kind,TargetId=target,Action=action,Reason=reason,CreatedAt=Now});
    private static bool ReasonValid(string? reason)=>!string.IsNullOrWhiteSpace(reason)&&reason.Length<=2000;
    public async Task<QueueModel> QueueAsync(string? status,int page,CancellationToken ct)
    {
        status=status is "resolved" or "dismissed"?"resolved":"pending";
        var reports=db.ContentReports.AsNoTracking().Where(r=>status=="pending"?r.Status=="pending":r.Status!="pending");
        var total=await reports.CountAsync(ct);
        var verificationCount=await db.PublisherVerifications.CountAsync(v=>status=="pending"?v.Status=="pending":v.Status!="pending",ct);
        var pages=Math.Max(Math.Max(1,(total+19)/20),Math.Max(1,(verificationCount+19)/20));page=Math.Clamp(page,1,pages);
        var rows=await reports.OrderBy(r=>r.CreatedAt).ThenBy(r=>r.Id).Skip((page-1)*20).Take(20)
            .Select(r=>new ReportRow(r.Id,r.Version,r.PostId,r.CommentId,r.TargetId,r.Kind,r.Category,r.Details,r.Excerpt,r.Status,r.CreatedAt)).ToArrayAsync(ct);
        var verification=await db.PublisherVerifications.AsNoTracking().Where(v=>status=="pending"?v.Status=="pending":v.Status!="pending")
            .OrderByDescending(v=>v.SubmittedAt).ThenByDescending(v=>v.Id).Skip((page-1)*20).Take(20)
            .Select(v=>new DevSync.Web.Features.Verification.RequestRow(v.Id,v.Version,v.EvidenceUrl,v.Statement,v.Status,v.DecisionReason,v.SubmittedAt)).ToArrayAsync(ct);
        return new(rows,verification,status,page,Math.Max(pages,Math.Max(1,(verificationCount+19)/20)));
    }
    public async Task<VerificationModel?> VerificationAsync(Guid id,CancellationToken ct)
    {
        var v=await db.PublisherVerifications.AsNoTracking().Where(v=>v.Id==id)
            .Select(v=>new DevSync.Web.Features.Verification.RequestRow(v.Id,v.Version,v.EvidenceUrl,v.Statement,v.Status,v.DecisionReason,v.SubmittedAt)).SingleOrDefaultAsync(ct);
        if(v is null)return null;
        return new(v,await AuditsAsync("verification",id,ct));
    }
    private Task<AuditRow[]> AuditsAsync(string kind,Guid id,CancellationToken ct)=>db.GovernanceAudits.AsNoTracking().Where(a=>a.Kind==kind&&a.TargetId==id)
        .OrderByDescending(a=>a.CreatedAt).ThenByDescending(a=>a.Id).Take(50).Select(a=>new AuditRow(a.Action,a.Reason,a.CreatedAt)).ToArrayAsync(ct);
    public async Task<Result> ReviewAsync(Guid id,Guid actor,Guid version,string? action,string? reason,CancellationToken ct)
    {
        var userId=await db.PublisherVerifications.Where(v=>v.Id==id).Select(v=>(Guid?)v.UserId).SingleOrDefaultAsync(ct);
        if(userId is null)return new(404);
        if(userId==actor)return new(403,"Reviewers cannot review their own verification requests.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId.Value} FOR UPDATE").SingleAsync(ct);
        var v=await db.PublisherVerifications.SingleOrDefaultAsync(v=>v.Id==id,ct);if(v is null)return new(404);
        if(v.Version!=version)return new(409,"This request changed. Reload before reviewing.");
        if(!ReasonValid(reason))return new(422,"Explain the decision using 1–2,000 characters.");
        var next=action switch{"approve" when v.Status=="pending"=>"approved","reject" when v.Status=="pending"=>"rejected","revoke" when v.Status=="approved"=>"revoked",_=>null};
        if(next is null)return new(422,"Choose an action available for this request.");
        v.Status=next;v.DecisionReason=reason!.Trim();v.ReviewerId=actor;v.ReviewedAt=Now;v.Version=Guid.NewGuid();
        Audit(actor,"verification",id,next,v.DecisionReason);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(200);
    }
    public async Task<Result> ReportAsync(Guid actor,Guid postId,Guid? commentId,string? category,string? details,CancellationToken ct)
    {
        if(category is not("spam" or "abuse" or "unsafe" or "copyright" or "other") || !ReasonValid(details))return new(422,"Choose a category and explain the report using 1–2,000 characters.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var post=await db.BlogPosts.FromSqlInterpolated($"SELECT * FROM \"BlogPosts\" WHERE \"Id\" = {postId} AND \"PublishedAt\" IS NOT NULL AND NOT \"IsHidden\" FOR SHARE").AsNoTracking().SingleOrDefaultAsync(ct);
        if(post is null)return new(404);
        var excerpt=post.PublishedTitle!;var kind="post";var target=postId;
        if(commentId is not null){
            var comment=await db.BlogComments.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==commentId && c.PostId==postId && !c.IsHidden,ct);
            if(comment is null)return new(404);excerpt=comment.Body;kind="comment";target=comment.Id;
        }
        if(await db.ContentReports.AnyAsync(r=>r.ReporterId==actor&&r.Kind==kind&&r.TargetId==target&&r.Status=="pending",ct))return new(200);
        var report=new ContentReport{ReporterId=actor,PostId=postId,CommentId=commentId,Kind=kind,TargetId=target,Category=category,Details=details!.Trim(),Excerpt=excerpt[..Math.Min(excerpt.Length,2000)],CreatedAt=Now};
        db.ContentReports.Add(report);
        try{await db.SaveChangesAsync(ct);}
        catch(DbUpdateException ex) when(ex.InnerException is PostgresException{SqlState:PostgresErrorCodes.UniqueViolation}){return new(200);}
        await tx.CommitAsync(ct);return new(200);
    }
    public async Task<ReportModel?> ReportAsync(Guid id,CancellationToken ct)
    {
        var report=await db.ContentReports.AsNoTracking().Where(r=>r.Id==id).Select(r=>new ReportRow(r.Id,r.Version,r.PostId,r.CommentId,r.TargetId,r.Kind,r.Category,r.Details,r.Excerpt,r.Status,r.CreatedAt)).SingleOrDefaultAsync(ct);
        if(report is null)return null;
        Guid version=default;bool hidden=false;bool exists=false;
        if(report.Kind=="post"){var p=await db.BlogPosts.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==report.TargetId,ct);if(p is not null){version=p.Version;hidden=p.IsHidden;exists=true;}}
        else{var c=await db.BlogComments.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==report.TargetId&&c.PostId==report.PostId,ct);if(c is not null){version=c.Version;hidden=c.IsHidden;exists=true;}}
        return new(report,version,hidden,exists,await AuditsAsync("report",id,ct));
    }
    public async Task<Result> ActAsync(Guid id,Guid actor,Guid version,Guid targetVersion,string? action,string? reason,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var info=await db.ContentReports.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==id,ct);if(info is null)return new(404);
        var post=await db.BlogPosts.FromSqlInterpolated($"SELECT * FROM \"BlogPosts\" WHERE \"Id\" = {info.PostId} FOR UPDATE").SingleOrDefaultAsync(ct);
        if(post is null)return new(404);
        var report=await db.ContentReports.FromSqlInterpolated($"SELECT * FROM \"ContentReports\" WHERE \"Id\" = {id} FOR UPDATE").SingleAsync(ct);
        if(report.Version!=version)return new(409,"This report changed. Reload before acting.");
        if(!ReasonValid(reason))return new(422,"Record a reason using 1–2,000 characters.");
        if(action=="dismiss" && report.Status=="pending")report.Status="dismissed";
        else if(action is "hide" or "restore"){
            var hidden=action=="hide";
            if(report.Kind=="post"){if(post.Version!=targetVersion)return new(409,"The article changed. Reload before acting.");if(post.IsHidden==hidden)return new(422,"That visibility state is already applied.");post.IsHidden=hidden;post.Version=Guid.NewGuid();}
            else{
                var c=await db.BlogComments.FromSqlInterpolated($"SELECT * FROM \"BlogComments\" WHERE \"Id\" = {report.TargetId} AND \"PostId\" = {post.Id} FOR UPDATE").SingleOrDefaultAsync(ct);
                if(c is null)return new(404);if(c.Version!=targetVersion)return new(409,"The comment changed. Reload before acting.");if(c.IsHidden==hidden)return new(422,"That visibility state is already applied.");c.IsHidden=hidden;c.Version=Guid.NewGuid();
            }
            report.Status="resolved";
        }
        else return new(422,"Choose an available moderation action.");
        report.Version=Guid.NewGuid();Audit(actor,"report",id,action!,reason!.Trim());
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new(200);
    }
}
