using System.Security.Claims;
using Kumpas.AdminWeb.Services;
using Kumpas.AdminWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumpas.AdminWeb.Controllers;

[Authorize(Policy = "AdminOnly")]
public class SettingsController(AccountService accountService, ISupabaseAuthService supabaseAuthService) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return RedirectToAction("Login", "Auth");
        }

        var profile = await accountService.GetProfileAsync(userId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        return View(new ProfileSettingsViewModel
        {
            UserId = profile.Id,
            FirstName = profile.FirstName ?? string.Empty,
            LastName = profile.LastName ?? string.Empty,
            Email = profile.AuthUser?.Email ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileSettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var updated = await accountService.UpdateProfileAsync(model, cancellationToken);
        if (!updated)
        {
            TempData["ErrorMessage"] = "Profile update failed.";
            return RedirectToAction(nameof(Index));
        }

        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            var passwordResult = await supabaseAuthService.UpdatePasswordAsync(model.UserId, model.NewPassword, cancellationToken);
            if (!passwordResult.Succeeded)
            {
                TempData["ErrorMessage"] = passwordResult.ErrorMessage ?? "Profile updated, but password change failed.";
                return RedirectToAction(nameof(Index));
            }
        }

        TempData["StatusMessage"] = "Profile settings saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
