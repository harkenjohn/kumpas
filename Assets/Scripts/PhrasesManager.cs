// ============================================================
// PhrasesManager.cs — Unity 6 + Inference Engine 2.3.0
// ============================================================
// Handles the LSTM phrase recognition pipeline.
//
// How it works:
//   1. Activated by ASLManager when dynamic hand motion is detected
//   2. Buffers frames (pose + hand landmarks) for up to PHRASE_BUFFER_TIME
//   3. When hand disappears OR buffer time exceeded → runs LSTM inference
//   4. Reports result back to ASLManager via OnPhraseRecognized callback
//
// Coordinate vector per frame: 225 values
//   Pose      : 33 landmarks × 3 (x, y, z) = 99  — normalized to shoulder midpoint/width
//   Left hand : 21 landmarks × 3 (x, y, z) = 63  — normalized to wrist + middle MCP dist
//   Right hand: 21 landmarks × 3 (x, y, z) = 63  — normalized to wrist + middle MCP dist
//
// Missing landmarks: reuse last valid frame values.
//                    Zero-fill only if never detected in this sequence.
//
// Performance notes (mobile):
//   - PoseLandmarkerRunner is only ACTIVATED during phrase buffering,
//     not during static CNN letter recognition. This saves significant
//     CPU/GPU on mobile.
//   - LSTM inference only fires once per gesture, not every frame.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.InferenceEngine;

public class PhrasesManager : MonoBehaviour
{
    // ── Inspector Fields ──────────────────────────────────────
    [Header("Inference Engine Model")]
    [Tooltip("Drag phrases_model.onnx here from your Assets folder")]
    public ModelAsset phrasesModelAsset;

    [Header("Manager References")]
    public PoseLandmarkBridge poseLandmarkBridge;
    public HandLandmarkBridge handLandmarkBridge;

    [Header("UI")]
    public TMP_Text phraseStatusText;

    [Header("Timing")]
    [Tooltip("Maximum seconds to buffer before forcing LSTM inference")]
    public float phraseBufferTime = 2.0f;

    [Tooltip("Minimum confidence to accept a phrase result (0-1)")]
    [Range(0f, 1f)]
    public float phraseConfidenceThreshold = 0.75f;

    // ── Phrase class names — must match phrases_classes.txt ───
    // Update this list to match your trained phrase classes exactly.
    // ── Phrase class names — must match phrases_model.onnx output order exactly ──
    // Order is taken directly from the exported checkpoint's 'class_names' field:
    // ['GoodAfternoon_Eng', 'GoodAfternoon_Fil', 'GoodEvening', 'GoodMorning_Eng',
    //  'GoodMorning_Fil', 'Hello', 'HowAreYou_Eng', 'HowAreYou_Fil', 'ImFine',
    //  'J', 'No', 'ThankYou', 'Yes', 'YoureWelcome_Eng', 'YoureWelcome_Fil', 'Z']
    private readonly string[] PHRASE_NAMES =
    {
        "GOOD AFTERNOON",       // GoodAfternoon_Eng
        "MAGANDANG HAPON",      // GoodAfternoon_Fil
        "GOOD EVENING",         // GoodEvening
        "GOOD MORNING",         // GoodMorning_Eng
        "MAGANDANG UMAGA",      // GoodMorning_Fil
        "HELLO",                // Hello
        "HOW ARE YOU",          // HowAreYou_Eng
        "KUMUSTA KA",           // HowAreYou_Fil
        "I'M FINE",             // ImFine
        "J",                    // J
        "NO",                   // No
        "THANK YOU",            // ThankYou
        "YES",                  // Yes
        "YOU'RE WELCOME",       // YoureWelcome_Eng
        "WALANG ANUMAN",        // YoureWelcome_Fil
        "Z"                     // Z
    };

    // ── LSTM constants — must match training ──────────────────
    private const int SEQ_LEN = 60;
    private const int INPUT_SIZE = 225;

    // Pose landmark indices
    private const int LEFT_SHOULDER = 11;
    private const int RIGHT_SHOULDER = 12;

    // Hand landmark indices
    private const int WRIST = 0;
    private const int MIDDLE_MCP = 9;

    // ── Inference Engine ──────────────────────────────────────
    private Model _runtimeModel;
    private Worker _worker;
    private bool _modelLoaded = false;

    // ── Buffering state ───────────────────────────────────────
    private bool _isBuffering = false;
    private float _bufferTimer = 0f;
    private List<float[]> _frameBuffer = new List<float[]>();

    // Last-valid-frame values for missing landmark reuse
    private float[] _lastPoseVec = new float[99];
    private float[] _lastLeftHandVec = new float[63];
    private float[] _lastRightHandVec = new float[63];

    // ── Callback to ASLManager ────────────────────────────────
    // Called when a phrase is recognized: (phraseName, confidence)
    public Action<string, float> OnPhraseRecognized;

