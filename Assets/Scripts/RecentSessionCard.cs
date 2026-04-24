using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RecentSessionCard : MonoBehaviour
{
    public TMP_Text partnerNameText;
    public TMP_Text sessionCodeText;
    public Button joinButton;

    private string _roomCode;
    private System.Action<string> _onJoin;

    public void Setup(string partnerName, string roomCode, System.Action<string> onJoin)
    {
        _roomCode = roomCode;
        _onJoin = onJoin;

        partnerNameText.text = string.IsNullOrEmpty(partnerName) 
            ? "No partner yet" 
            : partnerName;
        sessionCodeText.text = "Code: " + roomCode;

        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(() => _onJoin?.Invoke(_roomCode));
    }
}