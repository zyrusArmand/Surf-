// The daily spin, ported from the web: one spin a calendar day, ten slices whose values climb
// with the rank, and prizes that are spent on the NEXT run rather than numbers going up: a
// second life, double puka, a chest for certain, a board or a rider on loan. The wheel itself
// is the web's 3D prop (wheel.glb, with the eighteen painted boards of wheelboards.glb laid on
// its disc), seen through a second camera into a texture on the pane and turned the way the
// web turns it; without the files it is drawn as the web's older DOM wheel, ten blades at
// even angles.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// the ladder's titles and purses, number for number from the web, for the wheel and the level sheet
public static class Rungs
{
    public static string RankAt(int L) { string n = Progress.Ranks[0].Item2; foreach (var r in Progress.Ranks) { if (L >= r.Item1) n = r.Item2; else break; } return n; }
    public static int RankIdx(int L) { int i = 0; for (int k = 0; k < Progress.Ranks.Length; k++) if (L >= Progress.Ranks[k].Item1) i = k; return i; }
    /// is this level the first of a rung? that is what pays; -1 for a level inside one
    public static int RankRungAt(int L) { for (int k = 0; k < Progress.Ranks.Length; k++) if (Progress.Ranks[k].Item1 == L) return k; return -1; }
    /// the next title and the level it lands on; false at the top of the ladder
    public static bool NextRank(int L, out int at, out string name)
    {
        foreach (var r in Progress.Ranks) if (r.Item1 > L) { at = r.Item1; name = r.Item2; return true; }
        at = 0; name = null; return false;
    }
    /// the promotion purse for rung i: round(400 * (i+1)^1.35 / 50) * 50
    public static int RankPay(int i) => JsRound(400.0 * System.Math.Pow(i + 1, 1.35) / 50.0) * 50;
    /// every rung crossed between two levels, so a run that jumps two of them pays for both
    public static int RankPayBetween(int from, int to)
    {
        int n = 0;
        for (int L = from + 1; L <= to; L++) { int k = RankRungAt(L); if (k >= 0) n += RankPay(k); }
        return n;
    }
    /// JavaScript's Math.round: halves go up, where Mathf.RoundToInt sends them to the even side
    public static int JsRound(double x) => (int)System.Math.Floor(x + 0.5);

    /// the web's hidden boards, and the tube, which is never lent; and its one hidden rider
    public static bool BoardHidden(string id) => id == "cattleclass" || id == "tube";
    public static bool RiderHidden(string id) => id == "cow";
    public static BoardCatalog.Board FindBoard(string id) { foreach (var b in BoardCatalog.All) if (b.id == id) return b; return null; }
    public static RiderCatalog.Rider FindRider(string id) { foreach (var r in RiderCatalog.All) if (r.id == id) return r; return null; }

    /// the web's unlocksAt: the boards and riders not yet owned whose gate is exactly L, by name
    public static List<string> UnlocksAt(int L)
    {
        var o = new List<string>();
        foreach (var b in BoardCatalog.All) if (!SaveData.Owns(b.id) && !BoardHidden(b.id) && Ladder.BoardLvl(b.id) == L) o.Add(b.name);
        foreach (var r in RiderCatalog.All) if (!SaveData.OwnsRider(r.id) && !RiderHidden(r.id) && Ladder.CharLvl(r.id) == L) o.Add(r.name);
        // gear goes last: it is the one thing here you simply receive
        foreach (var g in GearCatalog.All) if (g.lv == L) o.Add(g.name);
        return o;
    }

    /// the web's dayStamp: the local calendar day as yyyy-mm-dd
    public static string DayStamp() => System.DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}

/// the wheel's rules: what is on it today, who won what, and what that does to the next run
public static class Wheel
{
    /// the web's WHEEL_FREE, true there while the wheel was being looked at; the daily gate is on here
    public const bool Free = false;
    public class Prize { public string k; public int w, n; public string id = "", label; }
    public class Won { public string kind, label, note = ""; public int n; }

    public static bool Ready => Free || Rungs.DayStamp() != SaveData.S.wheelLast;

