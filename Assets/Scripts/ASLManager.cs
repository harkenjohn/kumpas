// ============================================================
// ASLManager.cs — API-based ASL Recognition
// ============================================================
// Flow:
//   1. Record 2 seconds → collect frames
//   2. Downsample to 60 frames
//   3. Send all 60 frames to /predict/auto
//   4. Server runs CNN majority vote → if ≥75% → letter
//   5. Else server runs LSTM → phrase
//   6. Commit result to sentence
//   7. 1 second gap → repeat
//   8. Hand gone 5s → end session
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.Networking;

public class ASLManager : MonoBehaviour
{
    [Header("API")]
    [Tooltip("Your Hugging Face Space URL, no trailing slash")]
    public string apiBaseUrl = "https://kennn14-sign-language-recognition-api.hf.space";

    [Header("Camera")]
    [Tooltip("The WebCamTexture feeding the camera canvas")]
    public WebCamTexture webCamTexture;

    [Header("UI")]
    public TMP_Text predictionText;
    public TMP_Text sentenceText;
    public TMP_Text confidenceText;
    public TMP_Text statusText;

    [Header("Manager References")]
    public AppManager appManager;
    public UIManager uiManager;

    // ── Timing ────────────────────────────────────────────────
    private const float RECORD_DURATION = 2.0f;
    private const float GAP_DURATION = 1.0f;

    // ── Session end trigger ───────────────────────────────────
    private const int MAX_FAILED_CLASSIFICATIONS = 2;

    // ── Frame capture ─────────────────────────────────────────
    [Header("Frame Capture")]
    public int sendWidth = 320;
    public int sendHeight = 240;
    private const int TARGET_FRAMES = 60;

    // ── State machine ─────────────────────────────────────────
    private enum State { Recording, Gap, Classifying, SessionEnd }
    private State _state = State.Recording;

    // ── Sentence ──────────────────────────────────────────────
    private string _sentence = "";

    // ── Session ───────────────────────────────────────────────
    private bool _sessionActive = false;
    private bool _waitingForCam = false;
    private float _camWaitTimer = 0f;

    // ── Recording ─────────────────────────────────────────────
    private List<byte[]> _recordedFrames = new List<byte[]>();
    private float _recordTimer = 0f;
    private float _gapTimer = 0f;

    // ── Session end tracking ──────────────────────────────────
    private int _consecutiveFailedClassifications = 0;

    // ── Frame capture helpers ──────────────────────────────────
    private int _frameCounter = 0;

    // ── Android TTS ───────────────────────────────────────────
    private AndroidJavaObject _tts;
    private bool _ttsReady = false;

    // =========================================================
    // Unity Lifecycle
    // =========================================================
    void Start()
    {
        InitTTS();
        SetStatus("Ready");
    }

    void Update()
    {
        if (_waitingForCam)
        {
            _camWaitTimer += Time.deltaTime;
            if (_camWaitTimer >= 2.0f)
                BeginSession();
            else
                return;
        }

        if (!_sessionActive) return;
        if (_state == State.Classifying || _state == State.SessionEnd) return;

        switch (_state)
        {
            case State.Recording: RunRecording(); break;
            case State.Gap: RunGap(); break;
        }

        UpdateUI();
    }

    void OnDestroy()
    {
        _tts?.Call("shutdown");
    }

    // =========================================================
    // Session
    // =========================================================
    public void StartSession()
    {
        _sentence = "";
        _state = State.Recording;
        _sessionActive = false;
        _waitingForCam = true;
        _camWaitTimer = 0f;
        _recordTimer = 0f;
        _gapTimer = 0f;
        _consecutiveFailedClassifications = 0;
        _frameCounter = 0;
        _recordedFrames.Clear();

        if (webCamTexture == null)
        {
            RawImage[] rawImages = FindObjectsOfType<RawImage>();
            foreach (var ri in rawImages)
            {
                if (ri.texture is WebCamTexture wct)
                {
                    webCamTexture = wct;
                    Debug.Log($"[ASL] Found WebCamTexture on {ri.gameObject.name}");
                    break;
                }
            }
        }

        SetStatus("Waiting for camera…");
        Debug.Log("[ASL] Session starting...");
    }

