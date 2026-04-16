using System.Data;
using Kumpas.AdminWeb.Data;
using Kumpas.AdminWeb.ViewModels;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kumpas.AdminWeb.Services;

public class AdminAnalyticsService(KumpasDbContext dbContext, IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var utcToday = DateTime.UtcNow.Date;
        var todayStart = new DateTimeOffset(utcToday, TimeSpan.Zero);
        var todayEnd = todayStart.AddDays(1);
        var conversationService = new ConversationService(dbContext);

        return new DashboardViewModel
        {
            TotalAccounts = await dbContext.Profiles.CountAsync(cancellationToken),
            ActiveAccounts = await dbContext.Profiles.CountAsync(x => x.IsActive, cancellationToken),
            TotalSessions = await dbContext.ChatSessions.CountAsync(cancellationToken),
            SessionsToday = await dbContext.ChatSessions.CountAsync(x => x.CreatedAt >= todayStart && x.CreatedAt < todayEnd, cancellationToken),
            TotalMessages = await dbContext.ChatMessages.CountAsync(cancellationToken),
            ErrorsToday = await GetSystemLogCountAsync(todayStart, todayEnd, "error", cancellationToken),
            RecentAccounts = (await GetRecentAccountsAsync(cancellationToken)).Take(5).ToList(),
            RecentSessions = (await conversationService.GetSessionsAsync(null, null, null, 1, 5, cancellationToken)).Sessions,
            RecentErrors = (await GetSystemLogsAsync(todayStart, todayEnd, "error", cancellationToken)).Take(5).ToList()
        };
    }

    public async Task<TranslationServicesViewModel> GetTranslationServicesAsync(DateTime? fromDate, DateTime? toDate, CancellationToken cancellationToken = default)
    {
        var from = fromDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc))
            : new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-30), TimeSpan.Zero);
        var to = toDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc))
            : new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero);
        var totalSessions = await dbContext.ChatSessions.CountAsync(x => x.CreatedAt >= from && x.CreatedAt <= to, cancellationToken);
        var totalMessages = await dbContext.ChatMessages.CountAsync(x => x.CreatedAt >= from && x.CreatedAt <= to, cancellationToken);

        return new TranslationServicesViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalSessions = totalSessions,
            TotalMessages = totalMessages,
            TotalGestureLibraryItems = await CountRowsAsync("public", "gesture_library", cancellationToken),
            TotalArModels = await CountRowsAsync("public", "ar_models", cancellationToken),
            TotalRecognitionRecords = await CountRowsAsync("public", "gesture_recognition_data", cancellationToken),
            TotalErrorLogs = await GetSystemLogCountAsync(from, to, null, cancellationToken),
            AverageMessagesPerSession = totalSessions == 0 ? 0 : Math.Round((decimal)totalMessages / totalSessions, 2),
            Logs = await GetSystemLogsAsync(from, to, null, cancellationToken)
        };
    }

    public async Task<ReportsViewModel> GetReportsAsync(DateTime? fromDate, DateTime? toDate, string? search, CancellationToken cancellationToken = default)
    {
        var from = fromDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc))
            : new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-30), TimeSpan.Zero);
        var to = toDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc))
            : new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero);

        return new ReportsViewModel
        {
            FromDate = fromDate,
            ToDate = toDate,
            Search = search,
            TotalAccounts = await dbContext.Profiles.CountAsync(cancellationToken),
            ActiveAccounts = await dbContext.Profiles.CountAsync(x => x.IsActive, cancellationToken),
            TotalSessions = await dbContext.ChatSessions.CountAsync(x => x.CreatedAt >= from && x.CreatedAt <= to, cancellationToken),
            TotalMessages = await dbContext.ChatMessages.CountAsync(x => x.CreatedAt >= from && x.CreatedAt <= to, cancellationToken),
            DailyUsage = await GetDailyUsageAsync(from, to, cancellationToken),
            TopUsers = await GetTopUsersAsync(from, to, search, cancellationToken),
            MessageTypes = await GetMessageTypeBreakdownAsync(from, to, cancellationToken)
        };
    }

    private async Task<List<AccountRowViewModel>> GetRecentAccountsAsync(CancellationToken cancellationToken)
    {
        return await (
            from profile in dbContext.Profiles.AsNoTracking()
            join authUser in dbContext.AuthUsers.AsNoTracking() on profile.Id equals authUser.Id into authGroup
            from authUser in authGroup.DefaultIfEmpty()
            orderby profile.CreatedAt descending
            select new AccountRowViewModel
            {
                Id = profile.Id,
                FullName = ((profile.FirstName ?? string.Empty) + " " + (profile.LastName ?? string.Empty)).Trim(),
                Email = authUser != null && !string.IsNullOrWhiteSpace(authUser.Email) ? authUser.Email! : "No email",
                UserType = profile.UserType ?? string.Empty,
                IsActive = profile.IsActive,
                CreatedAt = profile.CreatedAt,
                LastSignInAt = null
            }).Take(5).ToListAsync(cancellationToken);
    }

    private async Task<List<DailyUsageRowViewModel>> GetDailyUsageAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var sessions = await dbContext.ChatSessions
            .Where(x => x.CreatedAt >= from && x.CreatedAt <= to)
            .GroupBy(x => DateOnly.FromDateTime(x.CreatedAt!.Value.UtcDateTime))
            .Select(x => new { Day = x.Key, Sessions = x.Count() })
            .ToListAsync(cancellationToken);

        var messages = await dbContext.ChatMessages
            .Where(x => x.CreatedAt >= from && x.CreatedAt <= to)
            .GroupBy(x => DateOnly.FromDateTime(x.CreatedAt!.Value.UtcDateTime))
            .Select(x => new { Day = x.Key, Messages = x.Count() })
            .ToListAsync(cancellationToken);

        return sessions
            .GroupJoin(messages, x => x.Day, x => x.Day, (session, messageGroup) => new DailyUsageRowViewModel
            {
                Day = session.Day,
                Sessions = session.Sessions,
                Messages = messageGroup.FirstOrDefault()?.Messages ?? 0
            })
            .OrderByDescending(x => x.Day)
            .ToList();
    }

    private async Task<List<TopUserRowViewModel>> GetTopUsersAsync(DateTimeOffset from, DateTimeOffset to, string? search, CancellationToken cancellationToken)
    {
        var query = dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.CreatedAt >= from && message.CreatedAt <= to)
            .Join(
                dbContext.Profiles.AsNoTracking(),
                message => message.SenderId,
                profile => profile.Id,
                (message, profile) => new { profile })
            .GroupJoin(
                dbContext.AuthUsers.AsNoTracking(),
                row => row.profile.Id,
                authUser => authUser.Id,
                (row, authUsers) => new { row.profile, authUser = authUsers.FirstOrDefault() })
            .GroupBy(row => new
            {
                row.profile.FirstName,
                row.profile.LastName,
                Email = row.authUser != null ? row.authUser.Email : null
            })
            .Select(grouped => new TopUserRowViewModel
            {
                UserName = (grouped.Key.FirstName + " " + grouped.Key.LastName).Trim(),
                Email = grouped.Key.Email ?? "No email",
                MessageCount = grouped.Count()
            });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.UserName.ToLower().Contains(term) || x.Email.ToLower().Contains(term));
        }

        return await query.OrderByDescending(x => x.MessageCount).Take(10).ToListAsync(cancellationToken);
    }

    private async Task<List<MessageTypeRowViewModel>> GetMessageTypeBreakdownAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        return await dbContext.ChatMessages
            .AsNoTracking()
            .Where(x => x.CreatedAt >= from && x.CreatedAt <= to)
            .GroupBy(x => x.GestureId.HasValue ? "Gesture" : "Text")
            .Select(x => new MessageTypeRowViewModel
            {
                MessageType = x.Key,
                Total = x.Count()
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync(cancellationToken);
    }

    private async Task<int> CountRowsAsync(string schema, string table, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand($@"select count(*) from ""{schema}"".""{table}"";", connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private async Task<int> GetSystemLogCountAsync(DateTimeOffset from, DateTimeOffset to, string? levelFilter, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var columns = await GetTableColumnsAsync(connection, "public", "system_logs", cancellationToken);
        var timestampColumn = FirstAvailable(columns, "created_at", "logged_at", "timestamp");
        var levelColumn = FirstAvailable(columns, "level", "log_level", "severity");

        if (string.IsNullOrWhiteSpace(timestampColumn))
        {
            return 0;
        }

        var sql = $@"select count(*) from public.system_logs where {timestampColumn} between @from and @to";
        if (!string.IsNullOrWhiteSpace(levelFilter) && !string.IsNullOrWhiteSpace(levelColumn))
        {
            sql += $" and lower({levelColumn}::text) = @level";
        }

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("from", from);
        command.Parameters.AddWithValue("to", to);
        if (!string.IsNullOrWhiteSpace(levelFilter) && !string.IsNullOrWhiteSpace(levelColumn))
        {
            command.Parameters.AddWithValue("level", levelFilter);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    private async Task<IReadOnlyList<SystemLogItemViewModel>> GetSystemLogsAsync(DateTimeOffset from, DateTimeOffset to, string? levelFilter, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var columns = await GetTableColumnsAsync(connection, "public", "system_logs", cancellationToken);
        var idColumn = FirstAvailable(columns, "id");
        var timestampColumn = FirstAvailable(columns, "created_at", "logged_at", "timestamp");
        var levelColumn = FirstAvailable(columns, "level", "log_level", "severity");
        var messageColumn = FirstAvailable(columns, "message", "details", "description");
        var sourceColumn = FirstAvailable(columns, "source", "module", "category");
        var profileColumn = FirstAvailable(columns, "profile_id", "user_id", "created_by");

        if (string.IsNullOrWhiteSpace(timestampColumn))
        {
            return [];
        }

        var sql = $@"
            select
                {(idColumn is not null ? $"{idColumn}::text" : "null")} as id,
                {(levelColumn is not null ? $"{levelColumn}::text" : "'INFO'")} as level,
                {(messageColumn is not null ? $"{messageColumn}::text" : "'No message column found'")} as message,
                {(sourceColumn is not null ? $"{sourceColumn}::text" : "'System'")} as source,
                {timestampColumn} as created_at,
                {(profileColumn is not null ? $"{profileColumn}::text" : "null")} as profile_id
            from public.system_logs
            where {timestampColumn} between @from and @to";

        if (!string.IsNullOrWhiteSpace(levelFilter) && !string.IsNullOrWhiteSpace(levelColumn))
        {
            sql += $" and lower({levelColumn}::text) = @level";
        }

        sql += $" order by {timestampColumn} desc limit 25";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("from", from);
        command.Parameters.AddWithValue("to", to);
        if (!string.IsNullOrWhiteSpace(levelFilter) && !string.IsNullOrWhiteSpace(levelColumn))
        {
            command.Parameters.AddWithValue("level", levelFilter);
        }

        var results = new List<SystemLogItemViewModel>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var profileIdText = reader["profile_id"]?.ToString();
            var userName = "System";

            if (Guid.TryParse(profileIdText, out var profileId))
            {
                var profile = await dbContext.Profiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == profileId, cancellationToken);
                if (profile is not null)
                {
                    userName = $"{profile.FirstName ?? string.Empty} {profile.LastName ?? string.Empty}".Trim();
                }
            }

            results.Add(new SystemLogItemViewModel
            {
                Id = reader["id"]?.ToString() ?? string.Empty,
                Level = reader["level"]?.ToString() ?? "INFO",
                Message = reader["message"]?.ToString() ?? string.Empty,
                Source = reader["source"]?.ToString() ?? "System",
                UserName = userName,
                CreatedAt = reader["created_at"] is DateTimeOffset dto
                    ? dto
                    : DateTimeOffset.TryParse(reader["created_at"]?.ToString(), out var parsed) ? parsed : null
            });
        }

        return results;
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(NpgsqlConnection connection, string schema, string table, CancellationToken cancellationToken)
    {
        const string sql = """
            select column_name
            from information_schema.columns
            where table_schema = @schema and table_name = @table;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("table", table);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static string? FirstAvailable(HashSet<string> columns, params string[] names) =>
        names.FirstOrDefault(columns.Contains);
}
