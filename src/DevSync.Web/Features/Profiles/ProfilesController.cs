using System.Security.Claims;
using DevSync.Web.Features.Profiles.Edit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevSync.Web.Features.Profiles;

[Authorize(Policy = "Member")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfilesController(Handler handler) : Controller
{
    [AllowAnonymous, HttpGet("/people/{handle}")]
    public async Task<IActionResult> Public(string handle, [FromServices] Public.Handler query, int page = 1, CancellationToken ct = default)
    {
        var model = await query.HandleAsync(handle, page, ct);
        return model is null ? NotFound() : View(model);
    }

    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("/account/profile")]
    public async Task<IActionResult> Edit(CancellationToken cancellationToken)
    {
        var command = await handler.LoadAsync(UserId, cancellationToken);
        return command is null ? NotFound() : View(command);
    }

    [HttpPost("/account/profile")]
    public async Task<IActionResult> Edit(Command command, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(command);
        var error = await handler.HandleAsync(UserId, command, cancellationToken);
        if (error is null)
        {
            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Edit));
        }
        ModelState.AddModelError("", error);
        var latest = await handler.LoadAsync(UserId, cancellationToken);
        if (latest is not null)
        {
            command.Version = latest.Version;
            ModelState.Remove(nameof(command.Version));
        }
        return View(command);
    }
}
