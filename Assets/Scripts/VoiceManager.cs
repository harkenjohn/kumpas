using UnityEngine;
using TMPro;
using System.Collections;

public class VoiceManager : MonoBehaviour
{
    public TMP_Text transcriptionText;
    public GameObject sentCheckmark;
    private AppManager appManager;

    private string finalText = "";

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject speechRecognizer;
    private AndroidJavaObject recognizerIntent;
    private AndroidJavaObject activity;
    private bool isInitialized = false;
#endif

    void Start()
    {
        appManager = FindFirstObjectByType<AppManager>();

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            // Run initialization on UI thread and WAIT for it
            bool done = false;
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                AndroidJavaClass src = new AndroidJavaClass("android.speech.SpeechRecognizer");
                speechRecognizer = src.CallStatic<AndroidJavaObject>("createSpeechRecognizer", activity);

                recognizerIntent = new AndroidJavaObject("android.content.Intent", "android.speech.action.RECOGNIZE_SPEECH");
                recognizerIntent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.LANGUAGE_MODEL", "free_form");
                recognizerIntent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.PARTIAL_RESULTS", true);

                recognizerIntent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.LANGUAGE", "fil-PH");
                recognizerIntent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.LANGUAGE_PREFERENCE", "fil-PH");
                recognizerIntent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.ONLY_RETURN_LANGUAGE_PREFERENCE", false);

                speechRecognizer.Call("setRecognitionListener", new SpeechListener(this));
                isInitialized = true;
                done = true;
            }));

            // Wait until UI thread finishes
            float timeout = 5f;
            while (!done && timeout > 0)
            {
                System.Threading.Thread.Sleep(50);
                timeout -= 0.05f;
            }

            Debug.Log("VoiceManager initialized: " + isInitialized);
        }
        catch (System.Exception e)
        {
            Debug.LogError("VoiceManager Init Error: " + e.Message);
        }
#endif
    }

    public void StartRecording()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized)
        {
            Debug.LogError("SpeechRecognizer not initialized yet!");
            return;
        }

        if (transcriptionText != null)
            transcriptionText.text = "Listening...";
        finalText = "";

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            speechRecognizer.Call("startListening", recognizerIntent);
        }));
#endif
    }

    public void StopRecording()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized) return;

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            speechRecognizer.Call("stopListening");
        }));
#endif

        if (!string.IsNullOrEmpty(finalText))
            StartCoroutine(SendToDatabase(finalText));
    }

    public void OnSpeechResult(string result)
    {
        finalText = result;
        if (transcriptionText != null)
            transcriptionText.text = result;
    }

    public void OnPartialResult(string partial)
    {
        if (transcriptionText != null)
            transcriptionText.text = partial;
    }

    IEnumerator SendToDatabase(string message)
    {
        if (appManager == null)
        {
            Debug.LogError("AppManager not found!");
            yield break;
        }

        // THIS LINE SENDS TO SUPABASE
        appManager.SendTextMessage(message.Trim(), "TEXT_TO_SIGN");

        Debug.Log("Voice message sent to Supabase: " + message);

        // UI feedback
        if (sentCheckmark != null)
            sentCheckmark.SetActive(true);

        yield return new WaitForSeconds(3f);

        if (sentCheckmark != null)
            sentCheckmark.SetActive(false);
    }
}