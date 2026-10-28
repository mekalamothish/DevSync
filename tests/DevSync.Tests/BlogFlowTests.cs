using System.Net;
using System.Text.RegularExpressions;
using DevSync.Domain.Blogs;
using DevSync.Domain.Profiles;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevSync.Tests;

[Collection("PostgreSQL")]
[Trait("Category", "Database")]
public sealed class BlogFlowTests(AccountFixture fixture) : IClassFixture<AccountFixture>
{
    private const string Password = "Blog-Test-Password42!";
    private static async Task CaptureAsync(string name, string html)
    {
        if (Environment.GetEnvironmentVariable("DEVSYNC_CAPTURE_UI") != "1") return;
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/blog-review"));
        Directory.CreateDirectory(directory);
        html = Regex.Replace(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", "");
        // These snapshots are for visual review after their test users are removed.
        // Disable writes/autosave, and omit the token that was used by the test client.
        html = Regex.Replace(html, "<script[^>]*src=\"[^\"]*(?:blog-editor|note-editor|rich-text|note-code).js[^\"]*\"[^>]*></script>", "");
        html = Regex.Replace(html, "(<button[^>]*type=\"submit\")", "$1 disabled");
        await File.WriteAllTextAsync(Path.Combine(directory, name + ".html"), html);
    }
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
            db.DeveloperProfiles.Add(new DeveloperProfile { UserId = id, Handle = fixture.RunId + "_" + suffix, DisplayName = "Author " + suffix });
            await db.SaveChangesAsync();
        }
        var client = fixture.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, "/account/sign-in", "/account/sign-in", new()
        { ["Email"] = fixture.Email(suffix), ["Password"] = Password })).StatusCode);
        return (client, id);
    }

    private static async Task<Guid> CreateAsync(HttpClient client)
    {
        var response = await PostAsync(client, "/blogs/mine", "/blogs/create", new());
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return Guid.Parse(response.Headers.Location!.OriginalString.Split('/')[2]);
    }

    private static async Task<Dictionary<string, string>> DraftAsync(HttpClient client, Guid id, string title = "Middleware explained")
    {
        var html = await client.GetStringAsync($"/blogs/{id}/edit");
        return new()
        {
            ["Version"] = Field(html, "Version"), ["Title"] = title, ["Summary"] = "A practical guide to request pipelines.",
            ["Markdown"] = "# Request pipelines\nUse `app.Use` to add middleware.\n```csharp\nConsole.WriteLine(\"<hello>\");\n```\n<script>alert(1)</script>\n[Unsafe](javascript:alert)\n[Docs](https://example.com/docs)\n![Request delegate pipeline](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/index/_static/request-delegate-pipeline.png)",
            ["Tags"] = "ASP.NET, c#, architecture", ["AuthorId"] = Guid.NewGuid().ToString()
        };
    }

    [Fact]
    public async Task Rich_articles_publish_without_markdown_and_public_block_links_never_expose_private_drafts()
    {
        var (client,_)=await MemberAsync("richpublish");using var owner=client;
        var source=await CreateAsync(owner);var sourceFields=await DraftAsync(owner,source,"Private source title");
        Assert.Equal(HttpStatusCode.Redirect,(await PostAsync(owner,$"/blogs/{source}/edit",$"/blogs/{source}/save",sourceFields)).StatusCode);
        var id=await CreateAsync(owner);var path=$"/blogs/{id}/edit";var fields=await DraftAsync(owner,id);
        fields["Markdown"]="";fields["EditorFormat"]="richtext";fields["RichText"]="{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\"}]}";
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await PostAsync(owner,path,$"/blogs/{id}/publish",fields)).StatusCode);
        fields["RichText"]=RichTextDocumentTests.Document();
        var response=await PostAsync(owner,path,$"/blogs/{id}/publish",fields);Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        var publishedUrl=response.Headers.Location!;
        fields["Version"]=Field(await owner.GetStringAsync(path),"Version");fields["EditorFormat"]="blocks";
        fields["Blocks[0].Id"]=Guid.NewGuid().ToString();fields["Blocks[0].Kind"]="article";fields["Blocks[0].LinkedPostId"]=source.ToString();
        fields["Blocks[0].Content"]="Private article annotation";
        response=await PostAsync(owner,path,$"/blogs/{id}/publish",fields);Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        Assert.Contains("Private source title",await owner.GetStringAsync($"/blogs/{id}/preview"));
        using var reader=fixture.Client();var html=await reader.GetStringAsync(publishedUrl);
        Assert.DoesNotContain("Private source title",html);Assert.DoesNotContain("Private article annotation",html);Assert.Contains("Linked article is unavailable",html);
        fields["Version"]=Field(await owner.GetStringAsync(path),"Version");fields["kind"]="richtext";
        Assert.Equal(HttpStatusCode.Redirect,(await PostAsync(owner,path,$"/blogs/{id}/blocks?operation=add",fields)).StatusCode);
        Assert.Contains("data-block-kind=\"richtext\"",await owner.GetStringAsync(path));
        var version=Field(await owner.GetStringAsync(path),"Version");
        Assert.Equal(HttpStatusCode.OK,(await PostAsync(owner,path,$"/blogs/{id}/format-code",new(){["Language"]="csharp",["Code"]="class Example{int x=1;}"})).StatusCode);
        Assert.Equal(version,Field(await owner.GetStringAsync(path),"Version"));
    }

    [Fact]
    public async Task Rich_text_and_blocks_round_trip_without_overwriting_markdown_or_published_snapshots()
    {
        var (author,authorId)=await MemberAsync("richauthor");using var owner=author;
        var (outsider,_)=await MemberAsync("richoutsider");using var other=outsider;
        var id=await CreateAsync(owner);var path=$"/blogs/{id}/edit";
        var fields=await DraftAsync(owner,id,"Editors round trip");var markdown=fields["Markdown"];
        fields["EditorFormat"]="richtext";fields["RichText"]=RichTextDocumentTests.Document();
        fields["Blocks[0].Id"]=Guid.NewGuid().ToString();fields["Blocks[0].Kind"]="code";fields["Blocks[0].Language"]="C#";fields["Blocks[0].Content"]="Console.WriteLine(\"<safe>\");";
        Assert.Equal(HttpStatusCode.Redirect,(await PostAsync(owner,path,$"/blogs/{id}/save",fields)).StatusCode);
        var preview=await owner.GetStringAsync($"/blogs/{id}/preview");Assert.Contains("<strong>Formatted text</strong>",preview);Assert.DoesNotContain("Request pipelines",preview);
        string slug;
        using(var scope=fixture.Factory.Services.CreateScope())
        {
            var post=await scope.ServiceProvider.GetRequiredService<AppDbContext>().BlogPosts.SingleAsync(p=>p.Id==id);
            Assert.Equal(markdown,post.Markdown);Assert.Equal(fields["RichText"],post.RichText);Assert.Contains("Console.WriteLine",post.BlocksJson);
        }
        var stale=fields["Version"];fields["Version"]=Field(await owner.GetStringAsync(path),"Version");
        Assert.Equal(HttpStatusCode.Redirect,(await PostAsync(owner,path,$"/blogs/{id}/publish",fields)).StatusCode);
        using(var scope=fixture.Factory.Services.CreateScope())slug=(await scope.ServiceProvider.GetRequiredService<AppDbContext>().BlogPosts.SingleAsync(p=>p.Id==id)).Slug!;
        fields["Version"]=Field(await owner.GetStringAsync(path),"Version");fields["EditorFormat"]="blocks";
        Assert.Equal(HttpStatusCode.OK,(await PostAsync(owner,path,$"/blogs/{id}/autosave",fields)).StatusCode);
        preview=await owner.GetStringAsync($"/blogs/{id}/preview");Assert.Contains("data-highlight-code=\"C#\"",preview);Assert.Contains("&lt;safe&gt;",preview);
        using var reader=fixture.Client();var published=await reader.GetStringAsync("/blogs/"+slug);Assert.Contains("<strong>Formatted text</strong>",published);Assert.DoesNotContain("Console.WriteLine",published);
        fields["Version"]=Field(await owner.GetStringAsync(path),"Version");fields["EditorFormat"]="markdown";
        Assert.Equal(HttpStatusCode.Redirect,(await PostAsync(owner,path,$"/blogs/{id}/save",fields)).StatusCode);
        Assert.Contains("Request pipelines",await owner.GetStringAsync($"/blogs/{id}/preview"));
        Assert.Equal(HttpStatusCode.NotFound,(await PostAsync(other,"/blogs/mine",$"/blogs/{id}/save",fields)).StatusCode);
        fields["Version"]=stale;Assert.Equal(HttpStatusCode.Conflict,(await PostAsync(owner,path,$"/blogs/{id}/save",fields)).StatusCode);
        fields["Version"]=Field(await owner.GetStringAsync(path),"Version");fields["RichText"]="{bad}";
        Assert.Equal(HttpStatusCode.UnprocessableEntity,(await PostAsync(owner,path,$"/blogs/{id}/save",fields)).StatusCode);
        using var verify=fixture.Factory.Services.CreateScope();Assert.Equal(authorId,(await verify.ServiceProvider.GetRequiredService<AppDbContext>().BlogPosts.SingleAsync(p=>p.Id==id)).AuthorId);
    }

    [Fact]
    public async Task Drafts_are_private_publish_is_stable_and_draft_edits_do_not_change_public_snapshot()
    {
        var (author, authorId) = await MemberAsync("lifecycle"); using var authorClient = author;
        var (other, _) = await MemberAsync("other"); using var otherClient = other;
        using var anonymous = fixture.Client();
        var id = await CreateAsync(author);
        var editor = $"/blogs/{id}/edit";
        Assert.Contains("Private drafts", await author.GetStringAsync("/blogs/mine"));
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync(editor)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(editor)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/blogs/{id}/preview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/blogs/{id}")).StatusCode);
        var fields = await DraftAsync(author, id);
        var saved = await PostAsync(author, editor, $"/blogs/{id}/save", fields);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var preview = await author.GetStringAsync($"/blogs/{id}/preview");
        await CaptureAsync("editor", await author.GetStringAsync(editor));
        await CaptureAsync("dashboard", await author.GetStringAsync("/blogs/mine"));
        Assert.Contains("Private preview", preview);
        Assert.Contains("&lt;script&gt;", preview);
        Assert.DoesNotContain("<script>alert", preview);
        Assert.DoesNotContain("href=\"javascript:", preview);
        Assert.Contains("<pre><code>", preview);
        Assert.Contains("<img src=\"https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/index/_static/request-delegate-pipeline.png\"", preview);
        Assert.Contains("href=\"https://example.com/docs\"", preview);
        Assert.DoesNotContain("Middleware explained", await anonymous.GetStringAsync("/blogs"));
        fields = await DraftAsync(author, id);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(other, "/blogs/mine", $"/blogs/{id}/publish", fields)).StatusCode);
        var published = await PostAsync(author, editor, $"/blogs/{id}/publish", fields);
        Assert.Equal(HttpStatusCode.Redirect, published.StatusCode);
        var url = published.Headers.Location!.OriginalString;
        Assert.Contains("Middleware explained", await anonymous.GetStringAsync(url));
        await CaptureAsync("reader", await anonymous.GetStringAsync(url));
        await CaptureAsync("explore", await anonymous.GetStringAsync("/blogs"));
        Assert.Contains("Middleware explained", await anonymous.GetStringAsync("/"));
        fields = await DraftAsync(author, id, "Private next title");
        fields["Tags"] = "private-tag";
        fields["Summary"] = "Private summary";
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(author, editor, $"/blogs/{id}/save", fields)).StatusCode);
        var publicCopy = await anonymous.GetStringAsync(url);
        Assert.Contains("alt=\"Request delegate pipeline\"", publicCopy);
        Assert.DoesNotContain("Private next title", publicCopy);
        Assert.DoesNotContain("Private summary", publicCopy);
        Assert.DoesNotContain("private-tag", publicCopy);
        Assert.DoesNotContain("Middleware explained", await anonymous.GetStringAsync("/blogs?tag=private-tag"));
        Assert.Contains("unpublished edits", await author.GetStringAsync("/blogs/mine"));
        fields = await DraftAsync(author, id, "A new public title");
        var republish = await PostAsync(author, editor, $"/blogs/{id}/publish", fields);
        Assert.Equal(url, republish.Headers.Location!.OriginalString);
        Assert.Contains("A new public title", await anonymous.GetStringAsync(url));
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(authorId, (await db.BlogPosts.SingleAsync(p => p.Id == id)).AuthorId);
        Assert.Equal(new[] { "architecture", "asp.net", "c#" }, await db.BlogTags.Where(t => t.PostId == id).OrderBy(t => t.Name).Select(t => t.Name).ToArrayAsync());
    }

    [Fact]
    public async Task Autosave_conflicts_validation_and_antiforgery_preserve_the_latest_draft()
    {
        var (client, _) = await MemberAsync("autosave"); using var owner = client;
        var id = await CreateAsync(client);
        var editor = $"/blogs/{id}/edit";
        var first = await DraftAsync(client, id, "Winning draft");
        var stale = new Dictionary<string, string>(first) { ["Title"] = "Stale draft" };
        var saved = await PostAsync(client, editor, $"/blogs/{id}/autosave", first);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Contains("version", await saved.Content.ReadAsStringAsync());
        var conflict = await PostAsync(client, editor, $"/blogs/{id}/autosave", stale);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("changed in another tab", await conflict.Content.ReadAsStringAsync());
        var formConflict = await PostAsync(client, editor, $"/blogs/{id}/save", stale);
        Assert.Equal(HttpStatusCode.Conflict, formConflict.StatusCode);
        Assert.Equal(stale["Version"], Field(await formConflict.Content.ReadAsStringAsync(), "Version"));
        var invalid = await DraftAsync(client, id); invalid["Title"] = new string('x', 161);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, editor, $"/blogs/{id}/autosave", invalid)).StatusCode);
        invalid = await DraftAsync(client, id); invalid["Tags"] = "bad tag";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, editor, $"/blogs/{id}/publish", invalid)).StatusCode);
        invalid = await DraftAsync(client, id); invalid["Summary"] = "";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, editor, $"/blogs/{id}/publish", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/blogs/{id}/save", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var post = await scope.ServiceProvider.GetRequiredService<AppDbContext>().BlogPosts.SingleAsync(p => p.Id == id);
        Assert.Equal("Winning draft", post.Title);
        Assert.Null(post.Slug);
    }

    [Fact]
    public async Task Simultaneous_saves_allow_one_winner_and_do_not_overwrite_it()
    {
        var (client, _) = await MemberAsync("race"); using var owner = client;
        var id = await CreateAsync(client);
        var editor = $"/blogs/{id}/edit";
        var first = await DraftAsync(client, id, "First candidate");
        first["__RequestVerificationToken"] = Field(await client.GetStringAsync(editor), "__RequestVerificationToken");
        var second = new Dictionary<string, string>(first) { ["Title"] = "Second candidate" };
        var responses = await Task.WhenAll(
            client.PostAsync($"/blogs/{id}/autosave", new FormUrlEncodedContent(first)),
            client.PostAsync($"/blogs/{id}/autosave", new FormUrlEncodedContent(second)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        using var scope = fixture.Factory.Services.CreateScope();
        var title = await scope.ServiceProvider.GetRequiredService<AppDbContext>().BlogPosts.Where(p => p.Id == id).Select(p => p.Title).SingleAsync();
        Assert.Equal(responses[0].StatusCode == HttpStatusCode.OK ? "First candidate" : "Second candidate", title);
    }

    [Fact]
    public async Task Reader_actions_are_idempotent_and_comments_are_encoded_and_owned_by_the_writer()
    {
        var (author, _) = await MemberAsync("socialauthor"); using var a = author;
        var (reader, readerId) = await MemberAsync("reader"); using var r = reader;
        var id = await CreateAsync(author);
        var editor = $"/blogs/{id}/edit";
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(reader, "/blogs/mine", $"/blogs/{id}/react", new() { ["enabled"] = "true" })).StatusCode);
        var published = await PostAsync(author, editor, $"/blogs/{id}/publish", await DraftAsync(author, id, "Reader article"));
        var url = published.Headers.Location!.OriginalString;
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(reader, url, $"/blogs/{id}/react", new() { ["enabled"] = "true" })).StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(reader, url, $"/blogs/{id}/bookmark", new() { ["enabled"] = "true" })).StatusCode);
        }
        Assert.Contains("Reader article", await reader.GetStringAsync("/blogs/bookmarks"));
        const string comment = "<img src=x onerror=alert(1)> Nice work!";
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(reader, url, $"/blogs/{id}/comments", new() { ["Body"] = comment, ["UserId"] = Guid.NewGuid().ToString() })).StatusCode);
        var read = await reader.GetStringAsync(url);
        Assert.Contains("&lt;img", read); Assert.DoesNotContain(comment, read);
        Guid commentId;
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.BlogReactions.CountAsync(x => x.PostId == id));
            Assert.Equal(1, await db.BlogBookmarks.CountAsync(x => x.PostId == id));
            var persisted = await db.BlogComments.SingleAsync(x => x.PostId == id);
            Assert.Equal(readerId, persisted.UserId); commentId = persisted.Id;
        }
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(author, url, $"/blogs/{id}/comments/{commentId}/delete", new())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(reader, url, $"/blogs/{id}/comments/{commentId}/delete", new())).StatusCode);
        await PostAsync(reader, url, $"/blogs/{id}/react", new() { ["enabled"] = "false" });
        await PostAsync(reader, url, $"/blogs/{id}/bookmark", new() { ["enabled"] = "false" });
        Assert.DoesNotContain("Reader article", await reader.GetStringAsync("/blogs/bookmarks"));
        Assert.Equal(HttpStatusCode.BadRequest, (await reader.PostAsync($"/blogs/{id}/comments", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.PostAsync($"/blogs/{id}/react", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
    }

    [Fact]
    public async Task Explore_filters_and_paginates_public_content_and_dashboard_counts_real_articles()
    {
        var (client, authorId) = await MemberAsync("pagination"); using var owner = client;
        var prefix = fixture.RunId + "-article";
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 14; i++)
            {
                var post = new BlogPost { AuthorId = authorId, Title = prefix + i, Summary = "Query fixture", Markdown = "Content" };
                if (i < 13)
                {
                    post.PublishedAt = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(i); post.Slug = prefix + i;
                    post.PublishedTitle = post.Title; post.PublishedSummary = post.Summary; post.PublishedMarkdown = post.Markdown;
                    db.BlogTags.Add(new BlogTag { PostId = post.Id, Name = fixture.RunId }); post.DraftTags = fixture.RunId;
                }
                db.BlogPosts.Add(post);
            }
            await db.SaveChangesAsync();
        }
        using var visitor = fixture.Client();
        var page1 = await visitor.GetStringAsync("/blogs?tag=" + fixture.RunId);
        Assert.Contains("Page 1 of 2", page1); Assert.Contains(prefix + "12", page1);
        Assert.DoesNotContain(prefix + "13", page1); Assert.DoesNotContain($"href=\"/blogs/{prefix}0\"", page1);
        var page2 = await visitor.GetStringAsync("/blogs?tag=" + fixture.RunId + "&page=2");
        Assert.Contains($"href=\"/blogs/{prefix}0\"", page2);
        var filtered = await visitor.GetStringAsync("/blogs?search=" + prefix + "12");
        Assert.Contains(prefix + "12", filtered); Assert.DoesNotContain($"href=\"/blogs/{prefix}11\"", filtered);
        Assert.Contains("Page 1 of 2", await visitor.GetStringAsync("/blogs?tag=" + fixture.RunId + "&page=-100"));
        Assert.Contains("Page 2 of 2", await visitor.GetStringAsync("/blogs?tag=" + fixture.RunId + "&page=999"));
        Assert.Contains("1 private drafts", await client.GetStringAsync("/"));
        Assert.Contains("13 published articles", await client.GetStringAsync("/"));
        Assert.Contains(prefix + "13", await client.GetStringAsync("/blogs/mine"));
    }
}
