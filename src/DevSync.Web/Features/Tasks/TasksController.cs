using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevSync.Web.Features.Tasks;

[Authorize(Policy = "Member")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TasksController : Controller
{
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("/tasks")]
    public async Task<IActionResult> List([FromServices] List.Handler handler, Guid? boardId, string? search, string? priority,
        string? state, string? assignment, string? due, string? sort, int page = 1, CancellationToken ct = default)
    {
        var model = await handler.HandleAsync(OwnerId, boardId, search, priority, state, assignment, due, sort, page, ct);
        return model is null ? NotFound() : View(model);
    }
    [HttpGet("/tasks/board")]
    public async Task<IActionResult> Board([FromServices] Board.Handler handler, Guid? boardId, string? search, string? priority,
        string? state, string? assignment, string? due, string? sort, CancellationToken ct = default)
    {
        var model = await handler.HandleAsync(OwnerId, boardId, search, priority, state, assignment, due, sort, ct);
        return model is null ? NotFound() : View(model);
    }
    [HttpPost("/tasks/boards")]
    public async Task<IActionResult> CreateBoard(string? name, [FromServices] Edit.Handler handler, [FromServices] List.Handler list, CancellationToken ct)
    {
        var result = await handler.CreateBoardAsync(OwnerId, name, ct);
        if (result.Status == 404) return NotFound();
        if (result.Error is not null)
        {
            Response.StatusCode = result.Status; TempData["Error"] = result.Error;
            return View("List", await list.HandleAsync(OwnerId, null, null, null, null, null, null, null, 1, ct));
        }
        TempData["Success"] = "Personal board created.";
        return Redirect("/tasks/board?boardId=" + result.Id);
    }
    [HttpGet("/tasks/create")]
    public async Task<IActionResult> Create(Guid? boardId, [FromServices] Edit.Handler handler, CancellationToken ct)
    {
        var command = await handler.NewAsync(OwnerId, boardId, ct);
        if (command is null)
        {
            if (boardId is not null) return NotFound();
            TempData["Error"] = "Create a personal board before adding your first task.";
            return Redirect("/tasks");
        }
        return View("Edit", command);
    }
    [HttpPost("/tasks/create")]
    public async Task<IActionResult> CreatePost(Edit.Command command, [FromServices] Edit.Handler handler,
        string? operation, Guid? itemId, CancellationToken ct)
    {
        var existing = await handler.NewAsync(OwnerId, command.BoardId, ct);
        if (existing is null) return NotFound();
        command.IsNew = true; await handler.PopulateChoicesAsync(command, OwnerId, ct);
        PrepareChecklistValidation(command, operation, itemId);
        if (!ModelState.IsValid) return EditorError(command, "Check title, dates, field lengths, and checklist limits before saving.", 422);
        var result = await handler.CreateAsync(OwnerId, command, operation, itemId, ct);
        if (result.Status == 404) return NotFound();
        if (result.Error is not null) return EditorError(command, result.Error, result.Status);
        TempData["Success"] = "Task created.";
        return Redirect(operation is null ? "/tasks/" + result.Id : "/tasks/" + result.Id + "/edit");
    }
    [HttpGet("/tasks/{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, [FromServices] Edit.Handler handler, CancellationToken ct)
    {
        var command = await handler.LoadAsync(id, OwnerId, ct);
        return command is null ? NotFound() : View(command);
    }
    [HttpPost("/tasks/{id:guid}/save")]
    public Task<IActionResult> Save(Guid id, Edit.Command command, [FromServices] Edit.Handler handler, CancellationToken ct) =>
        SaveEditorAsync(id, command, handler, null, null, ct);

    private async Task<IActionResult> SaveEditorAsync(Guid id, Edit.Command command, Edit.Handler handler, string? operation, Guid? itemId, CancellationToken ct)
    {
        var existing = await handler.LoadAsync(id, OwnerId, ct);
        if (existing is null) return NotFound();
        command.Id = id; command.BoardId = existing.BoardId; command.IsArchived = existing.IsArchived;
        await handler.PopulateChoicesAsync(command, OwnerId, ct);
        PrepareChecklistValidation(command, operation, itemId);
        if (!ModelState.IsValid) return EditorError(command, "Check title, dates, field lengths, and checklist limits before saving.", 422);
        var result = await handler.SaveAsync(id, OwnerId, command, operation, itemId, ct);
        if (result.Status == 404) return NotFound();
        if (result.Error is not null) return EditorError(command, result.Error, result.Status);
        TempData["Success"] = "Task saved.";
        return Redirect(operation is null ? "/tasks/" + id : "/tasks/" + id + "/edit");
    }
    private void PrepareChecklistValidation(Edit.Command command, string? operation, Guid? itemId)
    {
        if (operation != "remove") return;
        var index = command.Checklist.FindIndex(i => i.Id == itemId);
        if (index < 0) return;
        var prefix = $"Checklist[{index}].";
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray()) ModelState.Remove(key);
    }
    private IActionResult EditorError(Edit.Command command, string error, int status)
    {
        Response.StatusCode = status; ViewData["SavePaused"] = status == 409 || command.IsArchived;
        ModelState.AddModelError("", error); return View("Edit", command);
    }
    [HttpGet("/tasks/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, [FromServices] Details.Handler handler, int activityPage = 1, CancellationToken ct = default)
    {
        var model = await handler.HandleAsync(id, OwnerId, activityPage, ct);
        return model is null ? NotFound() : View(model);
    }
    [HttpPost("/tasks/{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, Guid version, Guid boardVersion, Guid columnId, int? targetPosition, string? direction,
        string? returnUrl, [FromServices] Edit.Handler handler, [FromServices] Details.Handler details, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await MutationResultAsync(id, version, boardVersion, new(422, "Choose a valid column and position."), details, returnUrl, ct);
        var result = await handler.MoveAsync(id, OwnerId, version, boardVersion, columnId, targetPosition, direction, ct);
        return await MutationResultAsync(id, version, boardVersion, result, details, returnUrl, ct);
    }
    [HttpPost("/tasks/{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, Guid version, string? returnUrl, [FromServices] Edit.Handler handler, [FromServices] Details.Handler details, CancellationToken ct) =>
        ChangeStateAsync(id, version, "complete", returnUrl, handler, details, ct);
    [HttpPost("/tasks/{id:guid}/reopen")]
    public Task<IActionResult> Reopen(Guid id, Guid version, string? returnUrl, [FromServices] Edit.Handler handler, [FromServices] Details.Handler details, CancellationToken ct) =>
        ChangeStateAsync(id, version, "reopen", returnUrl, handler, details, ct);
    [HttpPost("/tasks/{id:guid}/archive")]
    public Task<IActionResult> Archive(Guid id, Guid version, string? returnUrl, [FromServices] Edit.Handler handler, [FromServices] Details.Handler details, CancellationToken ct) =>
        ChangeStateAsync(id, version, "archive", returnUrl, handler, details, ct);
    [HttpPost("/tasks/{id:guid}/restore")]
    public Task<IActionResult> Restore(Guid id, Guid version, string? returnUrl, [FromServices] Edit.Handler handler, [FromServices] Details.Handler details, CancellationToken ct) =>
        ChangeStateAsync(id, version, "restore", returnUrl, handler, details, ct);
    private async Task<IActionResult> ChangeStateAsync(Guid id, Guid version, string operation, string? returnUrl, Edit.Handler handler, Details.Handler details, CancellationToken ct)
    {
        var result = ModelState.IsValid ? await handler.StateAsync(id, OwnerId, version, operation, ct) : new Edit.Result(422, "Use the version shown on the task before changing its status.");
        return await MutationResultAsync(id, version, null, result, details, returnUrl, ct);
    }
    [HttpPost("/tasks/{id:guid}/checklist")]
    public async Task<IActionResult> Checklist(Guid id, Edit.Command command, string? operation, Guid? itemId, bool isCompleted,
        string? returnUrl, [FromServices] Edit.Handler handler, [FromServices] Details.Handler details, CancellationToken ct)
    {
        if (operation != "toggle") return await SaveEditorAsync(id, command, handler, operation, itemId, ct);
        // Toggle posts contain only a task version and one checklist item, rather than the full editor.
        ModelState.Remove(nameof(DevSync.Web.Features.Tasks.Edit.Command.Title));
        ModelState.Remove(nameof(DevSync.Web.Features.Tasks.Edit.Command.Priority));
        if (!ModelState.IsValid) return await MutationResultAsync(id, command.Version, null, new(422, "Check the task version and checklist value."), details, returnUrl, ct);
        var result = itemId is null ? new Edit.Result(422, "Choose a checklist item.") :
            await handler.ToggleAsync(id, OwnerId, command.Version, itemId.Value, isCompleted, ct);
        return await MutationResultAsync(id, command.Version, null, result, details, returnUrl, ct);
    }
    private async Task<IActionResult> MutationResultAsync(Guid id, Guid submittedVersion, Guid? submittedBoardVersion, Edit.Result result,
        Details.Handler details, string? returnUrl, CancellationToken ct)
    {
        if (result.Status == 404) return NotFound();
        if (result.Error is not null)
        {
            var model = await details.HandleAsync(id, OwnerId, 1, ct);
            if (model is null) return NotFound();
            Response.StatusCode = result.Status; ModelState.AddModelError("", result.Error);
            ViewData["SavePaused"] = result.Status == 409;
            // Retain rejected tokens in every displayed action form. Reloading is required to obtain fresh versions.
            return View("Details", model with { Version = submittedVersion, BoardVersion = submittedBoardVersion ?? model.BoardVersion });
        }
        TempData["Success"] = "Task updated.";
        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/tasks/" + id);
    }
}
