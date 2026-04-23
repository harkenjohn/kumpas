// ============================================================
// ASLOrientationHandler.cs
// ============================================================
// Watches for screen orientation changes while the ASL camera
// session is active and:
//   1. Repositions Status and Preview text
//   2. Rotates the camera feed display so it appears upright
//
// Portrait  → Camera feed 0°, Status/Preview centered
// Landscape → Camera feed -90°, Status/Preview full-width
//
// HOW TO SET UP IN UNITY:
//   1. Attach this script to the same GameObject as ASLManager
//   2. In the Inspector, assign:
//        - Status Text  → "Status" RectTransform (PoseLandmark Canvas)
//        - Preview Text → "Preview" RectTransform (PoseLandmark Canvas)
//        - Camera Feed Body → "Body" RectTransform (PoseLandmark Canvas → Container Panel)
//   3. Done — rotates automatically when phone orientation changes
// ============================================================

using UnityEngine;

public class ASLOrientationHandler : MonoBehaviour
{
    [Header("Text RectTransforms (from PoseLandmark Canvas)")]
    [Tooltip("The 'Status' TMP Text RectTransform")]
    public RectTransform statusText;

    [Tooltip("The 'Preview' TMP Text RectTransform")]
    public RectTransform previewText;

    [Header("Camera Feed")]
    [Tooltip("The 'Body' RectTransform containing the camera feed (PoseLandmark Canvas → Container Panel → Body)")]
    public RectTransform cameraFeedBody;

    // ── Portrait original values (from Inspector screenshots) ──
    // Anchors: center (0.5, 0.5), Width: 2000, Height: 120
    // Status  PosY:  2336
    // Preview PosY: -2249

    private const float PORTRAIT_WIDTH = 2000f;
    private const float PORTRAIT_HEIGHT = 120f;
    private const float STATUS_PORTRAIT_Y = 2336f;
    private const float PREVIEW_PORTRAIT_Y = -2249f;

    // ── Landscape values ──────────────────────────────────────
    // Switch to horizontal-stretch anchors (MinX=0, MaxX=1)
    // so the text fills the full width of the landscape screen.
    // PosY values are smaller because landscape screen height is ~half of portrait height in Unity units.
    // We keep a ~60px inset from the top/bottom edges.
    private const float LANDSCAPE_HEIGHT = 120f;
    private const float STATUS_LANDSCAPE_Y = 60f;   // from top edge (positive = up from center-anchor baseline)
    private const float PREVIEW_LANDSCAPE_Y = -60f;  // from bottom edge

    // ── Padding from screen edges in landscape (in pixels) ────
    private const float LANDSCAPE_PADDING = 20f;

    // ── State tracking ────────────────────────────────────────
    private bool _isLandscape = false;
    private bool _isActive = false;  // only reposition while ASL session canvas is active

    // =========================================================
    // Public API — called by UIManager
    // =========================================================

    /// <summary>Call this when the ASL camera session starts.</summary>
    public void OnASLSessionStarted()
    {
        _isActive = true;
        ApplyCurrentOrientation();
    }

    /// <summary>Call this when the ASL camera session ends.</summary>
    public void OnASLSessionEnded()
    {
        _isActive = false;
        RestorePortrait();
    }

    // =========================================================
    // Unity Lifecycle
    // =========================================================

    void Update()
    {
        if (!_isActive) return;

        bool landscape = Screen.width > Screen.height;

        if (landscape != _isLandscape)
        {
            _isLandscape = landscape;
            ApplyCurrentOrientation();
            Debug.Log($"[ASLOrientation] Orientation changed → {(landscape ? "Landscape" : "Portrait")}");
        }
    }

    // =========================================================
    // Layout Helpers
    // =========================================================

    private void ApplyCurrentOrientation()
    {
        _isLandscape = Screen.width > Screen.height;

        if (_isLandscape)
            ApplyLandscape();
        else
            RestorePortrait();
    }

    private void ApplyLandscape()
    {
        if (statusText != null)
        {
            // Stretch anchors horizontally
            statusText.anchorMin = new Vector2(0f, 1f);
            statusText.anchorMax = new Vector2(1f, 1f);
            statusText.pivot = new Vector2(0.5f, 1f);

            // Full-width stretch: left/right offsets act as padding
            statusText.offsetMin = new Vector2(LANDSCAPE_PADDING, -LANDSCAPE_HEIGHT - LANDSCAPE_PADDING);
            statusText.offsetMax = new Vector2(-LANDSCAPE_PADDING, -LANDSCAPE_PADDING);
        }

        if (previewText != null)
        {
            // Stretch anchors horizontally, anchor to bottom
            previewText.anchorMin = new Vector2(0f, 0f);
            previewText.anchorMax = new Vector2(1f, 0f);
            previewText.pivot = new Vector2(0.5f, 0f);

            // Full-width stretch: left/right offsets act as padding
            previewText.offsetMin = new Vector2(LANDSCAPE_PADDING, LANDSCAPE_PADDING);
            previewText.offsetMax = new Vector2(-LANDSCAPE_PADDING, LANDSCAPE_HEIGHT + LANDSCAPE_PADDING);
        }

        // Rotate camera feed -90° so it appears upright when phone is landscape
        if (cameraFeedBody != null)
        {
            cameraFeedBody.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }

        Debug.Log("[ASLOrientation] Applied landscape layout + camera rotation");
    }

    private void RestorePortrait()
    {
        if (statusText != null)
        {
            statusText.anchorMin = new Vector2(0.5f, 0.5f);
            statusText.anchorMax = new Vector2(0.5f, 0.5f);
            statusText.pivot = new Vector2(0.5f, 0.5f);

            statusText.sizeDelta = new Vector2(PORTRAIT_WIDTH, PORTRAIT_HEIGHT);
            statusText.anchoredPosition = new Vector2(0f, STATUS_PORTRAIT_Y);
        }

        if (previewText != null)
        {
            previewText.anchorMin = new Vector2(0.5f, 0.5f);
            previewText.anchorMax = new Vector2(0.5f, 0.5f);
            previewText.pivot = new Vector2(0.5f, 0.5f);

            previewText.sizeDelta = new Vector2(PORTRAIT_WIDTH, PORTRAIT_HEIGHT);
            previewText.anchoredPosition = new Vector2(0f, PREVIEW_PORTRAIT_Y);
        }

        // Reset camera feed rotation to 0°
        if (cameraFeedBody != null)
        {
            cameraFeedBody.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }

        Debug.Log("[ASLOrientation] Restored portrait layout + camera rotation");
    }
}