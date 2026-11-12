// A number that counts up to itself: the run card's distance, tricks and puka roll from zero
// over most of a second, fast at first and settling on the value, instead of just being there.
// Unscaled time, so it runs over a paused or slowed game.
using UnityEngine;
using UnityEngine.UI;

public class CountUp : MonoBehaviour
{
    Text t; int to; string pre, post; float e, dur, wait;

    public static void On(Text t, int to, string pre = "", string post = "", float dur = 0.9f, float delay = 0f)
    {
        if (t == null) return;
        var c = t.GetComponent<CountUp>() ?? t.gameObject.AddComponent<CountUp>();
        c.t = t; c.to = to; c.pre = pre; c.post = post; c.dur = Mathf.Max(0.05f, dur); c.wait = delay; c.e = 0f; c.enabled = true;
        t.text = pre + Pane.N(0) + post;
    }

    void Update()
    {
        if (t == null) { enabled = false; return; }
        e += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01((e - wait) / dur);
        k = 1f - (1f - k) * (1f - k) * (1f - k);
        t.text = pre + Pane.N(Mathf.RoundToInt(to * k)) + post;
        if (k >= 1f) enabled = false;
    }
}
