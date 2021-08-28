using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class ComplianceScript : MonoBehaviour
{
    enum btnColors
    {
        Red,
        Blue
    }
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMNeedyModule Module;
    public KMSelectable[] buttons;
    public TextMesh[] yesNoTexts;
    public TextMesh[] redNumbers, blueNumbers;
    public TextMesh[] faceNumbers;

    private static readonly string[] strings = { "YNB+B", "BR+YN", "RNYN-", "-BY+-", "+YBNB", "B--RY", "YBNRB", "Y-BR+", "+Y-BR", "N+YYB", "+BRYN", "Y-+NB", "-BNYR", "--RYN", "BRYN+", "+-NYR", "-RYNY", "NBNYN", "Y-BRY", "N-BRY", "BNR+B", "-R+YR", "N++RY", "R++DB", "RBNB-", "RBRYR", "NR+Y-", "RYRBB", "NYRBY", "RYN-B", "-BYB-", "BYR-+", "BYBNB", "YNB++", "YBR+N", "YNNY+", "B++NY", "B-Y+-", "+RYBR", "+BRYN", "+-B+Y", "+BNYB", "+-BBR", "-+-Y+", "Y+N+R", "+YRB+", "YB+NB", "YN-RB", "Y-NB-", "R+-BY", "R+YNY", "NBYRB", "R-BN-", "BR-NR", "BR-YN", "YBRN+", "--BRY", "BN+YR", "NB+-R", "BRNBY", "RN+Y-", "B-NYN", "RYNBB", "BY+RN" };

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;
    private bool active;
    private int[] dispNums;
    private int solutionPointer;
    private int tablePointer = -1;
    private int[] order;
    private bool TwitchPlaysActive;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        
        Module.OnNeedyActivation += Activate;
        Module.OnNeedyDeactivation += Deactivate;
        Module.OnTimerExpired += TimeOut;
        Module.OnActivate += CheckTP;

        buttons[0].OnInteract += delegate () { ButtonPress(0); return false; };
        buttons[1].OnInteract += delegate () { ButtonPress(1); return false; };

    }

    void Start()
    {
        if (Bomb.GetIndicators().Count() > 3)
            order = Enumerable.Range(0, 64).ToArray();
        else if (Bomb.GetBatteryCount() > 3)
            order = Enumerable.Range(0, 64).Select(x => 8 * (x % 8) + (x / 8)).ToArray();
        else if (Bomb.GetPortCount() > 6)
            order = Enumerable.Range(0, 64).Reverse().ToArray();
        else order = Enumerable.Range(0, 64).Select(x => 8 * (x % 8) + (x / 8)).Reverse().ToArray();
        Debug.LogFormat("[Compliance #{0}] The first used string is string {1} in reading order, while the second is string {2}.", moduleId, order[0] + 1, order[1] + 1);
    }
    void ButtonPress(int ix)
    {
        if (!active)
            return;
        buttons[ix].AddInteractionPunch(0.75f);
        Audio.PlaySoundAtTransform("CompButton", buttons[ix].transform);
        Debug.LogFormat("[Compliance #{0}] Pressed the {1} button.", moduleId, ix == 0 ? "red" : "blue");
        if (IsValid(ix))
        {
            solutionPointer++;
            if (solutionPointer == 5)
            {
                Debug.LogFormat("[Compliance #{0}] All commands performed, deactivating.", moduleId);
                Audio.PlaySoundAtTransform("CompPass", transform);
                Module.OnPass();
                Deactivate();
            }
        }
        else
        {
            Debug.LogFormat("[Compliance #{0}] Pressed the {1} button, while expected {2}. Strike!", moduleId, ix == 0 ? "red" : "blue", ix == 0 ? "blue" : "red");
            Module.OnStrike();
            StartCoroutine(SpeakNum());
            Deactivate();
        }
    }
    bool IsValid(int ix)
    {
        switch (strings[order[tablePointer]][solutionPointer])
        {
            case 'R': return ix == 0;
            case 'B': return ix == 1;
            case 'Y': return yesNoTexts[ix].text == "YES";
            case 'N': return yesNoTexts[ix].text == "NO";
            case '+': return dispNums[ix] > dispNums[1 - ix];
            case '-': return dispNums[ix] < dispNums[1 - ix];
            default: throw new ArgumentException("Unexpected character in instruction string '" + strings[order[tablePointer]][solutionPointer] + "'");
        }
    }
    void CheckTP()
    {
        if (TwitchPlaysActive)
        {
            yesNoTexts[0].transform.localScale = 0.25f * Vector3.one;
            yesNoTexts[1].transform.localScale = 0.25f * Vector3.one;
            yesNoTexts[0].transform.localPosition += 0.02f * Vector3.down;
            yesNoTexts[1].transform.localPosition += 0.02f * Vector3.down;
        }
    }
    void Activate()
    {
        if (active)
            return;
        active = true;
        solutionPointer = 0;

        if (Rnd.Range(0,2) == 0)
        {
            yesNoTexts[0].text = "YES";
            yesNoTexts[1].text = "NO";
        }
        else
        {
            yesNoTexts[0].text = "NO";
            yesNoTexts[1].text = "YES";
        }
        dispNums = Enumerable.Range(0, 10).ToArray().Shuffle().Take(2).ToArray();
        for (int i = 0; i < 6; i++)
        {
            redNumbers[i].gameObject.SetActive(false);
            blueNumbers[i].gameObject.SetActive(false);
        }
        int redPos = Rnd.Range(0, 6);
        int bluePos = Rnd.Range(0, 6);
        TextMesh[] modifiedTexts = TwitchPlaysActive ?
            new[] { faceNumbers[0], faceNumbers[1] } :
            new[] { redNumbers[redPos], blueNumbers[bluePos] };

        modifiedTexts[0].gameObject.SetActive(true);
        modifiedTexts[1].gameObject.SetActive(true);
        modifiedTexts[0].text = dispNums[0].ToString();
        modifiedTexts[1].text = dispNums[1].ToString();
        SetButtonPositions();
        StartCoroutine(RevealButtons());

        tablePointer++;
        tablePointer %= 64;

        Debug.LogFormat("[Compliance #{0}] ==Activated==", moduleId);
        Debug.LogFormat("[Compliance #{0}] The red button has label {1} and number {2}.", moduleId, yesNoTexts[0].text, dispNums[0]);
        Debug.LogFormat("[Compliance #{0}] The blue button has label {1} and number {2}.", moduleId, yesNoTexts[1].text, dispNums[1]);
        Debug.LogFormat("[Compliance #{0}] The used string for this stage is {1}.", moduleId, strings[order[tablePointer]]);
    }
    void SetButtonPositions()
    {
        Vector3 redPos;
        Vector3 bluePos;
        do
        {
            redPos = new Vector3(Rnd.Range(-0.055f, 0.055f), -0.005f, Rnd.Range(0.01f, -0.055f));
            bluePos = new Vector3(Rnd.Range(-0.055f, 0.055f), -0.005f, Rnd.Range(0.01f, -0.055f));
        } while (Math.Abs(redPos.x - bluePos.x) < 0.045 || Math.Abs(redPos.z - bluePos.z) < 0.051f);
        buttons[0].transform.localPosition = redPos;
        buttons[1].transform.localPosition = bluePos;
    }
    void Deactivate()
    {
        active = false;
        StartCoroutine(RetractButtons());
    }
    void TimeOut()
    {
        Debug.LogFormat("[Compliance #{0}] Time ran out at {1}!", moduleId, Bomb.GetFormattedTime());
        Module.OnStrike();
        StartCoroutine(SpeakNum());
        Deactivate();
    }
    IEnumerator RetractButtons()
    {
        while (buttons[0].transform.localPosition.y > -0.005f && buttons[1].transform.localPosition.y > -0.005f)
        {
            buttons[0].transform.localPosition += 0.03f * Time.deltaTime * Vector3.down;
            buttons[1].transform.localPosition += 0.03f * Time.deltaTime * Vector3.down;
            yield return null;
        }
    }
    IEnumerator RevealButtons()
    {
        while (buttons[0].transform.localPosition.y < 0.0125f && buttons[1].transform.localPosition.y < 0.0125f)
        {
            buttons[0].transform.localPosition += 0.03f * Time.deltaTime * Vector3.up;
            buttons[1].transform.localPosition += 0.03f * Time.deltaTime * Vector3.up;
            yield return null;
        }
    }
    IEnumerator SpeakNum()
    {
        foreach (char letter in (tablePointer + 1).ToString())
        {
            Audio.PlaySoundAtTransform(letter.ToString(), transform);
            yield return new WaitForSeconds(1);
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use <!{0} press BR> to press the blue button, then the red button. Other aliases are not allowed. .";
#pragma warning restore 414

    IEnumerator Press(KMSelectable btn, float delay = 0.1f)
    {
        btn.OnInteract();
        yield return new WaitForSeconds(delay);
    }

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToUpperInvariant();
        Match m = Regex.Match(command, @"^(?:PRESS\s+|SUBMIT\s+)?((?:[BR]\s*)+)$");
        if (m.Success)
        {
            yield return null;
            foreach (char btn in m.Groups[1].Value.Where(x => !char.IsWhiteSpace(x)))
                yield return Press(buttons[btn == 'R' ? 0 : 1], 0.2f);
        }
    }

    void TwitchHandleForcedSolve()
    {
        StartCoroutine(Autosolve());
    }
    IEnumerator Autosolve()
    {
        while (true)
        {
            while (active)
                if (IsValid(0))
                    yield return Press(buttons[0], 0.2f);
                else yield return Press(buttons[1], 0.2f);
            yield return null;
        }
    }
}
