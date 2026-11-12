// Opening the chest (web 27412-27600, 1488-1514, 2888-2897). Catching a chest on the run only
// keeps it; after the run, before the card, it floats in front of whatever shot the crash left,
// spinning slowly behind a mostly clear overlay. Each tap shakes it harder; on the twelfth it
// turns to face you and the light comes up out of the box, and three quarters of a second later
// a little card over its opening says what was inside. The reward is rolled at that moment:
// mostly puka, rarely a board, very rarely a surfer. One more tap carries on to the run card.
//
// The chest is drawn OVER the world, not in it (web CHEST_LAYER): the run can end anywhere --
// alongside a buoy, inside a jet ski's wake -- and sharing the scene's depth it would cut through
// whatever is there. It is on a layer of its own that the main camera leaves out, drawn by an
// overlay camera stacked on it, onto a cleared depth buffer: on top of everything, and still
// hiding its own back. It is the same chest model the Crate floats down the lane; that model is
// a single mesh with its lid modelled open, so, as on the web once its model has loaded, there is
// no lid to swing, and the opening is the turn to face you and the light inside.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

public class ChestCeremony : MonoBehaviour
{
    public Camera cam; public Transform ui;
    public bool Showing => show;

    const int Layer = 29;                                            // nothing else draws on it (Previews has 30)
    const float Dist = 7.2f, Sc = 1.26f, Drop = 1.30f, Glow = 0.16f; // web CH_DIST, CH_SC, CH_DROP, CH_GLOW
    const int TapsToOpen = 12; const float RewardWait = 0.75f;

    Transform chest, inner; Renderer[] chestR; Material innerMat; Camera over;
    RectTransform overlay, reward; Text hint;
    bool show, opened, done, faceSet; int taps; float lid, shakeE, spin, face, faceTo, rewardIn = -1f;
    System.Action then;
    struct Prize { public string kind, name; public int n; }
    Prize got;

    /// the chest up in front of the lens, and `carryOn` called once the player has opened it and tapped on
    public void Play(System.Action carryOn)
    {
        Build();
        then = carryOn;
        taps = 0; opened = false; lid = 0f; shakeE = 0f; spin = 0f; done = false; faceSet = false; rewardIn = -1f;
        if (reward != null) { Destroy(reward.gameObject); reward = null; }
        hint.text = "Tap it open!"; hint.gameObject.SetActive(true);
        overlay.SetAsLastSibling(); overlay.gameObject.SetActive(true);
        chest.gameObject.SetActive(true);
        if (over != null) over.gameObject.SetActive(true);
        show = true;
        Place(0f);
    }

    /// teardown only: the run card is the caller's business (web crateCeremonyEnd)
    public void End()
    {
        show = false; then = null; rewardIn = -1f;
        if (chest != null) chest.gameObject.SetActive(false);
        if (overlay != null) overlay.gameObject.SetActive(false);
        if (over != null) over.gameObject.SetActive(false);
    }

    // put the chest away and give the screen to the card that has been waiting behind it (web crateCarryOn)
    void CarryOn() { var cb = then; End(); cb?.Invoke(); }

    void Tap()
    {
        if (!show) return;
        // Once the reward is up the next tap is the way on. Not during the three quarters of a
        // second before it: somebody tapping twelve times fast is still tapping when the twelfth
        // lands, and must not skip the one thing the whole ceremony is for.
        if (opened) { if (done) CarryOn(); return; }
        taps++;
        Sfx.Buzz(10);
        shakeE = Mathf.Min(1.6f, shakeE + 0.55f * Mathf.Min(1f, taps / 6f));
        if (taps >= TapsToOpen)
        {
            opened = true;
            Sfx.Buzz(25, 35, 90);
            hint.gameObject.SetActive(false);
            got = Roll();
            rewardIn = RewardWait;                                  // the lid takes about that long; the words wait for the light
        }
    }

