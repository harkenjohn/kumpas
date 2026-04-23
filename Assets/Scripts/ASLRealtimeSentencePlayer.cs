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
        { "THANK YOU",        "ASL_ThankYou" },
        { "THANKS",           "ASL_ThankYou" },
        { "YOU'RE WELCOME",   "ASL_YoureWelcome" },
        { "YOURE WELCOME",    "ASL_YoureWelcome" },
        { "WALANG ANUMAN",    "ASL_WalangAnuman" },
        { "YES",              "ASL_Yes" },
        { "NO",               "ASL_No" },
        { "GOOD AFTERNOON",   "ASL_GoodAfternoon" },
        { "GOOD MORNING",     "ASL_GoodMorning" },
        { "GOOD EVENING",     "ASL_GoodEvening" },
        { "MAGANDANG HAPON",  "ASL_MagandangHapon" },
        { "MAGANDANG UMAGA",  "ASL_MagandangUmaga" },
        { "I'M FINE",         "ASL_ImFine" },
        { "IM FINE",          "ASL_ImFine" },
        { "HELLO",            "ASL_Hello" },
        { "HI",               "ASL_Hello" },
        { "KUMUSTA KA",       "ASL_KumustaKa" },
        { "HOW ARE YOU",      "ASL_HowAreYou" },
        { "SEE YOU TOMORROW", "ASL_SeeYouTomorrow" },
        { "NICE TO MEET YOU", "ASL_NiceToMeetYou" },
    };

    void Awake() { _instance = this; }
    void OnEnable() { _instance = this; }

    void Start()
    {
        FindUIElements();
    }

    // Only targets "GameObject" — other hand objects are ignored
    Animator GetActiveHandAnimator()
    {
        Animator[] all = FindObjectsOfType<Animator>();
        foreach (Animator a in all)
        {
            if (a.gameObject.name == "GameObject" && a.gameObject.activeInHierarchy)
                return a;
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
    // Each token is either a whole-word sign name (string) or a
    // single character (char) to be fingerspelled.
    // e.g. "Hello 3" → ["ASL_Hello", '3']
    // ─────────────────────────────────────────────────────────────
    List<object> Tokenise(string sentence)
    {
        List<object> tokens = new List<object>();
        string upper = sentence.ToUpper().Trim();

        // Sort keys by descending length so longer phrases match first
        List<string> keys = new List<string>(wordSignMap.Keys);
        keys.Sort((a, b) => b.Length.CompareTo(a.Length));

        int i = 0;
        while (i < upper.Length)
        {
            bool matched = false;

            foreach (string key in keys)
            {
                if (i + key.Length <= upper.Length &&
                    upper.Substring(i, key.Length) == key)
                {
                    int end      = i + key.Length;
                    bool endOk   = end >= upper.Length || !char.IsLetterOrDigit(upper[end]);
                    bool startOk = i == 0             || !char.IsLetterOrDigit(upper[i - 1]);

                    if (endOk && startOk)
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

        // Return to the true idle/default state when fully done
        Animator anim = GetActiveHandAnimator();
        if (anim != null) anim.Play("Default");
    }

    // Returns true if the token is a fingerspellable letter or digit (1–9)
    bool IsAlphaNumericToken(object token)
    {
        if (token is char c)
            return char.IsLetter(c) || (char.IsDigit(c) && c >= '1' && c <= '9');
        return false;
    }

    IEnumerator PlayTokensRoutine(List<object> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            object token = tokens[i];

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
                // ── Whole-word sign (no connector needed) ──
                yield return StartCoroutine(PlayAnimationState(stateName));
                yield return new WaitForSeconds(0.2f);
            }
            else if (token is char c)
            {
                if (char.IsLetter(c) || (char.IsDigit(c) && c >= '1' && c <= '9'))
                {
                    // Check whether the NEXT token is also a letter/digit.
                    // If not, this is the "last" in the fingerspell run → skip connector.
                    bool nextIsAlphaNum = (i + 1 < tokens.Count) && IsAlphaNumericToken(tokens[i + 1]);
                    yield return StartCoroutine(PlayAlphaNumericWithConnector("ASL_" + c, !nextIsAlphaNum));
                }
                else if (c == ' ')
                {
                    yield return new WaitForSeconds(letterDelay * 1.5f);
                }
                // '0' and other chars are skipped (no ASL_0 defined)
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Plays the alpha/numeric sign, then ASL_DefaultAlphaNumeric as
    // a connector OUT — unless it is the last token, in which case
    // it returns straight to Default.
    // Call with isLast = true for the final letter/digit.
    // Pattern: A → connector → D → connector → … → N → Default
    // ─────────────────────────────────────────────────────────────
    IEnumerator PlayAlphaNumericWithConnector(string targetState, bool isLast)
    {
        // 1. Play the actual letter / digit
        yield return StartCoroutine(PlayAnimationState(targetState));
        //yield return new WaitForSeconds(0.1f);

        if (isLast)
        {
            // 2a. Last sign — return to true idle Default
            Animator anim = GetActiveHandAnimator();
            if (anim != null)
            {
                anim.Play("Default", 0, 0f);
                yield return null;
            }
        }
        else
        {
            // 2b. Not last — play connector as transition to next sign
            yield return StartCoroutine(PlayAnimationState("ASL_DefaultAlphaNumeric"));
        }
    }

    // Plays one animation state and waits for it to complete.
    // Re-acquires the animator each frame in case it becomes inactive.
    IEnumerator PlayAnimationState(string stateName)
    {
        // Wait until a hand animator is available
        yield return new WaitUntil(() => GetActiveHandAnimator() != null);

        Animator anim = GetActiveHandAnimator();
        anim.Play("Default", 0, 0f);
        yield return null;
        anim.Play(stateName, 0, 0f);
        yield return null;

        // Wait for the animation to finish
        yield return new WaitUntil(() =>
        {
            Animator current = GetActiveHandAnimator();
            if (current == null) return false;

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
            if (token is string)
            {
                total += letterDelay * 3f;          // whole-word signs ~3× longer
            }
            else if (token is char c)
            {
                if (char.IsLetter(c))
                    total += (letterDelay + 0.1f) * 2f; // connector + letter
                else if (char.IsDigit(c) && c >= '1' && c <= '9')
                    total += (letterDelay + 0.1f) * 2f; // connector + digit
                else if (c == ' ')
                    total += letterDelay * 1.5f;
            }
        }
        return total;
    }

    public void SetFaceDetected(bool detected) { faceDetected = detected; }
}