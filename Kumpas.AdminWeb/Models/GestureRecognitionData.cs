namespace Kumpas.AdminWeb.Models;

public class GestureRecognitionData
{
    public long DataId { get; set; }
    public long GestureId { get; set; }
    public string? ImagePath { get; set; }
    public string? VideoPath { get; set; }
    public string? KeypointData { get; set; }

    public GestureLibrary? Gesture { get; set; }
}
