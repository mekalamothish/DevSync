using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Blogs.Explore;

public sealed record ArticleCard(Guid Id, string Title, string Summary, string? Slug, string Author,
    DateTimeOffset Date, string[] Tags, bool Published = true);
public sealed record ViewModel(IReadOnlyList<ArticleCard> Articles, int Page, int Pages, int Total,
    string Search, string Tag, bool Bookmarks = false);

public sealed class Handler(AppDbContext db)
{
    public async Task<ViewModel> HandleAsync(string? search, string? tag, int page, Guid? bookmarkUser, CancellationToken ct, Guid? viewerId = null)
    {
        search = (search ?? "").Trim();
        search = search[..Math.Min(search.Length, 200)];
        tag = (tag ?? "").Trim().ToLowerInvariant();
        var posts = db.BlogPosts.AsNoTracking().Where(p => p.PublishedAt != null && !p.IsHidden);
        if (search.Length > 0)
        {
            var needle = search.ToLowerInvariant();
            posts = posts.Where(p => p.PublishedTitle!.ToLower().Contains(needle) || p.PublishedSummary!.ToLower().Contains(needle));
        }
        if (tag.Length > 0) posts = posts.Where(p => db.BlogTags.Any(t => t.PostId == p.Id && t.Name == tag));
        if (bookmarkUser is not null) posts = posts.Where(p => db.BlogBookmarks.Any(b => b.PostId == p.Id && b.UserId == bookmarkUser));
        var pageSize = viewerId is null ? 12 : await db.AccountPreferences.Where(s => s.UserId == viewerId).Select(s => (int?)s.ArticlesPageSize).SingleOrDefaultAsync(ct) ?? 12;
        var total = await posts.CountAsync(ct);
        var pages = Math.Max(1, (total + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, pages);
        var rows = await (from p in posts join a in db.DeveloperProfiles on p.AuthorId equals a.UserId
            orderby p.PublishedAt descending, p.Id descending
            select new { p.Id, Title = p.PublishedTitle!, Summary = p.PublishedSummary!, p.Slug, Author = a.DisplayName, Date = p.PublishedAt!.Value })
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = rows.Select(r => r.Id).ToArray();
        var tags = await db.BlogTags.AsNoTracking().Where(t => ids.Contains(t.PostId)).OrderBy(t => t.Name).ToListAsync(ct);
        return new(rows.Select(r => new ArticleCard(r.Id, r.Title, r.Summary, r.Slug, r.Author, r.Date,
            tags.Where(t => t.PostId == r.Id).Select(t => t.Name).ToArray())).ToList(), page, pages, total, search, tag, bookmarkUser is not null);
    }
}
