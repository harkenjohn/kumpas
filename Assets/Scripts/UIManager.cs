using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Kumpas.Models;
using System.Collections.Generic;
using System.Collections;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

/*
 * UI MANAGER (VIEW) - CONNECTED
 * * WHAT IT DOES:
 * - Holds references to all UI GameObjects.
 * - Shows and hides panels based on commands from the AppManager.
 */
public class UIManager : MonoBehaviour
{
    // --- Private References ---
    private AppManager appManager; // This will be our connection to the "brain"
    private List<GameObject> activeHistoryCards = new List<GameObject>(); // Tracks dynamically created cards
    private List<GameObject> activeMessageBubbles = new List<GameObject>(); // Tracks dynamically created message bubbles

    // --- PANELS ---
    [Header("Panels")]
    public GameObject uiRoot; // The root 'UI' GameObject containing all app panels
    public GameObject authPanel;
    public GameObject registerPanel;
    public GameObject homePanel;
    public GameObject profilePanel;
    public GameObject convertToSignSessionPanel;
    public GameObject convertToSpeechSessionPanel;
    public GameObject cameraInputMethodPanel;
    public GameObject audioInputMethodPanel;
    public GameObject voiceInputPanel;
    public GameObject toSpeechQuickChatPanel; // ADDED THIS
    public GameObject toSignQuickChatPanel;

    // --- NEW TEXT INPUT PANELS ---
    [Header("Text Input Panels")]
    public GameObject textToSpeechInputPanel; // For Sign User to type
    public GameObject textToSignInputPanel;   // For Speech User to type

    [Header("Text To Sign Toast")]
    public GameObject textToSignToastObject;  // the SendToast panel
    public TMP_Text   textToSignToastIcon;    // ✓ or ✗
    public TMP_Text   textToSignToastMessage; // "Message Sent!" etc.
    public float      textToSignToastDuration = 3f;

private Coroutine _textToSignToastCoroutine;

    // --- NEW HISTORY PANELS ---
    [Header("History Panels")]
    public GameObject historyPanel;         // The panel containing chat history list
    public GameObject conversationViewPanel; // The panel showing messages inside a chat

    // --- CONVERSATION VIEW UI (UPDATED) ---
    [Header("Conversation View UI")]
    public TMP_Text conversationPartnerNameText;
    public Transform messageContentContainer;    // Parent for all message bubbles
    public GameObject messageCardPrefab;          // Single prefab for all messages (Sender/Receiver)
    public Button backToHistoryButton;

    // --- DYNAMIC CONTENT REFERENCES ---
    [Header("Dynamic Content References")]
    public GameObject conversationCardPrefab; // The prefab to instantiate
    public Transform historyContentContainer; // The parent where cards will be spawned
    public TMP_Text historyStatusText;        // Text to show "Loading..." or "No conversations"

    // --- LOGIN INPUTS ---
    [Header("Login Inputs")]
    public TMP_InputField loginEmailInput;
    public TMP_InputField loginPasswordInput;
    public TMP_Text loginStatusText;

    // --- REGISTER INPUTS ---
    [Header("Register Inputs")]
    public TMP_InputField registerFirstNameInput;
    public TMP_InputField registerLastNameInput;
    public TMP_InputField registerEmailInput;
    public TMP_InputField registerPasswordInput;
    public TMP_InputField registerConfirmPasswordInput;
    public TMP_Text registerStatusText;

    // --- PROFILE PANEL UI ---
    [Header("Profile Panel UI")]
    public TMP_Text profileUserNameText;
    public TMP_InputField passwordInput;
    public TMP_InputField confirmPasswordInput;
    public GameObject changePasswordSuccessText;
    public GameObject changePasswordErrorText;
    public GameObject areYouSurePanel;

    // --- SIGN SESSION PANEL (NEW) ---
    [Header("Sign Session Panel")]
    public Button sign_CreateSessionButton;
    public Button sign_JoinSessionButton;
    public Button sign_JoinWithoutSessionButton;
    public TMP_Text sign_JoinSessionText;
    public TMP_InputField sign_JoinSessionInput;

    // --- SPEECH SESSION PANEL (NEW) ---
    [Header("Speech Session Panel")]
    public Button speech_CreateSessionButton;
    public Button speech_JoinSessionButton;
    public Button speech_JoinWithoutSessionButton;
    public TMP_Text speech_JoinSessionText;
    public TMP_InputField speech_JoinSessionInput;

    // --- TEXT INPUT PANELS UI (NEW) ---
    [Header("Text Input Panels UI")]
    public TMP_InputField textToSpeechInput;
    public Button textToSpeechSendButton;
    public TMP_InputField textToSignInput;
    public Button textToSignSendButton;

