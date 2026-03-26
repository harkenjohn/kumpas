namespace Kumpas.AdminWeb.Models;

public class ChatMessage
{
    public long Id { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public Guid SessionId { get; set; }
    public Guid SenderId { get; set; }
    public string? MessageContent { get; set; }
    public long? GestureId { get; set; }

    public ChatSession? ChatSession { get; set; }
    public Profile? Sender { get; set; }
    public GestureLibrary? Gesture { get; set; }
}
