namespace Kumpas.AdminWeb.Models;

public class SystemLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string? LogLevel { get; set; }
    public string? Module { get; set; }
    public string? Message { get; set; }
    public string? ErrorStack { get; set; }
    public DateTimeOffset? Timestamp { get; set; }

    public Profile? User { get; set; }
}
