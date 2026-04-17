using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/*
 * QUICK CHAT MANAGER
 *
 * Handles the Quick Chat phrase panel (toSpeechQuickChatPanel).
 * When a phrase button is clicked, it:
 *   1. Sends the phrase to Supabase via AppManager.SendTextMessage()
 *      as TEXT_TO_SIGN — this triggers the ASL camera on the receiver.
 *   2. Shows a "Sent!" confirmation toast on the sender's screen.
 *
 * HOW TO WIRE IN INSPECTOR:
 *   - Attach this script to your toSpeechQuickChatPanel GameObject.
 *   - Assign the sentToastObject and sentToastText fields below.
 *   - For EACH phrase button, set its OnClick() to:
 *       QuickChatManager.OnPhraseButtonClicked  →  pass the phrase string
 *     OR use the individual named wrappers at the bottom of this file.
 */
public class QuickChatManager : MonoBehaviour
{
    [Header("Sent Toast UI")]
    [Tooltip("The GameObject that contains the 'Sent!' feedback (e.g. a panel with text).")]
    public GameObject sentToastObject;

    [Tooltip("The TMP_Text inside the sent toast — shows which phrase was sent.")]
    public TMP_Text sentToastText;

    [Tooltip("How long (seconds) the toast stays visible before fading.")]
    public float toastDuration = 2f;

    // Internal reference — found automatically at runtime
    private AppManager appManager;
    private Coroutine toastCoroutine;

    // -------------------------------------------------------------------------
    void Start()
    {
        appManager = FindFirstObjectByType<AppManager>();

        if (appManager == null)
            Debug.LogError("[QuickChatManager] AppManager not found in scene!");

        // Make sure toast is hidden at start
        if (sentToastObject != null)
            sentToastObject.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // CORE SEND METHOD
    // Called by every phrase button via OnClick (pass the phrase as a string arg)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends the phrase to Supabase as TEXT_TO_SIGN and shows a sent toast.
    /// Wire each Button's OnClick → QuickChatManager.OnPhraseButtonClicked
    /// and type the phrase text in the string argument field in the Inspector.
    /// </summary>
    public void OnPhraseButtonClicked(string phrase)
    {
        if (appManager == null)
        {
            Debug.LogError("[QuickChatManager] Cannot send — AppManager is null.");
            return;
        }

        if (string.IsNullOrWhiteSpace(phrase))
        {
            Debug.LogWarning("[QuickChatManager] Phrase is empty, nothing sent.");
            return;
        }

        string trimmedPhrase = phrase.Trim();

        // Send to Supabase — this triggers the ASL camera on the receiver's device
        appManager.SendTextMessage(trimmedPhrase, "TEXT_TO_SIGN");

        Debug.Log($"[QuickChatManager] Sent phrase: '{trimmedPhrase}' as TEXT_TO_SIGN");

        // Show "Sent!" toast feedback to the sender
        ShowSentToast(trimmedPhrase);
    }

    // -------------------------------------------------------------------------
    // TOAST FEEDBACK
    // -------------------------------------------------------------------------

    private void ShowSentToast(string phrase)
    {
        if (sentToastObject == null) return;

        // Update toast text
        if (sentToastText != null)
            sentToastText.text = $"✓  \"{phrase}\" sent!";

        // Cancel previous toast if still visible
        if (toastCoroutine != null)
            StopCoroutine(toastCoroutine);

        toastCoroutine = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        sentToastObject.SetActive(true);
        yield return new WaitForSeconds(toastDuration);
        sentToastObject.SetActive(false);
        toastCoroutine = null;
    }

    // -------------------------------------------------------------------------
    // NAMED WRAPPERS — one per phrase button
    // Use these on OnClick() if you prefer named methods over passing strings.
    //
    // GREETINGS
    // -------------------------------------------------------------------------

    public void SendGoodMorning()       => OnPhraseButtonClicked("Good Morning");
    public void SendMagandangAraw()     => OnPhraseButtonClicked("Magandang Araw");
    public void SendGoodAfternoon()     => OnPhraseButtonClicked("Good Afternoon");
    public void SendMagandangHapon()    => OnPhraseButtonClicked("Magandang Hapon");
    public void SendGoodEvening()       => OnPhraseButtonClicked("Good Evening");

    // INQUIRIES & WELL-BEING
    public void SendHowAreYou()         => OnPhraseButtonClicked("How Are You");
    public void SendKumustaKa()         => OnPhraseButtonClicked("Kumusta Ka");
    public void SendImFine()            => OnPhraseButtonClicked("I'm Fine");

    // POLITE EXPRESSIONS
    public void SendThankYou()          => OnPhraseButtonClicked("Thank You");
    public void SendYoureWelcome()      => OnPhraseButtonClicked("You're Welcome");
    public void SendWalangAnuman()      => OnPhraseButtonClicked("Walang Anuman");

    // BASIC RESPONSES
    public void SendYes()               => OnPhraseButtonClicked("Yes");
    public void SendNo()                => OnPhraseButtonClicked("No");
}