    [Header("MediaPipe Integration")]
    public GameObject mediaPipeSolution;  // The 'Solution' GameObject
    public GameObject cameraFeedContainer; // The 'Container Panel' inside Main Canvas
    public GameObject landMarker; // the faccelandmark

    [Header("ASL Camera Feed")]
    public GameObject aslCameraFeed;
    public ASLManager aslManager;
    public GameObject handLandmarkCanvas;
    public GameObject poseLandmarkCanvas;  // Added for ASL camera feed
    public GameObject handSolution;

    [SerializeField] private FaceLandmarkerRunner faceLandmarkerRunner;
    [SerializeField] private AnnotationCleaner annotationCleaner;
    [Header("ASL Orientation")]
    [Tooltip("Assign the GameObject that has ASLOrientationHandler attached")]
    public ASLOrientationHandler aslOrientationHandler;

    [Header("Join Error Text")]
    public TMP_Text signJoinErrorText;
    public TMP_Text speechJoinErrorText;

    private Coroutine _joinErrorCoroutine;

    // This function will be called by AppManager to connect them
    public void Initialize(AppManager am)
    {
        appManager = am;
        // Link the back button action here
        if (backToHistoryButton != null)
        {
            backToHistoryButton.onClick.AddListener(OnBackToHistoryButton);
        }
    }

    // --- HELPER: HIDES ALL PANELS ---
    public void HideAllPanels()
    {
        // Always make sure app UI root is visible
        if (uiRoot != null) uiRoot.SetActive(true);

        authPanel?.SetActive(false);
        registerPanel?.SetActive(false);
        homePanel?.SetActive(false);
        profilePanel?.SetActive(false);
        convertToSignSessionPanel?.SetActive(false);
        convertToSpeechSessionPanel?.SetActive(false);
        cameraInputMethodPanel?.SetActive(false);
        audioInputMethodPanel?.SetActive(false);
        textToSpeechInputPanel?.SetActive(false);
        textToSignInputPanel?.SetActive(false);
        historyPanel?.SetActive(false);
        conversationViewPanel?.SetActive(false);
        voiceInputPanel?.SetActive(false);
        toSpeechQuickChatPanel?.SetActive(false); // ADDED THIS
        toSignQuickChatPanel?.SetActive(false);

        // Disable the visual canvas but leave handSolution running —
        // disabling a MediaPipe Async runner breaks it permanently.
        //if (mediaPipeSolution != null) mediaPipeSolution.SetActive(false);
        if (cameraFeedContainer != null) cameraFeedContainer.SetActive(false);
        if (handLandmarkCanvas != null) handLandmarkCanvas.SetActive(false);
        if (landMarker != null) landMarker.SetActive(false);
    }

    // --- HELPER: SHOWS CAMERA FULLSCREEN (hides all app UI) ---
    public void ShowCameraFullscreen(string messageToSign = "")
    {
        // Hide the entire app UI
        if (uiRoot != null) uiRoot.SetActive(false);

        // Show camera feed and enable MediaPipe
        if (cameraFeedContainer != null) cameraFeedContainer.SetActive(true);
        if (mediaPipeSolution != null) mediaPipeSolution.SetActive(true);
        if (landMarker != null) landMarker.SetActive(true);

        Debug.Log("[UIManager] Camera fullscreen shown - all UI hidden, MediaPipe enabled");

        // Start coroutine to wait for ASLRealtimeSentencePlayer to be ready
        if (!string.IsNullOrEmpty(messageToSign))
        {
            StartCoroutine(WaitAndPlay(messageToSign));
        }
    }

