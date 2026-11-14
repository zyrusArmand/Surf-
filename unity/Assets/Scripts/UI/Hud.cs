// Every screen the game has so far, built in code from UiKit: the title's top bar (puka and
// its +, the level pill, settings, the daily wheel with its pip, profile), the ride's HUD band
// and the trick column and JUMP, the trick message, and the card after a wipeout with its
// rows, its free life, the row of buttons and the two offers. Sizes and positions are the web
// stylesheet's, in points on a 430-wide phone.
using System.Collections;
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
    /// the carved SURF logo over the title, and the title's overlay it sits in (the Intro drops it in)
    public RectTransform Logo; public GameObject Menu => menu;
    CanvasGroup jumpCg;                                   // JUMP dims to a quarter once a trick is live
    Text tScore, tDist, tMph, tCoins, tBestS, tBestD, tMsg, tMenuBest, tMenuCoins, tToast;
    RectTransform cardRoot;
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
        var ver = UiKit.Label(m, "v" + UiKit.Version, 12, new Color(1, 1, 1, 0.62f), FontStyle.Bold, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(200, 16), TextAnchor.MiddleCenter, 0.5f);
        ver.raycastTarget = true;
        UiKit.Pressable(ver.gameObject, null, () =>
        {
            float now = Time.unscaledTime;
            verTaps = now - verTapT < 1.2f ? verTaps + 1 : 1; verTapT = now;
            if (verTaps >= 5) { verTaps = 0; Sfx.Buzz(20); onSecret?.Invoke(); }
        });
        // ---- the logo: the game's name, carved, across the top of the title under the bar ----
        var logoSp = UiKit.Icon("logo");
        if (logoSp != null)
        {
            float lw = 330f, lh = lw * logoSp.rect.height / logoSp.rect.width;
            var li = UiKit.Pic(m, "logo", logoSp, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -92), new Vector2(lw, lh));
            li.raycastTarget = false; Logo = li.rectTransform;
        }
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
        jumpCg = jump.AddComponent<CanvasGroup>();
        // a tap on the water and a HOLD under it (web 23740-23753): the held state is recorded beside
        // the tap, and the dive reads it to climb; nothing above the water does
        UiKit.Pressable(jump, () => { Sfx.Buzz(10); if (dive != null) dive.jumpHeld = true; rider.Jump(); },
                              () => { if (dive != null) dive.jumpHeld = false; });
        BuildRope(r);
        BuildStreaks(r);
        BuildCoco(r);
        BuildStick(r);
    }

    // ---- ROPE! (web #ropeBtn, 1111): JUMP's shape one slot up, 142 off the bottom. It is up
    // while the tow plane is in reach and while he hangs from it, when it reads LET GO ----
    GameObject ropeBtn; Text tRope;
    void BuildRope(Transform r)
    {
        var b = UiKit.Pic(r, "rope", UiKit.Rounded(106, 86, 27, 0, 0, 27, UiKit.Body.Glass, 2.5f, false, true), new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(0, 142), new Vector2(106, 86));
        tRope = UiKit.Label(b.transform, "ROPE!", 15, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        b.raycastTarget = true; ropeBtn = b.gameObject;
        UiKit.Pressable(ropeBtn, () => rider.onRope?.Invoke());
        ropeBtn.SetActive(false);
    }
    void StepRope()
    {
        if (ropeBtn == null) return;
        bool show = rider.running && !rider.wipe && (rider.ropeOffer || rider.hanging);
        if (ropeBtn.activeSelf != show) ropeBtn.SetActive(show);
        string s = rider.hanging ? "LET GO" : "ROPE!";
        if (show && tRope.text != s) tRope.text = s;
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
        // .tbtn.ready: the release cue, laid over the glass and under the gesture and the name
        slotReady[slot] = ReadyGlow(im.transform);
        // the held five hold and release and the rider sorts out what each one means; the release
        // lets go of whatever the press started. The rider plays the trick's sound, and only when
        // the press really starts it (web noteTrick).
        UiKit.Pressable(im.gameObject, () => { Sfx.Buzz(8); SlotDown(slot); }, () => SlotUp(slot));
    }

    readonly string[] slotHeld = new string[TrickCatalog.SlotCount];
    readonly GameObject[] slotReady = new GameObject[TrickCatalog.SlotCount];
    /// web trickDown / trickUp (23809-23823): the button and its number key go through here alike
    void SlotDown(int slot)
    {
        // a press is never refused for a release that went missing (the column hidden under a held finger)
        slotHeld[slot] = slotOn[slot] ?? TrickCatalog.DefSlots[slot];
        rider.Hold(slotHeld[slot], true);
    }
    void SlotUp(int slot)
    {
        if (slotHeld[slot] == null) return;
        rider.Hold(slotHeld[slot], false);
        slotHeld[slot] = null;
    }

    // The ready cue (web .tbtn.ready, 2756-2759): a warm wash over the glass, the rim gone to
    // #fff8c0, a 3px ring of rgba(255,246,150,.92) round the outside and a 26px glow of
    // rgba(255,224,60,.75) past that. The button is cut flat against the screen's left edge, so
    // the shape is only rounded on its right, and the glow is drawn out to the right, up and down.
    static Sprite readySprite;
    const float ReadyG = 28f;
    static GameObject ReadyGlow(Transform slot)
    {
        if (readySprite == null)
        {
            const float S = 2f, w = 70f, h = 56f, r = 18f, G = ReadyG;
            int W = Mathf.CeilToInt((w + G) * S), H = Mathf.CeilToInt((h + 2f * G) * S);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H];
            Color wash0 = Solid.Rgba(255, 247, 190, 0.34f), wash1 = Solid.Rgba(255, 226, 110, 0.16f);
            Color rim = Solid.Rgba(255, 248, 192), ring = Solid.Rgba(255, 246, 150, 0.92f), glow = Solid.Rgba(255, 224, 60, 0.75f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float ux = (x + 0.5f) / S, uy = (y + 0.5f) / S - G;            // the button's own box is 0..w by 0..h
                // signed distance to the button: open on the left, rounded top right and bottom right
                float cx = (w - 40f) * 0.5f, hx = (w + 40f) * 0.5f, cy = h * 0.5f, hy = h * 0.5f;
                float rr = ux > cx ? r : 0f;
                float qx = Mathf.Abs(ux - cx) - (hx - rr), qy = Mathf.Abs(uy - cy) - (hy - rr);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - rr;
                Color c;
                if (d <= 0f)
                {
                    // the wash, along the stylesheet's 157 degrees, and the 2px rim inside the edge
                    float t = Mathf.Clamp01((ux / w) * 0.4f + (1f - uy / h) * 0.6f);
                    c = Color.Lerp(wash0, wash1, t);
                    float rimK = Mathf.Clamp01((d + 2f) * S + 0.5f);
                    c = Color.Lerp(c, rim, rimK);
                }
                else
                {
                    // the ring, then the glow falling away as a blurred shadow does
                    float g = glow.a * 0.5f * Mathf.Exp(-(d * d) / (2f * 9f * 9f));
                    c = new Color(glow.r, glow.g, glow.b, g);
                    float ringK = Mathf.Clamp01((3f - d) * S + 0.5f) * Mathf.Clamp01(d * S + 0.5f);
                    if (ringK > 0f) c = Color.Lerp(c, ring, ringK * ring.a);
                }
                px[y * W + x] = c;
            }
            tex.SetPixels(px); tex.Apply(false, true);
            readySprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), S);
        }
        var im = UiKit.Pic(slot, "ready", readySprite, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(70f + ReadyG, 56f + 2f * ReadyG));
        im.raycastTarget = false;
        im.transform.SetAsFirstSibling();
        im.gameObject.SetActive(false);
        return im.gameObject;
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
    // The web's run card (gameOver ~28523, the .over rules ~1525-1739, measured off the page at
    // 430 wide): "Astro's Run Ended" in gold, then one glass card -- the two figures the run is
    // about on a tile each, what it paid, the level it reached if it reached one and what that
    // opened (or what the next level opens), a hairline, and the three goals with a bar each --
    // then one row: Again, the warm pill, beside the dial's round glass buttons (the free Life
    // while there is one, Menu, Share), and the two offers under it, and the version at the foot.
    // Everything glass on it is frosted over the frame the run ended on (CardArt): the frame is
    // taken at the end of the frame the card is asked for, and the card goes up on the next.
    public class RunCard
    {
        public int dist, score, earned; public bool doubled;
        public int lvUp, level, pay; public bool rankUp;          // levels crossed, where that left you, what the levels and the title paid
        public List<string> unlocked = new List<string>();        // what the promotion opened, by name
        public string rider = "Astro";                            // whose run it was, first name
        // the bests as they stood when the run began, read before the run-end moves them; -1 when
        // not known, and then no NEW BEST is shown
        public int bestBefore = -1, bestDistBefore = -1;
    }
    RunCard shown, pendingRc;
    int cardToken; bool cardPending; float cardAskedT;
    readonly List<Object> cardMade = new List<Object>();          // the pictures this card drew, destroyed with it

    static readonly Color Ink = Solid.Rgba(4, 22, 42);
    static readonly Color Earn = Solid.Rgba(255, 223, 46);        // #ffdf2e
    static readonly Color Gilt = Solid.Rgba(255, 233, 138);       // #ffe98a
    static readonly Color Mist = Solid.Rgba(207, 228, 242);       // #cfe4f2
    static readonly Color Rope = Solid.Rgba(242, 211, 145);       // #f2d391, the dial's glyphs
    static readonly Color FlatSea = ShopArt.Rgb(70, 104, 124);    // what the glass is laid on when the frame cannot be had
    static readonly Vector2 TL = new Vector2(0, 1);
    // #overlay's 20px padding either side of a 430 phone, and the row under the card:
    // #ovBtns' 20 and #overRow's 12 over the row, 56 discs, 22 to the offers, 47.4 of offer
    const float CardW = 390f, DiscD = 56f, DiscGap = 10f, AgainH = 48f, OfferH = 47.4f, OfferGap = 8f;

    void BuildCard(Transform root)
    {
        card = UiKit.Rect(root, "card", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        cardRoot = UiKit.Rect(card.transform, "stack", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
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
        bar.SetActive(!on); jump.SetActive(!on);
        if (!on || rc == null) { DropCard(); card.SetActive(false); return; }
        // the ride's band goes with the trick column (showMenu hides #hud for the card), so the
        // frame taken for the glass is the wipeout and nothing laid over it
        ride.SetActive(false);
        DropCard();
        card.SetActive(false);
        shown = rc; pendingRc = rc; cardPending = true; cardAskedT = Time.unscaledTime;
        runEarned = rc.earned; runDoubled = false;            // what DOUBLE COINS would pay again, and it has not yet (web 28576)
        StartCoroutine(GrabThenShow(++cardToken));
    }

    /// the frame, read back once it has been drawn, then the card built over it
    IEnumerator GrabThenShow(int token)
    {
        yield return new WaitForEndOfFrame();
        if (token != cardToken || !cardPending) yield break;
        var rt = (RectTransform)canvas.transform;
        CardArt.Backdrop bd = null; Texture2D shot = null;
        try { shot = ScreenCapture.CaptureScreenshotAsTexture(); bd = CardArt.Grab(shot, rt.rect.width, rt.rect.height, 14f); }
        catch (System.Exception e) { Debug.LogWarning("Hud: the run card's frame could not be read back: " + e.Message); bd = null; }
        if (shot != null) Destroy(shot);
        BuildCardNow(bd);
    }

    /// the card's pictures and rows, gone
    void DropCard()
    {
        cardPending = false;
        if (cardRoot != null) Pane.Clear(cardRoot);
        foreach (var o in cardMade) if (o != null) Destroy(o);
        cardMade.Clear();
        greyWas.Clear();
    }

    void BuildCardNow(CardArt.Backdrop bd)
    {
        cardPending = false;
        var rc = pendingRc; if (rc == null) return;
        var root = (RectTransform)canvas.transform;
        float W = root.rect.width, H = root.rect.height;
        if (bd == null) bd = CardArt.Flat(FlatSea, W, H);
        Pane.Clear(cardRoot);
        float X = (W - CardW) * 0.5f;

        // #overlay.over's wash, live over the moving sea
        var scrimTex = CardArt.ScrimTexture(bd); cardMade.Add(scrimTex);
        var scrim = UiKit.Rect(cardRoot, "scrim", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
        scrim.texture = scrimTex; scrim.raycastTarget = false;
        // what moves on the card (the Again halo, NEW BEST, the goals' bars), on unscaled time
        var fx = UiKit.Rect(cardRoot, "fx", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero).gameObject.AddComponent<CardFx>();

        // ---- the card's rows, top to bottom, in card px (its 1px border and 16px padding included) ----
        var goals = Quests.Ensure();
        bool got = rc.unlocked.Count > 0;
        int nxL = 0, nxMore = 0; string nxName = got ? null : Ladder.NextUnlock(out nxL, out nxMore);
        bool next = nxName != null;
        const float In = 17f, InW = CardW - 34f;                 // 1px border + 16px padding each side
        float y = In + 77.2f + 13f;                              // the tiles, then .rearn's 13px margin
        float earnTop = y; y += 34.1f;
        float lvlTop = -1f; if (rc.lvUp > 0) { y += 10f; lvlTop = y; y += 43.35f; }
        float gotTop = -1f; if (got || next) { gotTop = y; y += 27.9f; }
        y += 11f; float ruleTop = y; y += 1f + 4f;               // .rrule: margin 11px 2px 4px
        var goalTop = new float[goals.Count];
        for (int i = 0; i < goals.Count; i++) { y += 7f; goalTop[i] = y; y += 53.7f; }
        float cardH = y + 14f + 1f;

        // The whole block (title, card, row, offers) centred in the screen's safe area. The web's
        // 6vh over and max(9vh, 62px) under left a fifth of a tall phone empty below it, so the
        // margins are now only floors: clear of the notch above and of the version line below.
        // When it does not fit it is packed from the top ("safe"), as the web's is.
        float total = 26.5f + 2f + 10f + cardH + 20f + 12f + DiscD + 22f + OfferH;
        float k2 = Screen.height > 0 ? H / Screen.height : 1f;
        var sa = Screen.safeArea;
        float safeTop = Mathf.Max(0f, (Screen.height - sa.yMax) * k2), safeBot = Mathf.Max(0f, sa.yMin * k2);
        float minTop = safeTop + 14f, maxBot = H - Mathf.Max(safeBot + 16f, 46f);    // 46: the version's 24 off the foot and its line
        float top = safeTop + (H - safeTop - safeBot - total) * 0.5f;
        top = Mathf.Max(minTop, Mathf.Min(top, maxBot - total));
        float Y = top + 26.5f + 2f + 10f;

        // h1#ovTitle: clamp(15px, 5.6vw, 30px) -- 24 on a phone -- gold, a soft seat of a shadow
        var title = UiKit.Label(cardRoot, rc.rider + "'s Run Ended", 24, Earn, FontStyle.Bold, TL, TL, new Vector2(0.5f, 1), new Vector2(W * 0.5f, -top), new Vector2(W - 12f, 26.5f), TextAnchor.MiddleCenter, 0f);
        ShopArt.Shade(title, ShopArt.Rgb(130, 66, 10), 0.42f, new Vector2(0f, -2f));

        // ---- the glass and what is painted on it ----
        var cardBox = new CardArt.Box { x = 0, y = 0, w = CardW, h = cardH, r = 20, c0 = Solid.Rgba(255, 246, 228, 0.20f), c1 = Solid.Rgba(255, 236, 208, 0.08f), p1 = 0.46f, c2 = Solid.Rgba(226, 242, 255, 0.10f), edge = Solid.Rgba(255, 244, 222, 0.32f), hi = Solid.Rgba(255, 250, 236, 0.40f) };
        var boxes = new List<CardArt.Box>();
        float tw = (InW - 12f) / 2f;
        // a figure that beat the best the run started with: its tile edged in gold and a NEW BEST
        // ribbon on it (not on a first run, when there was no best to beat)
        var newBest = new[] { rc.bestDistBefore > 0 && rc.dist > rc.bestDistBefore, rc.bestBefore > 0 && rc.score > rc.bestBefore };
        for (int i = 0; i < 2; i++)                               // .rstat
            boxes.Add(newBest[i]
                ? new CardArt.Box { x = In + i * (tw + 12f), y = In, w = tw, h = 77.2f, r = 14, c0 = Solid.Rgba(255, 214, 90, 0.14f), c1 = new Color(Ink.r, Ink.g, Ink.b, 0.16f), edge = Solid.Rgba(255, 228, 150, 0.62f), hi = Solid.Rgba(255, 244, 210, 0.22f), glow = Solid.Rgba(255, 196, 70, 0.22f), glowBlur = 12f }
                : new CardArt.Box { x = In + i * (tw + 12f), y = In, w = tw, h = 77.2f, r = 14, c0 = new Color(Ink.r, Ink.g, Ink.b, 0.20f), c1 = new Color(Ink.r, Ink.g, Ink.b, 0.20f), edge = new Color(1, 1, 1, 0.14f), hi = new Color(1, 1, 1, 0.10f) });
        if (lvlTop >= 0f)                                         // .rlvl, and .rlvl.up on a promotion
            boxes.Add(rc.rankUp
                ? new CardArt.Box { x = In, y = lvlTop, w = InW, h = 43.35f, r = 11, c0 = Solid.Rgba(255, 236, 160, 0.34f), c1 = Solid.Rgba(255, 196, 52, 0.16f), edge = Solid.Rgba(255, 238, 182, 0.62f), ring = Solid.Rgba(255, 226, 140, 0.22f), glow = Solid.Rgba(255, 182, 40, 0.18f), glowY = 3f, glowBlur = 14f, hi = Solid.Rgba(255, 250, 226, 0.34f) }
                : new CardArt.Box { x = In, y = lvlTop, w = InW, h = 43.35f, r = 11, c0 = Solid.Rgba(255, 222, 120, 0.20f), c1 = Solid.Rgba(255, 190, 60, 0.10f), edge = Solid.Rgba(255, 224, 150, 0.38f) });
        boxes.Add(new CardArt.Box { x = In + 2f, y = ruleTop, w = InW - 4f, h = 1f, r = 0, across = true, c0 = new Color(1, 1, 1, 0f), c1 = new Color(1, 1, 1, 0.26f), p1 = 0.5f, c2 = new Color(1, 1, 1, 0f) });
        // the goals' bars: the track is painted into the glass, a dim glass line; the fill goes over
        // it live, so it can run on from where the goal stood when the run began
        var fracs = new float[goals.Count]; var fracs0 = new float[goals.Count]; var tracks = new CardArt.Box[goals.Count];
        for (int i = 0; i < goals.Count; i++)
        {
            var g = goals[i]; var d = Quests.Def(g.id);
            fracs[i] = g.done ? 1f : Mathf.Clamp01(Mathf.Min(g.n, d.target) / (float)Mathf.Max(1, d.target));
            int n0 = Quests.StartN(g.id);                         // -1: not known, so it runs up from nothing
            fracs0[i] = n0 < 0 ? 0f : Mathf.Min(fracs[i], Mathf.Clamp01(Mathf.Min(n0, d.target) / (float)Mathf.Max(1, d.target)));
            boxes.Add(new CardArt.Box { x = In, y = goalTop[i], w = InW, h = 53.7f, r = 12, c0 = g.done ? Solid.Rgba(255, 214, 90, 0.14f) : new Color(Ink.r, Ink.g, Ink.b, 0.18f), c1 = g.done ? Solid.Rgba(255, 214, 90, 0.14f) : new Color(Ink.r, Ink.g, Ink.b, 0.18f), edge = g.done ? Solid.Rgba(255, 230, 160, 0.34f) : new Color(1, 1, 1, 0.11f) });
            // 6 tall and fully round, 7 under the line
            var track = new CardArt.Box { x = In + 12f, y = goalTop[i] + 1f + 8f + 21.7f + 7f, w = InW - 24f, h = 6f, r = 3f, c0 = new Color(1, 1, 1, 0.10f), c1 = new Color(1, 1, 1, 0.07f), edge = new Color(1, 1, 1, 0.16f), hi = Solid.Rgba(0, 16, 34, 0.30f), hiY = 1f, hiBlur = 1f };
            boxes.Add(track); tracks[i] = track;
        }
        System.Func<float, float, Color> seen;
        var cardSp = CardArt.Bake(cardBox, boxes, X, Y, bd, out seen);
        cardMade.Add(cardSp.texture); cardMade.Add(cardSp);
        var cardIm = UiKit.Pic(cardRoot, "rcard", cardSp, TL, TL, TL, new Vector2(X - CardArt.Pad, -(Y - CardArt.Pad)), new Vector2(CardW + 2f * CardArt.Pad, cardH + 2f * CardArt.Pad));
        cardIm.raycastTarget = false;
        var c = UiKit.Rect(cardRoot, "rows", TL, TL, TL, new Vector2(X, -Y), new Vector2(CardW, cardH));

        // ---- the type on it ----
        // .rstat: the figure 32px over its word, 12px caps at .72 tracked .12em
        for (int i = 0; i < 2; i++)
        {
            float tx = In + i * (tw + 12f);
            string word = i == 0 ? "DISTANCE" : "TRICKS";
            var v = Txt(c, i == 0 ? Pane.N(rc.dist) + " m" : Pane.N(rc.score), 32, Color.white, tx, In + 1f + 10f + 16.8f, tw, TextAnchor.MiddleCenter);
            ShopArt.Shade(v, ShopArt.Rgb(0, 20, 40), 0.35f, new Vector2(0f, -1.5f));
            if (i == 0) CountUp.On(v, rc.dist, "", " m", 0.9f, 0.15f); else CountUp.On(v, rc.score, "", "", 0.9f, 0.25f);
            float wy = In + 1f + 10f + 33.6f + 2f + 9.3f;
            var wd = Txt(c, word, 12, ShopArt.Mix(seen(tx + tw * 0.5f, wy), Color.white, 0.72f), tx, wy, tw, TextAnchor.MiddleCenter);
            ShopArt.Shade(wd, ShopArt.Rgb(0, 20, 40), 0.3f, new Vector2(0f, -1f));
            Tracking.On(wd, 0.12f);
            if (newBest[i]) NewBest(c, fx, tx + tw * 0.5f, In, i == 0 ? 1.0f : 1.1f);   // as its figure finishes counting
        }
        // .rearn: the shell 1.45em, 9px, "EARNED: 21 SHELLS" 22px gold tracked .05em, and the wheel's x2 when it doubled
        {
            float cy = earnTop + 17.05f;
            string full = "EARNED: " + Pane.N(rc.earned) + " SHELLS";
            var t = Txt(c, full, 22, Earn, 0f, cy, 300f, TextAnchor.MiddleLeft);
            ShopArt.Shade(t, ShopArt.Rgb(120, 60, 10), 0.45f, new Vector2(0f, -2f));
            Tracking.On(t, 0.05f);
            float textW = t.preferredWidth + Tracking.Extra(full, 22, 0.05f);
            float badgeW = 0f; Text bt = null;
            if (rc.doubled)
            {
                bt = Txt(c, "×2 from the wheel", 12.5f, Solid.Rgba(14, 26, 18), 0f, cy, 160f, TextAnchor.MiddleCenter);
                bt.GetComponent<Shadow>().enabled = false;
                Tracking.On(bt, 0.04f);
                badgeW = bt.preferredWidth + Tracking.Extra(bt.text, 12.5f, 0.04f) + 14f;
            }
            float rowW = 31.9f + 9f + textW + (rc.doubled ? 15f + badgeW : 0f), x0 = (CardW - rowW) * 0.5f;
            earnPk = Pic(c, "puka", 31.9f, x0, cy, Color.white);
            earnT = t; earnCy = cy; earnBadge = bt; earnBadgeW = badgeW; earnPill = null;   // kept, so DOUBLE COINS can say what it paid
            t.rectTransform.anchoredPosition = new Vector2(x0 + 31.9f + 9f, -cy); t.rectTransform.sizeDelta = new Vector2(textW + 4f, 36f);
            CountUp.On(t, rc.earned, "EARNED: ", " SHELLS", 1.0f, 0.45f);
            if (bt != null)
            {
                // .x2: #0e1a12 on a mint pill, linear-gradient(180deg, #9df5c8, #4fd79a), 2px 7px 3px
                float bx = x0 + 31.9f + 9f + textW + 15f;
                var pill = Solid.Pic(c, "x2", Solid.VGradient(Mathf.Round(badgeW), 24.4f, 12.2f, Solid.Rgba(157, 245, 200), Solid.Rgba(79, 215, 154), Color.clear, 0f), Color.white, TL, TL, new Vector2(0, 0.5f), new Vector2(bx, -cy), new Vector2(Mathf.Round(badgeW), 24.4f));
                pill.transform.SetSiblingIndex(bt.transform.GetSiblingIndex()); earnPill = pill;
                bt.rectTransform.anchoredPosition = new Vector2(bx, -cy + 0.5f); bt.rectTransform.sizeDelta = new Vector2(badgeW, 20f);
            }
        }
        // .rlvl: "Level 5" 17px #ffe98a, the rank (a star and #fff3c8 on a promotion), "+1,100" and a shell at the right
        if (lvlTop >= 0f)
        {
            float cy = lvlTop + 1f + 7f + 13.2f, lx = In + 1f + 11f, rx = In + InW - 1f - 11f;
            string lv = "Level " + rc.level;
            var b = Txt(c, lv, 17, Gilt, lx, cy, 120f, TextAnchor.MiddleLeft); Tracking.On(b, 0.02f);
            float bW = b.preferredWidth + Tracking.Extra(lv, 17, 0.02f);
            Pic(c, "puka", 16.8f, rx - 16.8f, cy, Color.white);
            var em = Txt(c, "+" + Pane.N(rc.pay), 16, Earn, rx - 16.8f - 3f - 140f, cy, 140f, TextAnchor.MiddleRight);
            float emL = rx - 16.8f - 3f - em.preferredWidth;
            float sx = lx + bW + 8f, sw = Mathf.Max(20f, emL - 8f - sx);
            string rank = (rc.rankUp ? "★ " : "") + Rungs.RankAt(rc.level);
            var sp = Txt(c, rank, 14, rc.rankUp ? Solid.Rgba(255, 243, 200) : ShopArt.Mix(seen(sx + 20f, cy), Color.white, 0.85f), sx, cy, sw, TextAnchor.MiddleLeft, rc.rankUp ? FontStyle.Bold : FontStyle.Normal);
            Ellipsize(sp, rank, sw);
        }
        // .rgot: "UNLOCKED" 11.5px caps tracked .1em, then the names; or "NEXT" and what the next level opens
        if (gotTop >= 0f)
        {
            float cy = gotTop + 5f + 10.45f;
            string head = got ? "UNLOCKED" : "NEXT";
            string body = got ? string.Join(" · ", rc.unlocked.GetRange(0, Mathf.Min(3, rc.unlocked.Count))) + (rc.unlocked.Count > 3 ? " +" + (rc.unlocked.Count - 3) : "")
                              : "Lv " + nxL + " · " + nxName + (nxMore > 0 ? " +" + nxMore : "");
            Color hc = got ? ShopArt.Mix(seen(60f, cy), Solid.Rgba(255, 217, 138), 0.78f) : ShopArt.Mix(seen(60f, cy), Mist, 0.62f);
            Color bc = got ? Solid.Rgba(253, 243, 220) : ShopArt.Mix(seen(CardW * 0.5f, cy), Solid.Rgba(231, 238, 246), 0.80f);
            var hs = Txt(c, head, 11.5f, hc, 0f, cy + 0.6f, 100f, TextAnchor.MiddleLeft); Tracking.On(hs, 0.1f);
            float hW = hs.preferredWidth + Tracking.Extra(head, 11.5f, 0.1f);
            var bs = Txt(c, body, 13.5f, bc, 0f, cy, 300f, TextAnchor.MiddleLeft);
            float room = InW - 12f - hW - 7f;
            Ellipsize(bs, body, room);
            float bW = Mathf.Min(room, bs.preferredWidth), x0 = (CardW - (hW + 7f + bW)) * 0.5f;
            hs.rectTransform.anchoredPosition = new Vector2(x0, -(cy + 0.6f));
            bs.rectTransform.anchoredPosition = new Vector2(x0 + hW + 7f, -cy); bs.rectTransform.sizeDelta = new Vector2(bW + 4f, bs.rectTransform.sizeDelta.y);
        }
        // the goals: the icon (the drawing as a mask, in the row's colour), the words, the count, the bar
        for (int i = 0; i < goals.Count; i++)
        {
            var g = goals[i]; var d = Quests.Def(g.id);
            float cy = goalTop[i] + 1f + 8f + 10.85f, lx = In + 1f + 11f, rx = In + InW - 1f - 11f;
            Pic(c, "goal_" + GoalIcon(g.id), 19f, lx, cy, g.done ? Gilt : ShopArt.Mix(seen(lx + 9f, cy), Solid.Rgba(226, 240, 250), 0.96f));
            // the count, or a green tick and DONE +100 (the reward the quest sheet shows, Quests.Pay)
            string count = g.done ? "DONE +" + Quests.Pay : Pane.N(Mathf.Min(g.n, d.target)) + " / " + Pane.N(d.target);
            var b = Txt(c, count, 13f, g.done ? Gilt : Solid.Rgba(226, 240, 250), rx - 120f, cy + 0.6f, 120f, TextAnchor.MiddleRight);
            Tracking.On(b, 0.03f);
            float bW = b.preferredWidth + Tracking.Extra(count, 13f, 0.03f);
            bool advanced = fracs[i] > fracs0[i] + 0.001f;
            if (g.done)
            {
                const float TickD = 15f;
                var tick = Solid.Pic(c, "tick", CardFx.Tick(TickD, cardMade), Color.white, TL, TL, new Vector2(0.5f, 0.5f), new Vector2(rx - bW - 5f - TickD * 0.5f, -(cy + 0.3f)), new Vector2(TickD, TickD));
                if (advanced) fx.Pop(tick.rectTransform, BarAt(i) + BarDur - 0.05f, 0.32f);     // when its bar arrives
                bW += 5f + TickD;
            }
            float sx = lx + 20f + 9f, sw = rx - bW - 9f - sx;
            var s = Txt(c, d.text, 15, g.done ? Gilt : Solid.Rgba(248, 251, 255), sx, cy, sw, TextAnchor.MiddleLeft);
            Ellipsize(s, d.text, sw);
            // the fill: blue to gold across its own width (all gold once done), run on from where the run found it
            var tr = tracks[i];
            var fillSp = g.done ? CardFx.BarFill(tr.h, Gilt, Solid.Rgba(255, 204, 64), cardMade) : CardFx.BarFill(tr.h, Solid.Rgba(111, 220, 255), Earn, cardMade);
            var fill = Solid.Pic(c, "fill", fillSp, Color.white, TL, TL, TL, new Vector2(tr.x, -tr.y), new Vector2(tr.w * fracs[i], tr.h));
            fill.type = Image.Type.Sliced;
            if (advanced) fx.Grow(fill, BarAt(i), BarDur, tr.w * fracs0[i], tr.w * fracs[i]);
            else fill.enabled = fracs[i] > 0f;
        }

        // ---- the row: Again, then the dial's discs ----
        float rowTop = Y + cardH + 20f + 12f;
        System.Func<float, float, System.Func<float, float, Color>> under = (ox, oy) => (px, py) => bd.Under(ox + px, oy + py);
        var discs = new List<(string name, string icon, System.Action tap)>();
        if (Wheel.Revives > 0) discs.Add(("Life", "ob_life", Revive));          // the free life is the one that comes and goes
        discs.Add(("Menu", "ob_menu", () => { Sfx.Buzz(8); onMenu?.Invoke(); }));
        discs.Add(("Share", "ob_share", Share));
        float againW = CardW - discs.Count * (DiscD + DiscGap);
        {
            float ay = rowTop + DiscD - AgainH;                    // the row aligns its items to the foot
            // The one filled button on the card, so it reads as the way on: a warm gold pill, light
            // at the top to amber at the foot, with dark type, and a halo behind it that breathes
            // slowly. The halo and the pill's hair of growth are on children, so the press's own
            // scale on the button (PressAnim) is left alone.
            var again = ShopArt.Hit(cardRoot, "Again", TL, TL, TL, new Vector2(X, -ay), new Vector2(againW, AgainH));
            var halo = UiKit.Pic(again.transform, "halo", CardFx.Halo(againW, AgainH, 16f, cardMade), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ShopArt.Pad * 2f, ShopArt.Pad * 2f));
            halo.raycastTarget = false; halo.color = Solid.Rgba(255, 196, 72, 0.18f);
            var art = ShopArt.Art(again.transform, CardFx.GoldPill(againW, AgainH, under(X, ay), cardMade));
            fx.Breathe(halo, art.rectTransform, 2.8f, 0.14f, 0.42f, 0.012f);
            // the play triangle 18px, 8px, "Again" 17px tracked .6px, in the card's ink with a light seat under it
            var t = UiKit.Label(art.transform, "Again", 17, Ink, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 24), TextAnchor.MiddleCenter, 0f);
            ShopArt.Shade(t, Solid.Rgba(255, 246, 214), 0.45f, new Vector2(0f, -1f));
            Tracking.On(t, 0.6f / 17f);
            float tW = t.preferredWidth + Tracking.Extra("Again", 17, 0.6f / 17f), grp = 18f + 8f + tW;
            t.rectTransform.anchoredPosition = new Vector2(-grp * 0.5f + 26f + tW * 0.5f, 0f);
            var play = UiKit.Icon("ob_play");
            if (play != null)
            {
                var pi = UiKit.Pic(art.transform, "ob_play", play, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-grp * 0.5f + 9f, 0f), new Vector2(18f, 18f));
                pi.color = Ink; pi.raycastTarget = false; pi.preserveAspect = true;
                ShopArt.Shade(pi, Solid.Rgba(255, 246, 214), 0.45f, new Vector2(0f, -1f));
            }
            UiKit.Pressable(again.gameObject, null, () => { Sfx.Buzz(12); onPlay?.Invoke(); });
        }
        for (int i = 0; i < discs.Count; i++)
        {
            var dd = discs[i];
            float dx = X + againW + DiscGap + i * (DiscD + DiscGap);
            var disc = Glass(ShopArt.Glass.Disc, dx, rowTop, DiscD, DiscD, under(dx, rowTop), -1f);
            // svg 40% of the 52px inside, 1px, the word 9.5px tracked .7px: centred as a column
            Glyph(disc.transform, dd.icon, 20.8f, new Vector2(0f, DiscD * 0.5f - 22f));
            var lb = UiKit.Label(disc.transform, dd.name, 9.5f, Color.white, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, DiscD * 0.5f - 38.9f), new Vector2(DiscD, 12f), TextAnchor.MiddleCenter, 0f);
            ShopArt.Shade(lb, ShopArt.TextShade, 0.7f, new Vector2(0f, -1f));
            Tracking.On(lb, 0.7f / 9.5f);
            UiKit.Pressable(disc.gameObject, null, dd.tap);
        }
        // ---- the offers: one quiet strip under the row ----
        float offY = rowTop + DiscD + 22f, offW = (CardW - OfferGap) * 0.5f;
        Offer(bd, X, offY, offW, "Double coins", "puka", Color.white, !(runEarned > 0 && !runDoubled), DoubleCoins);     // .rwd[disabled] when there is nothing to double
        Offer(bd, X + offW + OfferGap, offY, offW, "Second life", "ob_life", Solid.Rgba(255, 128, 150), false, SecondLife);

        // #ver: the version, 13px at .55, 24px off the foot
        string vs = "v" + UiKit.Version;
        var ver = UiKit.Label(cardRoot, vs, 13, ShopArt.Mix(bd.Under(W * 0.5f, H - 31f), Color.white, 0.55f), FontStyle.Bold, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(200, 15), TextAnchor.MiddleCenter, 0f);
        ShopArt.Shade(ver, ShopArt.Rgb(0, 40, 80), 0.6f, new Vector2(0f, -1f));
        Tracking.On(ver, 0.5f / 13f);

        card.SetActive(true);
    }

    /// a frosted control: the glass drawn over the frame behind it, and a rect that takes the finger
    Image Glass(ShopArt.Glass g, float x, float y, float w, float h, System.Func<float, float, Color> backAt, float radius)
    {
        var hit = ShopArt.Hit(cardRoot, g.ToString(), TL, TL, TL, new Vector2(x, -y), new Vector2(w, h));
        var sp = ShopArt.PillOver(w, h, g, backAt, radius);
        cardMade.Add(sp.texture); cardMade.Add(sp);
        ShopArt.Art(hit.transform, sp);
        return hit;
    }

    /// .rwd: what it gives as a picture (the puka for coins, the heart for a life), and beside it the
    /// offer's name 13.5px over a small arrow and WATCH AD in gold caps tracked .14em, centred as one
    void Offer(CardArt.Backdrop bd, float x, float y, float w, string label, string icon, Color iconTint, bool off, System.Action<GameObject> tap)
    {
        var im = Glass(ShopArt.Glass.Offer, x, y, w, OfferH, (px, py) => bd.Under(x + px, y + py), 14f);
        const float IconD = 23f, IconGap = 9f;
        var pivL = new Vector2(0f, 0.5f);
        var t = UiKit.Label(im.transform, label, 13.5f, Color.white, FontStyle.Bold, TL, TL, pivL, new Vector2(0f, -16.4f), new Vector2(160f, 18f), TextAnchor.MiddleLeft, 0f);
        ShopArt.Shade(t, ShopArt.TextShade, 0.7f, new Vector2(0f, -1f));
        Tracking.On(t, 0.02f);
        float lW = t.preferredWidth + Tracking.Extra(label, 13.5f, 0.02f);
        // the arrow and the words share the em's .85; the arrow its own .9 on top
        Color back = ShopArt.Saturate(bd.Under(x + w * 0.5f, y + 31.6f), 1.35f);
        const string ad = "WATCH AD";
        var e = UiKit.Label(im.transform, ad, 10.5f, ShopArt.Mix(back, Gilt, 0.85f), FontStyle.Bold, TL, TL, pivL, new Vector2(0f, -31.6f), new Vector2(120f, 14f), TextAnchor.MiddleLeft, 0f);
        Tracking.On(e, 0.14f);
        float eW = e.preferredWidth + Tracking.Extra(ad, 10.5f, 0.14f), grp = 6f + 4f + eW;
        var ic = UiKit.Icon(icon);
        float iconW = ic != null ? IconD + IconGap : 0f;
        float x0 = Mathf.Max(10f, (w - (iconW + Mathf.Max(lW, grp))) * 0.5f), tx = x0 + iconW;
        if (ic != null)
        {
            var p = UiKit.Pic(im.transform, icon, ic, TL, TL, new Vector2(0.5f, 0.5f), new Vector2(x0 + IconD * 0.5f, -OfferH * 0.5f), new Vector2(IconD, IconD));
            p.color = iconTint; p.raycastTarget = false; p.preserveAspect = true;
            ShopArt.Shade(p, ShopArt.TextShade, 0.45f, new Vector2(0f, -1f));
        }
        t.rectTransform.anchoredPosition = new Vector2(tx, -16.4f); t.rectTransform.sizeDelta = new Vector2(lW + 4f, 18f);
        e.rectTransform.anchoredPosition = new Vector2(tx + 10f, -31.6f); e.rectTransform.sizeDelta = new Vector2(eW + 4f, 14f);
        var tri = UiKit.Icon("ob_tri");
        if (tri != null)
        {
            var a = UiKit.Pic(im.transform, "arrow", tri, TL, TL, new Vector2(0.5f, 0.5f), new Vector2(tx + 3f, -31.6f), new Vector2(6f, 8f));
            a.color = ShopArt.Mix(back, Gilt, 0.85f * 0.9f); a.raycastTarget = false;
        }
        if (off) Grey(im.gameObject, true);
        // the web's placeholder: a five-second clip, then the reward (web 27202-27243)
        UiKit.Pressable(im.gameObject, null, () => { if (!Greyed(im.gameObject)) tap?.Invoke(im.gameObject); });
    }

    // ---- the two offers and the free life all ride the one placeholder clip (web 27202-27255) ----
    // A real build hands this slot to a rewarded-ad SDK; nothing here talks to a network. Spent
    // offers grey out in place rather than going, so the row keeps its shape.
    int runEarned; bool runDoubled, clipOn;
    Text earnT, earnBadge; Image earnPk, earnPill; float earnCy, earnBadgeW;

    // A spent or unavailable offer is greyed as well as faded, so it cannot be mistaken for one
    // that is only sitting on a dark patch of sea: every picture and word in it is taken to grey
    // (the colours it had are kept, for an offer that comes back) and the whole is drawn at .38.
    readonly Dictionary<Graphic, Color> greyWas = new Dictionary<Graphic, Color>();
    void Grey(GameObject g, bool on)
    {
        var cg = g.GetComponent<CanvasGroup>(); if (cg == null) cg = g.AddComponent<CanvasGroup>();
        cg.alpha = on ? 0.38f : 1f; cg.interactable = !on;
        foreach (var gr in g.GetComponentsInChildren<Graphic>(true))
        {
            if (on)
            {
                if (greyWas.ContainsKey(gr)) continue;
                var c = gr.color; if (c.a <= 0f) continue;       // the clear rect that takes the finger
                greyWas[gr] = c;
                float l = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
                float k = gr is Text ? 0.85f : 0.62f;            // the glass and the pictures darker than the words, so the words still read
                gr.color = new Color(l * k, l * k, l * k, c.a);
            }
            else if (greyWas.TryGetValue(gr, out var was)) { gr.color = was; greyWas.Remove(gr); }
        }
    }

    // the goals' bars run in one after another once the card is up, each over BarDur seconds
    const float BarDur = 0.6f;
    static float BarAt(int i) => 0.55f + 0.12f * i;

    /// NEW BEST: a small gold ribbon straddling the top edge of a figure's tile, centred at (cx, cy)
    /// in card px, that pops in at `at` seconds, as its figure finishes counting, and has a shine
    /// cross it once
    void NewBest(Transform c, CardFx fx, float cx, float cy, float at)
    {
        const float RW = 76f, RH = 16f;
        const string word = "NEW BEST";
        var sp = CardFx.Capsule(RW, RH, Solid.Rgba(255, 240, 170), Solid.Rgba(255, 210, 92), Solid.Rgba(244, 168, 44), Solid.Rgba(150, 84, 10, 0.65f),
                                0.30f, 0f, 0f, null, cardMade, "new best");
        var rib = Solid.Pic(c, "newBest", sp, Color.white, TL, TL, new Vector2(0.5f, 0.5f), new Vector2(cx, -cy), new Vector2(RW, RH));
        var mask = rib.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = true;      // the shine stays inside the ribbon's shape
        var shine = Solid.Pic(rib.transform, "shine", CardFx.ShineBand(cardMade), new Color(1f, 1f, 1f, 0.85f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, RH * 2.2f));
        shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -22f);
        var t = UiKit.Label(rib.transform, word, 9.5f, Ink, FontStyle.Bold, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(RW, RH), TextAnchor.MiddleCenter, 0f);
        ShopArt.Shade(t, Solid.Rgba(255, 248, 220), 0.5f, new Vector2(0f, -1f));
        Tracking.On(t, 0.1f);
        fx.Pop(rib.rectTransform, at, 0.38f);
        fx.Sweep(shine.rectTransform, at + 0.32f, 0.6f, -RW * 0.5f - 14f, RW * 0.5f + 14f);
    }
    static bool Greyed(GameObject g) { var cg = g.GetComponent<CanvasGroup>(); return cg != null && !cg.interactable; }

    /// DOUBLE COINS: the run's earnings paid a second time once the clip has played, and the card says so
    void DoubleCoins(GameObject g)
    {
        if (runDoubled || runEarned <= 0 || clipOn) return;
        runDoubled = true; Grey(g, true); Sfx.Buzz(10);
        Stats.Bump("revives");                                   // the web counts every clip watched here
        clipOn = true;
        AdClip.Play(canvas.transform, () =>
        {
            clipOn = false;
            SaveData.Coins += runEarned; Stats.Bump("earned", runEarned); SaveData.Flush();
            SetEarned(runEarned * 2);
        });
    }

    /// SECOND LIFE: shown on every card and pays out either way, spending the free life first if
    /// it is still in hand (web 27232-27242)
    void SecondLife(GameObject g)
    {
        if (clipOn) return;
        if (Wheel.Revives > 0) Wheel.Revives--;
        Grey(g, true); Sfx.Buzz(10);
        Stats.Bump("revives");
        clipOn = true;
        AdClip.Play(canvas.transform, () => { clipOn = false; Grey(g, false); ShowCard(false, null); onRevive?.Invoke(); });
    }

    /// the earned line rewritten in place after a double, and the row centred again on its new width
    void SetEarned(int n)
    {
        if (earnT == null) return;
        var cu = earnT.GetComponent<CountUp>(); if (cu != null) cu.enabled = false;
        string full = "EARNED: " + Pane.N(n) + " SHELLS";
        earnT.text = full;
        float textW = earnT.preferredWidth + Tracking.Extra(full, 22, 0.05f);
        float rowW = 31.9f + 9f + textW + (earnBadge != null ? 15f + earnBadgeW : 0f), x0 = (CardW - rowW) * 0.5f;
        if (earnPk != null) earnPk.rectTransform.anchoredPosition = new Vector2(x0, -earnCy);
        earnT.rectTransform.anchoredPosition = new Vector2(x0 + 31.9f + 9f, -earnCy); earnT.rectTransform.sizeDelta = new Vector2(textW + 4f, 36f);
        if (earnBadge != null)
        {
            float bx = x0 + 31.9f + 9f + textW + 15f;
            if (earnPill != null) earnPill.rectTransform.anchoredPosition = new Vector2(bx, -earnCy);
            earnBadge.rectTransform.anchoredPosition = new Vector2(bx, -earnCy + 0.5f);
        }
    }

    /// the chest ceremony plays over the crash with nothing of the HUD's up and the card held back
    /// behind it (web showMenu('over') hides #hud, then crateCeremony hides the overlay)
    public void ShowNone()
    {
        menu.SetActive(false); ride.SetActive(false); bar.SetActive(false); jump.SetActive(false);
        card.SetActive(false); DropCard();
    }

    /// a line of the card's type: left edge x, centred on cy, w wide, in card px
    static Text Txt(Transform c, string s, float size, Color col, float x, float cy, float w, TextAnchor al, FontStyle st = FontStyle.Bold)
    {
        var t = UiKit.Label(c, s, size, col, st, TL, TL, new Vector2(0, 0.5f), new Vector2(x, -cy), new Vector2(w, Mathf.Ceil(size * 1.6f)), al, 0.3f);
        return t;
    }

    /// a picture from Resources/UI, size px square, its left edge at x and centred on cy
    static Image Pic(Transform c, string name, float size, float x, float cy, Color tint)
    {
        var sp = UiKit.Icon(name); if (sp == null) return null;
        var im = UiKit.Pic(c, name, sp, TL, TL, new Vector2(0, 0.5f), new Vector2(x, -cy), new Vector2(size, size));
        im.preserveAspect = true; im.raycastTarget = false; im.color = tint;
        return im;
    }

    /// one of the dial's glyphs in #f2d391 with its drop shadow, centred at pos in the control
    static void Glyph(Transform p, string name, float size, Vector2 pos)
    {
        var sp = UiKit.Icon(name); if (sp == null) return;
        var im = UiKit.Pic(p, name, sp, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
        im.color = Rope; im.raycastTarget = false; im.preserveAspect = true;
        ShopArt.Shade(im, Solid.Rgba(0, 16, 22), 0.6f, new Vector2(0f, -1f));
    }

    /// the web's GOAL_POOL icons (web 7524), read off the pool so the card and the quest sheet agree
    static string GoalIcon(string id) { var d = Quests.Def(id); return string.IsNullOrEmpty(d.icon) ? "wave" : d.icon; }

    /// text-overflow: ellipsis
    static void Ellipsize(Text t, string full, float w)
    {
        t.text = full;
        if (t.preferredWidth <= w) return;
        for (int n = full.Length - 1; n > 1; n--)
        {
            t.text = full.Substring(0, n).TrimEnd() + "…";
            if (t.preferredWidth <= w) return;
        }
    }

    // the free life: one second chance a run, two when the wheel banked one. It plays the same
    // five-second placeholder before putting him back (web 27244-27255).
    void Revive()
    {
        if (Wheel.Revives <= 0 || clipOn) return;
        Wheel.Revives--; Sfx.Buzz(10);
        Stats.Bump("revives");
        clipOn = true;
        AdClip.Play(canvas.transform, () => { clipOn = false; ShowCard(false, null); onRevive?.Invoke(); });
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
        menu.SetActive(true); ride.SetActive(false); card.SetActive(false); DropCard();
        tMenuCoins.text = SaveData.Coins.ToString("N0");
        tMenuBest.text = "Level " + Progress.Level; if (xpFill != null) xpFill.fillAmount = Progress.Frac;
        if (pip != null) pip.gameObject.SetActive(Wheel.Ready);
        SizeBar();
    }

    public void ShowRide()
    {
        menu.SetActive(false); ride.SetActive(true); card.SetActive(false); bar.SetActive(true); jump.SetActive(true); DropCard();
        holdOff = Input.touchCount > 0 || Input.GetMouseButton(0);
        ApplyTrickSlots();
        DropSteer(); auxId = -1; auxArmed = false; auxHand = false;   // no finger or drag carried over from the last screen
        tBestS.text = "best " + SaveData.Best.ToString("N0"); tBestD.text = "best " + SaveData.BestDist.ToString("N0") + " m";
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
        // the frame for the card's glass is read at the end of a drawn frame; if none comes (a
        // device that has stopped presenting), the card still goes up, on the even sea colour
        if (cardPending && Time.unscaledTime - cardAskedT > 0.5f) BuildCardNow(null);
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
        StepRope();
        // The trick column stands down under the water and on the sand, and JUMP on the sand (under
        // the water it is the one button there, held to climb). Both go the moment he wipes out,
        // not when the card comes up (web 27912, 45620-45632). Anything held on the column is let
        // go as it hides, since a finger on a button that is no longer there never lifts off it.
        bool ashore = island != null && island.Ashore, under = dive != null && dive.On;
        bool showBar = rider.running && !under && !ashore, showJump = rider.running && !ashore;
        if (bar.activeSelf != showBar) { if (!showBar) for (int s = 0; s < TrickCatalog.SlotCount; s++) SlotUp(s); bar.SetActive(showBar); }
        if (jump.activeSelf != showJump) { if (!showJump && dive != null) dive.jumpHeld = false; jump.SetActive(showJump); }
        // the button lights while that axis is squared up, which is what teaches the timing (web 45633-45640)
        if (showBar)
            for (int s = 0; s < TrickCatalog.SlotCount; s++)
            {
                if (slotReady[s] == null) continue;
                string id = slotOn[s];
                bool rdy = id == "roll" ? rider.flipHeld && rider.flipRot > 0.5f && Square(rider.flipRot)
                         : id == "flip" ? rider.pitchHeld && rider.pitchRot > 0.5f && Square(rider.pitchRot)
                         : id == "spin" ? rider.spinHeld && rider.spinRot > 0.5f && Square(rider.spinRot)
                         : id == "heli" && rider.heliAir && rider.flipRot > 0.5f && Square(rider.flipRot);
                if (slotReady[s].activeSelf != rdy) slotReady[s].SetActive(rdy);
            }
        if (jumpCg != null) jumpCg.alpha = rider.TrickReady ? 0.25f : 1f;   // web 45641
        // the band (web 47217-47228): every figure with its separators, and each record counting
        // the run in progress, lit for as long as the run is past it
        int ds = Mathf.RoundToInt(rider.dist);
        tScore.text = rider.score.ToString("N0");
        tDist.text = ds.ToString("N0") + " m";
        tMph.text = ((int)(rider.eff * 0.62f)).ToString();
        tCoins.text = rider.coins.ToString("N0");
        tMsg.text = rider.msgT > 0 ? rider.lastMsg : "";
        tBestS.text = "best " + Mathf.Max(SaveData.Best, rider.score).ToString("N0");
        tBestD.text = "best " + Mathf.Max(SaveData.BestDist, ds).ToString("N0") + " m";
        tBestS.color = rider.score > SaveData.Best ? RecNew : RecOld;
        tBestD.color = ds > SaveData.BestDist ? RecNew : RecOld;

        // steering: keys, or a finger (or the mouse) dragged on the water, which also throws tricks
        float k2 = (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? -1 : 0) + (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0);
        StepGestures(ashore, under);
        rider.steer = Mathf.Clamp(k2 + steerVal, -1f, 1f);

        // the keys (web 23499-23521): J spins and K rolls, but only once he is up, and neither pops
        // him; L takes the rail in the air and puts a paw in the water on it; H stands on his hands;
        // 1 to 5 are the five buttons, whatever is on them. R is the rope button, for a keyboard.
        if (Input.GetKeyDown(KeyCode.Space)) { Sfx.Buzz(10); rider.Jump(); }
        if (Input.GetKeyDown(KeyCode.J)) rider.StartTrick("spin"); if (Input.GetKeyUp(KeyCode.J)) rider.ReleaseTrick("spin");
        if (Input.GetKeyDown(KeyCode.K)) rider.StartTrick("roll"); if (Input.GetKeyUp(KeyCode.K)) rider.ReleaseTrick("roll");
        if (Input.GetKeyDown(KeyCode.L)) { if (rider.airborne || rider.hanging) rider.StartTrick("grab"); else rider.StartDrag(); }
        if (Input.GetKeyUp(KeyCode.L)) { rider.EndGrab(); rider.EndDrag(); }
        if (Input.GetKeyDown(KeyCode.H)) rider.Hold("hand", true); if (Input.GetKeyUp(KeyCode.H)) rider.Hold("hand", false);
        for (int s = 0; s < TrickCatalog.SlotCount && s < SlotKeys.Length; s++)
        {
            if (Input.GetKeyDown(SlotKeys[s])) SlotDown(s);
            if (Input.GetKeyUp(SlotKeys[s])) SlotUp(s);
        }
        if (Input.GetKeyDown(KeyCode.R)) rider.onRope?.Invoke();      // the ROPE! button, for a keyboard
    }

    static readonly KeyCode[] SlotKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
    static readonly Color RecOld = new Color(1, 1, 1, 0.52f), RecNew = new Color(1f, 0.886f, 0.369f, 0.95f);   // #hud .rec, and .rec.new (web 244)
    static bool Square(float a) => Mathf.Abs(Mathf.Repeat(a + Mathf.PI, Mathf.PI * 2f) - Mathf.PI) < 0.45f;

    // ---- the gestures on the water (web 23530-23735, 23906-23919) ----
    // Tap to jump, tap again in the air for another roll; swipe up or down for a roll that way,
    // and across in the air for a flip; draw a loop for a spin; hold still to grab the rail in the
    // air or drag a paw on the water; a second finger held is a handstand. All of it is measured
    // from where the finger went down, kept apart from the steering anchor, which is dragged along
    // as the swipe runs past its stop so a long carve can keep going. Positions are taken in the
    // page's own terms (y down from the top) so every sign below reads as the web's does, and the
    // web's CSS pixels are the canvas's units.
    const float TapS = 0.270f, HoldS = 0.235f, TapSlop = 24f;
    const int MouseId = -99;
    float touchX0, gT0, gX0, gY0, gMove, gFireT = -99f;
    bool gSwiped, gGrab, gHoldArmed;
    readonly List<Vector2> cPts = new List<Vector2>();
    float cAng, cLast; bool cHas, cCirc;
    int auxId = -1; float auxT0; bool auxHand, auxArmed;
    bool touchMode;                                       // a real touch has been seen: the mouse stands down for good
    Vector2 lastMouse;

    Vector2 Page(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y);
    float Css => canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : Screen.width / 430f;

    // a finger lost without an end (the system took it) also lets go of a grab or a drag it held
    void DropSteer() { if (gGrab) { rider.EndGrab(); rider.EndDrag(); gGrab = false; } steerPointer = -1; steerVal = 0f; gHoldArmed = false; }
    bool holdOff;
    void CircleReset() { cPts.Clear(); cAng = 0f; cCirc = false; cHas = false; }
    void Fired(Vector2 p) { gFireT = Time.unscaledTime; gX0 = p.x; gY0 = p.y; }
    // one gesture per flick: without this a single swipe throws three rotations before the finger stops
    bool CanFire() => Time.unscaledTime - gFireT > 0.25f;

    /// a loop drawn with one finger: the angle swept round a rolling centre of the recent path, so
    /// where on the glass it is drawn and how big does not matter; three quarters of a turn is a spin
    int CircleFeed(Vector2 p)
    {
        cPts.Add(p);
        if (cPts.Count > 12) cPts.RemoveAt(0);
        if (cPts.Count < 4) return 0;
        Vector2 m = Vector2.zero;
        foreach (var q in cPts) m += q;
        m /= cPts.Count;
        if ((p - m).magnitude < Screen.width * 0.045f) { cHas = false; return 0; }   // too tight to be a deliberate loop
        float a = Mathf.Atan2(p.y - m.y, p.x - m.x);
        if (cHas)
        {
            float d = a - cLast;
            while (d > Mathf.PI) d -= Mathf.PI * 2f;
            while (d < -Mathf.PI) d += Mathf.PI * 2f;
            if (Mathf.Abs(d) < 1.2f) cAng += d;                      // jumps are not sweep
            if (Mathf.Abs(cAng) > 0.55f) cCirc = true;               // flagged early, so the first quarter is not read as a flick
        }
        cLast = a; cHas = true;
        if (Mathf.Abs(cAng) > Mathf.PI * 1.5f)
        {
            int dir = cAng > 0f ? -1 : 1;
            cAng = 0f; cPts.Clear();
            return dir;
        }
        return 0;
    }

    void GestureStart(Vector2 p)
    {
        touchX0 = p.x;
        gT0 = Time.unscaledTime; gX0 = p.x; gY0 = p.y; gMove = 0f; gSwiped = false; gGrab = false;
        CircleReset(); gFireT = -99f;
    }

    // the steering finger moved: steer by it, and read it for a loop (web 23637-23667)
    void GestureMove(Vector2 p, bool under, bool mouse)
    {
        float range = Screen.width * 0.16f;
        float v = (p.x - touchX0) / range;
        if (v > 1f) { touchX0 = p.x - range; v = 1f; }               // let the swipe keep going
        if (v < -1f) { touchX0 = p.x + range; v = -1f; }
        steerVal = v;                                                // side to side and nothing else
        if (under) return;                                           // the dive reads the vertical itself, and nothing down there is a trick
        float dx = p.x - gX0, dy = p.y - gY0;
        gMove = Mathf.Max(gMove, Mathf.Sqrt(dx * dx + dy * dy));
        int loop = CircleFeed(p);
        if (loop != 0 && CanFire()) { gSwiped = true; gHoldArmed = false; rider.AddSpin(loop); Fired(p); }
        else if (!mouse && !gGrab && (Mathf.Abs(dx) > TapSlop * Css || Mathf.Abs(dy) > TapSlop * Css)) gHoldArmed = false;
    }

    // the steering finger lifted: a tap, a straight swipe, or the end of a hold (web 23679-23695, 23906-23916)
    void GestureEnd(Vector2 p)
    {
        float dx = p.x - gX0, dy = p.y - gY0;
        bool up = rider.airborne || rider.hanging;
        if (gGrab) { rider.EndGrab(); rider.EndDrag(); }
        else if (!gSwiped && Time.unscaledTime - gT0 < TapS && gMove < TapSlop * Css)
        {
            if (up) rider.AddRoll(rider.flipSign);                   // keep tapping, keep rolling
            else rider.Jump();
        }
        else if (!cCirc && !gSwiped)
        {
            if (Mathf.Abs(dy) > Screen.height * 0.09f && Mathf.Abs(dy) > Mathf.Abs(dx) * 1.3f) rider.AddRoll(dy < 0f ? 1f : -1f);
            else if (up && Mathf.Abs(dx) > Screen.width * 0.19f && Mathf.Abs(dx) > Mathf.Abs(dy) * 1.3f) rider.AddFlip(dx > 0f ? 1f : -1f);
        }
    }

    void ReleaseAux()
    {
        auxId = -1; auxArmed = false;
        if (auxHand) { rider.handOn = false; auxHand = false; }
    }

    void StepGestures(bool ashore, bool under)
    {
        float now = Time.unscaledTime;
        var es = EventSystem.current;
        if (Input.touchCount > 0) touchMode = true;
        // the press that started the run is not the run's first gesture: nothing new begins until
        // every finger (and the mouse) has come off the glass once
        if (holdOff && Input.touchCount == 0 && !Input.GetMouseButton(0)) holdOff = false;
        // a finger the system took away without an end (a notification, a system swipe) would hold
        // its slot for good; reclaim both (web 23597-23617)
        if (steerPointer >= 0 && !Alive(steerPointer)) DropSteer();
        if (auxId >= 0 && !Alive(auxId)) ReleaseAux();
        for (int i = 0; i < Input.touchCount; i++)
        {
            var tc = Input.GetTouch(i);
            var p = Page(tc.position);
            if (tc.phase == TouchPhase.Began)
            {
                // ashore the glass is not a steering wheel; the walk ring has its own finger
                if (ashore) continue;
                if (UiKit.OverUI(tc.position)) continue;
                if (holdOff) continue;                    // the finger that pressed Play is still down
                if (steerPointer < 0) { steerPointer = tc.fingerId; GestureStart(p); gHoldArmed = true; }
                else if (auxId < 0 && rider.running) { auxId = tc.fingerId; auxT0 = now; auxHand = false; auxArmed = true; }
            }
            else if (tc.fingerId == steerPointer && tc.phase == TouchPhase.Moved) GestureMove(p, under, false);
            if (tc.phase == TouchPhase.Ended || tc.phase == TouchPhase.Canceled)
            {
                if (tc.fingerId == auxId) ReleaseAux();
                if (tc.fingerId == steerPointer)
                {
                    gHoldArmed = false;
                    // under the water this finger was swimming, not performing
                    if (!under) { float dx = p.x - gX0, dy = p.y - gY0; gMove = Mathf.Max(gMove, Mathf.Sqrt(dx * dx + dy * dy)); GestureEnd(p); }
                    DropSteer();
                }
            }
        }
        // held still: take hold of the rail in the air, or put a paw in the water on it (web 23625-23629)
        if (gHoldArmed && steerPointer >= 0 && now - gT0 >= HoldS)
        {
            gHoldArmed = false;
            if (!(gMove > TapSlop * Css || gSwiped || under))
            {
                gGrab = true;
                if (rider.airborne || rider.hanging) rider.StartTrick("grab"); else rider.StartDrag();
            }
        }
        // a second finger held is a handstand (web 23630-23634)
        if (auxArmed && auxId >= 0 && now - auxT0 >= HoldS + 0.015f) { auxArmed = false; auxHand = true; rider.handOn = true; }
        if (touchMode)
        {
            if (Input.touchCount == 0) { if (steerPointer >= 0) DropSteer(); if (auxId >= 0) ReleaseAux(); }
            return;
        }
        // ---- the mouse, for a desktop: the same drag, loops and flicks, but no hold (web 23708-23736) ----
        bool overUi = UiKit.OverUI(Input.mousePosition);
        var mp = Page(Input.mousePosition);
        if (Input.GetMouseButtonDown(0) && rider.running && !ashore && !overUi && steerPointer < 0 && !holdOff)
        { steerPointer = MouseId; GestureStart(mp); lastMouse = mp; }
        if (steerPointer == MouseId)
        {
            if (Input.GetMouseButton(0)) { if (mp != lastMouse) GestureMove(mp, under, true); lastMouse = mp; }
            else
            {
                if (rider.running && !under) GestureEnd(lastMouse);
                rider.EndGrab(); rider.EndDrag();
                DropSteer();
            }
        }
    }

    bool Alive(int id)
    {
        if (id == MouseId) return true;
        for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).fingerId == id) return true;
        return false;
    }
}
