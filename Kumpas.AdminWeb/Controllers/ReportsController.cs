using Kumpas.AdminWeb.Data;
using Kumpas.AdminWeb.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kumpas.AdminWeb.Controllers;

[Authorize(Policy = "AdminOnly")]
public class ReportsController : Controller
{
    private readonly KumpasDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public ReportsController(KumpasDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string? search, int page = 1)
    {
        page = Math.Max(page, 1);
        const int pageSize = 10;
        var fromUtc = fromDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc))
            : new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-30), TimeSpan.Zero);
        var toUtc = toDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc))
            : new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero);

        var totalAccounts = await _dbContext.Profiles.CountAsync();
        var activeAccounts = await _dbContext.Profiles.CountAsync(x => x.IsActive);
        var inactiveAccounts = totalAccounts - activeAccounts;
        var totalArModels = await CountRowsIfTableExistsAsync("ar_models");
        var configuredModelUrl = _configuration["ModelAssets:ArModelUrl"];
        var configuredModelProvider = _configuration["ModelAssets:ArModelProvider"] ?? "Hugging Face";
        var configuredModelStatus = _configuration["ModelAssets:Status"];
        var generatedAt = DateTimeOffset.UtcNow;
        var yearStart = new DateTimeOffset(generatedAt.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var monthStart = new DateTimeOffset(generatedAt.Year, generatedAt.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var dayStart = new DateTimeOffset(generatedAt.UtcDateTime.Date, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var totalSessions = await _dbContext.ChatSessions.CountAsync(x =>
            x.CreatedAt.HasValue &&
            x.CreatedAt.Value >= fromUtc &&
            x.CreatedAt.Value < toUtc);

        var totalMessages = await _dbContext.ChatMessages.CountAsync(x =>
            x.CreatedAt.HasValue &&
            x.CreatedAt.Value >= fromUtc &&
            x.CreatedAt.Value < toUtc);

        var sessionGroups = await _dbContext.ChatSessions
            .AsNoTracking()
            .Where(x =>
                x.CreatedAt.HasValue &&
                x.CreatedAt.Value >= fromUtc &&
                x.CreatedAt.Value < toUtc)
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
                x.CreatedAt.Value >= fromUtc &&
                x.CreatedAt.Value < toUtc)
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
                  message.CreatedAt.Value >= fromUtc &&
                  message.CreatedAt.Value < toUtc
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

        var topUsersCount = await topUsersQuery.CountAsync();

        var topUsers = await topUsersQuery
            .OrderByDescending(x => x.MessageCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var messageTypes = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(x =>
                x.CreatedAt.HasValue &&
                x.CreatedAt.Value >= fromUtc &&
                x.CreatedAt.Value < toUtc)
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
            InactiveAccounts = inactiveAccounts,
            TotalSessions = totalSessions,
            TotalMessages = totalMessages,
            TotalArModels = totalArModels,
            ModelProvider = string.IsNullOrWhiteSpace(configuredModelUrl) ? "Not configured" : configuredModelProvider,
            ModelUrl = configuredModelUrl,
            ModelStatus = !string.IsNullOrWhiteSpace(configuredModelStatus)
                ? configuredModelStatus
                : !string.IsNullOrWhiteSpace(configuredModelUrl)
                    ? "Hosted externally"
                    : totalArModels > 0 ? "Operational" : "No AR models found",
            ErrorLogsThisYear = await CountErrorLogsAsync(yearStart, generatedAt),
            ErrorLogsThisMonth = await CountErrorLogsAsync(monthStart, generatedAt),
            ErrorLogsToday = await CountErrorLogsAsync(dayStart, dayEnd),
            GeneratedBy = User.Identity?.Name ?? "Administrator",
            GeneratedAt = generatedAt,
            DailyUsage = dailyUsage,
            TopUsers = topUsers,
            MessageTypes = messageTypes,
            TopUsersPagination = new PaginationViewModel
            {
                Action = "Index",
                Controller = "Reports",
                ItemLabel = "records",
                PageNumber = page,
                PageSize = pageSize,
                TotalCount = topUsersCount,
                RouteValues = new Dictionary<string, string>
                {
                    ["search"] = search ?? string.Empty,
                    ["fromDate"] = fromDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    ["toDate"] = toDate?.ToString("yyyy-MM-dd") ?? string.Empty
                }
            }
        };

        return View(model);
    }

    private async Task<int> CountErrorLogsAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var connectionString = _dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return 0;
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            const string tableCheckSql = """
                select 1
                from information_schema.tables
                where table_schema = 'public' and table_name = 'system_logs'
                limit 1;
                """;

            await using (var tableCommand = new NpgsqlCommand(tableCheckSql, connection))
            {
                var tableExists = await tableCommand.ExecuteScalarAsync();
                if (tableExists is null || tableExists == DBNull.Value)
                {
                    return 0;
                }
            }

            const string sql = """
                select count(*)
                from public.system_logs
                where timestamp >= @from
                  and timestamp < @to
                  and lower(coalesce(log_level, '')) = 'error';
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("from", from);
            command.Parameters.AddWithValue("to", to);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            return 0;
        }
    }

    private async Task<int> CountRowsIfTableExistsAsync(string tableName)
    {
        var connectionString = _dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return 0;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string tableCheckSql = """
            select 1
            from information_schema.tables
            where table_schema = 'public' and table_name = @tableName
            limit 1;
            """;

        await using (var tableCommand = new NpgsqlCommand(tableCheckSql, connection))
        {
            tableCommand.Parameters.AddWithValue("tableName", tableName);
            var tableExists = await tableCommand.ExecuteScalarAsync();
            if (tableExists is null || tableExists == DBNull.Value)
            {
                return 0;
            }
        }

        await using var command = new NpgsqlCommand($@"select count(*) from public.""{tableName}"";", connection);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}