    IEnumerator WaitAndPlay(string messageToSign)
    {
        float timeout = 15f;
        float elapsed = 0f;

        Debug.Log("[UIManager] Waiting for ASLRealtimeSentencePlayer instance...");

        while (ASLRealtimeSentencePlayer.Instance == null)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= timeout)
            {
                Debug.LogError("[UIManager] Timed out waiting for ASLRealtimeSentencePlayer!");
                yield break;
            }
            yield return null;
        }

        Debug.Log("[UIManager] ASLRealtimeSentencePlayer found! Playing sentence...");
        ASLRealtimeSentencePlayer.Instance.PlaySentence(messageToSign);
    }

    // --- HELPER: HIDES CAMERA, RESTORES APP UI ---
    public void HideCameraFullscreen()
    {
        // Hide camera feed and disable MediaPipe
        if (landMarker != null) landMarker.SetActive(false);
        if (mediaPipeSolution != null) mediaPipeSolution.SetActive(false);
        if (cameraFeedContainer != null) cameraFeedContainer.SetActive(false);
        

        // Restore the app UI
        if (uiRoot != null) uiRoot.SetActive(true);

        Debug.Log("[UIManager] Camera hidden - app UI restored");
    }

    // --- 1. PANEL SWITCHING FUNCTIONS (UPDATED TO USE HideAllPanels) ---

    public void ShowLoginPanel()
    {
        HideAllPanels();
        if (authPanel != null) authPanel.SetActive(true);
        ClearLoginInputs();
        ShowStatus("", "login");
    }

    public void ShowRegisterPanel()
    {
        HideAllPanels();
        if (registerPanel != null) registerPanel.SetActive(true);
    }

    public void ShowHomePanel()
    {
        HideAllPanels();
        if (homePanel != null) homePanel.SetActive(true);
    }

    public void ShowProfilePanel()
    {
        HideAllPanels();
        if (profilePanel != null) profilePanel.SetActive(true);
        ClearPasswordFields();
        if (areYouSurePanel != null) areYouSurePanel.SetActive(false);
    }

    public void ShowSignSessionPanel()
    {
        HideAllPanels();
        if (convertToSignSessionPanel != null) convertToSignSessionPanel.SetActive(true);
        // Clear old status messages
        if (sign_JoinSessionText != null) sign_JoinSessionText.text = "Join a session or create a new one";
        if (sign_JoinSessionInput != null) sign_JoinSessionInput.text = ""; // Clear input
        if (sign_CreateSessionButton != null) sign_CreateSessionButton.interactable = true;
        if (sign_JoinSessionButton != null) sign_JoinSessionButton.interactable = true;
    }

    public void ShowSpeechSessionPanel()
    {
        HideAllPanels();
        if (convertToSpeechSessionPanel != null) convertToSpeechSessionPanel.SetActive(true);
        // Clear old status messages
        if (speech_JoinSessionText != null) speech_JoinSessionText.text = "Join a session or create a new one";
        if (speech_JoinSessionInput != null) speech_JoinSessionInput.text = "";
        if (speech_CreateSessionButton != null) speech_CreateSessionButton.interactable = true;
        if (speech_JoinSessionButton != null) speech_JoinSessionButton.interactable = true;
    }

    public void ShowCameraInputMethodPanel()
    {
        // Show the app UI with the input method selection buttons
        // Camera does NOT open here - it only opens via Realtime trigger
        HideAllPanels();
        if (cameraInputMethodPanel != null) cameraInputMethodPanel.SetActive(true);
        Debug.Log("[UIManager] Camera Input Method Panel shown - waiting for Realtime trigger to open camera");
    }

    // ADDED THIS FUNCTION
    public void ShowToSpeechQuickChatPanel()
    {
        HideAllPanels();
        if (toSpeechQuickChatPanel != null) toSpeechQuickChatPanel.SetActive(true);
    }

    public void ShowToSignQuickChatPanel()
    {
        HideAllPanels();
        if (toSignQuickChatPanel != null) toSignQuickChatPanel.SetActive(true);
    }

    // Called by the Camera button inside CameraInputMethodPanel
    // Opens the ASL fullscreen camera feed and starts the ASL session
    public void OnOpenASLCameraButton()
    {
        if (aslManager == null)
        {
            Debug.LogError("[UIManager] ASLManager reference not set!");
            return;
        }

        // Hide the entire app UI, show the hand+pose camera canvases
        if (uiRoot != null) uiRoot.SetActive(false);
        if (handLandmarkCanvas != null) handLandmarkCanvas.SetActive(true);
        if (poseLandmarkCanvas != null) poseLandmarkCanvas.SetActive(true);
        // NOTE: handSolution and PoseLandmarkerRunner are NOT toggled —
        // disabling MediaPipe Async runners breaks them permanently.

        // Force landscape for ASL recognition and notify orientation handler
        //Screen.orientation = ScreenOrientation.LandscapeLeft;
        if (aslOrientationHandler != null) aslOrientationHandler.OnASLSessionStarted();

        aslManager.StartSession();

        Debug.Log("[UIManager] ASL Camera opened - forced landscape");
    }

    // Called by ASLManager when the session ends (3s timeout)
    // Hides the camera feed and returns to the CameraInputMethodPanel
    public void OnASLSessionEnded()
    {
        // Hide the camera canvases, restore app UI
        if (handLandmarkCanvas != null) handLandmarkCanvas.SetActive(false);
        if (poseLandmarkCanvas != null) poseLandmarkCanvas.SetActive(false);
        if (uiRoot != null) uiRoot.SetActive(true);
        if (cameraInputMethodPanel != null) cameraInputMethodPanel.SetActive(true);
        // NOTE: handSolution and PoseLandmarkerRunner are NOT touched —
        // disabling MediaPipe Async runners breaks them permanently.

        // Revert to portrait and notify orientation handler
        //Screen.orientation = ScreenOrientation.Portrait;
        if (aslOrientationHandler != null) aslOrientationHandler.OnASLSessionEnded();

        Debug.Log("[UIManager] ASL session ended - returned to CameraInputMethodPanel");
    }

    public void ShowAudioInputMethodPanel()
    {
        HideAllPanels();
        // Restore UI in case camera was previously shown
        if (uiRoot != null) uiRoot.SetActive(true);
        if (audioInputMethodPanel != null) audioInputMethodPanel.SetActive(true);
        if (mediaPipeSolution != null) mediaPipeSolution.SetActive(false);
        if (cameraFeedContainer != null) cameraFeedContainer.SetActive(false);
        Debug.Log("[UIManager] Audio Input Panel shown - MediaPipe disabled");
    }

    // --- TEXT INPUT SHOW FUNCTIONS ---
    public void ShowTextToSpeechInputPanel()
    {
        HideAllPanels();
        if (textToSpeechInputPanel != null) textToSpeechInputPanel.SetActive(true); // SHOW
    }

    public void ShowTextToSignInputPanel()
    {
        HideAllPanels();
        if (textToSignInputPanel != null) textToSignInputPanel.SetActive(true);      // SHOW
    }

    // --- HISTORY SHOW FUNCTIONS (NEW) ---
    public void ShowHistoryPanel()
    {
        HideAllPanels();
        if (historyPanel != null) historyPanel.SetActive(true);
    }

    public void ShowConversationViewPanel()
    {
        HideAllPanels();
        if (conversationViewPanel != null) conversationViewPanel.SetActive(true);
    }

    public void ShowVoiceInputPanel()
    {
        HideAllPanels();
        if (voiceInputPanel != null)
            voiceInputPanel.SetActive(true);

        Debug.Log("[UIManager] Voice Input Panel shown");
    }


    // --- 2. DYNAMIC HISTORY FUNCTIONS (NEW) ---

    // Called by AppManager to create a new card from fetched data
    public void CreateConversationCard(ChatSession session, string partnerName, string myUserId)
    {
        if (conversationCardPrefab == null || historyContentContainer == null)
        {
            Debug.LogError("Cannot create conversation card: Prefab or Container is missing.");
            return;
        }

        GameObject newCardObject = Instantiate(conversationCardPrefab, historyContentContainer);
        ConversationCard newCard = newCardObject.GetComponent<ConversationCard>();

        if (newCard != null)
        {
            newCard.Initialize(session, this, appManager, partnerName, myUserId);
            activeHistoryCards.Add(newCardObject);
        }
        else
        {
            Debug.LogError("ConversationCard script missing on prefab!");
            Destroy(newCardObject);
        }
    }

    // Called by AppManager to remove all old cards
    public void ClearHistoryCards()
    {
        foreach (GameObject card in activeHistoryCards)
        {
            Destroy(card);
        }
        activeHistoryCards.Clear();
    }

    // Called by AppManager to show loading/error messages in the history view
    public void SetHistoryStatus(string message)
    {
        if (historyStatusText != null)
        {
            historyStatusText.text = message;
        }
    }

    // --- DYNAMIC CONVERSATION VIEW FUNCTIONS (NEW) ---

    // Called by AppManager to set the partner's name in the header
    public void SetConversationPartnerName(string name)
    {
        if (conversationPartnerNameText != null)
        {
            conversationPartnerNameText.text = name;
        }
    }

    // Clears all existing message bubbles
    public void ClearMessageBubbles()
    {
        foreach (GameObject bubble in activeMessageBubbles)
        {
            Destroy(bubble);
        }
        activeMessageBubbles.Clear();
    }

    // Creates a new message card (called by AppManager)
    public void CreateMessageBubble(ChatMessage message, string myUserId)
    {
        // Since we are using one full-width card template, we reference the new 'messageCardPrefab'
        if (messageCardPrefab == null || messageContentContainer == null)
        {
            Debug.LogError("Cannot create message card: Prefab or Container is missing.");
            return;
        }

        // Instantiate the single full-width prefab
        GameObject newCardObject = Instantiate(messageCardPrefab, messageContentContainer);
        MessageCard newCard = newCardObject.GetComponent<MessageCard>();

        // Determine the sender's name for display
        string senderDisplayName = message.SenderId == myUserId ? "You" : appManager.GetCurrentPartnerName();

        if (newCard != null)
        {
            // The MessageCard script handles the display, including identifying the sender.
            newCard.Initialize(message, senderDisplayName);
            activeMessageBubbles.Add(newCardObject);
        }
        else
        {
            Debug.LogError("MessageCard script missing on prefab!");
            Destroy(newCardObject);
        }
    }


    // --- 3. BUTTON CLICK FUNCTIONS ---

    // MainCanvas (Camera UI): Back Button - closes camera and returns to CameraInputMethodPanel
    public void OnCloseCameraButton()
    {
        if (appManager == null) return;
        appManager.CloseCamera();
        Debug.Log("[UIManager] Close camera button pressed");
    }

    // HomePanel: History Button (NEW)
    public void OnShowHistoryPanelButton()
    {
        if (appManager == null) return;
        appManager.LoadChatHistory(); // Load and then transition
    }

    // ConversationViewPanel: Back Button (NEW)
    public void OnBackToHistoryButton()
    {
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.History); // Return to the list of sessions
    }

    // AuthPanel: Sign In Button
    public void OnLoginButton()
    {
        if (appManager == null) return;
        string email = loginEmailInput.text;
        string password = loginPasswordInput.text;

        // Pass the login request to the AppManager
        appManager.Login(email, password);
    }

    // AuthPanel: Create Account Button (Switches panels)
    public void OnShowRegisterPanelButton()
    {
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.Register);
        ShowStatus("", "login"); // Clear any old error messages
    }

    // RegisterPanel: Back to Sign In Button (Switches panels)
    public void OnBackToLoginButton()
    {
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.Login);
        ShowStatus("", "register"); // Clear any old error messages
    }


    // RegisterPanel: Create Account Button (Handles logic)
    public void OnRegisterButton()
    {
        if (appManager == null) return;

        // 1. Get the text from the input fields
        string firstName = registerFirstNameInput.text;
        string lastName = registerLastNameInput.text;
        string email = registerEmailInput.text;
        string password = registerPasswordInput.text;
        string confirmPassword = registerConfirmPasswordInput.text;

        // 2. Check if fields are empty
        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowStatus("All fields are required!", "register");
            return;
        }

        // 3. Validate email format
        if (!IsEmailValid(email))
        {
            ShowStatus("Please enter a valid email address.", "register");
            return;
        }

        // 4. Validate password strength
        if (!IsPasswordValid(password))
        {
            ShowStatus("Must be 8+ chars, with uppercase, lowercase, number & symbol.", "register");
            return;
        }

        // 5. Check if passwords match
        if (password != confirmPassword)
        {
            ShowStatus("Passwords do not match!", "register");
            return;
        }

        // 6. Pass the data to the AppManager to handle
        appManager.Register(email, password, firstName, lastName);
    }

    // This is for your 'toSignButton' (navigates to the panel)
    public void OnShowSignSessionPanelButton()
    {
        if (appManager == null) return;
        Debug.Log("[UIManager] OnShowSignSessionPanelButton called - changing to SignSession state");
        appManager.ChangeState(AppManager.AppState.SignSession);
    }

    // This is for your 'toSpeechButton' (navigates to the panel)
    public void OnShowSpeechSessionPanelButton()
    {
        if (appManager == null) return;
        Debug.Log("[UIManager] OnShowSpeechSessionPanelButton called - changing to SpeechSession state");
        appManager.ChangeState(AppManager.AppState.SpeechSession);
    }

    // --- SESSION LOBBY BUTTONS ---

    // This is for the "Create" button INSIDE your ConvertToSignSessionPanel
    public void OnSignCreateSessionButton()
    {
        if (appManager == null) return;

        Debug.Log("[UIManager] OnSignCreateSessionButton called");

        if (sign_CreateSessionButton != null) sign_CreateSessionButton.interactable = false;
        if (sign_JoinSessionButton != null) sign_JoinSessionButton.interactable = false;
        if (sign_JoinSessionText != null) sign_JoinSessionText.text = "Creating session...";

        appManager.CreateChatSession();
    }

    // This is for the "Create" button INSIDE your ConvertToSpeechSessionPanel
    public void OnSpeechCreateSessionButton()
    {
        if (appManager == null) return;

        Debug.Log("[UIManager] OnSpeechCreateSessionButton called");

        if (speech_CreateSessionButton != null) speech_CreateSessionButton.interactable = false;
        if (speech_JoinSessionButton != null) speech_JoinSessionButton.interactable = false;
        if (speech_JoinSessionText != null) speech_JoinSessionText.text = "Creating session...";

        // The logic for creating a session is shared, AppManager determines the next state
        appManager.CreateChatSession();
    }

    // This is for the "Join" button INSIDE your ConvertToSignSessionPanel
    public void OnSignJoinSessionButton()
    {
        if (appManager == null) return;

        Debug.Log("[UIManager] OnSignJoinSessionButton called");

        string roomCode = sign_JoinSessionInput.text;

        // FIX: Added whitespace check
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            if (sign_JoinSessionText != null) sign_JoinSessionText.text = "Room Code cannot be empty.";
            return;
        }

        // Disable buttons to prevent double-click while waiting for the join attempt
        if (sign_JoinSessionButton != null) sign_JoinSessionButton.interactable = false;
        if (sign_CreateSessionButton != null) sign_CreateSessionButton.interactable = false;

        if (sign_JoinSessionText != null) sign_JoinSessionText.text = "Joining session...";

        // Pass the trimmed room code to the AppManager
        appManager.JoinChatSession(roomCode.Trim());
    }

    // This is for the "Join" button INSIDE your ConvertToSpeechSessionPanel
    public void OnSpeechJoinSessionButton()
    {
        if (appManager == null) return;

        Debug.Log("[UIManager] OnSpeechJoinSessionButton called");

        string roomCode = speech_JoinSessionInput.text;

        // FIX: Added whitespace check
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            if (speech_JoinSessionText != null) speech_JoinSessionText.text = "Room Code cannot be empty.";
            return;
        }

        // Disable button to prevent double-click
        if (speech_JoinSessionButton != null) speech_JoinSessionButton.interactable = false;
        if (speech_CreateSessionButton != null) speech_CreateSessionButton.interactable = false;

        if (speech_JoinSessionText != null) speech_JoinSessionText.text = "Joining session...";

        // Pass the trimmed room code to the AppManager
        appManager.JoinChatSession(roomCode.Trim());
    }

    // --- INPUT METHOD NAVIGATION BUTTONS ---

    // CameraInputMethodPanel: Text Input Button (Sign User) -> Text to Speech Panel
    public void OnShowTextToSpeechInputButton()
    {
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.TextToSpeechInput);
    }

    // CameraInputMethodPanel: Quick Chat Button
    // ADDED THIS FUNCTION
    public void OnShowToSpeechQuickChatButton()
    {
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.ToSpeechQuickChat);
    }

    public void OnShowToSignQuickChatButton()
    {
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.ToSignQuickChat);
    }

    // AudioInputMethodPanel: Text Input Button (Speech User) -> Text to Sign Panel
    public void OnShowTextToSignInputButton()
    {
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.TextToSignInput);
    }

    // TextToSpeechInputPanel: Back Button (Returns to Camera Input)
    public void OnBackToCameraInputButton()
    {
        //faceLandmarkerRunner.Stop();
        //faceLandmarkerRunner.ClearAnnotations();
        //faceLandmarkerRunner.ForceClear();
        //annotationCleaner.ForceClear();
        //ImageSourceProvider.ImageSource?.Stop();
        StartCoroutine(faceLandmarkerRunner.ResetRunner());
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.CameraInput);
    }

    // TextToSignInputPanel: Back Button (Returns to Audio Input)
    public void OnBackToAudioInputButton()
    {
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.AudioInput);
    }

    // --- TEXT INPUT SEND BUTTONS (UPDATED) ---

    // TextToSpeechInputPanel: Send Text Button (Sign User types text)
    public void OnTextToSpeechSendButton()
    {
        if (appManager == null) return;

        string message = textToSpeechInput.text;
        if (string.IsNullOrWhiteSpace(message)) return;

        // Sign user typed text → Send as TEXT_TO_SPEECH
        appManager.SendTextMessage(message.Trim(), "TEXT_TO_SPEECH");
        textToSpeechInput.text = "";

        Debug.Log("[UIManager] Sent TEXT_TO_SPEECH message");
    }

    // TextToSignInputPanel: Send Text Button (Speech User types text)
    public void OnTextToSignSendButton()
    {
        if (appManager == null)
        {
            ShowTextToSignToast(success: false, "Message Not Sent");
            return;
        }

        string message = textToSignInput.text;

        if (string.IsNullOrWhiteSpace(message))
        {
            ShowTextToSignToast(success: false, "Please type a message first");
            return;
        }

        try
        {
            appManager.SendTextMessage(message.Trim(), "TEXT_TO_SIGN");
            textToSignInput.text = "";
            ShowTextToSignToast(success: true, "Message Sent!");
            Debug.Log("[UIManager] Sent TEXT_TO_SIGN message - Camera should trigger for partner");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[UIManager] SendTextMessage failed: " + ex.Message);
            ShowTextToSignToast(success: false, "Message Not Sent");
        }
    }

    private void ShowTextToSignToast(bool success, string message)
    {
        if (textToSignToastObject == null) return;

        // Set icon and message
        if (textToSignToastIcon != null)
            textToSignToastIcon.text = success ? "✓" : "✗";

        if (textToSignToastMessage != null)
            textToSignToastMessage.text = message;

        // Set color — green for success, red for failure
        if (textToSignToastIcon != null)
            textToSignToastIcon.color = success
                ? new Color(0.18f, 0.8f, 0.44f)   // green
                : new Color(0.91f, 0.3f, 0.24f);   // red

        // Cancel previous toast if still showing
        if (_textToSignToastCoroutine != null)
            StopCoroutine(_textToSignToastCoroutine);

        _textToSignToastCoroutine = StartCoroutine(TextToSignToastRoutine());
    }

    private IEnumerator TextToSignToastRoutine()
    {
        textToSignToastObject.SetActive(true);
        yield return new WaitForSeconds(textToSignToastDuration);
        textToSignToastObject.SetActive(false);
        _textToSignToastCoroutine = null;
    }

    public void OnShowVoiceInputButton()
    {
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.VoiceInput);
    }

    // HomePanel: Profile Button (Switches panels)
    public void OnShowProfilePanelButton()
    {
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.Profile);
    }

    // ProfilePanel: Back to Home Button
    public void OnShowHomePanelButton()
    {
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.Home);
    }

    // ProfilePanel: Logout Button
    public void OnLogoutButton()
    {
        if (appManager == null) return;
        appManager.Logout();
    }

    // ProfilePanel: Update Password Button
    public void OnUpdatePasswordButton()
    {
        if (appManager == null) return;

        string newPassword = passwordInput.text;
        string confirmNewPassword = confirmPasswordInput.text;

        // Hide all helper text
        if (changePasswordSuccessText != null) changePasswordSuccessText.SetActive(false);
        if (changePasswordErrorText != null) changePasswordErrorText.SetActive(false);

        if (string.IsNullOrEmpty(newPassword))
        {
            ShowPasswordError("New password cannot be empty.");
            return;
        }

        // Validate password strength
        if (!IsPasswordValid(newPassword))
        {
            ShowPasswordError("Must be 8+ chars, with uppercase, lowercase, number & symbol.");
            return;
        }

        if (newPassword != confirmNewPassword)
        {
            ShowPasswordError("Passwords do not match.");
            return;
        }

        appManager.UpdatePassword(newPassword);
    }

    // ProfilePanel: Delete Account Button
    public void OnDeleteAccountButton()
    {
        if (areYouSurePanel != null) areYouSurePanel.SetActive(true);
    }

    // AreYouSurePanel: YES Button
    public void OnDeleteAccountYesButton()
    {
        if (appManager == null) return;
        appManager.DeactivateAccount();
        if (areYouSurePanel != null) areYouSurePanel.SetActive(false);
    }

    // AreYouSurePanel: NO Button
    public void OnDeleteAccountNoButton()
    {
        if (areYouSurePanel != null) areYouSurePanel.SetActive(false);
    }


    // --- 4. HELPER FUNCTIONS ---

    // Validates email format:
    // - Must contain exactly one @
    // - Local part cannot start or end with a dot, or have consecutive dots
    // - Domain must contain a dot
    // - TLD must be at least 2 characters
    // - Only allows valid characters in local part and domain
    private bool IsEmailValid(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        int atIndex = email.IndexOf('@');

        // Must have exactly one @, and it cannot be the first character
        if (atIndex <= 0) return false;
        if (email.IndexOf('@', atIndex + 1) >= 0) return false;

        string local = email.Substring(0, atIndex);
        string domain = email.Substring(atIndex + 1);

        // Local part: no leading/trailing dot, no consecutive dots
        if (local.StartsWith(".") || local.EndsWith(".")) return false;
        if (local.Contains("..")) return false;

        // Domain: must contain a dot, and TLD must be at least 2 characters
        int dotIndex = domain.LastIndexOf('.');
        if (dotIndex <= 0) return false;
        string tld = domain.Substring(dotIndex + 1);
        if (tld.Length < 2) return false;

        // Only allow valid characters in the local part
        foreach (char c in local)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-' && c != '+')
                return false;
        }

        // Only allow valid characters in the domain
        foreach (char c in domain)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '-')
                return false;
        }

        return true;
    }

    // Validates password meets minimum requirements:
    // - At least 8 characters
    // - At least 1 uppercase letter
    // - At least 1 lowercase letter
    // - At least 1 digit
    // - At least 1 special character
    private bool IsPasswordValid(string password)
    {
        if (password.Length < 8) return false;

        bool hasUpper = false;
        bool hasLower = false;
        bool hasDigit = false;
        bool hasSpecial = false;

        foreach (char c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else if (!char.IsLetterOrDigit(c)) hasSpecial = true;
        }

        return hasUpper && hasLower && hasDigit && hasSpecial;
    }

    // A generic function to set the status on the currently active panel
    public void SetSessionStatus(string message)
    {
        // Check Sign Session Panel
        if (convertToSignSessionPanel != null && convertToSignSessionPanel.activeSelf && sign_JoinSessionText != null)
        {
            sign_JoinSessionText.text = message;
            if (sign_CreateSessionButton != null) sign_CreateSessionButton.interactable = true;
            if (sign_JoinSessionButton != null) sign_JoinSessionButton.interactable = true;
        }
        // Check Speech Session Panel
        else if (convertToSpeechSessionPanel != null && convertToSpeechSessionPanel.activeSelf && speech_JoinSessionText != null)
        {
            speech_JoinSessionText.text = message;
            if (speech_CreateSessionButton != null) speech_CreateSessionButton.interactable = true;
            if (speech_JoinSessionButton != null) speech_JoinSessionButton.interactable = true;
        }
    }

    public void OnJoinSessionFailed(string errorMessage)
    {
        // Re-enable buttons so user can try again
        if (sign_JoinSessionButton != null) sign_JoinSessionButton.interactable = true;
        if (sign_CreateSessionButton != null) sign_CreateSessionButton.interactable = true;
        if (speech_JoinSessionButton != null) speech_JoinSessionButton.interactable = true;
        if (speech_CreateSessionButton != null) speech_CreateSessionButton.interactable = true;

        // Stop existing toast if already showing
        if (_joinErrorCoroutine != null) StopCoroutine(_joinErrorCoroutine);
        _joinErrorCoroutine = StartCoroutine(ShowJoinErrorToast(errorMessage));
    }

    private IEnumerator ShowJoinErrorToast(string errorMessage)
    {
        // Show the error on both panels
        if (signJoinErrorText != null) signJoinErrorText.text = errorMessage;
        if (speechJoinErrorText != null) speechJoinErrorText.text = errorMessage;

        // Wait 2 seconds then clear
        yield return new WaitForSeconds(2f);

        if (signJoinErrorText != null) signJoinErrorText.text = "";
        if (speechJoinErrorText != null) speechJoinErrorText.text = "";

        _joinErrorCoroutine = null;
    }

    // This is called by AppManager on success
    public void ShowRoomCode(string roomCode)
    {
        // Check Sign Session Panel
        if (convertToSignSessionPanel != null && convertToSignSessionPanel.activeSelf)
        {
            if (sign_JoinSessionInput != null) sign_JoinSessionInput.text = roomCode;
            if (sign_JoinSessionText != null) sign_JoinSessionText.text = "Session ready! Choose your input method below.";
            if (sign_CreateSessionButton != null) sign_CreateSessionButton.interactable = true;
            if (sign_JoinSessionButton != null) sign_JoinSessionButton.interactable = true;
            if (sign_JoinWithoutSessionButton != null) sign_JoinWithoutSessionButton.interactable = true;
        }
        // Check Speech Session Panel
        else if (convertToSpeechSessionPanel != null && convertToSpeechSessionPanel.activeSelf)
        {
            if (speech_JoinSessionInput != null) speech_JoinSessionInput.text = roomCode;
            if (speech_JoinSessionText != null) speech_JoinSessionText.text = "Session ready! Choose your input method below.";
            if (speech_CreateSessionButton != null) speech_CreateSessionButton.interactable = true;
            if (speech_JoinSessionButton != null) speech_JoinSessionButton.interactable = true;
            if (speech_JoinWithoutSessionButton != null) speech_JoinWithoutSessionButton.interactable = true;
        }
    }

    // Clears the login fields
    public void ClearLoginInputs()
    {
        if (loginEmailInput != null) loginEmailInput.text = "";
        if (loginPasswordInput != null) loginPasswordInput.text = "";
    }

    // Shows messages to the user (e.g., errors)
    public void ShowStatus(string message, string panelType)
    {
        if (panelType == "login" && loginStatusText != null)
        {
            loginStatusText.text = message;
        }
        else if (panelType == "register" && registerStatusText != null)
        {
            registerStatusText.text = message;
        }
        else
        {
            Debug.Log($"STATUS ({panelType}): {message}");
        }
    }

    // Sets the user's name in the Profile Panel
    public void SetProfileName(string firstName, string lastName)
    {
        if (profileUserNameText != null)
        {
            profileUserNameText.text = $"{firstName} {lastName}";
        }
    }

    // Clears the password fields in the Profile Panel
    public void ClearPasswordFields()
    {
        if (passwordInput != null) passwordInput.text = "";
        if (confirmPasswordInput != null) confirmPasswordInput.text = "";
        if (changePasswordSuccessText != null) changePasswordSuccessText.SetActive(false);
        if (changePasswordErrorText != null) changePasswordErrorText.SetActive(false);
    }

    // Shows a password error message
    public void ShowPasswordError(string message)
    {
        if (changePasswordErrorText == null) return;

        // Find the TextMeshPro component on the error object
        TMP_Text errorText = changePasswordErrorText.GetComponent<TMP_Text>();
        if (errorText != null)
        {
            errorText.text = message;
        }
        changePasswordErrorText.SetActive(true);
    }

    // Shows the password success message
    public void ShowPasswordUpdateSuccess()
    {
        ClearPasswordFields();
        if (changePasswordSuccessText != null) changePasswordSuccessText.SetActive(true);
    }
}