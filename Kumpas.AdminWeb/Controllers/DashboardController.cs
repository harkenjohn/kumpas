using Kumpas.AdminWeb.Data;
using Kumpas.AdminWeb.Models;
using Kumpas.AdminWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace Kumpas.AdminWeb.Controllers;

[Authorize(Policy = "AdminOnly")]
public class DashboardController : Controller
{
    private readonly KumpasDbContext _dbContext;

    public DashboardController(KumpasDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var utcToday = DateTime.UtcNow.Date;
        var todayStart = new DateTimeOffset(utcToday, TimeSpan.Zero);
        var todayEnd = todayStart.AddDays(1);

        var totalAccounts = await _dbContext.Profiles.CountAsync();
        var activeAccounts = await _dbContext.Profiles.CountAsync(x => x.IsActive);
        var totalSessions = await _dbContext.ChatSessions.CountAsync();
        var sessionsToday = await _dbContext.ChatSessions.CountAsync(x =>
            x.CreatedAt.HasValue &&
            x.CreatedAt.Value >= todayStart &&
            x.CreatedAt.Value < todayEnd);
        var totalMessages = await _dbContext.ChatMessages.CountAsync();
        var errorsToday = 0;

        var recentAccounts = await (
            from profile in _dbContext.Profiles.AsNoTracking()
            join authUser in _dbContext.AuthUsers.AsNoTracking() on profile.Id equals authUser.Id into authJoin
            from authUser in authJoin.DefaultIfEmpty()
            orderby profile.CreatedAt descending
            select new AccountRowViewModel
            {
                Id = profile.Id,
                FullName = ((profile.FirstName ?? string.Empty) + " " + (profile.LastName ?? string.Empty)).Trim(),
                Email = authUser != null ? authUser.Email ?? string.Empty : string.Empty,
                UserType = profile.UserType ?? string.Empty,
                IsActive = profile.IsActive,
                CreatedAt = profile.CreatedAt,
                LastSignInAt = null
            })
            .Take(5)
            .ToListAsync();

        var recentSessions = await _dbContext.ChatSessions
            .AsNoTracking()
            .Include(x => x.User1)
            .Include(x => x.User2)
            .Include(x => x.Messages)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new ConversationSessionRowViewModel
            {
                Id = x.Id,
                RoomCode = x.RoomCode ?? string.Empty,
                ParticipantOne = ((x.User1 != null ? x.User1.FirstName : string.Empty) + " " + (x.User1 != null ? x.User1.LastName : string.Empty)).Trim(),
                ParticipantTwo = ((x.User2 != null ? x.User2.FirstName : string.Empty) + " " + (x.User2 != null ? x.User2.LastName : string.Empty)).Trim(),
                MessageCount = x.Messages.Count,
                CreatedAt = x.CreatedAt,
                LastMessageAt = x.Messages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => m.CreatedAt)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var recentErrors = new List<SystemLogItemViewModel>();
        if (await SystemLogsTableExistsAsync())
        {
            try
            {
                errorsToday = await _dbContext.SystemLogs.CountAsync(x =>
                    x.Timestamp.HasValue &&
                    x.Timestamp.Value >= todayStart &&
                    x.Timestamp.Value < todayEnd &&
                    x.LogLevel != null &&
                    x.LogLevel.ToLower() == "error");

                recentErrors = await _dbContext.SystemLogs
                    .AsNoTracking()
                    .Include(x => x.User)
                    .Where(x => x.LogLevel != null && x.LogLevel.ToLower() == "error")
                    .OrderByDescending(x => x.Timestamp)
                    .Take(5)
                    .Select(x => new SystemLogItemViewModel
                    {
                        Id = x.Id.ToString(),
                        Level = x.LogLevel ?? string.Empty,
                        Message = x.Message ?? string.Empty,
                        Source = x.Module ?? string.Empty,
                        UserName = x.User != null
                            ? ((x.User.FirstName ?? string.Empty) + " " + (x.User.LastName ?? string.Empty)).Trim()
                            : "System",
                        CreatedAt = x.Timestamp
                    })
                    .ToListAsync();
            }
            catch (PostgresException ex) when (ex.SqlState == "42P01")
            {
                errorsToday = 0;
                recentErrors = [];
            }
        }

        var model = new DashboardViewModel
        {
            TotalAccounts = totalAccounts,
            ActiveAccounts = activeAccounts,
            TotalSessions = totalSessions,
            SessionsToday = sessionsToday,
            TotalMessages = totalMessages,
            ErrorsToday = errorsToday,
            RecentAccounts = recentAccounts,
            RecentSessions = recentSessions,
            RecentErrors = recentErrors
        };

        return View(model);
    }

    private async Task<bool> SystemLogsTableExistsAsync()
    {
        await using var connection = new NpgsqlConnection(_dbContext.Database.GetConnectionString());
        await connection.OpenAsync();

        const string sql = """
            select 1
            from information_schema.tables
            where table_schema = 'public' and table_name = 'system_logs'
            limit 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync();
        return result is not null && result != DBNull.Value;
    }

    [AllowAnonymous]
    public IActionResult Error()
    {
        return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
        {
            RequestId = HttpContext.TraceIdentifier
        });
    }
}