    // ---- what a spin banked, spent on the run it is spent on ----
    /// the web's revivesLeft: one a run, two when the wheel banked a life
    public static int Revives = 1;
    public static bool X2Banked => SaveData.S.wheelX2;
    // The loans stay in the save (wheelLoan, wheelRider) for the run they are spent on, because
    // Bootstrap.BuildRider reads them there and a loan outranks the equipped pair; they are
    // cleared when that run ends, which is the web's loanReturn.
    static bool loanOut;                   // a loan is on the water: this run is the one it was for
    static bool chestDue;

    /// a board the LEVEL has locked, not one you merely cannot afford, picked at random
    static string LoanPick()
    {
        var c = new List<string>(); int level = Progress.Level;
        foreach (var b in BoardCatalog.All)
        {
            if (Rungs.BoardHidden(b.id) || SaveData.Owns(b.id)) continue;
            if (Ladder.BoardLvl(b.id) > level) c.Add(b.id);
        }
        return c.Count > 0 ? c[Random.Range(0, c.Count)] : "";
    }
    static string RiderPick()
    {
        var c = new List<string>(); int level = Progress.Level;
        foreach (var r in RiderCatalog.All)
        {
            if (Rungs.RiderHidden(r.id) || SaveData.OwnsRider(r.id)) continue;
            if (Ladder.CharLvl(r.id) > level) c.Add(r.id);
        }
        return c.Count > 0 ? c[Random.Range(0, c.Count)] : "";
    }

    /// a piece the RANK has locked, at random from what is left; being shown something you have is not a prize
    static string GearPick()
    {
        var c = new List<string>();
        foreach (var g in GearCatalog.All) if (!GearCatalog.Have(g.id)) c.Add(g.id);
        return c.Count > 0 ? c[Random.Range(0, c.Count)] : "";
    }

    static string N(int v) => v.ToString("N0");

    /// the web's wheelPrizes: ten slices, every one on the wheel at every level, the values
    /// scaling with the rung. The weights are the odds; the face does not show them.
    public static List<Prize> Prizes()
    {
        int i = Rungs.RankIdx(Progress.Level);
        double k = 1 + 2.2 * System.Math.Sqrt(i / (double)(Progress.Ranks.Length - 1));
        int sh = Rungs.JsRound(500 * k / 50) * 50;
        string loan = LoanPick(), ride = RiderPick(), gift = GearPick();
        int jack = Rungs.JsRound(sh * 6 / 100.0) * 100;
        return new List<Prize> {
            new Prize { k = "shells",  w = 15, n = sh,   label = N(sh) + " puka" },
            new Prize { k = "x2",      w = 14, n = 0,    label = "Double puka" },
            // falls back to puka when the rack is finished, the only state with no board left to lend
            loan != "" ? new Prize { k = "loan", w = 12, id = loan, label = "Test ride" }
                       : new Prize { k = "shells", w = 12, n = sh * 2, label = N(sh * 2) + " puka" },
            new Prize { k = "chest",   w = 12, n = 0,    label = "Free chest" },
            new Prize { k = "life",    w = 13, n = 1,    label = "Second life" },
            new Prize { k = "swap",    w = 9,  n = 0,    label = "Swap a quest" },
            new Prize { k = "quest",   w = 9,  n = 0,    label = "Finish a quest" },
            ride != "" ? new Prize { k = "rider", w = 9, id = ride, label = "Guest rider" }
                       : new Prize { k = "life", w = 9, n = 1, label = "Second life" },
            // the only slice you keep and can wear; rare on purpose, and puka once the shelf is empty
            gift != "" ? new Prize { k = "gear", w = 4, id = gift, label = "Free gear" }
                       : new Prize { k = "shells", w = 4, n = jack, label = N(jack) + " puka" },
            new Prize { k = "jackpot", w = 3,  n = jack, label = "Jackpot" },
        };
    }

    /// the weighted draw; the wheel is turned to match afterwards, never the other way round
    public static int Pick(List<Prize> P)
    {
        float t = 0; foreach (var p in P) t += p.w;
        float r = Random.value * t;
        for (int i = 0; i < P.Count; i++) { r -= P[i].w; if (r <= 0) return i; }
        return 0;
    }

