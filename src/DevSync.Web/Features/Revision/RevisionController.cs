using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevSync.Web.Features.Revision;

[Authorize(Policy = "Member")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RevisionController : Controller
{
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("/revision")]
    public async Task<IActionResult> Dashboard([FromServices] Dashboard.Handler handler, string? view,
        int page = 1, int historyPage = 1, CancellationToken ct = default) =>
        View(await handler.HandleAsync(UserId, view, page, historyPage, ct));

    [HttpGet("/revision/create")]
    public async Task<IActionResult> Create(Guid noteId, [FromServices] Create.Handler handler, CancellationToken ct)
    {
        var command = await handler.LoadAsync(noteId, UserId, ct);
        return command is null ? NotFound() : View(command);
    }

    [HttpPost("/revision/create")]
    public async Task<IActionResult> Create(Create.Command command, [FromServices] Create.Handler handler, CancellationToken ct)
    {
        var existing = await handler.LoadAsync(command.NoteId, UserId, ct);
        if (existing is null) return NotFound();
        command.NoteTitle = existing.NoteTitle;
        if (!ModelState.IsValid)
        { Response.StatusCode = 422; return View(command); }
        var id = await handler.HandleAsync(UserId, command, ct);
        if (id is null) return NotFound();
        TempData["Success"] = "Your card is ready for its first review.";
        return Redirect($"/revision/{id}");
    }

    [HttpGet("/revision/{id:guid}")]
    public async Task<IActionResult> Review(Guid id, [FromServices] Review.Handler handler, int historyPage = 1, CancellationToken ct = default)
    {
        var model = await handler.LoadAsync(id, UserId, historyPage, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("/revision/{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, Review.Command command, [FromServices] Review.Handler handler, CancellationToken ct)
    {
        var model = await handler.LoadAsync(id, UserId, 1, ct);
        if (model is null) return NotFound();
        if (!ModelState.IsValid)
        { Response.StatusCode = 422; return View(model); }
        var result = await handler.HandleAsync(id, UserId, command, ct);
        if (result.Status == 404) return NotFound();
        if (result.Error is not null)
        {
            Response.StatusCode = result.Status;
            ModelState.AddModelError("", result.Error);
            // Render the latest schedule without making a stale submission silently current.
            model = await handler.LoadAsync(id, UserId, 1, ct);
            return model is null ? NotFound() : View(model);
        }
        TempData["Success"] = result.Duplicate ? "This review was already saved. Your schedule is unchanged." : "Review saved. Your next review is scheduled.";
        return Redirect($"/revision/{id}");
    }
}
