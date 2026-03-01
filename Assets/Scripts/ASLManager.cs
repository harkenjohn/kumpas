// ============================================================
// ASLManager.cs — Unity 6 + Inference Engine 2.3.0
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.InferenceEngine;

public class ASLManager : MonoBehaviour
{
    // ── Inspector Fields ──────────────────────────────────────
    [Header("Inference Engine Model")]
    [Tooltip("Drag asl_model.onnx here from your Assets folder")]
    public ModelAsset modelAsset;

    [Header("UI")]
    public TMP_Text predictionText;
    public TMP_Text sentenceText;
    public TMP_Text confidenceText;
    public TMP_Text statusText;

    [Header("Manager References")]
    public AppManager appManager;
    public UIManager uiManager;
    public HandLandmarkBridge handLandmarkBridge;

    // ── Timing constants ──────────────────────────────────────
    private const float LETTER_HOLD_TIME = 1.5f;
    private const float SPACE_THRESHOLD = 1.5f;
    private const float SESSION_END_TIME = 3.0f;

    // ── Class names ───────────────────────────────────────────
    private readonly string[] CLASS_NAMES =
    {
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "K",
        "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U",
        "V", "W", "X", "Y"
    };

    private const int IMG_SIZE = 64;
    private const int BUFFER_SIZE = 15;

    // ── Inference Engine ──────────────────────────────────────
    private Model _runtimeModel;
    private Worker _worker;

    // ── Prediction state ──────────────────────────────────────
    private string _stableLetter = "";
    private float _stableConf = 0f;
    private Queue<string> _predBuffer = new Queue<string>();

    // ── Timing state ──────────────────────────────────────────
    private string _currentHeldLetter = "";
    private float _letterHoldTimer = 0f;
    private bool _letterCommittedThisHold = false;
    private float _noDetectionTimer = 0f;
    private bool _spaceInserted = false;

    // ── Sentence ──────────────────────────────────────────────
    private string _sentence = "";

    // ── Session ───────────────────────────────────────────────
    private bool _sessionActive = false;
    private bool _waitingForCamera = false;
    private float _cameraWaitTimer = 0f;

    // ── Android TTS ───────────────────────────────────────────
    private AndroidJavaObject _tts;
    private bool _ttsReady = false;

    // =========================================================
    // Unity Lifecycle
    // =========================================================
    void Start()
    {
        InitInferenceEngine();
        InitTTS();
        SetStatus("Ready");
    }

    void Update()
    {
        if (_waitingForCamera)
        {
            _cameraWaitTimer += Time.deltaTime;
            if (_cameraWaitTimer >= 3.0f)
                BeginSession();
            else
                return;
        }

        if (_sessionActive)
            RunPipeline();

        if (_sessionActive)
            UpdateTimers();

        UpdateUI();
    }

    void OnDestroy()
    {
        _worker?.Dispose();
        _tts?.Call("shutdown");
    }

    // =========================================================
    // Inference Engine Init
    // =========================================================
    void InitInferenceEngine()
    {
        _runtimeModel = ModelLoader.Load(modelAsset);
        _worker = new Worker(_runtimeModel, BackendType.GPUCompute);
        Debug.Log("[ASL] Model loaded");
    }

    // =========================================================
    // Session
    // =========================================================
    public void StartSession()
    {
        _sentence = "";
        _currentHeldLetter = "";
        _letterHoldTimer = 0f;
        _letterCommittedThisHold = false;
        _noDetectionTimer = 0f;
        _spaceInserted = false;
        _sessionActive = false;
        _waitingForCamera = true;
        _cameraWaitTimer = 0f;
        SetStatus("Waiting for camera…");
        Debug.Log("[ASL] Waiting for MediaPipe camera to start...");
    }

    void BeginSession()
    {
        _waitingForCamera = false;
        _sessionActive = true;
        SetStatus("Scanning…");
        Debug.Log("[ASL] Session started");
    }

