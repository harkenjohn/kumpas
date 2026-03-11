using System.Collections;
using UnityEngine;
using TMPro;

public class ASLRealtimeSentencePlayer : MonoBehaviour
{
    [Header("Timing")]
    public float delayBeforeStart = 2f;
    public float letterDelay = 0.7f;

    private static ASLRealtimeSentencePlayer _instance;
    public static ASLRealtimeSentencePlayer Instance => _instance;

    private Animator handAnimator;
    private TMP_Text messageDisplayText;
    private TMP_Text countdownText;

    private bool faceDetected = false;
    private bool isPlaying = false;
    private Coroutine currentRoutine;

    void Awake()
    {
        _instance = this;
    }

    void OnEnable()
    {
        _instance = this;
    }

    void Start()
    {
        FindUIElements();
        StartCoroutine(FindAnimatorWhenReady());
    }

    // Keeps trying to find Player hand's Animator until it's active
    IEnumerator FindAnimatorWhenReady()
    {
        while (handAnimator == null)
        {
            // FindObjectOfType only finds ACTIVE objects in the scene
            Animator[] allAnimators = FindObjectsOfType<Animator>();
            foreach (Animator a in allAnimators)
            {
                if (a.gameObject.name == "Player hand")
                {
                    handAnimator = a;
                    Debug.Log("[ASLRealtimeSentencePlayer] Found ACTIVE Player hand Animator!");
                    yield break;
                }
            }

            //Debug.Log("[ASLRealtimeSentencePlayer] Player hand not active yet, retrying...");
            yield return new WaitForSeconds(0.5f);
        }
    }

    void FindUIElements()
    {
        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (TMP_Text t in allTexts)
        {
            if (t.gameObject.name == "MessageDisplayText")
            {
                messageDisplayText = t;
                Debug.Log("[ASLRealtimeSentencePlayer] Found MessageDisplayText!");
            }
            if (t.gameObject.name == "CountdownText")
            {
                countdownText = t;
                Debug.Log("[ASLRealtimeSentencePlayer] Found CountdownText!");
            }
        }

        if (messageDisplayText == null)
            Debug.LogError("[ASLRealtimeSentencePlayer] MessageDisplayText NOT found!");
        if (countdownText == null)
            Debug.LogError("[ASLRealtimeSentencePlayer] CountdownText NOT found!");
    }

    public void RegisterInstance()
    {
        _instance = this;
    }

    public void PlaySentence(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        if (messageDisplayText == null || countdownText == null)
            FindUIElements();

        if (messageDisplayText != null)
            messageDisplayText.text = "" + message;

        if (countdownText != null)
            countdownText.text = "";

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(MasterRoutine(message));
    }

    IEnumerator MasterRoutine(string sentence)
    {
        isPlaying = false;
        yield return new WaitForSeconds(delayBeforeStart);

        if (countdownText != null)
            countdownText.text = "Waiting for face...";

        // Wait until face is detected
        yield return new WaitUntil(() => faceDetected);

        float totalTime = CalculateDuration(sentence);
        isPlaying = true;
        StartCoroutine(CountdownRoutine(totalTime));
        yield return StartCoroutine(PlayLettersRoutine(sentence));

        isPlaying = false;

        if (countdownText != null)
            countdownText.text = "Done";

        // Play idle only if hand is active
        if (handAnimator != null && handAnimator.gameObject.activeInHierarchy)
            handAnimator.Play("Default");
    }

    IEnumerator PlayLettersRoutine(string sentence)
    {
        sentence = sentence.ToUpper();

        foreach (char c in sentence)
        {
            // Pause here until face returns
            if (!faceDetected)
            {
                if (countdownText != null)
                    countdownText.text = "Face lost...";

                // Keep waiting until face comes back
                yield return new WaitUntil(() => faceDetected);

                // Small buffer after face returns
                yield return new WaitForSeconds(0.3f);
            }

            if (!isPlaying) yield break;

            if (char.IsLetter(c))
            {
                string stateName = "ASL_" + c;

                // Only play if hand is currently active
                if (handAnimator != null && handAnimator.gameObject.activeInHierarchy)
                {
                    handAnimator.Play("Default", 0, 0f);
                    yield return null;
                    handAnimator.Play(stateName, 0, 0f);
                    yield return null;

                    // Wait for animation to finish
                    yield return new WaitUntil(() =>
                    {
                        if (!handAnimator.gameObject.activeInHierarchy)
                            return false; // hand went inactive, stay paused
                        var state = handAnimator.GetCurrentAnimatorStateInfo(0);
                        return state.IsName(stateName) && state.normalizedTime >= 1f;
                    });
                }
                else
                {
                    // Hand is inactive (no face), wait until it comes back
                    yield return new WaitUntil(() =>
                        handAnimator != null && handAnimator.gameObject.activeInHierarchy
                    );
                    yield return new WaitForSeconds(0.3f);
                }

                yield return new WaitForSeconds(0.1f);
            }
            else if (c == ' ')
            {
                yield return new WaitForSeconds(letterDelay * 1.5f);
            }
        }
    }

    IEnumerator CountdownRoutine(float duration)
    {
        float timer = duration;

        while (timer > 0 && isPlaying)
        {
            if (faceDetected)
            {
                timer -= Time.deltaTime;
                if (countdownText != null)
                    countdownText.text = Mathf.CeilToInt(timer).ToString() + "s";
            }
            yield return null;
        }
    }

    float CalculateDuration(string sentence)
    {
        float total = 0f;
        foreach (char c in sentence.ToUpper())
        {
            if (char.IsLetter(c)) total += letterDelay + 0.1f;
            else if (c == ' ') total += letterDelay * 1.5f;
        }
        return total;
    }

    public void SetFaceDetected(bool detected)
    {
        faceDetected = detected;
    }
}