    void BeginSession()
    {
        _waitingForCam = false;
        _sessionActive = true;
        _state = State.Recording;
        SetStatus("Recording…");
        Debug.Log("[ASL] Session started");
    }

    void EndSession()
    {
        _sessionActive = false;
        _sentence = _sentence.Trim();
        _recordedFrames.Clear();
        _state = State.SessionEnd;

        Debug.Log($"[ASL] Session ended. Sentence: '{_sentence}'");

        if (!string.IsNullOrEmpty(_sentence))
        {
            appManager?.SendTextMessage(_sentence, "TEXT_TO_SPEECH");
            SpeakSentence(_sentence);
        }

        SetStatus("Done ✅");
        uiManager?.OnASLSessionEnded();
    }

    // =========================================================
    // State: Recording
    // =========================================================
    void RunRecording()
    {
        _recordTimer += Time.deltaTime;

        // Capture frame directly from webCamTexture
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            var fullTex = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
            fullTex.SetPixels(webCamTexture.GetPixels());
            fullTex.Apply();

            var rt = RenderTexture.GetTemporary(sendWidth, sendHeight, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(fullTex, rt);
            RenderTexture.active = rt;

            var resizedFrame = new Texture2D(sendWidth, sendHeight, TextureFormat.RGB24, false);
            resizedFrame.ReadPixels(new Rect(0, 0, sendWidth, sendHeight), 0, 0);
            resizedFrame.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(fullTex);

            _recordedFrames.Add(resizedFrame.EncodeToJPG(60));
            Destroy(resizedFrame);
        }

        SetStatus($"Recording… {_recordTimer:F1}s");

        if (_recordTimer >= RECORD_DURATION)
        {
            Debug.Log($"[ASL] 2s recorded. {_recordedFrames.Count} raw frames → processing");
            _recordTimer = 0f;
            _state = State.Classifying;
            StartCoroutine(ProcessRecording(_recordedFrames));
            _recordedFrames = new List<byte[]>();
        }
    }

    // =========================================================
    // State: Gap
    // =========================================================
    void RunGap()
    {
        _gapTimer += Time.deltaTime;
        SetStatus($"Gap… {_gapTimer:F1}s");

        if (_gapTimer >= GAP_DURATION)
        {
            _gapTimer = 0f;
            _state = State.Recording;
            SetStatus("Recording…");
            Debug.Log("[ASL] Gap done → Recording");
        }
    }

    // =========================================================
    // Processing Pipeline
    // =========================================================
    IEnumerator ProcessRecording(List<byte[]> rawFrames)
    {
        // Downsample to 60 frames evenly
        List<byte[]> frames60 = SampleFrames(rawFrames, TARGET_FRAMES);
        Debug.Log($"[ASL] Downsampled to {frames60.Count} frames → sending to /predict/auto");

        SetStatus("Classifying…");
        yield return StartCoroutine(SendAutoFrames(frames60));

        // Go to gap after classification
        _state = State.Gap;
        _gapTimer = 0f;
    }

    // =========================================================
    // Frame Sampling
    // =========================================================
    List<byte[]> SampleFrames(List<byte[]> frames, int targetCount)
    {
        if (frames.Count == 0) return new List<byte[]>();
        if (frames.Count <= targetCount) return new List<byte[]>(frames);

        var sampled = new List<byte[]>();
        for (int i = 0; i < targetCount; i++)
        {
            int idx = Mathf.RoundToInt(i * (frames.Count - 1) / (float)(targetCount - 1));
            sampled.Add(frames[Mathf.Clamp(idx, 0, frames.Count - 1)]);
        }
        return sampled;
    }

