using Kumpas.AdminWeb.Data;
using Kumpas.AdminWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kumpas.AdminWeb.Controllers;

[Authorize(Policy = "AdminOnly")]
public class ReportsController : Controller
{
    private readonly KumpasDbContext _dbContext;

    public ReportsController(KumpasDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string? search)
    {
        var fromUtc = fromDate?.Date ?? DateTime.UtcNow.Date.AddDays(-30);
        var toUtc = toDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow;

        var totalAccounts = await _dbContext.Profiles.CountAsync();
        var activeAccounts = await _dbContext.Profiles.CountAsync(x => x.IsActive);

        var totalSessions = await _dbContext.ChatSessions.CountAsync(x =>
            x.CreatedAt.HasValue &&
            x.CreatedAt.Value.UtcDateTime >= fromUtc &&
            x.CreatedAt.Value.UtcDateTime <= toUtc);

        var totalMessages = await _dbContext.ChatMessages.CountAsync(x =>
            x.CreatedAt.HasValue &&
            x.CreatedAt.Value.UtcDateTime >= fromUtc &&
            x.CreatedAt.Value.UtcDateTime <= toUtc);

        var sessionGroups = await _dbContext.ChatSessions
            .AsNoTracking()
            .Where(x =>
                x.CreatedAt.HasValue &&
                x.CreatedAt.Value.UtcDateTime >= fromUtc &&
                x.CreatedAt.Value.UtcDateTime <= toUtc)
            .GroupBy(x => DateOnly.FromDateTime(x.CreatedAt!.Value.UtcDateTime))
            .Select(x => new
            {
                Day = x.Key,
                Sessions = x.Count()
            })
            .ToListAsync();

        var messageGroups = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(x =>
                x.CreatedAt.HasValue &&
                x.CreatedAt.Value.UtcDateTime >= fromUtc &&
                x.CreatedAt.Value.UtcDateTime <= toUtc)
            .GroupBy(x => DateOnly.FromDateTime(x.CreatedAt!.Value.UtcDateTime))
            .Select(x => new
            {
                Day = x.Key,
                Messages = x.Count()
            })
            .ToListAsync();

        var dailyUsage = sessionGroups
            .GroupJoin(
                messageGroups,
                s => s.Day,
                m => m.Day,
                (s, mg) => new DailyUsageRowViewModel
                {
                    Day = s.Day,
                    Sessions = s.Sessions,
                    Messages = mg.FirstOrDefault()?.Messages ?? 0
                })
            .OrderByDescending(x => x.Day)
            .ToList();

        var topUsersQuery =
            from message in _dbContext.ChatMessages.AsNoTracking()
            join profile in _dbContext.Profiles.AsNoTracking() on message.SenderId equals profile.Id
            join authUser in _dbContext.AuthUsers.AsNoTracking() on profile.Id equals authUser.Id into authJoin
            from authUser in authJoin.DefaultIfEmpty()
            where message.CreatedAt.HasValue &&
                  message.CreatedAt.Value.UtcDateTime >= fromUtc &&
                  message.CreatedAt.Value.UtcDateTime <= toUtc
            group new { profile, authUser } by new
            {
                profile.FirstName,
                profile.LastName,
                Email = authUser != null ? authUser.Email : null
            }
            into grouped
            select new TopUserRowViewModel
            {
                UserName = (((grouped.Key.FirstName ?? string.Empty) + " " + (grouped.Key.LastName ?? string.Empty)).Trim()),
                Email = grouped.Key.Email ?? string.Empty,
                MessageCount = grouped.Count()
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            topUsersQuery = topUsersQuery.Where(x =>
                x.UserName.ToLower().Contains(term) ||
                x.Email.ToLower().Contains(term));
        }

        var topUsers = await topUsersQuery
            .OrderByDescending(x => x.MessageCount)
            .Take(10)
            .ToListAsync();

        var messageTypes = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(x =>
                x.CreatedAt.HasValue &&
                x.CreatedAt.Value.UtcDateTime >= fromUtc &&
                x.CreatedAt.Value.UtcDateTime <= toUtc)
            .GroupBy(x => x.GestureId.HasValue ? "Gesture" : "Text")
            .Select(x => new MessageTypeRowViewModel
            {
                MessageType = x.Key,
                Total = x.Count()
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync();

        var model = new ReportsViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            Search = search,
            TotalAccounts = totalAccounts,
            ActiveAccounts = activeAccounts,
            TotalSessions = totalSessions,
            TotalMessages = totalMessages,
            DailyUsage = dailyUsage,
            TopUsers = topUsers,
            MessageTypes = messageTypes
        };

        return View(model);
    }
}
