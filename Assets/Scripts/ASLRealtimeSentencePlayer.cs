using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ASLRealtimeSentencePlayer : MonoBehaviour
{
    [Header("Timing")]
    public float delayBeforeStart = 2f;
    public float letterDelay = 0.7f;

    private static ASLRealtimeSentencePlayer _instance;
    public static ASLRealtimeSentencePlayer Instance => _instance;

    private TMP_Text messageDisplayText;
    private TMP_Text countdownText;

    private bool faceDetected = false;
    private bool isPlaying = false;
    private Coroutine currentRoutine;

    // Known whole-word ASL animations (uppercase keys for easy matching)
    private static readonly Dictionary<string, string> wordSignMap = new Dictionary<string, string>()
    {
        { "THANK YOU",       "ASL_ThankYou" },
        { "THANKS",          "ASL_ThankYou" },
        { "YOU'RE WELCOME",  "ASL_YoureWelcome" },
        { "YOURE WELCOME",   "ASL_YoureWelcome" },
        { "WALANG ANUMAN",   "ASL_WalangAnuman" },
        { "YES",             "ASL_Yes" },
        { "NO",              "ASL_No" },
        { "GOOD AFTERNOON",  "ASL_GoodAfternoon" },
        { "GOOD MORNING",    "ASL_GoodMorning" },
        { "GOOD EVENING",    "ASL_GoodEvening" },
        { "MAGANDANG HAPON", "ASL_MagandangHapon" },
        { "MAGANDANG UMAGA", "ASL_MagandangUmaga" },
        { "I'M FINE",        "ASL_ImFine" },
        { "IM FINE",         "ASL_ImFine" },
        { "HELLO",           "ASL_Hello" },
        { "HI",              "ASL_Hello" },
        { "KUMUSTA KA",      "ASL_KumustaKa" },
        { "HOW ARE YOU",     "ASL_HowAreYou" },
    };

    void Awake() { _instance = this; }
    void OnEnable() { _instance = this; }

    void Start()
    {
        FindUIElements();
    }

    // Always get the currently ACTIVE animator at runtime — never cache it
    Animator GetActiveHandAnimator()
    {
        Animator[] all = FindObjectsOfType<Animator>();
        foreach (Animator a in all)
        {
            if ((a.gameObject.name == "Player hand" || a.gameObject.name == "MyRiggedArms2" || a.gameObject.name == "GameObject")
                && a.gameObject.activeInHierarchy)
            {
                return a;
            }
        }
        return null;
    }

    void FindUIElements()
    {
        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (TMP_Text t in allTexts)
        {
            if (t.gameObject.name == "MessageDisplayText") messageDisplayText = t;
            if (t.gameObject.name == "CountdownText")      countdownText = t;
        }
    }

    public void RegisterInstance() { _instance = this; }

    public void PlaySentence(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        if (messageDisplayText == null || countdownText == null) FindUIElements();
        if (messageDisplayText != null) messageDisplayText.text = message;
        if (countdownText != null)      countdownText.text = "";

        if (currentRoutine != null) StopCoroutine(currentRoutine);
        currentRoutine = StartCoroutine(MasterRoutine(message));
    }

    // ─────────────────────────────────────────────────────────────
    // Tokenise the sentence into a list of "tokens".
    // Each token is either a whole-word sign name or a single char.
    // e.g. "Hello Adrian" → ["ASL_Hello", 'A','D','R','I','A','N']
    // ─────────────────────────────────────────────────────────────
    List<object> Tokenise(string sentence)
    {
        List<object> tokens = new List<object>();
        string upper = sentence.ToUpper().Trim();

        int i = 0;
        while (i < upper.Length)
        {
            bool matched = false;

            // Try to match multi-word phrases first (longest match wins)
            // Sort by descending key length so "GOOD MORNING" beats "GOOD"
            List<string> keys = new List<string>(wordSignMap.Keys);
            keys.Sort((a, b) => b.Length.CompareTo(a.Length));

            foreach (string key in keys)
            {
                if (i + key.Length <= upper.Length &&
                    upper.Substring(i, key.Length) == key)
                {
                    // Make sure we're not cutting mid-word (check boundary)
                    int end = i + key.Length;
                    bool boundaryOk = end >= upper.Length || !char.IsLetterOrDigit(upper[end]);
                    bool startOk    = i == 0            || !char.IsLetterOrDigit(upper[i - 1]);

                    if (boundaryOk && startOk)
                    {
                        tokens.Add(wordSignMap[key]); // whole-word animation name
                        i += key.Length;
                        matched = true;
                        break;
                    }
                }
            }

            if (!matched)
            {
                tokens.Add(upper[i]); // raw char — fingerspell
                i++;
            }
        }

        return tokens;
    }

    IEnumerator MasterRoutine(string sentence)
    {
        isPlaying = false;
        yield return new WaitForSeconds(delayBeforeStart);

        if (countdownText != null) countdownText.text = "Waiting for face...";
        yield return new WaitUntil(() => faceDetected);

        List<object> tokens = Tokenise(sentence);
        float totalTime = CalculateDurationFromTokens(tokens);

        isPlaying = true;
        StartCoroutine(CountdownRoutine(totalTime));
        yield return StartCoroutine(PlayTokensRoutine(tokens));

        isPlaying = false;
        if (countdownText != null) countdownText.text = "Done";

        Animator anim = GetActiveHandAnimator();
        if (anim != null) anim.Play("Default");
    }

    IEnumerator PlayTokensRoutine(List<object> tokens)
    {
        foreach (object token in tokens)
        {
            // Pause if face is lost
            if (!faceDetected)
            {
                if (countdownText != null) countdownText.text = "Face lost...";
                yield return new WaitUntil(() => faceDetected);
                yield return new WaitForSeconds(0.3f);
            }

            if (!isPlaying) yield break;

            if (token is string stateName)
            {
                // ── Whole-word sign ──
                yield return StartCoroutine(PlayAnimationState(stateName));
                yield return new WaitForSeconds(0.2f); // brief pause after word sign
            }
            else if (token is char c)
            {
                if (char.IsLetter(c))
                {
                    // ── Fingerspell single letter ──
                    string stateName2 = "ASL_" + c;
                    yield return StartCoroutine(PlayAnimationState(stateName2));
                    yield return new WaitForSeconds(0.1f);
                }
                else if (c == ' ')
                {
                    yield return new WaitForSeconds(letterDelay * 1.5f);
                }
            }
        }
    }

    // Plays one animation state on whichever hand is currently active.
    // If the hand goes inactive mid-animation, waits until it returns.
    IEnumerator PlayAnimationState(string stateName)
    {
        // Wait until a hand is available
        yield return new WaitUntil(() => GetActiveHandAnimator() != null);

        Animator anim = GetActiveHandAnimator();

        anim.Play("Default", 0, 0f);
        yield return null;
        anim.Play(stateName, 0, 0f);
        yield return null;

        // Wait for the animation to finish, re-acquiring the animator each frame
        yield return new WaitUntil(() =>
        {
            Animator current = GetActiveHandAnimator();
            if (current == null) return false; // hand switched / went inactive

            var state = current.GetCurrentAnimatorStateInfo(0);
            return state.IsName(stateName) && state.normalizedTime >= 1f;
        });
    }

    IEnumerator CountdownRoutine(float duration)
    {
        float timer = duration;
        while (timer > 0 && isPlaying)
        {
            if (faceDetected) timer -= Time.deltaTime;
            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(timer).ToString() + "s";
            yield return null;
        }
    }

    float CalculateDurationFromTokens(List<object> tokens)
    {
        float total = 0f;
        foreach (object token in tokens)
        {
            if (token is string)          total += letterDelay * 3f; // word signs take ~3× longer
            else if (token is char c)
            {
                if (char.IsLetter(c))     total += letterDelay + 0.1f;
                else if (c == ' ')        total += letterDelay * 1.5f;
            }
        }
        return total;
    }

    public void SetFaceDetected(bool detected) { faceDetected = detected; }
}