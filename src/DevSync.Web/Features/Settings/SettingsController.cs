using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DevSync.Web.Features.Settings;
[Authorize(Policy="Member"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class SettingsController(Handler handler):Controller
{
    private Guid UserId=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("/account/settings")]
    public async Task<IActionResult> Index(CancellationToken ct)=>View(await handler.LoadAsync(UserId,ct));
    [HttpPost("/account/settings")]
    public async Task<IActionResult> Save(Command command,CancellationToken ct)
    {
        var latest=await handler.LoadAsync(UserId,ct);command.Handle=latest.Handle;
        var result=ModelState.IsValid?await handler.SaveAsync(UserId,command,ct):(422,"Check your settings.");
        if(result.Item1==404)return NotFound();
        if(result.Item1!=200){Response.StatusCode=result.Item1;ModelState.AddModelError("",result.Item2!);return View("Index",command);}
        TempData["Success"]="Settings saved.";return Redirect("/account/settings");
    }
}
