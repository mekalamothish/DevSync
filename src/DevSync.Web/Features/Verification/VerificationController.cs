using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DevSync.Web.Features.Verification;
[Authorize(Policy="Member"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class VerificationController(Handler handler):Controller
{
    private Guid UserId=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("/account/verification")]
    public async Task<IActionResult> Index(CancellationToken ct)=>View(await handler.LoadAsync(UserId,ct));
    [HttpPost("/account/verification")]
    public async Task<IActionResult> Submit(Command command,CancellationToken ct)
    {
        var result=ModelState.IsValid?await handler.SubmitAsync(UserId,command,ct):(422,"Check your evidence link and statement.");
        if(result.Item1==404)return NotFound();
        if(result.Item1!=200){Response.StatusCode=result.Item1;ModelState.AddModelError("",result.Item2!);var model=await handler.LoadAsync(UserId,ct);return View("Index",model with{Command=command});}
        TempData["Success"]="Verification request submitted.";return Redirect("/account/verification");
    }
}
