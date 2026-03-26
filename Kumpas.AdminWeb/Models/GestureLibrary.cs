namespace Kumpas.AdminWeb.Models;

public class GestureLibrary
{
    public long GestureId { get; set; }
    public string? GestureName { get; set; }
    public string? GestureType { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }

    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    public ICollection<ArModel> ArModels { get; set; } = new List<ArModel>();
    public ICollection<GestureRecognitionData> RecognitionData { get; set; } = new List<GestureRecognitionData>();
}
