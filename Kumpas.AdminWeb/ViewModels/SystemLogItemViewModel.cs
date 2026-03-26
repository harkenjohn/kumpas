namespace Kumpas.AdminWeb.ViewModels;

public class SystemLogItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = "System";
    public string UserName { get; set; } = "System";
    public DateTimeOffset? CreatedAt { get; set; }
}
