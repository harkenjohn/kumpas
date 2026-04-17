using UnityEngine;
using UnityEngine.UI;

/*
 * TO SPEECH QUICK CHAT MANAGER
 *
 * WHAT IT DOES:
 * - Handles all pre-set phrase buttons in the ToSpeechQuickChatPanel.
 * - When a button is pressed, it sends the phrase as TEXT_TO_SPEECH
 *   to the partner via Supabase AND speaks it locally via TTS.
 *
 * HOW TO SET UP IN UNITY:
 * 1. Attach this script to the ToSpeechQuickChatPanel GameObject.
 * 2. Assign the AppManager reference in the Inspector.
 * 3. Wire each button's OnClick() to the corresponding method below.
 */
public class ToSpeechQuickChatManager : MonoBehaviour
{
    [Header("Manager References")]
    public AppManager appManager;

    // =========================================================
    // Core Send Function
    // =========================================================

    private void SendPhrase(string phrase)
    {
        if (appManager == null)
        {
            Debug.LogError("[ToSpeechQuickChat] AppManager reference not set!");
            return;
        }

        Debug.Log($"[ToSpeechQuickChat] Sending phrase: '{phrase}'");
        appManager.SendTextMessage(phrase, "TEXT_TO_SPEECH");
        appManager.SpeakText(phrase);
    }

    // =========================================================
    // Greetings
    // =========================================================

    public void OnGoodMorning() => SendPhrase("Good Morning");
    public void OnMagandangAraw() => SendPhrase("Magandang Araw");
    public void OnGoodAfternoon() => SendPhrase("Good Afternoon");
    public void OnMagandangHapon() => SendPhrase("Magandang Hapon");
    public void OnGoodEvening() => SendPhrase("Good Evening");

    // =========================================================
    // Inquiries & Well-Being
    // =========================================================

    public void OnHowAreYou() => SendPhrase("How are you?");
    public void OnKumustaKa() => SendPhrase("Kumusta ka?");
    public void OnImFine() => SendPhrase("I'm Fine");

    // =========================================================
    // Polite Expressions
    // =========================================================

    public void OnThankYou() => SendPhrase("Thank You");
    public void OnYoureWelcome() => SendPhrase("You're Welcome");
    public void OnWalangAnuman() => SendPhrase("Walang Anuman");

    // =========================================================
    // Basic Responses
    // =========================================================

    public void OnYes() => SendPhrase("Yes");
    public void OnNo() => SendPhrase("No");
}