    /// applies one slice and says what happened; stamps the day and saves
    public static Won Claim(Prize p)
    {
        var s = SaveData.S; var o = new Won { kind = p.k, n = p.n, label = p.label };
        switch (p.k)
        {
            case "shells": SaveData.Coins += p.n; break;
            case "x2": s.wheelX2 = true; o.note = "Your next run pays double."; break;
            case "life": s.wheelLife = true; o.note = "Your next run starts with two chances."; break;
            case "chest": s.wheelChest = true; o.note = "A chest is waiting on your next run."; break;
            case "loan":
            {
                s.wheelLoan = p.id; var b = Rungs.FindBoard(p.id);
                o.note = b != null ? "One run on the " + b.name + "." : "One run on a locked board.";
                o.label = "Test ride" + (b != null ? " — " + b.name : "");
                break;
            }
            case "jackpot": SaveData.Coins += p.n; o.note = "+" + N(p.n) + " puka, straight in."; break;
            case "gear":
            {
                // worn at once: a cosmetic you have to go and find in a menu is one you do not know you won
                if (!s.ownedGear.Contains(p.id)) s.ownedGear.Add(p.id);
                var g = GearCatalog.Get(p.id);
                if (g != null) GearCatalog.SetOn(s, g.slot, p.id);
                o.note = g != null ? "The " + g.name + " is yours, and on." : "A new piece is yours.";
                o.label = "Free gear" + (g != null ? " — " + g.name : "");
                break;
            }
            case "rider":
            {
                s.wheelRider = p.id; var c = Rungs.FindRider(p.id);
                o.note = c != null ? "One run with " + c.name + "." : "One run with a locked rider.";
                o.label = "Guest rider" + (c != null ? " — " + c.name : "");
                break;
            }
            case "quest":
            {
                // finishes the one you are CLOSEST to landing, paid the hundred a quest pays
                var gs = Quests.Ensure(); int w = -1;
                for (int j = 0; j < gs.Count; j++)
                {
                    if (gs[j].done) continue;
                    if (w < 0) { w = j; continue; }
                    var dj = Quests.Def(gs[j].id); var dw = Quests.Def(gs[w].id);
                    if (gs[j].n / (float)Mathf.Max(1, dj.target) > gs[w].n / (float)Mathf.Max(1, dw.target)) w = j;
                }
                if (w < 0) { SaveData.Coins += Quests.Pay; o.note = "All three were already done. +" + Quests.Pay + " instead."; }
                else
                {
                    var d = Quests.Def(gs[w].id); gs[w].n = d.target; gs[w].done = true; SaveData.Coins += Quests.Pay;
                    o.note = d.text + " — done. +" + Quests.Pay;
                }
                break;
            }
            case "swap":
            {
                // one quest, the one you have done LEAST on; the two you are chasing are never touched
                var gs = Quests.Ensure(); int w = 0;
                for (int j = 1; j < gs.Count; j++)
                {
                    var a2 = Quests.Def(gs[j].id); var b2 = Quests.Def(gs[w].id);
                    if (gs[j].n / (float)Mathf.Max(1, a2.target) < gs[w].n / (float)Mathf.Max(1, b2.target)) w = j;
                }
                string old = Quests.Def(gs[w].id).text;
                var keep = new List<GoalSave>(gs); keep.RemoveAt(w);
                var ng = FreshGoal(keep); gs[w] = ng;
                o.note = old + " → " + Quests.Def(ng.id).text;
                break;
            }
        }
        s.wheelLast = Rungs.DayStamp();
        SaveData.Flush();
        return o;
    }

    // the web's freshGoal, against the goals being kept
    static GoalSave FreshGoal(List<GoalSave> have)
    {
        var c = new List<Quests.Goal>();
        foreach (var g in Quests.Pool) { bool taken = false; foreach (var h in have) if (h.id == g.id) { taken = true; break; } if (!taken) c.Add(g); }
        var pick = c.Count > 0 ? c[Random.Range(0, c.Count)] : Quests.Pool[0];
        return new GoalSave { id = pick.id, n = 0, done = false };
    }

