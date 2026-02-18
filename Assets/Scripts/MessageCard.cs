using UnityEngine;
using TMPro;
using Kumpas.Models;

/*
 * MESSAGE CARD (VIEW)
 * * WHAT IT DOES:
 * - Displays the Sender's Name and the Message Content within a full-width card.
 */
public class MessageCard : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text senderNameText;
    public TMP_Text messageContentText;

    // Call this immediately after instantiation to set the data
    public void Initialize(ChatMessage message, string senderName)
    {
        senderNameText.text = senderName;
        messageContentText.text = message.MessageContent;
    }
}