using DevSync.Domain.Blogs;
using DevSync.Infrastructure.Persistence;

namespace DevSync.Web.Features.Blogs.Create;

public sealed class Handler(AppDbContext db)
{
    public async Task<Guid> HandleAsync(Guid userId, CancellationToken ct)
    {
        var post = new BlogPost { AuthorId = userId };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync(ct);
        return post.Id;
    }
}
