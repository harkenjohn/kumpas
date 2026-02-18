using UnityEngine;
using System.Linq;
using System.Threading.Tasks;
using Kumpas.Models;
using System.Collections.Generic; // For List<T>
using Postgrest.Responses;
using System;

/*
 * CHAT MANAGER (MODEL/SERVICE)
 * * WHAT IT DOES:
 * - Handles persistence operations for ChatSessions and ChatMessages.
 */
public class ChatManager : MonoBehaviour
{
    // Supabase.Client Instance is automatically available via SupabaseManager.Instance

    // Reference to AppManager is needed to call back LoadChatHistory on delete
    public AppManager appManager;

    // --- FETCH CHAT SESSIONS ---

    // Fetches all chat sessions where the given userId is User1 OR User2
    public async Task<List<ChatSession>> GetUserSessions(string userId)
    {
        Debug.Log($"[ChatManager] Fetching sessions for user: {userId}");

        try
        {
            // 1. Query sessions where I am User1
            var sessions1 = SupabaseManager.Instance
                .From<ChatSession>()
                .Where(s => s.User1Id == userId);

            // 2. Query sessions where I am User2
            var sessions2 = SupabaseManager.Instance
                .From<ChatSession>()
                .Where(s => s.User2Id == userId);

            // Execute the queries
            var response1 = await sessions1.Get();
            var response2 = await sessions2.Get();

            List<ChatSession> allSessions = new List<ChatSession>();

            if (response1.Models != null)
            {
                allSessions.AddRange(response1.Models);
            }
            if (response2.Models != null)
            {
                // Combine the two lists, ensuring no duplicates
                foreach (var session in response2.Models)
                {
                    if (!allSessions.Any(s => s.Id == session.Id))
                    {
                        allSessions.Add(session);
                    }
                }
            }

            // Order by most recent first
            return allSessions.OrderByDescending(s => s.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatManager] Error fetching sessions: {ex.Message}");
            return new List<ChatSession>();
        }
    }

    // --- FETCH USER PROFILES ---

    // Fetches a specific user profile
    public async Task<Profile> GetUserProfile(string userId)
    {
        try
        {
            var profileResponse = await SupabaseManager.Instance
                .From<Profile>()
                .Where(p => p.Id == userId)
                .Single();

            return profileResponse;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatManager] Error fetching profile {userId}: {ex.Message}");
            return null;
        }
    }

    // --- NEW: FETCH CHAT MESSAGES FOR A SESSION ---

    // Fetches all messages for a given session ID, ordered by timestamp.
    public async Task<List<ChatMessage>> GetChatMessages(string sessionId)
    {
        Debug.Log($"[ChatManager] Fetching messages for session: {sessionId}");

        try
        {
            var response = await SupabaseManager.Instance
                .From<ChatMessage>()
                .Where(m => m.SessionId == sessionId)
                // IMPORTANT: Sort by created_at to display messages in order
                .Order(m => m.CreatedAt, Postgrest.Constants.Ordering.Ascending)
                .Get();

            if (response.Models == null)
            {
                return new List<ChatMessage>();
            }

            return response.Models.ToList();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatManager] Error fetching messages for session {sessionId}: {ex.Message}");
            return new List<ChatMessage>();
        }
    }

    // --- CHAT MESSAGE INSERT ---

    // Inserts a new message into the database
    // messageType options:
    // - "TEXT_TO_SIGN": Speech user typed text to trigger Sign user's camera
    // - "TEXT_TO_SPEECH": Sign user typed text to trigger Speech user's TTS
    // - "SIGN_VIDEO": Sign user sent sign language video (future use)
    // - "SPEECH_AUDIO": Speech user sent audio (future use)
    public async void InsertMessage(string sessionId, string senderId, string content, string messageType)
    {
        Debug.Log($"[ChatManager] Inserting message - Content: '{content}', Type: '{messageType}', Session: {sessionId}");

        try
        {
            var newMessage = new ChatMessage
            {
                SessionId = sessionId,
                SenderId = senderId,
                MessageContent = content,
                MessageType = messageType,
                CreatedAt = DateTime.UtcNow
            };

            // Insert the message into the 'chat_messages' table
            var response = await SupabaseManager.Instance.From<ChatMessage>().Insert(newMessage);

            ChatMessage insertedMessage = response.Models.FirstOrDefault();

            if (insertedMessage != null)
            {
                Debug.Log($"[ChatManager] Message inserted successfully with ID: {insertedMessage.Id}");
            }
            else
            {
                Debug.LogError("[ChatManager] Database insert failed. Response was null.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ChatManager] Error inserting message: {ex.Message}");
        }
    }
}