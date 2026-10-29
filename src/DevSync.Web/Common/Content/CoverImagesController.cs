using System.Security.Claims;
using DevSync.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevSync.Web.Common.Content;

[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class CoverImagesController(AppDbContext db) : Controller
{
    private Guid? Actor => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var id) ? id : null;

    [HttpGet("/blogs/{id:guid}/cover")]
    public async Task<IActionResult> Blog(Guid id, bool draft=false, CancellationToken ct=default)
    {
        var post=await db.BlogPosts.AsNoTracking().Where(p=>p.Id==id && (draft ? p.AuthorId==Actor : p.PublishedAt!=null && !p.IsHidden))
            .Select(p=>new {ImageId=draft ? p.CoverImageId : p.PublishedCoverImageId,p.AuthorId}).SingleOrDefaultAsync(ct);
        return post is null ? NotFound() : await Image(post.ImageId,post.AuthorId,ct);
    }

    [Authorize(Policy="Member"),HttpGet("/notes/{id:guid}/cover")]
    public async Task<IActionResult> Note(Guid id,CancellationToken ct)
    {
        var note=await db.StudyNotes.AsNoTracking().Where(n=>n.Id==id && n.OwnerId==Actor)
            .Select(n=>new {n.CoverImageId,n.OwnerId}).SingleOrDefaultAsync(ct);
        return note is null ? NotFound() : await Image(note.CoverImageId,note.OwnerId,ct);
    }

    private async Task<IActionResult> Image(Guid? id,Guid owner,CancellationToken ct)
    {
        Response.Headers.CacheControl="private, no-store";
        Response.Headers["X-Content-Type-Options"]="nosniff";
        Response.Headers.ContentSecurityPolicy="default-src 'none'; sandbox";
        var image=await db.ImageAssets.AsNoTracking().Where(i=>i.Id==id && i.OwnerId==owner)
            .Select(i=>new {i.Data,i.ContentType}).SingleOrDefaultAsync(ct);
        return image is null ? NotFound() : File(image.Data,image.ContentType);
    }
}
