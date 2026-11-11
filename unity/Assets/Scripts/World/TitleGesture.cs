// The title screen's loiter, as the web does it (web 20556-20640): he stands in his own stance,
// and every six to twelve seconds his file's 'Stand_and_Chat' plays over it, faded in over 0.55 s
// and out over 0.95 s starting before the clip ends, and ONLY on the spine, neck and head. The
// clip was drawn for a person; played on the whole of a pug it folds his forearms into his ribs,
// while on the back and head alone it reads as a dog shifting his weight and nodding at you.
// Everything else stays exactly where the stance put it, so his paws stay planted.
//
// The stance is posed in Update (RiderRig.Idle, which eases from wherever the bones are). This
// samples the clip in LateUpdate over that, and restores the stance at the very start of the
// next frame, so the stance's own ease never feeds on the gesture.
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class TitleGesture : MonoBehaviour
{
    const float GapLo = 6.5f, GapHi = 12.0f, FadeIn = 0.55f, FadeOut = 0.95f, First = 3.0f;
    static readonly HashSet<string> Only = new HashSet<string> { "Spine", "Spine01", "Spine02", "neck", "Head", "head_end", "headfront" };

    AnimationClip clip;
    Transform[] all; Vector3[] pos; Quaternion[] rot; Vector3[] scl;
    Transform[] masked; Quaternion[] stance;
    float wait = First, e; bool playing, restore; GameObject sampleRoot;

    public static TitleGesture Attach(Transform rider, AnimationClip clip)
    {
        if (rider == null || clip == null) return null;
        var g = rider.GetComponent<TitleGesture>(); if (g == null) g = rider.gameObject.AddComponent<TitleGesture>();
        g.Init(clip);
        return g;
    }

    void Init(AnimationClip c)
    {
        clip = c; wait = First; playing = false; restore = false; e = 0f;
        var an = GetComponentInChildren<Animator>(true); if (an != null) an.enabled = false;   // the clip is sampled by hand, nothing else drives him
        sampleRoot = an != null ? an.gameObject : gameObject;                                  // the clip's paths start where the importer put the animator
        all = GetComponentsInChildren<Transform>(true);
        pos = new Vector3[all.Length]; rot = new Quaternion[all.Length]; scl = new Vector3[all.Length];
        var m = new List<Transform>();
        foreach (var t in all) if (Only.Contains(t.name)) m.Add(t);
        masked = m.ToArray(); stance = new Quaternion[masked.Length];
    }

    void OnDisable() { Restore(); }

    // the stance back, before anything else poses him this frame
    void Update() { Restore(); }

    void Restore()
    {
        if (!restore || masked == null) return;
        for (int i = 0; i < masked.Length; i++) if (masked[i] != null) masked[i].localRotation = stance[i];
        restore = false;
    }

    void LateUpdate()
    {
        if (clip == null || all == null) return;
        float dt = Time.deltaTime;
        if (!playing)
        {
            wait -= dt;
            if (wait > 0f) return;
            playing = true; e = 0f;
        }
        e += dt;
        float len = Mathf.Max(0.1f, clip.length);
        if (e >= len) { playing = false; wait = Random.Range(GapLo, GapHi); return; }
        float a = Smooth(Mathf.Clamp01(e / FadeIn)) * Smooth(Mathf.Clamp01((len - e) / FadeOut));
        // what the stance left: all of it, so the sample can be taken back off everything it should not touch
        for (int i = 0; i < all.Length; i++) { var t = all[i]; pos[i] = t.localPosition; rot[i] = t.localRotation; scl[i] = t.localScale; }
        for (int i = 0; i < masked.Length; i++) stance[i] = masked[i].localRotation;
        clip.SampleAnimation(sampleRoot, e);
        var got = new Quaternion[masked.Length];
        for (int i = 0; i < masked.Length; i++) got[i] = masked[i].localRotation;
        for (int i = 0; i < all.Length; i++) { var t = all[i]; t.localPosition = pos[i]; t.localRotation = rot[i]; t.localScale = scl[i]; }
        for (int i = 0; i < masked.Length; i++) masked[i].localRotation = Quaternion.Slerp(stance[i], got[i], a);
        restore = true;
    }

    static float Smooth(float x) => x * x * (3f - 2f * x);
}
