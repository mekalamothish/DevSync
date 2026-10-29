using DevSync.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace DevSync.Web.Features.Profiles.Public;
public sealed record ViewModel(string Handle,string Name,string? Bio,bool Verified,IReadOnlyList<DevSync.Web.Features.Blogs.Explore.ArticleCard> Articles,int Page,int Pages,int Total);
public sealed class Handler(AppDbContext db)
{
    public async Task<ViewModel?> HandleAsync(string handle,int page,CancellationToken ct)
    {
        handle=handle.ToLowerInvariant();
        var profile=await(from p in db.DeveloperProfiles.AsNoTracking() join s in db.AccountPreferences.AsNoTracking() on p.UserId equals s.UserId
            where p.Handle==handle && s.PublicProfile select new{p.UserId,p.Handle,p.DisplayName,Bio=s.ShowBio?p.Bio:null}).SingleOrDefaultAsync(ct);
        if(profile is null)return null;
        var posts=db.BlogPosts.AsNoTracking().Where(p=>p.AuthorId==profile.UserId && p.PublishedAt!=null && !p.IsHidden);
        var total=await posts.CountAsync(ct);var pages=Math.Max(1,(total+11)/12);page=Math.Clamp(page,1,pages);
        var rows=await posts.OrderByDescending(p=>p.PublishedAt).ThenByDescending(p=>p.Id).Skip((page-1)*12).Take(12)
            .Select(p=>new Blogs.Explore.ArticleCard(p.Id,p.PublishedTitle!,p.PublishedSummary!,p.Slug,profile.DisplayName,p.PublishedAt!.Value,Array.Empty<string>(),true,p.PublishedCoverImageId)).ToListAsync(ct);
        var verified=await db.PublisherVerifications.AnyAsync(v=>v.UserId==profile.UserId && v.Status=="approved",ct);
        return new(profile.Handle,profile.DisplayName,profile.Bio,verified,rows,page,pages,total);
    }
}
