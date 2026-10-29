using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevSync.Web.Features.Images;

[Authorize(Policy="Member")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ImagesController(Handler handler) : Controller
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("/images")]
    [HttpGet("/teams/{teamId:guid}/images")]
    public async Task<IActionResult> Index(Guid? teamId, int page=1, CancellationToken ct=default)
    {
        var model=await handler.Load(Actor,teamId,page,ct);
        if(model is null) return NotFound();
        Response.Headers.CacheControl="private, no-store";
        return View("~/Features/Images/View.cshtml",model);
    }
    [HttpPost("/images/upload")]
    [HttpPost("/teams/{teamId:guid}/images/upload")]
    [RequestSizeLimit(6*1024*1024)]
    [RequestFormLimits(MultipartBodyLengthLimit=6*1024*1024)]
    public async Task<IActionResult> Upload(Guid? teamId, IFormFile? image, string? description, CancellationToken ct)
        => await Finish(await handler.Upload(Actor,teamId,image,description,ct),teamId,ct,"Image uploaded privately. Copy its URL or Markdown below.");
    [HttpPost("/images/cover-upload")]
    [RequestSizeLimit(6*1024*1024)]
    [RequestFormLimits(MultipartBodyLengthLimit=6*1024*1024)]
    public async Task<IActionResult> UploadCover(IFormFile? image, CancellationToken ct)
    {
        var result=await handler.Upload(Actor,null,image,"Cover image",ct);
        return StatusCode(result.Status,new {id=result.ImageId,error=result.Error});
    }
    [HttpPost("/images/{id:guid}/visibility")]
    [HttpPost("/teams/{teamId:guid}/images/{id:guid}/visibility")]
    public async Task<IActionResult> Visibility(Guid? teamId, Guid id, Guid version, bool isPublic, CancellationToken ct)
        => await Finish(await handler.Visibility(Actor,teamId,id,version,isPublic,ct),teamId,ct,
            isPublic ? "Public link enabled. Anyone with the URL can view this image." : "Public link disabled. Future requests require library access.");
    private async Task<IActionResult> Finish(Result result, Guid? teamId, CancellationToken ct, string success)
    {
        if(result.Status==200) { TempData["Success"]=success; return Redirect(teamId==null ? "/images" : $"/teams/{teamId}/images"); }
        if(result.Error is null) return StatusCode(result.Status);
        ModelState.AddModelError("",result.Error); Response.StatusCode=result.Status;
        var model=await handler.Load(Actor,teamId,1,ct);
        return model is null ? NotFound() : View("~/Features/Images/View.cshtml",model);
    }
    [AllowAnonymous]
    [HttpGet("/media/images/{id:guid}")]
    public async Task<IActionResult> File(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl="private, no-store";
        Response.Headers["X-Content-Type-Options"]="nosniff";
        Response.Headers.ContentSecurityPolicy="default-src 'none'; sandbox";
        var actor=Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier),out var userId) ? userId : (Guid?)null;
        var image=await handler.Read(id,actor,ct);
        return image is null ? NotFound() : base.File(image.Value.Data,image.Value.ContentType);
    }
}
