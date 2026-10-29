using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Notes.Preview;

public sealed record Block(Guid Id, string Kind, string Content, string? Language, string? ArticleTitle, string? ArticleUrl, bool IsDraft);
public sealed record ViewModel(Guid Id, string Title, string? NotebookName, string[] Tags,
    IReadOnlyList<Block> Blocks, bool IsArchived, DateTimeOffset UpdatedAt, Guid? CoverImageId = null);

public sealed class Handler(AppDbContext db)
{
    public async Task<ViewModel?> HandleAsync(Guid id, Guid ownerId, CancellationToken ct)
    {
        var note = await db.StudyNotes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == id && n.OwnerId == ownerId, ct);
        if (note is null) return null;
        var notebookName = note.NotebookId is null ? null : await db.StudyNotebooks.AsNoTracking()
            .Where(n => n.Id == note.NotebookId && n.OwnerId == ownerId).Select(n => n.Name).SingleOrDefaultAsync(ct);
        var tags = await db.StudyNoteTags.AsNoTracking().Where(t => t.NoteId == id).OrderBy(t => t.Name).Select(t => t.Name).ToArrayAsync(ct);
        var blocks = await db.StudyNoteBlocks.AsNoTracking().Where(b => b.NoteId == id).OrderBy(b => b.Position).ThenBy(b => b.Id).ToListAsync(ct);
        var linkedIds = blocks.Where(b => b.LinkedPostId != null).Select(b => b.LinkedPostId!.Value).Distinct().ToArray();
        var links = await db.BlogPosts.AsNoTracking().Where(p => linkedIds.Contains(p.Id) && (p.AuthorId == ownerId || (p.PublishedAt != null && !p.IsHidden)))
            .Select(p => new { p.Id, Title = p.AuthorId == ownerId ? p.Title : p.PublishedTitle!, p.AuthorId, p.PublishedAt, p.Slug }).ToListAsync(ct);
        return new(note.Id, note.Title, notebookName, tags, blocks.Select(b =>
        {
            var post = links.SingleOrDefault(p => p.Id == b.LinkedPostId);
            var isDraft = post?.AuthorId == ownerId;
            var url = post is null ? null : isDraft ? $"/blogs/{post.Id}/preview" : "/blogs/" + post.Slug;
            return new Block(b.Id, b.Kind, b.Content, b.Language, post?.Title, url, isDraft);
        }).ToList(), note.IsArchived, note.UpdatedAt, note.CoverImageId);
    }
}
