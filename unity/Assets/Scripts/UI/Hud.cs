// Every screen the game has so far, built in code from UiKit: the title's top bar (puka and
// its +, the level pill, settings, the daily wheel with its pip, profile), the ride's HUD band
// and the trick column and JUMP, the trick message, and the card after a wipeout with its
// rows, its free life, the row of buttons and the two offers. Sizes and positions are the web
// stylesheet's, in points on a 430-wide phone.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Hud : MonoBehaviour
{
    public BoardController rider;
    public System.Action onPlay, onMenu, onShop, onQuests, onSettings, onProfile, onSchool;
    public System.Action onWheel, onLevel, onPuka;        // the bar's wheel, the level pill, the + on the purse
    public System.Action onSecret;                        // five taps on the version
    int verTaps; float verTapT;
    public System.Action onRevive;                        // the card's free life was taken: put him back on the board
    public Coconuts cocos;                                // the coconut meter reads the charge
    public Island island; public Dive dive;               // the walk stick ashore; the air left under water
    public ChaseCamera chase;                             // the speed streaks read the lens
    public Transform UiRoot => canvas.transform;
    Image xpFill;

    Canvas canvas;
    GameObject menu, ride, card, bar, jump;
    Text tScore, tDist, tMph, tCoins, tBestS, tBestD, tMsg, tMenuBest, tMenuCoins, tToast;
    Transform cardRoot; RectTransform cardStack;
    Image pip;                                            // the wheel button's dot: an unspun day shows it
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
    // overlay is only the web game's top bar: puka and its + on the left, the level pill and
    // its bar in the middle, settings, the daily wheel and the profile on the right.
    void BuildMenu(Transform root)
    {
        menu = UiKit.Rect(root, "menu", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        var m = menu.transform;
        // the version, small at the foot of the beach, as the web prints it
        // five taps on it open the secret panel, as the web's version does
        var ver = UiKit.Label(m, "v" + Application.version, 12, new Color(1, 1, 1, 0.62f), FontStyle.Bold, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(200, 16), TextAnchor.MiddleCenter, 0.5f);
        ver.raycastTarget = true;
        UiKit.Pressable(ver.gameObject, null, () =>
        {
            float now = Time.unscaledTime;
            verTaps = now - verTapT < 1.2f ? verTaps + 1 : 1; verTapT = now;
            if (verTaps >= 5) { verTaps = 0; Sfx.Buzz(20); onSecret?.Invoke(); }
        });
        // ---- the bar, the web's #homeBar: 10 from the top, 12 in from each side ----
        Color pillTop = Solid.Rgba(0x3c, 0x60, 0x99), pillBot = Solid.Rgba(0x2a, 0x44, 0x72), rim = Solid.Rgba(160, 196, 240, 0.26f);
        // the purse: navy pill, 30 tall, the shell, the count, and the green + inside it
        purse = Solid.Pic(m, "puka", Solid.VGradient(120, 30, 15, pillTop, pillBot, rim, 1f), Color.white, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -10), new Vector2(120, 30));
        purseSh = Drop(purse.rectTransform);
        Shell(purse.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), 17);
        tMenuCoins = UiKit.Label(purse.transform, "0", 14.5f, Color.white, FontStyle.Bold, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(31, 0), new Vector2(80, 0), TextAnchor.MiddleLeft, 0.45f);
        purse.raycastTarget = false;
        plus = Solid.Pic(purse.transform, "plus", Solid.VGradient(20, 20, 10, Solid.Rgba(0x5c, 0xc9, 0x6d), Solid.Rgba(0x35, 0xa0, 0x48), Color.clear, 0f), Color.white, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-4, 0), new Vector2(20, 20));
        var plusIcon = UiKit.Icon("bar_plus");
        if (plusIcon != null) { var pi = UiKit.Pic(plus.transform, "glyph", plusIcon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20)); pi.raycastTarget = false; }
        plus.raycastTarget = true; UiKit.Pressable(plus.gameObject, null, () => { Sfx.Buzz(8); onPuka?.Invoke(); });
        // the level: the same pill centred on the screen, gold ink, 17 bold
        lvPill = Solid.Pic(m, "level", Solid.VGradient(96, 30, 15, pillTop, pillBot, rim, 1f), Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(96, 30));
        lvSh = Drop(lvPill.rectTransform);
        tMenuBest = UiKit.Label(lvPill.transform, "Level 1", 17, Solid.Rgba(0xff, 0xd2, 0x4a), FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 0.45f);
        lvPill.raycastTarget = true; UiKit.Pressable(lvPill.gameObject, null, () => { Sfx.Buzz(8); onLevel?.Invoke(); });
        // the XP bar under it: 232 by 8, a dark track, an orange-to-gold fill with a warm halo
        var halo = Solid.Pic(m, "xpglow", Solid.Rounded(250, 26, 13), Solid.Rgba(255, 168, 48, 0.16f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(250, 26));
        halo.raycastTarget = false;
        var track = Solid.Pic(m, "xp", Solid.Rounded(232, 8, 4), Solid.Rgba(0x24, 0x39, 0x5f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -45), new Vector2(232, 8));
        track.raycastTarget = false;
        var fill = Solid.Pic(track.transform, "xpfill", Solid.Gradient(232, 8, 4, Solid.Rgba(0xf7, 0x94, 0x1e), Solid.Rgba(0xff, 0xd9, 0x3b)), Color.white, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0.35f; fill.raycastTarget = false; xpFill = fill;
        // settings and profile: 32 blue discs on the right, nine apart; the wheel under the purse
        Disc(m, "Settings", "bar_gear", new Vector2(1, 1), new Vector2(-12 - 32 - 9, -9), () => onSettings?.Invoke());
        Disc(m, "Profile", "bar_prof", new Vector2(1, 1), new Vector2(-12, -9), () => onProfile?.Invoke());
        var wheel = Disc(m, "Wheel", "bar_wheel", new Vector2(0, 1), new Vector2(12, -66), () => { Sfx.Buzz(8); onWheel?.Invoke(); });
        // the dot is the whole interface: an unspun day shows it, a day already taken shows a plain wheel
        pip = Solid.Pic(wheel.transform, "pip", Solid.Circle(13), Solid.Rgba(8, 26, 40, 0.75f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-6.5f, -6.5f), new Vector2(13, 13));
        Solid.Pic(pip.transform, "dot", Solid.Circle(9), Solid.Rgba(255, 212, 46), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-4, -4));
    }

    Image purse, plus, lvPill, purseSh, lvSh;

    // the pills size to what they say, as the web's flex pills do
    void SizeBar()
    {
        if (purse == null || lvPill == null) return;
        float pw = 31f + tMenuCoins.preferredWidth + 4f + 2f + 20f + 4f;
        purse.rectTransform.sizeDelta = new Vector2(pw, 30); if (purseSh != null) purseSh.rectTransform.sizeDelta = new Vector2(pw + 12, 42);
        if (purseSh != null) purseSh.sprite = Solid.Rounded(pw + 12, 42, 21);
        purse.sprite = Solid.VGradient(Mathf.Round(pw), 30, 15, Solid.Rgba(0x3c, 0x60, 0x99), Solid.Rgba(0x2a, 0x44, 0x72), Solid.Rgba(160, 196, 240, 0.26f), 1f);
        float lw = tMenuBest.preferredWidth + 24f;
        lvPill.rectTransform.sizeDelta = new Vector2(lw, 30); if (lvSh != null) { lvSh.rectTransform.sizeDelta = new Vector2(lw + 12, 42); lvSh.sprite = Solid.Rounded(lw + 12, 42, 21); }
        lvPill.sprite = Solid.VGradient(Mathf.Round(lw), 30, 15, Solid.Rgba(0x3c, 0x60, 0x99), Solid.Rgba(0x2a, 0x44, 0x72), Solid.Rgba(160, 196, 240, 0.26f), 1f);
    }

    // the web's soft drop shadow under a pill: 0 2px 8px rgba(4,14,36,.34)
    static Image Drop(RectTransform rt)
    {
        var sh = Solid.Pic(rt.parent, rt.name + ":shadow", Solid.Rounded(rt.sizeDelta.x + 12, rt.sizeDelta.y + 12, (rt.sizeDelta.y + 12) * 0.5f), Solid.Rgba(4, 14, 36, 0.20f), rt.anchorMin, rt.anchorMax, rt.pivot, rt.anchoredPosition + new Vector2(rt.pivot.x == 0 ? -6 : rt.pivot.x == 1 ? 6 : 0, -2 + 6 * (rt.pivot.y - 0.5f) * 2f), rt.sizeDelta + new Vector2(12, 12));
        sh.raycastTarget = false; sh.transform.SetSiblingIndex(rt.GetSiblingIndex());
        return sh;
    }

    Image Disc(Transform m, string name, string icon, Vector2 anchor, Vector2 pos, System.Action tap)
    {
        var im = Solid.Pic(m, name, Solid.VGradient(32, 32, 16, Solid.Rgba(0x4a, 0x72, 0xb4), Solid.Rgba(0x31, 0x4f, 0x85), Solid.Rgba(160, 196, 240, 0.26f), 1f), Color.white, anchor, anchor, anchor, pos, new Vector2(32, 32));
        Drop(im.rectTransform);
        var sp = UiKit.Icon(icon);
        if (sp != null) { var g = UiKit.Pic(im.transform, "glyph", sp, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(sp.rect.width / 4f, sp.rect.height / 4f)); g.raycastTarget = false; }
        im.raycastTarget = true;
        UiKit.Pressable(im.gameObject, null, tap);
        return im;
    }

    Image Round(Transform m, string name, string glyph, float x, System.Action tap)
    {
        var im = UiKit.Pic(m, name, UiKit.Circle(36, UiKit.Body.Pill, 1.5f), new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(x, -11), new Vector2(36, 36));
        if (glyph != "") UiKit.Label(im.transform, glyph, 18, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 1), Vector2.zero);
        im.raycastTarget = true;
        UiKit.Pressable(im.gameObject, null, tap);
        return im;
    }

    // the web's wheel glyph: a ring with four spokes through the hub
    static void WheelIcon(Transform p)
    {
        Solid.Pic(p, "ring", Solid.Circle(18, 1.7f), Color.white, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18 - 36, 18 - 36));
        for (int i = 0; i < 4; i++)
        {
            var s = Solid.Pic(p, "spoke", Solid.Rounded(1.7f, 15.4f, 0.85f), Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1.7f, 15.4f));
            s.rectTransform.localEulerAngles = new Vector3(0, 0, i * 45f);
        }
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
        // five SLOTS, top first, filled from the trick rack (web 6570-6585; ROLL FLIP SPIN HAND HELI until something is bought)
        for (int s = 0; s < TrickCatalog.SlotCount; s++) Hold(bar.transform, s);
        ApplyTrickSlots();
        // JUMP: 106 by 86, cut flat against the right edge
        var j = UiKit.Pic(r, "jump", UiKit.Rounded(106, 86, 27, 0, 0, 27, UiKit.Body.Glass, 2.5f, false, true), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 46), new Vector2(106, 86));
        Ges(j.transform, "jump", 27, new Vector2(0, 14));
        UiKit.Label(j.transform, "JUMP", 17, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -14), Vector2.zero);
        j.raycastTarget = true; jump = j.gameObject;
        UiKit.Pressable(jump, () => { Sfx.Buzz(10); rider.Jump(); });
        BuildStreaks(r);
        BuildCoco(r);
        BuildStick(r);
    }

    // ---- the walk stick: a ring low on the right while he is on the island, the nub follows
    // the finger and the island reads it as a direction ----
    RectTransform stickRt, stickNub; int stickId = -1; Vector2 stickC;
    void BuildStick(Transform r)
    {
        stickRt = UiKit.Rect(r, "walkStick", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-28, 150), new Vector2(120, 120));
        var ring = UiKit.Pic(stickRt, "ring", UiKit.Circle(120, UiKit.Body.Glass, 2.5f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        ring.raycastTarget = true; ring.color = new Color(1, 1, 1, 0.9f);
        var nub = UiKit.Pic(stickRt, "nub", UiKit.Circle(44, UiKit.Body.Teal, 2f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44, 44));
        nub.raycastTarget = false; stickNub = nub.rectTransform;
        stickRt.gameObject.SetActive(false);
    }

    void StepStick()
    {
        if (stickRt == null || island == null) return;
        bool on = island.StickOn;
        if (stickRt.gameObject.activeSelf != on) { stickRt.gameObject.SetActive(on); if (!on) { island.walkX = island.walkY = 0; stickId = -1; stickNub.anchoredPosition = Vector2.zero; } }
        if (!on) return;
        Vector2 v = Vector2.zero; bool held = false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            var tc = Input.GetTouch(i);
            if (tc.phase == TouchPhase.Began && stickId < 0 && RectTransformUtility.RectangleContainsScreenPoint(stickRt, tc.position)) { stickId = tc.fingerId; RectTransformUtility.ScreenPointToLocalPointInRectangle(stickRt, tc.position, null, out stickC); }
            if (tc.fingerId == stickId)
            {
                if (tc.phase == TouchPhase.Ended || tc.phase == TouchPhase.Canceled) { stickId = -1; continue; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(stickRt, tc.position, null, out var lp);
                v = (lp - stickC) / 40f; held = true;
            }
        }
        if (Input.touchCount == 0)
        {
            if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(stickRt, Input.mousePosition)) { stickId = 98; RectTransformUtility.ScreenPointToLocalPointInRectangle(stickRt, Input.mousePosition, null, out stickC); }
            if (stickId == 98 && Input.GetMouseButton(0)) { RectTransformUtility.ScreenPointToLocalPointInRectangle(stickRt, Input.mousePosition, null, out var lp); v = (lp - stickC) / 40f; held = true; }
            else if (stickId == 98) stickId = -1;
        }
        if (v.magnitude > 1f) v.Normalize();
        // keys walk too
        float kx = (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? -1 : 0) + (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0);
        float ky = (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? -1 : 0) + (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0);
        if (!held) v = new Vector2(kx, ky);
        island.walkX = Mathf.Clamp(v.x, -1f, 1f); island.walkY = Mathf.Clamp(v.y, -1f, 1f);
        stickNub.anchoredPosition = v * 40f;
    }

    // ---- the speed streaks: seven a side, leaned in toward the vanishing point, raining down
    // the edges while the lens is wide; the middle stays clean. Never fully opaque. ----
    RectTransform[] streaks; float[] streakY, streakLen, streakAlpha, streakX; RectTransform streakL, streakR;
    static readonly float[] SlX = { 6, 18, 30, 11, 42, 24, 2 }, SlD = { .05f, .21f, .33f, .14f, .27f, .39f, .18f }, SlH = { 22, 30, 18, 34, 14, 24, 30 }, SlA = { .9f, .6f, .5f, .7f, .35f, .55f, .8f };
    static readonly float[] SrD = { .11f, .29f, .02f, .36f, .17f, .24f, .31f };
    void BuildStreaks(Transform r)
    {
        streaks = new RectTransform[14]; streakY = new float[14]; streakLen = new float[14]; streakAlpha = new float[14]; streakX = new float[14];
        streakL = UiKit.Rect(r, "streaksL", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.5f, 0), new Vector2(0.12f * 430f, -0.1f * 932f), new Vector2(0.24f * 430f, 1.2f * 932f));
        streakR = UiKit.Rect(r, "streaksR", new Vector2(1, 0), new Vector2(1, 1), new Vector2(0.5f, 0), new Vector2(-0.12f * 430f, -0.1f * 932f), new Vector2(0.24f * 430f, 1.2f * 932f));
        streakL.localRotation = Quaternion.Euler(0, 0, -6f); streakR.localRotation = Quaternion.Euler(0, 0, 6f);
        var sp = Solid.Gradient(3, 64, 1.5f, new Color(1, 1, 1, 0), new Color(1, 1, 1, 1));
        for (int i = 0; i < 14; i++)
        {
            bool left = i < 7; int k = i % 7;
            float W = 0.24f * 430f, x = left ? SlX[k] / 100f * W : W - SlX[k] / 100f * W;
            var im = UiKit.Pic(left ? streakL : streakR, "streak", sp, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(x, 0), new Vector2(3, SlH[k] / 100f * 932f));
            im.raycastTarget = false; im.color = new Color(1, 1, 1, 0);
            streaks[i] = im.rectTransform; streakX[i] = x; streakLen[i] = SlH[k] / 100f * 932f; streakAlpha[i] = SlA[k];
            streakY[i] = (left ? SlD[k] : SrD[k]) / 0.42f;      // the stagger, as a share of the 0.42 s fall
        }
        streakL.gameObject.SetActive(false); streakR.gameObject.SetActive(false);
    }

    // ---- the coconut meter: the carved plaque over the band, a nut lit gold as each goes in,
    // up for a couple of seconds after one and the whole time a charge runs ----
    Image cocoBase; Image[] cocoLit; RectTransform cocoRt; CanvasGroup cocoCg; float cocoShow, cocoPop, cocoFlash; int cocoPopI = -1; bool cocoWasLive;
    void BuildCoco(Transform r)
    {
        var baseSp = UiKit.Icon("totem"); if (baseSp == null) return;
        float w = 232f, h = w * baseSp.rect.height / baseSp.rect.width;
        cocoRt = UiKit.Rect(r, "cocoBar", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(w, h));
        cocoCg = cocoRt.gameObject.AddComponent<CanvasGroup>(); cocoCg.alpha = 0; cocoCg.blocksRaycasts = false;
        cocoBase = UiKit.Pic(cocoRt, "base", baseSp, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); cocoBase.raycastTarget = false;
        cocoLit = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var sp = UiKit.Icon("totem_lit" + i); if (sp == null) continue;
            cocoLit[i] = UiKit.Pic(cocoRt, "lit" + i, sp, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            cocoLit[i].raycastTarget = false; cocoLit[i].color = new Color(1, 1, 1, 0);
        }
        cocoRt.gameObject.SetActive(false);
    }

    void StepRideFx(float dt)
    {
        // the streaks come in over the top third of the lens's range
        if (streaks != null && chase != null)
        {
            float st = Mathf.Clamp01((chase.Speed - 0.42f) / 0.58f) * 0.70f;
            bool on = st > 0.01f;
            if (streakL.gameObject.activeSelf != on) { streakL.gameObject.SetActive(on); streakR.gameObject.SetActive(on); }
            if (on)
            {
                float H = 932f;
                for (int i = 0; i < 14; i++)
                {
                    streakY[i] += dt / 0.42f; if (streakY[i] > 1f) streakY[i] -= 1f;
                    float y = -0.4f * H + 1.6f * H * streakY[i];       // -40vh down to 120vh
                    streaks[i].anchoredPosition = new Vector2(streakX[i], -y);
                    var c = streaks[i].GetComponent<Image>().color; c.a = st * streakAlpha[i]; streaks[i].GetComponent<Image>().color = c;
                }
            }
        }
        if (cocoRt == null || cocos == null) return;
        bool live = cocos.power > 0f, show = live || cocos.peek > 0f;
        if (cocos.popped >= 0) { cocoPopI = cocos.popped; cocoPop = 0.42f; if (cocos.got >= Coconuts.Max && !cocoWasLive) cocoFlash = 1.3f; }
        cocoWasLive = live;
        cocoShow = Mathf.MoveTowards(cocoShow, show ? 1f : 0f, dt / 0.22f);
        if (cocoShow > 0f && !cocoRt.gameObject.activeSelf) cocoRt.gameObject.SetActive(true);
        if (cocoShow <= 0f && cocoRt.gameObject.activeSelf) cocoRt.gameObject.SetActive(false);
        cocoCg.alpha = cocoShow;
        cocoRt.anchoredPosition = new Vector2(0, -96f + 6f * (1f - cocoShow));
        for (int i = 0; i < 3; i++)
        {
            if (cocoLit[i] == null) continue;
            var c = cocoLit[i].color; c.a = Mathf.MoveTowards(c.a, i < cocos.got ? 1f : 0f, dt / 0.2f); cocoLit[i].color = c;
            float sc = 1f;
            if (i == cocoPopI && cocoPop > 0f) { float u = 1f - cocoPop / 0.42f; sc = 1f + 0.16f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u / 0.76f)); }
            cocoLit[i].rectTransform.localScale = Vector3.one * sc;
        }
        if (cocoPop > 0f) cocoPop -= dt;
        // the live glow breathes; the flash is a one-shot on top of it
        float glow = live ? 0.5f + 0.5f * Mathf.Sin(Time.time / 1.1f * Mathf.PI * 2f) : 0f;
        if (cocoFlash > 0f) { cocoFlash -= dt; glow = ((int)(cocoFlash / 0.16f)) % 2 == 0 ? 1.6f : glow; }
        cocoBase.color = Color.Lerp(Color.white, new Color(1f, 0.93f, 0.62f), Mathf.Min(1f, glow));
    }

    Text Cell(Transform p, string lab, float ax, out Text rec, string recText)
    {
        var a = new Vector2(ax, 1);
        UiKit.Label(p, lab, 10, new Color(1, 1, 1, 0.60f), FontStyle.Bold, a, a, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(100, 14), TextAnchor.MiddleCenter, 0.6f);
        var v = UiKit.Label(p, "0", 22, Color.white, FontStyle.Bold, a, a, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(106, 28), TextAnchor.MiddleCenter, 0.6f);
        rec = UiKit.Label(p, recText, 9.5f, new Color(1, 1, 1, 0.52f), FontStyle.Bold, a, a, new Vector2(0.5f, 1), new Vector2(0, -58), new Vector2(100, 14), TextAnchor.MiddleCenter, 0.6f);
        return v;
    }

    // ---- the trick column: five slots, not five buttons (web 23804-23825) ----
    // Each button is wired once; what it does is read off its slot at the moment of the press,
    // which is what lets the shop's Tricks shelf re-label a button without re-wiring anything.
    readonly Image[] slotGes = new Image[TrickCatalog.SlotCount];
    readonly Text[] slotLab = new Text[TrickCatalog.SlotCount];
    readonly string[] slotOn = new string[TrickCatalog.SlotCount];

    void Hold(Transform p, int slot)
    {
        int i = TrickCatalog.SlotCount - 1 - slot;          // slot 0 is the top button
        var im = UiKit.Pic(p, "slot" + (slot + 1), UiKit.Rounded(70, 56, 0, 18, 18, 0, UiKit.Body.Glass, 2f, true, false), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, i * 62), new Vector2(70, 56));
        var g = UiKit.Pic(im.transform, "ges", null, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 7), new Vector2(30, 30));
        g.raycastTarget = false; g.preserveAspect = true; slotGes[slot] = g;
        slotLab[slot] = UiKit.Label(im.transform, "", 9.5f, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -16), Vector2.zero);
        im.raycastTarget = true;
        // the held five hold and release and the rider sorts out what each one means; the release
        // lets go of whatever the press started
        string held = null;
        UiKit.Pressable(im.gameObject, () => { held = slotOn[slot] ?? TrickCatalog.DefSlots[slot]; Sfx.Buzz(8); Sfx.Trick(held); rider.Hold(held, true); },
                                       () => { if (held != null) rider.Hold(held, false); held = null; });
    }

    /// applyTrickSlots (web 6572): writes the slots onto the five buttons, the gesture and the
    /// short name of what is on each. Called when the column is built and at every run's start,
    /// so a change made on the shelf is on the bar the next time it is seen.
    public void ApplyTrickSlots()
    {
        for (int s = 0; s < TrickCatalog.SlotCount; s++)
        {
            if (slotGes[s] == null) continue;
            string id = TrickCatalog.Slot(s);
            var T = TrickCatalog.Get(id) ?? TrickCatalog.Get(TrickCatalog.DefSlots[s]);
            if (slotOn[s] == T.id) continue;
            slotOn[s] = T.id;
            var sp = UiKit.Icon("ges_" + T.id);
            slotGes[s].sprite = sp; slotGes[s].enabled = sp != null;
            slotLab[s].text = T.lab;
        }
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
    static Image Shell(Transform p, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, float size)
    {
        var sp = UiKit.Icon("puka"); if (sp == null) return null;
        var im = UiKit.Pic(p, "puka", sp, aMin, aMax, pivot, pos, new Vector2(size, size)); im.raycastTarget = false; im.preserveAspect = true;
        return im;
    }

    // ---- the card ----
    // The web's run card: the two figures the run is about, what it paid (and whether the wheel
    // doubled it), the level it reached if it reached one and what that opened, the three goals
    // with a bar each; then the row the beach already taught you (Again, the free Life, Menu,
    // Share) and the two offers under it.
    public class RunCard
    {
        public int dist, score, earned; public bool doubled;
        public int lvUp, level, pay; public bool rankUp;          // levels crossed, where that left you, what the levels and the title paid
        public List<string> unlocked = new List<string>();        // what the promotion opened, by name
        public string rider = "Astro";                            // whose run it was, first name
    }
    RunCard shown;

    static readonly Color Ink = Solid.Rgba(4, 22, 42);
    static readonly Color Earn = Solid.Rgba(255, 223, 46);        // #ffdf2e
    static readonly Color Gilt = Solid.Rgba(255, 233, 138);       // #ffe98a
    static readonly Color Mist = Solid.Rgba(207, 228, 242);       // #cfe4f2

    void BuildCard(Transform root)
    {
        card = UiKit.Rect(root, "card", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        cardStack = UiKit.Rect(card.transform, "stack", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(430, 10));
        cardRoot = cardStack;
        card.SetActive(false);
    }

    /// the older call, kept for the run-end block that does not know the card's extra rows yet
    public void ShowCard(bool on, int dist, int score, int best, int xp = 0, int pay = 0)
    {
        if (!on) { ShowCard(false, null); return; }
        ShowCard(true, new RunCard { dist = dist, score = score, earned = rider != null ? rider.coins : 0, lvUp = pay > 0 ? 1 : 0, level = Progress.Level, pay = pay, rider = RiderName() });
    }

    public static string RiderName() => RiderCatalog.Get(SaveData.S.rider).name.Split(' ')[0];

    public void ShowCard(bool on, RunCard rc)
    {
        card.SetActive(on); bar.SetActive(!on); jump.SetActive(!on);
        if (!on || rc == null) return;
        shown = rc;
        Pane.Clear(cardRoot);
        float W = 390f, y = 0f;
        UiKit.Label(cardRoot, rc.rider + "'s Run Ended", 44, Color.white, FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(430, 52));
        y += 52 + 10;
        y += Rcard(rc, W, y);
        // the row: one obvious Again, the rest small and round
        y += 20;
        var again = UiKit.Pic(cardRoot, "again", UiKit.Rounded(302, 48, 24, 24, 24, 24, UiKit.Body.Teal, 2f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(302, 48));
        UiKit.Label(again.transform, "▶  AGAIN", 16, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        again.raycastTarget = true; UiKit.Pressable(again.gameObject, null, () => { Sfx.Buzz(12); onPlay?.Invoke(); });
        y += 48 + 10;
        var discs = new List<(string name, string glyph, System.Action tap)>();
        if (Wheel.Revives > 0) discs.Add(("LIFE", "♥", Revive));          // the free life is the one that comes and goes
        discs.Add(("MENU", "⌂", () => { Sfx.Buzz(8); onMenu?.Invoke(); }));
        discs.Add(("SHARE", "", Share));
        float total = discs.Count * 56 + (discs.Count - 1) * 10, x0 = -total / 2 + 28;
        for (int i = 0; i < discs.Count; i++)
        {
            var d = discs[i];
            var im = UiKit.Pic(cardRoot, d.name, UiKit.Circle(56, UiKit.Body.Teal, 2f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x0 + i * 66, -y), new Vector2(56, 56));
            if (d.glyph != "") UiKit.Label(im.transform, d.glyph, 17, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 6), Vector2.zero);
            else ShareIcon(im.transform);
            UiKit.Label(im.transform, d.name, 9.5f, UiKit.GoldText, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -13), Vector2.zero);
            im.raycastTarget = true; UiKit.Pressable(im.gameObject, null, d.tap);
        }
        y += 56 + 12;
        // the two offers: on the web they are paid for by watching a clip, and there are no ads
        // in this build, so they read as the web's and sit greyed, saying so when tapped
        Offer(W, y, -1, "Double coins"); Offer(W, y, 1, "Second life");
        y += 50;
        cardStack.sizeDelta = new Vector2(430, y);
        float H = ((RectTransform)canvas.transform).rect.height;
        cardStack.anchoredPosition = new Vector2(0, -Mathf.Max(36f, (H - y) * 0.5f));
    }

    void Offer(float W, float y, int side, string label)
    {
        float w = (W - 8) / 2;
        var im = UiKit.Pic(cardRoot, label, UiKit.Rounded(w, 50, 14, 14, 14, 14, UiKit.Body.Glass, 1f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(side * (w / 2 + 4), -y), new Vector2(w, 50));
        UiKit.Label(im.transform, label, 13.5f, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, 8), Vector2.zero, TextAnchor.MiddleCenter, 0.7f);
        UiKit.Label(im.transform, "▸  WATCH AD", 10.5f, new Color(Gilt.r, Gilt.g, Gilt.b, 0.85f), FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0, -10), Vector2.zero, TextAnchor.MiddleCenter, 0.5f);
        var cg = im.gameObject.AddComponent<CanvasGroup>(); cg.alpha = 0.45f;
        im.raycastTarget = true; UiKit.Pressable(im.gameObject, null, () => Toast("No ads in this build — the heart is the free life"));
    }

    // the glass card with the run's rows; returns its height
    float Rcard(RunCard rc, float W, float top)
    {
        bool got = rc.unlocked.Count > 0;
        bool next = !got && Ladder.NextUnlock(out int nxL, out int nxMore) != null;
        var goals = Quests.Ensure();
        float h = 16 + 62 + 13 + 32 + (rc.lvUp > 0 ? 10 + 36 : 0) + (got || next ? 24 : 0) + 11 + 1 + 4 + goals.Count * (7 + 48) + 14;
        var c = UiKit.Pic(cardRoot, "rcard", UiKit.Rounded(W, h, 20, 20, 20, 20, UiKit.Body.Card, 1f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -top), new Vector2(W, h)).transform;
        float cy = 16f, iw = W - 32;
        // the two figures the run is about: a tile each
        float tw = (W - 32 - 12) / 2;
        CountUp.On(Tile(c, -(W / 2) + 16 + tw / 2, cy, tw, "DISTANCE", Pane.N(rc.dist) + " m"), rc.dist, "", " m", 0.9f, 0.15f);
        CountUp.On(Tile(c, (W / 2) - 16 - tw / 2, cy, tw, "TRICKS", Pane.N(rc.score)), rc.score, "", "", 0.9f, 0.25f);
        cy += 62 + 13;
        // what it paid, which is the line anybody came for
        var earn = Line(c, cy, iw, 32, 9);
        Icon(earn, "puka", 32);
        CountUp.On(Flow(earn, "EARNED: " + Pane.N(rc.earned) + " PUKA", 22, Earn, 0.7f), rc.earned, "EARNED: ", " PUKA", 1.0f, 0.45f);
        if (rc.doubled)
        {
            var x2 = Solid.Pic(earn, "x2", Solid.Rounded(118, 20, 10), Solid.Rgba(118, 230, 177), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(118, 20));
            var le = x2.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 118; le.preferredHeight = 20;
            UiKit.Label(x2.transform, "×2 from the wheel", 12.5f, Solid.Rgba(14, 26, 18), FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, 0f);
        }
        cy += 32;
        // the level you just reached, only on the runs that levelled you up
        if (rc.lvUp > 0)
        {
            cy += 10;
            Box(c, cy, iw, 36, 11, rc.rankUp ? Solid.Rgba(255, 236, 160, 0.34f) : Solid.Rgba(255, 222, 120, 0.20f), rc.rankUp ? Solid.Rgba(255, 238, 182, 0.62f) : Solid.Rgba(255, 224, 150, 0.38f));
            var row = Line(c, cy, iw - 22, 36, 8, TextAnchor.MiddleLeft);
            Flow(row, "Level " + rc.level, 17, Gilt, 0.6f);
            var rk = Flow(row, (rc.rankUp ? "★ " : "") + Rungs.RankAt(rc.level), 14, rc.rankUp ? Solid.Rgba(255, 243, 200) : new Color(1, 1, 1, 0.85f), 0.5f);
            rk.GetComponent<LayoutElement>().flexibleWidth = 1;
            Flow(row, "+" + Pane.N(rc.pay), 16, Earn, 0.6f);
            Icon(row, "puka", 17);
            cy += 36;
        }
        // what it opened, by name; or when it opened nothing, what the next level could
        if (got || next)
        {
            var row = Line(c, cy, iw, 24, 7);
            if (got)
            {
                Flow(row, "UNLOCKED", 11.5f, new Color(1f, 0.851f, 0.541f, 0.78f), 0.4f);
                string names = string.Join(" · ", rc.unlocked.GetRange(0, Mathf.Min(3, rc.unlocked.Count)));
                Flow(row, names + (rc.unlocked.Count > 3 ? " +" + (rc.unlocked.Count - 3) : ""), 13.5f, Solid.Rgba(253, 243, 220), 0.4f);
            }
            else
            {
                string nm = Ladder.NextUnlock(out nxL, out nxMore);
                Flow(row, "NEXT", 11.5f, new Color(Mist.r, Mist.g, Mist.b, 0.62f), 0.4f);
                Flow(row, "Lv " + nxL + " · " + nm + (nxMore > 0 ? " +" + nxMore : ""), 13.5f, Solid.Rgba(231, 238, 246, 0.80f), 0.4f);
            }
            cy += 24;
        }
        cy += 11;
        Solid.Pic(c, "rule", Solid.Rounded(iw - 4, 1, 0), new Color(1, 1, 1, 0.22f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -cy), new Vector2(iw - 4, 1));
        cy += 1 + 4;
        // the goals: three quiet rows, a thin bar each
        foreach (var g in goals)
        {
            cy += 7;
            var d = Quests.Def(g.id);
            int n = Mathf.Min(g.n, d.target); float frac = g.done ? 1f : n / (float)Mathf.Max(1, d.target);
            Box(c, cy, iw, 48, 12, g.done ? Solid.Rgba(255, 214, 90, 0.14f) : new Color(Ink.r, Ink.g, Ink.b, 0.18f), g.done ? Solid.Rgba(255, 230, 160, 0.34f) : new Color(1, 1, 1, 0.11f));
            UiKit.Label(c, d.text, 14, g.done ? Gilt : Solid.Rgba(244, 248, 252, 0.95f), FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -cy - 8), new Vector2(iw - 22, 18), TextAnchor.MiddleLeft, 0.5f);
            UiKit.Label(c, g.done ? "DONE +" + Quests.Pay : Pane.N(n) + " / " + Pane.N(d.target), 12.5f, g.done ? Gilt : Mist, FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -cy - 8), new Vector2(iw - 22, 18), TextAnchor.MiddleRight, 0.5f);
            float bw = iw - 22;
            Solid.Pic(c, "track", Solid.Rounded(bw, 6, 3), new Color(Ink.r, Ink.g, Ink.b, 0.34f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -cy - 33), new Vector2(bw, 6));
            if (frac > 0)
            {
                var fillS = g.done ? Solid.Rounded(bw, 6, 3) : Solid.Gradient(bw, 6, 3, Solid.Rgba(111, 220, 255), Earn);
                Solid.Pic(c, "fill", fillS, g.done ? Gilt : Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(-bw / 2, -cy - 33), new Vector2(bw * frac, 6));
            }
            cy += 48;
        }
        return h;
    }

    // a stat tile: the number on top, the word under it
    static Text Tile(Transform c, float x, float y, float w, string word, string value)
    {
        Solid.Pic(c, "tile", Solid.Rounded(w, 62, 14), new Color(Ink.r, Ink.g, Ink.b, 0.20f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -y), new Vector2(w, 62));
        Solid.Pic(c, "edge", Solid.Rounded(w, 62, 14, 1f), new Color(1, 1, 1, 0.14f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -y), new Vector2(w, 62));
        var v = UiKit.Label(c, value, 32, Color.white, FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -y - 6), new Vector2(w, 36), TextAnchor.MiddleCenter, 0.7f);
        UiKit.Label(c, word, 12, new Color(1, 1, 1, 0.72f), FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x, -y - 42), new Vector2(w, 16), TextAnchor.MiddleCenter, 0.5f);
        return v;
    }

    // a tinted box with a hairline edge
    static void Box(Transform c, float y, float w, float h, float r, Color fill, Color edge)
    {
        Solid.Pic(c, "box", Solid.Rounded(w, h, r), fill, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(w, h));
        Solid.Pic(c, "edge", Solid.Rounded(w, h, r, 1f), edge, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(w, h));
    }

    // a row laid out left to right, centred unless told otherwise
    static Transform Line(Transform c, float y, float w, float h, float gap, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var rt = UiKit.Rect(c, "line", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(w, h));
        var hl = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = gap; hl.childAlignment = align; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
        return rt;
    }

    static Text Flow(Transform row, string s, float size, Color col, float shadow)
    {
        var t = UiKit.Label(row, s, size, col, FontStyle.Bold, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, shadow);
        var le = t.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = t.preferredWidth + 2; le.preferredHeight = size + 6;
        return t;
    }

    static void Icon(Transform row, string name, float size)
    {
        var sp = UiKit.Icon(name); if (sp == null) return;
        var im = UiKit.Pic(row, name, sp, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        im.preserveAspect = true; im.raycastTarget = false;
        var le = im.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = size; le.preferredHeight = size;
    }

    // the web's share glyph: three nodes and the two lines between them
    static void ShareIcon(Transform p)
    {
        var col = UiKit.GoldText;
        foreach (var v in new[] { new Vector2(6, 12), new Vector2(-6, 6), new Vector2(6, 0) })
            Solid.Pic(p, "node", Solid.Circle(5.5f), col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), v, new Vector2(5.5f, 5.5f));
        var a = Solid.Pic(p, "link", Solid.Rounded(13, 1.6f, 0.8f), col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 9), new Vector2(13, 1.6f)); a.rectTransform.localEulerAngles = new Vector3(0, 0, 27);
        var b = Solid.Pic(p, "link", Solid.Rounded(13, 1.6f, 0.8f), col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 3), new Vector2(13, 1.6f)); b.rectTransform.localEulerAngles = new Vector3(0, 0, -27);
    }

    // the free life: one second chance a run, two when the wheel banked one
    void Revive()
    {
        if (Wheel.Revives <= 0) return;
        Wheel.Revives--; Sfx.Buzz(10);
        Stats.Bump("revives");
        ShowCard(false, null);
        onRevive?.Invoke();
    }

    // the wipeout card is the shareable moment: the line goes to the clipboard, as the web's fallback does
    void Share()
    {
        if (shown == null) return;
        Sfx.Buzz(8);
        try { GUIUtility.systemCopyBuffer = "I surfed " + Pane.N(shown.dist) + " m with " + Pane.N(shown.score) + " trick points in Surf!"; Toast("COPIED!"); }
        catch { Toast("Could not share"); }
    }

    // ---- the screens ----
    public void ShowMenu()
    {
        menu.SetActive(true); ride.SetActive(false); card.SetActive(false);
        tMenuCoins.text = SaveData.Coins.ToString("N0");
        tMenuBest.text = "Level " + Progress.Level; if (xpFill != null) xpFill.fillAmount = Progress.Frac;
        if (pip != null) pip.gameObject.SetActive(Wheel.Ready);
        SizeBar();
    }

    public void ShowRide()
    {
        menu.SetActive(false); ride.SetActive(true); card.SetActive(false); bar.SetActive(true); jump.SetActive(true);
        ApplyTrickSlots();
        tBestS.text = "best " + SaveData.Best.ToString("N0"); tBestD.text = "best " + SaveData.BestDist + " m";
        tBestS.color = tBestD.color = new Color(1, 1, 1, 0.52f);
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
        // the pip pulses while the spin is there to take: 1 to 1.28 and back over 1.9 s, fading a little at the top
        if (menu.activeSelf && pip != null && pip.gameObject.activeSelf)
        {
            float k = 0.5f - 0.5f * Mathf.Cos((Time.time % 1.9f) / 1.9f * Mathf.PI * 2f);
            pip.rectTransform.localScale = Vector3.one * (1f + 0.28f * k);
            var pc = pip.color; pc.a = 0.75f * (1f - 0.28f * k); pip.color = pc;
        }
        if (rider == null || !ride.activeSelf) return;
        StepRideFx(Time.deltaTime);
        StepStick();
        // the trick column stands down under the water and on the sand; the lens cell reads the air left on a dive
        bool noTricks = (dive != null && dive.On) || (island != null && island.Ashore);
        if (bar.activeSelf == noTricks) bar.SetActive(!noTricks);
        if (jump.activeSelf == (island != null && island.Ashore)) jump.SetActive(!(island != null && island.Ashore));
        tScore.text = rider.score.ToString("N0");
        tDist.text = Mathf.RoundToInt(rider.dist) + " m";
        tMph.text = dive != null && dive.On ? "AIR " + Mathf.CeilToInt(dive.Left) : ((int)(rider.eff * 0.62f)).ToString();
        tCoins.text = rider.coins.ToString("N0");
        tMsg.text = rider.msgT > 0 ? rider.lastMsg : "";
        // the record lights up for the rest of the run once you pass it
        if (rider.score > SaveData.Best && SaveData.Best > 0) tBestS.color = new Color(1f, 0.886f, 0.369f, 0.95f);
        if (rider.dist > SaveData.BestDist && SaveData.BestDist > 0) tBestD.color = new Color(1f, 0.886f, 0.369f, 0.95f);

        // steering: keys, or a finger dragged on the screen away from the buttons
        float k2 = (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? -1 : 0) + (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0);
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
        rider.steer = Mathf.Clamp(k2 + touch, -1f, 1f);
        if (Input.GetKeyDown(KeyCode.Space)) { Sfx.Buzz(10); rider.Jump(); }
        if (Input.GetKeyDown(KeyCode.J)) { Sfx.Trick("spin"); rider.Hold("spin", true); } if (Input.GetKeyUp(KeyCode.J)) rider.Hold("spin", false);
        if (Input.GetKeyDown(KeyCode.K)) { Sfx.Trick("flip"); rider.Hold("flip", true); } if (Input.GetKeyUp(KeyCode.K)) rider.Hold("flip", false);
        if (Input.GetKeyDown(KeyCode.L)) { Sfx.Trick("roll"); rider.Hold("roll", true); } if (Input.GetKeyUp(KeyCode.L)) rider.Hold("roll", false);
        if (Input.GetKeyDown(KeyCode.H)) rider.Hold("hand", true); if (Input.GetKeyUp(KeyCode.H)) rider.Hold("hand", false);
        if (Input.GetKeyDown(KeyCode.G)) rider.Hold("heli", true); if (Input.GetKeyUp(KeyCode.G)) rider.Hold("heli", false);
    }
}
