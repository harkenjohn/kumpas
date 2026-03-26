using Kumpas.AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumpas.AdminWeb.Controllers;

[Authorize(Policy = "AdminOnly")]
public class TranslationServicesController(AdminAnalyticsService analyticsService) : Controller
{
    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken)
    {
        var model = await analyticsService.GetTranslationServicesAsync(fromDate, toDate, cancellationToken);
        return View(model);
    }
}
