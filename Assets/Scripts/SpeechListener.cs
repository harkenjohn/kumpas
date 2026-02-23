using UnityEngine;
using System.Collections.Generic;

public class SpeechListener : AndroidJavaProxy
{
    VoiceManager manager;

    public SpeechListener(VoiceManager manager) 
        : base("android.speech.RecognitionListener")
    {
        this.manager = manager;
    }

    public void onResults(AndroidJavaObject results)
    {
        var matches = results.Call<AndroidJavaObject>("getStringArrayList", "results_recognition");
        if (matches != null)
        {
            string result = matches.Call<string>("get", 0);
            manager.OnSpeechResult(result);
        }
    }

    public void onPartialResults(AndroidJavaObject results)
    {
        var matches = results.Call<AndroidJavaObject>("getStringArrayList", "results_recognition");
        if (matches != null)
        {
            string partial = matches.Call<string>("get", 0);
            manager.OnPartialResult(partial);  // live update
        }
    }

    public void onReadyForSpeech(AndroidJavaObject bundle) { }
    public void onBeginningOfSpeech() { }
    public void onRmsChanged(float rmsdB) { }
    public void onBufferReceived(byte[] buffer) { }
    public void onEndOfSpeech() { }
    public void onError(int error) { Debug.LogError("SpeechRecognizer error: " + error); }
    public void onEvent(int eventType, AndroidJavaObject bundle) { }
}