    /// at the top of a run (the web's reset + wheelSpend): the lives, the loans, the chest. Returns
    /// true when a previous loan was given back and the rider's model needs rebuilding.
    public static bool RunStart()
    {
        bool changed = RunEnd();            // give back anything still out on loan before taking a new one
        var s = SaveData.S;
        Revives = 1 + (s.wheelLife ? 1 : 0); s.wheelLife = false;
        // a loan on a board or a rider already owned is no loan: cleared, as the web's loader clears it
        if (!string.IsNullOrEmpty(s.wheelLoan) && (SaveData.Owns(s.wheelLoan) || Rungs.FindBoard(s.wheelLoan) == null)) { s.wheelLoan = ""; changed = true; }
        if (!string.IsNullOrEmpty(s.wheelRider) && (SaveData.OwnsRider(s.wheelRider) || Rungs.FindRider(s.wheelRider) == null)) { s.wheelRider = ""; changed = true; }
        loanOut = !string.IsNullOrEmpty(s.wheelLoan) || !string.IsNullOrEmpty(s.wheelRider);
        if (s.wheelChest) { s.wheelChest = false; chestDue = true; }
        SaveData.Flush();
        return changed;
    }

    /// the web's loanReturn: the board and the rider go back once the run they were lent for is
    /// over, whichever way it was left. Returns true when something was given back and the
    /// rider's model needs rebuilding.
    public static bool RunEnd()
    {
        if (!loanOut) return false;
        loanOut = false; var s = SaveData.S;
        s.wheelLoan = ""; s.wheelRider = "";
        SaveData.Flush();
        return true;
    }

    /// the chest prize: true once, at the start of the run it is spent on (the web pulls the crate timer down to 1.5 s)
    public static bool ConsumeChest() { bool d = chestDue; chestDue = false; return d; }

    /// the doubler, spent at the end of a run and only there: true when this run's earnings double
    public static bool ConsumeX2() { var s = SaveData.S; if (!s.wheelX2) return false; s.wheelX2 = false; return true; }
}

public class WheelPanel : MonoBehaviour
{
    public System.Action onClose, onChange;      // onChange: the purse or the quests moved
    GameObject pane; Text tRank, tWon, tWonSmall, tGo; Image goBtn; RectTransform stage;
    List<Wheel.Prize> P; int picked = -1; bool busy, live; float busyT;
    public bool Open => pane != null && pane.activeSelf;

    // ---- the 3D wheel ----
    static readonly Vector3 Home = new Vector3(0f, -700f, 0f);      // far under the sea, where no other camera looks
    const float StageW = 278f, StageH = 366f;
    Camera wcam; RenderTexture rt; RawImage view; GameObject prop; Transform disc, boards;
    float front = 1f;                                                 // which side of the disc the boards lie on
    float spinTurn, spinAim; float fadeA;
    // the eighteen painted boards, clockwise from the top at rest, and what each one says
    static readonly (float a, string k)[] Face = {
        (25.6f, "gear"), (46.4f, "loan"), (63.4f, "loan"), (84.4f, "chest"), (101.1f, "shells"), (117.1f, "life"),
        (135.9f, "swap"), (157.9f, "swap"), (178.3f, "shells"), (200.5f, "quest"), (222.5f, "rider"), (241.9f, "gear"),
        (258.7f, "loan"), (276.0f, "chest"), (294.8f, "shells"), (311.8f, "jackpot"), (335.4f, "x2"), (359.2f, "shells") };

    // ---- the drawn wheel, when the files are not there ----
    RectTransform wheel2d; readonly List<RectTransform> blades = new List<RectTransform>(); readonly List<Text> bladeText = new List<Text>();
    float[] stops; float rot2d, rotFrom, rotTo, animT = 99f; int turns;
    static readonly Color[] Paints = { Solid.Rgba(232, 214, 176), Solid.Rgba(47, 143, 138), Solid.Rgba(217, 102, 63), Solid.Rgba(74, 111, 165), Solid.Rgba(139, 191, 106), Solid.Rgba(201, 79, 124) };

