// Board school: every family of surfboard drawn against one foot rule, from the shaper's own
// spec sheet (length, width, and the widths a foot from each end, the outline fitted to
// them), what each is actually for, the ones the shop stocks, and a two-tap quiz that names a
// shape for the waves and the surfer. The web's panel, number for number.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GuidePanel : MonoBehaviour
{
    public class Spec { public float L, W, T, n12, t12, rockN, rockT, noseR, tailR, noseA, tailA, noseW, tailW, swallow; }
    public class Type { public string key, tag, name, len, fins, waves, say, bad; public string[] good, ids; public Spec real; public bool gap; }

    public static readonly Type[] Types = {
        new Type { key = "skim", tag = "Skim", name = "Skimboard", real = new Spec { L = 50, W = 20.00f, T = 0.55f, n12 = 17.0f, t12 = 18.0f, rockN = 2.0f, rockT = 0.6f, noseR = 0.28f, tailR = 0.22f, noseA = 2.690f, tailA = 3.200f, noseW = 0.55f, tailW = 0.650f },
            len = "45\" – 55\"", fins = "None. Nothing at all.", waves = "Shore break, and the six inches of water in front of it",
            say = "Thrown flat on wet sand and jumped on. No float, no fins, no paddling — it goes where it is pointed and stops when the water does.",
            good = new[] { "Running at a shore-break wedge from the beach", "Flat spins, and anything else that needs no grip", "Beaches with no real surf at all" },
            bad = "Hopeless out the back. There is nothing here to paddle with.",
            ids = new[] { "skipstone", "sandbar", "carbonskim", "boogie", "paipo", "shorepound", "sanddollar", "wedge" } },
        new Type { key = "wake", tag = "Wake", name = "Wake & kite twin-tips", real = new Spec { L = 55, W = 17.00f, T = 0.70f, n12 = 15.0f, t12 = 15.0f, rockN = 2.6f, rockT = 2.6f, noseR = 0.36f, tailR = 0.36f, noseA = 1.684f, tailA = 1.684f, noseW = 0.65f, tailW = 0.650f },
            len = "4'0\" – 4'8\"", fins = "Moulded rails, plus two to six small ones", waves = "None. A boat, a winch or a kite does the pulling.",
            say = "Symmetrical end to end, so it rides either way round: landing backwards is a choice rather than a crash.",
            good = new[] { "Flat water with a wake to hit", "Landing switch on purpose", "Hard, edgy carving with no drop to make" },
            bad = "Nothing in the ocean will ever push it along. Something has to pull.",
            ids = new[] { "nightshade", "riptide", "chrome", "koi", "bolt42", "slipstream", "tigerkite", "stormpetrel", "tradewind" } },
        new Type { key = "foil", tag = "Foil", name = "eFoil", real = new Spec { L = 66, W = 24.00f, T = 5.00f, n12 = 17.0f, t12 = 20.0f, rockN = 2.5f, rockT = 1.2f, noseR = 0.3f, tailR = 0.24f, noseA = 4.800f, tailA = 4.800f, noseW = 0.50f, tailW = 0.660f },
            len = "4'6\" – 5'6\", and heavy for the size", fins = "A wing on a mast, three feet under the board", waves = "Any of them, and none — it makes its own",
            say = "It leaves the surface altogether. Once the wing is flying the hull is out of the water and there is almost nothing left to slow you down.",
            good = new[] { "Open ocean with no wave to speak of", "Flying over the flat bits between sets", "Leaving next to no wake behind you" },
            bad = "A battery and a mast full of edges. Nothing about it is a surfboard.",
            ids = new[] { "mako" } },
        new Type { key = "fish", tag = "Fish", name = "Fish", gap = true, real = new Spec { L = 66, W = 20.75f, T = 2.50f, n12 = 15.0f, t12 = 16.5f, rockN = 4.0f, rockT = 1.6f, noseR = 0.32f, tailR = 0.26f, noseA = 0.853f, tailA = 4.800f, noseW = 0.05f, tailW = 0.687f, swallow = 5.0f },
            len = "5'0\" – 6'0\", ridden short", fins = "Twin keels, or a keel-ish quad", waves = "Knee to shoulder high and gutless",
            say = "Short, wide and flat, with a swallow tail. Drawn up in the seventies for waves that are not very good — which is most waves.",
            good = new[] { "Summer surf with no push in it", "Long, drawn-out speed lines", "Riding a foot shorter than your shortboard" },
            bad = "Vertical, critical surfing. That wide tail wants to run, not to pivot.",
            ids = new[] { "turbo", "wildstyle", "tangerine", "volt", "acid", "throwup", "qinghua", "leaf" } },
        new Type { key = "short", tag = "Short", name = "Shortboard", real = new Spec { L = 72, W = 18.75f, T = 2.44f, n12 = 11.25f, t12 = 14.5f, rockN = 5.0f, rockT = 2.5f, noseR = 0.34f, tailR = 0.28f, noseA = 1.063f, tailA = 3.200f, noseW = 0.03f, tailW = 0.590f },
            len = "5'6\" – 6'8\"", fins = "Thruster — three fins, evenly sized", waves = "Chest high and up, with a wall to work with",
            say = "Pointed nose, thin rails, barely enough foam to float you. Everything is given up for the ability to change direction now.",
            good = new[] { "Steep, hollow, fast waves", "Turning off the tail and off the lip", "Airs, and getting up under the lip in a barrel" },
            bad = "Learning on. It sinks under you and forgives nothing whatsoever.",
            ids = new[] { "grom", "obsidian", "pitcrew", "razor", "asym", "hydro", "roundpin", "stiletto" } },
        new Type { key = "fun", tag = "Fun", name = "Funboard & mid-length", real = new Spec { L = 90, W = 21.50f, T = 2.88f, n12 = 15.5f, t12 = 15.0f, rockN = 4.5f, rockT = 2.0f, noseR = 0.33f, tailR = 0.26f, noseA = 0.586f, tailA = 1.433f, noseW = 0.05f, tailW = 0.535f },
            len = "6'6\" – 8'6\"", fins = "2+1 — a big centre fin and two small ones", waves = "Ankle-slappers up to about head high",
            say = "Mini-mal, egg, mid-length: one idea at several lengths. Enough board to catch waves early, enough tail to turn once you are up.",
            good = new[] { "Your first hundred waves", "Small, soft, crumbling summer surf", "One board for a whole trip" },
            bad = "Heavy under the lip. Too much board to throw about once it gets steep.",
            ids = new[] { "fathom", "marble", "monarch", "astro", "hound", "cocoegg", "sunburst", "alaia", "glitch", "tikimal", "cup", "sundaybest", "boxcar" } },
        new Type { key = "gun", tag = "Gun", name = "Gun & step-up", real = new Spec { L = 90, W = 19.00f, T = 2.75f, n12 = 11.0f, t12 = 11.5f, rockN = 7.5f, rockT = 3.5f, noseR = 0.38f, tailR = 0.3f, noseA = 0.708f, tailA = 0.716f, noseW = 0.02f, tailW = 0.130f },
            len = "6'8\" – 10'0\" and beyond", fins = "Thruster, with a lot of area in it", waves = "Double overhead and upwards",
            say = "Long, narrow and pointed at both ends, with rocker built for one job: making a drop that would fold anything else in half.",
            good = new[] { "Waves that stand up and throw", "Paddling into something moving very fast", "Holding one line at speed and not deviating" },
            bad = "Dead weight on anything under head high. It will not turn for you.",
            ids = new[] { "reefrunner", "laurel", "midnight", "nitro", "aurora", "bombora", "outerreef", "elephantgun" } },
        new Type { key = "long", tag = "Long", name = "Longboard", real = new Spec { L = 108, W = 22.75f, T = 3.06f, n12 = 18.0f, t12 = 14.75f, rockN = 4.5f, rockT = 1.5f, noseR = 0.3f, tailR = 0.22f, noseA = 0.365f, tailA = 0.753f, noseW = 0.05f, tailW = 0.480f },
            len = "8'0\" – 12'0\"", fins = "One big single fin, sometimes with two small ones", waves = "Ankle to chest high, the softer the better",
            say = "Nine feet of glide, and a nose wide enough to stand on. The turns come from walking the board rather than from throwing it.",
            good = new[] { "Small, weak, unloved waves", "Noseriding — ten toes over, on a good day", "The longest rides of anyone in the water" },
            bad = "A handful to get out through the surf, and it hurts whatever it hits.",
            ids = new[] { "driftwood", "noserider", "quilt", "bubblegum", "pinelog", "malibu", "cypress", "pintail" } },
    };

    // ---- the quiz: not a personality test, a lookup table ----
    static readonly string[][] QWave = { new[] { "ankle", "Ankle high" }, new[] { "chest", "Chest high" }, new[] { "over", "Overhead" }, new[] { "huge", "Enormous" } };
    static readonly string[][] QSkill = { new[] { "new", "First time" }, new[] { "up", "Getting up" }, new[] { "turn", "Turning" }, new[] { "years", "Years in" } };
    static string Pick(string w, string s)
    {
        bool early = s == "new" || s == "up";
        if (w == "ankle") return early ? "fun" : "fish";
        if (w == "huge") return early ? "fun" : "gun";
        return early ? "fun" : "short";
    }
    static string Note(string w, string s)
    {
        string k = w + ":" + s;
        if (k == "over:new") return "Honestly, not today. Stay on the inside on plenty of foam and let the outside go.";
        if (k == "huge:new") return "No. Watch this one from the beach — everybody does, it is half the sport.";
        if (k == "huge:up") return "Not yet. This is the day to sit on the sand and take photographs.";
        if (k == "ankle:years") return "A longboard would be no disgrace on a day like this either.";
        return null;
    }

    // ---- svg units: a foot, the sand line every board stands on, the height of the box ----
    const float FT = 26f, IN = 26f / 12f, BASE = 270f, TOP = 300f, GAP = 22f;
    const float PX = 2f;        // texture pixels per unit

    static float[] PlanHalfWidths(Spec S, int N)
    {
        float a = S.noseA, b = S.tailA, up = a / (a + b);
        float peak = Mathf.Pow(up, a) * Mathf.Pow(1 - up, b);
        float Beta(float u) => Mathf.Pow(Mathf.Max(0f, u), a) * Mathf.Pow(Mathf.Max(0f, 1 - u), b) / peak;
        float Inv(float t, float lo, float hi) { for (int k = 0; k < 44; k++) { float m = (lo + hi) * 0.5f; if (Beta(m) < t) lo = m; else hi = m; } return (lo + hi) * 0.5f; }
        float uN = S.noseW > 0 ? Inv(S.noseW, 0, up) : 0, uT = S.tailW > 0 ? Inv(S.tailW, 1, up) : 1;
        var o = new float[N + 1];
        for (int i = 0; i <= N; i++) { float u = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * i / N); o[i] = Beta(uN + (uT - uN) * u); }
        return o;
    }

    // the outline as a closed polygon in svg units (y down), standing on `baseY` and `len` tall
    static List<Vector2> Outline(Spec S, float len, float wid, float cx, float baseY)
    {
        const int N = 54;
        var hw = PlanHalfWidths(S, N);
        float peak = 0.0001f; foreach (var v in hw) peak = Mathf.Max(peak, v);
        float k = wid * 0.5f / peak;
        float X(int i) => hw[i] * k;
        float rN = Mathf.Max(0.6f, X(0)), rT = S.swallow > 0 ? 0 : Mathf.Max(0.6f, X(N));
        float top = baseY - len + rN, run = len - rN - rT;
        float At(int i) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * i / N);
        float Y(int i) => top + At(i) * run;
        int Meet(int from, int dir, float r, float cy)
        {
            for (int n = 0; n <= N; n++) { int i = from + dir * n; if (i < 0 || i > N) break; float dx = X(i), dy = Y(i) - cy; if (Mathf.Sqrt(dx * dx + dy * dy) >= r - 0.01f) return i; }
            return from;
        }
        int iN = Meet(0, 1, rN, Y(0) + rN), iT = rT > 0 ? Meet(N, -1, rT, Y(N) - rT) : N;
        var pts = new List<Vector2>();
        void Cubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            for (int s = 1; s <= 14; s++) { float t = s / 14f, u = 1 - t; pts.Add(u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3); }
        }
        // the nose cap: a cubic with both handles at four thirds of the rise, apex on the nose line
        { float c = X(iN), y = Y(iN), s = y - (baseY - len), h = y - 4 * s / 3; pts.Add(new Vector2(cx - c, y)); Cubic(new Vector2(cx - c, y), new Vector2(cx - c, h), new Vector2(cx + c, h), new Vector2(cx + c, y)); }
        for (int i = iN + 1; i <= iT; i++) pts.Add(new Vector2(cx + X(i), Y(i)));
        if (S.swallow > 0) { pts.Add(new Vector2(cx, Y(N) - S.swallow / S.L * len)); pts.Add(new Vector2(cx - X(N), Y(N))); }
        else { float s = baseY - Y(iT), h = Y(iT) + 4 * s / 3; Cubic(new Vector2(cx + X(iT), Y(iT)), new Vector2(cx + X(iT), h), new Vector2(cx - X(iT), h), new Vector2(cx - X(iT), Y(iT))); }
        for (int i = iT - 1; i >= iN; i--) pts.Add(new Vector2(cx - X(i), Y(i)));
        return pts;
    }

    /// one tile of the rack: the silhouette filled white on clear, the tile `pitch` wide and TOP tall
    static Sprite Silhouette(Type T, float pitch)
    {
        int W = Mathf.CeilToInt(pitch * PX), H = Mathf.CeilToInt(TOP * PX);
        var poly = Outline(T.real, T.real.L * IN, T.real.W * IN, pitch * 0.5f, BASE);
        var px = new Color32[W * H];
        var xs = new List<float>();
        for (int y = 0; y < H; y++)
        {
            float sy = (y + 0.5f) / PX;          // svg y, measured from the top
            xs.Clear();
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                if ((a.y <= sy) == (b.y <= sy)) continue;
                xs.Add(a.x + (sy - a.y) / (b.y - a.y) * (b.x - a.x));
            }
            xs.Sort();
            for (int j = 0; j + 1 < xs.Count; j += 2)
            {
                int x0 = Mathf.Max(0, Mathf.RoundToInt(xs[j] * PX)), x1 = Mathf.Min(W - 1, Mathf.RoundToInt(xs[j + 1] * PX));
                int row = (H - 1 - y) * W;        // texture rows run bottom-up
                for (int x = x0; x <= x1; x++) px[row + x] = new Color32(255, 255, 255, 255);
            }
        }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px); tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), PX);
    }

    // ---- the panel ----
    public System.Action onClose, onChange, onShop;
    GameObject pane; RectTransform content; ScrollRect scroll;
    Transform rail; RectTransform body; Image[] sils; Sprite[] silSprites; Text[] tags;
    Type gType; string gPick; bool quizOn; string qWave, qSkill;
    public bool Open => pane != null && pane.activeSelf;

    public void Build(Transform canvas)
    {
        gType = Types[4];        // the shortboard is what the word means to most people
        pane = Pane.Build(canvas, "guide", "Board school", () => { Show(false); onClose?.Invoke(); }, out content, out scroll);
    }

    public void Show(bool on) { pane.SetActive(on); if (on) Fill(); }

    void Fill()
    {
        Pane.Clear(content);
        Pane.Note(content, "Three that aren't surfboards, then five that are. Tap one.", 24);
        Rack();
        Body();
        scroll.verticalNormalizedPosition = 1f;
    }

    // the rack: eight shapes on the glass at once against the foot rule, the three that are
    // not surfboards set off from the five that are by a gap
    void Rack()
    {
        float[] pitch = new float[Types.Length]; float W = GAP;
        for (int i = 0; i < Types.Length; i++) { pitch[i] = Mathf.Max(Types[i].real.W * IN + 14f, 52f); W += pitch[i]; }
        float ruleW = 34f, fullW = W + ruleW;
        float cardW = 402f, scale = Mathf.Min(1f, (cardW - 20f) / fullW);
        float h = TOP * scale + 8f;
        var card = Pane.Card(content, "rack", h);
        var holder = UiKit.Rect(card, "rail", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(10f, 4f), new Vector2(fullW * scale, TOP * scale));
        // the foot lines, every other one heavier, and the sand line
        for (int ft = 0; ft <= 10; ft++)
        {
            float y = (TOP - (BASE - ft * FT)) * scale;
            var ln = UiKit.Rect(holder, "ft", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(ruleW * scale, y), new Vector2(W * scale, ft % 2 == 0 ? 1.5f : 1f));
            var im = ln.gameObject.AddComponent<Image>(); im.color = new Color(1, 1, 1, ft % 2 == 0 ? 0.30f : 0.14f); im.raycastTarget = false;
            if (ft > 0 && ft % 2 == 0)
                UiKit.Label(holder, ft.ToString(), 9, new Color(1, 1, 1, 0.7f), FontStyle.Bold, new Vector2(0, 0), new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2((ruleW - 8f) * scale, y), new Vector2(20, 12), TextAnchor.MiddleRight, 0f);
        }
        UiKit.Label(holder, "ft", 9, new Color(1, 1, 1, 0.7f), FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(1, 1), new Vector2(ruleW * scale, 0), new Vector2(20, 12), TextAnchor.UpperRight, 0f);
        if (silSprites == null) { silSprites = new Sprite[Types.Length]; for (int i = 0; i < Types.Length; i++) silSprites[i] = Silhouette(Types[i], pitch[i]); }
        sils = new Image[Types.Length]; tags = new Text[Types.Length];
        float x = ruleW;
        for (int i = 0; i < Types.Length; i++)
        {
            var T = Types[i];
            if (T.gap) x += GAP;
            var im = UiKit.Pic(holder, "sil:" + T.key, silSprites[i], new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x * scale, 0), new Vector2(pitch[i] * scale, TOP * scale));
            im.raycastTarget = true; sils[i] = im;
            tags[i] = UiKit.Label(holder, T.tag, 9, Color.white, FontStyle.Bold, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2((x + pitch[i] * 0.5f) * scale, (TOP - BASE - 22f) * scale), new Vector2(pitch[i] * scale, 12), TextAnchor.MiddleCenter, 0.3f);
            int idx = i;
            UiKit.Pressable(im.gameObject, null, () => { if (gType == Types[idx]) return; gType = Types[idx]; gPick = null; Sfx.Buzz(6); Fill(); });
            x += pitch[i];
        }
        Light();
    }

    void Light()
    {
        for (int i = 0; i < Types.Length; i++)
        {
            bool on = Types[i] == gType;
            sils[i].color = on ? new Color(0.77f, 0.99f, 0.96f, 1f) : new Color(1, 1, 1, 0.42f);
            tags[i].color = on ? UiKit.GoldText : new Color(1, 1, 1, 0.6f);
        }
    }

    static string BestOf(Type T)
    {
        string best = T.ids[0]; int bt = -1, bp = -1;
        foreach (var id in T.ids)
        {
            var b = BoardCatalog.Get(id); int tot = b.speed + b.turn + b.air + b.tough;
            if (tot > bt || (tot == bt && b.price > bp)) { best = id; bt = tot; bp = b.price; }
        }
        return best;
    }

    void Body()
    {
        var T = gType;
        if (gPick == null || System.Array.IndexOf(T.ids, gPick) < 0) gPick = BestOf(T);
        int own = 0; foreach (var id in T.ids) if (SaveData.Owns(id)) own++;
        // the name, the length, and what it is
        var head = Pane.Card(content, "gname", 46);
        UiKit.Label(head, T.name, 17, Color.white, FontStyle.Bold, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-32, 0), TextAnchor.MiddleLeft, 0.5f);
        UiKit.Label(head, T.len, 12, UiKit.GoldText, FontStyle.Bold, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-32, 0), TextAnchor.MiddleRight, 0.5f);
        Pane.Note(content, T.say, 48);
        Pane.Row(content, "Fins", T.fins);
        Pane.Row(content, "Waves", T.waves);
        foreach (var g in T.good) Pane.Note(content, "•  " + g, 20, 12f);
        Pane.Note(content, "✕  " + T.bad, 34, 12f);
        Pane.Heading(content, T.ids.Length + " in the shop" + (own > 0 ? " · " + own + " owned" : ""));
        // the family: tap one to bring its card up
        float rowH = 30f; int n = T.ids.Length;
        var fam = Pane.Card(content, "gfam", 12 + rowH * Mathf.CeilToInt(n / 2f));
        for (int i = 0; i < n; i++)
        {
            string id = T.ids[i]; var b = BoardCatalog.Get(id); bool on = id == gPick;
            float bx = 12 + (i % 2) * 190f, by = -6 - (i / 2) * rowH;
            var btn = UiKit.Pic(fam, "fam:" + id, UiKit.Rounded(182, 26, 13, 13, 13, 13, on ? UiKit.Body.Teal : UiKit.Body.Pill, on ? 1.5f : 0f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(bx, by), new Vector2(182, 26));
            string lbl = (SaveData.Owns(id) ? "✓ " : "") + b.name + (Ladder.BoardLocked(id) ? "  · Lv " + Ladder.BoardLvl(id) : "");
            UiKit.Label(btn.transform, lbl, 11, on ? UiKit.GoldText : new Color(1, 1, 1, 0.8f), FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10, 0), TextAnchor.MiddleCenter, 0.3f);
            btn.raycastTarget = true;
            UiKit.Pressable(btn.gameObject, null, () => { gPick = id; Sfx.Buzz(6); Fill(); });
        }
        // the same card the rack builds: buying here is buying from the shop
        var bb = BoardCatalog.Get(gPick);
        bool owned = SaveData.Owns(bb.id), inUse = SaveData.S.equipped == bb.id, locked = Ladder.BoardLocked(bb.id);
        var card = Pane.Card(content, "gcard", 150);
        UiKit.Label(card, bb.name, 18, Color.white, FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(260, 24), TextAnchor.MiddleLeft);
        var bl = UiKit.Label(card, bb.blurb, 12, new Color(1, 1, 1, 0.82f), FontStyle.Normal, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(16, -38), new Vector2(-150, 40), TextAnchor.UpperLeft, 0.5f);
        bl.horizontalOverflow = HorizontalWrapMode.Wrap;
        string[] names = { "SPEED", "TURN", "AIR", "TOUGH" }; int[] vals = { bb.speed, bb.turn, bb.air, bb.tough };
        for (int i = 0; i < 4; i++)
        {
            float sx = 16 + i * 92;
            UiKit.Label(card, names[i], 9, new Color(1, 1, 1, 0.6f), FontStyle.Bold, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(sx, 34), new Vector2(80, 12), TextAnchor.MiddleLeft, 0.4f);
            var track = UiKit.Pic(card, "track", UiKit.Rounded(80, 7, 3.5f, 3.5f, 3.5f, 3.5f, UiKit.Body.Pill, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(sx, 22), new Vector2(80, 7));
            track.color = new Color(0, 0, 0, 0.35f); track.raycastTarget = false;
            var fill = UiKit.Pic(card, "fill", UiKit.Rounded(80, 7, 3.5f, 3.5f, 3.5f, 3.5f, UiKit.Body.Pill, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(sx, 22), new Vector2(80, 7));
            fill.color = UiKit.Gold; fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = vals[i] / 10f; fill.raycastTarget = false;
        }
        // the web's full shop card (shopCard with stats): RIDING on the one in use, Ride it on
        // one owned, the level on one the ladder holds back, the price on one for sale -- and when
        // the purse is short, how far short, on a button that does nothing until it is not
        bool shortOf = !owned && !locked && SaveData.Coins < bb.price;
        string label = inUse ? "RIDING" : owned ? "Ride it" : locked ? "Level " + Ladder.BoardLvl(bb.id)
                     : shortOf ? (bb.price - SaveData.Coins).ToString("N0") + " to go" : bb.price.ToString("N0");
        bool dim = inUse || locked || shortOf;
        bool pk = !owned && !locked;
        float bw = shortOf ? 138f : 110f;
        var btn2 = UiKit.Pic(card, "btn", UiKit.Rounded(bw, 40, 20, 20, 20, 20, dim ? UiKit.Body.Pill : UiKit.Body.Teal, dim ? 0 : 2f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -14), new Vector2(bw, 40));
        UiKit.Label(btn2.transform, label, shortOf ? 12.5f : 14, dim ? new Color(1, 1, 1, 0.7f) : UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(pk ? 6 : 0, 0), Vector2.zero);
        if (pk) { var shell = UiKit.Icon("puka"); if (shell != null) { var im = UiKit.Pic(btn2.transform, "puka", shell, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(16, 16)); im.raycastTarget = false; if (dim) im.color = new Color(1, 1, 1, 0.7f); } }
        btn2.raycastTarget = true;
        if (!dim) UiKit.Pressable(btn2.gameObject, null, () =>
        {
            Sfx.Buzz(10);
            if (!SaveData.Owns(bb.id)) { if (SaveData.Coins < bb.price) return; SaveData.Coins -= bb.price; SaveData.S.owned.Add(bb.id); Sfx.Buy(); Sfx.Buzz(16); }
            else Sfx.EquipBoard();
            SaveData.S.equipped = bb.id; SaveData.Flush(); onChange?.Invoke(); Fill();
        });
        Pane.Go(content, "See the whole rack", () => { Sfx.Buzz(8); Show(false); onShop?.Invoke(); });
        // two taps and it names a shape
        Pane.Go(content, "Match me", () => { Sfx.Buzz(8); quizOn = !quizOn; Fill(); });
        if (quizOn) Quiz();
    }

    void Quiz()
    {
        var card = Pane.Card(content, "quiz", 118);
        void Row(string title, string[][] list, string cur, float y, System.Action<string> set)
        {
            UiKit.Label(card, title, 9.5f, new Color(1, 1, 1, 0.6f), FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, y), new Vector2(44, 26), TextAnchor.MiddleLeft, 0.3f);
            for (int i = 0; i < list.Length; i++)
            {
                string k = list[i][0]; bool on = cur == k;
                var btn = UiKit.Pic(card, "q:" + k, UiKit.Rounded(80, 26, 13, 13, 13, 13, on ? UiKit.Body.Teal : UiKit.Body.Pill, on ? 1.5f : 0f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(58 + i * 84, y), new Vector2(80, 26));
                UiKit.Label(btn.transform, list[i][1], 10, on ? Color.white : new Color(1, 1, 1, 0.8f), FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 0.3f);
                btn.raycastTarget = true;
                UiKit.Pressable(btn.gameObject, null, () => { set(k); Sfx.Buzz(6); Fill(); });
            }
        }
        Row("WAVES", QWave, qWave, -8, k => qWave = k);
        Row("YOU", QSkill, qSkill, -42, k => qSkill = k);
        string outTxt;
        if (qWave == null || qSkill == null) outTxt = "Pick one from each row.";
        else
        {
            var T = System.Array.Find(Types, t => t.key == Pick(qWave, qSkill));
            outTxt = T.name + ". " + (Note(qWave, qSkill) ?? T.say);
            if (gType != T) { gType = T; gPick = null; }
        }
        var o = UiKit.Label(card, outTxt, 12, new Color(1, 1, 1, 0.9f), FontStyle.Normal, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(-24, 38), TextAnchor.UpperLeft, 0.3f);
        o.horizontalOverflow = HorizontalWrapMode.Wrap;
    }
}
