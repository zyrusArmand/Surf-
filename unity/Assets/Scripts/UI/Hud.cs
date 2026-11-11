// Every screen the game has so far, built in code from UiKit: the title with its dial of six
// buttons around PLAY, the ride's HUD band and the trick column and JUMP, the trick message,
// and the card after a wipeout. Sizes and positions are the web stylesheet's, in points on a
// 430-wide phone.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Hud : MonoBehaviour
{
    public BoardController rider;
    public System.Action onPlay, onMenu, onShop, onQuests, onSettings, onProfile, onSchool;
    public Transform UiRoot => canvas.transform;
    Image xpFill;

    Canvas canvas;
    GameObject menu, ride, card, bar, jump;
    Text tScore, tDist, tMph, tCoins, tBestS, tBestD, tMsg, tCardTitle, tCardBody, tMenuBest, tMenuCoins, tToast;
    float toastT;
    int steerPointer = -1; float steerX0; float steerVal;

    public void Build()
    {
        var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(430, 932); sc.matchWidthOrHeight = 0f;   // match width: a phone is held upright
        if (Object.FindFirstObjectByType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        BuildMenu(cgo.transform);
        BuildRide(cgo.transform);
        BuildCard(cgo.transform);
        // the toast, for buttons whose screens are not built yet
        tToast = UiKit.Label(cgo.transform, "", 15, Color.white, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(360, 40));
        tToast.gameObject.SetActive(false);
        ShowMenu();
    }

    // ---- the title ----
    // The scene is the menu: the signpost on the beach carries Play, Shop and Quests. The
    // overlay is only the web game's top bar: puka on the left, the level and its bar in the
    // middle, settings and profile on the right.
    void BuildMenu(Transform root)
    {
        menu = UiKit.Rect(root, "menu", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        var m = menu.transform;
        var pill = UiKit.Pic(m, "puka", UiKit.Rounded(104, 34, 17, 17, 17, 17, UiKit.Body.Pill, 1.5f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -12), new Vector2(104, 34));
        Shell(pill.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), 17);
        tMenuCoins = UiKit.Label(pill.transform, "0", 14.5f, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(4, 0), Vector2.zero);
        UiKit.Label(pill.transform, "+", 16, new Color(0.45f, 0.9f, 0.55f), FontStyle.Bold, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-9, 0), new Vector2(16, 16), TextAnchor.MiddleCenter, 0);
        pill.raycastTarget = true; UiKit.Pressable(pill.gameObject, null, () => Toast("The puka store is coming in the next round"));
        // the level, and the bar under it
        var lv = UiKit.Pic(m, "level", UiKit.Rounded(96, 30, 15, 15, 15, 15, UiKit.Body.Pill, 1.5f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(96, 30));
        tMenuBest = UiKit.Label(lv.transform, "Level 1", 14, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var track = UiKit.Pic(m, "xp", UiKit.Rounded(150, 7, 3.5f, 3.5f, 3.5f, 3.5f, UiKit.Body.Pill, 0), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(150, 7));
        track.color = new Color(0.12f, 0.22f, 0.45f, 0.95f); track.raycastTarget = false;
        var fill = UiKit.Pic(m, "xpfill", UiKit.Rounded(150, 7, 3.5f, 3.5f, 3.5f, 3.5f, UiKit.Body.Pill, 0), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(-75, -48), new Vector2(150, 7));
        fill.color = new Color(1f, 0.72f, 0.1f); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0.35f; fill.raycastTarget = false; xpFill = fill;
        // settings and profile
        Round(m, "Settings", "\u2699", -10, () => onSettings?.Invoke()); Round(m, "Profile", "\u263A", -52, () => onProfile?.Invoke());
    }

    void Round(Transform m, string name, string glyph, float x, System.Action tap)
    {
        var im = UiKit.Pic(m, name, UiKit.Circle(36, UiKit.Body.Pill, 1.5f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(x, -11), new Vector2(36, 36));
        UiKit.Label(im.transform, glyph, 18, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 1), Vector2.zero);
        im.raycastTarget = true;
        UiKit.Pressable(im.gameObject, null, tap);
    }

    // ---- the ride ----
    void BuildRide(Transform root)
    {
        ride = UiKit.Rect(root, "ride", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        var r = ride.transform;
        // the band across the top, with four cells and a hairline between each pair
        var band = UiKit.Pic(r, "band", UiKit.Rounded(430, 78, 0, 0, 0, 0, UiKit.Body.Band, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 78));
        band.raycastTarget = false;
        tScore = Cell(r, "TRICKS", 0.125f, out tBestS, "best 0");
        tDist = Cell(r, "DIST", 0.375f, out tBestD, "best 0 m");
        tMph = Cell(r, "MPH", 0.625f, out _, "");
        tCoins = Cell(r, "PUKA", 0.875f, out _, "this run");
        Shell(r, new Vector2(0.875f, 1), new Vector2(0.875f, 1), new Vector2(1, 0.5f), new Vector2(-20, -44), 19);
        tCoins.alignment = TextAnchor.MiddleLeft; tCoins.rectTransform.anchoredPosition = new Vector2(12, -30);
        for (int i = 1; i < 4; i++)
        {
            var hl = UiKit.Pic(r, "hairline", UiKit.Rounded(1, 40, 0, 0, 0, 0, UiKit.Body.Glass, 0), new Vector2(i * 0.25f, 1), new Vector2(i * 0.25f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(1, 40));
            hl.color = new Color(1, 1, 1, 0.24f); hl.raycastTarget = false;
        }
        // the trick message
        tMsg = UiKit.Label(r, "", 30, UiKit.TrickYellow, FontStyle.Bold, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 50), TextAnchor.MiddleCenter, 0.6f);
        // the trick column: 70 by 56, six apart, cut flat against the left edge
        bar = UiKit.Rect(r, "bar", new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 96), new Vector2(70, 304)).gameObject;
        Hold(bar.transform, "ROLL", 4, "roll"); Hold(bar.transform, "FLIP", 3, "flip"); Hold(bar.transform, "SPIN", 2, "spin");
        Hold(bar.transform, "HAND", 1, "hand"); Hold(bar.transform, "HELI", 0, "heli");
        // JUMP: 106 by 86, cut flat against the right edge
        var j = UiKit.Pic(r, "jump", UiKit.Rounded(106, 86, 27, 0, 0, 27, UiKit.Body.Glass, 2.5f, false, true), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 46), new Vector2(106, 86));
        Ges(j.transform, "jump", 27, new Vector2(0, 14));
        UiKit.Label(j.transform, "JUMP", 17, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -14), Vector2.zero);
        j.raycastTarget = true; jump = j.gameObject;
        UiKit.Pressable(jump, () => rider.Jump());
    }

    Text Cell(Transform p, string lab, float ax, out Text rec, string recText)
    {
        var a = new Vector2(ax, 1);
        UiKit.Label(p, lab, 10, new Color(1, 1, 1, 0.60f), FontStyle.Bold, a, a, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(100, 14), TextAnchor.MiddleCenter, 0.6f);
        var v = UiKit.Label(p, "0", 22, Color.white, FontStyle.Bold, a, a, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(106, 28), TextAnchor.MiddleCenter, 0.6f);
        rec = UiKit.Label(p, recText, 9.5f, new Color(1, 1, 1, 0.52f), FontStyle.Bold, a, a, new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(100, 14), TextAnchor.MiddleCenter, 0.6f);
        return v;
    }

    void Hold(Transform p, string label, int i, string trick)
    {
        var im = UiKit.Pic(p, label, UiKit.Rounded(70, 56, 0, 18, 18, 0, UiKit.Body.Glass, 2f, true, false), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, i * 62), new Vector2(70, 56));
        Ges(im.transform, trick, 30, new Vector2(0, 7));
        UiKit.Label(im.transform, label, 9.5f, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -16), Vector2.zero);
        im.raycastTarget = true;
        if (trick == "hand" || trick == "heli") UiKit.Pressable(im.gameObject, null, () => Toast((trick == "hand" ? "The handstand" : "The helicopter") + " is coming in the next round"));
        else UiKit.Pressable(im.gameObject, () => rider.Hold(trick, true), () => rider.Hold(trick, false));
    }

    // the gesture icon the web draws on every button, from the same SVG
    static void Ges(Transform p, string name, float size, Vector2 pos)
    {
        var sp = UiKit.Icon("ges_" + name);
        if (sp == null) { UiKit.Label(p, name.ToUpper().Substring(0, 1), size * 0.6f, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), pos, Vector2.zero); return; }
        var im = UiKit.Pic(p, "ges", sp, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
        im.raycastTarget = false; im.preserveAspect = true;
    }

    // the puka shell
    static void Shell(Transform p, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, float size)
    {
        var sp = UiKit.Icon("puka"); if (sp == null) return;
        var im = UiKit.Pic(p, "puka", sp, aMin, aMax, pivot, pos, new Vector2(size, size)); im.raycastTarget = false; im.preserveAspect = true;
    }

    // ---- the card ----
    void BuildCard(Transform root)
    {
        card = UiKit.Pic(root, "card", UiKit.Rounded(340, 300, 26, 26, 26, 26, UiKit.Body.Card, 1.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(340, 300)).gameObject;
        var c = card.transform;
        tCardTitle = UiKit.Label(c, "Wipeout!", 40, Color.white, FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(320, 50));
        tCardBody = UiKit.Label(c, "", 20, new Color(1, 1, 1, 0.92f), FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(320, 110));
        var again = UiKit.Pic(c, "again", UiKit.Circle(84, UiKit.Body.Teal, 2.5f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(34, 22), new Vector2(84, 84));
        UiKit.Label(again.transform, "▶", 22, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(2, 8), Vector2.zero);
        UiKit.Label(again.transform, "AGAIN", 11, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -16), Vector2.zero);
        again.raycastTarget = true; UiKit.Pressable(again.gameObject, null, () => onPlay?.Invoke());
        var mb = UiKit.Pic(c, "menu", UiKit.Circle(54, UiKit.Body.Teal, 2f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-64, 36), new Vector2(54, 54));
        UiKit.Label(mb.transform, "⌂", 17, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 6), Vector2.zero);
        UiKit.Label(mb.transform, "MENU", 8, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -12), Vector2.zero);
        mb.raycastTarget = true; UiKit.Pressable(mb.gameObject, null, () => onMenu?.Invoke());
        var sb = UiKit.Pic(c, "shop", UiKit.Circle(54, UiKit.Body.Teal, 2f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(124, 36), new Vector2(54, 54));
        UiKit.Label(sb.transform, "\u2302", 17, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 6), Vector2.zero);
        UiKit.Label(sb.transform, "SHOP", 8, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -12), Vector2.zero);
        sb.raycastTarget = true; UiKit.Pressable(sb.gameObject, null, () => onShop?.Invoke());
        card.SetActive(false);
    }

    // ---- the screens ----
    public void ShowMenu()
    {
        menu.SetActive(true); ride.SetActive(false); card.SetActive(false);
        tMenuCoins.text = SaveData.Coins.ToString("N0");
        tMenuBest.text = "Level " + Progress.Level; if (xpFill != null) xpFill.fillAmount = Progress.Frac;
    }

    public void ShowRide()
    {
        menu.SetActive(false); ride.SetActive(true); card.SetActive(false); bar.SetActive(true); jump.SetActive(true);
        tBestS.text = "best " + SaveData.Best.ToString("N0"); tBestD.text = "best " + SaveData.BestDist + " m";
        tBestS.color = tBestD.color = new Color(1, 1, 1, 0.52f);
    }

    public void ShowCard(bool on, int dist, int score, int best, int xp = 0, int pay = 0)
    {
        card.SetActive(on); bar.SetActive(!on); jump.SetActive(!on);
        if (on) tCardBody.text = dist + " m\n" + score.ToString("N0") + " trick points\nbest " + best.ToString("N0") + "\n+" + xp + " XP" + (pay > 0 ? "  \u00B7  level up! +" + pay + " puka" : "");
    }

    public void Toast(string s) { tToast.text = s; tToast.gameObject.SetActive(true); toastT = 1.8f; }

    // a starfish in the face: three soft blots over the picture, sliding down and fading
    Image[] splat; float splatT; Vector2[] splatV;
    public void Splat(Color tint)
    {
        if (splat == null)
        {
            splat = new Image[3]; splatV = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                float d = 150f + i * 70f;
                splat[i] = UiKit.Pic(ride.transform, "splat", UiKit.Circle(d, UiKit.Body.Glass, 0), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(d, d));
                splat[i].raycastTarget = false; splat[i].gameObject.SetActive(false);
            }
        }
        for (int i = 0; i < 3; i++)
        {
            splat[i].gameObject.SetActive(true);
            splat[i].color = new Color(tint.r, tint.g, tint.b, 0.92f);
            splat[i].rectTransform.anchoredPosition = new Vector2((Random.value - 0.5f) * 260f, (Random.value - 0.5f) * 420f);
            splat[i].rectTransform.localScale = Vector3.one * (0.7f + Random.value * 0.8f);
            splatV[i] = new Vector2((Random.value - 0.5f) * 20f, -30f - Random.value * 40f);
        }
        splatT = 2.4f;
    }

    void Update()
    {
        if (toastT > 0) { toastT -= Time.deltaTime; if (toastT <= 0) tToast.gameObject.SetActive(false); }
        if (splatT > 0 && splat != null)
        {
            splatT -= Time.deltaTime; float sk = Mathf.Clamp01(splatT / 2.4f);
            for (int i = 0; i < 3; i++)
            {
                var c = splat[i].color; c.a = 0.92f * sk * sk; splat[i].color = c;
                splat[i].rectTransform.anchoredPosition += splatV[i] * Time.deltaTime * (1f - sk);
                if (splatT <= 0) splat[i].gameObject.SetActive(false);
            }
        }
        if (rider == null || !ride.activeSelf) return;
        tScore.text = rider.score.ToString("N0");
        tDist.text = Mathf.RoundToInt(rider.dist) + " m";
        tMph.text = ((int)(rider.eff * 0.62f)).ToString();
        tCoins.text = rider.coins.ToString("N0");
        tMsg.text = rider.msgT > 0 ? rider.lastMsg : "";
        // the record lights up for the rest of the run once you pass it
        if (rider.score > SaveData.Best && SaveData.Best > 0) tBestS.color = new Color(1f, 0.886f, 0.369f, 0.95f);
        if (rider.dist > SaveData.BestDist && SaveData.BestDist > 0) tBestD.color = new Color(1f, 0.886f, 0.369f, 0.95f);

        // steering: keys, or a finger dragged on the screen away from the buttons
        float k = (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? -1 : 0) + (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0);
        float touch;
        for (int i = 0; i < Input.touchCount; i++)
        {
            var tc = Input.GetTouch(i);
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(tc.fingerId);
            if (tc.phase == TouchPhase.Began && !overUi && steerPointer < 0) { steerPointer = tc.fingerId; steerX0 = tc.position.x; }
            if (tc.fingerId == steerPointer)
            {
                if (tc.phase == TouchPhase.Ended || tc.phase == TouchPhase.Canceled) { steerPointer = -1; steerVal = 0; }
                else steerVal = Mathf.Clamp((tc.position.x - steerX0) / (Screen.width * 0.18f), -1f, 1f);
            }
        }
        if (Input.touchCount == 0) { steerPointer = -1; steerVal = 0; }
        touch = steerVal;
        // mouse drag for the editor
        if (Input.touchCount == 0 && Input.GetMouseButton(0) && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            if (steerPointer < 0) { steerPointer = 99; steerX0 = Input.mousePosition.x; }
            touch = Mathf.Clamp((Input.mousePosition.x - steerX0) / (Screen.width * 0.18f), -1f, 1f);
        }
        else if (Input.touchCount == 0 && steerPointer == 99) { steerPointer = -1; touch = 0; }
        rider.steer = Mathf.Clamp(k + touch, -1f, 1f);
        if (Input.GetKeyDown(KeyCode.Space)) rider.Jump();
        if (Input.GetKeyDown(KeyCode.J)) rider.Hold("spin", true); if (Input.GetKeyUp(KeyCode.J)) rider.Hold("spin", false);
        if (Input.GetKeyDown(KeyCode.K)) rider.Hold("flip", true); if (Input.GetKeyUp(KeyCode.K)) rider.Hold("flip", false);
        if (Input.GetKeyDown(KeyCode.L)) rider.Hold("roll", true); if (Input.GetKeyUp(KeyCode.L)) rider.Hold("roll", false);
    }
}
