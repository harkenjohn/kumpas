namespace Kumpas.AdminWeb.Models;

public class ArModel
{
    public long ModelId { get; set; }
    public long GestureId { get; set; }
    public string? ModelFilePath { get; set; }
    public string? AnimationFilePath { get; set; }

    public GestureLibrary? Gesture { get; set; }
}
