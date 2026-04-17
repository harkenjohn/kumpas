using Kumpas.AdminWeb.Services;
using Kumpas.AdminWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumpas.AdminWeb.Controllers;

[Authorize(Policy = "AdminOnly")]
public class AccountsController : Controller
{
    private readonly AccountService _accountService;
    private readonly ISupabaseAuthService _supabaseAuthService;

    public AccountsController(AccountService accountService, ISupabaseAuthService supabaseAuthService)
    {
        _accountService = accountService;
        _supabaseAuthService = supabaseAuthService;
    }

    public async Task<IActionResult> Index(string? search, string? status, string? userType, int page = 1, CancellationToken cancellationToken = default)
    {
        var model = await _accountService.GetAccountsAsync(search, status, userType, page, cancellationToken: cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await _accountService.GetAccountDetailsAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        if (await _accountService.SetActiveStatusAsync(id, isActive, cancellationToken))
        {
            TempData["StatusMessage"] = $"Account has been {(isActive ? "activated" : "deactivated")}.";
        }
        else
        {
            TempData["ErrorMessage"] = "Account was not found.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatusFromDetails(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        if (await _accountService.SetActiveStatusAsync(id, isActive, cancellationToken))
        {
            TempData["StatusMessage"] = $"Account has been {(isActive ? "activated" : "deactivated")}.";
        }
        else
        {
            TempData["ErrorMessage"] = "Account was not found.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePassword(UpdateUserPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Password update failed. Check the minimum password length and confirmation.";
            return RedirectToAction(nameof(Index));
        }

        var profile = await _accountService.GetProfileAsync(model.UserId, cancellationToken);
        if (profile is null)
        {
            TempData["ErrorMessage"] = "Account was not found.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _supabaseAuthService.UpdatePasswordAsync(model.UserId, model.NewPassword, cancellationToken);
        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] =
            result.Succeeded ? "Password updated successfully." : result.ErrorMessage ?? "Password update failed.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePasswordFromDetails(UpdateUserPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Password update failed. Check the minimum password length and confirmation.";
            return RedirectToAction(nameof(Details), new { id = model.UserId });
        }

        var profile = await _accountService.GetProfileAsync(model.UserId, cancellationToken);
        if (profile is null)
        {
            TempData["ErrorMessage"] = "Account was not found.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _supabaseAuthService.UpdatePasswordAsync(model.UserId, model.NewPassword, cancellationToken);
        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] =
            result.Succeeded ? "Password updated successfully." : result.ErrorMessage ?? "Password update failed.";

        return RedirectToAction(nameof(Details), new { id = model.UserId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var authResult = await _supabaseAuthService.DeleteUserAsync(id, cancellationToken);
        if (!authResult.Succeeded)
        {
            TempData["ErrorMessage"] = authResult.ErrorMessage ?? "Unable to delete auth user.";
            return RedirectToAction(nameof(Index));
        }

        var profileDeleted = await _accountService.DeleteProfileAsync(id, cancellationToken);
        TempData[profileDeleted ? "StatusMessage" : "ErrorMessage"] =
            profileDeleted ? "Account deleted successfully." : "Auth user deleted, but profile cleanup failed.";

        return RedirectToAction(nameof(Index));
    }
}
