using UnityEngine;

public class HandFaceFollower : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Offset from face center so hand appears beside/below face")]
    public Vector3 offset = new Vector3(0f, -200f, 0f);

    [Tooltip("How smoothly the hand follows the face")]
    public float smoothSpeed = 10f;

    [Tooltip("Scale of hand relative to face size")]
    public float scaleMultiplier = 1f;

    private Transform noseTip;
    private Transform leftCheek;
    private Transform rightCheek;

    private Vector3 targetPosition;
    private Vector3 targetScale;
    private bool faceFound = false;

    void Start()
    {
        StartCoroutine(FindFaceLandmarks());
    }

    System.Collections.IEnumerator FindFaceLandmarks()
    {
        while (noseTip == null)
        {
            // Get all Point Annotation clones
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            
            int landmarkIndex = 0;
            foreach (GameObject obj in allObjects)
            {
                if (obj.name == "Point Annotation(Clone)" && obj.activeInHierarchy)
                {
                    // Landmark 1 = nose tip in MediaPipe face landmarks
                    if (landmarkIndex == 1)
                    {
                        noseTip = obj.transform;
                        Debug.Log("[HandFaceFollower] Found nose tip landmark!");
                    }
                    // Landmark 234 = left cheek, 454 = right cheek
                    // We use these to estimate face width for scaling
                    if (landmarkIndex == 234) leftCheek = obj.transform;
                    if (landmarkIndex == 454) rightCheek = obj.transform;
                    
                    landmarkIndex++;
                }
            }

            if (noseTip == null)
            {
                Debug.Log("[HandFaceFollower] Waiting for face landmarks...");
                yield return new WaitForSeconds(0.5f);
            }
        }

        Debug.Log("[HandFaceFollower] Face landmarks ready!");
    }

    void LateUpdate()
    {
        if (noseTip == null) return;

        // Follow nose tip position with offset
        targetPosition = noseTip.position + offset;
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * smoothSpeed
        );

        // Scale based on face width if we have cheek landmarks
        if (leftCheek != null && rightCheek != null)
        {
            float faceWidth = Vector3.Distance(leftCheek.position, rightCheek.position);
            float dynamicScale = faceWidth * scaleMultiplier;
            targetScale = new Vector3(dynamicScale, dynamicScale, dynamicScale);
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                targetScale,
                Time.deltaTime * smoothSpeed
            );
        }
    }
}