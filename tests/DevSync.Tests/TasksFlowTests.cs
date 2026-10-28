using System.Net;
using System.Text.RegularExpressions;
using DevSync.Domain.Blogs;
using DevSync.Domain.Profiles;
using DevSync.Domain.StudyNotes;
using DevSync.Domain.Tasks;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevSync.Tests;

[Collection("PostgreSQL")]
[Trait("Category", "Database")]
public sealed class TasksFlowTests(AccountFixture fixture) : IClassFixture<AccountFixture>
{
    private const string Password = "Tasks-Test-Password42!";

    private static string Field(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success, $"Missing form field: {name}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string formPath, string target, Dictionary<string, string> fields)
    {
        var form = await client.GetAsync(formPath);
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        fields["__RequestVerificationToken"] = Field(await form.Content.ReadAsStringAsync(), "__RequestVerificationToken");
        return await client.PostAsync(target, new FormUrlEncodedContent(fields));
    }

    private static async Task<string> FollowAsync(HttpClient client, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var rendered = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, rendered.StatusCode);
        return await rendered.Content.ReadAsStringAsync();
    }

    private async Task<Guid> MemberAsync(HttpClient client, string suffix, bool moderator = false)
    {
        Guid id;
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Email = fixture.Email(suffix), UserName = fixture.Email(suffix), EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, ApplicationRoles.Member)).Succeeded);
            if (moderator) Assert.True((await users.AddToRoleAsync(user, ApplicationRoles.Moderator)).Succeeded);
            id = user.Id;
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.DeveloperProfiles.Add(new DeveloperProfile { UserId = id, Handle = fixture.RunId + "_" + suffix, DisplayName = "Planner " + suffix });
            await db.SaveChangesAsync();
        }
        await FollowAsync(client, await PostAsync(client, "/account/sign-in", "/account/sign-in", new()
            { ["Email"] = fixture.Email(suffix), ["Password"] = Password }));
        return id;
    }

    private WebApplicationFactory<Program> WithClock(RevisionTestClock clock) => fixture.Factory.WithWebHostBuilder(builder =>
        builder.ConfigureServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock); }));

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<Guid> BoardAsync(HttpClient client, string name = "Personal planning")
    {
        var response = await PostAsync(client, "/tasks", "/tasks/boards", new() { ["Name"] = name });
        await FollowAsync(client, response);
        var match = Regex.Match(response.Headers.Location!.OriginalString, "boardId=([0-9a-fA-F-]+)");
        Assert.True(match.Success, "Board redirect must identify the new board.");
        return Guid.Parse(match.Groups[1].Value);
    }

    private async Task<Guid> ColumnAsync(Guid boardId, string key)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TaskColumns
            .Where(c => c.BoardId == boardId && c.Key == key).Select(c => c.Id).SingleAsync();
    }

    private async Task<Dictionary<string, string>> NewFieldsAsync(HttpClient client, Guid boardId, string title)
    {
        var html = await client.GetStringAsync($"/tasks/create?boardId={boardId}");
        return new() { ["BoardId"] = boardId.ToString(), ["BoardVersion"] = Field(html, "BoardVersion"),
            ["Title"] = title, ["Description"] = "A practical next step.", ["Priority"] = "medium",
            ["ColumnId"] = (await ColumnAsync(boardId, "todo")).ToString(), ["AssignedToMe"] = "false" };
    }

    private async Task<Guid> TaskAsync(HttpClient client, Guid boardId, string title, Action<Dictionary<string, string>>? customize = null)
    {
        var fields = await NewFieldsAsync(client, boardId, title);
        customize?.Invoke(fields);
        var response = await PostAsync(client, $"/tasks/create?boardId={boardId}", "/tasks/create", fields);
        await FollowAsync(client, response);
        return Guid.Parse(response.Headers.Location!.OriginalString.Split('/')[2]);
    }

    private async Task<Dictionary<string, string>> EditFieldsAsync(HttpClient client, Guid id, string? title = null)
    {
        var html = await client.GetStringAsync($"/tasks/{id}/edit");
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == id);
        var fields = new Dictionary<string, string> { ["Id"] = id.ToString(), ["BoardId"] = task.BoardId.ToString(),
            ["Version"] = Field(html, "Version"), ["BoardVersion"] = Field(html, "BoardVersion"),
            ["ColumnId"] = task.ColumnId.ToString(), ["Title"] = title ?? task.Title, ["Description"] = task.Description,
            ["Priority"] = task.Priority, ["DueDate"] = task.DueDate?.ToString("yyyy-MM-dd") ?? "",
            ["AssignedToMe"] = (task.AssignedUserId == task.OwnerId).ToString().ToLowerInvariant() };
        if (task.LinkedNoteId is not null) fields["LinkedNoteId"] = task.LinkedNoteId.Value.ToString();
        if (task.LinkedPostId is not null) fields["LinkedPostId"] = task.LinkedPostId.Value.ToString();
        var items = await db.TaskChecklistItems.AsNoTracking().Where(i => i.TaskId == id).OrderBy(i => i.Position).ToListAsync();
        for (var i = 0; i < items.Count; i++)
        {
            fields[$"Checklist[{i}].Id"] = items[i].Id.ToString();
            fields[$"Checklist[{i}].Text"] = items[i].Text;
            fields[$"Checklist[{i}].IsCompleted"] = items[i].IsCompleted.ToString().ToLowerInvariant();
        }
        return fields;
    }

    private static async Task<Dictionary<string, string>> ActionFieldsAsync(HttpClient client, Guid id)
    {
        var html = await client.GetStringAsync($"/tasks/{id}");
        return new() { ["Version"] = Field(html, "Version") };
    }

    private static async Task<Dictionary<string, string>> MoveFieldsAsync(HttpClient client, Guid id, Guid columnId, int position)
    {
        var html = await client.GetStringAsync($"/tasks/{id}");
        return new() { ["Version"] = Field(html, "Version"), ["BoardVersion"] = Field(html, "BoardVersion"),
            ["ColumnId"] = columnId.ToString(), ["TargetPosition"] = position.ToString() };
    }

    private async Task AssertOrderAsync(Guid boardId, Guid columnId, params Guid[] expected)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AppDbContext>().WorkTasks.AsNoTracking()
            .Where(t => t.BoardId == boardId && t.ColumnId == columnId && !t.IsArchived).OrderBy(t => t.Position).ToListAsync();
        Assert.Equal(expected, rows.Select(t => t.Id));
        Assert.Equal(Enumerable.Range(0, expected.Length), rows.Select(t => t.Position));
    }

    [Fact]
    public async Task Personal_board_task_checklist_completion_and_archive_round_trip_render_and_persist()
    {
        using var client = fixture.Client();
        var ownerId = await MemberAsync(client, "life");
        var boardId = await BoardAsync(client, "My private project");
        var board = await client.GetStringAsync($"/tasks/board?boardId={boardId}");
        Assert.Contains("My private project", board);
        Assert.Contains("To do", board); Assert.Contains("In progress", board); Assert.Contains("Done", board);
        var id = await TaskAsync(client, boardId, "Build request pipeline", fields =>
        {
            fields["DueDate"] = "2026-10-08"; fields["Priority"] = "high"; fields["AssignedToMe"] = "true";
            fields["Checklist[0].Id"] = Guid.NewGuid().ToString(); fields["Checklist[0].Text"] = "Read the docs";
            fields["Checklist[0].IsCompleted"] = "true";
            fields["Checklist[1].Id"] = Guid.NewGuid().ToString(); fields["Checklist[1].Text"] = "Try the code";
        });
        Assert.Contains("Build request pipeline", await client.GetStringAsync("/tasks"));
        Assert.Contains("Build request pipeline", await client.GetStringAsync($"/tasks/board?boardId={boardId}"));
        var edited = await EditFieldsAsync(client, id, "Build the complete pipeline");
        edited["Description"] = "<script>alert('description')</script> Safe personal description";
        edited["Checklist[1].Text"] = "<img src=x onerror=alert(1)> Try safely";
        var detail = await FollowAsync(client, await PostAsync(client, $"/tasks/{id}/edit", $"/tasks/{id}/save", edited));
        Assert.Contains("Build the complete pipeline", detail); Assert.Contains("&lt;script&gt;", detail);
        Assert.DoesNotContain("<script>alert('description')</script>", detail);
        Assert.Contains("&lt;img", detail); Assert.DoesNotContain("<img src=x onerror=alert(1)>", detail);
        var completion = await ActionFieldsAsync(client, id);
        completion["ReturnUrl"] = "https://attacker.invalid/";
        var completedResponse = await PostAsync(client, $"/tasks/{id}", $"/tasks/{id}/complete", completion);
        Assert.Equal($"/tasks/{id}", completedResponse.Headers.Location!.OriginalString);
        await FollowAsync(client, completedResponse);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, $"/tasks/{id}", $"/tasks/{id}/complete", completion)).StatusCode);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var task = await db.WorkTasks.SingleAsync(t => t.Id == id);
            Assert.Equal(ownerId, task.OwnerId); Assert.Equal(ownerId, task.AssignedUserId);
            Assert.Equal(new DateOnly(2026, 10, 8), task.DueDate); Assert.Equal("high", task.Priority);
            Assert.NotNull(task.CompletedAt); Assert.Equal("done", (await db.TaskColumns.SingleAsync(c => c.Id == task.ColumnId)).Key);
            Assert.Equal(new[] { true, false }, await db.TaskChecklistItems.Where(i => i.TaskId == id).OrderBy(i => i.Position).Select(i => i.IsCompleted).ToArrayAsync());
        }
        Assert.Contains("Build the complete pipeline", await client.GetStringAsync("/tasks?state=completed"));
        var reopen = await ActionFieldsAsync(client, id); reopen["ReturnUrl"] = "/tasks?state=open";
        var reopenedResponse = await PostAsync(client, $"/tasks/{id}", $"/tasks/{id}/reopen", reopen);
        Assert.Equal("/tasks?state=open", reopenedResponse.Headers.Location!.OriginalString);
        await FollowAsync(client, reopenedResponse);
        await FollowAsync(client, await PostAsync(client, $"/tasks/{id}", $"/tasks/{id}/archive", await ActionFieldsAsync(client, id)));
        Assert.DoesNotContain("Build the complete pipeline", await client.GetStringAsync("/tasks?state=open"));
        Assert.Contains("Build the complete pipeline", await client.GetStringAsync("/tasks?state=archived"));
        Assert.DoesNotContain("Build the complete pipeline", await client.GetStringAsync($"/tasks/board?boardId={boardId}"));
        await FollowAsync(client, await PostAsync(client, $"/tasks/{id}", $"/tasks/{id}/restore", await ActionFieldsAsync(client, id)));
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var task = await db.WorkTasks.SingleAsync(t => t.Id == id);
            Assert.False(task.IsArchived); Assert.Null(task.CompletedAt);
            Assert.Equal("todo", (await db.TaskColumns.SingleAsync(c => c.Id == task.ColumnId)).Key);
            Assert.True(await db.TaskActivities.AnyAsync(a => a.TaskId == id && a.OwnerId == ownerId));
            Assert.Equal(2, await db.TaskChecklistItems.CountAsync(i => i.TaskId == id));
        }
        await AssertOrderAsync(boardId, await ColumnAsync(boardId, "todo"), id);
        var reloaded = await client.GetStringAsync($"/tasks/{id}");
        Assert.Contains("Read the docs", reloaded); Assert.Contains("Try safely", reloaded);
    }

    [Fact]
    public async Task Personal_boards_tasks_and_activity_are_owner_only_even_for_a_moderator()
    {
        using var owner = fixture.Client(); using var moderator = fixture.Client(); using var anonymous = fixture.Client();
        await MemberAsync(owner, "priv"); await MemberAsync(moderator, "mod", true);
        var boardId = await BoardAsync(owner, "Owner secret board");
        var id = await TaskAsync(owner, boardId, "Owner secret task");
        Assert.Equal(HttpStatusCode.NotFound, (await moderator.GetAsync($"/tasks/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await moderator.GetAsync($"/tasks/{id}/edit")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await moderator.GetAsync($"/tasks/board?boardId={boardId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await moderator.GetAsync($"/tasks/create?boardId={boardId}")).StatusCode);
        Assert.DoesNotContain("Owner secret", await moderator.GetStringAsync("/tasks"));
        var foreignList = await moderator.GetAsync($"/tasks?boardId={boardId}");
        Assert.Equal(HttpStatusCode.NotFound, foreignList.StatusCode);
        Assert.DoesNotContain("Owner secret", await foreignList.Content.ReadAsStringAsync());
        var fields = await EditFieldsAsync(owner, id, "Forged content");
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(moderator, "/tasks", $"/tasks/{id}/save", fields)).StatusCode);
        foreach (var action in new[] { "complete", "reopen", "archive", "restore" })
            Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(moderator, "/tasks", $"/tasks/{id}/{action}", new() { ["Version"] = fields["Version"] })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(moderator, "/tasks", $"/tasks/{id}/move", new()
            { ["Version"] = fields["Version"], ["BoardVersion"] = fields["BoardVersion"], ["ColumnId"] = fields["ColumnId"], ["TargetPosition"] = "0" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(moderator, "/tasks", "/tasks/create", fields)).StatusCode);
        var otherBoard = await BoardAsync(moderator, "Moderator personal board");
        var foreignColumn = await ColumnAsync(otherBoard, "todo");
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(owner, $"/tasks/{id}", $"/tasks/{id}/move",
            await MoveFieldsAsync(owner, id, foreignColumn, 0))).StatusCode);
        var foreignColumnSave = await EditFieldsAsync(owner, id); foreignColumnSave["ColumnId"] = foreignColumn.ToString();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(owner, $"/tasks/{id}/edit", $"/tasks/{id}/save", foreignColumnSave)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/tasks")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync($"/tasks/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync($"/tasks/{id}/complete", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await owner.GetAsync($"/tasks/{id}/complete")).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Owner secret task", (await db.WorkTasks.SingleAsync(t => t.Id == id)).Title);
        Assert.Single(await db.TaskActivities.Where(a => a.TaskId == id).ToListAsync());
    }

    [Fact]
    public async Task Linked_note_article_and_assignment_checks_preserve_private_titles_and_public_snapshots()
    {
        using var owner = fixture.Client(); using var other = fixture.Client();
        var ownerId = await MemberAsync(owner, "links"); var otherId = await MemberAsync(other, "author");
        var boardId = await BoardAsync(owner);
        var ownNote = new StudyNote { OwnerId = ownerId, Title = "My private linked note" };
        var otherNote = new StudyNote { OwnerId = otherId, Title = "Other hidden note" };
        var ownDraft = new BlogPost { AuthorId = ownerId, Title = "My linked working draft" };
        var otherDraft = new BlogPost { AuthorId = otherId, Title = "Other hidden draft" };
        var published = new BlogPost { AuthorId = otherId, Title = "Secret unpublished title", Summary = "Private next summary", Markdown = "Private next body",
            Slug = fixture.RunId + "-tasks-public", PublishedAt = DateTimeOffset.UtcNow, PublishedTitle = "Public linked snapshot",
            PublishedSummary = "Public summary", PublishedMarkdown = "Public body" };
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.StudyNotes.AddRange(ownNote, otherNote); db.BlogPosts.AddRange(ownDraft, otherDraft, published); await db.SaveChangesAsync();
        }
        var form = await owner.GetStringAsync($"/tasks/create?boardId={boardId}");
        Assert.Contains(ownNote.Title, form); Assert.Contains(ownDraft.Title, form); Assert.Contains(published.PublishedTitle!, form);
        Assert.DoesNotContain(otherNote.Title, form); Assert.DoesNotContain(otherDraft.Title, form); Assert.DoesNotContain(published.Title, form);
        var id = await TaskAsync(owner, boardId, "Link a useful resource", fields =>
        {
            fields["LinkedNoteId"] = ownNote.Id.ToString(); fields["LinkedPostId"] = ownDraft.Id.ToString();
            fields["AssignedToMe"] = "true"; fields["AssignedUserId"] = otherId.ToString(); fields["OwnerId"] = otherId.ToString();
        });
        var detail = await owner.GetStringAsync($"/tasks/{id}");
        Assert.Contains($"/notes/{ownNote.Id}", detail); Assert.Contains($"/blogs/{ownDraft.Id}/preview", detail);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var persisted = await scope.ServiceProvider.GetRequiredService<AppDbContext>().WorkTasks.SingleAsync(t => t.Id == id);
            Assert.Equal(ownerId, persisted.OwnerId); Assert.Equal(ownerId, persisted.AssignedUserId);
        }
        var invalid = await EditFieldsAsync(owner, id, "Must not replace the task");
        invalid["LinkedNoteId"] = otherNote.Id.ToString();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(owner, $"/tasks/{id}/edit", $"/tasks/{id}/save", invalid)).StatusCode);
        invalid = await EditFieldsAsync(owner, id); invalid["LinkedPostId"] = otherDraft.Id.ToString();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(owner, $"/tasks/{id}/edit", $"/tasks/{id}/save", invalid)).StatusCode);
        var valid = await EditFieldsAsync(owner, id); valid["LinkedPostId"] = published.Id.ToString();
        valid["AssignedToMe"] = "false"; valid["AssignedUserId"] = otherId.ToString();
        detail = await FollowAsync(owner, await PostAsync(owner, $"/tasks/{id}/edit", $"/tasks/{id}/save", valid));
        Assert.Contains(published.PublishedTitle!, detail); Assert.Contains("/blogs/" + published.Slug, detail);
        Assert.DoesNotContain(published.Title, detail); Assert.DoesNotContain(otherNote.Title, detail); Assert.DoesNotContain(otherDraft.Title, detail);
        using var scope2 = fixture.Factory.Services.CreateScope();
        var task = await scope2.ServiceProvider.GetRequiredService<AppDbContext>().WorkTasks.SingleAsync(t => t.Id == id);
        Assert.Equal(ownerId, task.OwnerId); Assert.Null(task.AssignedUserId);
        Assert.Equal(ownNote.Id, task.LinkedNoteId); Assert.Equal(published.Id, task.LinkedPostId);
        Assert.Equal("Link a useful resource", task.Title);
    }

    [Fact]
    public async Task Moves_and_reorders_persist_contiguous_positions_and_stale_board_or_task_tokens_reject_atomically()
    {
        using var client = fixture.Client(); await MemberAsync(client, "moves");
        var boardId = await BoardAsync(client); var todo = await ColumnAsync(boardId, "todo"); var doing = await ColumnAsync(boardId, "in-progress");
        var a = await TaskAsync(client, boardId, "Task A"); var b = await TaskAsync(client, boardId, "Task B");
        var c = await TaskAsync(client, boardId, "Task C");
        var d = await TaskAsync(client, boardId, "Task D", fields => fields["ColumnId"] = doing.ToString());
        await AssertOrderAsync(boardId, todo, a, b, c); await AssertOrderAsync(boardId, doing, d);
        var staleMove = await MoveFieldsAsync(client, a, doing, 0);
        await FollowAsync(client, await PostAsync(client, $"/tasks/{b}", $"/tasks/{b}/move", await MoveFieldsAsync(client, b, todo, 0)));
        await AssertOrderAsync(boardId, todo, b, a, c);
        // Keep a current task token but the board token from before the reorder.
        var boardConflict = await MoveFieldsAsync(client, a, doing, 0); boardConflict["BoardVersion"] = staleMove["BoardVersion"];
        var rejectedBoard = await PostAsync(client, $"/tasks/{a}", $"/tasks/{a}/move", boardConflict);
        Assert.Equal(HttpStatusCode.Conflict, rejectedBoard.StatusCode);
        var conflictHtml = await rejectedBoard.Content.ReadAsStringAsync();
        Assert.Equal(boardConflict["BoardVersion"], Field(conflictHtml, "BoardVersion"));
        Assert.Equal(boardConflict["Version"], Field(conflictHtml, "Version"));
        await AssertOrderAsync(boardId, todo, b, a, c); await AssertOrderAsync(boardId, doing, d);
        await FollowAsync(client, await PostAsync(client, $"/tasks/{c}", $"/tasks/{c}/move", await MoveFieldsAsync(client, c, doing, 0)));
        await AssertOrderAsync(boardId, todo, b, a); await AssertOrderAsync(boardId, doing, c, d);
        var oldTask = await MoveFieldsAsync(client, c, todo, 1);
        await FollowAsync(client, await PostAsync(client, $"/tasks/{c}/edit", $"/tasks/{c}/save", await EditFieldsAsync(client, c, "Edited C")));
        var taskConflict = await MoveFieldsAsync(client, c, todo, 1); taskConflict["Version"] = oldTask["Version"];
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, $"/tasks/{c}", $"/tasks/{c}/move", taskConflict)).StatusCode);
        await AssertOrderAsync(boardId, todo, b, a); await AssertOrderAsync(boardId, doing, c, d);
        await FollowAsync(client, await PostAsync(client, $"/tasks/{d}", $"/tasks/{d}/move?direction=up", await MoveFieldsAsync(client, d, doing, 1)));
        await AssertOrderAsync(boardId, doing, d, c);
        var rendered = await client.GetStringAsync($"/tasks/board?boardId={boardId}");
        Assert.Contains("Edited C", rendered); Assert.Contains("Task D", rendered);
        var invalid = await MoveFieldsAsync(client, a, doing, 999);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, $"/tasks/{a}", $"/tasks/{a}/move", invalid)).StatusCode);
        await AssertOrderAsync(boardId, todo, b, a); await AssertOrderAsync(boardId, doing, d, c);
    }

    [Fact]
    public async Task Simultaneous_moves_and_task_saves_allow_one_winner_without_mixing_order_or_checklists()
    {
        using var client = fixture.Client(); await MemberAsync(client, "race");
        var boardId = await BoardAsync(client); var todo = await ColumnAsync(boardId, "todo"); var doing = await ColumnAsync(boardId, "in-progress");
        var a = await TaskAsync(client, boardId, "Race A"); var b = await TaskAsync(client, boardId, "Race B");
        var firstMove = await MoveFieldsAsync(client, a, doing, 0);
        var secondMove = await MoveFieldsAsync(client, b, doing, 0);
        firstMove["__RequestVerificationToken"] = Field(await client.GetStringAsync($"/tasks/{a}"), "__RequestVerificationToken");
        secondMove["__RequestVerificationToken"] = firstMove["__RequestVerificationToken"];
        Assert.Equal(firstMove["BoardVersion"], secondMove["BoardVersion"]);
        var responses = await Task.WhenAll(client.PostAsync($"/tasks/{a}/move", new FormUrlEncodedContent(firstMove)),
            client.PostAsync($"/tasks/{b}/move", new FormUrlEncodedContent(secondMove)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Redirect);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await FollowAsync(client, responses.Single(r => r.StatusCode == HttpStatusCode.Redirect));
        var winner = responses[0].StatusCode == HttpStatusCode.Redirect ? a : b;
        await AssertOrderAsync(boardId, doing, winner); await AssertOrderAsync(boardId, todo, winner == a ? b : a);
        var taskId = await TaskAsync(client, boardId, "Checklist race", fields =>
        {
            fields["Checklist[0].Id"] = Guid.NewGuid().ToString(); fields["Checklist[0].Text"] = "Initial first";
            fields["Checklist[1].Id"] = Guid.NewGuid().ToString(); fields["Checklist[1].Text"] = "Initial second";
        });
        var first = await EditFieldsAsync(client, taskId, "First candidate");
        first["Checklist[0].Text"] = "First one"; first["Checklist[1].Text"] = "First two";
        first["__RequestVerificationToken"] = Field(await client.GetStringAsync($"/tasks/{taskId}/edit"), "__RequestVerificationToken");
        var second = new Dictionary<string, string>(first) { ["Title"] = "Second candidate",
            ["Checklist[0].Id"] = first["Checklist[1].Id"], ["Checklist[0].Text"] = "Second two",
            ["Checklist[1].Id"] = first["Checklist[0].Id"], ["Checklist[1].Text"] = "Second one" };
        var saves = await Task.WhenAll(client.PostAsync($"/tasks/{taskId}/save", new FormUrlEncodedContent(first)),
            client.PostAsync($"/tasks/{taskId}/save", new FormUrlEncodedContent(second)));
        Assert.Single(saves, r => r.StatusCode == HttpStatusCode.Redirect);
        Assert.Single(saves, r => r.StatusCode == HttpStatusCode.Conflict);
        await FollowAsync(client, saves.Single(r => r.StatusCode == HttpStatusCode.Redirect));
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await db.WorkTasks.SingleAsync(t => t.Id == taskId);
        var items = await db.TaskChecklistItems.Where(i => i.TaskId == taskId).OrderBy(i => i.Position).ToListAsync();
        Assert.Equal(task.Title == "First candidate" ? new[] { "First one", "First two" } : new[] { "Second two", "Second one" }, items.Select(i => i.Text));
        Assert.Equal(new[] { 0, 1 }, items.Select(i => i.Position));
        Assert.Equal(2, await db.TaskActivities.CountAsync(a => a.TaskId == taskId));
    }

    [Fact]
    public async Task Checklist_controls_save_current_fields_reorder_toggle_and_remove_without_accepting_stale_or_foreign_items()
    {
        using var client = fixture.Client(); await MemberAsync(client, "check");
        var boardId = await BoardAsync(client);
        var firstId = Guid.NewGuid(); var secondId = Guid.NewGuid();
        var id = await TaskAsync(client, boardId, "Checklist controls", fields =>
        {
            fields["Checklist[0].Id"] = firstId.ToString(); fields["Checklist[0].Text"] = "First item";
            fields["Checklist[1].Id"] = secondId.ToString(); fields["Checklist[1].Text"] = "Second item";
        });
        var fields = await EditFieldsAsync(client, id, "Saved while reordering");
        fields["Checklist[0].Text"] = "First revised"; fields["Checklist[1].Text"] = "Second revised";
        var stale = new Dictionary<string, string>(fields);
        await FollowAsync(client, await PostAsync(client, $"/tasks/{id}/edit", $"/tasks/{id}/checklist?operation=up&itemId={secondId}", fields));
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(client, $"/tasks/{id}/edit", $"/tasks/{id}/checklist?operation=remove&itemId={firstId}", stale)).StatusCode);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("Saved while reordering", (await db.WorkTasks.SingleAsync(t => t.Id == id)).Title);
            Assert.Equal(new[] { secondId, firstId }, await db.TaskChecklistItems.Where(i => i.TaskId == id).OrderBy(i => i.Position).Select(i => i.Id).ToArrayAsync());
        }
        var toggle = await ActionFieldsAsync(client, id); toggle["IsCompleted"] = "true";
        await FollowAsync(client, await PostAsync(client, $"/tasks/{id}", $"/tasks/{id}/checklist?operation=toggle&itemId={secondId}", toggle));
        var invalidToggle = await ActionFieldsAsync(client, id); invalidToggle["IsCompleted"] = "bad";
        int activityCount;
        using (var scope = fixture.Factory.Services.CreateScope())
            activityCount = await scope.ServiceProvider.GetRequiredService<AppDbContext>().TaskActivities.CountAsync(a => a.TaskId == id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, $"/tasks/{id}", $"/tasks/{id}/checklist?operation=toggle&itemId={secondId}", invalidToggle)).StatusCode);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True((await db.TaskChecklistItems.SingleAsync(i => i.Id == secondId)).IsCompleted);
            Assert.Equal(Guid.Parse(invalidToggle["Version"]), (await db.WorkTasks.SingleAsync(t => t.Id == id)).Version);
            Assert.Equal(activityCount, await db.TaskActivities.CountAsync(a => a.TaskId == id));
        }
        await FollowAsync(client, await PostAsync(client, $"/tasks/{id}/edit", $"/tasks/{id}/checklist?operation=down&itemId={secondId}", await EditFieldsAsync(client, id)));
        var add = await EditFieldsAsync(client, id, "Saved while adding");
        await FollowAsync(client, await PostAsync(client, $"/tasks/{id}/edit", $"/tasks/{id}/checklist?operation=add", add));
        Guid placeholder;
        using (var scope = fixture.Factory.Services.CreateScope())
            placeholder = await scope.ServiceProvider.GetRequiredService<AppDbContext>().TaskChecklistItems
                .Where(i => i.TaskId == id && i.Position == 2).Select(i => i.Id).SingleAsync();
        await FollowAsync(client, await PostAsync(client, $"/tasks/{id}/edit", $"/tasks/{id}/checklist?operation=remove&itemId={placeholder}", await EditFieldsAsync(client, id)));
        var foreignItem = Guid.NewGuid();
        var otherTask = await TaskAsync(client, boardId, "Other checklist task", f =>
            { f["Checklist[0].Id"] = foreignItem.ToString(); f["Checklist[0].Text"] = "Other task item"; });
        var forged = await EditFieldsAsync(client, id, "Must stay unchanged"); forged["Checklist[0].Id"] = foreignItem.ToString();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, $"/tasks/{id}/edit", $"/tasks/{id}/save", forged)).StatusCode);
        var oversized = await EditFieldsAsync(client, id);
        for (var i = 2; i < 31; i++) { oversized[$"Checklist[{i}].Id"] = Guid.NewGuid().ToString(); oversized[$"Checklist[{i}].Text"] = "Extra item " + i; }
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, $"/tasks/{id}/edit", $"/tasks/{id}/save", oversized)).StatusCode);
        var tooLong = await EditFieldsAsync(client, id); tooLong["Checklist[0].Text"] = new string('x', 301);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, $"/tasks/{id}/edit", $"/tasks/{id}/save", tooLong)).StatusCode);
        using var verify = fixture.Factory.Services.CreateScope();
        var verified = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Saved while adding", (await verified.WorkTasks.SingleAsync(t => t.Id == id)).Title);
        var persisted = await verified.TaskChecklistItems.Where(i => i.TaskId == id).OrderBy(i => i.Position).ToListAsync();
        Assert.Equal(new[] { firstId, secondId }, persisted.Select(i => i.Id));
        Assert.Equal(new[] { "First revised", "Second revised" }, persisted.Select(i => i.Text));
        Assert.Equal(new[] { false, true }, persisted.Select(i => i.IsCompleted));
        Assert.Equal(new[] { 0, 1 }, persisted.Select(i => i.Position));
        Assert.Equal(otherTask, (await verified.TaskChecklistItems.SingleAsync(i => i.Id == foreignItem)).TaskId);
    }

    [Fact]
    public async Task Date_filters_use_UTC_day_and_invalid_dates_do_not_clear_persisted_values()
    {
        var clock = new RevisionTestClock(new DateTimeOffset(2026, 10, 8, 0, 30, 0, TimeSpan.FromHours(5.5)));
        using var factory = WithClock(clock); using var client = Client(factory);
        await MemberAsync(client, "dates"); var boardId = await BoardAsync(client);
        var yesterday = await TaskAsync(client, boardId, "UTC yesterday", f => f["DueDate"] = "2026-10-06");
        var today = await TaskAsync(client, boardId, "UTC today", f => f["DueDate"] = "2026-10-07");
        var tomorrow = await TaskAsync(client, boardId, "UTC tomorrow", f => { f["DueDate"] = "2026-10-08"; f["AssignedToMe"] = "true"; f["Priority"] = "high"; });
        var noDate = await TaskAsync(client, boardId, "No date task");
        var completed = await TaskAsync(client, boardId, "Completed overdue", f => f["DueDate"] = "2026-10-06");
        await FollowAsync(client, await PostAsync(client, $"/tasks/{completed}", $"/tasks/{completed}/complete", await ActionFieldsAsync(client, completed)));
        var archived = await TaskAsync(client, boardId, "Archived overdue", f => f["DueDate"] = "2026-10-06");
        await FollowAsync(client, await PostAsync(client, $"/tasks/{archived}", $"/tasks/{archived}/archive", await ActionFieldsAsync(client, archived)));
        var overdue = await client.GetStringAsync("/tasks?due=overdue");
        Assert.Contains($"href=\"/tasks/{yesterday}\"", overdue); Assert.DoesNotContain($"href=\"/tasks/{today}\"", overdue);
        Assert.DoesNotContain("Completed overdue", overdue); Assert.DoesNotContain("Archived overdue", overdue);
        var dueToday = await client.GetStringAsync("/tasks?due=today");
        Assert.Contains($"href=\"/tasks/{today}\"", dueToday); Assert.DoesNotContain($"href=\"/tasks/{tomorrow}\"", dueToday);
        var future = await client.GetStringAsync("/tasks?due=upcoming");
        Assert.Contains($"href=\"/tasks/{tomorrow}\"", future); Assert.DoesNotContain($"href=\"/tasks/{today}\"", future);
        var undated = await client.GetStringAsync("/tasks?due=none");
        Assert.Contains($"href=\"/tasks/{noDate}\"", undated); Assert.DoesNotContain($"href=\"/tasks/{yesterday}\"", undated);
        Assert.Contains("UTC tomorrow", await client.GetStringAsync("/tasks?assignment=me&priority=high"));
        Assert.DoesNotContain("UTC today", await client.GetStringAsync("/tasks?assignment=me&priority=high"));
        var home = await client.GetStringAsync("/");
        Assert.Equal("4", Regex.Match(home, "data-task-count=\"open\">([0-9]+)<").Groups[1].Value);
        Assert.Equal("1", Regex.Match(home, "data-task-count=\"overdue\">([0-9]+)<").Groups[1].Value);
        var invalid = await EditFieldsAsync(client, today, "Invalid date must not save"); invalid["DueDate"] = "not-a-date";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, $"/tasks/{today}/edit", $"/tasks/{today}/save", invalid)).StatusCode);
        var badPriority = await EditFieldsAsync(client, today); badPriority["Priority"] = "critical";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, $"/tasks/{today}/edit", $"/tasks/{today}/save", badPriority)).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var task = await scope.ServiceProvider.GetRequiredService<AppDbContext>().WorkTasks.SingleAsync(t => t.Id == today);
        Assert.Equal(new DateOnly(2026, 10, 7), task.DueDate); Assert.Equal("UTC today", task.Title);
        Assert.Equal(Guid.Parse(invalid["Version"]), task.Version);
    }

    [Fact]
    public async Task List_pagination_search_and_board_capacity_are_bounded_without_partial_creation()
    {
        using var client = fixture.Client(); var ownerId = await MemberAsync(client, "limits");
        var boardId = await BoardAsync(client, "Capacity board"); var todo = await ColumnAsync(boardId, "todo");
        var overflow = await NewFieldsAsync(client, boardId, "Overflow task");
        var tasks = Enumerable.Range(0, 200).Select(i => new WorkTask { OwnerId = ownerId, BoardId = boardId, ColumnId = todo,
            Position = i, Title = "Capacity task " + i }).ToArray();
        var archived = new WorkTask { OwnerId = ownerId, BoardId = boardId, ColumnId = todo,
            Position = -1, IsArchived = true, Title = "Paused capacity task" };
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.WorkTasks.AddRange(tasks);
            db.WorkTasks.Add(archived);
            db.TaskBoards.AddRange(Enumerable.Range(1, 19).Select(i => new TaskBoard { OwnerId = ownerId, Name = "Other board " + i }));
            await db.SaveChangesAsync();
        }
        var first = await client.GetStringAsync($"/tasks?boardId={boardId}");
        Assert.Contains("Page 1 of 10", first); Assert.Contains($"href=\"/tasks/{tasks[0].Id}\"", first);
        Assert.DoesNotContain($"href=\"/tasks/{tasks[20].Id}\"", first);
        var last = await client.GetStringAsync($"/tasks?boardId={boardId}&page=999");
        Assert.Contains("Page 10 of 10", last); Assert.Contains($"href=\"/tasks/{tasks[199].Id}\"", last);
        Assert.Contains("Page 1 of 10", await client.GetStringAsync($"/tasks?boardId={boardId}&page=-10"));
        var search = await client.GetStringAsync($"/tasks?boardId={boardId}&search=Capacity%20task%20199");
        Assert.Contains($"href=\"/tasks/{tasks[199].Id}\"", search); Assert.DoesNotContain($"href=\"/tasks/{tasks[198].Id}\"", search);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, "/tasks", "/tasks/create", overflow)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, "/tasks", "/tasks/boards", new() { ["Name"] = "Twenty-first board" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(client, $"/tasks/{archived.Id}", $"/tasks/{archived.Id}/restore",
            await ActionFieldsAsync(client, archived.Id))).StatusCode);
        Assert.Contains("Capacity task 199", await client.GetStringAsync($"/tasks/board?boardId={boardId}"));
        using var verify = fixture.Factory.Services.CreateScope();
        var verified = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(201, await verified.WorkTasks.CountAsync(t => t.BoardId == boardId));
        Assert.Equal(200, await verified.WorkTasks.CountAsync(t => t.BoardId == boardId && !t.IsArchived));
        Assert.True((await verified.WorkTasks.SingleAsync(t => t.Id == archived.Id)).IsArchived);
        Assert.Equal(20, await verified.TaskBoards.CountAsync(b => b.OwnerId == ownerId));
        Assert.False(await verified.WorkTasks.AnyAsync(t => t.OwnerId == ownerId && t.Title == "Overflow task"));
        var taskIds = tasks.Select(t => t.Id).ToArray();
        Assert.False(await verified.TaskActivities.AnyAsync(a => taskIds.Contains(a.TaskId)));
    }
}
