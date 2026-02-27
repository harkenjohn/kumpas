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

    private bool faceDetected = true;
    private bool isPlaying = false;
    private Coroutine currentRoutine;

    void Awake()
    {
        _instance = this;
        handAnimator = GetComponent<Animator>();

        if (handAnimator == null)
            Debug.LogError("[ASLRealtimeSentencePlayer] No Animator found!");
        else
            Debug.Log("[ASLRealtimeSentencePlayer] Animator found!");
    }

    void OnEnable()
    {
        _instance = this;
    }

    void Start()
    {
        // Find TMP texts by name across ALL loaded scenes
        FindUIElements();
    }

    void FindUIElements()
    {
        // Search all TMP texts in ALL scenes
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
            Debug.LogError("[ASLRealtimeSentencePlayer] MessageDisplayText NOT found! Make sure it is named exactly 'MessageDisplayText'");

        if (countdownText == null)
            Debug.LogError("[ASLRealtimeSentencePlayer] CountdownText NOT found! Make sure it is named exactly 'CountdownText'");
    }

    public void RegisterInstance()
    {
        _instance = this;
        Debug.Log("[ASLRealtimeSentencePlayer] Instance manually registered.");
    }

    public void PlaySentence(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        // Try finding UI again in case it wasn't ready at Start
        if (messageDisplayText == null || countdownText == null)
            FindUIElements();

        if (messageDisplayText != null)
            messageDisplayText.text = " " + message;

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

        yield return new WaitUntil(() => faceDetected);

        float totalTime = CalculateDuration(sentence);
        isPlaying = true;
        StartCoroutine(CountdownRoutine(totalTime));
        yield return StartCoroutine(PlayLettersRoutine(sentence));

        isPlaying = false;

        if (countdownText != null)
            countdownText.text = "Done";

        if (handAnimator != null)
            handAnimator.Play("Default");
    }

    IEnumerator PlayLettersRoutine(string sentence)
    {
        sentence = sentence.ToUpper();

        foreach (char c in sentence)
        {
            if (!faceDetected)
            {
                if (countdownText != null)
                    countdownText.text = "Face lost...";

                yield return new WaitUntil(() => faceDetected);
            }

            if (!isPlaying) yield break;

            if (char.IsLetter(c))
            {
                if (handAnimator != null)
                    handAnimator.Play("ASL_" + c);
                yield return new WaitForSeconds(letterDelay);
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
            if (char.IsLetter(c)) total += letterDelay;
            else if (c == ' ') total += letterDelay * 1.5f;
        }
        return total;
    }

    public void SetFaceDetected(bool detected)
    {
        faceDetected = detected;
    }
}