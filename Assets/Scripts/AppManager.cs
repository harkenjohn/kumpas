using UnityEngine;
using Supabase.Gotrue;
using System.Threading.Tasks;
using Kumpas.Models;
using System.Collections.Generic;
using Postgrest.Responses;
using System.Linq;
using System;
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
    private ChatSession currentHistorySession;
    private string currentPartnerDisplayName = "Partner";

    // --- Realtime Trigger Flags ---
    private bool triggerOpenCamera = false;
    private bool isCameraFullscreen = false;
    private string receivedTextToSign = "";

    // --- TTS Trigger Flags ---
    private bool triggerTTS = false;
    private string receivedTTSText = "";

    // --- Android TTS ---
    private AndroidJavaObject _tts;
    private bool _ttsReady = false;

    private Supabase.Realtime.RealtimeChannel realtimeChannel;

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
        History,
        ConversationView
    }
    private AppState currentState;

    void Start()
    {
        if (uiManager != null) uiManager.Initialize(this);
        InitTTS();
        ChangeState(AppState.Login);
    }

    void Update()
    {
        if (triggerOpenCamera)
        {
            triggerOpenCamera = false;
            isCameraFullscreen = true;
            Debug.Log($"[AppManager] UPDATE: triggerOpenCamera caught! Opening camera. Text: {receivedTextToSign}");
            if (uiManager != null) uiManager.ShowCameraFullscreen(receivedTextToSign);
        }

        if (triggerTTS)
        {
            triggerTTS = false;
            Debug.Log($"[AppManager] UPDATE: triggerTTS caught! Speaking: {receivedTTSText}");
            SpeakText(receivedTTSText);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleBackButton();
        }
    }

    // -------------------------------------------------------------------------
    // --- ANDROID TTS ---------------------------------------------------------
    // -------------------------------------------------------------------------

    void InitTTS()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass  unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaProxy  listener    = new AppTTSInitListener(() => { _ttsReady = true; });
            _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, listener);
            Debug.Log("[AppManager] TTS initialized");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AppManager] TTS init failed: {e.Message}");
        }
#else
        _ttsReady = true;
#endif
    }

    public void SpeakText(string text)
    {
        // Convert to lowercase so Android TTS reads it as words, not letters
        string spokenText = text.ToLower();
        Debug.Log($"[AppManager] SpeakText: '{spokenText}'");
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_ttsReady && _tts != null)
            _tts.Call<int>("speak", spokenText, 0, null, null);
#else
        Debug.Log($"[AppManager] Editor TTS: '{spokenText}'");
