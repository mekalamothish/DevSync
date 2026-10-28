using System.ComponentModel.DataAnnotations;
using DevSync.Domain.Blogs;
using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Features.Blogs.Interact;

public sealed class CommentCommand
{
    [Required, StringLength(2000)]
    public string Body { get; set; } = "";
}

public sealed class Handler(AppDbContext db)
{
    public Task<string?> PublishedSlugAsync(Guid postId, CancellationToken ct) =>
        db.BlogPosts.Where(p => p.Id == postId && p.PublishedAt != null && !p.IsHidden).Select(p => p.Slug).SingleOrDefaultAsync(ct);

    public async Task<bool> SetAsync(Guid postId, Guid userId, bool enabled, bool bookmark, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await LockVisibleAsync(postId, ct)) return false;
        // Explicit state + composite keys make retries and concurrent clicks idempotent.
        if (bookmark)
        {
            if (enabled) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"BlogBookmarks\" (\"PostId\", \"UserId\") VALUES ({postId}, {userId}) ON CONFLICT DO NOTHING", ct);
            else await db.BlogBookmarks.Where(b => b.PostId == postId && b.UserId == userId).ExecuteDeleteAsync(ct);
        }
        else
        {
            if (enabled) await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"BlogReactions\" (\"PostId\", \"UserId\") VALUES ({postId}, {userId}) ON CONFLICT DO NOTHING", ct);
            else await db.BlogReactions.Where(r => r.PostId == postId && r.UserId == userId).ExecuteDeleteAsync(ct);
        }
        await tx.CommitAsync(ct); return true;
    }

    public async Task<bool> AddCommentAsync(Guid postId, Guid userId, CommentCommand command, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await LockVisibleAsync(postId, ct)) return false;
        db.BlogComments.Add(new BlogComment { PostId = postId, UserId = userId, Body = command.Body.Trim() });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct); return true;
    }

    private async Task<bool> LockVisibleAsync(Guid postId, CancellationToken ct) =>
        await db.BlogPosts.FromSqlInterpolated($"SELECT * FROM \"BlogPosts\" WHERE \"Id\" = {postId} AND \"PublishedAt\" IS NOT NULL AND NOT \"IsHidden\" FOR SHARE").AsNoTracking().SingleOrDefaultAsync(ct) is not null;

    public async Task<bool> DeleteCommentAsync(Guid postId, Guid id, Guid userId, CancellationToken ct) =>
        await db.BlogComments.Where(c => c.Id == id && c.PostId == postId && c.UserId == userId).ExecuteDeleteAsync(ct) == 1;
}
