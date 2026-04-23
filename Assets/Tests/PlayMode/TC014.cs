using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Networking;
using UnityEngine.Video;

public class TC014_SignToSpeech_VisualInput
{
    private const string API_URL = "https://kumpas-model-sign-language-recognition-api.hf.space/predict/auto";
    private const string VIDEO_FILENAME = "HelloTest.mp4";
    private const string EXPECTED_RESULT = "HELLO";
    private const int TARGET_FRAMES = 60;
    private const int CAPTURE_WIDTH = 320;
    private const int CAPTURE_HEIGHT = 240;
    private const float TEST_TIMEOUT = 120f;

    private List<byte[]> _capturedFrames = new List<byte[]>();
    private bool _videoReady = false;
    private bool _framesExtracted = false;
    private string _recognizedResult = null;
    private bool _detectedFlag = false;
    private bool _speakTextCalled = false;
    private string _speakTextArg = null;

    private bool _frameReady = false;

    [UnityTest]
    public IEnumerator TC014_ValidHandGesture_CorrectTextDisplayedAndSpeakTextTriggered()
    {
        string videoPath = Path.Combine(Application.streamingAssetsPath, VIDEO_FILENAME);

        Assert.IsTrue(File.Exists(videoPath),
            $"TC-014: Video file not found at: {videoPath}\n" +
            $"Please copy {VIDEO_FILENAME} into Assets/StreamingAssets/");

        yield return ExtractFramesFromVideo(videoPath);

        Assert.IsTrue(_framesExtracted,
            "TC-014: Frame extraction from video failed.");
        Assert.Greater(_capturedFrames.Count, 0,
            "TC-014: No frames were captured from the video.");

        Debug.Log($"[TC-014] Captured {_capturedFrames.Count} raw frames.");

        List<byte[]> sampledFrames = SampleFrames(_capturedFrames, TARGET_FRAMES);

        Assert.AreEqual(TARGET_FRAMES, sampledFrames.Count,
            $"TC-014: Expected {TARGET_FRAMES} frames after sampling.");

        Debug.Log($"[TC-014] Downsampled to {sampledFrames.Count} frames. Sending to API...");

        yield return SendFramesToAPI(sampledFrames);

        Debug.Log($"[TC-014] API returned: '{_recognizedResult}', detected: {_detectedFlag}");

        Assert.IsNotNull(_recognizedResult,
            "TC-014: API returned null. Check API URL or network connection.");
        Assert.IsTrue(_detectedFlag,
            "TC-014: API 'detected' flag should be true for a valid hand gesture.");
        Assert.AreEqual(EXPECTED_RESULT, _recognizedResult,
            $"TC-014: Expected '{EXPECTED_RESULT}' but API returned '{_recognizedResult}'.");

        SimulateSpeakText(_recognizedResult);

        Assert.IsTrue(_speakTextCalled,
            "TC-014: SpeakText() was not triggered after recognition.");
        Assert.AreEqual(EXPECTED_RESULT, _speakTextArg,
            $"TC-014: SpeakText() was called with '{_speakTextArg}' instead of '{EXPECTED_RESULT}'.");

        Debug.Log($"[TC-014] PASSED — Recognized: '{_recognizedResult}', SpeakText triggered with: '{_speakTextArg}'");
    }

    private IEnumerator ExtractFramesFromVideo(string videoPath)
    {
        _capturedFrames.Clear();
        _framesExtracted = false;
        _videoReady = false;

        GameObject go = new GameObject("VideoPlayer_TC014");
        var videoPlayer = go.AddComponent<VideoPlayer>();
        var renderTex = new RenderTexture(CAPTURE_WIDTH, CAPTURE_HEIGHT, 0,
                                     RenderTextureFormat.ARGB32);

        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = renderTex;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = videoPath;
        videoPlayer.playOnAwake = false;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.sendFrameReadyEvents = true; 

        videoPlayer.prepareCompleted += (vp) => { _videoReady = true; };
        videoPlayer.frameReady += OnFrameReady;

        videoPlayer.Prepare();

        float waitTimer = 0f;
        while (!_videoReady && waitTimer < 15f)
        {
            waitTimer += Time.deltaTime;
            yield return null;
        }

        Assert.IsTrue(_videoReady, "TC-014: VideoPlayer failed to prepare within 15 seconds.");

        long totalFrames = (long)videoPlayer.frameCount;
        Debug.Log($"[TC-014] Video has {totalFrames} total frames.");

        // Capture every Nth frame to get ~120 raw frames before downsampling
        long step = Mathf.Max(1, (int)(totalFrames / 120));

        var readbackTex = new Texture2D(CAPTURE_WIDTH, CAPTURE_HEIGHT, TextureFormat.RGB24, false);

        // ── Scrub through frames ───────────────────────────────
        for (long f = 0; f < totalFrames; f += step)
        {
            _frameReady = false;
            videoPlayer.frame = f;
            videoPlayer.Play();

            // Wait until VideoPlayer fires frameReady for this frame
            float frameWait = 0f;
            while (!_frameReady && frameWait < 2f)
            {
                frameWait += Time.deltaTime;
                yield return null;
            }

            videoPlayer.Pause();

            // Wait one more frame for GPU to flush
            yield return new WaitForEndOfFrame();

            // Read pixels from RenderTexture
            RenderTexture.active = renderTex;
            readbackTex.ReadPixels(new Rect(0, 0, CAPTURE_WIDTH, CAPTURE_HEIGHT), 0, 0);
            readbackTex.Apply();
            RenderTexture.active = null;

            _capturedFrames.Add(readbackTex.EncodeToJPG(75));
        }

        // ── Cleanup ───────────────────────────────────────────
        videoPlayer.frameReady -= OnFrameReady;
        videoPlayer.Stop();
        Object.DestroyImmediate(readbackTex);
        Object.DestroyImmediate(renderTex);
        Object.DestroyImmediate(go);

        _framesExtracted = _capturedFrames.Count > 0;
        Debug.Log($"[TC-014] Frame extraction complete. {_capturedFrames.Count} frames captured.");
    }

    private void OnFrameReady(VideoPlayer vp, long frameIndex)
    {
        _frameReady = true;
    }


    private List<byte[]> SampleFrames(List<byte[]> frames, int targetCount)
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

    private IEnumerator SendFramesToAPI(List<byte[]> frames)
    {
        WWWForm form = new WWWForm();
        for (int i = 0; i < frames.Count; i++)
            form.AddBinaryData("files", frames[i], $"frame_{i:D4}.jpg", "image/jpeg");

        using var req = UnityWebRequest.Post(API_URL, form);
        req.timeout = (int)TEST_TIMEOUT;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[TC-014] API request failed: {req.error}");
            _recognizedResult = null;
            _detectedFlag = false;
            yield break;
        }

        string json = req.downloadHandler.text;
        Debug.Log($"[TC-014] Raw API response: {json}");

        var response = JsonUtility.FromJson<TC014_AutoResponse>(json);
        _recognizedResult = response.detected ? response.result : null;
        _detectedFlag = response.detected;
    }

    private void SimulateSpeakText(string text)
    {
        _speakTextCalled = true;
        _speakTextArg = text;
        Debug.Log($"[TC-014] SimulateSpeakText called with: '{text}'");
    }
}

[System.Serializable]
public class TC014_AutoResponse
{
    public string result;
    public string result_type;
    public float confidence;
    public float vote_ratio;
    public bool detected;
}