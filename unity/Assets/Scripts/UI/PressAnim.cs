using UnityEngine;

/// the press: eased down to 0.9 and sprung back with a little overshoot, not snapped
public class PressAnim : MonoBehaviour
{
    float s = 1f, v; bool down;
    public void Press(bool on) { down = on; enabled = true; }
    void Update()
    {
        float want = down ? 0.9f : 1f, dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
        v += ((want - s) * 900f - v * 34f) * dt; s += v * dt;     // a stiff, slightly underdamped spring
        transform.localScale = new Vector3(s, s, 1f);
        if (!down && Mathf.Abs(s - 1f) < 0.0015f && Mathf.Abs(v) < 0.01f) { s = 1f; v = 0f; transform.localScale = Vector3.one; enabled = false; }
    }
}

