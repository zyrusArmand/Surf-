// The clips a .glb brought with it, played without an AnimatorController. glTFast's editor
// importer stores each animation as a sub-asset of the file (Resources.LoadAll finds them) and
// puts either a legacy Animation component or a bare Mecanim Animator on the prefab's root,
// depending on the import setting. This project imports with the Mecanim method, so the clips
// are non-legacy and there is no controller to play them through; a PlayableGraph with a
// mixer does that directly. The legacy path is kept so a file imported the other way plays
// too. Several clips can be held at once and blended by weight, which is what the gulls use
// to go from a flap to a glide and back.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class GltfClips
{
    static readonly Dictionary<string, AnimationClip[]> cache = new Dictionary<string, AnimationClip[]>();

    /// every clip in Resources/Models/<model>.glb
    public static AnimationClip[] Clips(string model)
    {
        if (string.IsNullOrEmpty(model)) return new AnimationClip[0];
        if (!cache.TryGetValue(model, out var clips))
        {
            clips = Resources.LoadAll<AnimationClip>("Models/" + model);
            if (clips == null) clips = new AnimationClip[0];
            cache[model] = clips;
        }
        return clips;
    }

    public static AnimationClip Find(string model, string clipName)
    {
        foreach (var c in Clips(model)) if (c != null && c.name == clipName) return c;
        return null;
    }

    public Transform root;
    AnimationClip[] clips = new AnimationClip[0];
    Animation legacy; Animator animator;
    PlayableGraph graph; AnimationMixerPlayable mixer; AnimationClipPlayable[] plays;
    bool useGraph;

    public bool Valid => root != null && ((legacy != null) || (useGraph && graph.IsValid()));
    public int Count => clips.Length;

    /// Start the named clips (those the file has) on the object spawned from Resources/Models/<model>;
    /// the first one plays at full weight, the rest at zero. Null when the file has none of them.
    public static GltfClips Start(Transform root, string model, string[] clipNames)
    {
        if (root == null || clipNames == null) return null;
        var found = new List<AnimationClip>();
        foreach (var n in clipNames) { var c = Find(model, n); if (c != null && !found.Contains(c)) found.Add(c); }
        if (found.Count == 0) return null;
        var g = new GltfClips { root = root, clips = found.ToArray() };
        if (g.clips[0].legacy)
        {
            var anim = root.GetComponentInChildren<Animation>(true);
            if (anim == null) anim = root.gameObject.AddComponent<Animation>();
            anim.enabled = true; anim.playAutomatically = false;
            foreach (var c in g.clips)
            {
                if (anim.GetClip(c.name) == null) anim.AddClip(c, c.name);
                var st = anim[c.name];
                if (st != null) { st.wrapMode = WrapMode.Loop; st.speed = 1f; st.weight = 0f; st.enabled = false; }
            }
            g.legacy = anim;
        }
        else
        {
            var an = root.GetComponentInChildren<Animator>(true);
            if (an == null) an = root.gameObject.AddComponent<Animator>();
            an.enabled = true; an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            g.animator = an;
            g.graph = PlayableGraph.Create("clips:" + root.name);
            g.graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var output = AnimationPlayableOutput.Create(g.graph, "clips", an);
            g.mixer = AnimationMixerPlayable.Create(g.graph, g.clips.Length);
            g.plays = new AnimationClipPlayable[g.clips.Length];
            for (int i = 0; i < g.clips.Length; i++)
            {
                g.plays[i] = AnimationClipPlayable.Create(g.graph, g.clips[i]);
                g.graph.Connect(g.plays[i], 0, g.mixer, i);
                g.mixer.SetInputWeight(i, 0f);
            }
            output.SetSourcePlayable(g.mixer);
            g.graph.Play();
            g.useGraph = true;
        }
        g.SetWeight(0, 1f);
        return g;
    }

    public float Length(int i) => (i >= 0 && i < clips.Length && clips[i] != null) ? clips[i].length : 1f;

    public void SetWeight(int i, float w)
    {
        if (i < 0 || i >= clips.Length) return;
        if (legacy != null) { var st = legacy[clips[i].name]; if (st != null) { st.weight = w; st.enabled = w > 0.001f; } }
        else if (useGraph && graph.IsValid()) mixer.SetInputWeight(i, w);
    }

    public void SetSpeed(int i, float s)
    {
        if (i < 0 || i >= clips.Length) return;
        if (legacy != null) { var st = legacy[clips[i].name]; if (st != null) st.speed = s; }
        else if (useGraph && graph.IsValid()) plays[i].SetSpeed(s);
    }

    public void SetTime(int i, float t)
    {
        if (i < 0 || i >= clips.Length) return;
        if (legacy != null) { var st = legacy[clips[i].name]; if (st != null) st.time = t; }
        else if (useGraph && graph.IsValid()) plays[i].SetTime(t);
    }

    /// hold the pose where it is (and start again)
    public void Pause(bool on)
    {
        if (legacy != null) legacy.enabled = !on;
        else if (useGraph && graph.IsValid()) { if (on) graph.Stop(); else graph.Play(); }
    }

    /// stop, and leave the components the way the rider code expects them: switched off
    public void Stop()
    {
        if (legacy != null) { legacy.Stop(); legacy.enabled = false; }
        legacy = null;
        if (useGraph && graph.IsValid()) graph.Destroy();
        useGraph = false;
        if (animator != null) animator.enabled = false;
        animator = null; root = null;
    }
}
