// The panel behind the version number: five taps on it opens the full records, the save tools
// and the test buttons, as the web's secret panel does. Grouped by what pressing one DOES: the
// save row (the destructive one asks twice), the hazards and events, and the set pieces.
using UnityEngine;
using UnityEngine.UI;

public class SecretPanel : MonoBehaviour
{
    public System.Action onClose, onChange;
    public System.Action<string> onTest;      // "wave", "whirl", "ufo", "fork", "dive", "drop": start a run with it
    GameObject pane; RectTransform content; ScrollRect scroll;
    bool claimArmed, wipeArmed;
    public bool Open => pane != null && pane.activeSelf;

    public void Build(Transform canvas)
    {
        pane = Pane.Build(canvas, "secret", "Records", () => { Show(false); onClose?.Invoke(); }, out content, out scroll);
    }

    public void Show(bool on) { pane.SetActive(on); if (on) { claimArmed = wipeArmed = false; Fill(); } }

    void Fill()
    {
        Pane.Clear(content);
        var s = SaveData.S;
        Pane.Note(content, "v" + Application.version + " · on this device only", 22);
        Pane.Heading(content, "Save");
        var c1 = Pane.Card(content, "save", 52);
        Btn(c1, "Copy", 12f, 110, false, () =>
        {
            GUIUtility.systemCopyBuffer = JsonUtility.ToJson(s, true);
            Toast("Copied");
        });
        int left = 0; foreach (var b in BoardCatalog.All) if (!b.hidden && !SaveData.Owns(b.id)) left++;
        Btn(c1, claimArmed ? "Sure?" : (left > 0 ? "Claim " + left + " boards" : "All boards owned"), 130f, 150, left > 0, () =>
        {
            if (left == 0) return;
            if (!claimArmed) { claimArmed = true; Fill(); return; }
            foreach (var b in BoardCatalog.All) if (!b.hidden && !SaveData.Owns(b.id)) SaveData.S.owned.Add(b.id);
            SaveData.Flush(); claimArmed = false; onChange?.Invoke(); Fill();
        });
        Btn(c1, wipeArmed ? "Sure?" : "Reset", 288f, 100, true, () =>
        {
            if (!wipeArmed) { wipeArmed = true; Fill(); return; }
            SaveData.Wipe(); wipeArmed = false; onChange?.Invoke(); Fill();
        });

        Pane.Heading(content, "Hazards & events");
        var c2 = Pane.Card(content, "hazards", 52);
        Btn(c2, "Test wave", 12f, 116, true, () => Test("wave"));
        Btn(c2, "Test whirl", 134f, 116, true, () => Test("whirl"));
        Btn(c2, "UFO", 256f, 64, true, () => Test("ufo"));
        Btn(c2, "Boat", 326f, 64, true, () => Test("boat"));

        Pane.Heading(content, "Set pieces — test only, not in the game");
        var c3 = Pane.Card(content, "pieces", 52);
        Btn(c3, "Fork", 12f, 100, true, () => Test("fork"));
        Btn(c3, "Dive", 118f, 100, true, () => Test("dive"));
        Btn(c3, "The Drop", 224f, 120, true, () => Test("drop"));

        Pane.Heading(content, "Runs");
        int avg = s.runs > 0 ? s.totalDist / s.runs : 0;
        Pane.Row(content, "Runs", Pane.N(s.runs));
        Pane.Row(content, "Best distance", Pane.N(s.bestDist) + " m");
        Pane.Row(content, "Average run", Pane.N(avg) + " m");
        Pane.Row(content, "Total distance", Pane.N(s.totalDist) + " m");
        Pane.Row(content, "Best trick score", Pane.N(s.best));
        Pane.Row(content, "Total score", Pane.N(s.totalScore));
        Pane.Row(content, "Time on the water", Pane.Hhmm(s.secs));
        Pane.Row(content, "Revives", Pane.N(s.revives));
        Pane.Heading(content, "Along the way");
        Pane.Row(content, "Tricks landed", Pane.N(s.tricks));
        Pane.Row(content, "Chests", Pane.N(s.crates));
        Pane.Row(content, "Close calls", Pane.N(s.closeCalls));
        Pane.Row(content, "Barrels", Pane.N(s.barrels));
        Pane.Row(content, "Jellyfish", Pane.N(s.jellies));
        Pane.Row(content, "Ramps", Pane.N(s.ramps));
        Pane.Row(content, "Dives", Pane.N(s.dives));
        Pane.Row(content, "Pearls", Pane.N(s.pearls));
        Pane.Heading(content, "Puka");
        Pane.Row(content, "Earned", Pane.N(s.earned));
        Pane.Row(content, "Purse now", Pane.N(s.coins));
        Pane.Heading(content, "Collection");
        int nB = s.owned.Count, nC = s.ownedChars.Count, allB = 0;
        foreach (var b in BoardCatalog.All) if (!b.hidden) allB++;
        Pane.Bar(content, "Boards owned", nB + " / " + allB, nB / (float)Mathf.Max(1, allB));
        Pane.Bar(content, "Surfers owned", nC + " / " + RiderCatalog.All.Count, nC / (float)Mathf.Max(1, RiderCatalog.All.Count));
        Pane.Row(content, "Riding", BoardCatalog.Get(s.equipped).name + "  ·  " + RiderCatalog.Get(s.rider).name);
        scroll.verticalNormalizedPosition = 1f;
    }

    void Test(string what) { Sfx.Buzz(10); Show(false); onTest?.Invoke(what); }

    Text toast; float toastT;
    void Toast(string msg)
    {
        if (toast == null)
        {
            toast = UiKit.Label(pane.transform, "", 14, UiKit.GoldText, FontStyle.Bold, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(360, 24), TextAnchor.MiddleCenter, 0.6f);
        }
        toast.text = msg; toast.gameObject.SetActive(true); toastT = 1.6f;
    }

    void Update()
    {
        if (toastT > 0f) { toastT -= Time.unscaledDeltaTime; if (toastT <= 0f && toast != null) toast.gameObject.SetActive(false); }
    }

    static void Btn(Transform card, string label, float x, float w, bool lit, System.Action tap)
    {
        var btn = UiKit.Pic(card, "btn", UiKit.Rounded(w, 34, 17, 17, 17, 17, lit ? UiKit.Body.Teal : UiKit.Body.Pill, lit ? 2f : 0f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(w, 34));
        UiKit.Label(btn.transform, label, 12.5f, lit ? UiKit.GoldText : new Color(1, 1, 1, 0.7f), FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        btn.raycastTarget = true;
        UiKit.Pressable(btn.gameObject, null, tap);
    }
}
