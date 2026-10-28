using DevSync.Web.Features.Account.Message;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DevSync.Infrastructure.Identity;

namespace DevSync.Web.Features.Account;

[EnableRateLimiting("account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController : Controller
{
    [HttpGet("/account/register")]
    public IActionResult Register() => View(new Register.Command());

    [HttpPost("/account/register")]
    public async Task<IActionResult> Register(Register.Command command, [FromServices] Register.Handler handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(command);
        foreach (var error in await handler.HandleAsync(command, cancellationToken)) ModelState.AddModelError("", error);
        return ModelState.IsValid ? RedirectToAction(nameof(CheckEmail)) : View(command);
    }

    [HttpGet("/account/sign-in")]
    public IActionResult SignIn(string? returnUrl) => User.Identity?.IsAuthenticated == true
        ? LocalRedirect(LocalReturn(returnUrl)) : View(new SignIn.Command { ReturnUrl = LocalReturn(returnUrl) });

    [HttpPost("/account/sign-in")]
    public async Task<IActionResult> SignIn(SignIn.Command command, [FromServices] SignIn.Handler handler)
    {
        if (!ModelState.IsValid) return View(command);
        if (await handler.HandleAsync(command)) return LocalRedirect(LocalReturn(command.ReturnUrl));
        ModelState.AddModelError("", "Unable to sign in. Check your credentials, verify your email, or try again later.");
        return View(command);
    }

    [Authorize]
    [HttpPost("/account/sign-out")]
    public async Task<IActionResult> SignOut([FromServices] SignInManager<ApplicationUser> signIn)
    {
        await signIn.SignOutAsync();
        return Redirect("/");
    }

    [HttpGet("/account/check-email")]
    public IActionResult CheckEmail() => Message(new("Check your inbox", "If your request is eligible, an email with a link will arrive shortly. Check your spam folder or request another link."));

    [HttpGet("/account/confirm-email")]
    public async Task<IActionResult> ConfirmEmail(Guid userId, string? token, [FromServices] ConfirmEmail.Handler handler)
    {
        var success = !string.IsNullOrWhiteSpace(token) && token.Length <= 4096 && await handler.HandleAsync(userId, token);
        Response.StatusCode = success ? 200 : 400;
        return Message(success ? new("Email verified", "You're ready to sign in and start building your workspace.")
            : new("Unable to verify email", "This link is invalid or expired. Request another verification email.", true));
    }

    [HttpGet("/account/forgot-password")]
    public IActionResult ForgotPassword() => EmailView(true, new RequestEmail.Command());

    [HttpPost("/account/forgot-password")]
    public async Task<IActionResult> ForgotPassword(RequestEmail.Command command, [FromServices] RequestEmail.Handler handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return EmailView(true, command);
        await handler.HandleAsync(command, true, cancellationToken);
        return RedirectToAction(nameof(CheckEmail));
    }

    [HttpGet("/account/resend-verification")]
    public IActionResult ResendVerification() => EmailView(false, new RequestEmail.Command());

    [HttpPost("/account/resend-verification")]
    public async Task<IActionResult> ResendVerification(RequestEmail.Command command, [FromServices] RequestEmail.Handler handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return EmailView(false, command);
        await handler.HandleAsync(command, false, cancellationToken);
        return RedirectToAction(nameof(CheckEmail));
    }

    [HttpGet("/account/reset-password")]
    public IActionResult ResetPassword(Guid userId, string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096)
        {
            Response.StatusCode = 400;
            return Message(new("Invalid recovery link", "Request another password recovery email.", true));
        }
        return View(new ResetPassword.Command { UserId = userId, Token = token });
    }

    [HttpPost("/account/reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPassword.Command command, [FromServices] ResetPassword.Handler handler)
    {
        if (!ModelState.IsValid) return View(command);
        foreach (var error in await handler.HandleAsync(command)) ModelState.AddModelError("", error);
        return ModelState.IsValid ? Message(new("Password updated", "Sign in with your new password.")) : View(command);
    }

    [HttpGet("/account/access-denied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = 403;
        return Message(new("Access denied", "Your account doesn't have permission to open this page.", true));
    }

    private string LocalReturn(string? value) => Url.IsLocalUrl(value) ? value! : "/";
    private ViewResult Message(ViewModel model) => View("/Features/Account/Message/View.cshtml", model);
    private ViewResult EmailView(bool reset, RequestEmail.Command command)
    {
        ViewData["ResetPassword"] = reset;
        return View("/Features/Account/RequestEmail/View.cshtml", command);
    }
}
