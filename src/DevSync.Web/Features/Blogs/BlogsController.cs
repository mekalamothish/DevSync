using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevSync.Web.Features.Blogs;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BlogsController : Controller
{
    private Guid? UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpGet("/blogs")]
    public async Task<IActionResult> Explore([FromServices] Explore.Handler handler, string? search, string? tag, int page = 1, CancellationToken ct = default) =>
        View(await handler.HandleAsync(search, tag, page, null, ct, UserId));

    [Authorize(Policy = "Member"), HttpGet("/blogs/bookmarks")]
    public async Task<IActionResult> Bookmarks([FromServices] Explore.Handler handler, int page = 1, CancellationToken ct = default) =>
        View("Explore", await handler.HandleAsync(null, null, page, UserId, ct, UserId));

    [Authorize(Policy = "Member"), HttpGet("/blogs/mine")]
    public async Task<IActionResult> MyBlogs([FromServices] MyBlogs.Handler handler, int page = 1, CancellationToken ct = default) =>
        View(await handler.HandleAsync(UserId!.Value, page, ct));

    [Authorize(Policy = "Member"), HttpPost("/blogs/create")]
    public async Task<IActionResult> Create([FromServices] Create.Handler handler, CancellationToken ct) =>
        RedirectToAction(nameof(Edit), new { id = await handler.HandleAsync(UserId!.Value, ct) });

    [Authorize(Policy = "Member"), HttpGet("/blogs/{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, [FromServices] Edit.Handler handler, CancellationToken ct)
    {
        var command = await handler.LoadAsync(id, UserId!.Value, ct);
        return command is null ? NotFound() : View(command);
    }

    [Authorize(Policy="Member"),HttpPost("/blogs/{id:guid}/blocks")]
    public Task<IActionResult> Blocks(Guid id,Edit.Command command,[FromServices] Edit.Handler handler,string? operation,Guid? blockId,string? kind,CancellationToken ct)
        => SaveArticleAsync(id,command,handler,false,false,ct,operation,blockId,kind);

    [Authorize(Policy="Member"),HttpPost("/blogs/{id:guid}/format-code"),RequestSizeLimit(262144)]
    public async Task<IActionResult> FormatCode(Guid id,Notes.Format.Command command,[FromServices] Edit.Handler handler,CancellationToken ct)
    {
        if(await handler.LoadAsync(id,UserId!.Value,ct) is null) return NotFound();
        if(!ModelState.IsValid || command.Language.Trim().ToLowerInvariant() is not ("c#" or "cs" or "csharp")) return UnprocessableEntity(new{error="Choose C# code within 60,000 characters."});
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try {var result=Notes.Format.CodeFormatter.CSharp(command.Code,timeout.Token);return result.Error is null ? Json(new{code=result.Code}):UnprocessableEntity(new{error=result.Error});}
        catch(OperationCanceledException) when(!ct.IsCancellationRequested) {return UnprocessableEntity(new{error="Formatting took too long. Try a smaller code block."});}
    }

    [Authorize(Policy = "Member"), HttpPost("/blogs/{id:guid}/save")]
    public Task<IActionResult> Save(Guid id, Edit.Command command, [FromServices] Edit.Handler handler, CancellationToken ct) =>
        SaveArticleAsync(id, command, handler, false, false, ct);

    [Authorize(Policy = "Member"), HttpPost("/blogs/{id:guid}/autosave")]
    public Task<IActionResult> Autosave(Guid id, Edit.Command command, [FromServices] Edit.Handler handler, CancellationToken ct) =>
        SaveArticleAsync(id, command, handler, false, true, ct);

    [Authorize(Policy = "Member"), HttpPost("/blogs/{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id, Edit.Command command, [FromServices] Edit.Handler handler, CancellationToken ct) =>
        SaveArticleAsync(id, command, handler, true, false, ct);

    private async Task<IActionResult> SaveArticleAsync(Guid id, Edit.Command command, Edit.Handler handler, bool publish, bool json, CancellationToken ct,
        string? operation=null,Guid? blockId=null,string? kind=null)
    {
        command.Id = id;
        // Verify ownership before returning validation errors or reflecting posted content.
        var existing = await handler.LoadAsync(id, UserId!.Value, ct);
        if (existing is null) return NotFound();
        command.PublishedUrl = existing.PublishedUrl;
        command.IsHidden = existing.IsHidden;
        command.Articles=existing.Articles;
        if (!ModelState.IsValid)
        {
            Response.StatusCode = 422;
            return json ? Json(new { error = "Check title and field lengths before saving." }) : View("Edit", command);
        }
        var result = await handler.HandleAsync(id, UserId!.Value, command, publish, ct,operation,blockId,kind);
        if (result.Error is not null)
        {
            Response.StatusCode = result.Status;
            ViewData["SavePaused"]=result.Status==409;
            // Keep the submitted version on conflict: it must never silently overwrite a newer draft.
            ModelState.AddModelError("", result.Error);
            return json ? Json(new { error = result.Error }) : View("Edit", command);
        }
        if (json) return Json(new { version = result.Version, publishedUrl = result.PublishedUrl });
        TempData["Success"] = publish ? "Your article is published." : "Draft saved.";
        return publish ? Redirect(result.PublishedUrl!) : RedirectToAction(nameof(Edit), new { id });
    }

    [Authorize(Policy = "Member"), HttpGet("/blogs/{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, [FromServices] Read.Handler handler, CancellationToken ct)
    {
        var model = await handler.HandleAsync(null, id, UserId, 1, ct);
        return model is null ? NotFound() : View("Read", model);
    }

    [HttpGet("/blogs/{slug}")]
    public async Task<IActionResult> Read(string slug, [FromServices] Read.Handler handler, int commentPage = 1, CancellationToken ct = default)
    {
        var model = await handler.HandleAsync(slug, null, UserId, commentPage, ct);
        return model is null ? NotFound() : View(model);
    }

    [Authorize(Policy = "Member"), HttpPost("/blogs/{id:guid}/bookmark")]
    public Task<IActionResult> Bookmark(Guid id, bool enabled, [FromServices] Interact.Handler handler, CancellationToken ct) =>
        InteractAsync(id, enabled, true, handler, ct);

    [Authorize(Policy = "Member"), HttpPost("/blogs/{id:guid}/react")]
    public Task<IActionResult> React(Guid id, bool enabled, [FromServices] Interact.Handler handler, CancellationToken ct) =>
        InteractAsync(id, enabled, false, handler, ct);

    private async Task<IActionResult> InteractAsync(Guid id, bool enabled, bool bookmark, Interact.Handler handler, CancellationToken ct)
    {
        var slug = await handler.PublishedSlugAsync(id, ct);
        if (slug is null) return NotFound();
        if (!await handler.SetAsync(id, UserId!.Value, enabled, bookmark, ct)) return NotFound();
        return Redirect("/blogs/" + slug);
    }

    [Authorize(Policy = "Member"), HttpPost("/blogs/{id:guid}/comments")]
    public async Task<IActionResult> Comment(Guid id, Interact.CommentCommand command, [FromServices] Interact.Handler handler, CancellationToken ct)
    {
        var slug = await handler.PublishedSlugAsync(id, ct);
        if (slug is null) return NotFound();
        if (!ModelState.IsValid) { TempData["CommentError"] = "Write a comment between 1 and 2,000 characters."; }
        else if (!await handler.AddCommentAsync(id, UserId!.Value, command, ct)) return NotFound();
        return Redirect("/blogs/" + slug + "#comments");
    }

    [Authorize(Policy = "Member"), HttpPost("/blogs/{id:guid}/comments/{commentId:guid}/delete")]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, [FromServices] Interact.Handler handler, CancellationToken ct)
    {
        var slug = await handler.PublishedSlugAsync(id, ct);
        if (slug is null || !await handler.DeleteCommentAsync(id, commentId, UserId!.Value, ct)) return NotFound();
        return Redirect("/blogs/" + slug + "#comments");
    }
}
