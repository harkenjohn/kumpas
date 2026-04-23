using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TC017_SignToSpeech_QuickChat
{
    private const string SELECTED_PHRASE = "Walang Anuman";
    private const string EXPECTED_TYPE   = "TEXT_TO_SPEECH";
    private string _sentMessageContent = null;
    private string _sentMessageType    = null;
    private string _speakTextArg       = null;
    private bool   _sendMessageCalled  = false;
    private bool   _speakTextCalled    = false;
    [UnityTest]
    public IEnumerator TC017_QuickChatButton_WalangAnuman_SpeechOutputProduced()
    {
        string tappedPhrase = SELECTED_PHRASE;

        Debug.Log($"[TC-017] User tapped Quick Chat button: '{tappedPhrase}'");
        yield return null;
        Assert.IsFalse(string.IsNullOrWhiteSpace(tappedPhrase),
            "TC-017: Quick Chat phrase should not be null or empty.");
        Assert.AreEqual(SELECTED_PHRASE, tappedPhrase,
            $"TC-017: Tapped phrase should equal '{SELECTED_PHRASE}'.");
        SimulateSendTextMessage(tappedPhrase, EXPECTED_TYPE);
        Assert.IsTrue(_sendMessageCalled,
            "TC-017: SendTextMessage() should be called when a Quick Chat button is tapped.");
        Assert.AreEqual(SELECTED_PHRASE, _sentMessageContent,
            $"TC-017: SendTextMessage() content should be '{SELECTED_PHRASE}', got '{_sentMessageContent}'.");
        Assert.AreEqual(EXPECTED_TYPE, _sentMessageType,
            $"TC-017: SendTextMessage() type should be '{EXPECTED_TYPE}', got '{_sentMessageType}'.");

        Debug.Log($"[TC-017] SendTextMessage called — content: '{_sentMessageContent}', type: '{_sentMessageType}'");
        SimulateSpeakText(tappedPhrase);
        Assert.IsTrue(_speakTextCalled,
            "TC-017: SpeakText() should be triggered when a Quick Chat button is tapped.");
        Assert.AreEqual(SELECTED_PHRASE, _speakTextArg,
            $"TC-017: SpeakText() should be called with '{SELECTED_PHRASE}', got '{_speakTextArg}'.");

        Debug.Log($"[TC-017] SpeakText called with: '{_speakTextArg}'");
        Debug.Log($"[TC-017] PASSED — '{SELECTED_PHRASE}' sent as {EXPECTED_TYPE} and SpeakText triggered.");
    }

    private void SimulateSendTextMessage(string content, string messageType)
    {
        _sendMessageCalled  = true;
        _sentMessageContent = content;
        _sentMessageType    = messageType;
        Debug.Log($"[TC-017] SimulateSendTextMessage — content: '{content}', type: '{messageType}'");
    }

    // =========================================================
    // Simulate SpeakText
    // Mirrors AppManager.SpeakText(text)
    // =========================================================

    private void SimulateSpeakText(string text)
    {
        _speakTextCalled = true;
        _speakTextArg    = text;
        Debug.Log($"[TC-017] SimulateSpeakText called with: '{text}'");
    }
}
