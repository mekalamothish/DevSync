using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Blogs.MyBlogs;

public sealed record Item(Guid Id, string Title, DateTimeOffset UpdatedAt, string? Slug, bool HasChanges, bool IsHidden = false, Guid? CoverImageId = null);
public sealed record ViewModel(IReadOnlyList<Item> Articles, int Page, int Pages, int Drafts, int Published);

public sealed class Handler(AppDbContext db)
{
    public async Task<ViewModel> HandleAsync(Guid userId, int page, CancellationToken ct)
    {
        var posts = db.BlogPosts.AsNoTracking().Where(p => p.AuthorId == userId);
        var drafts = await posts.CountAsync(p => p.PublishedAt == null, ct);
        var published = await posts.CountAsync(p => p.PublishedAt != null, ct);
        var pages = Math.Max(1, (drafts + published + 11) / 12);
        page = Math.Clamp(page, 1, pages);
        var rows = await posts.OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * 12).Take(12).Select(p => new { p.Id, p.Title, p.UpdatedAt, p.Slug, p.DraftTags, p.IsHidden, p.CoverImageId,
                TextChanges = p.CoverImageId != p.PublishedCoverImageId || p.Title != p.PublishedTitle || p.Summary != p.PublishedSummary || p.EditorFormat!=p.PublishedEditorFormat ||
                    (p.EditorFormat=="markdown" && p.Markdown!=p.PublishedMarkdown) || (p.EditorFormat=="blocks" && p.BlocksJson!=p.PublishedBlocksJson) ||
                    (p.EditorFormat=="richtext" && p.RichText!=p.PublishedRichText) }).ToListAsync(ct);
        var ids = rows.Select(p => p.Id).ToArray();
        var tags = await db.BlogTags.Where(t => ids.Contains(t.PostId)).OrderBy(t => t.Name).ToListAsync(ct);
        var items = rows.Select(p => new Item(p.Id, p.Title, p.UpdatedAt, p.Slug,
            p.Slug != null && (p.TextChanges ||
                p.DraftTags != string.Join(',', tags.Where(t => t.PostId == p.Id).Select(t => t.Name))), p.IsHidden, p.CoverImageId)).ToList();
        return new(items, page, pages, drafts, published);
    }
}
