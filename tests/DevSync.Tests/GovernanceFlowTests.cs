using System.Net;
using System.Text.RegularExpressions;
using DevSync.Domain.Blogs;
using DevSync.Domain.Governance;
using DevSync.Domain.Profiles;
using DevSync.Domain.StudyNotes;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace DevSync.Tests;

[Collection("PostgreSQL"),Trait("Category","Database")]
public sealed class GovernanceFlowTests(AccountFixture fixture):IClassFixture<AccountFixture>
{
    private const string Password="Governance-Test-Pass42!";
    private static string Field(string html,string name){
        var m=Regex.Match(html,$"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"");Assert.True(m.Success,"Missing "+name);return WebUtility.HtmlDecode(m.Groups[1].Value);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient c,string path,Dictionary<string,string> fields){
        fields["__RequestVerificationToken"]=Field(await c.GetStringAsync("/account/settings"),"__RequestVerificationToken");
        return await c.PostAsync(path,new FormUrlEncodedContent(fields));
    }
    private static async Task<string> Follow(HttpClient c,HttpResponseMessage r){
        Assert.Equal(HttpStatusCode.Redirect,r.StatusCode);var page=await c.GetAsync(r.Headers.Location);Assert.Equal(HttpStatusCode.OK,page.StatusCode);return await page.Content.ReadAsStringAsync();
    }
    private async Task<Guid> User(HttpClient c,string suffix,string? role=null){
        using var scope=fixture.Factory.Services.CreateScope();var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var u=new ApplicationUser{Email=fixture.Email(suffix),UserName=fixture.Email(suffix),EmailConfirmed=true};
        Assert.True((await manager.CreateAsync(u,Password)).Succeeded);Assert.True((await manager.AddToRoleAsync(u,ApplicationRoles.Member)).Succeeded);
        if(role is not null)Assert.True((await manager.AddToRoleAsync(u,role)).Succeeded);
        var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();db.DeveloperProfiles.Add(new DeveloperProfile{UserId=u.Id,Handle=fixture.RunId+"_"+suffix,DisplayName="Publisher "+suffix,Bio="Private bio marker"});await db.SaveChangesAsync();
        var html=await c.GetStringAsync("/account/sign-in");await Follow(c,await c.PostAsync("/account/sign-in",new FormUrlEncodedContent(new Dictionary<string,string>{["Email"]=fixture.Email(suffix),["Password"]=Password,["__RequestVerificationToken"]=Field(html,"__RequestVerificationToken")})));
        return u.Id;
    }
    private static BlogPost Published(Guid user,string title)=>new(){AuthorId=user,Title="Private working title",Summary="Private summary",Markdown="Private working text",PublishedTitle=title,PublishedSummary="Public summary",PublishedMarkdown="Public body",PublishedAt=DateTimeOffset.UtcNow,Slug="post-"+Guid.NewGuid().ToString("N")};
    private async Task<Guid> Request(HttpClient c,string url="https://example.com/portfolio"){
        var version=Field(await c.GetStringAsync("/account/verification"),"Version");
        await Follow(c,await Post(c,"/account/verification",new(){["Version"]=version,["EvidenceUrl"]=url,["Statement"]="Private evidence statement"}));
        using var scope=fixture.Factory.Services.CreateScope();return await scope.ServiceProvider.GetRequiredService<AppDbContext>().PublisherVerifications.Where(v=>v.EvidenceUrl==url).Select(v=>v.Id).SingleAsync();
    }
    private static async Task<Dictionary<string,string>> ReviewFields(HttpClient c,Guid id,string action)=>new(){["Version"]=Field(await c.GetStringAsync($"/moderation/verification/{id}"),"Version"),["Action"]=action,["Reason"]="Reviewed public work and publishing history."};
    private static async Task<Dictionary<string,string>> ActionFields(HttpClient c,Guid id,string action){
        var html=await c.GetStringAsync($"/moderation/reports/{id}");return new(){["Version"]=Field(html,"Version"),["TargetVersion"]=Field(html,"TargetVersion"),["Action"]=action,["Reason"]="Reviewed content against community rules."};
    }
    private async Task<Guid> Report(HttpClient c,Guid actor,Guid post,Guid? comment=null){
        var fields=new Dictionary<string,string>{["Category"]="spam",["Details"]="Please review this public content."};if(comment is not null)fields["CommentId"]=comment.ToString()!;
        await Follow(c,await Post(c,$"/blogs/{post}/report",fields));
        using var scope=fixture.Factory.Services.CreateScope();return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ContentReports.Where(r=>r.ReporterId==actor&&r.TargetId==(comment??post)&&r.Status=="pending").Select(r=>r.Id).SingleAsync();
    }

    [Fact]
    public async Task Settings_are_versioned_and_public_profile_requires_opt_in_without_private_data()
    {
        using var owner=fixture.Client();using var anon=fixture.Client();var ownerId=await User(owner,"prefs");
        var handle=fixture.RunId+"_prefs";
        Assert.Equal(HttpStatusCode.NotFound,(await anon.GetAsync("/people/"+handle)).StatusCode);
        using(var scope=fixture.Factory.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.BlogPosts.AddRange(Enumerable.Range(0,25).Select(i=>Published(ownerId,"Visible article "+i)));
            var hidden=Published(ownerId,"Hidden article marker");hidden.IsHidden=true;db.BlogPosts.Add(hidden);
            db.BlogPosts.Add(new BlogPost{AuthorId=ownerId,Title="Private draft marker"});await db.SaveChangesAsync();
        }
        var fields=new Dictionary<string,string>{["Version"]=Field(await owner.GetStringAsync("/account/settings"),"Version"),["PublicProfile"]="true",["ShowBio"]="false",["ReduceMotion"]="true",["ArticlesPageSize"]="24",["UserId"]=Guid.NewGuid().ToString()};
        await Follow(owner,await Post(owner,"/account/settings",fields));
        Assert.Equal(HttpStatusCode.Conflict,(await Post(owner,"/account/settings",fields)).StatusCode);
        var publicPage=await anon.GetStringAsync("/people/"+handle.ToUpperInvariant());
        Assert.Contains("25 published articles",publicPage);Assert.Contains("Page 1 of 3",publicPage);
        foreach(var secret in new[]{"Private draft marker","Private bio marker","Hidden article marker",fixture.Email("prefs"),"Private working title"})Assert.DoesNotContain(secret,publicPage);
        var explore=await owner.GetStringAsync("/blogs");Assert.Contains("Page 1 of 2",explore);Assert.Contains("class=\"reduce-motion\"",explore);
        var bad=new Dictionary<string,string>(fields){["Version"]=Field(await owner.GetStringAsync("/account/settings"),"Version"),["ArticlesPageSize"]="13"};
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(owner,"/account/settings",bad)).StatusCode);
        var updated=new Dictionary<string,string>(fields){["Version"]=Field(await owner.GetStringAsync("/account/settings"),"Version"),["ShowBio"]="true"};
        await Follow(owner,await Post(owner,"/account/settings",updated));Assert.Contains("Private bio marker",await anon.GetStringAsync("/people/"+handle));
        updated["Version"]=Field(await owner.GetStringAsync("/account/settings"),"Version");updated["PublicProfile"]="false";
        await Follow(owner,await Post(owner,"/account/settings",updated));Assert.Equal(HttpStatusCode.NotFound,(await anon.GetAsync("/people/"+handle)).StatusCode);
    }

    [Fact]
    public async Task Verification_reject_resubmit_approve_revoke_and_self_review_rules_have_audit_history()
    {
        using var applicant=fixture.Client();using var mod=fixture.Client();using var anon=fixture.Client();
        var userId=await User(applicant,"verify");var modId=await User(mod,"review",ApplicationRoles.Moderator);
        foreach(var url in new[]{"http://example.com","https://localhost","https://user:password@example.com"}){
            var invalid=await Post(applicant,"/account/verification",new(){["Version"]=Guid.Empty.ToString(),["EvidenceUrl"]=url,["Statement"]="Evidence"});
            Assert.Equal(HttpStatusCode.UnprocessableEntity,invalid.StatusCode);
        }
        var id=await Request(applicant);
        var denied=await applicant.GetAsync("/moderation");Assert.Equal(HttpStatusCode.Redirect,denied.StatusCode);Assert.Equal("/account/access-denied",denied.Headers.Location!.AbsolutePath);
        var reject=await ReviewFields(mod,id,"reject");await Follow(mod,await Post(mod,$"/moderation/verification/{id}",reject));
        Assert.Contains("rejected",await applicant.GetStringAsync("/account/verification"));
        Assert.Equal(HttpStatusCode.Conflict,(await Post(mod,$"/moderation/verification/{id}",reject)).StatusCode);
        var second=await Request(applicant,"https://example.com/updated-portfolio");
        await Follow(mod,await Post(mod,$"/moderation/verification/{second}",await ReviewFields(mod,second,"approve")));
        var pref=Field(await applicant.GetStringAsync("/account/settings"),"Version");
        await Follow(applicant,await Post(applicant,"/account/settings",new(){["Version"]=pref,["PublicProfile"]="true",["ArticlesPageSize"]="12"}));
        var profile=await anon.GetStringAsync("/people/"+fixture.RunId+"_verify");
        Assert.Contains("Verified publisher",profile);Assert.DoesNotContain("Private evidence statement",profile);Assert.DoesNotContain("updated-portfolio",profile);
        await Follow(mod,await Post(mod,$"/moderation/verification/{second}",await ReviewFields(mod,second,"revoke")));
        Assert.DoesNotContain("Verified publisher",await anon.GetStringAsync("/people/"+fixture.RunId+"_verify"));
        var own=await Request(mod,"https://example.com/reviewer");
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(mod,$"/moderation/verification/{own}",await ReviewFields(mod,own,"approve"))).StatusCode);
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(5,await db.GovernanceAudits.CountAsync(a=>a.TargetId==id||a.TargetId==second));
        Assert.Equal("revoked",(await db.PublisherVerifications.SingleAsync(v=>v.Id==second)).Status);
        Assert.Equal(modId,(await db.PublisherVerifications.SingleAsync(v=>v.Id==second)).ReviewerId);
    }

    [Fact]
    public async Task Article_hiding_removes_public_reads_feeds_bookmarks_links_and_interactions_until_restore()
    {
        using var author=fixture.Client();using var reader=fixture.Client();using var mod=fixture.Client();using var anon=fixture.Client();
        var authorId=await User(author,"author");var readerId=await User(reader,"reader");await User(mod,"hide",ApplicationRoles.Administrator);
        var post=Published(authorId,"Moderated public article");var draft=new BlogPost{AuthorId=authorId,Title="Never-public draft"};var note=new StudyNote{OwnerId=readerId,Title="Reader note"};
        using(var scope=fixture.Factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();db.BlogPosts.AddRange(post,draft);db.StudyNotes.Add(note);db.StudyNoteBlocks.Add(new StudyNoteBlock{NoteId=note.Id,Kind="article",LinkedPostId=post.Id});await db.SaveChangesAsync();}
        await Follow(reader,await Post(reader,$"/blogs/{post.Id}/bookmark",new(){["enabled"]="true"}));
        Assert.Equal(HttpStatusCode.NotFound,(await Post(reader,$"/blogs/{draft.Id}/report",new(){["Category"]="spam",["Details"]="No private access"})).StatusCode);
        var report=await Report(reader,readerId,post.Id);Assert.Equal(report,await Report(reader,readerId,post.Id));
        var hide=await ActionFields(mod,report,"hide");await Follow(mod,await Post(mod,$"/moderation/reports/{report}",hide));
        Assert.Equal(HttpStatusCode.Conflict,(await Post(mod,$"/moderation/reports/{report}",hide)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await anon.GetAsync("/blogs/"+post.Slug)).StatusCode);
        Assert.DoesNotContain("Moderated public article",await anon.GetStringAsync("/blogs"));
        Assert.DoesNotContain("Moderated public article",await anon.GetStringAsync("/"));
        Assert.DoesNotContain("Moderated public article",await reader.GetStringAsync("/blogs/bookmarks"));
        Assert.DoesNotContain("Moderated public article",await reader.GetStringAsync($"/notes/{note.Id}/preview"));
        foreach(var path in new[]{"react","bookmark","comments"})Assert.Equal(HttpStatusCode.NotFound,(await Post(reader,$"/blogs/{post.Id}/{path}",new(){["enabled"]="true",["Body"]="No hidden write"})).StatusCode);
        var editHtml=await author.GetStringAsync($"/blogs/{post.Id}/edit");
        var publish=await Post(author,$"/blogs/{post.Id}/publish",new(){["Version"]=Field(editHtml,"Version"),["Title"]="Republish attempt",["Summary"]="Summary",["Markdown"]="Body"});
        Assert.Equal(HttpStatusCode.UnprocessableEntity,publish.StatusCode);
        await Follow(mod,await Post(mod,$"/moderation/reports/{report}",await ActionFields(mod,report,"restore")));
        Assert.Contains("Moderated public article",await anon.GetStringAsync("/blogs/"+post.Slug));
        Assert.Contains("Moderated public article",await reader.GetStringAsync($"/notes/{note.Id}/preview"));
        using var scope2=fixture.Factory.Services.CreateScope();var db2=scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2,await db2.GovernanceAudits.CountAsync(a=>a.Kind=="report"&&a.TargetId==report));Assert.False((await db2.BlogPosts.SingleAsync(p=>p.Id==post.Id)).IsHidden);
    }

    [Fact]
    public async Task Comments_can_be_hidden_restored_or_deleted_with_report_history_preserved()
    {
        using var author=fixture.Client();using var reader=fixture.Client();using var mod=fixture.Client();
        var authorId=await User(author,"ca");var readerId=await User(reader,"cr");await User(mod,"cm",ApplicationRoles.Moderator);
        var post=Published(authorId,"Comment review article");var other=Published(authorId,"Other article");var comment=new BlogComment{PostId=post.Id,UserId=readerId,Body="Public comment marker"};
        using(var scope=fixture.Factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();db.BlogPosts.AddRange(post,other);db.BlogComments.Add(comment);await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.NotFound,(await Post(reader,$"/blogs/{other.Id}/report",new(){["CommentId"]=comment.Id.ToString(),["Category"]="abuse",["Details"]="Forged target"})).StatusCode);
        var report=await Report(author,authorId,post.Id,comment.Id);
        await Follow(mod,await Post(mod,$"/moderation/reports/{report}",await ActionFields(mod,report,"hide")));
        Assert.DoesNotContain("Public comment marker",await reader.GetStringAsync("/blogs/"+post.Slug));
        await Follow(mod,await Post(mod,$"/moderation/reports/{report}",await ActionFields(mod,report,"restore")));
        Assert.Contains("Public comment marker",await reader.GetStringAsync("/blogs/"+post.Slug));
        var next=await Report(author,authorId,post.Id,comment.Id);
        await Follow(reader,await Post(reader,$"/blogs/{post.Id}/comments/{comment.Id}/delete",new()));
        var review=await mod.GetStringAsync($"/moderation/reports/{next}");Assert.Contains("original content was removed",review);
        await Follow(mod,await Post(mod,$"/moderation/reports/{next}",await ActionFields(mod,next,"dismiss")));
        using var scope2=fixture.Factory.Services.CreateScope();var r=await scope2.ServiceProvider.GetRequiredService<AppDbContext>().ContentReports.SingleAsync(r=>r.Id==next);
        Assert.Null(r.CommentId);Assert.Equal("Public comment marker",r.Excerpt);Assert.Equal("dismissed",r.Status);
    }

    [Fact]
    public async Task Reviewer_permissions_csrf_concurrent_decisions_and_stale_target_versions_are_enforced()
    {
        using var applicant=fixture.Client();using var mod1=fixture.Client();using var mod2=fixture.Client();using var anon=fixture.Client();
        var userId=await User(applicant,"race");await User(mod1,"m1",ApplicationRoles.Moderator);await User(mod2,"m2",ApplicationRoles.Moderator);
        Assert.Equal(HttpStatusCode.Redirect,(await anon.GetAsync("/moderation")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await applicant.PostAsync("/account/settings",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        var id=await Request(applicant,"https://example.com/race");
        var left=await ReviewFields(mod1,id,"approve");var right=await ReviewFields(mod2,id,"reject");
        var decisions=await Task.WhenAll(Post(mod1,$"/moderation/verification/{id}",left),Post(mod2,$"/moderation/verification/{id}",right));
        Assert.Single(decisions,r=>r.StatusCode==HttpStatusCode.Redirect);Assert.Single(decisions,r=>r.StatusCode==HttpStatusCode.Conflict);
        var post=Published(userId,"Target race");
        using(var scope=fixture.Factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();db.BlogPosts.Add(post);await db.SaveChangesAsync();}
        var report=await Report(applicant,userId,post.Id);var stale=await ActionFields(mod1,report,"hide");
        using(var scope=fixture.Factory.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<AppDbContext>().BlogPosts.Where(p=>p.Id==post.Id).ExecuteUpdateAsync(s=>s.SetProperty(p=>p.Version,Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Conflict,(await Post(mod1,$"/moderation/reports/{report}",stale)).StatusCode);
        var forbidden=await Post(applicant,$"/moderation/reports/{report}",stale);Assert.Equal(HttpStatusCode.Redirect,forbidden.StatusCode);Assert.Equal("/account/access-denied",forbidden.Headers.Location!.AbsolutePath);
        using var verify=fixture.Factory.Services.CreateScope();var db2=verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1,await db2.GovernanceAudits.CountAsync(a=>a.TargetId==id&&a.Action!="submitted"));
        Assert.False((await db2.BlogPosts.SingleAsync(p=>p.Id==post.Id)).IsHidden);Assert.False(await db2.GovernanceAudits.AnyAsync(a=>a.TargetId==report));
    }
}