    // ── Public state read by ASLManager ──────────────────────
    public bool IsBuffering => _isBuffering;

    // =========================================================
    // Unity Lifecycle
    // =========================================================
    void Start()
    {
        InitInferenceEngine();
    }

    void Update()
    {
        if (!_isBuffering) return;

        _bufferTimer += Time.deltaTime;

        // Collect one frame of landmarks into the buffer
        CollectFrame();

        bool handGone = handLandmarkBridge == null || !handLandmarkBridge.HandDetected;
        bool timeExpired = _bufferTimer >= phraseBufferTime;

        if (handGone || timeExpired)
        {
            string reason = handGone ? "hand gone" : "time expired";
            Debug.Log($"[Phrases] Buffering ended ({reason}). Frames: {_frameBuffer.Count}");
            RunInference();
        }
    }

    void OnDestroy()
    {
        _worker?.Dispose();
    }

    // =========================================================
    // Init
    // =========================================================
    void InitInferenceEngine()
    {
        if (phrasesModelAsset == null)
        {
            Debug.LogWarning("[Phrases] No model asset assigned — LSTM disabled.");
            return;
        }

        try
        {
            _runtimeModel = ModelLoader.Load(phrasesModelAsset);
            _worker = new Worker(_runtimeModel, BackendType.GPUCompute);
            _modelLoaded = true;
            Debug.Log("[Phrases] LSTM model loaded");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Phrases] Model load failed: {e.Message}");
        }
    }

    // =========================================================
    // Public API — called by ASLManager
    // =========================================================

    /// <summary>
    /// Start buffering frames for LSTM inference.
    /// Called by ASLManager when dynamic motion is detected.
    /// Also activates PoseLandmarkerRunner to save mobile resources.
    /// </summary>
    public void StartBuffering()
    {
        if (_isBuffering) return;

        _isBuffering = true;
        _bufferTimer = 0f;
        _frameBuffer.Clear();

        // Reset last-valid-frame caches
        Array.Clear(_lastPoseVec, 0, _lastPoseVec.Length);
        Array.Clear(_lastLeftHandVec, 0, _lastLeftHandVec.Length);
        Array.Clear(_lastRightHandVec, 0, _lastRightHandVec.Length);

        // Do NOT toggle poseLandmarkBridge — disabling a MediaPipe Async runner
        // breaks it permanently. Leave it running the whole time.

        SetStatus("Buffering gesture…");
        Debug.Log("[Phrases] Started buffering");
    }

    /// <summary>
    /// Force stop buffering without running inference.
    /// Called by ASLManager if session ends during buffering.
    /// </summary>
    public void CancelBuffering()
    {
        if (!_isBuffering) return;

        _isBuffering = false;
        _frameBuffer.Clear();

        SetStatus("");
        Debug.Log("[Phrases] Buffering cancelled");
    }

    // =========================================================
    // Frame Collection
    // =========================================================
    void CollectFrame()
    {
        // ── Pose ─────────────────────────────────────────────
        float[] poseVec;
        Vector3[] poseLms = poseLandmarkBridge != null ? poseLandmarkBridge.GetLandmarks() : null;

        if (poseLms != null && poseLms.Length >= 33)
        {
            poseVec = NormalizePose(poseLms);
            _lastPoseVec = poseVec;
        }
        else
        {
            // Reuse last valid frame (or zeros if never detected)
            poseVec = _lastPoseVec;
        }

        // ── Hands ─────────────────────────────────────────────
        float[] leftHandVec;
        float[] rightHandVec;

        // HandLandmarkBridge currently exposes Vector2 (x, y).
        // We call GetLandmarksWithHandedness() which returns both hands separately.
        Vector3[] leftLms = null;
        Vector3[] rightLms = null;

        if (handLandmarkBridge != null)
            handLandmarkBridge.GetHandLandmarksBySide(out leftLms, out rightLms);

        if (leftLms != null && leftLms.Length >= 21)
        {
            leftHandVec = NormalizeHand(leftLms);
            _lastLeftHandVec = leftHandVec;
        }
        else
        {
            leftHandVec = _lastLeftHandVec;
        }

        if (rightLms != null && rightLms.Length >= 21)
        {
            rightHandVec = NormalizeHand(rightLms);
            _lastRightHandVec = rightHandVec;
        }
        else
        {
            rightHandVec = _lastRightHandVec;
        }

        // ── Concatenate: pose(99) | left(63) | right(63) ─────
        float[] frame = new float[INPUT_SIZE];
        Array.Copy(poseVec, 0, frame, 0, 99);
        Array.Copy(leftHandVec, 0, frame, 99, 63);
        Array.Copy(rightHandVec, 0, frame, 162, 63);

        _frameBuffer.Add(frame);
    }

    // =========================================================
    // Normalization — must exactly match extract_pose_hand_coords_v4.py
    // =========================================================

    /// <summary>
    /// Normalize 33 pose landmarks.
    /// Origin = shoulder midpoint, Scale = shoulder width.
    /// Returns float[99].
    /// </summary>
    float[] NormalizePose(Vector3[] lms)
    {
        float midX = (lms[LEFT_SHOULDER].x + lms[RIGHT_SHOULDER].x) / 2f;
        float midY = (lms[LEFT_SHOULDER].y + lms[RIGHT_SHOULDER].y) / 2f;
        float scale = Mathf.Abs(lms[LEFT_SHOULDER].x - lms[RIGHT_SHOULDER].x);
        if (scale < 1e-6f) scale = 1e-6f;

        float[] vec = new float[99];
        for (int i = 0; i < 33; i++)
        {
            vec[i * 3 + 0] = (lms[i].x - midX) / scale;
            vec[i * 3 + 1] = (lms[i].y - midY) / scale;
            vec[i * 3 + 2] = lms[i].z / scale;
        }
        return vec;
    }

    /// <summary>
    /// Normalize 21 hand landmarks.
    /// Origin = wrist (lm 0), Scale = wrist→middle MCP (lm 9) distance.
    /// Returns float[63].
    /// </summary>
    float[] NormalizeHand(Vector3[] lms)
    {
        float ox = lms[WRIST].x;
        float oy = lms[WRIST].y;
        float refX = lms[MIDDLE_MCP].x;
        float refY = lms[MIDDLE_MCP].y;
        float scale = Mathf.Sqrt((refX - ox) * (refX - ox) + (refY - oy) * (refY - oy));
        if (scale < 1e-6f) scale = 1e-6f;

        float[] vec = new float[63];
        for (int i = 0; i < 21; i++)
        {
            vec[i * 3 + 0] = (lms[i].x - ox) / scale;
            vec[i * 3 + 1] = (lms[i].y - oy) / scale;
            vec[i * 3 + 2] = lms[i].z / scale;
        }
        return vec;
    }

    // =========================================================
    // LSTM Inference
    // =========================================================
    void RunInference()
    {
        _isBuffering = false;

        if (!_modelLoaded || _frameBuffer.Count == 0)
        {
            Debug.LogWarning("[Phrases] Inference skipped — no model or empty buffer.");
            OnPhraseRecognized?.Invoke("", 0f);
            return;
        }

        // ── Sample exactly SEQ_LEN frames evenly from buffer ──
        float[] sequence = SampleSequence(_frameBuffer, SEQ_LEN);

        // ── Build input tensor (1, SEQ_LEN, INPUT_SIZE) ───────
        using Tensor<float> inputTensor = new Tensor<float>(
            new TensorShape(1, SEQ_LEN, INPUT_SIZE), sequence);

        _worker.Schedule(inputTensor);

        Tensor<float> outputTensor = _worker.PeekOutput() as Tensor<float>;
        float[] logits = outputTensor.DownloadToArray();

        // ── Softmax ───────────────────────────────────────────
        float maxVal = float.MinValue;
        foreach (float v in logits) if (v > maxVal) maxVal = v;

        float sum = 0f;
        float[] probs = new float[PHRASE_NAMES.Length];
        for (int i = 0; i < PHRASE_NAMES.Length; i++)
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

        string phrase = PHRASE_NAMES[bestIdx];
        float confidence = bestProb;

        Debug.Log($"[Phrases] Result: '{phrase}' ({confidence * 100f:F1}%)");
        SetStatus($"Phrase: {phrase} ({confidence * 100f:F0}%)");

        // Report back to ASLManager — it decides whether to use this result
        OnPhraseRecognized?.Invoke(phrase, confidence);
    }

    // =========================================================
    // Sequence Sampler
    // =========================================================

    /// <summary>
    /// Evenly sample targetLen frames from the buffer.
    /// Matches the linspace sampling in PhraseCoordsDataset and extraction script.
    /// Returns a flat float array of shape (targetLen * INPUT_SIZE).
    /// </summary>
    float[] SampleSequence(List<float[]> buffer, int targetLen)
    {
        float[] output = new float[targetLen * INPUT_SIZE];
        int n = buffer.Count;

        for (int i = 0; i < targetLen; i++)
        {
            // linspace index: evenly spread across available frames
            int srcIdx = (n == 1) ? 0 : Mathf.RoundToInt(i * (n - 1) / (float)(targetLen - 1));
            srcIdx = Mathf.Clamp(srcIdx, 0, n - 1);

            Array.Copy(buffer[srcIdx], 0, output, i * INPUT_SIZE, INPUT_SIZE);
        }

        return output;
    }

    // =========================================================
    // UI
    // =========================================================
    void SetStatus(string msg)
    {
        if (phraseStatusText != null) phraseStatusText.text = msg;
        if (!string.IsNullOrEmpty(msg)) Debug.Log($"[Phrases] {msg}");
    }
}