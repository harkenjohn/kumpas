using UnityEngine;

public class FaceDetectionBridge : MonoBehaviour
{
    [Header("References")]
    public GameObject faceOffsetObject;

    private ASLRealtimeSentencePlayer aslPlayer;

    void Update()
    {
        if (faceOffsetObject == null) return;

        // Find the instance at runtime since it lives inside a prefab
        if (aslPlayer == null)
            aslPlayer = ASLRealtimeSentencePlayer.Instance;

        if (aslPlayer == null) return;

        bool faceDetected = faceOffsetObject.activeInHierarchy;
        aslPlayer.SetFaceDetected(faceDetected);
    }
}