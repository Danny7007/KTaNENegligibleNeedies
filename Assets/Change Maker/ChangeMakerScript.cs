using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;

public class ChangeMakerScript : MonoBehaviour {
    enum MoneyUnits
    {
        Quarter,
        Dime,
        Nickel,
        Penny,
        One,
        Five,
        Ten
    }
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMNeedyModule Module;
    public KMSelectable[] moneyButtons;
    public KMSelectable redo, ok;
    public TextMesh costText;
    public Sprite[] moneySprites;
    public SpriteRenderer[] paidMoneySprites;
    public List<SpriteRenderer> inputtedDollarSprites = new List<SpriteRenderer>();
    public List<SpriteRenderer> coins = new List<SpriteRenderer>();

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    private static decimal[] moneyVals = new Decimal[] { 0.25M, 0.1M, 0.05M, 0.01M, 1, 5, 10 };
    private static decimal cost, amountPaid, inputtedAmount, correctChange;
    private bool active;
    private int coinCount, dollarCount;
    private bool TwitchPlaysActive;

    void Awake () {
        moduleId = moduleIdCounter++;
        for (int i = 0; i < 5; i++)
        {
            int ix = i;
            moneyButtons[ix].OnInteract += delegate () { AddMoney(ix, moneyVals[ix]); return false; };
        }
        redo.OnInteract += delegate () { Redo(); return false; };
        ok.OnInteract += delegate () { Submit(); return false; };
        
        Module.OnNeedyActivation += Activate;
        Module.OnNeedyDeactivation += Deactivate;
        Module.OnTimerExpired += TimeOut;
        Clear(true);
    }
    void Redo()
    {
        redo.AddInteractionPunch(0.75f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, redo.transform);
        Clear(false);
    }
    void Submit()
    {
        ok.AddInteractionPunch(0.75f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ok.transform);
        if (!active)
            return;
        if (inputtedAmount == correctChange)
        {
            Debug.LogFormat("[Change Maker #{0}] Submitted {1}. Module deactivated.", moduleId, MoneyFormat(inputtedAmount));
            Module.HandlePass();
            active = false;
        }
        else
        {
            Debug.LogFormat("[Change Maker #{0}] Submitted {1} when expected {2}, strike!", moduleId, MoneyFormat(inputtedAmount), MoneyFormat(correctChange));
            Module.OnStrike();
            Module.OnPass();
            active = false;
        }
    }