    void EndSession()
    {
        _sessionActive = false;
        _sentence = _sentence.Trim();
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
    // Timer Logic
    // =========================================================
    void UpdateTimers()
    {
        bool signDetected = !string.IsNullOrEmpty(_stableLetter);

        if (signDetected)
        {
            _noDetectionTimer = 0f;
            _spaceInserted = false;

            if (_stableLetter == _currentHeldLetter)
            {
                _letterHoldTimer += Time.deltaTime;

                if (_letterHoldTimer >= LETTER_HOLD_TIME && !_letterCommittedThisHold)
                {
                    _sentence += _stableLetter;
                    _letterCommittedThisHold = true;
                    Debug.Log($"[ASL] Letter: '{_stableLetter}' → '{_sentence}'");
                    SetStatus($"Letter: {_stableLetter}");
                }
            }
            else
            {
                _currentHeldLetter = _stableLetter;
                _letterHoldTimer = 0f;
                _letterCommittedThisHold = false;
            }
        }
        else
        {
            _currentHeldLetter = "";
            _letterHoldTimer = 0f;
            _letterCommittedThisHold = false;
            _noDetectionTimer += Time.deltaTime;

            if (_noDetectionTimer >= SPACE_THRESHOLD && !_spaceInserted)
            {
                if (_sentence.Length > 0 && !_sentence.EndsWith(" "))
                {
                    _sentence += " ";
                    _spaceInserted = true;
                    Debug.Log($"[ASL] Space → '{_sentence}'");
                    SetStatus("Pause — space added");
                }
            }

            if (_noDetectionTimer >= SESSION_END_TIME)
                EndSession();
        }
    }

    // =========================================================
    // Main Pipeline
    // =========================================================
    void RunPipeline()
    {
        Vector2[] landmarks = GetHandLandmarks();

        if (landmarks == null || landmarks.Length < 21)
        {
            _stableLetter = "";
            _stableConf = 0f;
            return;
        }

        Texture2D skeleton = RenderSkeleton(landmarks, IMG_SIZE);
        (string letter, float conf) = Predict(skeleton);
        Destroy(skeleton);

        _predBuffer.Enqueue(letter);
        if (_predBuffer.Count > BUFFER_SIZE) _predBuffer.Dequeue();

        var votes = new Dictionary<string, int>();
        foreach (string p in _predBuffer)
        {
            if (!votes.ContainsKey(p)) votes[p] = 0;
            votes[p]++;
        }

        string bestLetter = letter;
        int bestVotes = 0;
        foreach (var kv in votes)
        {
            if (kv.Value > bestVotes) { bestVotes = kv.Value; bestLetter = kv.Key; }
        }

        _stableLetter = bestLetter;
        _stableConf = conf;
    }

    // =========================================================
    // CNN Inference — Inference Engine 2.3.0
    // =========================================================
    (string letter, float conf) Predict(Texture2D skeleton)
    {
        Color[] pixels = skeleton.GetPixels();
        float[] data = new float[3 * IMG_SIZE * IMG_SIZE];

        for (int y = 0; y < IMG_SIZE; y++)
            for (int x = 0; x < IMG_SIZE; x++)
            {
                Color c = pixels[y * IMG_SIZE + x];
                int idx = y * IMG_SIZE + x;
                data[0 * IMG_SIZE * IMG_SIZE + idx] = c.r * 2f - 1f;
                data[1 * IMG_SIZE * IMG_SIZE + idx] = c.g * 2f - 1f;
                data[2 * IMG_SIZE * IMG_SIZE + idx] = c.b * 2f - 1f;
            }

        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, IMG_SIZE, IMG_SIZE), data);
        _worker.Schedule(inputTensor);

        Tensor<float> outputTensor = _worker.PeekOutput() as Tensor<float>;
        float[] logits = outputTensor.DownloadToArray();

        // Softmax
        float maxVal = float.MinValue;
        foreach (float v in logits) if (v > maxVal) maxVal = v;

        float sum = 0f;
        float[] probs = new float[CLASS_NAMES.Length];
        for (int i = 0; i < CLASS_NAMES.Length; i++)
        {
            probs[i] = Mathf.Exp(logits[i] - maxVal);
            sum += probs[i];
        }
        for (int i = 0; i < probs.Length; i++) probs[i] /= sum;

        int bestIdx = 0;
        float bestProb = 0f;
        for (int i = 0; i < probs.Length; i++)
        {
            if (probs[i] > bestProb) { bestProb = probs[i]; bestIdx = i; }
        }

