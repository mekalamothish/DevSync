using System.Net;
using System.Text.RegularExpressions;
using DevSync.Domain.Blogs;
using DevSync.Domain.Profiles;
using DevSync.Domain.StudyNotes;
using DevSync.Domain.Tasks;
using DevSync.Domain.Teams;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevSync.Tests;

[Collection("PostgreSQL")]
[Trait("Category", "Database")]
public sealed class TeamsFlowTests(AccountFixture fixture) : IClassFixture<AccountFixture>
{
    private const string Password = "Teams-Test-Password42!";
    private static string Field(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success, $"Missing field: {name}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, Dictionary<string,string> fields)
    {
        fields["__RequestVerificationToken"] = Field(await client.GetStringAsync("/teams"), "__RequestVerificationToken");
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }
    private static async Task<string> Follow(HttpClient client, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var page = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        return await page.Content.ReadAsStringAsync();
    }
    private async Task<Guid> User(HttpClient client, string suffix, bool privileged = false)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { Email = fixture.Email(suffix), UserName = fixture.Email(suffix), EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, ApplicationRoles.Member)).Succeeded);
        if(privileged) Assert.True((await users.AddToRoleAsync(user, ApplicationRoles.Administrator)).Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DeveloperProfiles.Add(new DeveloperProfile { UserId = user.Id, Handle = fixture.RunId + "_" + suffix, DisplayName = "Teammate " + suffix });
        await db.SaveChangesAsync();
        var html = await client.GetStringAsync("/account/sign-in");
        var login = await client.PostAsync("/account/sign-in", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["Email"] = fixture.Email(suffix), ["Password"] = Password, ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken") }));
        await Follow(client, login); return user.Id;
    }
    private static async Task<Guid> Team(HttpClient client, string name = "Workspace team")
    {
        var response = await Post(client, "/teams/create", new() { ["Name"] = name });
        await Follow(client, response); return Guid.Parse(response.Headers.Location!.OriginalString.Split('/')[2]);
    }
    private static async Task<string> Version(HttpClient client, Guid team) => Field(await client.GetStringAsync($"/teams/{team}/members"), "Version");
    private static async Task<string> Invite(HttpClient client, Guid team, string email, string role = "member")
    {
        var html = await Follow(client, await Post(client, $"/teams/{team}/invitations", new() {
            ["Version"] = await Version(client, team), ["Email"] = email, ["Role"] = role }));
        var link = Regex.Match(html, "id=\"invitation-link\" href=\"([^\"]*)\"");
        Assert.True(link.Success); return WebUtility.HtmlDecode(link.Groups[1].Value).Split("token=")[1];
    }
    private static async Task Join(HttpClient client, string token)
    {
        var page = await client.GetAsync("/teams/invitations/accept?token=" + token);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("no-referrer", page.Headers.GetValues("Referrer-Policy").Single());
        await Follow(client, await Post(client, "/teams/invitations/accept", new() { ["Token"] = token }));
    }
    private static async Task<Guid> Item(HttpClient client, Guid team, string kind = "note", string title = "Shared knowledge")
    {
        var response = await Post(client, $"/teams/{team}/items", new() { ["Kind"] = kind, ["Title"] = title, ["Body"] = "<script>safe</script>\n## Team plan" });
        await Follow(client, response); return Guid.Parse(response.Headers.Location!.OriginalString.Split('/')[4]);
    }
    private static async Task<Dictionary<string,string>> Edit(HttpClient client, Guid team, Guid item, string title = "Saved team knowledge")
    {
        return new() { ["Version"] = Field(await client.GetStringAsync($"/teams/{team}/items/{item}"), "Version"),
            ["Title"] = title, ["Body"] = "Updated content", ["Kind"] = "note", ["State"] = "in-progress", ["Priority"] = "high" };
    }
    private static async Task<HttpResponseMessage> Change(HttpClient client, Guid team, string op, Guid? member = null, string? role = null, string? version = null) =>
        await Post(client, $"/teams/{team}/members", new() { ["Version"] = version ?? await Version(client, team),
            ["Operation"] = op, ["MemberId"] = member?.ToString() ?? "", ["Role"] = role ?? "" });

    [Fact]
    public async Task Invitation_is_email_bound_single_use_replaced_revoked_and_expiring()
    {
        using var owner = fixture.Client(); using var member = fixture.Client(); using var other = fixture.Client();
        await User(owner, "io"); await User(member, "im"); await User(other, "ix");
        var team = await Team(owner);
        var old = await Invite(owner, team, fixture.Email("im").ToUpperInvariant());
        var delivered=Assert.Single(fixture.Mailbox.Messages,m=>m.Body.Contains(old,StringComparison.Ordinal));
        Assert.Equal(fixture.Email("im").ToUpperInvariant(),delivered.Recipient);
        Assert.Contains("http://localhost:5180/teams/invitations/accept?token="+old,delivered.Body);
        Assert.Contains("Workspace team",delivered.Body);
        Assert.Contains("verify this email address",delivered.Body);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync("/teams/invitations/accept?token=" + old)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(other, "/teams/invitations/accept", new(){["Token"]=old})).StatusCode);
        var current = await Invite(owner, team, fixture.Email("im"));
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync("/teams/invitations/accept?token=" + old)).StatusCode);
        await Join(member, current);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(member, "/teams/invitations/accept", new(){["Token"]=current})).StatusCode);
        var revoked = await Invite(owner, team, fixture.Email("ix"), "viewer");
        Guid inviteId;
        using(var scope=fixture.Factory.Services.CreateScope()) inviteId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().TeamInvitations.Where(i=>i.TeamId==team && i.Email==fixture.Email("ix") && i.RevokedAt==null).Select(i=>i.Id).SingleAsync();
        await Follow(owner, await Post(owner,$"/teams/{team}/invitations/{inviteId}/revoke",new(){["Version"]=await Version(owner,team)}));
        Assert.Equal(HttpStatusCode.NotFound,(await Post(other,"/teams/invitations/accept",new(){["Token"]=revoked})).StatusCode);
        var expired=await Invite(owner,team,fixture.Email("ix"));
        using(var scope=fixture.Factory.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().TeamInvitations.Where(i=>i.TeamId==team && i.RevokedAt==null && i.ConsumedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(i=>i.ExpiresAt,DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync("/teams/invitations/accept?token="+expired)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Post(other,"/teams/invitations/accept",new(){["Token"]=expired})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await owner.GetAsync("/teams/invitations/accept?token=garbage")).StatusCode);
        using var verify=fixture.Factory.Services.CreateScope(); var db=verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2,await db.TeamMemberships.CountAsync(m=>m.TeamId==team));
        Assert.DoesNotContain(current, await db.TeamInvitations.Where(i=>i.TeamId==team).Select(i=>i.TokenHash).ToArrayAsync());
    }

    [Fact]
    public async Task Rich_personal_copies_keep_formatting_and_omit_private_article_targets()
    {
        using var owner=fixture.Client();var user=await User(owner,"richcopy");var team=await Team(owner);
        Guid blogId,noteId;
        using(var scope=fixture.Factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var blog=new BlogPost{AuthorId=user,Title="Rich personal article",EditorFormat="richtext",RichText=RichTextDocumentTests.Document(),Markdown="Inactive private markdown"};
            var note=new StudyNote{OwnerId=user,Title="Mixed rich note"};blogId=blog.Id;noteId=note.Id;
            db.BlogPosts.Add(blog);db.StudyNotes.Add(note);
            db.StudyNoteBlocks.AddRange(new StudyNoteBlock{NoteId=note.Id,Position=0,Kind="richtext",Content=blog.RichText},
                new StudyNoteBlock{NoteId=note.Id,Position=1,Kind="code",Content="class Example {}",Language="C#"},
                new StudyNoteBlock{NoteId=note.Id,Position=2,Kind="article",Content="Private target annotation",LinkedPostId=blog.Id});
            await db.SaveChangesAsync();
        }
        var first=await Post(owner,$"/teams/{team}/share",new(){["Kind"]="blog",["SourceId"]=blogId.ToString()});
        var html=await Follow(owner,first);Assert.Contains("<strong>Formatted text</strong>",html);Assert.DoesNotContain("Inactive private markdown",html);
        var second=await Post(owner,$"/teams/{team}/share",new(){["Kind"]="note",["SourceId"]=noteId.ToString()});
        html=await Follow(owner,second);Assert.Contains("<strong>Formatted text</strong>",html);Assert.Contains("class Example {}",html);Assert.DoesNotContain("Private target annotation",html);
        using var verify=fixture.Factory.Services.CreateScope();var rows=await verify.ServiceProvider.GetRequiredService<AppDbContext>().TeamWorkspaceItems.Where(i=>i.TeamId==team).ToListAsync();
        Assert.All(rows,item=>{Assert.Equal("richtext",item.ContentFormat);Assert.Null(DevSync.Web.Common.Content.RichTextDocument.Validate(item.Body));});
    }

    [Fact]
    public async Task Failed_invitation_email_retains_the_invitation_for_manual_delivery()
    {
        using var owner=fixture.Client(); await User(owner,"mailfailure");
        var team=await Team(owner); var token=await Invite(owner,team,fixture.Email("mailrecipient"));
        using var scope=fixture.Factory.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invite=await db.TeamInvitations.SingleAsync(i=>i.TeamId==team && i.RevokedAt==null);
        var mail=new DevSync.Web.Features.Teams.Invitations.InvitationMail(db,new FailedMail(),
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
            new EmailDeliveryTests.EnvironmentStub("Development"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DevSync.Web.Features.Teams.Invitations.InvitationMail>.Instance);
        var message=await mail.SendAsync(invite.Id,token,CancellationToken.None);
        Assert.Contains("email could not be sent",message);
        Assert.Contains("Share the link",message);
        await db.Entry(invite).ReloadAsync(); Assert.Null(invite.RevokedAt); Assert.Null(invite.ConsumedAt);
    }
    private sealed class FailedMail:DevSync.Infrastructure.Email.IAccountEmailSender
    {
        public Task SendAsync(string recipient,string subject,string body,CancellationToken cancellationToken)
            => throw new System.Net.Mail.SmtpException("Test delivery failure");
    }

    [Fact]
    public async Task Roles_ownership_transfer_leave_and_access_revocation_are_enforced()
    {
        using var owner=fixture.Client(); using var member=fixture.Client(); using var viewer=fixture.Client(); using var admin=fixture.Client(); using var outsider=fixture.Client();
        var ownerId=await User(owner,"ro"); var memberId=await User(member,"rm"); var viewerId=await User(viewer,"rv"); var adminId=await User(admin,"ra"); await User(outsider,"rx",true);
        var team=await Team(owner); var foreign=await Team(outsider,"Other team");
        await Join(member,await Invite(owner,team,fixture.Email("rm")));
        await Join(viewer,await Invite(owner,team,fixture.Email("rv"),"viewer"));
        await Join(admin,await Invite(owner,team,fixture.Email("ra")));
        await Follow(owner,await Change(owner,team,"role",adminId,"admin"));
        var item=await Item(member,team);
        Assert.Equal(HttpStatusCode.NotFound,(await outsider.GetAsync($"/teams/{team}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await outsider.GetAsync($"/teams/{team}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await outsider.GetAsync($"/teams/{team}/items/{item}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await owner.GetAsync($"/teams/{foreign}/items/{item}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(viewer,$"/teams/{team}/items",new(){["Title"]="forged",["Kind"]="note"})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Post(viewer,$"/teams/{team}/items/{item}/save",await Edit(member,team,item))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Change(member,team,"role",viewerId,"member")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Change(admin,team,"role",memberId,"admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await Change(admin,team,"transfer",memberId)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Change(owner,team,"leave")).StatusCode);
        await Follow(owner,await Change(owner,team,"transfer",memberId));
        Assert.Contains("owner",await member.GetStringAsync($"/teams/{team}/members"));
        Assert.Equal(HttpStatusCode.Forbidden,(await Change(owner,team,"role",adminId,"member")).StatusCode);
        await Follow(member,await Change(member,team,"remove",viewerId));
        Assert.Equal(HttpStatusCode.NotFound,(await viewer.GetAsync($"/teams/{team}/items/{item}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Post(viewer,$"/teams/{team}/items/{item}/save",await Edit(member,team,item))).StatusCode);
        await Follow(owner,await Change(owner,team,"leave"));
        Assert.Equal(HttpStatusCode.NotFound,(await owner.GetAsync($"/teams/{team}")).StatusCode);
        using var scope=fixture.Factory.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(memberId,(await db.Teams.SingleAsync(t=>t.Id==team)).OwnerId);
        Assert.Equal(1,await db.TeamMemberships.CountAsync(m=>m.TeamId==team && m.Role=="owner"));
        Assert.False(await db.TeamMemberships.AnyAsync(m=>m.TeamId==team && m.UserId==ownerId));
    }

    [Fact]
    public async Task Workspace_edits_assignment_archive_validation_and_conflicts_persist()
    {
        using var owner=fixture.Client(); using var member=fixture.Client(); using var viewer=fixture.Client(); using var outsider=fixture.Client();
        await User(owner,"wo"); var memberId=await User(member,"wm"); var viewerId=await User(viewer,"wv"); var outsiderId=await User(outsider,"wx");
        var team=await Team(owner); await Join(member,await Invite(owner,team,fixture.Email("wm"))); await Join(viewer,await Invite(owner,team,fixture.Email("wv"),"viewer"));
        var item=await Item(owner,team,"task","Shared task");
        var html=await viewer.GetStringAsync($"/teams/{team}/items/{item}");
        Assert.Contains("&lt;script&gt;",html); Assert.DoesNotContain("<script>safe</script>",html); Assert.DoesNotContain("Save team content",html);
        var fields=await Edit(member,team,item,"Assigned task"); fields["AssignedUserId"]=memberId.ToString(); fields["DueDate"]="2026-10-12";
        await Follow(member,await Post(member,$"/teams/{team}/items/{item}/save",fields));
        var stale=await Post(owner,$"/teams/{team}/items/{item}/save",fields);
        Assert.Equal(HttpStatusCode.Conflict,stale.StatusCode); Assert.Contains("Saving is paused",await stale.Content.ReadAsStringAsync());
        foreach(var badId in new[]{viewerId,outsiderId}) {
            var bad=await Edit(owner,team,item,"Must not save"); bad["AssignedUserId"]=badId.ToString();
            Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(owner,$"/teams/{team}/items/{item}/save",bad)).StatusCode);
        }
        var badDate=await Edit(owner,team,item); badDate["DueDate"]="invalid-date";
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(owner,$"/teams/{team}/items/{item}/save",badDate)).StatusCode);
        await Follow(owner,await Change(owner,team,"remove",memberId));
        var unassigned=await owner.GetStringAsync($"/teams/{team}/items/{item}"); Assert.Contains("Unassigned",unassigned);
        var version=Field(unassigned,"Version");
        await Follow(owner,await Post(owner,$"/teams/{team}/items/{item}/archive",new(){["Version"]=version,["Archived"]="true"}));
        Assert.DoesNotContain("Assigned task",await owner.GetStringAsync($"/teams/{team}?kind=task"));
        Assert.Contains("Assigned task",await owner.GetStringAsync($"/teams/{team}?kind=task&archived=true"));
        var archived=await owner.GetStringAsync($"/teams/{team}/items/{item}"); Assert.Contains("Restore item",archived);
        await Follow(owner,await Post(owner,$"/teams/{team}/items/{item}/archive",new(){["Version"]=Field(archived,"Version"),["Archived"]="false"}));
        using var scope=fixture.Factory.Services.CreateScope(); var task=await scope.ServiceProvider.GetRequiredService<AppDbContext>().TeamWorkspaceItems.SingleAsync(x=>x.Id==item);
        Assert.Equal("task",task.Kind); Assert.Equal("in-progress",task.State); Assert.Equal("Assigned task",task.Title);
        Assert.Null(task.AssignedUserId); Assert.Equal(new DateOnly(2026,10,12),task.DueDate); Assert.False(task.IsArchived);
    }

    [Fact]
    public async Task Sharing_copies_owned_content_without_exposing_private_sources_or_following_changes()
    {
        using var owner=fixture.Client(); using var member=fixture.Client(); var ownerId=await User(owner,"so"); var memberId=await User(member,"sm");
        var team=await Team(owner); await Join(member,await Invite(owner,team,fixture.Email("sm")));
        var blog=new BlogPost{AuthorId=ownerId,Title="Personal article",Markdown="Original article"};
        var secret=new BlogPost{AuthorId=ownerId,Title="Unshared secret",Markdown="Secret body"};
        var note=new StudyNote{OwnerId=ownerId,Title="Personal note"};
        var board=new TaskBoard{OwnerId=ownerId,Name="Source board"}; var column=new TaskColumn{OwnerId=ownerId,BoardId=board.Id};
        var task=new WorkTask{OwnerId=ownerId,BoardId=board.Id,ColumnId=column.Id,Title="Personal task",Description="Original task",LinkedPostId=secret.Id,LinkedNoteId=note.Id};
        using(var scope=fixture.Factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.BlogPosts.AddRange(blog,secret); db.StudyNotes.Add(note); db.TaskBoards.Add(board); db.TaskColumns.Add(column); db.WorkTasks.Add(task);
            db.StudyNoteBlocks.AddRange(new StudyNoteBlock{NoteId=note.Id,Position=0,Content="Original note"},new StudyNoteBlock{NoteId=note.Id,Position=1,Kind="article",LinkedPostId=secret.Id});
            db.TaskChecklistItems.Add(new TaskChecklistItem{TaskId=task.Id,Position=0,Text="Verify copy",IsCompleted=true}); await db.SaveChangesAsync();
        }
        foreach(var source in new[]{("blog",blog.Id),("note",note.Id),("task",task.Id)}) {
            Assert.Equal(HttpStatusCode.NotFound,(await Post(member,$"/teams/{team}/share",new(){["Kind"]=source.Item1,["SourceId"]=source.Item2.ToString()})).StatusCode);
            var response=await Post(owner,$"/teams/{team}/share",new(){["Kind"]=source.Item1,["SourceId"]=source.Item2.ToString()}); await Follow(owner,response);
            var copied=await member.GetStringAsync(response.Headers.Location!.OriginalString);
            Assert.Contains("Original",copied); Assert.DoesNotContain("Unshared secret",copied); Assert.DoesNotContain(secret.Id.ToString(),copied);
        }
        Assert.Equal(HttpStatusCode.NotFound,(await member.GetAsync($"/notes/{note.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await member.GetAsync($"/blogs/{blog.Id}/edit")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await member.GetAsync($"/tasks/{task.Id}")).StatusCode);
        using(var scope=fixture.Factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.BlogPosts.Where(x=>x.Id==blog.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Markdown,"Later private edit"));
            var copies=await db.TeamWorkspaceItems.Where(x=>x.TeamId==team).ToListAsync(); Assert.Equal(3,copies.Count);
            Assert.Equal("Original article",copies.Single(x=>x.Kind=="blog").Body);
            Assert.DoesNotContain("secret",copies.Single(x=>x.Kind=="note").Body,StringComparison.OrdinalIgnoreCase);
            Assert.Contains("- [x] Verify copy",copies.Single(x=>x.Kind=="task").Body);
        }
    }

    [Fact]
    public async Task Simultaneous_saves_acceptance_and_stale_management_have_one_winner()
    {
        using var owner=fixture.Client(); using var member=fixture.Client(); await User(owner,"co"); await User(member,"cm");
        var team=await Team(owner); var token=await Invite(owner,team,fixture.Email("cm"));
        var csrf=Field(await member.GetStringAsync("/teams"),"__RequestVerificationToken");
        var accepts=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>member.PostAsync("/teams/invitations/accept",new FormUrlEncodedContent(new Dictionary<string,string>{["Token"]=token,["__RequestVerificationToken"]=csrf}))));
        Assert.Single(accepts,x=>x.StatusCode==HttpStatusCode.Redirect); Assert.Single(accepts,x=>x.StatusCode==HttpStatusCode.NotFound);
        await Follow(member,accepts.Single(x=>x.StatusCode==HttpStatusCode.Redirect));
        var item=await Item(owner,team); var left=await Edit(owner,team,item,"First winner"); var right=await Edit(member,team,item,"Second winner");
        var saves=await Task.WhenAll(Post(owner,$"/teams/{team}/items/{item}/save",left),Post(member,$"/teams/{team}/items/{item}/save",right));
        Assert.Single(saves,x=>x.StatusCode==HttpStatusCode.Redirect); Assert.Single(saves,x=>x.StatusCode==HttpStatusCode.Conflict);
        var version=await Version(owner,team);
        await Item(member,team,"note","New team update");
        Assert.Equal(HttpStatusCode.Conflict,(await Post(owner,$"/teams/{team}/invitations",new(){["Version"]=version,["Email"]=fixture.Email("unused"),["Role"]="member"})).StatusCode);
        using var scope=fixture.Factory.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2,await db.TeamMemberships.CountAsync(m=>m.TeamId==team));
        Assert.Equal(1,await db.TeamActivities.CountAsync(a=>a.TeamId==team && a.Description=="A team note was updated."));
    }

    [Fact]
    public async Task Csrf_anonymous_access_invalid_roles_and_limits_cannot_write()
    {
        using var owner=fixture.Client(); var ownerId=await User(owner,"lo"); var team=await Team(owner);
        using var anonymous=fixture.Client();
        Assert.Equal(HttpStatusCode.Redirect,(await anonymous.GetAsync($"/teams/{team}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsync($"/teams/{team}/items",new FormUrlEncodedContent(new Dictionary<string,string>{["Title"]="CSRF attempt",["Kind"]="note"}))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(owner,$"/teams/{team}/invitations",new(){["Version"]=await Version(owner,team),["Email"]=fixture.Email("bad"),["Role"]="owner"})).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(owner,$"/teams/{team}/items",new(){["Title"]="Invalid kind",["Kind"]="secret"})).StatusCode);
        using(var scope=fixture.Factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TeamWorkspaceItems.AddRange(Enumerable.Range(0,500).Select(i=>new TeamWorkspaceItem{TeamId=team,Title="Capacity item "+i})); await db.SaveChangesAsync();
        }
        Assert.Contains("Page 1 of 25",await owner.GetStringAsync($"/teams/{team}"));
        Assert.Contains("Page 25 of 25",await owner.GetStringAsync($"/teams/{team}?page=999"));
        var rejected = await Post(owner,$"/teams/{team}/items",new(){["Title"]="Overflow",["Kind"]="note",["Body"]="Retain this unsaved draft"});
        Assert.Equal(HttpStatusCode.UnprocessableEntity,rejected.StatusCode);
        Assert.Contains("Retain this unsaved draft",await rejected.Content.ReadAsStringAsync());
        using(var scope=fixture.Factory.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach(var i in Enumerable.Range(1,19)) { var t=new Team{OwnerId=ownerId,Name="Other "+i}; db.Teams.Add(t); db.TeamMemberships.Add(new TeamMembership{TeamId=t.Id,UserId=ownerId,Role="owner"}); }
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Post(owner,"/teams/create",new(){["Name"]="Twenty-first"})).StatusCode);
    }
}