    void Activate()
    {
        if (active)
            return;
        active = true;
        do amountPaid = (decimal)Rnd.Range(1, 31);
        while (ConvertToCurrency(amountPaid).Count() > 3);
        do cost = amountPaid - Rnd.Range(1, 200) / 100M; //Take a random value from $0.01 to $2.00
        while (cost <= 0);
        correctChange = amountPaid - cost;

        Debug.LogFormat("[Change Maker #{0}] ==Activated==", moduleId);
        Debug.LogFormat("[Change Maker #{0}] The cost of the item is {1}.", moduleId, MoneyFormat(cost));
        Debug.LogFormat("[Change Maker #{0}] The customer paid {1}.", moduleId, MoneyFormat(amountPaid));
        Debug.LogFormat("[Change Maker #{0}] The correct change is {1}.", moduleId, MoneyFormat(correctChange));
        MoneyUnits[] bills = ConvertToCurrency(amountPaid).ToArray();
        for (int i = 0; i < bills.Length; i++)
            paidMoneySprites[i].sprite = moneySprites[(int)bills[i]];
        costText.text = MoneyFormat(cost).Substring(1);
    }
    void Deactivate()
    {
        active = false;
        Clear(true);
    }
    void TimeOut()
    {
        Debug.LogFormat("[Change Maker #{0}] Time ran out at {1}!", moduleId, Bomb.GetFormattedTime());
        Module.OnStrike();
        Deactivate();
    }
    void AddMoney(int buttonIx, decimal value)
    {
        moneyButtons[buttonIx].AddInteractionPunch(0.1f);
        if (!active)
            return;
        inputtedAmount += value;
        if (value == 1)
            HandleAddDollar();
        else HandleAddCoin((MoneyUnits)buttonIx);
    }
    void HandleAddDollar()
    {
        if (dollarCount == 12)
            return;
        moneyButtons[(int)MoneyUnits.One].AddInteractionPunch(0.5f);
        Audio.PlaySoundAtTransform("bill" + Rnd.Range(0, 5), moneyButtons[(int)MoneyUnits.One].transform);
        if (dollarCount == 0)
        {
            inputtedDollarSprites[0].gameObject.SetActive(true);
            inputtedDollarSprites[0].sprite = moneySprites[(int)MoneyUnits.One];
        }
        else
        {
            SpriteRenderer orig = inputtedDollarSprites.Last();
            inputtedDollarSprites.Add(Instantiate(orig, orig.transform.parent));
            SpriteRenderer addedDollar = inputtedDollarSprites.Last();
            addedDollar.name = "Dollar" + (dollarCount + 1);
            addedDollar.transform.localPosition += 0.266f * Vector3.down;
            addedDollar.sortingOrder++;
        }
        dollarCount++;
    }
    void HandleAddCoin(MoneyUnits unit)
    {
        Audio.PlaySoundAtTransform("coin" + Rnd.Range(0, 2), moneyButtons[(int)unit].transform);
        if (coinCount == 0)
        {
            coins[0].gameObject.SetActive(true);
            coins[0].sprite = moneySprites[(int)unit];
        }
        else
        {
            SpriteRenderer orig = coins.Last();
            SpriteRenderer addedCoin = Instantiate(orig, orig.transform.parent);
            coins.Add(addedCoin);
            addedCoin.name = "Coin" + (coinCount + 1);
            addedCoin.transform.localPosition += 0.175f * Vector3.down;
            addedCoin.sortingOrder++;
            addedCoin.sprite = moneySprites[(int)unit];
        }
        coinCount++;
    }
    void Clear( bool clearPaid)
    {
        dollarCount = 0;
        coinCount = 0;
        inputtedAmount = 0;
        if (clearPaid)
        {
            costText.text = "";
            for (int i = 0; i < 3; i++)
                paidMoneySprites[i].sprite = null;
        }
        if (inputtedDollarSprites.Count != 0)
            foreach (SpriteRenderer rend in inputtedDollarSprites.Skip(1))
                Destroy(rend);
        if (coins.Count != 0)
            foreach (SpriteRenderer rend in coins.Skip(1))
                Destroy(rend);
        inputtedDollarSprites.First().gameObject.SetActive(false);
        coins.First().gameObject.SetActive(false);
        inputtedDollarSprites = new List<SpriteRenderer>() { inputtedDollarSprites.First() };
        coins = new List<SpriteRenderer>() { coins.First() };
    }

    IEnumerable<MoneyUnits> ConvertToCurrency(decimal val)
    {
        for (int i = 0; i < 3; i++)
            while (val >= moneyVals[6 - i])
            {
                yield return (MoneyUnits)(6 - i);
                val -= moneyVals[6 - i];
            }
        for (int i = 0; i < 4; i++)
            while (val >= moneyVals[i])
            {
                yield return (MoneyUnits)i;
                val -= moneyVals[i];
            }
    }

    string MoneyFormat(decimal input)
    {
        return string.Format("{0:C}", input).Trim(new[] { '(', ')' });
    }

    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use <!{0} submit OQDDNPPP> to submit those values. O = One-Dollar, Q = Quarter, D = Dime, N = Nickel, P = Penny.";
    #pragma warning restore 414

    IEnumerator Press(KMSelectable btn, float delay = 0.1f)
    {
        btn.OnInteract();
        yield return new WaitForSeconds(delay);
    }

    IEnumerator ProcessTwitchCommand (string command)
    {
        command = command.Trim().ToUpperInvariant();
        string coinAbbvs = "QDNPO";
        Match m = Regex.Match(command, @"^(?:SUBMIT\s+)?((?:[QDNPO]\s*)+)$");
        if (m.Success)
        {
            yield return null;
            if (inputtedAmount != 0)
                yield return Press(redo, 0.2f);
            foreach (char letter in m.Groups[1].Value.Where(x => !char.IsWhiteSpace(x)))
                yield return Press(moneyButtons[coinAbbvs.IndexOf(letter)], 0.1f);
            yield return Press(ok, 0.1f);
        }
    }

    void TwitchHandleForcedSolve ()
    {
        StartCoroutine(Autosolve());
    }
    IEnumerator Autosolve()
    {
        while (true)
        {
            if (active)
            {
                yield return new WaitForSeconds(0.1f);
                if (inputtedAmount > correctChange)
                    yield return Press(redo, 0.2f);
                var units = ConvertToCurrency(correctChange - inputtedAmount);
                Debug.Log(units.Join());
                foreach (MoneyUnits unit in units)
                    yield return Press(moneyButtons[(int)unit], 0.1f);
                yield return Press(ok, 0.1f);
            }
            yield return null;
        }
    }
}
