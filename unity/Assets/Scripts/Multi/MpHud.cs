// The multiplayer screens, on the HUD's canvas: a "Play with friends" button on the title, the mode
// picker (Race, Together, Brawl; how many others), a status line along the top during a round (your
// place and the metres to go, the crew, your hearts), PUNCH and KICK over JUMP in a brawl, and the
// result when the round is over.
using UnityEngine;
using UnityEngine.UI;

public class MpHud : MonoBehaviour
{
    public Multi mp; public Hud hud; public BoardController rider;
    public System.Action<MpMode, int> onStart;      // the picker's choice: the round starts
    public System.Action onDone;                    // the result closed: back to the title

    RectTransform root; GameObject titleBtn, picker, result, brawl; Text status, resTitle, resRows;
    int count = 3;
    Text countLabel;

    public void Build()
    {
        root = UiKit.Rect(hud.UiRoot, "multi", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        // ---- the title's button: under the bar, centred ----
        titleBtn = Button(root, "Play with friends", new Vector2(0.5f, 0f), new Vector2(0, 52), new Vector2(230, 54), () => { Sfx.Buzz(10); ShowPicker(true); }, 19).gameObject;
        // ---- the picker ----
        picker = UiKit.Rect(root, "picker", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        var dim = picker.AddComponent<Image>(); dim.color = new Color(0.02f, 0.08f, 0.16f, 0.86f);
        UiKit.Label(picker.transform, "PLAY WITH FRIENDS", 26, UiKit.TrickYellow, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(400, 40));
        UiKit.Label(picker.transform, "Bots ride with you for now. Online friends are coming next.", 14, Pane.Faint, FontStyle.Normal, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(380, 40));
        ModeButton("RACE", "First to 1000 m wins. Wipe out and you're back on your board.", -230, MpMode.Race);
        ModeButton("TOGETHER", "One team. A fallen surfer is back in 8 s while anyone's still up. Go as far as you can.", -360, MpMode.Together);
        ModeButton("BRAWL", "Punch and kick rivals off their boards. Three hearts each. Last one standing wins.", -490, MpMode.Brawl);
        // how many others
        countLabel = UiKit.Label(picker.transform, "", 18, Color.white, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -610), new Vector2(300, 30));
        Button(picker.transform, "-", new Vector2(0.5f, 1f), new Vector2(-120, -640), new Vector2(54, 44), () => { count = Mathf.Max(1, count - 1); Count(); }, 24);
        Button(picker.transform, "+", new Vector2(0.5f, 1f), new Vector2(120, -640), new Vector2(54, 44), () => { count = Mathf.Min(3, count + 1); Count(); }, 24);
        Count();
        Button(picker.transform, "Back", new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(160, 46), () => ShowPicker(false), 17);
        picker.SetActive(false);
        // ---- during a round ----
        status = UiKit.Label(root, "", 16, Color.white, FontStyle.Bold, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -92), new Vector2(400, 26), TextAnchor.MiddleCenter, 0.8f);
        status.raycastTarget = false;
        brawl = UiKit.Rect(root, "brawl", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0, 146), new Vector2(106, 170)).gameObject;
        AttackButton(brawl.transform, "KICK", 0f, true);
        AttackButton(brawl.transform, "PUNCH", 88f, false);
        brawl.SetActive(false);
        // ---- the result ----
        result = UiKit.Rect(root, "result", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        var rdim = result.AddComponent<Image>(); rdim.color = new Color(0.02f, 0.08f, 0.16f, 0.88f);
        resTitle = UiKit.Label(result.transform, "", 28, UiKit.TrickYellow, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(400, 44));
        resRows = UiKit.Label(result.transform, "", 18, Color.white, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, 120), new Vector2(380, 260), TextAnchor.UpperCenter);
        Button(result.transform, "Back to the beach", new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(240, 54), () => { result.SetActive(false); onDone?.Invoke(); }, 18);
        result.SetActive(false);
    }

    void Count() { countLabel.text = count == 1 ? "1 other surfer" : count + " other surfers"; }

    void ModeButton(string title, string blurb, float y, MpMode m)
    {
        var b = Button(picker.transform, "", new Vector2(0.5f, 1f), new Vector2(0, y), new Vector2(360, 110), () => { Sfx.Buzz(12); ShowPicker(false); onStart?.Invoke(m, count); }, 24);
        UiKit.Label(b.transform, title, 26, UiKit.TrickYellow, FontStyle.Bold, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(0, 34));
        UiKit.Label(b.transform, blurb, 14, Color.white, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0, -16), new Vector2(-28, -44));
    }

    void AttackButton(Transform parent, string label, float y, bool kick)
    {
        var a = UiKit.Pic(parent, label, UiKit.Rounded(106, 76, 24, 0, 0, 24, UiKit.Body.Glass, 2.5f, false, true), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, y), new Vector2(106, 76));
        UiKit.Label(a.transform, label, 17, kick ? new Color(1f, 0.6f, 0.4f) : Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        a.raycastTarget = true;
        UiKit.Pressable(a.gameObject, () => mp.LocalAttack(kick));
    }

    Image Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, System.Action tap, float font)
    {
        var b = UiKit.Pic(parent, label, UiKit.Rounded(size.x, size.y, 16, 16, 16, 16, UiKit.Body.Glass, 2.5f), anchor, anchor, new Vector2(0.5f, anchor.y >= 1f ? 1f : anchor.y <= 0f ? 0f : 0.5f), pos, size);
        if (!string.IsNullOrEmpty(label)) UiKit.Label(b.transform, label, font, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        b.raycastTarget = true;
        UiKit.Pressable(b.gameObject, null, tap);
        return b;
    }

    public void ShowPicker(bool on) { if (picker != null) picker.SetActive(on); }
    public bool PickerOpen => picker != null && picker.activeSelf;
    public bool ResultOpen => result != null && result.activeSelf;

    /// on the title (true) the button shows; in a round, the status line and, in a brawl, the buttons
    public void Title(bool on) { if (titleBtn != null) titleBtn.SetActive(on && !PickerOpen); }

    public void ShowResult(MpResult r)
    {
        resTitle.text = r.title;
        resRows.text = string.Join("\n", r.rows) + "\n\n+" + r.coins + " puka";
        result.SetActive(true);
        brawl.SetActive(false); status.text = "";
    }

    public void Step(bool riding)
    {
        if (titleBtn != null && titleBtn.activeSelf) titleBtn.SetActive(false);
        bool live = mp != null && mp.Running && riding;
        if (brawl.activeSelf != (live && mp.mode == MpMode.Brawl && !mp.localOut)) brawl.SetActive(live && mp.mode == MpMode.Brawl && !mp.localOut);
        if (!live) { if (status.text.Length > 0) status.text = ""; return; }
        if (mp.mode == MpMode.Race)
        {
            int place = 1; foreach (var o in mp.others) if (o.dist > rider.dist || o.finished) place++;
            status.text = "Place " + place + "/" + (mp.others.Count + 1) + "   " + Mathf.Max(0, Mathf.RoundToInt(Multi.RaceGoal - rider.dist)) + " m to go";
        }
        else if (mp.mode == MpMode.Together)
        {
            int up = rider.wipe ? 0 : 1; foreach (var o in mp.others) if (!o.down) up++;
            status.text = "Crew up: " + up + "/" + (mp.others.Count + 1) + "   " + Mathf.RoundToInt(rider.dist) + " m";
        }
        else
        {
            int left = mp.localOut ? 0 : 1; foreach (var o in mp.others) if (!o.outOfGame) left++;
            status.text = "You " + new string('♥', Mathf.Max(0, mp.localHearts)) + "   " + left + " left";
        }
    }
}
