namespace Kumpas.AdminWeb.ViewModels;

public class ReportsViewModel
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Search { get; set; }
    public int TotalAccounts { get; set; }
    public int ActiveAccounts { get; set; }
    public int InactiveAccounts { get; set; }
    public int TotalSessions { get; set; }
    public int TotalMessages { get; set; }
    public int TotalArModels { get; set; }
    public decimal ActiveAccountPercent { get; set; }
    public decimal? SessionChangePercent { get; set; }
    public decimal? MessageChangePercent { get; set; }
    public string ModelStatus { get; set; } = "Operational";
    public string ModelProvider { get; set; } = "Not configured";
    public string? ModelUrl { get; set; }
    public DateOnly UptimeReportDate { get; set; }
    public decimal ModelUptimePercent { get; set; }
    public int ErrorLogsThisYear { get; set; }
    public int ErrorLogsThisMonth { get; set; }
    public int ErrorLogsToday { get; set; }
    public string GeneratedBy { get; set; } = "Administrator";
    public DateTimeOffset GeneratedAt { get; set; }
    public IReadOnlyList<DailyUsageRowViewModel> DailyUsage { get; set; } = [];
    public IReadOnlyList<ModelUptimeHourViewModel> ModelUptimeHours { get; set; } = [];
    public IReadOnlyList<TopUserRowViewModel> TopUsers { get; set; } = [];
    public IReadOnlyList<MessageTypeRowViewModel> MessageTypes { get; set; } = [];
    public PaginationViewModel TopUsersPagination { get; set; } = new();
}

public class DailyUsageRowViewModel
{
    public DateOnly Day { get; set; }
    public int Sessions { get; set; }
    public int Messages { get; set; }
}

public class TopUserRowViewModel
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = "No email";
    public int MessageCount { get; set; }
}

public class MessageTypeRowViewModel
{
    public string MessageType { get; set; } = string.Empty;
    public int Total { get; set; }
}

public class ModelUptimeHourViewModel
{
    public int Hour { get; set; }
    public int TotalChecks { get; set; }
    public int UpChecks { get; set; }

    public string Label => DateTime.Today.AddHours(Hour).ToString("h:mm tt");
    public bool HasData => TotalChecks > 0;
    public bool IsUp => HasData && UpChecks == TotalChecks;
    public decimal UptimePercent => HasData ? Math.Round((decimal)UpChecks / TotalChecks * 100, 1) : 0;
    public string DisplayStatus => !HasData ? "No Data" : IsUp ? "OK" : "Issue";
}
