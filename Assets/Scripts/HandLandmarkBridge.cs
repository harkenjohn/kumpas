// ============================================================
// HandLandmarkBridge.cs
// ============================================================
// Sits between the MediaPipe HandLandmarkerRunner and ASLManager.
// Stores the latest hand landmark result and exposes it as a
// simple Vector2 array that ASLManager can read every frame.
//
// Setup:
//   1. Add this script to the same GameObject as HandLandmarkerRunner
//      (or any active GameObject in the scene)
//   2. Assign the HandLandmarkerRunner reference in the Inspector
//   3. Assign this bridge to ASLManager's "Hand Landmark Bridge" field
// ============================================================

using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

public class HandLandmarkBridge : MonoBehaviour
{
    [Header("References")]
    public HandLandmarkerRunner handLandmarkerRunner;

    // ── Singleton so ASLManager can find it easily ────────────
    private static HandLandmarkBridge _instance;
    public static HandLandmarkBridge Instance => _instance;

    // ── Latest landmarks — read by ASLManager every frame ─────
    private Vector2[] _latestLandmarks = null;
    private bool _handDetected = false;

    public bool HandDetected => _handDetected;

    void Awake()
    {
        _instance = this;
    }

    void OnEnable()
    {
        _instance = this;
        // Subscribe to the runner's result callback
        if (handLandmarkerRunner != null)
            HandLandmarkerRunner.OnHandLandmarksDetected += OnLandmarksDetected;
    }

    void OnDisable()
    {
        HandLandmarkerRunner.OnHandLandmarksDetected -= OnLandmarksDetected;
        _latestLandmarks = null;
        _handDetected = false;
    }

    // ── Called by HandLandmarkerRunner when results are ready ──
    private void OnLandmarksDetected(HandLandmarkerResult result)
    {
        if (result.handLandmarks == null || result.handLandmarks.Count == 0)
        {
            _latestLandmarks = null;
            _handDetected = false;
            return;
        }

        var landmarks = result.handLandmarks[0].landmarks;

        if (landmarks == null || landmarks.Count < 21)
        {
            _latestLandmarks = null;
            _handDetected = false;
            return;
        }

        // Convert to Vector2 array (x, y normalized 0-1)
        // FIX: Flip X (1f - x) to match the Python training pipeline which
        // mirrored the camera frame with cv2.flip(frame, 1) before detection.
        _latestLandmarks = new Vector2[21];
        for (int i = 0; i < 21; i++)
            _latestLandmarks[i] = new Vector2(1f - landmarks[i].x, landmarks[i].y);

        _handDetected = true;
    }

    // ── Called by ASLManager.GetHandLandmarks() every frame ───
    public Vector2[] GetLandmarks()
    {
        return _handDetected ? _latestLandmarks : null;
    }
}