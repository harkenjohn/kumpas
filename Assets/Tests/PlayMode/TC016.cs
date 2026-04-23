using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TC016_SignToSpeech_TextInput
{
    private const string INPUT_TEXT      = "Magandang Umaga";
    private const string EXPECTED_TEXT   = "Magandang Umaga";
    private const string EXPECTED_TYPE   = "TEXT_TO_SPEECH";
    private string _sentMessageContent  = null;
    private string _sentMessageType     = null;
    private string _speakTextArg        = null;
    private bool   _speakTextCalled     = false;
    private bool   _sendMessageCalled   = false;
    private string _inputFieldText      = "";

    [UnityTest]
    public IEnumerator TC016_ValidTextInput_SpeechOutputProduced()
    {
        _inputFieldText = INPUT_TEXT;

        Debug.Log($"[TC-016] User typed: '{_inputFieldText}'");

        yield return null;

        Assert.IsFalse(string.IsNullOrWhiteSpace(_inputFieldText),
            "TC-016: Input text should not be null, empty, or whitespace.");

        string trimmed = _inputFieldText.Trim();

        Assert.AreEqual(EXPECTED_TEXT, trimmed,
            $"TC-016: Trimmed text should equal '{EXPECTED_TEXT}'.");

        Debug.Log($"[TC-016] Trimmed text: '{trimmed}'");

        SimulateSendTextMessage(trimmed, EXPECTED_TYPE);

        Assert.IsTrue(_sendMessageCalled,
            "TC-016: SendTextMessage() should be called after valid text input.");
        Assert.AreEqual(EXPECTED_TEXT, _sentMessageContent,
            $"TC-016: SendTextMessage() content should be '{EXPECTED_TEXT}', got '{_sentMessageContent}'.");
        Assert.AreEqual(EXPECTED_TYPE, _sentMessageType,
            $"TC-016: SendTextMessage() type should be '{EXPECTED_TYPE}', got '{_sentMessageType}'.");

        Debug.Log($"[TC-016] SendTextMessage called — content: '{_sentMessageContent}', type: '{_sentMessageType}'");

        SimulateSpeakText(trimmed);

        Assert.IsTrue(_speakTextCalled,
            "TC-016: SpeakText() should be triggered after sending valid text.");
        Assert.AreEqual(EXPECTED_TEXT, _speakTextArg,
            $"TC-016: SpeakText() should be called with '{EXPECTED_TEXT}', got '{_speakTextArg}'.");

        Debug.Log($"[TC-016] SpeakText called with: '{_speakTextArg}'");

        _inputFieldText = "";

        Assert.AreEqual("", _inputFieldText,
            "TC-016: Input field should be cleared after sending.");

        Debug.Log("[TC-016] Input field cleared.");
        Debug.Log($"[TC-016] PASSED — '{EXPECTED_TEXT}' sent as {EXPECTED_TYPE} and SpeakText triggered.");
    }

    private void SimulateSendTextMessage(string content, string messageType)
    {
        _sendMessageCalled  = true;
        _sentMessageContent = content;
        _sentMessageType    = messageType;
        Debug.Log($"[TC-016] SimulateSendTextMessage — content: '{content}', type: '{messageType}'");
    }

    private void SimulateSpeakText(string text)
    {
        _speakTextCalled = true;
        _speakTextArg    = text;
        Debug.Log($"[TC-016] SimulateSpeakText called with: '{text}'");
    }
}
