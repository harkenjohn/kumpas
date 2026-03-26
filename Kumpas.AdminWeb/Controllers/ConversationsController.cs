using Kumpas.AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumpas.AdminWeb.Controllers;

[Authorize(Policy = "AdminOnly")]
public class ConversationsController : Controller
{
    private readonly ConversationService _conversationService;

    public ConversationsController(ConversationService conversationService)
    {
        _conversationService = conversationService;
    }

    public async Task<IActionResult> Index(string? search, DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken)
    {
        var model = await _conversationService.GetSessionsAsync(search, fromDate, toDate, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var model = await _conversationService.GetSessionDetailsAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (await _conversationService.DeleteSessionAsync(id, cancellationToken))
        {
            TempData["StatusMessage"] = "Conversation deleted successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = "Conversation not found.";
        }

        return RedirectToAction(nameof(Index));
    }
}