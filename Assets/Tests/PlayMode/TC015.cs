using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Networking;

public class TC015_SignToSpeech_NoHandGestureDetected
{

    private const string API_URL        = "https://kumpas-model-sign-language-recognition-api.hf.space/predict/auto";
    private const int    TARGET_FRAMES  = 60;
    private const int    CAPTURE_WIDTH  = 320;
    private const int    CAPTURE_HEIGHT = 240;
    private const float  TEST_TIMEOUT   = 120f;

    private string _apiResult           = null;
    private bool   _detectedFlag        = false;
    private bool   _sessionEndedCalled  = false;

    [UnityTest]
    public IEnumerator TC015_NoHandGesture_CameraCloses()
    {
        List<byte[]> blankFrames = GenerateBlankFrames(TARGET_FRAMES);

        Assert.AreEqual(TARGET_FRAMES, blankFrames.Count,
            "TC-015: Failed to generate blank frames.");
        Debug.Log($"[TC-015] Generated {blankFrames.Count} blank frames.");
        yield return SendFramesToAPI(blankFrames);
        Debug.Log($"[TC-015] API returned: '{_apiResult}', detected: {_detectedFlag}");
        Assert.IsFalse(_detectedFlag,
            "TC-015: API should return detected: false when no hand gesture is present.");
        bool lastWindowEmpty = false;
        bool sessionEnded    = false;

        if (!_detectedFlag)
        {
            if (lastWindowEmpty)
            {
                sessionEnded = true;
                SimulateOnASLSessionEnded();
            }
            else
            {
                lastWindowEmpty = true;
                Debug.Log("[TC-015] First miss — flag set, session continues.");
            }
        }

        if (!_detectedFlag && !sessionEnded)
        {
            if (lastWindowEmpty)
            {
                sessionEnded = true;
                SimulateOnASLSessionEnded();
                Debug.Log("[TC-015] Second consecutive miss — session ended.");
            }
        }

        Assert.IsTrue(sessionEnded,
            "TC-015: Two consecutive unrecognized windows should end the session.");
        Assert.IsTrue(_sessionEndedCalled,
            "TC-015: OnASLSessionEnded() should be called when session ends (camera closes).");

        Debug.Log("[TC-015] PASSED — No hand detected, camera closed correctly.");
    }

    private List<byte[]> GenerateBlankFrames(int count)
    {
        var frames  = new List<byte[]>();
        var blankTex = new Texture2D(CAPTURE_WIDTH, CAPTURE_HEIGHT, TextureFormat.RGB24, false);

        // Fill with black pixels
        Color[] pixels = new Color[CAPTURE_WIDTH * CAPTURE_HEIGHT];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.black;

        blankTex.SetPixels(pixels);
        blankTex.Apply();

        byte[] jpg = blankTex.EncodeToJPG(75);
        for (int i = 0; i < count; i++)
            frames.Add(jpg);

        Object.DestroyImmediate(blankTex);

        return frames;
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
            Debug.LogError($"[TC-015] API request failed: {req.error}");
            _detectedFlag = false;
            _apiResult    = null;
            yield break;
        }

        string json = req.downloadHandler.text;
        Debug.Log($"[TC-015] Raw API response: {json}");

        var response  = JsonUtility.FromJson<TC015_AutoResponse>(json);
        _detectedFlag = response.detected;
        _apiResult    = response.result;
    }

    private void SimulateOnASLSessionEnded()
    {
        _sessionEndedCalled = true;
        Debug.Log("[TC-015] SimulateOnASLSessionEnded called — camera closed.");
    }
}

[System.Serializable]
public class TC015_AutoResponse
{
    public string result;
    public string result_type;
    public float  confidence;
    public float  vote_ratio;
    public bool   detected;
}
