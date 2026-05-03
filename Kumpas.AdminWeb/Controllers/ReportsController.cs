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
        var model = await BuildReportViewModelAsync(fromDate, toDate, search, page);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Print(DateTime? fromDate, DateTime? toDate, string? search)
    {
        var model = await BuildReportViewModelAsync(fromDate, toDate, search, 1, 5);
        return View(model);
    }

    private async Task<ReportsViewModel> BuildReportViewModelAsync(DateTime? fromDate, DateTime? toDate, string? search, int page, int pageSize = 10)
    {
        page = Math.Max(page, 1);

        var fromUtc = fromDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc))
            : new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-30), TimeSpan.Zero);
        var toUtc = toDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc))
            : new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero);

        var nonAdminProfiles = _dbContext.Profiles.Where(x => !EF.Functions.ILike(x.UserType ?? string.Empty, "admin"));

        var totalAccounts = await nonAdminProfiles.CountAsync();
        var activeAccounts = await nonAdminProfiles.CountAsync(x => x.IsActive);
        var inactiveAccounts = totalAccounts - activeAccounts;
        var activeAccountPercent = totalAccounts == 0
            ? 0
            : Math.Round(activeAccounts * 100m / totalAccounts, 1);

        var totalArModels = await CountRowsIfTableExistsAsync("ar_models");
        var configuredModelUrl = _configuration["ModelAssets:ArModelUrl"];
        var configuredModelProvider = _configuration["ModelAssets:ArModelProvider"] ?? "Hugging Face";
        var configuredModelStatus = _configuration["ModelAssets:Status"];

        var philippineZone = GetPhilippineTimeZone();
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var generatedAt = TimeZoneInfo.ConvertTime(generatedAtUtc, philippineZone);
        var uptimeReportDate = DateOnly.FromDateTime((toDate ?? generatedAt.Date).Date);
        var uptimeHours = await GetModelUptimeHoursAsync(uptimeReportDate);
        var uptimeHoursWithData = uptimeHours.Where(x => x.HasData).ToList();
        var uptimePercent = uptimeHoursWithData.Count == 0
            ? 0
            : Math.Round(uptimeHoursWithData.Average(x => x.UptimePercent), 1);
        var modelStatus = uptimeHoursWithData.Count == 0
            ? "No uptime data"
            : uptimeHoursWithData.Any(x => !x.IsUp)
                ? "Issues detected"
                : "OK";

        var localYearStart = new DateTime(generatedAt.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var localMonthStart = new DateTime(generatedAt.Year, generatedAt.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var localDayStart = generatedAt.Date;

        var yearStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localYearStart, philippineZone), TimeSpan.Zero);
        var monthStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMonthStart, philippineZone), TimeSpan.Zero);
        var dayStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDayStart, philippineZone), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var totalSessions = await _dbContext.ChatSessions.CountAsync(x =>
            x.CreatedAt.HasValue &&
            x.CreatedAt.Value >= fromUtc &&
            x.CreatedAt.Value < toUtc);

        var totalMessages = await _dbContext.ChatMessages.CountAsync(x =>
            x.CreatedAt.HasValue &&
            x.CreatedAt.Value >= fromUtc &&
            x.CreatedAt.Value < toUtc);

        var periodLength = Math.Max(1, (toUtc.UtcDateTime.Date - fromUtc.UtcDateTime.Date).Days);
        var previousFromUtc = fromUtc.AddDays(-periodLength);
        var previousToUtc = fromUtc;

        var previousSessions = await _dbContext.ChatSessions.CountAsync(x =>
            x.CreatedAt.HasValue &&
            x.CreatedAt.Value >= previousFromUtc &&
            x.CreatedAt.Value < previousToUtc);

        var previousMessages = await _dbContext.ChatMessages.CountAsync(x =>
            x.CreatedAt.HasValue &&
            x.CreatedAt.Value >= previousFromUtc &&
            x.CreatedAt.Value < previousToUtc);

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
                  message.CreatedAt.Value < toUtc &&
                  !EF.Functions.ILike(profile.UserType ?? string.Empty, "admin")
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
            var term = search.Trim().ToLowerInvariant();
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

        var messageTypes = await GetMessageTypeBreakdownAsync(fromUtc, toUtc);

        return new ReportsViewModel
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
            ActiveAccountPercent = activeAccountPercent,
            SessionChangePercent = CalculateTrendPercent(totalSessions, previousSessions),
            MessageChangePercent = CalculateTrendPercent(totalMessages, previousMessages),
            ModelProvider = string.IsNullOrWhiteSpace(configuredModelUrl) ? "Not configured" : configuredModelProvider,
            ModelUrl = configuredModelUrl,
            ModelStatus = uptimeHoursWithData.Count > 0 || string.IsNullOrWhiteSpace(configuredModelStatus)
                ? modelStatus
                : configuredModelStatus,
            UptimeReportDate = uptimeReportDate,
            ModelUptimePercent = uptimePercent,
            ErrorLogsThisYear = await CountErrorLogsAsync(yearStart, generatedAtUtc),
            ErrorLogsThisMonth = await CountErrorLogsAsync(monthStart, generatedAtUtc),
            ErrorLogsToday = await CountErrorLogsAsync(dayStart, dayEnd),
            GeneratedBy = User.Identity?.Name ?? "Administrator",
            GeneratedAt = generatedAt,
            DailyUsage = dailyUsage,
            ModelUptimeHours = uptimeHours,
            TopUsers = topUsers,
            MessageTypes = messageTypes,
            TopUsersPagination = new PaginationViewModel
            {
                Action = nameof(Index),
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
    }

    private async Task<IReadOnlyList<ModelUptimeHourViewModel>> GetModelUptimeHoursAsync(DateOnly date)
    {
        var connectionString = _dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return BuildEmptyUptimeHours();
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            const string tableCheckSql = """
                select 1
                from information_schema.tables
                where table_schema = 'public' and table_name = 'model_status_logs'
                limit 1;
                """;

            await using (var tableCommand = new NpgsqlCommand(tableCheckSql, connection))
            {
                var tableExists = await tableCommand.ExecuteScalarAsync();
                if (tableExists is null || tableExists == DBNull.Value)
                {
                    return BuildEmptyUptimeHours();
                }
            }

            var philippineZone = GetPhilippineTimeZone();
            var localStart = date.ToDateTime(TimeOnly.MinValue);
            var localEnd = localStart.AddDays(1);
            var utcStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, philippineZone), TimeSpan.Zero);
            var utcEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, philippineZone), TimeSpan.Zero);

            const string sql = """
                select extract(hour from recorded_at at time zone 'Asia/Manila')::int as hour,
                       count(*)::int as total_checks,
                       count(*) filter (where upper(coalesce(status, '')) = 'OK')::int as up_checks
                from public.model_status_logs
                where recorded_at >= @from
                  and recorded_at < @to
                group by hour
                order by hour;
                """;

            var hourlyData = new Dictionary<int, ModelUptimeHourViewModel>();
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("from", utcStart);
            command.Parameters.AddWithValue("to", utcEnd);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var hour = reader.GetInt32(0);
                hourlyData[hour] = new ModelUptimeHourViewModel
                {
                    Hour = hour,
                    TotalChecks = reader.GetInt32(1),
                    UpChecks = reader.GetInt32(2)
                };
            }

            return Enumerable.Range(0, 24)
                .Select(hour => hourlyData.TryGetValue(hour, out var row)
                    ? row
                    : new ModelUptimeHourViewModel { Hour = hour })
                .ToList();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            return BuildEmptyUptimeHours();
        }
    }

    private static IReadOnlyList<ModelUptimeHourViewModel> BuildEmptyUptimeHours()
    {
        return Enumerable.Range(0, 24)
            .Select(hour => new ModelUptimeHourViewModel { Hour = hour })
            .ToList();
    }

    private static TimeZoneInfo GetPhilippineTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
        }
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

    private async Task<IReadOnlyList<MessageTypeRowViewModel>> GetMessageTypeBreakdownAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var connectionString = _dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return [];
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            const string tableCheckSql = """
                select 1
                from information_schema.tables
                where table_schema = 'public' and table_name = 'chat_messages'
                limit 1;
                """;

            await using (var tableCommand = new NpgsqlCommand(tableCheckSql, connection))
            {
                var tableExists = await tableCommand.ExecuteScalarAsync();
                if (tableExists is null || tableExists == DBNull.Value)
                {
                    return [];
                }
            }

            const string sql = """
                select message_type, total
                from (
                    select 'Translated to Speech' as message_type,
                           count(*)::int as total
                    from public.chat_messages
                    where created_at >= @from
                      and created_at < @to
                      and upper(coalesce(message_type, '')) = 'TEXT_TO_SPEECH'

                    union all

                    select 'Translated to Sign' as message_type,
                           count(*)::int as total
                    from public.chat_messages
                    where created_at >= @from
                      and created_at < @to
                      and upper(coalesce(message_type, '')) = 'TEXT_TO_SIGN'
                ) counts
                where total > 0
                order by total desc;
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("from", from);
            command.Parameters.AddWithValue("to", to);

            var results = new List<MessageTypeRowViewModel>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new MessageTypeRowViewModel
                {
                    MessageType = reader.GetString(0),
                    Total = reader.GetInt32(1)
                });
            }

            return results;
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01" || ex.SqlState == "42703")
        {
            return [];
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

    private static decimal? CalculateTrendPercent(int currentValue, int previousValue)
    {
        if (previousValue <= 0)
        {
            return null;
        }

        return Math.Round((currentValue - previousValue) * 100m / previousValue, 1);
    }
}
