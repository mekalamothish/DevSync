using System.ComponentModel.DataAnnotations;
using DevSync.Domain.Revision;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Revision.Review;

public sealed class Command
{
    public Guid Version { get; set; }
    public Guid SubmissionId { get; set; }
    [Required, RegularExpression("^(again|hard|good|easy)$")]
    public string Grade { get; set; } = "";
}

public sealed record HistoryItem(RevisionGrade Grade, DateTimeOffset ReviewedAt, DateTimeOffset NextDueAt);
public sealed record GradeChoice(string Value, string Label, string NextReview);
public sealed class ViewModel
{
    public Guid Id { get; init; }
    public Guid NoteId { get; init; }
    public string NoteTitle { get; init; } = "";
    public string Prompt { get; init; } = "";
    public string Answer { get; init; } = "";
    public DateTimeOffset DueAt { get; init; }
    public bool CanReview { get; init; }
    public int ReviewCount { get; init; }
    public int LapseCount { get; init; }
    public Guid Version { get; set; }
    public Guid SubmissionId { get; set; } = Guid.NewGuid();
    public IReadOnlyList<GradeChoice> Choices { get; init; } = [];
    public IReadOnlyList<HistoryItem> History { get; init; } = [];
    public int HistoryPage { get; init; }
    public int HistoryPages { get; init; }
}

public sealed record ReviewResult(int Status, string? Error = null, bool Duplicate = false);

public sealed class Handler(AppDbContext db, TimeProvider clock)
{
    public async Task<ViewModel?> LoadAsync(Guid id, Guid ownerId, int historyPage, CancellationToken ct)
    {
        var card = await (from c in db.RevisionCards.AsNoTracking()
            join n in db.StudyNotes.AsNoTracking() on c.NoteId equals n.Id
            where c.Id == id && c.OwnerId == ownerId && n.OwnerId == ownerId && !n.IsArchived
            select new { Card = c, NoteTitle = n.Title }).SingleOrDefaultAsync(ct);
        if (card is null) return null;
        var reviews = db.RevisionReviews.AsNoTracking().Where(r => r.CardId == id && r.OwnerId == ownerId);
        var count = await reviews.CountAsync(ct);
        var pages = Math.Max(1, (count + 9) / 10);
        var page = Math.Clamp(historyPage, 1, pages);
        var history = await reviews.OrderByDescending(r => r.ReviewedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * 10).Take(10).Select(r => new HistoryItem(r.Grade, r.ReviewedAt, r.NextDueAt)).ToListAsync(ct);
        var now = clock.GetUtcNow().ToUniversalTime();
        var choices = Enum.GetValues<RevisionGrade>().Select(grade =>
        {
            var schedule = RevisionSchedule.Next(card.Card.IntervalDays, grade, now);
            var next = grade == RevisionGrade.Again ? "10 minutes" : schedule.IntervalDays == 1 ? "1 day" : $"{schedule.IntervalDays} days";
            return new GradeChoice(grade.ToString().ToLowerInvariant(), grade.ToString(), next);
        }).ToArray();
        return new ViewModel { Id = id, NoteId = card.Card.NoteId, NoteTitle = card.NoteTitle,
            Prompt = card.Card.Prompt, Answer = card.Card.Answer, DueAt = card.Card.DueAt,
            CanReview = card.Card.DueAt <= now, ReviewCount = card.Card.ReviewCount, LapseCount = card.Card.LapseCount,
            Version = card.Card.Version, Choices = choices, History = history, HistoryPage = page, HistoryPages = pages };
    }

    public async Task<ReviewResult> HandleAsync(Guid id, Guid ownerId, Command command, CancellationToken ct)
    {
        if (command.Version == Guid.Empty || command.SubmissionId == Guid.Empty ||
            !Enum.TryParse<RevisionGrade>(command.Grade, true, out var grade) || !Enum.IsDefined(grade) ||
            command.Grade != grade.ToString().ToLowerInvariant())
            return new(422, "Choose a valid review grade and reload the card if its form is incomplete.");

        var noteId = await db.RevisionCards.AsNoTracking().Where(c => c.Id == id && c.OwnerId == ownerId)
            .Select(c => (Guid?)c.NoteId).SingleOrDefaultAsync(ct);
        if (noteId is null) return new(404);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Lock note before card consistently. This also serializes reviews against note archive.
        var notes = await db.StudyNotes.FromSqlInterpolated($"SELECT * FROM \"StudyNotes\" WHERE \"Id\" = {noteId.Value} AND \"OwnerId\" = {ownerId} FOR UPDATE").ToListAsync(ct);
        if (notes.SingleOrDefault() is not { IsArchived: false }) return new(404);
        var cards = await db.RevisionCards.FromSqlInterpolated($"SELECT * FROM \"RevisionCards\" WHERE \"Id\" = {id} AND \"OwnerId\" = {ownerId} FOR UPDATE").ToListAsync(ct);
        var card = cards.SingleOrDefault();
        if (card is null) return new(404);
        var previous = await db.RevisionReviews.AsNoTracking().SingleOrDefaultAsync(
            r => r.CardId == id && r.OwnerId == ownerId && r.SubmissionId == command.SubmissionId, ct);
        if (previous is not null)
            return previous.SubmittedVersion == command.Version && previous.Grade == grade
                ? new(200, Duplicate: true)
                : new(409, "This submission was already used for a different review. Reload the card.");
        if (card.Version != command.Version)
            return new(409, "This card was reviewed in another tab. Reload before reviewing again.");
        var now = clock.GetUtcNow().ToUniversalTime();
        if (card.DueAt > now) return new(409, "This card is scheduled for later. Come back when it is due.");
        var schedule = RevisionSchedule.Next(card.IntervalDays, grade, now);
        db.RevisionReviews.Add(new RevisionReview { CardId = id, OwnerId = ownerId,
            SubmissionId = command.SubmissionId, SubmittedVersion = command.Version, Grade = grade,
            ReviewedAt = now, PreviousDueAt = card.DueAt, NextDueAt = schedule.DueAt, IntervalDays = schedule.IntervalDays });
        card.DueAt = schedule.DueAt;
        card.IntervalDays = schedule.IntervalDays;
        card.ReviewCount++;
        if (grade == RevisionGrade.Again) card.LapseCount++;
        card.Version = Guid.NewGuid();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        { return new(409, "This card was reviewed in another tab. Reload before reviewing again."); }
        await transaction.CommitAsync(ct);
        return new(200);
    }
}
