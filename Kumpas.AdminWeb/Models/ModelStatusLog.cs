namespace Kumpas.AdminWeb.Models;

public class ModelStatusLog
{
    public long Id { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? RecordedAt { get; set; }
}
