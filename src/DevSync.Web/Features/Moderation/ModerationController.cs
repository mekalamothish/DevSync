using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace DevSync.Web.Features.Moderation;
[Authorize(Policy="Moderator"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ModerationController(Handler handler):Controller
{
    private Guid Actor=>Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("/moderation")]
    public async Task<IActionResult> Index(string? status,int page=1,CancellationToken ct=default)=>View(await handler.QueueAsync(status,page,ct));
    [HttpGet("/moderation/verification/{id:guid}")]
    public async Task<IActionResult> Verification(Guid id,CancellationToken ct){var model=await handler.VerificationAsync(id,ct);return model is null?NotFound():View(model);}
    [HttpPost("/moderation/verification/{id:guid}")]
    public async Task<IActionResult> Review(Guid id,Guid version,string? action,string? reason,CancellationToken ct)
    {
        if(!ModelState.IsValid)return BadRequest();
        var result=await handler.ReviewAsync(id,Actor,version,action,reason,ct);
        if(result.Status==404)return NotFound();
        if(result.Status!=200){Response.StatusCode=result.Status;ModelState.AddModelError("",result.Error!);ViewData["Paused"]=result.Status==409;var model=await handler.VerificationAsync(id,ct);return model is null?NotFound():View("Verification",model with{Request=model.Request with{Version=version}});}
        TempData["Success"]="Verification decision recorded.";return Redirect($"/moderation/verification/{id}");
    }
    [HttpGet("/moderation/reports/{id:guid}")]
    public async Task<IActionResult> Report(Guid id,CancellationToken ct){var model=await handler.ReportAsync(id,ct);return model is null?NotFound():View(model);}
    [HttpPost("/moderation/reports/{id:guid}")]
    public async Task<IActionResult> Act(Guid id,Guid version,Guid targetVersion,string? action,string? reason,CancellationToken ct)
    {
        if(!ModelState.IsValid)return BadRequest();
        var result=await handler.ActAsync(id,Actor,version,targetVersion,action,reason,ct);
        if(result.Status==404)return NotFound();
        if(result.Status!=200){Response.StatusCode=result.Status;ModelState.AddModelError("",result.Error!);ViewData["Paused"]=result.Status==409;var model=await handler.ReportAsync(id,ct);return model is null?NotFound():View("Report",model with{Report=model.Report with{Version=version},TargetVersion=targetVersion});}
        TempData["Success"]="Moderation action recorded.";return Redirect($"/moderation/reports/{id}");
    }
}
[Authorize(Policy="Member"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ReportsController(Handler handler):Controller
{
    [HttpPost("/blogs/{id:guid}/report")]
    public async Task<IActionResult> Submit(Guid id,Guid? commentId,string? category,string? details,CancellationToken ct)
    {
        if(!ModelState.IsValid)return BadRequest();
        var actor=Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result=await handler.ReportAsync(actor,id,commentId,category,details,ct);
        if(result.Status==404)return NotFound();
        if(result.Status!=200){Response.StatusCode=result.Status;ViewData["Title"]="Report";return View("/Features/Moderation/ReportError.cshtml",result.Error);}
        TempData["Success"]="Report received. A reviewer can now assess it.";return Redirect("/blogs/"+await HttpContext.RequestServices.GetRequiredService<DevSync.Web.Features.Blogs.Interact.Handler>().PublishedSlugAsync(id,ct));
    }
}
