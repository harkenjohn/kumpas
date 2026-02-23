using UnityEngine;
using Supabase.Gotrue;
using System.Threading.Tasks;
using Kumpas.Models;
using System.Collections.Generic;
using Postgrest.Responses;
using System.Linq;
using System;
// Fixes Namespace collision
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;

public class AppManager : MonoBehaviour
{
    // --- Public References ---
    [Header("Manager References")]
    public UIManager uiManager;
    public ChatManager chatManager;

    // --- Private State ---
    private Session currentSession;
    private Profile currentUserProfile;
    private ChatSession currentChatSession;
    private ChatSession currentHistorySession; // To hold the session being reviewed
    private string currentPartnerDisplayName = "Partner"; // Stores the partner's name for UI use

    // --- Realtime Trigger Flags ---
    private bool triggerOpenCamera = false;
    private bool isCameraFullscreen = false; // Tracks if camera is currently fullscreen
    private string receivedTextToSign = "";

    // FIX: Use the specific class 'RealtimeChannel' because 'Supabase.Realtime.Channel' is a namespace
    private Supabase.Realtime.RealtimeChannel realtimeChannel;

    // --- UPDATED AppState with all necessary states ---
    public enum AppState
    {
        Login,
        Register,
        Home,
        Profile,
        SignSession,
        SpeechSession,
        CameraInput,
        AudioInput,
        TextToSpeechInput,
        TextToSignInput,
        VoiceInput,
        History,         // The list of past sessions
        ConversationView   // The panel showing messages inside a chat
    }
    private AppState currentState;

    void Start()
    {
        if (uiManager != null) uiManager.Initialize(this);

        // --- NEW TTS INITIALIZATION ---
        // This MUST be called on startup for native Android TTS to work.
        //ChatManager.InitializeNativeTTS();
        // ------------------------------

        ChangeState(AppState.Login);
    }