    /// what was inside (web crateRoll 27550-27572): a surfer you do not have, 1.2% of the time; a
    /// board you do not have, 2% more; otherwise puka -- 200 to 350 one time in ten, 100 a fifth of
    /// the time, 50 a third, else 25. Banked as soon as it is rolled.
    static Prize Roll()
    {
        var lockedC = RiderCatalog.All.FindAll(c => !SaveData.OwnsRider(c.id));
        var lockedB = BoardCatalog.All.FindAll(b => !SaveData.Owns(b.id));
        float r = Random.value;
        if (lockedC.Count > 0 && r < 0.012f)
        {
            var c = lockedC[Random.Range(0, lockedC.Count)];
            SaveData.S.ownedChars.Add(c.id); SaveData.Flush();
            return new Prize { kind = "char", name = c.name };
        }
        if (lockedB.Count > 0 && r < 0.032f)
        {
            var b = lockedB[Random.Range(0, lockedB.Count)];
            SaveData.S.owned.Add(b.id); SaveData.Flush();
            return new Prize { kind = "board", name = b.name };
        }
        float q = Random.value;
        int n = q < 0.10f ? 200 + 25 * Random.Range(0, 7) : q < 0.32f ? 100 : q < 0.64f ? 50 : 25;
        SaveData.Coins += n; SaveData.Flush();
        return new Prize { kind = "puka", n = n };
    }

    void LateUpdate()
    {
        if (!show || cam == null) return;
        float dt = Mathf.Min(0.1f, Time.unscaledDeltaTime);
        if (rewardIn > 0f)
        {
            rewardIn -= dt;
            if (rewardIn <= 0f)
            {
                ShowReward();
                hint.text = "Tap to carry on"; hint.gameObject.SetActive(true);
                done = true;
            }
        }
        Place(dt);
    }

