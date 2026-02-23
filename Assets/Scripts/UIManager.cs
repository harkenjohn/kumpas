using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Kumpas.Models;
using System.Collections.Generic;

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

    // --- NEW TEXT INPUT PANELS ---
    [Header("Text Input Panels")]
    public GameObject textToSpeechInputPanel; // For Sign User to type
    public GameObject textToSignInputPanel;   // For Speech User to type

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

        // Always disable MediaPipe and camera feed when hiding all panels
        if (mediaPipeSolution != null) mediaPipeSolution.SetActive(false);
        if (cameraFeedContainer != null) cameraFeedContainer.SetActive(false);
    }

    // --- HELPER: SHOWS CAMERA FULLSCREEN (hides all app UI) ---
    public void ShowCameraFullscreen()
    {
        // Hide the entire app UI
        if (uiRoot != null) uiRoot.SetActive(false);

        // Show camera feed and enable MediaPipe
        if (cameraFeedContainer != null) cameraFeedContainer.SetActive(true);
        if (mediaPipeSolution != null) mediaPipeSolution.SetActive(true);

        Debug.Log("[UIManager] Camera fullscreen shown - all UI hidden, MediaPipe enabled");
    }

    // --- HELPER: HIDES CAMERA, RESTORES APP UI ---
    public void HideCameraFullscreen()
    {
        // Hide camera feed and disable MediaPipe
        if (cameraFeedContainer != null) cameraFeedContainer.SetActive(false);
        if (mediaPipeSolution != null) mediaPipeSolution.SetActive(false);

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

        // 3. Check if passwords match
        if (password != confirmPassword)
        {
            ShowStatus("Passwords do not match!", "register");
            return; // Stop the function here
        }

        // 4. Pass the data to the AppManager to handle
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

    // AudioInputMethodPanel: Text Input Button (Speech User) -> Text to Sign Panel
    public void OnShowTextToSignInputButton()
    {
        if (appManager == null) return;
        appManager.ChangeState(AppManager.AppState.TextToSignInput);
    }

    // TextToSpeechInputPanel: Back Button (Returns to Camera Input)
    public void OnBackToCameraInputButton()
    {
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
        if (appManager == null) return;

        string message = textToSignInput.text;
        if (string.IsNullOrWhiteSpace(message)) return;

        // Speech user typed text → Send as TEXT_TO_SIGN (triggers camera!)
        appManager.SendTextMessage(message.Trim(), "TEXT_TO_SIGN");
        textToSignInput.text = "";

        Debug.Log("[UIManager] Sent TEXT_TO_SIGN message - Camera should trigger for partner");
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