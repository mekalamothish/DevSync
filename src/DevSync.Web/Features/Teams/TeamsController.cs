using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DevSync.Web.Features.Teams;

[Authorize(Policy = "Member")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TeamsController : Controller
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("/teams")]
    public async Task<IActionResult> List([FromServices] List.Handler handler, CancellationToken ct) => View(await handler.HandleAsync(Actor, ct));
    [HttpPost("/teams/create")]
    public async Task<IActionResult> Create(string? name, [FromServices] Manage.Handler handler, CancellationToken ct)
    {
        var result = await handler.CreateAsync(Actor, name, ct);
        if (result.Error is not null) return Error(result, null);
        return Redirect("/teams/" + result.Id);
    }
    [HttpGet("/teams/{id:guid}")]
    public async Task<IActionResult> Workspace(Guid id, [FromServices] Workspace.Handler handler, string? kind, bool archived = false, int page = 1, CancellationToken ct = default)
    {
        var model = await handler.LoadAsync(id, Actor, kind, archived, page, ct);
        return model is null ? NotFound() : View(model);
    }
    [HttpGet("/teams/{id:guid}/members")]
    public async Task<IActionResult> Manage(Guid id, [FromServices] Manage.Handler handler, CancellationToken ct)
    {
        var model = await handler.LoadAsync(id, Actor, ct); return model is null ? NotFound() : View(model);
    }
    [HttpPost("/teams/{id:guid}/members")]
    public async Task<IActionResult> Members(Guid id, Guid version, string operation, Guid? memberId, string? role, [FromServices] Manage.Handler handler, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await handler.ChangeAsync(id, Actor, version, operation, memberId, role, ct);
        if (result.Status != 200) return Error(result, id);
        TempData["Success"] = "Team membership updated.";
        return Redirect(operation == "leave" ? "/teams" : $"/teams/{id}/members");
    }
    [HttpPost("/teams/{id:guid}/invitations")]
    public async Task<IActionResult> Invite(Guid id, Guid version, string? email, string? role, [FromServices] Invitations.Handler handler,
        [FromServices] Invitations.InvitationMail mail, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await handler.InviteAsync(id, Actor, version, email, role, ct);
        if (result.Status != 200) return Error(result, id);
        TempData["InvitationLink"] = "/teams/invitations/accept?token=" + result.Token;
        TempData["Success"] = await mail.SendAsync(result.Id!.Value,result.Token!,ct);
        return Redirect($"/teams/{id}/members");
    }
    [HttpPost("/teams/{id:guid}/invitations/{inviteId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, Guid inviteId, Guid version, [FromServices] Invitations.Handler handler, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await handler.RevokeAsync(id, inviteId, Actor, version, ct);
        if (result.Status != 200) return Error(result, id);
        TempData["Success"] = "Invitation revoked."; return Redirect($"/teams/{id}/members");
    }
    [HttpGet("/teams/invitations/accept")]
    public async Task<IActionResult> Invitation(string? token, [FromServices] Invitations.Handler handler, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var model = await handler.LoadAsync(token, Actor, ct); return model is null ? NotFound() : View(model);
    }
    [HttpPost("/teams/invitations/accept")]
    public async Task<IActionResult> Accept(string? token, [FromServices] Invitations.Handler handler, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var result = await handler.AcceptAsync(token, Actor, ct);
        if (result.Status != 200) return Error(result, null);
        TempData["Success"] = "You joined the team."; return Redirect("/teams/" + result.Id);
    }
    [HttpPost("/teams/{id:guid}/items")]
    public async Task<IActionResult> CreateItem(Guid id, Workspace.Command command, [FromServices] Workspace.Handler handler, CancellationToken ct)
    {
        var result = ModelState.IsValid ? await handler.SaveAsync(id, null, Actor, command, ct)
            : new Result(422, "Check the title, content length, assignment, and date.");
        if (result.Status == 422)
        {
            var model = await handler.LoadAsync(id, Actor, null, false, 1, ct);
            if (model is null) return NotFound();
            if (!Access.Write(model.Team.Role)) return Error(new(403, "Viewers cannot change team content."), id);
            Response.StatusCode = 422; ModelState.AddModelError("", result.Error!);
            ViewData["CreateCommand"] = command;
            return View("Workspace", model);
        }
        if (result.Status != 200) return Error(result, id);
        return Redirect($"/teams/{id}/items/{result.Id}");
    }
    [HttpPost("/teams/{id:guid}/share")]
    public async Task<IActionResult> Share(Guid id, string? kind, Guid sourceId, [FromServices] Workspace.Handler handler, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await handler.ImportAsync(id, Actor, kind, sourceId, ct);
        if (result.Status != 200) return Error(result, id);
        TempData["Success"] = "A separate team copy was created. Your personal original stays private.";
        return Redirect($"/teams/{id}/items/{result.Id}");
    }
    [HttpGet("/teams/{id:guid}/items/{itemId:guid}")]
    public async Task<IActionResult> Item(Guid id, Guid itemId, [FromServices] Workspace.Handler handler, CancellationToken ct)
    {
        var model = await handler.ItemAsync(id, itemId, Actor, ct); return model is null ? NotFound() : View(model);
    }
    [HttpPost("/teams/{id:guid}/items/{itemId:guid}/save")]
    public async Task<IActionResult> Save(Guid id, Guid itemId, Workspace.Command command, [FromServices] Workspace.Handler handler, CancellationToken ct)
    {
        var model = await handler.ItemAsync(id, itemId, Actor, ct); if (model is null) return NotFound();
        if (!Access.Write(model.Team.Role)) return Error(new(403, "Viewers cannot change team content."), id);
        var result = ModelState.IsValid ? await handler.SaveAsync(id, itemId, Actor, command, ct) : new Result(422, "Check the title, content length, assignment, and date.");
        if (result.Status == 404) return NotFound();
        if (result.Status != 200)
        {
            if (result.Status == 403) return Error(result, id);
            Response.StatusCode = result.Status; ModelState.AddModelError("", result.Error!);
            ViewData["SavePaused"] = result.Status == 409;
            command.Kind = model.Command.Kind;
            return View("Item", model with { Command = command });
        }
        TempData["Success"] = "Team content saved."; return Redirect($"/teams/{id}/items/{itemId}");
    }
    [HttpPost("/teams/{id:guid}/items/{itemId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, Guid itemId, Guid version, bool archived, [FromServices] Workspace.Handler handler, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await handler.ArchiveAsync(id, itemId, Actor, version, archived, ct);
        if (result.Status != 200) return Error(result, id);
        return Redirect($"/teams/{id}/items/{itemId}");
    }
    [NonAction]
    private IActionResult Error(Result result, Guid? id)
    {
        if (result.Status == 404) return NotFound();
        Response.StatusCode = result.Status; ViewData["TeamId"] = id;
        return View("Error", result.Error ?? "The request could not be completed.");
    }
}
