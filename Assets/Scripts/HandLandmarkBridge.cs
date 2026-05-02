// ============================================================
// HandLandmarkBridge.cs
// ============================================================
// Sits between the MediaPipe HandLandmarkerRunner and ASLManager/PhrasesManager.
//
// Changes from original:
//   - Now stores BOTH hands separately (left + right) for LSTM pipeline
//   - Exposes Vector3[] (x, y, z) per hand in addition to Vector2[] for CNN
//   - GetHandLandmarksBySide() used by PhrasesManager to build 225-value vector
//   - GetLandmarks() (Vector2[]) unchanged — ASLManager CNN path unaffected
//
// Setup:
//   1. Add this script to the same GameObject as HandLandmarkerRunner
//   2. Assign the HandLandmarkerRunner reference in the Inspector
//   3. Assign this bridge to ASLManager's "Hand Landmark Bridge" field
//   4. Assign this bridge to PhrasesManager's "Hand Landmark Bridge" field
// ============================================================

using UnityEngine;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

public class HandLandmarkBridge : MonoBehaviour
{
    [Header("References")]
    public HandLandmarkerRunner handLandmarkerRunner;

    // ── Singleton ─────────────────────────────────────────────
    private static HandLandmarkBridge _instance;
    public static HandLandmarkBridge Instance => _instance;

    // ── CNN path: first detected hand as Vector2 (unchanged) ──
    private Vector2[] _latestLandmarks2D = null;
    private bool _handDetected = false;
    public bool HandDetected => _handDetected;

    // ── LSTM path: both hands as Vector3, separated by side ───
    // X is already flipped (1f - x) to match training pipeline.
    private Vector3[] _leftHandLandmarks3D = null;
    private Vector3[] _rightHandLandmarks3D = null;

    void Awake()
    {
        _instance = this;
        // Subscribe once here and never unsubscribe until destroyed.
        // Subscribing in OnEnable() caused the subscription to be lost on second+
        // sessions: HandLandmarkerRunner resets its static event during its own
        // OnEnable(), wiping any subscription made before it in the same frame.
        HandLandmarkerRunner.OnHandLandmarksDetected += OnLandmarksDetected;
    }

    void OnEnable()
    {
        _instance = this;
    }

    void OnDisable()
    {
        // Do NOT unsubscribe here — just clear the cached landmark data.
        // The subscription stays alive so it works immediately on re-enable.
        _latestLandmarks2D = null;
        _leftHandLandmarks3D = null;
        _rightHandLandmarks3D = null;
        _handDetected = false;
    }

    void OnDestroy()
    {
        // Only truly unsubscribe when the object is destroyed
        HandLandmarkerRunner.OnHandLandmarksDetected -= OnLandmarksDetected;
    }

    // ── Called by HandLandmarkerRunner when results are ready ──
    private void OnLandmarksDetected(HandLandmarkerResult result)
    {
        // Reset both hands each frame
        _leftHandLandmarks3D = null;
        _rightHandLandmarks3D = null;

        if (result.handLandmarks == null || result.handLandmarks.Count == 0)
        {
            _latestLandmarks2D = null;
            _handDetected = false;
            return;
        }

        // ── Process all detected hands ────────────────────────
        for (int h = 0; h < result.handLandmarks.Count; h++)
        {
            var landmarks = result.handLandmarks[h].landmarks;
            if (landmarks == null || landmarks.Count < 21) continue;

            // Determine handedness label
            string side = "Right"; // default fallback
            try
            {
                if (result.handedness != null && result.handedness.Count > h)
                {
                    var classifications = result.handedness[h];
                    if (classifications.categories != null && classifications.categories.Count > 0)
                        side = classifications.categories[0].categoryName; // "Left" or "Right"
                }
            }
            catch { /* keep default "Right" if handedness is unavailable */ }

            // Build Vector3 array with X flip to match training pipeline
            var lms3D = new Vector3[21];
            for (int i = 0; i < 21; i++)
            {
                lms3D[i] = new Vector3(
                    1f - landmarks[i].x,   // flip X
                    landmarks[i].y,
                    landmarks[i].z
                );
            }

            if (side == "Left")
                _leftHandLandmarks3D = lms3D;
            else
                _rightHandLandmarks3D = lms3D;
        }

        // ── CNN path: use first hand as Vector2 (original behavior) ──
        var firstHand = result.handLandmarks[0].landmarks;
        if (firstHand != null && firstHand.Count >= 21)
        {
            _latestLandmarks2D = new Vector2[21];
            for (int i = 0; i < 21; i++)
                _latestLandmarks2D[i] = new Vector2(firstHand[i].x, firstHand[i].y);

            _handDetected = true;
        }
        else
        {
            _latestLandmarks2D = null;
            _handDetected = false;
        }
    }

    // ── Called by ASLManager (CNN path) — unchanged behavior ──
    public Vector2[] GetLandmarks()
    {
        return _handDetected ? _latestLandmarks2D : null;
    }

    // ── Called by PhrasesManager (LSTM path) ──────────────────
    // Returns left and right hand landmarks as Vector3[] separately.
    // Either value is null if that hand was not detected this frame.
    public void GetHandLandmarksBySide(out Vector3[] leftHand, out Vector3[] rightHand)
    {
        leftHand = _leftHandLandmarks3D;
        rightHand = _rightHandLandmarks3D;
    }

    // ── Velocity helper: average movement of all landmarks ────
    // Used by ASLManager to detect static vs dynamic motion.
    // Returns average displacement between prevLandmarks and current.
    public float CalculateVelocity(Vector2[] prevLandmarks)
    {
        if (prevLandmarks == null || _latestLandmarks2D == null) return 0f;
        if (prevLandmarks.Length != _latestLandmarks2D.Length) return 0f;

        float totalDisplacement = 0f;
        for (int i = 0; i < _latestLandmarks2D.Length; i++)
            totalDisplacement += Vector2.Distance(prevLandmarks[i], _latestLandmarks2D[i]);

        return totalDisplacement / _latestLandmarks2D.Length;
    }
}