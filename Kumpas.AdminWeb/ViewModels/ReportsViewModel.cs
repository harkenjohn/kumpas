namespace Kumpas.AdminWeb.ViewModels;

public class ReportsViewModel
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Search { get; set; }
    public int TotalAccounts { get; set; }
    public int ActiveAccounts { get; set; }
    public int TotalSessions { get; set; }
    public int TotalMessages { get; set; }
    public IReadOnlyList<DailyUsageRowViewModel> DailyUsage { get; set; } = [];
    public IReadOnlyList<TopUserRowViewModel> TopUsers { get; set; } = [];
    public IReadOnlyList<MessageTypeRowViewModel> MessageTypes { get; set; } = [];
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