    // =========================================================
    // API Call — /predict/auto
    // =========================================================
    IEnumerator SendAutoFrames(List<byte[]> frames)
    {
        if (frames.Count == 0)
        {
            Debug.Log("[ASL] No frames to send — skipping");
            yield break;
        }

        WWWForm form = new WWWForm();
        for (int i = 0; i < frames.Count; i++)
            form.AddBinaryData("files", frames[i], $"frame_{i:D4}.jpg", "image/jpeg");

        using var req = UnityWebRequest.Post($"{apiBaseUrl}/predict/auto", form);
        req.timeout = 60;
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[ASL] Auto API error: {req.error}");
            SetStatus("API error — check connection");

            // Treat API error as failed classification
            _consecutiveFailedClassifications++;
            Debug.Log($"[ASL] Failed classification count: {_consecutiveFailedClassifications}/{MAX_FAILED_CLASSIFICATIONS}");

            if (_consecutiveFailedClassifications >= MAX_FAILED_CLASSIFICATIONS)
            {
                Debug.Log("[ASL] 2 consecutive failed classifications → ending session");
                EndSession();
            }
        }
        else
        {
            var response = JsonUtility.FromJson<AutoResponse>(req.downloadHandler.text);

            if (response.detected && !string.IsNullOrEmpty(response.result))
            {
                // SUCCESS — reset counter and append result
                _consecutiveFailedClassifications = 0;
                AppendToSentence(response.result);

                if (response.result_type == "letter")
                {
                    SetStatus($"Letter: {response.result} ({response.vote_ratio * 100f:F0}% vote)");
                    Debug.Log($"[ASL] ✓ Letter: {response.result} ({response.vote_ratio * 100f:F0}%)");
                }
                else
                {
                    SetStatus($"Phrase: {response.result} ({response.confidence * 100f:F0}%)");
                    Debug.Log($"[ASL] ✓ Phrase: {response.result} ({response.confidence * 100f:F0}%)");
                }
            }
            else
            {
                // FAILED — increment counter
                _consecutiveFailedClassifications++;
                SetStatus("Not recognized");
                Debug.Log($"[ASL] Nothing recognized this window. Failed count: {_consecutiveFailedClassifications}/{MAX_FAILED_CLASSIFICATIONS}");

                if (_consecutiveFailedClassifications >= MAX_FAILED_CLASSIFICATIONS)
                {
                    Debug.Log("[ASL] 2 consecutive failed classifications → ending session");
                    EndSession();
                }
            }
        }
    }

    // =========================================================
    // Sentence
    // =========================================================
    void AppendToSentence(string word)
    {
        if (_sentence.Length > 0 && !_sentence.EndsWith(" "))
            _sentence += " ";
        _sentence += word;
        Debug.Log($"[ASL] Sentence so far: '{_sentence}'");
    }

    // =========================================================
    // Android TTS
    // =========================================================
    void InitTTS()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            AndroidJavaClass  unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity    = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaProxy  listener    = new TTSInitListener(() => { _ttsReady = true; });
            _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, listener);
        }
        catch (Exception e) { Debug.LogError($"[ASL] TTS init failed: {e.Message}"); }
#else
        _ttsReady = true;
#endif
    }

    void SpeakSentence(string sentence)
    {
        string spokenText = sentence.ToLower();
        Debug.Log($"[ASL] Speaking: '{spokenText}'");
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_ttsReady && _tts != null)
            _tts.Call<int>("speak", spokenText, 0, null, null);
#else
        Debug.Log($"[ASL] Editor TTS: '{spokenText}'");
#endif
    }

    // =========================================================
    // UI
    // =========================================================
    void UpdateUI()
    {
        if (sentenceText != null)
            sentenceText.text = string.IsNullOrEmpty(_sentence) ? "_" : _sentence;
    }

    void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log($"[ASL] {msg}");
    }
}

// ============================================================
// API Response Models
// ============================================================
[Serializable]
public class AutoResponse
{
    public string result;
    public string result_type;
    public float confidence;
    public float vote_ratio;
    public bool detected;
}

[Serializable]
public class AlphabetResponse
{
    public string letter;
    public float confidence;
    public bool detected;
}

[Serializable]
public class PhraseResponse
{
    public string phrase;
    public float confidence;
    public bool detected;
}

[Serializable]
public class BatchAlphabetResponse
{
    public string letter;
    public float vote_ratio;
    public bool detected;
}

// ============================================================
// Android TTS Listener
// ============================================================
#if UNITY_ANDROID && !UNITY_EDITOR
public class TTSInitListener : AndroidJavaProxy
{
    private System.Action _onReady;
    public TTSInitListener(System.Action onReady)
        : base("android.speech.tts.TextToSpeech$OnInitListener") { _onReady = onReady; }
    public void onInit(int status)
    {
        if (status == 0) _onReady?.Invoke();
        else UnityEngine.Debug.LogError("[ASL] TTS init failed: " + status);
    }
}
#endif