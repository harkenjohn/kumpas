namespace Kumpas.AdminWeb.Models;

public class Profile
{
    public Guid Id { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? UserType { get; set; }
    public bool IsActive { get; set; }

    public AuthUser? AuthUser { get; set; }

    public ICollection<ChatSession> ChatSessionsAsUser1 { get; set; } = new List<ChatSession>();
    public ICollection<ChatSession> ChatSessionsAsUser2 { get; set; } = new List<ChatSession>();
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    public ICollection<SystemLog> SystemLogs { get; set; } = new List<SystemLog>();
}
