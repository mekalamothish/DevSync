using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DevSync.Web.Features.Errors;

[AllowAnonymous]
public sealed class ErrorsController : Controller
{
    [Route("/errors/{statusCode:int}")]
    [IgnoreAntiforgeryToken]
    public IActionResult Index(int statusCode)
    {
        Response.StatusCode = statusCode is >= 400 and <= 599 ? statusCode : 500;
        return View(new ViewModel(Response.StatusCode));
    }
}
