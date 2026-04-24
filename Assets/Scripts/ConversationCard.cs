using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Kumpas.Models;
using System;

/*
 * CONVERSATION CARD (VIEW)
 * * WHAT IT DOES:
 * - Displays session data (Date, Partner Name).
 * - Handles Click/Edit/Delete actions for this specific session.
 */
public class ConversationCard : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text dateText;
    public TMP_Text partnerNameText;
    public Button editButton;
    public Button deleteButton;
    public Button cardButton; // The main button covering the whole card

    // Private state for the session this card represents
    private ChatSession session;
    private UIManager uiManager;
    private AppManager appManager;
    private string myUserId;

    // Call this immediately after instantiation to set the data
    public void Initialize(ChatSession chatSession, UIManager ui, AppManager am, string partnerName, string userId, DateTime? latestMessageDate = null)
    {
        session = chatSession;
        uiManager = ui;
        appManager = am;
        myUserId = userId;

        // Use the latest message date if available, otherwise fall back to session creation date
        DateTime? displayDate = latestMessageDate ?? session.CreatedAt;

        if (displayDate.HasValue)
        {
            dateText.text = displayDate.Value.ToLocalTime().ToString("MMM dd, yyyy");
        }
        else
        {
            dateText.text = "Just Now";
        }

        partnerNameText.text = partnerName;

        // Set up listeners (The cardButton is the main click action)
        // RemoveAllListeners prevents stacking events if the card is pooled/reused
        cardButton.onClick.RemoveAllListeners();
        cardButton.onClick.AddListener(OnCardClicked);

        editButton.onClick.RemoveAllListeners();
        editButton.onClick.AddListener(OnEditClicked);

        deleteButton.onClick.RemoveAllListeners();
        deleteButton.onClick.AddListener(OnDeleteClicked);
    }

    private void OnCardClicked()
    {
        Debug.Log($"Card clicked! Opening Session ID: {session.Id}");

        // Tell AppManager to load this session for viewing
        appManager.ViewChatHistory(session);
    }

    private void OnEditClicked()
    {
        Debug.Log($"Edit clicked for Session ID: {session.Id}");
        // Optional: Implement simple edit functionality or just review.
    }

    private void OnDeleteClicked()
    {
        Debug.Log($"Delete clicked for Session ID: {session.Id}");
        // Calls the AppManager function to delete the record in Supabase
        appManager.DeleteChatSession(session);
    }
}