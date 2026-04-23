using UnityEngine;
using Mediapipe.Unity.Sample.FaceLandmarkDetection;

public class AnnotationCleaner : MonoBehaviour
{
    [SerializeField] private FaceLandmarkerRunner runner;

    public void ForceClear()
    {
        if (runner == null)
        {
            Debug.LogError("Runner is not assigned!");
            return;
        }

        // 🔥 Find the annotation controller automatically
        var controller = runner.GetComponentInChildren<MonoBehaviour>(true);

        if (controller != null)
        {
            Debug.Log("Clearing annotation...");

            // 🔥 Force refresh (this removes stuck landmarks)
            controller.gameObject.SetActive(false);
            controller.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Annotation controller not found!");
        }
    }
}