#endif
    }

    // -------------------------------------------------------------------------
    // --- BACK BUTTON ---------------------------------------------------------
    // -------------------------------------------------------------------------

    private void HandleBackButton()
    {
        Debug.Log($"[AppManager] Back button pressed. CurrentState: {currentState}, isCameraFullscreen: {isCameraFullscreen}");

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

    // -------------------------------------------------------------------------
    // --- STATE MANAGEMENT ----------------------------------------------------
    // -------------------------------------------------------------------------

    public void ChangeState(AppState newState)
    {
        Debug.Log($"[AppManager] ChangeState called: {currentState} -> {newState}");
        currentState = newState;

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
    // --- REALTIME SUBSCRIPTION -----------------------------------------------
    // -------------------------------------------------------------------------

    private async void SubscribeToSessionMessages(string sessionId)
    {
        if (realtimeChannel != null)
        {
            try
            {
                realtimeChannel.Unsubscribe();
                Debug.Log("[Realtime] Unsubscribed from previous channel");
            }
            catch { }
        }

        try
        {
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
                            else if (message.MessageType == "TEXT_TO_SPEECH")
                            {
                                receivedTTSText = message.MessageContent;
                                triggerTTS = true;
                                Debug.Log($"[Realtime] *** TTS TRIGGER SET: '{message.MessageContent}' ***");
                            }
                            else
                            {
                                Debug.Log($"[Realtime] Message type '{message.MessageType}' not handled.");
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
    // --- UTILITY -------------------------------------------------------------
    // -------------------------------------------------------------------------

    public string GetCurrentPartnerName()
    {
        return currentPartnerDisplayName;
    }

    // -------------------------------------------------------------------------
    // --- HISTORY / CONVERSATION VIEW -----------------------------------------
    // -------------------------------------------------------------------------

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
            if (string.IsNullOrEmpty(session.User2Id)) continue;

            bool isUser1 = session.User1Id == myUserId;
            if ((isUser1 && session.User1Deleted) || (!isUser1 && session.User2Deleted)) continue;

            string partnerId = isUser1 ? session.User2Id : session.User1Id;
            string partnerName = "";

            if (!string.IsNullOrEmpty(partnerId))
            {
                Profile partnerProfile = await chatManager.GetUserProfile(partnerId);
                if (partnerProfile != null)
                    partnerName = $"{partnerProfile.FirstName} {partnerProfile.LastName}";
            }

            uiManager.CreateConversationCard(session, partnerName, myUserId);
            renderedCount++;
        }

        if (renderedCount == 0)
            uiManager.SetHistoryStatus("No completed conversations found.");
    }

    public async void ViewChatHistory(ChatSession session)
    {
        if (currentUserProfile == null || chatManager == null || uiManager == null) return;

        currentHistorySession = session;
        await LoadMessagesForSession(session);
        ChangeState(AppState.ConversationView);
    }

    public async Task LoadMessagesForSession(ChatSession session)
    {
        if (currentUserProfile == null || chatManager == null || uiManager == null) return;

        string myUserId = currentUserProfile.Id;
        string partnerId = session.User1Id == myUserId ? session.User2Id : session.User1Id;
        string partnerName = "Partner";

        Profile partnerProfile = await chatManager.GetUserProfile(partnerId);
        if (partnerProfile != null)
            partnerName = $"{partnerProfile.FirstName} {partnerProfile.LastName}";

        currentPartnerDisplayName = partnerName;
        uiManager.SetConversationPartnerName(partnerName);
        uiManager.ClearMessageBubbles();

        List<ChatMessage> messages = await chatManager.GetChatMessages(session.Id);

        if (messages == null || messages.Count == 0)
        {
            Debug.Log("[AppManager] No messages found for this session.");
            return;
        }

        foreach (var message in messages)
            uiManager.CreateMessageBubble(message, myUserId);
    }

    public async void DeleteChatSession(ChatSession sessionToDelete)
    {
        if (chatManager == null || sessionToDelete == null || currentUserProfile == null) return;

        string myUserId = currentUserProfile.Id;
        bool isUser1 = sessionToDelete.User1Id == myUserId;

        Debug.Log($"[AppManager] Soft Deleting session ID: {sessionToDelete.Id} as User {(isUser1 ? 1 : 2)}");

        try
        {
            if (isUser1)
                sessionToDelete.User1Deleted = true;
            else
                sessionToDelete.User2Deleted = true;

            await sessionToDelete.Update<ChatSession>();
            Debug.Log("[AppManager] Session marked as deleted for user. Reloading history...");
            LoadChatHistory();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AppManager] Error deleting session: {ex.Message}");
            uiManager.SetHistoryStatus("Error deleting session.");
        }
    }

    // -------------------------------------------------------------------------
    // --- CHAT SESSION MANAGEMENT ---------------------------------------------
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
            var baseQuery = SupabaseManager.Instance
                .From<ChatSession>()
                .Where(s => s.RoomCode == roomCode);

            var existingSessions = await baseQuery
                .Or(new List<Postgrest.Interfaces.IPostgrestQueryFilter>
                {
                    new Postgrest.QueryFilter("user_1_id", Postgrest.Constants.Operator.Equals, myUserId),
                    new Postgrest.QueryFilter("user_2_id", Postgrest.Constants.Operator.Equals, myUserId),
                    new Postgrest.QueryFilter("user_2_id", Postgrest.Constants.Operator.Is, "null")
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

            if (sessionToJoin.User1Id == myUserId || sessionToJoin.User2Id == myUserId)
            {
                Debug.Log("[AppManager] User is already a participant in this session. Rejoining.");

                currentChatSession = sessionToJoin;
                SubscribeToSessionMessages(currentChatSession.Id);
                uiManager.ShowRoomCode(sessionToJoin.RoomCode);

                AppState nextState = currentState == AppState.SignSession
                    ? AppState.CameraInput
                    : AppState.AudioInput;

                Debug.Log($"[JoinChatSession] Rejoining - navigating to: {nextState}");
                ChangeState(nextState);
                return;
            }

            if (!string.IsNullOrEmpty(sessionToJoin.User2Id))
            {
                Debug.LogError("[AppManager] Found room, but User 2 is occupied by another ID.");
                uiManager.SetSessionStatus("Error: Room is full.");
                return;
            }

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
        Debug.Log($"[CreateChatSession] Current State: {currentState}");

        try
        {
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
                    codeIsUnique = true;
            }

            var newSession = new ChatSession
            {
                User1Id = myUserId,
                User2Id = null,
                RoomCode = newCode,
                User1Deleted = false,
                User2Deleted = false,
                CreatedAt = DateTime.UtcNow
            };

            var response = await SupabaseManager.Instance.From<ChatSession>().Insert(newSession);
            ChatSession createdSession = response.Models.FirstOrDefault();

            if (createdSession == null)
            {
                uiManager.SetSessionStatus("Error: Could not create session.");
                return;
            }

            Debug.Log($"Session created: {createdSession.Id} with Code: {createdSession.RoomCode}");
            currentChatSession = createdSession;

            SubscribeToSessionMessages(currentChatSession.Id);
            uiManager.ShowRoomCode(createdSession.RoomCode);

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

    public void CloseCamera()
    {
        isCameraFullscreen = false;
        if (uiManager != null) uiManager.HideCameraFullscreen();
        ChangeState(AppState.CameraInput);
        Debug.Log("[AppManager] Camera closed - returned to CameraInputMethodPanel");
    }

    // -------------------------------------------------------------------------
    // --- AUTHENTICATION ------------------------------------------------------
    // -------------------------------------------------------------------------

    public async void Login(string email, string password)
    {
        if (uiManager == null) return;

        uiManager.ShowStatus("Logging in...", "login");
        try
        {
            currentSession = await SupabaseManager.Instance.Auth.SignIn(email, password);
            if (currentSession == null || currentSession.User == null)
                throw new System.Exception("Invalid login credentials.");

            var profileResponse = await SupabaseManager.Instance
                .From<Profile>()
                .Where(p => p.Id == currentSession.User.Id)
                .Single();

            currentUserProfile = profileResponse;

            if (currentUserProfile == null)
                throw new System.Exception("Profile not found for this user.");

            if (currentUserProfile.IsActive == false)
            {
                Debug.LogWarning("User is deactivated. Logging out.");
                uiManager.ShowStatus("Account is deactivated.", "login");
                await SupabaseManager.Instance.Auth.SignOut();
                currentSession = null;
                currentUserProfile = null;
                return;
            }

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
            if (realtimeChannel != null)
            {
                try { realtimeChannel.Unsubscribe(); } catch { }
            }

            if (_tts != null)
            {
                try { _tts.Call("shutdown"); } catch { }
            }

            await SupabaseManager.Instance.Auth.SignOut();
            currentSession = null;
            currentUserProfile = null;
            if (uiManager != null)
                uiManager.ClearPasswordFields();
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

// ============================================================
// Android TTS Listener for AppManager
// ============================================================
#if UNITY_ANDROID && !UNITY_EDITOR
public class AppTTSInitListener : AndroidJavaProxy
{
    private System.Action _onReady;
    public AppTTSInitListener(System.Action onReady)
        : base("android.speech.tts.TextToSpeech$OnInitListener") { _onReady = onReady; }
    public void onInit(int status)
    {
        if (status == 0) _onReady?.Invoke();
        else UnityEngine.Debug.LogError("[AppManager] TTS init failed: " + status);
    }
}
#endif