    void Update()
    {
        if (triggerOpenCamera)
        {
            triggerOpenCamera = false;
            isCameraFullscreen = true;
            Debug.Log($"[AppManager] UPDATE: triggerOpenCamera caught! Opening camera. Text: {receivedTextToSign}");
            if (uiManager != null) uiManager.ShowCameraFullscreen();
        }

        // Native Android back button support
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleBackButton();
        }
    }

    private void HandleBackButton()
    {
        Debug.Log($"[AppManager] Back button pressed. CurrentState: {currentState}, isCameraFullscreen: {isCameraFullscreen}");

        // If camera is fullscreen, always close it first regardless of state
        if (isCameraFullscreen)
        {
            isCameraFullscreen = false;
            if (uiManager != null)
            {
                uiManager.HideCameraFullscreen();
                uiManager.ShowCameraInputMethodPanel();
            }
            Debug.Log("[AppManager] Closed fullscreen camera, returned to CameraInput panel");
            return;
        }

        switch (currentState)
        {
            case AppState.CameraInput:
                ChangeState(AppState.SignSession);
                break;
            case AppState.AudioInput:
                ChangeState(AppState.SpeechSession);
                break;
            case AppState.TextToSpeechInput:
                ChangeState(AppState.CameraInput);
                break;
            case AppState.TextToSignInput:
                ChangeState(AppState.AudioInput);
                break;
            case AppState.SignSession:
                ChangeState(AppState.Home);
                break;
            case AppState.SpeechSession:
                ChangeState(AppState.Home);
                break;
            case AppState.Profile:
                ChangeState(AppState.Home);
                break;
            case AppState.Register:
                ChangeState(AppState.Login);
                break;
            case AppState.History:
                ChangeState(AppState.Home);
                break;
            case AppState.ConversationView:
                ChangeState(AppState.History);
                break;
            case AppState.VoiceInput:
                ChangeState(AppState.AudioInput);
                break;
            case AppState.Home:
            case AppState.Login:
                Debug.Log("[AppManager] Already at root panel, back button ignored.");
                break;
        }
    }

    // --- State Management ---
    public void ChangeState(AppState newState)
    {
        Debug.Log($"[AppManager] ChangeState called: {currentState} -> {newState}");
        currentState = newState;

        // Update UI based on the new state
        switch (currentState)
        {
            case AppState.Login:
                if (uiManager != null) uiManager.ShowLoginPanel();
                break;
            case AppState.Register:
                if (uiManager != null) uiManager.ShowRegisterPanel();
                break;
            case AppState.Home:
                if (uiManager != null) uiManager.ShowHomePanel();
                break;
            case AppState.Profile:
                if (uiManager != null) uiManager.ShowProfilePanel();
                break;
            case AppState.SignSession:
                if (uiManager != null) uiManager.ShowSignSessionPanel();
                break;
            case AppState.SpeechSession:
                if (uiManager != null) uiManager.ShowSpeechSessionPanel();
                break;
            case AppState.CameraInput:
                if (uiManager != null) uiManager.ShowCameraInputMethodPanel();
                break;
            case AppState.AudioInput:
                if (uiManager != null) uiManager.ShowAudioInputMethodPanel();
                break;
            case AppState.TextToSpeechInput:
                if (uiManager != null) uiManager.ShowTextToSpeechInputPanel();
                break;
            case AppState.TextToSignInput:
                if (uiManager != null) uiManager.ShowTextToSignInputPanel();
                break;
            case AppState.History:
                if (uiManager != null) uiManager.ShowHistoryPanel();
                break;
            case AppState.VoiceInput:
                if (uiManager != null) uiManager.ShowVoiceInputPanel();
                break;
            case AppState.ConversationView:
                if (uiManager != null) uiManager.ShowConversationViewPanel();
                break;
        }
    }

    // -------------------------------------------------------------------------
    // --- REALTIME SUBSCRIPTION LOGIC -----------------------------------------
    // -------------------------------------------------------------------------

    private async void SubscribeToSessionMessages(string sessionId)
    {
        // Unsubscribe from any previous channels just in case
        if (realtimeChannel != null)
        {
            try
            {
                realtimeChannel.Unsubscribe();
                Debug.Log("[Realtime] Unsubscribed from previous channel");
            }
            catch { /* Ignore if already closed */ }
        }

        try
        {
            // FIXED: Use the correct wildcard channel format for Supabase Realtime
            realtimeChannel = SupabaseManager.Instance.Realtime.Channel("realtime", "public", "chat_messages");

            realtimeChannel.AddPostgresChangeHandler(
                PostgresChangesOptions.ListenType.Inserts,
                (sender, change) =>
                {
                    try
                    {
                        Debug.Log("[Realtime] *** EVENT RECEIVED ***");

                        var message = change.Model<ChatMessage>();

                        if (message == null)
                        {
                            Debug.LogError("[Realtime] Message is NULL after parsing!");
                            return;
                        }

                        Debug.Log($"[Realtime] Parsed - SessionId: {message.SessionId}, SenderId: {message.SenderId}, Type: '{message.MessageType}', Content: '{message.MessageContent}'");

                        if (currentChatSession == null)
                        {
                            Debug.LogError("[Realtime] currentChatSession is NULL!");
                            return;
                        }

                        if (currentUserProfile == null)
                        {
                            Debug.LogError("[Realtime] currentUserProfile is NULL!");
                            return;
                        }

                        Debug.Log($"[Realtime] Expected SessionId: {currentChatSession.Id}, MyId: {currentUserProfile.Id}");

                        bool correctSession = message.SessionId == currentChatSession.Id;
                        bool notFromMe = message.SenderId != currentUserProfile.Id;

                        Debug.Log($"[Realtime] correctSession: {correctSession}, notFromMe: {notFromMe}");

                        if (correctSession && notFromMe)
                        {
                            Debug.Log($"[Realtime] Message is valid! Type: '{message.MessageType}'");

                            if (message.MessageType == "TEXT_TO_SIGN")
                            {
                                receivedTextToSign = message.MessageContent;
                                triggerOpenCamera = true;
                                Debug.Log("[Realtime] *** CAMERA TRIGGER SET! ***");
                            }
                            else
                            {
                                Debug.Log($"[Realtime] Message type '{message.MessageType}' does not trigger camera.");
                            }
                        }
                        else
                        {
                            Debug.Log($"[Realtime] Message ignored - correctSession: {correctSession}, notFromMe: {notFromMe}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Realtime] Parse error: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            );

            await realtimeChannel.Subscribe();
            Debug.Log($"[Realtime] Successfully subscribed to chat_messages for session: {sessionId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Realtime] SUBSCRIPTION FAILED: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // --- UTILITY GETTER ------------------------------------------------
    // -------------------------------------------------------------------------

    // Allows UIManager to get the partner's display name without complex logic
    public string GetCurrentPartnerName()
    {
        return currentPartnerDisplayName;
    }

    // -------------------------------------------------------------------------
    // --- HISTORY / CONVERSATION VIEW FUNCTIONS -------------------------------
    // -------------------------------------------------------------------------

    // 1. Triggered by the "History" button. Loads the list of sessions.
    public async void LoadChatHistory()
    {
        if (currentUserProfile == null || chatManager == null || uiManager == null) return;

        uiManager.ClearHistoryCards();
        uiManager.SetHistoryStatus("Loading conversations...");

        ChangeState(AppState.History);

        string myUserId = currentUserProfile.Id;
        List<ChatSession> sessions = await chatManager.GetUserSessions(myUserId);

        if (sessions == null || sessions.Count == 0)
        {
            uiManager.SetHistoryStatus("No past conversations found.");
            return;
        }

        uiManager.SetHistoryStatus("");

        int renderedCount = 0;

        foreach (var session in sessions)
        {
            // 1. FILTER: Must have User 2 assigned (chat started)
            if (string.IsNullOrEmpty(session.User2Id))
            {
                continue;
            }

            // 2. SOFT DELETE FILTER: Hide if current user has deleted it.
            bool isUser1 = session.User1Id == myUserId;
            if ((isUser1 && session.User1Deleted) || (!isUser1 && session.User2Deleted))
            {
                continue;
            }

            string partnerId = isUser1 ? session.User2Id : session.User1Id;
            string partnerName = "";

            if (!string.IsNullOrEmpty(partnerId))
            {
                // Fetch the partner's profile to get their name
                Profile partnerProfile = await chatManager.GetUserProfile(partnerId);
                if (partnerProfile != null)
                {
                    partnerName = $"{partnerProfile.FirstName} {partnerProfile.LastName}";
                }
            }

            uiManager.CreateConversationCard(session, partnerName, myUserId);
            renderedCount++;
        }

        if (renderedCount == 0)
        {
            uiManager.SetHistoryStatus("No completed conversations found.");
        }
    }

    // 2. Triggered by clicking a ConversationCard. Loads the messages.
    public async void ViewChatHistory(ChatSession session)
    {
        if (currentUserProfile == null || chatManager == null || uiManager == null) return;

        // Set the session data globally for the conversation view panel to use
        currentHistorySession = session;

        // Load the messages and transition the state
        await LoadMessagesForSession(session);

        ChangeState(AppState.ConversationView);
    }

    // Handles fetching messages, populating the UI, and setting the header
    public async Task LoadMessagesForSession(ChatSession session)
    {
        if (currentUserProfile == null || chatManager == null || uiManager == null) return;

        string myUserId = currentUserProfile.Id;
        string partnerId = session.User1Id == myUserId ? session.User2Id : session.User1Id;
        string partnerName = "Partner"; // Default name

        // 1. Fetch Partner Name for the Header and store it
        Profile partnerProfile = await chatManager.GetUserProfile(partnerId);
        if (partnerProfile != null)
        {
            partnerName = $"{partnerProfile.FirstName} {partnerProfile.LastName}";
        }
        currentPartnerDisplayName = partnerName; // Store name for UIManager use

        // 2. Update UI Header and clear old messages
        uiManager.SetConversationPartnerName(partnerName);
        uiManager.ClearMessageBubbles();

        // 3. Fetch Messages for this Session
        List<ChatMessage> messages = await chatManager.GetChatMessages(session.Id);

        if (messages == null || messages.Count == 0)
        {
            Debug.Log("[AppManager] No messages found for this session.");
            return;
        }

        // 4. Instantiate Message Bubbles
        foreach (var message in messages)
        {
            uiManager.CreateMessageBubble(message, myUserId);
        }
    }

    // 3. Triggered by the Delete button on the ConversationCard
    public async void DeleteChatSession(ChatSession sessionToDelete)
    {
        if (chatManager == null || sessionToDelete == null || currentUserProfile == null) return;

        string myUserId = currentUserProfile.Id;
        bool isUser1 = sessionToDelete.User1Id == myUserId;

        Debug.Log($"[AppManager] Soft Deleting session ID: {sessionToDelete.Id} as User {(isUser1 ? 1 : 2)}");

        try
        {
            // Set the appropriate soft-delete flag
            if (isUser1)
            {
                sessionToDelete.User1Deleted = true;
            }
            else
            {
                sessionToDelete.User2Deleted = true;
            }

            // Update the record in the database
            await sessionToDelete.Update<ChatSession>();

            Debug.Log("[AppManager] Session marked as deleted for user. Reloading history...");

            // Reload the history list to remove the deleted card instantly
            LoadChatHistory();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AppManager] Error deleting session: {ex.Message}");
            uiManager.SetHistoryStatus("Error deleting session.");
        }
    }

    // -------------------------------------------------------------------------
    // --- CHAT SESSION / AUTHENTICATION MANAGEMENT ----------------------------
    // -------------------------------------------------------------------------

    public async void JoinChatSession(string roomCode)
    {
        if (uiManager == null || currentUserProfile == null) return;

        if (string.IsNullOrWhiteSpace(roomCode))
        {
            Debug.LogError("[AppManager] JoinChatSession called with empty room code.");
            uiManager.SetSessionStatus("Room Code cannot be empty.");
            return;
        }

        string myUserId = currentUserProfile.Id;
        uiManager.SetSessionStatus("Searching for room...");
        Debug.Log($"[AppManager] Attempting to join Room: {roomCode} as User: {myUserId}");
        Debug.Log($"[JoinChatSession] Current State: {currentState}");

        try
        {
            // 1. Search for sessions matching the room code.
            var baseQuery = SupabaseManager.Instance
                .From<ChatSession>()
                .Where(s => s.RoomCode == roomCode);

            // 2. Apply OR conditions using the EXPLICIT Interface List syntax.
            var existingSessions = await baseQuery
                .Or(new List<Postgrest.Interfaces.IPostgrestQueryFilter>
                {
                    new Postgrest.QueryFilter("user_1_id", Postgrest.Constants.Operator.Equals, myUserId), // Case A.1
                    new Postgrest.QueryFilter("user_2_id", Postgrest.Constants.Operator.Equals, myUserId), // Case A.2
                    new Postgrest.QueryFilter("user_2_id", Postgrest.Constants.Operator.Is, "null")        // Case B
                })
                .Get();

            var sessionToJoin = existingSessions.Models.FirstOrDefault();

            if (sessionToJoin == null)
            {
                Debug.LogError("[AppManager] Session not found or room is full.");
                uiManager.SetSessionStatus("Error: Room not found or is full.");
                return;
            }

            Debug.Log($"[AppManager] Found Session: {sessionToJoin.Id}. User1: {sessionToJoin.User1Id}, User2: {sessionToJoin.User2Id}");

            // 2. CHECK: If I am already in this session (User1 or User2)
            if (sessionToJoin.User1Id == myUserId || sessionToJoin.User2Id == myUserId)
            {
                Debug.Log("[AppManager] User is already a participant in this session. Rejoining with current flow choice.");

                currentChatSession = sessionToJoin;
                SubscribeToSessionMessages(currentChatSession.Id);
                uiManager.ShowRoomCode(sessionToJoin.RoomCode);

                // Navigate to correct input panel based on chosen flow
                // CameraInput = Sign to Speech (shows camera/text/quickchat buttons)
                // AudioInput = Speech to Sign (shows audio/text/quickchat buttons)
                AppState nextState = currentState == AppState.SignSession
                    ? AppState.CameraInput
                    : AppState.AudioInput;

                Debug.Log($"[JoinChatSession] Rejoining - navigating to: {nextState}");
                ChangeState(nextState);
                return;
            }

            // 3. CHECK: Safety check, ensures User2 is truly empty before joining.
            if (!string.IsNullOrEmpty(sessionToJoin.User2Id))
            {
                Debug.LogError("[AppManager] Found room, but User 2 is occupied by another ID.");
                uiManager.SetSessionStatus("Error: Room is full.");
                return;
            }

            // 4. Join as User 2 - This is the successful first-time join path
            sessionToJoin.User2Id = myUserId;
            Debug.Log("[AppManager] Sending Update to Supabase to take User 2 seat...");

            var response = await sessionToJoin.Update<ChatSession>();
            ChatSession joinedSession = response.Models.FirstOrDefault();

            if (joinedSession != null)
            {
                Debug.Log($"[AppManager] SUCCESS! Joined Session: {joinedSession.Id}.");
                currentChatSession = joinedSession;

                SubscribeToSessionMessages(currentChatSession.Id);
                uiManager.ShowRoomCode(joinedSession.RoomCode);

                // Navigate to correct input panel based on chosen flow
                AppState nextState = currentState == AppState.SignSession
                    ? AppState.CameraInput
                    : AppState.AudioInput;

                Debug.Log($"[JoinChatSession] First time join - navigating to: {nextState}");
                ChangeState(nextState);
            }
            else
            {
                Debug.LogError("[AppManager] Update failed. Response was null.");
                uiManager.SetSessionStatus("Error: Join failed (Database Error).");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[AppManager] EXCEPTION: {ex.Message}");
            uiManager.SetSessionStatus("Error: Could not join session.");
        }
    }

    public async void CreateChatSession()
    {
        if (uiManager == null || currentUserProfile == null) return;

        string myUserId = currentUserProfile.Id;

        // ADD THIS DEBUG LOG
        Debug.Log($"[CreateChatSession] Current State: {currentState}");

        try
        {
            // 1. Generate a unique 6-digit code
            string newCode = "";
            bool codeIsUnique = false;
            System.Random rand = new System.Random();

            while (!codeIsUnique)
            {
                newCode = rand.Next(100000, 999999).ToString();
                var existing = await SupabaseManager.Instance
                    .From<ChatSession>()
                    .Where(s => s.RoomCode == newCode)
                    .Get();

                if (existing.Models.Count == 0)
                {
                    codeIsUnique = true;
                }
            }

            // 2. Create the new session in the database
            var newSession = new ChatSession
            {
                User1Id = myUserId,
                User2Id = null,
                RoomCode = newCode,
                User1Deleted = false,
                User2Deleted = false,
                CreatedAt = DateTime.UtcNow // FIX: Explicitly set time to avoid sending null and triggering DB constraint error
            };

            var response = await SupabaseManager.Instance.From<ChatSession>().Insert(newSession);
            ChatSession createdSession = response.Models.FirstOrDefault();

            if (createdSession == null)
            {
                uiManager.SetSessionStatus("Error: Could not create session.");
                return;
            }

            // 3. We have a session! Store it and show the code.
            Debug.Log($"Session created: {createdSession.Id} with Code: {createdSession.RoomCode}");
            currentChatSession = createdSession;

            SubscribeToSessionMessages(currentChatSession.Id);
            uiManager.ShowRoomCode(createdSession.RoomCode);

            // Navigate to correct input panel based on chosen flow
            AppState nextState = currentState == AppState.SignSession
                ? AppState.CameraInput
                : AppState.AudioInput;

            Debug.Log($"[CreateChatSession] Navigating to: {nextState}");
            ChangeState(nextState);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error creating chat session: {ex.Message}");
            uiManager.SetSessionStatus("Error: Could not create session.");
        }
    }

    // --- SEND MESSAGE ---
    // UPDATED: Now accepts string messageType instead of bool
    public void SendTextMessage(string content, string messageType)
    {
        if (currentChatSession != null && currentUserProfile != null)
        {
            Debug.Log($"[AppManager] SendTextMessage - Content: '{content}', Type: '{messageType}', SessionId: {currentChatSession.Id}, SenderId: {currentUserProfile.Id}");
            chatManager.InsertMessage(currentChatSession.Id, currentUserProfile.Id, content, messageType);
        }
        else
        {
            Debug.LogError($"[AppManager] SendTextMessage FAILED - currentChatSession is null: {currentChatSession == null}, currentUserProfile is null: {currentUserProfile == null}");
        }
    }

    // Called when Sign user closes the camera (e.g. Back button on Main Canvas)
    public void CloseCamera()
    {
        isCameraFullscreen = false;
        if (uiManager != null) uiManager.HideCameraFullscreen();
        ChangeState(AppState.CameraInput);
        Debug.Log("[AppManager] Camera closed - returned to CameraInputMethodPanel");
    }

    // -------------------------------------------------------------------------
    // --- AUTHENTICATION FUNCTIONS --------------------------------------------
    // -------------------------------------------------------------------------

    public async void Login(string email, string password)
    {
        if (uiManager == null) return;

        uiManager.ShowStatus("Logging in...", "login");
        try
        {
            // 1. Sign in the user
            currentSession = await SupabaseManager.Instance.Auth.SignIn(email, password);
            if (currentSession == null || currentSession.User == null)
            {
                throw new System.Exception("Invalid login credentials.");
            }

            // 2. Fetch the user's profile
            var profileResponse = await SupabaseManager.Instance
                .From<Profile>()
                .Where(p => p.Id == currentSession.User.Id)
                .Single();

            currentUserProfile = profileResponse;

            if (currentUserProfile == null)
            {
                throw new System.Exception("Profile not found for this user.");
            }

            // 3. CHECK IF USER IS ACTIVE
            if (currentUserProfile.IsActive == false)
            {
                Debug.LogWarning("User is deactivated. Logging out.");
                uiManager.ShowStatus("Account is deactivated.", "login");
                await SupabaseManager.Instance.Auth.SignOut();
                currentSession = null;
                currentUserProfile = null;
                return;
            }

            // 4. Success!
            Debug.Log("Login Successful! User ID: " + currentSession.User.Id);
            uiManager.ShowStatus("", "login");
            uiManager.SetProfileName(currentUserProfile.FirstName, currentUserProfile.LastName);

            ChangeState(AppState.Home);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Login Failed: " + ex.Message);
            uiManager.ShowStatus("Login Failed: " + ex.Message, "login");
        }
    }

    public async void Register(string email, string password, string firstName, string lastName)
    {
        if (uiManager == null) return;

        uiManager.ShowStatus("Registering...", "register");
        try
        {
            var session = await SupabaseManager.Instance.Auth.SignUp(email, password, new Supabase.Gotrue.SignUpOptions
            {
                Data = new Dictionary<string, object>
                {
                    { "first_name", firstName },
                    { "last_name", lastName }
                }
            });

            if (session != null)
            {
                Debug.Log("Registration Successful!");
                uiManager.ShowStatus("Registration Successful! Please check your email to verify and then Log in.", "register");
                ChangeState(AppState.Login);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Registration Failed: " + ex.Message);
            uiManager.ShowStatus("Registration Failed: " + ex.Message, "register");
        }
    }

    public async void Logout()
    {
        Debug.Log("Logging out...");
        try
        {
            // Clean up subscriptions on logout
            if (realtimeChannel != null)
            {
                try { realtimeChannel.Unsubscribe(); } catch { }
            }

            await SupabaseManager.Instance.Auth.SignOut();
            currentSession = null;
            currentUserProfile = null;
            if (uiManager != null)
            {
                uiManager.ClearPasswordFields();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Logout Failed: " + ex.Message);
        }
        finally
        {
            ChangeState(AppState.Login);
        }
    }

    public async void UpdatePassword(string newPassword)
    {
        if (uiManager == null) return;
        uiManager.ShowPasswordError("Updating password...");

        try
        {
            await SupabaseManager.Instance.Auth.Update(new UserAttributes
            {
                Password = newPassword
            });

            Debug.Log("Password updated!");
            uiManager.ShowPasswordUpdateSuccess();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Password Update Failed: " + ex.Message);
            uiManager.ShowPasswordError("Error: " + ex.Message);
        }
    }

    public async void DeactivateAccount()
    {
        if (currentUserProfile == null) return;

        try
        {
            currentUserProfile.IsActive = false;
            await SupabaseManager.Instance.From<Profile>().Update(currentUserProfile);
            Debug.Log("Account deactivated. Logging out.");
            Logout();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Deactivation Failed: " + ex.Message);
        }
    }
}