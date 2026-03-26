namespace Kumpas.AdminWeb.Models;

public class ChatSession
{
    public Guid Id { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public Guid User1Id { get; set; }
    public Guid User2Id { get; set; }
    public string? RoomCode { get; set; }
    public bool User1Deleted { get; set; }
    public bool User2Deleted { get; set; }

    public Profile? User1 { get; set; }
    public Profile? User2 { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
