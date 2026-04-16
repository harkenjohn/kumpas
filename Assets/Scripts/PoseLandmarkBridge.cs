// ============================================================
// PoseLandmarkBridge.cs
// ============================================================
// Sits between the MediaPipe PoseLandmarkerRunner and PhrasesManager.
// Stores the latest pose landmark result and exposes it as a
// Vector3 array (x, y, z normalized) that PhrasesManager reads
// every frame to build the 225-value coordinate vector.
//
// Setup:
//   1. Add this script to the same GameObject as PoseLandmarkerRunner
//   2. Assign the PoseLandmarkerRunner reference in the Inspector
//   3. Assign this bridge to PhrasesManager's "Pose Landmark Bridge" field
//
// NOTE: Pose landmarks are NOT X-flipped here. The extraction script
// (extract_pose_hand_coords_v4.py) flipped the entire image before
// running MediaPipe, so both pose and hand detections were made on the
// flipped frame. We replicate that by flipping X (1f - x) for pose too.
// ============================================================

using UnityEngine;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;

public class PoseLandmarkBridge : MonoBehaviour
{
    [Header("References")]
    public PoseLandmarkerRunner poseLandmarkerRunner;

    // ── Singleton ─────────────────────────────────────────────
    private static PoseLandmarkBridge _instance;
    public static PoseLandmarkBridge Instance => _instance;

    // ── Latest landmarks ──────────────────────────────────────
    // Vector3: x, y normalized 0-1, z is depth (relative)
    private Vector3[] _latestLandmarks = null;
    private bool _poseDetected = false;

    public bool PoseDetected => _poseDetected;

    // ── Frame throttle — only process every Nth frame ─────────
    // Pose detection is expensive on mobile. Running every 2nd frame
    // halves the cost with minimal accuracy loss for gesture sequences.
    [Header("Performance")]
    [Tooltip("Run pose detection every N frames. 1 = every frame, 2 = every other frame.")]
    public int poseDetectionInterval = 2;
    private int _frameCounter = 0;

    void Awake()
    {
        _instance = this;
    }

    void OnEnable()
    {
        _instance = this;
        if (poseLandmarkerRunner != null)
            PoseLandmarkerRunner.OnPoseLandmarksDetected += OnLandmarksDetected;
    }

    void OnDisable()
    {
        PoseLandmarkerRunner.OnPoseLandmarksDetected -= OnLandmarksDetected;
        _latestLandmarks = null;
        _poseDetected = false;
    }

    // ── Called by PoseLandmarkerRunner when results are ready ──
    private void OnLandmarksDetected(PoseLandmarkerResult result)
    {
        // Throttle: skip frames based on interval setting
        _frameCounter++;
        if (_frameCounter % poseDetectionInterval != 0)
            return;

        if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
        {
            _latestLandmarks = null;
            _poseDetected = false;
            return;
        }

        var landmarks = result.poseLandmarks[0].landmarks;

        if (landmarks == null || landmarks.Count < 33)
        {
            _latestLandmarks = null;
            _poseDetected = false;
            return;
        }

        // Flip X (1f - x) to match the Python extraction pipeline which ran
        // cv2.flip(frame, 1) before pose detection.
        _latestLandmarks = new Vector3[33];
        for (int i = 0; i < 33; i++)
        {
            _latestLandmarks[i] = new Vector3(
                1f - landmarks[i].x,
                landmarks[i].y,
                landmarks[i].z
            );
        }

        _poseDetected = true;
    }

    // ── Called by PhrasesManager every frame ──────────────────
    public Vector3[] GetLandmarks()
    {
        return _poseDetected ? _latestLandmarks : null;
    }
}