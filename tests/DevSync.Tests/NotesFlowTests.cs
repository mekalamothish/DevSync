using System.Net;
using System.Text.RegularExpressions;
using DevSync.Domain.Blogs;
using DevSync.Domain.Profiles;
using DevSync.Domain.StudyNotes;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevSync.Tests;

[Collection("PostgreSQL")]
[Trait("Category", "Database")]
public sealed class NotesFlowTests(AccountFixture fixture) : IClassFixture<AccountFixture>
{
    private const string Password = "Notes-Test-Password42!";
    private static string Field(string html, string name) => WebUtility.HtmlDecode(
        Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string formPath, string target, Dictionary<string, string> fields)
    {
        var form = await client.GetAsync(formPath);
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        fields["__RequestVerificationToken"] = Field(await form.Content.ReadAsStringAsync(), "__RequestVerificationToken");
        return await client.PostAsync(target, new FormUrlEncodedContent(fields));
    }

    private async Task<(HttpClient Client, Guid Id)> MemberAsync(string suffix)
    {
        Guid id;
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Email = fixture.Email(suffix), UserName = fixture.Email(suffix), EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, ApplicationRoles.Member)).Succeeded);
            id = user.Id;
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.DeveloperProfiles.Add(new DeveloperProfile { UserId = id, Handle = fixture.RunId + "_" + suffix, DisplayName = "Student " + suffix });
            await db.SaveChangesAsync();
        }
        var client = fixture.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, "/account/sign-in", "/account/sign-in", new()
        { ["Email"] = fixture.Email(suffix), ["Password"] = Password })).StatusCode);
        return (client, id);
    }

    private static async Task<Guid> CreateAsync(HttpClient client, Guid? notebookId = null)
    {
        var fields = new Dictionary<string, string>();
        if (notebookId is not null) fields["NotebookId"] = notebookId.Value.ToString();
        var response = await PostAsync(client, "/notes", "/notes/create", fields);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return Guid.Parse(response.Headers.Location!.OriginalString.Split('/')[2]);
    }

    private static async Task<Dictionary<string, string>> DraftAsync(HttpClient client, Guid id, string title = "Request pipeline study")
    {
        var html = await client.GetStringAsync($"/notes/{id}");
        return new()
        {
            ["Version"] = Field(html, "Version"), ["Title"] = title, ["Tags"] = "asp.net,study",
            ["Blocks[0].Id"] = Field(html, "Blocks[0].Id"), ["Blocks[0].Kind"] = "paragraph",
            ["Blocks[0].Content"] = "Middleware runs in the configured order. <script>alert(1)</script>",
            ["OwnerId"] = Guid.NewGuid().ToString()
        };
    }

    [Fact]
    public async Task Rich_text_note_blocks_preserve_json_and_render_formatting_without_replacing_code_blocks()
    {
        var (client,_) = await MemberAsync("richnotes");using var owner=client;
        var id=await CreateAsync(owner);var path=$"/notes/{id}";var fields=await DraftAsync(owner,id);
        fields["Blocks[0].Kind"]="richtext";fields["Blocks[0].Content"]=RichTextDocumentTests.Document();
        fields["Blocks[1].Id"]=Guid.NewGuid().ToString();fields["Blocks[1].Kind"]="code";fields["Blocks[1].Language"]="C#";fields["Blocks[1].Content"]="class Example {}";
        Assert.Equal(HttpStatusCode.Redirect,(await PostAsync(owner,path,path+"/save",fields)).StatusCode);
        var preview=await owner.GetStringAsync(path+"/preview");Assert.Contains("<strong>Formatted text</strong>",preview);Assert.Contains("class Example {}",preview);
        using(var scope=fixture.Factory.Services.CreateScope())
        {
            var blocks=await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyNoteBlocks.Where(b=>b.NoteId==id).OrderBy(b=>b.Position).ToListAsync();
            Assert.Equal("richtext",blocks[0].Kind);Assert.Equal(fields["Blocks[0].Content"],blocks[0].Content);Assert.Equal("code",blocks[1].Kind);
        }
        fields["Version"]=Field(await owner.GetStringAsync(path),"Version");fields["Blocks[0].Content"]="{invalid}";
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await PostAsync(owner,path,path+"/autosave",fields)).StatusCode);
        Assert.Contains("<strong>Formatted text</strong>",await owner.GetStringAsync(path+"/preview"));
    }

    [Fact]
    public async Task Code_formatting_is_owner_bound_antiforgery_protected_and_does_not_save_the_note()
    {
        var (client,_) = await MemberAsync("formatter"); using var owner=client;
        var (otherClient,_) = await MemberAsync("formatterother"); using var other=otherClient;
        var id=await CreateAsync(owner); var before=await owner.GetStringAsync($"/notes/{id}");
        var fields=new Dictionary<string,string>{["Language"]="csharp",["Code"]="public class Example{public int Value{get;set;}}"};
        var valid=await PostAsync(owner,$"/notes/{id}",$"/notes/{id}/format-code",fields);
        Assert.Equal(HttpStatusCode.OK,valid.StatusCode);
        Assert.Contains("public int Value",await valid.Content.ReadAsStringAsync());
        Assert.Equal(Field(before,"Version"),Field(await owner.GetStringAsync($"/notes/{id}"),"Version"));
        Assert.Equal(HttpStatusCode.NotFound,(await PostAsync(other,"/notes",$"/notes/{id}/format-code",fields)).StatusCode);
        fields["Code"]="public class Broken{";
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await PostAsync(owner,$"/notes/{id}",$"/notes/{id}/format-code",fields)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsync($"/notes/{id}/format-code",new FormUrlEncodedContent(new Dictionary<string,string>{["Language"]="csharp",["Code"]="class Example{}"}))).StatusCode);
    }

    [Fact]
    public async Task Personal_notes_are_private_blocks_are_ordered_and_preview_encodes_text()
    {
        var (client, ownerId) = await MemberAsync("owner"); using var owner = client;
        var (otherClient, _) = await MemberAsync("other"); using var other = otherClient;
        using var anonymous = fixture.Client();
        var notebook = await PostAsync(client, "/notes", "/notes/notebooks", new() { ["Name"] = "ASP.NET Fundamentals" });
        Assert.Equal(HttpStatusCode.Redirect, notebook.StatusCode);
        Guid notebookId;
        using (var scope = fixture.Factory.Services.CreateScope())
            notebookId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyNotebooks.Where(n => n.OwnerId == ownerId).Select(n => n.Id).SingleAsync();
        var id = await CreateAsync(client, notebookId);
        var editor = $"/notes/{id}";
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync(editor)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(editor)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(editor + "/preview")).StatusCode);
        var fields = await DraftAsync(client, id);
        fields["NotebookId"] = notebookId.ToString();
        fields["Blocks[1].Id"] = Guid.NewGuid().ToString(); fields["Blocks[1].Kind"] = "code";
        fields["Blocks[1].Content"] = "Console.WriteLine(\"<hello>\");"; fields["Blocks[1].Language"] = "csharp";
        fields["Blocks[2].Id"] = Guid.NewGuid().ToString(); fields["Blocks[2].Kind"] = "checklist";
        fields["Blocks[2].Content"] = "[x] Read the guide\n[ ] Practice locally";
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(other, "/notes", editor + "/save", fields)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, editor, editor + "/save", fields)).StatusCode);
        var preview = await client.GetStringAsync(editor + "/preview");
        Assert.Contains("Request pipeline study", preview);
        Assert.Contains("&lt;script&gt;", preview); Assert.DoesNotContain("<script>alert", preview);
        Assert.Contains("&lt;hello&gt;", preview);
        Assert.DoesNotContain("Request pipeline study", await other.GetStringAsync("/notes"));
        using var verify = fixture.Factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var note = await db.StudyNotes.SingleAsync(n => n.Id == id);
        Assert.Equal(ownerId, note.OwnerId); Assert.Equal(notebookId, note.NotebookId);
        Assert.Equal(new[] { "paragraph", "code", "checklist" }, await db.StudyNoteBlocks.Where(b => b.NoteId == id).OrderBy(b => b.Position).Select(b => b.Kind).ToArrayAsync());
    }

    [Fact]
    public async Task Saves_conflicts_limits_and_antiforgery_leave_the_winning_note_intact()
    {
        var (client, _) = await MemberAsync("save"); using var owner = client;
        var id = await CreateAsync(client);
        var editor = $"/notes/{id}";
        var first = await DraftAsync(client, id, "Winning note");
        var stale = new Dictionary<string, string>(first) { ["Title"] = "Stale note" };
        var saved = await PostAsync(client, editor, editor + "/autosave", first);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode); Assert.Contains("version", await saved.Content.ReadAsStringAsync());
        var conflict = await PostAsync(client, editor, editor + "/autosave", stale);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var formConflict = await PostAsync(client, editor, editor + "/save", stale);
        Assert.Equal(HttpStatusCode.Conflict, formConflict.StatusCode);
        Assert.Equal(stale["Version"], Field(await formConflict.Content.ReadAsStringAsync(), "Version"));
        var invalid = await DraftAsync(client, id); invalid["Blocks[0].Kind"] = "html";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, editor, editor + "/autosave", invalid)).StatusCode);
        invalid = await DraftAsync(client, id); invalid["Blocks[0].Content"] = new string('a', 30001);
        invalid["Blocks[1].Id"] = Guid.NewGuid().ToString(); invalid["Blocks[1].Kind"] = "paragraph"; invalid["Blocks[1].Content"] = new string('b', 30000);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, editor, editor + "/autosave", invalid)).StatusCode);
        invalid = await DraftAsync(client, id); invalid["Title"] = new string('x', 161);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, editor, editor + "/save", invalid)).StatusCode);
        invalid = await DraftAsync(client, id);
        for (var i = 1; i < 51; i++) { invalid[$"Blocks[{i}].Id"] = Guid.NewGuid().ToString(); invalid[$"Blocks[{i}].Kind"] = "paragraph"; }
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, editor, editor + "/autosave", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(editor + "/save", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        Assert.Equal("Winning note", (await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyNotes.SingleAsync(n => n.Id == id)).Title);
    }

    [Fact]
    public async Task Article_links_and_notebooks_enforce_owner_or_published_visibility()
    {
        var (client, ownerId) = await MemberAsync("links"); using var owner = client;
        var (otherClient, otherId) = await MemberAsync("author"); using var other = otherClient;
        var id = await CreateAsync(client); var editor = $"/notes/{id}";
        var ownerDraft = new BlogPost { AuthorId = ownerId, Title = "My private working article" };
        var otherDraft = new BlogPost { AuthorId = otherId, Title = "Hidden draft title" };
        var published = new BlogPost { AuthorId = otherId, Title = "Hidden unpublished changes", Summary = "Private next summary", Markdown = "Private",
            Slug = fixture.RunId + "-public", PublishedAt = DateTimeOffset.UtcNow, PublishedTitle = "Public snapshot article", PublishedSummary = "Public summary", PublishedMarkdown = "Public content" };
        var otherNotebook = new StudyNotebook { OwnerId = otherId, Name = "Private other notebook", NormalizedName = "private other notebook" };
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.BlogPosts.AddRange(ownerDraft, otherDraft, published); db.StudyNotebooks.Add(otherNotebook); await db.SaveChangesAsync();
        }
        var html = await client.GetStringAsync(editor);
        Assert.Contains(ownerDraft.Title, html); Assert.Contains(published.PublishedTitle!, html);
        Assert.DoesNotContain(otherDraft.Title, html); Assert.DoesNotContain(published.Title, html); Assert.DoesNotContain(otherNotebook.Name, html);
        var fields = await DraftAsync(client, id); fields["NotebookId"] = otherNotebook.Id.ToString();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, editor, editor + "/autosave", fields)).StatusCode);
        fields = await DraftAsync(client, id); fields["Blocks[0].Kind"] = "article"; fields["Blocks[0].LinkedPostId"] = otherDraft.Id.ToString();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, editor, editor + "/autosave", fields)).StatusCode);
        fields["Blocks[0].LinkedPostId"] = ownerDraft.Id.ToString();
        fields["Blocks[1].Id"] = Guid.NewGuid().ToString(); fields["Blocks[1].Kind"] = "article"; fields["Blocks[1].LinkedPostId"] = published.Id.ToString();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, editor, editor + "/save", fields)).StatusCode);
        var preview = await client.GetStringAsync(editor + "/preview");
        Assert.Contains($"/blogs/{ownerDraft.Id}/preview", preview); Assert.Contains("/blogs/" + published.Slug, preview);
        Assert.Contains(published.PublishedTitle!, preview); Assert.DoesNotContain(published.Title, preview); Assert.DoesNotContain(otherDraft.Title, preview);
    }

    [Fact]
    public async Task Server_block_actions_archive_restore_and_stale_actions_preserve_the_whole_note()
    {
        var (client, _) = await MemberAsync("actions"); using var owner = client;
        var id = await CreateAsync(client); var editor = $"/notes/{id}";
        var placeholder = await DraftAsync(client, id);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, editor, editor + "/blocks?operation=add&kind=article", placeholder)).StatusCode);
        var placeholderHtml = await client.GetStringAsync(editor);
        var placeholderId = Field(placeholderHtml, "Blocks[1].Id");
        placeholder = await DraftAsync(client, id); placeholder["Blocks[1].Id"] = placeholderId; placeholder["Blocks[1].Kind"] = "article";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, editor, editor + "/autosave", placeholder)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, editor, editor + "/blocks?operation=remove&blockId=" + placeholderId, placeholder)).StatusCode);
        var fields = await DraftAsync(client, id, "Saved while adding code");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, editor, editor + "/blocks?operation=add&kind=code", fields)).StatusCode);
        var html = await client.GetStringAsync(editor);
        var codeId = Field(html, "Blocks[1].Id");
        fields = await DraftAsync(client, id); fields["Blocks[1].Id"] = codeId; fields["Blocks[1].Kind"] = "code";
        fields["Blocks[1].Content"] = "Console.WriteLine(42);";
        var stale = new Dictionary<string, string>(fields);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, editor, editor + "/blocks?operation=up&blockId=" + codeId, fields)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, editor, editor + "/blocks?operation=remove&blockId=" + codeId, stale)).StatusCode);
        var version = Field(await client.GetStringAsync(editor), "Version");
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, editor, editor + "/archive", new() { ["Version"] = stale["Version"] })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, editor, editor + "/archive", new() { ["Version"] = version })).StatusCode);
        Assert.DoesNotContain("Request pipeline study", await client.GetStringAsync("/notes"));
        Assert.Contains("Request pipeline study", await client.GetStringAsync("/notes?archived=true"));
        var archived = await DraftAsync(client, id, "Cannot edit archive");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, editor, editor + "/autosave", archived)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, editor, editor + "/restore", new() { ["Version"] = version })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, editor, editor + "/restore", new() { ["Version"] = archived["Version"] })).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False((await db.StudyNotes.SingleAsync(n => n.Id == id)).IsArchived);
        Assert.Equal(new[] { "code", "paragraph" }, await db.StudyNoteBlocks.Where(b => b.NoteId == id).OrderBy(b => b.Position).Select(b => b.Kind).ToArrayAsync());
        Assert.Equal("Console.WriteLine(42);", await db.StudyNoteBlocks.Where(b => b.NoteId == id && b.Kind == "code").Select(b => b.Content).SingleAsync());
    }

    [Fact]
    public async Task Simultaneous_saves_allow_one_winner_without_mixing_blocks()
    {
        var (client, _) = await MemberAsync("race"); using var owner = client;
        var id = await CreateAsync(client); var editor = $"/notes/{id}";
        var initial = await DraftAsync(client, id);
        initial["Blocks[1].Id"] = Guid.NewGuid().ToString(); initial["Blocks[1].Kind"] = "code"; initial["Blocks[1].Content"] = "Initial code";
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, editor, editor + "/save", initial)).StatusCode);
        var first = await DraftAsync(client, id, "First candidate");
        first["Blocks[0].Content"] = "First content";
        first["Blocks[1].Id"] = initial["Blocks[1].Id"]; first["Blocks[1].Kind"] = "code"; first["Blocks[1].Content"] = "First code";
        first["__RequestVerificationToken"] = Field(await client.GetStringAsync(editor), "__RequestVerificationToken");
        var second = new Dictionary<string, string>(first)
        {
            ["Title"] = "Second candidate", ["Blocks[0].Id"] = first["Blocks[1].Id"], ["Blocks[0].Kind"] = "code", ["Blocks[0].Content"] = "Second code",
            ["Blocks[1].Id"] = first["Blocks[0].Id"], ["Blocks[1].Kind"] = "paragraph", ["Blocks[1].Content"] = "Second content"
        };
        var responses = await Task.WhenAll(client.PostAsync(editor + "/autosave", new FormUrlEncodedContent(first)),
            client.PostAsync(editor + "/autosave", new FormUrlEncodedContent(second)));
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(r => r.StatusCode).OrderBy(s => s).ToArray());
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var note = await db.StudyNotes.SingleAsync(n => n.Id == id);
        var blocks = await db.StudyNoteBlocks.Where(b => b.NoteId == id).OrderBy(b => b.Position).ToListAsync();
        Assert.Equal(note.Title.StartsWith("First", StringComparison.Ordinal) ? new[] { "First content", "First code" } : new[] { "Second code", "Second content" }, blocks.Select(b => b.Content).ToArray());
    }

    [Fact]
    public async Task List_search_tag_notebook_and_pagination_include_only_owned_notes()
    {
        var (client, ownerId) = await MemberAsync("filter"); using var owner = client;
        var (otherClient, otherId) = await MemberAsync("hidden"); using var other = otherClient;
        var notebook = new StudyNotebook { OwnerId = ownerId, Name = "Concurrency", NormalizedName = "concurrency" };
        var prefix = fixture.RunId + "-note";
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.StudyNotebooks.Add(notebook);
            for (var i = 0; i < 14; i++)
            {
                var note = new StudyNote { OwnerId = ownerId, NotebookId = notebook.Id, Title = prefix + i, UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(i), IsArchived = i == 13 };
                db.StudyNotes.Add(note); db.StudyNoteBlocks.Add(new StudyNoteBlock { NoteId = note.Id, Content = i == 12 ? "Unique block phrase" : "Other content" });
                db.StudyNoteTags.Add(new StudyNoteTag { NoteId = note.Id, Name = fixture.RunId });
            }
            db.StudyNotes.Add(new StudyNote { OwnerId = otherId, Title = prefix + "-secret" }); await db.SaveChangesAsync();
        }
        var page1 = await client.GetStringAsync("/notes?tag=" + fixture.RunId + "&notebookId=" + notebook.Id);
        Assert.Contains("Page 1 of 2", page1); Assert.Contains(prefix + "12", page1); Assert.DoesNotContain(prefix + "13", page1); Assert.DoesNotContain(prefix + "-secret", page1);
        var page2 = await client.GetStringAsync("/notes?tag=" + fixture.RunId + "&page=2"); Assert.Contains(prefix + "0", page2);
        var search = await client.GetStringAsync("/notes?search=Unique%20block%20phrase"); Assert.Contains(prefix + "12", search); Assert.DoesNotContain(prefix + "11", search);
        Assert.Contains("Page 1 of 2", await client.GetStringAsync("/notes?tag=" + fixture.RunId + "&page=-20"));
        Assert.Contains("Page 2 of 2", await client.GetStringAsync("/notes?tag=" + fixture.RunId + "&page=999"));
        Assert.DoesNotContain(prefix, await other.GetStringAsync("/notes?notebookId=" + notebook.Id));
        Assert.Contains(prefix + "13", await client.GetStringAsync("/notes?archived=true"));
        Assert.DoesNotContain(prefix + "13", await other.GetStringAsync("/notes?archived=true"));
    }
}
