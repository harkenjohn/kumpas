using System.Security.Claims;
using Kumpas.AdminWeb.Data;
using Kumpas.AdminWeb.Services;
using Kumpas.AdminWeb.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kumpas.AdminWeb.Controllers;

public class AuthController : Controller
{
    private readonly KumpasDbContext _dbContext;
    private readonly ISupabaseAuthService _supabaseAuthService;

    public AuthController(KumpasDbContext dbContext, ISupabaseAuthService supabaseAuthService)
    {
        _dbContext = dbContext;
        _supabaseAuthService = supabaseAuthService;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var loginResult = await _supabaseAuthService.SignInAsync(model.Email, model.Password, cancellationToken);
        if (!loginResult.Succeeded || loginResult.UserId is null)
        {
            ModelState.AddModelError(string.Empty, loginResult.ErrorMessage ?? "Invalid email or password.");
            return View(model);
        }

        var profile = await _dbContext.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == loginResult.UserId.Value, cancellationToken);

        if (profile is null)
        {
            ModelState.AddModelError(string.Empty, "No profile is linked to this account.");
            return View(model);
        }

        if (!profile.IsActive)
        {
            ModelState.AddModelError(string.Empty, "This account is inactive.");
            return View(model);
        }

        if (!string.Equals(profile.UserType, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "Only admin accounts can access this panel.");
            return View(model);
        }

        var authUser = await _dbContext.AuthUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == profile.Id, cancellationToken);

        var displayName = $"{profile.FirstName ?? string.Empty} {profile.LastName ?? string.Empty}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = authUser?.Email ?? "Administrator";
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, profile.Id.ToString()),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Email, authUser?.Email ?? model.Email),
            new("UserType", profile.UserType ?? string.Empty)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(model.RememberMe ? 24 : 8)
            });

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
