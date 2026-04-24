using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Kumpas.Models;
using System;

/*
 * CONVERSATION CARD (VIEW)
 * * WHAT IT DOES:
 * - Displays session data (Date, Partner Name / Nickname).
 * - Edit button opens the nickname modal via UIManager.
 */
public class ConversationCard : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text dateText;
    public TMP_Text partnerNameText;
    public Button editButton;
    public Button deleteButton;
    public Button cardButton;

    // Private state
    private ChatSession session;
    private UIManager uiManager;
    private AppManager appManager;
    private string myUserId;
    private string currentDisplayName;

    public void Initialize(ChatSession chatSession, UIManager ui, AppManager am, string partnerName, string userId, DateTime? latestMessageDate = null)
    {
        session = chatSession;
        uiManager = ui;
        appManager = am;
        myUserId = userId;
        currentDisplayName = partnerName;

        // Date display
        DateTime? displayDate = latestMessageDate ?? session.CreatedAt;
        dateText.text = displayDate.HasValue
            ? displayDate.Value.ToLocalTime().ToString("MMM dd, yyyy")
            : "Just Now";

        partnerNameText.text = currentDisplayName;

        // Wire buttons
        cardButton.onClick.RemoveAllListeners();
        cardButton.onClick.AddListener(OnCardClicked);

        editButton.onClick.RemoveAllListeners();
        editButton.onClick.AddListener(OnEditClicked);

        deleteButton.onClick.RemoveAllListeners();
        deleteButton.onClick.AddListener(OnDeleteClicked);
    }

    // Called by UIManager after the user confirms a nickname in the modal
    public void ApplyNickname(string newNickname)
    {
        if (string.IsNullOrWhiteSpace(newNickname)) return;

        currentDisplayName = newNickname;
        partnerNameText.text = newNickname;
        appManager.SaveNickname(session, newNickname);
        Debug.Log($"[ConversationCard] Nickname applied: '{newNickname}'");
    }

    private void OnEditClicked()
    {
        Debug.Log($"[ConversationCard] Edit clicked for session {session.Id}");
        uiManager.OpenNicknameModal(this, currentDisplayName);
    }

    private void OnCardClicked()
    {
        Debug.Log($"[ConversationCard] Card clicked. Opening session {session.Id}");
        appManager.ViewChatHistory(session);
    }

    private void OnDeleteClicked()
    {
        Debug.Log($"[ConversationCard] Delete clicked for session {session.Id}");
        appManager.DeleteChatSession(session);
    }
}