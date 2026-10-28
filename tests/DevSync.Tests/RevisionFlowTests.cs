using System.Net;
using System.Text.RegularExpressions;
using DevSync.Domain.Profiles;
using DevSync.Domain.Revision;
using DevSync.Domain.StudyNotes;
using DevSync.Infrastructure.Identity;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevSync.Tests;

public sealed class RevisionTestClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now.ToUniversalTime();
    public override DateTimeOffset GetUtcNow() => Now;
}

[Collection("PostgreSQL")]
[Trait("Category", "Database")]
public sealed class RevisionFlowTests(AccountFixture fixture) : IClassFixture<AccountFixture>
{
    private const string Password = "Revision-Test-Password42!";
    private static string Field(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"");
        Assert.True(match.Success, $"Missing form field: {name}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static int Count(string html, string name) => int.Parse(Regex.Match(html,
        $"data-revision-count=\"{Regex.Escape(name)}\">([0-9]+)<").Groups[1].Value);

    private WebApplicationFactory<Program> WithClock(RevisionTestClock clock) => fixture.Factory.WithWebHostBuilder(builder =>
        builder.ConfigureServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock); }));

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string formPath, string target, Dictionary<string, string> fields)
    {
        var form = await client.GetAsync(formPath);
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        fields["__RequestVerificationToken"] = Field(await form.Content.ReadAsStringAsync(), "__RequestVerificationToken");
        return await client.PostAsync(target, new FormUrlEncodedContent(fields));
    }

    private async Task<Guid> MemberAsync(HttpClient client, string suffix)
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
            db.DeveloperProfiles.Add(new DeveloperProfile { UserId = id, Handle = fixture.RunId + "_" + suffix, DisplayName = "Learner " + suffix });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, "/account/sign-in", "/account/sign-in", new()
            { ["Email"] = fixture.Email(suffix), ["Password"] = Password })).StatusCode);
        return id;
    }

    private async Task<Guid> NoteAsync(Guid ownerId, string title = "Private study note", bool archived = false)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var note = new StudyNote { OwnerId = ownerId, Title = title, IsArchived = archived };
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.StudyNotes.Add(note);
        await db.SaveChangesAsync();
        return note.Id;
    }

    private static async Task<Guid> CardAsync(HttpClient client, Guid noteId, string prompt = "What is a vertical slice?", string answer = "A feature with its own request and behavior.")
    {
        var response = await PostAsync(client, $"/revision/create?noteId={noteId}", "/revision/create", new()
            { ["NoteId"] = noteId.ToString(), ["Prompt"] = prompt, ["Answer"] = answer });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var id = Guid.Parse(response.Headers.Location!.OriginalString.Split('/')[2]);
        Assert.Equal($"/revision/{id}", response.Headers.Location.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(response.Headers.Location)).StatusCode);
        return id;
    }

    private static async Task<Dictionary<string, string>> ReviewAsync(HttpClient client, Guid id, string grade)
    {
        var html = await client.GetStringAsync($"/revision/{id}");
        return new() { ["Version"] = Field(html, "Version"), ["SubmissionId"] = Field(html, "SubmissionId"),
            ["Grade"] = grade, ["__RequestVerificationToken"] = Field(html, "__RequestVerificationToken") };
    }

    [Fact]
    public async Task Creation_review_history_retry_and_future_rejection_work_end_to_end()
    {
        var clock = new RevisionTestClock(new DateTimeOffset(2026, 10, 7, 12, 30, 0, TimeSpan.Zero));
        using var factory = WithClock(clock); using var client = Client(factory);
        var ownerId = await MemberAsync(client, "review");
        var noteId = await NoteAsync(ownerId);
        const string prompt = "<script>alert('prompt')</script> Question";
        const string answer = "<img src=x onerror=alert(1)> Answer";
        var id = await CardAsync(client, noteId, prompt, answer);
        var initial = await client.GetStringAsync($"/revision/{id}");
        Assert.Contains("&lt;script&gt;", initial); Assert.DoesNotContain(prompt, initial);
        Assert.Contains("&lt;img", initial); Assert.DoesNotContain(answer, initial);
        Assert.Contains($"href=\"/notes/{noteId}\"", initial);
        Assert.Contains("Reveal answer", initial);
        Assert.Contains("no-store", (await client.GetAsync($"/revision/{id}")).Headers.CacheControl!.ToString());
        Assert.Equal(1, Count(await client.GetStringAsync("/revision"), "due"));
        var homeBefore = await client.GetStringAsync("/");
        Assert.Contains("data-learning-count=\"notes\">1<", homeBefore);
        Assert.Contains("data-learning-count=\"due\">1<", homeBefore);
        var fields = await ReviewAsync(client, id, "good");
        var graded = await client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, graded.StatusCode);
        Assert.Equal($"/revision/{id}", graded.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(graded.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(fields))).StatusCode);
        var tamperedRetry = new Dictionary<string, string>(fields) { ["Grade"] = "easy" };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(tamperedRetry))).StatusCode);
        var stale = new Dictionary<string, string>(fields) { ["SubmissionId"] = Guid.NewGuid().ToString() };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(stale))).StatusCode);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var card = await db.RevisionCards.SingleAsync(c => c.Id == id);
            Assert.Equal(ownerId, card.OwnerId); Assert.Equal(prompt, card.Prompt); Assert.Equal(answer, card.Answer);
            Assert.Equal(1, card.ReviewCount); Assert.Equal(0, card.LapseCount); Assert.Equal(3, card.IntervalDays);
            Assert.Equal(clock.Now.AddDays(3), card.DueAt);
            Assert.NotEqual(Guid.Parse(fields["Version"]), card.Version);
            var history = await db.RevisionReviews.SingleAsync(r => r.CardId == id);
            Assert.Equal(clock.Now, history.PreviousDueAt); Assert.Equal(card.DueAt, history.NextDueAt);
            Assert.Equal(clock.Now, history.ReviewedAt); Assert.Equal(RevisionGrade.Good, history.Grade);
            fields["Version"] = card.Version.ToString(); fields["SubmissionId"] = Guid.NewGuid().ToString();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(fields))).StatusCode);
        var dashboard = await client.GetStringAsync("/revision");
        Assert.Equal(0, Count(dashboard, "due")); Assert.Equal(1, Count(dashboard, "scheduled")); Assert.Equal(1, Count(dashboard, "today"));
        Assert.Contains("data-learning-count=\"due\">0<", await client.GetStringAsync("/"));
        Assert.Contains("Review scheduled", await client.GetStringAsync($"/revision/{id}"));
        Assert.Contains("Question", await client.GetStringAsync("/revision?view=scheduled"));
    }

    [Fact]
    public async Task Due_boundary_again_reset_and_next_success_are_based_on_the_injected_UTC_clock()
    {
        var clock = new RevisionTestClock(new DateTimeOffset(2026, 10, 7, 23, 55, 0, TimeSpan.Zero));
        using var factory = WithClock(clock); using var client = Client(factory);
        var ownerId = await MemberAsync(client, "boundary");
        var id = await CardAsync(client, await NoteAsync(ownerId));
        var first = await ReviewAsync(client, id, "again");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(first))).StatusCode);
        clock.Now = clock.Now.AddMinutes(10).AddMilliseconds(-1);
        var before = await client.GetStringAsync("/revision");
        Assert.Equal(0, Count(before, "due")); Assert.Equal(1, Count(before, "scheduled")); Assert.Equal(0, Count(before, "today"));
        clock.Now = clock.Now.AddMilliseconds(1);
        Assert.Equal(1, Count(await client.GetStringAsync("/revision"), "due"));
        var good = await ReviewAsync(client, id, "good");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(good))).StatusCode);
        clock.Now = clock.Now.AddDays(3);
        var hard = await ReviewAsync(client, id, "hard");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(hard))).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var card = await db.RevisionCards.SingleAsync(c => c.Id == id);
        Assert.Equal(3, card.ReviewCount); Assert.Equal(1, card.LapseCount); Assert.Equal(4, card.IntervalDays);
        Assert.Equal(clock.Now.AddDays(4), card.DueAt);
        var reviews = await db.RevisionReviews.Where(r => r.CardId == id).OrderBy(r => r.ReviewedAt).ToListAsync();
        Assert.Equal(new[] { RevisionGrade.Again, RevisionGrade.Good, RevisionGrade.Hard }, reviews.Select(r => r.Grade));
        Assert.Equal(reviews[0].NextDueAt, reviews[1].PreviousDueAt);
        Assert.Equal(reviews[1].NextDueAt, reviews[2].PreviousDueAt);
    }

    [Fact]
    public async Task Cards_and_history_are_owner_only_and_archived_notes_suspend_reviews_and_creation()
    {
        using var owner = fixture.Client(); using var stranger = fixture.Client(); using var anonymous = fixture.Client();
        var ownerId = await MemberAsync(owner, "private"); var strangerId = await MemberAsync(stranger, "stranger");
        var noteId = await NoteAsync(ownerId, "Owner secret note");
        var id = await CardAsync(owner, noteId, "Owner secret question", "Owner secret answer");
        var review = await ReviewAsync(owner, id, "again");
        Assert.Equal(HttpStatusCode.Redirect, (await owner.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(review))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/revision/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/revision/create?noteId={noteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(stranger, "/revision", "/revision/create", new()
            { ["NoteId"] = noteId.ToString(), ["Prompt"] = "Forged", ["Answer"] = "Forged", ["OwnerId"] = strangerId.ToString() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(stranger, "/revision", $"/revision/{id}/review", review)).StatusCode);
        Assert.DoesNotContain("Owner secret", await stranger.GetStringAsync("/revision"));
        var strangerHome = await stranger.GetStringAsync("/");
        Assert.Contains("data-learning-count=\"notes\">0<", strangerHome);
        Assert.Contains("data-learning-count=\"due\">0<", strangerHome);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/revision")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync($"/revision/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
        var noteForm = await owner.GetStringAsync($"/notes/{noteId}");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(owner, $"/notes/{noteId}", $"/notes/{noteId}/archive", new()
            { ["Version"] = Field(noteForm, "Version") })).StatusCode);
        var dashboard = await owner.GetStringAsync("/revision");
        Assert.Contains("1 cards are paused", dashboard); Assert.Contains("Owner secret question", dashboard);
        Assert.Equal(0, Count(dashboard, "due")); Assert.Equal(0, Count(dashboard, "scheduled"));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/revision/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/revision/create?noteId={noteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(owner, "/revision", $"/revision/{id}/review", review)).StatusCode);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.RevisionReviews.CountAsync(r => r.CardId == id));
            var version = await db.StudyNotes.Where(n => n.Id == noteId).Select(n => n.Version).SingleAsync();
            Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(owner, "/revision", $"/notes/{noteId}/restore", new() { ["Version"] = version.ToString() })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/revision/{id}")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_reviews_allow_one_distinct_winner_and_duplicate_retries_write_only_one_history_row()
    {
        using var client = fixture.Client();
        var ownerId = await MemberAsync(client, "race"); var noteId = await NoteAsync(ownerId);
        var id = await CardAsync(client, noteId);
        var first = await ReviewAsync(client, id, "good");
        var second = new Dictionary<string, string>(first) { ["SubmissionId"] = Guid.NewGuid().ToString(), ["Grade"] = "easy" };
        var responses = await Task.WhenAll(client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(first)),
            client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(second)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Redirect);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var duplicateId = await CardAsync(client, noteId, "Retry this card");
        var duplicate = await ReviewAsync(client, duplicateId, "again");
        var retries = await Task.WhenAll(client.PostAsync($"/revision/{duplicateId}/review", new FormUrlEncodedContent(duplicate)),
            client.PostAsync($"/revision/{duplicateId}/review", new FormUrlEncodedContent(duplicate)));
        Assert.All(retries, r => Assert.Equal(HttpStatusCode.Redirect, r.StatusCode));
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var winner = await db.RevisionReviews.SingleAsync(r => r.CardId == id);
        Assert.Equal(responses[0].StatusCode == HttpStatusCode.Redirect ? RevisionGrade.Good : RevisionGrade.Easy, winner.Grade);
        Assert.Equal(1, (await db.RevisionCards.SingleAsync(c => c.Id == id)).ReviewCount);
        Assert.Equal(1, await db.RevisionReviews.CountAsync(r => r.CardId == duplicateId));
        var retriedCard = await db.RevisionCards.SingleAsync(c => c.Id == duplicateId);
        Assert.Equal(1, retriedCard.ReviewCount); Assert.Equal(1, retriedCard.LapseCount);
    }

    [Fact]
    public async Task Invalid_fields_and_grades_leave_card_and_history_unchanged_and_database_rejects_cross_owner_links()
    {
        using var client = fixture.Client(); using var other = fixture.Client();
        var ownerId = await MemberAsync(client, "validation"); var otherId = await MemberAsync(other, "other");
        var noteId = await NoteAsync(ownerId);
        var invalid = await PostAsync(client, $"/revision/create?noteId={noteId}", "/revision/create", new()
            { ["NoteId"] = noteId.ToString(), ["Prompt"] = new string('x', 501), ["Answer"] = "Answer" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var id = await CardAsync(client, noteId);
        var fields = await ReviewAsync(client, id, "Good");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(fields))).StatusCode);
        fields["Grade"] = "999";
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(fields))).StatusCode);
        fields["Grade"] = "good"; fields["SubmissionId"] = Guid.Empty.ToString();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsync($"/revision/{id}/review", new FormUrlEncodedContent(fields))).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.RevisionCards.CountAsync(c => c.OwnerId == ownerId));
        Assert.Equal(0, (await db.RevisionCards.SingleAsync(c => c.Id == id)).ReviewCount);
        Assert.False(await db.RevisionReviews.AnyAsync(r => r.CardId == id));
        db.RevisionCards.Add(new RevisionCard { OwnerId = otherId, NoteId = noteId, Prompt = "Forbidden", Answer = "Forbidden" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Dashboard_filters_paginates_and_counts_due_and_UTC_review_day_boundaries()
    {
        var clock = new RevisionTestClock(new DateTimeOffset(2026, 10, 7, 23, 59, 59, TimeSpan.Zero));
        using var factory = WithClock(clock); using var client = Client(factory);
        var ownerId = await MemberAsync(client, "pages"); var noteId = await NoteAsync(ownerId);
        var archivedNote = await NoteAsync(ownerId, "Archived", true);
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cards = Enumerable.Range(0, 13).Select(i => new RevisionCard { OwnerId = ownerId, NoteId = noteId,
                Prompt = "Queue question " + i, Answer = "Answer", DueAt = clock.Now.AddMinutes(-i) }).ToArray();
            db.RevisionCards.AddRange(cards);
            db.RevisionCards.Add(new RevisionCard { OwnerId = ownerId, NoteId = noteId, Prompt = "First future", Answer = "Answer", DueAt = clock.Now.AddMilliseconds(1) });
            db.RevisionCards.Add(new RevisionCard { OwnerId = ownerId, NoteId = noteId, Prompt = "Later future", Answer = "Answer", DueAt = clock.Now.AddDays(1) });
            db.RevisionCards.Add(new RevisionCard { OwnerId = ownerId, NoteId = archivedNote, Prompt = "Paused question", Answer = "Answer", DueAt = clock.Now });
            var day = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
            var reviewTimes = new[] { day, clock.Now, day.AddSeconds(-1), day.AddDays(1) }
                .Concat(Enumerable.Range(0, 8).Select(i => day.AddDays(-2).AddMinutes(i))).ToArray();
            cards[0].ReviewCount = reviewTimes.Length;
            foreach (var time in reviewTimes)
                db.RevisionReviews.Add(new RevisionReview { CardId = cards[0].Id, OwnerId = ownerId, Grade = RevisionGrade.Good,
                    SubmissionId = Guid.NewGuid(), SubmittedVersion = Guid.NewGuid(), ReviewedAt = time,
                    PreviousDueAt = time, NextDueAt = time.AddDays(3), IntervalDays = 3 });
            await db.SaveChangesAsync();
        }
        var page1 = await client.GetStringAsync("/revision");
        Assert.Equal(13, Count(page1, "due")); Assert.Equal(2, Count(page1, "scheduled")); Assert.Equal(2, Count(page1, "today"));
        Assert.Contains("1 cards are paused", page1); Assert.Contains("Page 1 of 2", page1);
        Assert.Contains("Queue question 12", page1);
        var page2 = await client.GetStringAsync("/revision?page=999&historyPage=999");
        Assert.Contains("Page 2 of 2", page2); Assert.Contains("Queue question 0", page2);
        var scheduled = await client.GetStringAsync("/revision?view=scheduled");
        Assert.Contains("First future", scheduled); Assert.Contains("Later future", scheduled); Assert.DoesNotContain("Paused question", scheduled);
        Assert.Contains("Page 1 of 2", await client.GetStringAsync("/revision?page=-100&historyPage=-100"));
    }
}
