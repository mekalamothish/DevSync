using DevSync.Web.Features.Home.Index;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DevSync.Web.Features.Home;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class HomeController(Handler handler) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await handler.HandleAsync(
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null, ct));
}
