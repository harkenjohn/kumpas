namespace Kumpas.AdminWeb.ViewModels;

public class DashboardViewModel
{
    public int TotalAccounts { get; set; }
    public int ActiveAccounts { get; set; }
    public int TotalSessions { get; set; }
    public int SessionsToday { get; set; }
    public int TotalMessages { get; set; }
    public int ErrorsToday { get; set; }
    public IReadOnlyList<AccountRowViewModel> RecentAccounts { get; set; } = [];
    public IReadOnlyList<ConversationSessionRowViewModel> RecentSessions { get; set; } = [];
    public IReadOnlyList<SystemLogItemViewModel> RecentErrors { get; set; } = [];
}