        return (CLASS_NAMES[bestIdx], bestProb);
    }

    // =========================================================
    // Skeleton Renderer
    // =========================================================
    private readonly (int, int)[] HAND_CONNECTIONS =
    {
        (0,1),(1,2),(2,3),(3,4),
        (0,5),(5,6),(6,7),(7,8),
        (5,9),(9,10),(10,11),(11,12),
        (9,13),(13,14),(14,15),(15,16),
        (13,17),(0,17),(17,18),(18,19),(19,20)
    };

    Texture2D RenderSkeleton(Vector2[] landmarks, int size)
    {
        Texture2D canvas = new Texture2D(size, size, TextureFormat.RGB24, false);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.black;
        canvas.SetPixels(pixels);

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (Vector2 lm in landmarks)
        {
            minX = Mathf.Min(minX, lm.x); minY = Mathf.Min(minY, lm.y);
            maxX = Mathf.Max(maxX, lm.x); maxY = Mathf.Max(maxY, lm.y);
        }

        float pad = 0.15f;
        minX = Mathf.Max(0f, minX - pad); minY = Mathf.Max(0f, minY - pad);
        maxX = Mathf.Min(1f, maxX + pad); maxY = Mathf.Min(1f, maxY + pad);
        float rx = (maxX > minX) ? maxX - minX : 1f;
        float ry = (maxY > minY) ? maxY - minY : 1f;

        Vector2Int ToPx(Vector2 lm)
        {
            int cx = Mathf.Clamp((int)(((lm.x - minX) / rx) * (size - 1)), 0, size - 1);
            int cy = Mathf.Clamp((int)(((lm.y - minY) / ry) * (size - 1)), 0, size - 1);
            return new Vector2Int(cx, cy);
        }

        Color boneColor = new Color(200f / 255f, 200f / 255f, 200f / 255f);
        Color tipColor = Color.white;
        Color jointColor = new Color(180f / 255f, 180f / 255f, 180f / 255f);
        HashSet<int> tips = new HashSet<int> { 4, 8, 12, 16, 20 };

        foreach (var (p1, p2) in HAND_CONNECTIONS)
            DrawLine(canvas, ToPx(landmarks[p1]), ToPx(landmarks[p2]), boneColor, 2);

        for (int i = 0; i < landmarks.Length; i++)
            DrawCircle(canvas, ToPx(landmarks[i]), tips.Contains(i) ? 4 : 3, tips.Contains(i) ? tipColor : jointColor);

        canvas.Apply();
        return canvas;
    }

    void DrawLine(Texture2D tex, Vector2Int a, Vector2Int b, Color color, int thickness)
    {
        int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
        int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        while (true)
        {
            for (int tx = -thickness / 2; tx <= thickness / 2; tx++)
                for (int ty = -thickness / 2; ty <= thickness / 2; ty++)
                    tex.SetPixel(Mathf.Clamp(x0 + tx, 0, tex.width - 1), Mathf.Clamp(y0 + ty, 0, tex.height - 1), color);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    void DrawCircle(Texture2D tex, Vector2Int center, int radius, Color color)
    {
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            {
                if (x * x + y * y <= radius * radius)
                    tex.SetPixel(Mathf.Clamp(center.x + x, 0, tex.width - 1), Mathf.Clamp(center.y + y, 0, tex.height - 1), color);
            }
    }

    // =========================================================
    // MediaPipe
    // =========================================================
    Vector2[] GetHandLandmarks()
    {
        if (handLandmarkBridge != null)
            return handLandmarkBridge.GetLandmarks();
        return null;
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
        catch (System.Exception e) { Debug.LogError($"[ASL] TTS init failed: {e.Message}"); }
#else
        _ttsReady = true;
#endif
    }

    void SpeakSentence(string sentence)
    {
        // Convert to lowercase so Android TTS reads it as words, not letters
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
        if (predictionText != null)
            predictionText.text = string.IsNullOrEmpty(_stableLetter) ? "—" : _stableLetter;

        if (confidenceText != null)
            confidenceText.text = _stableConf > 0 ? $"{_stableConf * 100f:F0}%" : "";

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