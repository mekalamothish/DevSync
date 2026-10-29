using DevSync.Infrastructure.Persistence;
using DevSync.Web.Features.Notes.Edit;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Notes.List;

public sealed record NoteItem(Guid Id, string Title, Guid Version, DateTimeOffset UpdatedAt, Guid? NotebookId,
    string? NotebookName, string[] Tags, int BlockCount, bool IsArchived, Guid? CoverImageId = null);
public sealed record ViewModel(IReadOnlyList<NoteItem> Notes, IReadOnlyList<NotebookChoice> Notebooks,
    IReadOnlyList<string> Tags, int Page, int Pages, int Total, int Active, int Archived,
    string Search, Guid? NotebookId, string Tag, bool ShowArchived);

public sealed class Handler(AppDbContext db)
{
    public async Task<ViewModel> HandleAsync(Guid ownerId, string? search, Guid? notebookId, string? tag,
        bool archived, int page, CancellationToken ct)
    {
        search = (search ?? "").Trim();
        search = search[..Math.Min(search.Length, 200)];
        tag = (tag ?? "").Trim().ToLowerInvariant();
        tag = tag[..Math.Min(tag.Length, 30)];
        var notebooks = await db.StudyNotebooks.AsNoTracking().Where(n => n.OwnerId == ownerId)
            .OrderBy(n => n.NormalizedName).Select(n => new NotebookChoice(n.Id, n.Name)).ToListAsync(ct);
        var notes = db.StudyNotes.AsNoTracking().Where(n => n.OwnerId == ownerId);
        var activeCount = await notes.CountAsync(n => !n.IsArchived, ct);
        var archivedCount = await notes.CountAsync(n => n.IsArchived, ct);
        var allTags = await db.StudyNoteTags.AsNoTracking().Where(t => notes.Any(n => n.Id == t.NoteId))
            .Select(t => t.Name).Distinct().OrderBy(t => t).ToListAsync(ct);
        notes = notes.Where(n => n.IsArchived == archived);
        if (notebookId is not null) notes = notes.Where(n => n.NotebookId == notebookId);
        if (tag.Length > 0) notes = notes.Where(n => db.StudyNoteTags.Any(t => t.NoteId == n.Id && t.Name == tag));
        if (search.Length > 0)
        {
            var needle = search.ToLowerInvariant();
            notes = notes.Where(n => n.Title.ToLower().Contains(needle) || db.StudyNoteBlocks.Any(b => b.NoteId == n.Id && b.Content.ToLower().Contains(needle)));
        }
        var total = await notes.CountAsync(ct);
        var pages = Math.Max(1, (total + 11) / 12);
        page = Math.Clamp(page, 1, pages);
        var rows = await notes.OrderByDescending(n => n.UpdatedAt).ThenByDescending(n => n.Id)
            .Skip((page - 1) * 12).Take(12).Select(n => new
            {
                n.Id, n.Title, n.Version, n.UpdatedAt, n.NotebookId, n.IsArchived, n.CoverImageId,
                BlockCount = db.StudyNoteBlocks.Count(b => b.NoteId == n.Id)
            }).ToListAsync(ct);
        var ids = rows.Select(n => n.Id).ToArray();
        var tags = await db.StudyNoteTags.AsNoTracking().Where(t => ids.Contains(t.NoteId)).OrderBy(t => t.Name).ToListAsync(ct);
        return new(rows.Select(n => new NoteItem(n.Id, n.Title, n.Version, n.UpdatedAt, n.NotebookId,
            notebooks.FirstOrDefault(book => book.Id == n.NotebookId)?.Name,
            tags.Where(t => t.NoteId == n.Id).Select(t => t.Name).ToArray(), n.BlockCount, n.IsArchived, n.CoverImageId)).ToList(),
            notebooks, allTags, page, pages, total, activeCount, archivedCount, search, notebookId, tag, archived);
    }
}
