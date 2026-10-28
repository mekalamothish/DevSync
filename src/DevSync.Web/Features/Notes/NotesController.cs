using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevSync.Web.Features.Notes;

[Authorize(Policy = "Member")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotesController : Controller
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("/notes/{id:guid}/format-code")]
    [RequestSizeLimit(262144)]
    public async Task<IActionResult> FormatCode(Guid id, Format.Command command, [FromServices] Edit.Handler handler, CancellationToken ct)
    {
        var note=await handler.LoadAsync(id,OwnerId,ct);
        if(note is null) return NotFound();
        if(note.IsArchived) return StatusCode(403);
        if(!ModelState.IsValid) return UnprocessableEntity(new {error="Choose a language and code up to 60,000 characters."});
        if(command.Language.Trim().ToLowerInvariant() is not ("c#" or "cs" or "csharp"))
            return UnprocessableEntity(new {error="This endpoint formats C# code. Select the appropriate language in the editor."});
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            var result=Format.CodeFormatter.CSharp(command.Code,timeout.Token);
            return result.Error is null ? Json(new {code=result.Code}) : UnprocessableEntity(new {error=result.Error});
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested)
        { return UnprocessableEntity(new {error="Formatting took too long. Try a smaller code block."}); }
    }

    [HttpGet("/notes")]
    public async Task<IActionResult> List([FromServices] List.Handler handler, string? search, Guid? notebookId,
        string? tag, bool archived = false, int page = 1, CancellationToken ct = default) =>
        View(await handler.HandleAsync(OwnerId, search, notebookId, tag, archived, page, ct));

    [HttpPost("/notes/create")]
    public async Task<IActionResult> Create(Guid? notebookId, [FromServices] Edit.Handler handler, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await handler.CreateAsync(OwnerId, notebookId, ct);
        if (result.Error is not null) { TempData["Error"] = result.Error; return RedirectToAction(nameof(List)); }
        return RedirectToAction(nameof(Edit), new { id = result.Id });
    }

    [HttpPost("/notes/notebooks")]
    public async Task<IActionResult> Notebook(string? name, [FromServices] Edit.Handler handler, CancellationToken ct)
    {
        var result = await handler.CreateNotebookAsync(OwnerId, name, ct);
        TempData[result.Error is null ? "Success" : "Error"] = result.Error ?? "Notebook created.";
        return RedirectToAction(nameof(List), new { notebookId = result.Id });
    }

    [HttpGet("/notes/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, [FromServices] Edit.Handler handler, CancellationToken ct)
    {
        var command = await handler.LoadAsync(id, OwnerId, ct);
        return command is null ? NotFound() : View(command);
    }

    [HttpPost("/notes/{id:guid}/save")]
    public Task<IActionResult> Save(Guid id, Edit.Command command, [FromServices] Edit.Handler handler, CancellationToken ct) =>
        SaveAsync(id, command, handler, false, null, null, null, ct);

    [HttpPost("/notes/{id:guid}/autosave")]
    public Task<IActionResult> Autosave(Guid id, Edit.Command command, [FromServices] Edit.Handler handler, CancellationToken ct) =>
        SaveAsync(id, command, handler, true, null, null, null, ct);

    [HttpPost("/notes/{id:guid}/blocks")]
    public Task<IActionResult> Blocks(Guid id, Edit.Command command, [FromServices] Edit.Handler handler,
        string? operation, Guid? blockId, string? kind, CancellationToken ct) =>
        SaveAsync(id, command, handler, false, operation, blockId, kind, ct);

    private async Task<IActionResult> SaveAsync(Guid id, Edit.Command command, Edit.Handler handler, bool json,
        string? operation, Guid? blockId, string? kind, CancellationToken ct)
    {
        command.Id = id;
        var existing = await handler.LoadAsync(id, OwnerId, ct);
        if (existing is null) return NotFound();
        command.IsArchived = existing.IsArchived;
        command.UpdatedAt = existing.UpdatedAt;
        command.Notebooks = existing.Notebooks;
        command.Articles = existing.Articles;
        if (!ModelState.IsValid)
        {
            Response.StatusCode = 422;
            return json ? Json(new { error = "Check title, field lengths, and the 50-block limit before saving." }) : View("Edit", command);
        }
        var result = await handler.HandleAsync(id, OwnerId, command, operation, blockId, kind, ct);
        if (result.Error is not null)
        {
            Response.StatusCode = result.Status;
            ViewData["SavePaused"] = result.Status == 409 || existing.IsArchived;
            ModelState.AddModelError("", result.Error);
            // Preserve the posted token so an old tab cannot overwrite the winning save.
            return json ? Json(new { error = result.Error }) : View("Edit", command);
        }
        if (json) return Json(new { version = result.Version });
        TempData["Success"] = "Note saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpGet("/notes/{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, [FromServices] Preview.Handler handler, CancellationToken ct)
    {
        var model = await handler.HandleAsync(id, OwnerId, ct);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost("/notes/{id:guid}/archive")]
    public Task<IActionResult> Archive(Guid id, Guid version, [FromServices] Edit.Handler handler, CancellationToken ct) =>
        ArchiveAsync(id, version, true, handler, ct);

    [HttpPost("/notes/{id:guid}/restore")]
    public Task<IActionResult> Restore(Guid id, Guid version, [FromServices] Edit.Handler handler, CancellationToken ct) =>
        ArchiveAsync(id, version, false, handler, ct);

    private async Task<IActionResult> ArchiveAsync(Guid id, Guid version, bool archived, Edit.Handler handler, CancellationToken ct)
    {
        var result = await handler.SetArchivedAsync(id, OwnerId, version, archived, ct);
        if (result.Status == 404) return NotFound();
        if (result.Error is not null)
        {
            Response.StatusCode = result.Status;
            var command = await handler.LoadAsync(id, OwnerId, ct);
            command!.Version = version;
            ModelState.Clear();
            ModelState.AddModelError("", result.Error);
            ViewData["SavePaused"] = true;
            return View("Edit", command);
        }
        TempData["Success"] = archived ? "Note archived. You can restore it any time." : "Note restored.";
        return RedirectToAction(archived ? nameof(List) : nameof(Edit), archived ? new { archived = true } : new { id });
    }
}
