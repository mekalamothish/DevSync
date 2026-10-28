using DevSync.Domain.Revision;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Revision.Dashboard;

public sealed record CardItem(Guid Id, Guid NoteId, string NoteTitle, string Prompt, DateTimeOffset DueAt, int ReviewCount);
public sealed record HistoryItem(Guid CardId, string Prompt, RevisionGrade Grade, DateTimeOffset ReviewedAt, DateTimeOffset NextDueAt, bool IsArchived);
public sealed record ViewModel(int Due, int Scheduled, int Suspended, int ReviewedToday, int TotalReviews,
    string View, IReadOnlyList<CardItem> Cards, int Page, int Pages,
    IReadOnlyList<HistoryItem> History, int HistoryPage, int HistoryPages);

public sealed class Handler(AppDbContext db, TimeProvider clock)
{
    public async Task<ViewModel> HandleAsync(Guid ownerId, string? view, int page, int historyPage, CancellationToken ct)
    {
        var now = clock.GetUtcNow().ToUniversalTime();
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var tomorrow = today.AddDays(1);
        var owned = from c in db.RevisionCards.AsNoTracking()
            join n in db.StudyNotes.AsNoTracking() on c.NoteId equals n.Id
            where c.OwnerId == ownerId && n.OwnerId == ownerId
            select new { Card = c, NoteTitle = n.Title, n.IsArchived };
        var active = owned.Where(x => !x.IsArchived);
        var due = await active.CountAsync(x => x.Card.DueAt <= now, ct);
        var scheduled = await active.CountAsync(x => x.Card.DueAt > now, ct);
        var suspended = await owned.CountAsync(x => x.IsArchived, ct);
        var selection = view == "scheduled" ? "scheduled" : "due";
        var filtered = selection == "scheduled" ? active.Where(x => x.Card.DueAt > now) : active.Where(x => x.Card.DueAt <= now);
        var total = selection == "scheduled" ? scheduled : due;
        var pages = Math.Max(1, (total + 11) / 12);
        page = Math.Clamp(page, 1, pages);
        var cards = await filtered.OrderBy(x => x.Card.DueAt).ThenBy(x => x.Card.Id)
            .Skip((page - 1) * 12).Take(12)
            .Select(x => new CardItem(x.Card.Id, x.Card.NoteId, x.NoteTitle, x.Card.Prompt, x.Card.DueAt, x.Card.ReviewCount)).ToListAsync(ct);
        var reviews = from r in db.RevisionReviews.AsNoTracking()
            join c in db.RevisionCards.AsNoTracking() on r.CardId equals c.Id
            join n in db.StudyNotes.AsNoTracking() on c.NoteId equals n.Id
            where r.OwnerId == ownerId && c.OwnerId == ownerId && n.OwnerId == ownerId
            select new { Review = r, Card = c, n.IsArchived };
        var totalReviews = await reviews.CountAsync(ct);
        var reviewedToday = await reviews.CountAsync(x => x.Review.ReviewedAt >= today && x.Review.ReviewedAt < tomorrow, ct);
        var historyPages = Math.Max(1, (totalReviews + 9) / 10);
        historyPage = Math.Clamp(historyPage, 1, historyPages);
        var history = await reviews.OrderByDescending(x => x.Review.ReviewedAt).ThenByDescending(x => x.Review.Id)
            .Skip((historyPage - 1) * 10).Take(10)
            .Select(x => new HistoryItem(x.Card.Id, x.Card.Prompt, x.Review.Grade, x.Review.ReviewedAt, x.Review.NextDueAt, x.IsArchived)).ToListAsync(ct);
        return new(due, scheduled, suspended, reviewedToday, totalReviews, selection, cards, page, pages, history, historyPage, historyPages);
    }
}