    // ---- where it floats and how it moves (web placeCeremonyChest 27471-27528) ----
    void Place(float dt)
    {
        var ct = cam.transform; var cp = ct.position;
        // a few feet in front of the lens, and dropped by an amount scaled with that distance so
        // pushing it back does not also walk it up the frame
        var p = cp + ct.forward * Dist; p.y -= Drop * (Dist / 4.9f);
        chest.position = p;
        shakeE = Mathf.Max(0f, shakeE - dt * 2.2f);
        // Spins while it is being tapped; the moment it opens it turns to FACE the camera, lock
        // toward you, so you look into the box
        if (!opened) { spin += dt * (0.9f + shakeE * 7f); faceSet = false; }
        else
        {
            float want = Mathf.Atan2(cp.x - p.x, cp.z - p.z);
            if (!faceSet)
            {
                faceSet = true; face = spin % (Mathf.PI * 2f);
                float d = want - face;
                while (d > Mathf.PI) d -= Mathf.PI * 2f; while (d < -Mathf.PI) d += Mathf.PI * 2f;
                faceTo = face + d;
            }
            face += (faceTo - face) * Mathf.Min(1f, dt * 5f);
            spin = face;
        }
        chest.rotation = BoardController.Xyz((Random.value - 0.5f) * 0.16f * shakeE, spin, (Random.value - 0.5f) * 0.16f * shakeE);
        chest.localScale = Vector3.one * (Sc * (1f + 0.06f * shakeE));
        if (opened) lid = Mathf.Min(1f, lid + dt * 1.5f);
        float e = lid * lid * (3f - 2f * lid);
        // the light off the treasure, dimmed to a suggestion and kept inside the box
        if (inner != null)
        {
            inner.rotation = ct.rotation;                            // a sprite: it always faces the lens
            inner.localScale = Vector3.one * (0.80f + e * 0.22f);
            innerMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, e * Glow));
            inner.gameObject.SetActive(e > 0.001f);
        }
        if (over != null) { over.fieldOfView = cam.fieldOfView; over.nearClipPlane = 0.1f; }
        cam.cullingMask &= ~(1 << Layer);
        PlaceReward();
    }

    // pinned over the OPENING, projected from the chest itself every frame so it rides the float
    // and the settle: 0.62 of the way up the chest's box is its lip (web placeCrateReward)
    void PlaceReward()
    {
        if (reward == null || chestR == null || chestR.Length == 0) return;
        var b = chestR[0].bounds; foreach (var r in chestR) if (r != null) b.Encapsulate(r.bounds);
        var lip = new Vector3(chest.position.x, b.min.y + (b.max.y - b.min.y) * 0.62f, chest.position.z);
        var sp = cam.WorldToScreenPoint(lip);
        if (sp.z <= 0f) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, sp, null, out var local)) reward.anchoredPosition = local;
    }

    // ---- the reward card: "+n" and a shell, or the name and "View the shop to equip." (web 27591-27593) ----
    static readonly Color Gold = Solid.Rgba(255, 223, 46);
    void ShowReward()
    {
        reward = UiKit.Rect(overlay, "crateReward", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var bg = reward.gameObject.AddComponent<Image>(); bg.raycastTarget = false;
        // .big: min(7.4vw, 30px) bold gold, line-height 1.15, a seat of a shadow under it
        const float Big = 30f, BigH = 34.5f, Sub = 13f, SubH = 15.6f;
        var big = UiKit.Label(reward, got.kind == "puka" ? "+" + got.n : got.name, Big, Gold, FontStyle.Bold, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0f, -7f), new Vector2(400f, BigH), TextAnchor.MiddleCenter, 0f);
        ShopArt.Shade(big, Solid.Rgba(64, 36, 7), 0.5f, new Vector2(0f, -2f));
        float bigW = big.preferredWidth, w = bigW, h = 7f + BigH + 8f;
        if (got.kind == "puka")
        {
            // i.pk: 1em, its margins .14em before and .1em after
            float pk = Big, rowW = bigW + Big * 0.14f + pk + Big * 0.1f;
            big.alignment = TextAnchor.MiddleLeft;
            big.rectTransform.sizeDelta = new Vector2(bigW + 4f, BigH);
            big.rectTransform.anchoredPosition = new Vector2(-rowW * 0.5f + (bigW + 4f) * 0.5f, -7f);
            var sp = UiKit.Icon("puka");
            if (sp != null)
            {
                var im = UiKit.Pic(reward, "pk", sp, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0f, 0.5f), new Vector2(-rowW * 0.5f + bigW + Big * 0.14f, -7f - BigH * 0.5f - 0.14f * Big * 0.5f), new Vector2(pk, pk));   // vertical-align -.14em
                im.preserveAspect = true; im.raycastTarget = false;
            }
            w = rowW;
        }
        else
        {
            // .sub: 13px #eaf6ff at .92, 3px under
            var sub = UiKit.Label(reward, "View the shop to equip.", Sub, Solid.Rgba(234, 246, 255, 0.92f), FontStyle.Normal, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0f, -7f - BigH - 3f), new Vector2(400f, SubH), TextAnchor.MiddleCenter, 0f);
            w = Mathf.Max(bigW, sub.preferredWidth);
            h += 3f + SubH;
        }
        w += 30f;                                                     // 15px either side
        reward.sizeDelta = new Vector2(w, h);
        // rgba(6,20,34,.60) on a 14px radius, a 1px rim of rgba(255,223,46,.34)
        bg.sprite = Solid.Rounded(Mathf.Round(w), Mathf.Round(h), 14f); bg.color = Solid.Rgba(6, 20, 34, 0.60f);
        var rim = UiKit.Pic(reward, "rim", Solid.Rounded(Mathf.Round(w), Mathf.Round(h), 14f, 1f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        rim.color = Solid.Rgba(255, 223, 46, 0.34f); rim.raycastTarget = false;
        rim.transform.SetSiblingIndex(0);
        PlaceReward();
    }

    // ---- built once ----
    void Build()
    {
        if (chest != null) return;
        // the chest, on its own layer
        chest = new GameObject("ceremony chest").transform;
        Models.Spawn("chest", chest, 1.26f, PrimitiveType.Cube);
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad); g.name = "inner glow";
        var col = g.GetComponent<Collider>(); if (col != null) Destroy(col);
        g.transform.SetParent(chest, false); g.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        innerMat = Bootstrap.SoftParticles(GlowTex());
        if (innerMat != null)
        {
            // additive, as the web's sprite is
            innerMat.SetFloat("_Blend", 2f);
            innerMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            innerMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            g.GetComponent<Renderer>().sharedMaterial = innerMat;
        }
        else innerMat = g.GetComponent<Renderer>().material;
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        inner = g.transform;
        foreach (var t in chest.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
        var rs = new System.Collections.Generic.List<Renderer>();
        foreach (var r in chest.GetComponentsInChildren<Renderer>(true)) { if (r.transform == inner) continue; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rs.Add(r); }
        chestR = rs.ToArray();
        chest.gameObject.SetActive(false);

        // the lens that draws it, stacked over the main one onto a cleared depth buffer
        var main = cam != null ? cam.GetComponent<UniversalAdditionalCameraData>() : null;
        if (main != null)
        {
            var lens = new GameObject("ceremony lens"); lens.layer = Layer;
            lens.transform.SetParent(cam.transform, false);
            over = lens.AddComponent<Camera>();
            over.cullingMask = 1 << Layer; over.clearFlags = CameraClearFlags.Depth; over.depth = cam.depth + 1f;
            over.nearClipPlane = 0.1f; over.farClipPlane = 60f; over.fieldOfView = cam.fieldOfView;
            var od = lens.AddComponent<UniversalAdditionalCameraData>();
            od.renderType = CameraRenderType.Overlay; od.renderPostProcessing = false; od.renderShadows = false;
            if (!main.cameraStack.Contains(over)) main.cameraStack.Add(over);
            cam.cullingMask &= ~(1 << Layer);
            lens.SetActive(false);
        }
        else
        {
            // no stack to join: drawn by the main camera, in the scene
            foreach (var t in chest.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
        }

        // the overlay (#crateOv): clear across the middle where the chest is, a gradient of dark
        // only at the top and the bottom where the words are; it takes the taps
        overlay = UiKit.Rect(ui, "crateOv", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var bg = overlay.gameObject.AddComponent<RawImage>(); bg.texture = ScrimTex(); bg.raycastTarget = true;
        var et = overlay.gameObject.AddComponent<EventTrigger>();
        UiKit.Add(et, EventTriggerType.PointerDown, _ => Tap());
        // h2 "Treasure chest!": min(9vw, 40px) gold, 12vh down, a 3px seat of a shadow
        var h2 = UiKit.Label(overlay, "Treasure chest!", 38.7f, Gold, FontStyle.Bold, new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.88f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(420f, 47f), TextAnchor.MiddleCenter, 0f);
        ShopArt.Shade(h2, Solid.Rgba(64, 36, 7), 0.55f, new Vector2(0f, -3f));
        // .hint: min(4.4vw, 17px) #dff1fb at .9, 7vh off the foot
        hint = UiKit.Label(overlay, "Tap it open!", 17f, Solid.Rgba(223, 241, 251, 0.9f), FontStyle.Normal, new Vector2(0.5f, 0.07f), new Vector2(0.5f, 0.07f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(420f, 22f), TextAnchor.MiddleCenter, 0f);
        overlay.gameObject.SetActive(false);
    }

    // rgba(4,15,27): .42 at the top, .22 at 14%, clear from 30% to 74%, .20 at 88%, .40 at the foot
    static Texture2D scrim, glow;
    static Texture2D ScrimTex()
    {
        if (scrim != null) return scrim;
        const int N = 256;
        scrim = new Texture2D(1, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "crate scrim" };
        float[] at = { 0f, 0.14f, 0.30f, 0.74f, 0.88f, 1f }, a = { 0.42f, 0.22f, 0f, 0f, 0.20f, 0.40f };
        for (int y = 0; y < N; y++)
        {
            float v = 1f - (y + 0.5f) / N, al = 0f;                         // 0 at the top
            for (int i = 0; i + 1 < at.Length; i++) if (v >= at[i] && v <= at[i + 1]) { al = Mathf.Lerp(a[i], a[i + 1], (v - at[i]) / (at[i + 1] - at[i])); break; }
            scrim.SetPixel(0, y, new Color(4 / 255f, 15 / 255f, 27 / 255f, al));
        }
        scrim.Apply(false, true);
        return scrim;
    }

    // the web chest's glow sprite: a radial gradient from warm gold to nothing (web 24033-24040)
    static Texture2D GlowTex()
    {
        if (glow != null) return glow;
        const int N = 128;
        glow = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "chest glow" };
        Color c0 = Solid.Rgba(255, 222, 130, 0.95f), c1 = Solid.Rgba(255, 196, 80, 0.38f), c2 = Solid.Rgba(255, 180, 60, 0f);
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float d = Mathf.Sqrt((x + 0.5f - 64f) * (x + 0.5f - 64f) + (y + 0.5f - 64f) * (y + 0.5f - 64f));
            float k = Mathf.Clamp01((d - 4f) / 58f);
            glow.SetPixel(x, y, k < 0.45f ? Color.Lerp(c0, c1, k / 0.45f) : Color.Lerp(c1, c2, (k - 0.45f) / 0.55f));
        }
        glow.Apply(false, true);
        return glow;
    }
}
