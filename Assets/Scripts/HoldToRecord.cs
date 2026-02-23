using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoldToRecord : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public Image buttonImage;
    public VoiceManager voiceManager;

    private Color idleColor = Color.red;
    private Color recordingColor = Color.green;

    public void OnPointerDown(PointerEventData eventData)
    {
        buttonImage.color = recordingColor;
        voiceManager.StartRecording();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        buttonImage.color = idleColor;
        voiceManager.StopRecording();
    }
}