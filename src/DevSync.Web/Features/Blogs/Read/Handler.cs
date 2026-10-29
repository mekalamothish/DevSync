using DevSync.Infrastructure.Persistence;
using DevSync.Web.Features.Blogs.Common;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Blogs.Read;

public sealed record Comment(Guid Id, Guid UserId, string Author, string Body, DateTimeOffset Date);
public sealed record ViewModel(Guid Id, Guid AuthorId, string Title, string Summary, string Html, string Author,
    string[] Tags, DateTimeOffset Date, string? Slug, bool Preview, bool Bookmarked, bool Reacted,
    int Reactions, IReadOnlyList<Comment> Comments, int CommentPage, int CommentPages, int CommentCount, string? AuthorHandle = null, bool PublisherVerified = false, Guid? CoverImageId = null);

public sealed class Handler(AppDbContext db, IConfiguration config)
{
    public async Task<ViewModel?> HandleAsync(string? slug, Guid? previewId, Guid? userId, int commentPage, CancellationToken ct)
    {
        var query = db.BlogPosts.AsNoTracking();
        query = previewId is not null ? query.Where(p => p.Id == previewId && p.AuthorId == userId)
            : query.Where(p => p.Slug == slug && p.PublishedAt != null && !p.IsHidden);
        var row = await (from p in query join a in db.DeveloperProfiles on p.AuthorId equals a.UserId
            select new { Post = p, Author = a.DisplayName, PublicHandle = db.AccountPreferences.Any(s => s.UserId == a.UserId && s.PublicProfile) ? a.Handle : null, Verified = db.PublisherVerifications.Any(v => v.UserId == a.UserId && v.Status == "approved") }).SingleOrDefaultAsync(ct);
        if (row is null) return null;
        var post = row.Post;
        if (previewId is not null)
            return new(post.Id, post.AuthorId, post.Title, post.Summary, await BlogDocument.RenderAsync(db,post.EditorFormat,post.Markdown,post.BlocksJson,post.RichText,userId,config["Account:PublicBaseUrl"],ct), row.Author,
                post.DraftTags.Split(',', StringSplitOptions.RemoveEmptyEntries), post.UpdatedAt, post.Slug, true, false, false, 0, [], 1, 1, 0, CoverImageId:post.CoverImageId);
        var tags = await db.BlogTags.Where(t => t.PostId == post.Id).OrderBy(t => t.Name).Select(t => t.Name).ToArrayAsync(ct);
        var reactions = await db.BlogReactions.CountAsync(r => r.PostId == post.Id, ct);
        var bookmarked = userId is not null && await db.BlogBookmarks.AnyAsync(b => b.PostId == post.Id && b.UserId == userId, ct);
        var reacted = userId is not null && await db.BlogReactions.AnyAsync(r => r.PostId == post.Id && r.UserId == userId, ct);
        var comments = db.BlogComments.AsNoTracking().Where(c => c.PostId == post.Id && !c.IsHidden);
        var total = await comments.CountAsync(ct);
        var pages = Math.Max(1, (total + 19) / 20);
        commentPage = Math.Clamp(commentPage, 1, pages);
        var items = await (from c in comments join a in db.DeveloperProfiles on c.UserId equals a.UserId
            orderby c.CreatedAt, c.Id
            select new Comment(c.Id, c.UserId, a.DisplayName, c.Body, c.CreatedAt))
            .Skip((commentPage - 1) * 20).Take(20).ToListAsync(ct);
        return new(post.Id, post.AuthorId, post.PublishedTitle!, post.PublishedSummary!, await BlogDocument.RenderAsync(db,post.PublishedEditorFormat,post.PublishedMarkdown!,post.PublishedBlocksJson,post.PublishedRichText,null,config["Account:PublicBaseUrl"],ct),
            row.Author, tags, post.PublishedAt!.Value, post.Slug, false, bookmarked, reacted, reactions, items, commentPage, pages, total, row.PublicHandle, row.Verified, post.PublishedCoverImageId);
    }
}