    public void Build(Transform canvas)
    {
        pane = UiKit.Rect(canvas, "spin", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        var bg = pane.AddComponent<Image>(); bg.color = new Color(0.04f, 0.16f, 0.30f, 1f);
        var p = pane.transform;
        var close = UiKit.Pic(p, "close", UiKit.Circle(40, UiKit.Body.Teal, 2f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -16), new Vector2(40, 40));
        UiKit.Label(close.transform, "×", 24, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 1), Vector2.zero);
        close.raycastTarget = true; UiKit.Pressable(close.gameObject, null, Close);
        // the sign
        var sign = UiKit.Icon("spin_sign");
        if (sign != null) { var im = UiKit.Pic(p, "sign", sign, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(214, 84)); im.preserveAspect = true; im.raycastTarget = false; }
        else UiKit.Label(p, "SPIN OF THE DAY", 26, UiKit.GoldText, FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(400, 44));
        tRank = UiKit.Label(p, "", 13, new Color(1f, 0.851f, 0.541f, 0.80f), FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(400, 18), TextAnchor.MiddleCenter, 0.5f);
        // the stage: a tap anywhere on it spins, because shown a wheel a person reaches for the wheel
        stage = UiKit.Rect(p, "stage", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(StageW, StageH));
        var hit = stage.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); hit.raycastTarget = true;
        var et = stage.gameObject.AddComponent<EventTrigger>(); UiKit.Add(et, EventTriggerType.PointerClick, _ => DoSpin());
        if (!Build3D()) Build2D();
        // the result, and the buttons
        tWon = UiKit.Label(p, "", 16, Solid.Rgba(255, 233, 138), FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -522), new Vector2(400, 24), TextAnchor.MiddleCenter, 0.6f);
        tWonSmall = UiKit.Label(p, "", 12.5f, Solid.Rgba(223, 234, 242, 0.82f), FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -548), new Vector2(400, 36), TextAnchor.UpperCenter, 0.5f);
        tWonSmall.horizontalOverflow = HorizontalWrapMode.Wrap;
        goBtn = UiKit.Pic(p, "go", UiKit.Rounded(240, 44, 22, 22, 22, 22, UiKit.Body.Teal, 2f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -592), new Vector2(240, 44));
        tGo = UiKit.Label(goBtn.transform, "Spin", 14, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        goBtn.raycastTarget = true; UiKit.Pressable(goBtn.gameObject, null, DoSpin);
        var cl = UiKit.Pic(p, "closeGo", UiKit.Rounded(240, 44, 22, 22, 22, 22, UiKit.Body.Pill, 0f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -648), new Vector2(240, 44));
        UiKit.Label(cl.transform, "Close", 14, new Color(1, 1, 1, 0.75f), FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        cl.raycastTarget = true; UiKit.Pressable(cl.gameObject, null, Close);
        pane.SetActive(false);
    }

    // ---- the prop, its camera, and the texture the pane shows ----
    bool Build3D()
    {
        var wheelPrefab = Resources.Load<GameObject>("Models/wheel");
        if (wheelPrefab == null) return false;
        var root = new GameObject("WheelStage"); root.transform.position = Home;
        prop = Instantiate(wheelPrefab, root.transform); prop.name = "wheel"; Models.Repair(prop);
        // the builder named the disc; its origin is the hub, so the boards hang on it for free
        disc = null;
        foreach (var t in prop.GetComponentsInChildren<Transform>(true)) if (t.name.ToLowerInvariant().Contains("disc")) { disc = t; break; }
        if (disc == null) disc = prop.transform;
        // the face is the side of the disc the boards lie on: the deeper side of its mesh (the
        // importer may mirror an axis, so it is read off the mesh rather than assumed)
        var mf = disc.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) { var b = mf.sharedMesh.bounds; front = Mathf.Abs(b.max.z) >= Mathf.Abs(b.min.z) ? 1f : -1f; }
        var boardsPrefab = Resources.Load<GameObject>("Models/wheelboards");
        if (boardsPrefab != null)
        {
            var bgo = Instantiate(boardsPrefab, disc); bgo.name = "boards"; Models.Repair(bgo);
            boards = bgo.transform;
            boards.localPosition = new Vector3(0, 0, 0.32f * front); boards.localRotation = Quaternion.identity; boards.localScale = Vector3.one * 0.94f;
            foreach (var r in bgo.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // flat on the disc; their shadow would only stripe the spokes
        }
        // the camera, square on to the face, the hoop across most of the width and the hub a little above centre
        var cgo = new GameObject("WheelCam"); cgo.transform.SetParent(root.transform, false);
        wcam = cgo.AddComponent<Camera>();
        wcam.clearFlags = CameraClearFlags.SolidColor; wcam.backgroundColor = new Color(0, 0, 0, 0);
        wcam.nearClipPlane = 0.05f; wcam.farClipPlane = 30f; wcam.depth = -10f; wcam.cullingMask = -1;
        var ucam = cgo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); ucam.renderPostProcessing = false;
        rt = new RenderTexture(Mathf.RoundToInt(StageW * 2), Mathf.RoundToInt(StageH * 2), 24, RenderTextureFormat.ARGB32); rt.Create();
        wcam.targetTexture = rt; wcam.enabled = false;
        var rs = prop.GetComponentsInChildren<Renderer>();
        Bounds wb = rs.Length > 0 ? rs[0].bounds : new Bounds(disc.position, Vector3.one);
        foreach (var r in rs) wb.Encapsulate(r.bounds);
        float D = Mathf.Max(0.1f, wb.size.x);
        float hhalf = 15f * Mathf.Deg2Rad;                               // a long lens: 30 degrees across
        float vhalf = Mathf.Atan(Mathf.Tan(hhalf) / (StageW / StageH));
        wcam.fieldOfView = 2f * vhalf * Mathf.Rad2Deg;
        float dist = D / (0.85f * 2f * Mathf.Tan(hhalf));
        float halfH = dist * Mathf.Tan(vhalf);
        var hub = disc.position;
        var aim = new Vector3(hub.x, wb.max.y - halfH + 0.05f * D, hub.z);
        var fwd = disc.TransformDirection(new Vector3(0, 0, front)).normalized;
        cgo.transform.position = aim + fwd * dist; cgo.transform.LookAt(aim, Vector3.up);
        // a fill from the lens, so the painted boards read whichever way the sun is
        var lgo = new GameObject("WheelLight"); lgo.transform.SetParent(cgo.transform, false); lgo.transform.localPosition = new Vector3(0.6f, 0.8f, 0f);
        var l = lgo.AddComponent<Light>(); l.type = LightType.Point; l.range = dist * 4f; l.intensity = 1.4f; l.color = new Color(1f, 0.96f, 0.9f);
        var vrt = UiKit.Rect(stage, "view", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        view = vrt.gameObject.AddComponent<RawImage>(); view.texture = rt; view.raycastTarget = false; view.color = new Color(1, 1, 1, 0);
        return true;
    }

    /// the web's spinStopFor: a board at angle A, clockwise from the top, is brought under the
    /// ticker by turning the ring to exactly A; always forward and never fewer than five turns
    float StopFor(string kind)
    {
        var c = new List<int>();
        for (int i = 0; i < Face.Length; i++) if (Face[i].k == kind) c.Add(i);
        int b = c.Count > 0 ? c[Random.Range(0, c.Count)] : Random.Range(0, Face.Length);
        float A = Face[b].a;
        int n = Mathf.CeilToInt((spinTurn + 5 * 360f - A) / 360f);
        return A + n * 360f;
    }

    // ---- the drawn wheel ----
    void Build2D()
    {
        var easel = UiKit.Icon("spin_easel");
        if (easel != null) { var im = UiKit.Pic(stage, "easel", easel, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -StageH * 0.02f), new Vector2(StageW * 0.64f, StageH * 0.96f)); im.preserveAspect = true; im.raycastTarget = false; }
        var ring = UiKit.Icon("spin_ring");
        var rim = ring != null ? UiKit.Pic(stage, "ring", ring, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(StageW, StageW))
                               : UiKit.Pic(stage, "ring", UiKit.Circle(StageW, UiKit.Body.Pill, 3f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(StageW, StageW));
        rim.preserveAspect = true; rim.raycastTarget = false;
        wheel2d = UiKit.Rect(stage, "wheel", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -StageW / 2), new Vector2(StageW, StageW));
        wheel2d.localScale = Vector3.one * 0.97f;         // the rosette sits inside the hoop
        var hub = UiKit.Icon("spin_hub");
        float hd = StageW * 0.13f;
        var hubIm = hub != null ? UiKit.Pic(stage, "hub", hub, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -StageW / 2), new Vector2(hd, hd))
                                : Solid.Pic(stage, "hub", Solid.Circle(hd), Solid.Rgba(90, 58, 30), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -StageW / 2), new Vector2(hd, hd));
        hubIm.preserveAspect = true; hubIm.raycastTarget = false;
        if (hub == null) Solid.Pic(hubIm.transform, "ring", Solid.Circle(hd, 2f), UiKit.Gold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var fin = UiKit.Icon("spin_fin");
        float fw = StageW * 0.11f;
        var finIm = fin != null ? UiKit.Pic(stage, "fin", fin, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, StageH * 0.06f), new Vector2(fw, fw * 1.4f))
                                : Solid.Pic(stage, "fin", Solid.Rounded(12, 30, 6), UiKit.Gold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 10), new Vector2(12, 30));
        finIm.preserveAspect = true; finIm.raycastTarget = false;
    }

    void DrawBlades()
    {
        foreach (var b in blades) if (b) Destroy(b.gameObject);
        blades.Clear(); bladeText.Clear();
        int n = P.Count; float step = 360f / n; stops = new float[n];
        float bw = StageW * 0.16f, bh = bw * 3.12f;                    // the petal that reaches the rim
        for (int i = 0; i < n; i++)
        {
            stops[i] = i * step;
            var bl = UiKit.Rect(wheel2d, "bl", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(bw, bh));
            bl.localEulerAngles = new Vector3(0, 0, -i * step);         // the stylesheet turns clockwise; Unity's z turns the other way
            var sp = UiKit.Icon("spin_blade" + (i % 6 + 1));
            Image im;
            if (sp != null) { im = UiKit.Pic(bl, "board", sp, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); im.preserveAspect = true; }
            else { im = UiKit.Pic(bl, "board", UiKit.Rounded(bw, bh, bw / 2, bw / 2, 8, 8, UiKit.Body.Glass, 1.2f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); im.color = Paints[i % 6]; }
            im.raycastTarget = false;
            // the label lies along the board, in the middle of the part the hub leaves showing
            var t = UiKit.Label(bl, P[i].label, 12, Solid.Rgba(255, 246, 228), FontStyle.Bold, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, bh * 0.565f), new Vector2(bh * 0.8f, 16), TextAnchor.MiddleCenter, 0f);
            t.rectTransform.localEulerAngles = new Vector3(0, 0, 90);
            var ol = t.gameObject.AddComponent<Outline>(); ol.effectColor = Solid.Rgba(28, 16, 6, 0.9f); ol.effectDistance = new Vector2(1, -1);
            blades.Add(bl); bladeText.Add(t);
        }
        Flips();
    }

    // a blade pointing into the left half reads the other way up, so its label turns to stay readable
    void Flips()
    {
        if (stops == null) return;
        for (int i = 0; i < bladeText.Count && i < stops.Length; i++)
        {
            float a = ((stops[i] + rot2d) % 360f + 360f) % 360f;
            bladeText[i].rectTransform.localEulerAngles = new Vector3(0, 0, a > 180f ? -90f : 90f);
        }
    }

    // cubic-bezier(.12,.72,.06,1), the stylesheet's spin curve, solved by bisection
    static float Ease(float u)
    {
        float lo = 0, hi = 1, t = u;
        for (int i = 0; i < 24; i++)
        {
            t = (lo + hi) * 0.5f; float mt = 1 - t;
            float x = 3 * mt * mt * t * 0.12f + 3 * mt * t * t * 0.06f + t * t * t;
            if (x < u) lo = t; else hi = t;
        }
        float m = 1 - t;
        return 3 * m * m * t * 0.72f + 3 * m * t * t * 1f + t * t * t;
    }

    // ---- the pane ----
    public void Show(bool on)
    {
        pane.SetActive(on);
        if (wcam != null) wcam.enabled = on;
        if (!on) return;
        Sfx.Buzz(8);
        tWon.text = ""; tWonSmall.text = ""; fadeA = 0f;
        Draw();
    }

    void Close() { Show(false); onClose?.Invoke(); }

    /// the web's spinDraw: the prizes for today, the rank line, and the button's state
    void Draw()
    {
        P = Wheel.Prizes();
        if (wheel2d != null) DrawBlades();
        tRank.text = Rungs.RankAt(Progress.Level).ToUpperInvariant();
        bool rdy = Wheel.Ready;
        bool enabled = rdy && !busy;
        goBtn.color = new Color(1, 1, 1, enabled ? 1f : 0.45f); tGo.color = new Color(UiKit.GoldText.r, UiKit.GoldText.g, UiKit.GoldText.b, enabled ? 1f : 0.45f);
        tGo.text = rdy ? "Spin" : "Come back tomorrow";
        live = enabled;
        if (!rdy && !busy && tWon.text == "" && tWonSmall.text == "") tWonSmall.text = "One spin a day. The prizes get better as you rank up.";
        if (Wheel.Free && !busy && tWon.text == "" && tWonSmall.text == "") tWonSmall.text = "Spin away — the daily limit is off. Prizes still climb with rank.";
    }

    void DoSpin()
    {
        if (busy || !Wheel.Ready || P == null) return;
        busy = true; busyT = 0f; Sfx.Buzz(14); live = false;
        picked = Wheel.Pick(P);
        goBtn.color = new Color(1, 1, 1, 0.45f); tGo.color = new Color(UiKit.GoldText.r, UiKit.GoldText.g, UiKit.GoldText.b, 0.45f);
        tWon.text = ""; tWonSmall.text = "";
        // the mesh stops on the board that says what was won: the board is chosen FROM the prize
        if (boards != null) spinAim = StopFor(P[picked].k);
        else if (wheel2d != null && stops != null)
        {
            // the pointer is at the top, so the slice has to arrive at 0: turn backward by its own
            // angle, plus whole turns for the show, accumulated so the wheel never un-spins
            float mid = stops[picked];
            float jitter = (Random.value - 0.5f) * Mathf.Min(14f, 360f / P.Count * 0.45f);
            turns += 5;
            rotFrom = rot2d; rotTo = turns * 360f - mid + jitter; animT = 0f;
        }
    }

    void Finish()
    {
        var r = Wheel.Claim(P[picked]);
        Sfx.Buzz(22);
        busy = false;
        Draw();
        tWon.text = "You won " + r.label;
        tWonSmall.text = r.note;
        onChange?.Invoke();
    }

    void Update()
    {
        if (pane == null || !pane.activeSelf) return;
        float dt = Time.deltaTime;
        if (view != null) { fadeA = Mathf.Min(1f, fadeA + dt / 0.55f); view.color = new Color(1, 1, 1, fadeA); }
        if (boards != null)
        {
            // only the boards turn; the rim is part of the stand. Walked toward the aim a frame at a
            // time, so a spin that finishes while the pane is closing still lands where it paid out
            spinTurn += (spinAim - spinTurn) * Mathf.Min(1f, dt * 2.6f);
            boards.localRotation = Quaternion.Euler(0, 0, -front * spinTurn);
        }
        else if (wheel2d != null)
        {
            float nudge = 0f;
            if (live)
            {
                // a degree and a half, the smallest movement that says this thing is on a bearing
                float ph = (Time.time % 3.6f) / 3.6f;
                if (ph > 0.88f && ph <= 0.93f) nudge = Mathf.Lerp(0f, 1.5f, (ph - 0.88f) / 0.05f);
                else if (ph > 0.93f && ph <= 0.965f) nudge = Mathf.Lerp(1.5f, -1f, (ph - 0.93f) / 0.035f);
                else if (ph > 0.965f) nudge = Mathf.Lerp(-1f, 0f, (ph - 0.965f) / 0.035f);
            }
            if (animT < 4.2f) { animT += dt; rot2d = Mathf.Lerp(rotFrom, rotTo, Ease(Mathf.Clamp01(animT / 4.2f))); }
            wheel2d.localEulerAngles = new Vector3(0, 0, -(rot2d + nudge));
        }
        if (busy) { busyT += dt; if (busyT >= 4.3f) { if (wheel2d != null) Flips(); Finish(); } }
    }
}
