// CSS letter-spacing for a one-line legacy Text: every glyph is pushed along by its index times
// the spacing, and the line is pulled back so it stays aligned the way the Text is. The web sets
// a good deal of its small capitals tracked out (the run card's DISTANCE at .12em, WATCH AD at
// .14em, the viewer's SPEED at .1em), and set solid they read cramped next to it.
//
// It runs after the label's Shadow (UiKit.Label adds that first, and modifiers run in component
// order), so the stream it sees is the shadow's copies followed by the glyphs themselves, six
// vertices a glyph: each copy is moved the same way. One line only -- a wrapped Text would be
// spaced as if it were one long line.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class Tracking : BaseMeshEffect
{
    public float em;                       // the spacing, in ems of the Text's own size
    static readonly List<UIVertex> buf = new List<UIVertex>();

    /// letter-spacing: `em` on t (add after any Shade, so the shadow is counted)
    public static Tracking On(Text t, float em)
    {
        if (t == null) return null;
        var k = t.GetComponent<Tracking>() ?? t.gameObject.AddComponent<Tracking>();
        k.em = em;
        t.SetVerticesDirty();
        return k;
    }

    /// the width the spacing adds to `s` at size `px` -- for laying out next to a tracked label
    public static float Extra(string s, float px, float em) => string.IsNullOrEmpty(s) ? 0f : s.Length * Mathf.RoundToInt(px) * em;

    static readonly List<int> at = new List<int>();

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || em == 0f) return;
        var text = GetComponent<Text>(); if (text == null) return;
        buf.Clear(); vh.GetUIVertexStream(buf);
        int quads = buf.Count / 6; if (quads == 0) return;
        int copies = 1;
        foreach (var s in GetComponents<Shadow>()) if (s.enabled) copies += s is Outline ? 4 : 1;
        int per = Mathf.Max(1, quads / copies);
        // which character each quad drew: every one, or (as newer Unity builds it) every one but
        // the spaces and the rich-text tags -- the spacing follows the character, so a word after
        // a space still moves on by the space's share
        string str = text.text ?? "";
        at.Clear(); int len = 0; bool tag = false;
        for (int i = 0; i < str.Length; i++)
        {
            char ch = str[i];
            if (text.supportRichText && ch == '<') { int close = str.IndexOf('>', i); if (close > i) { tag = true; } }
            if (tag) { if (ch == '>') tag = false; continue; }
            if (!char.IsWhiteSpace(ch)) at.Add(len);
            len++;
        }
        bool mapped = at.Count == per;
        float sp = em * text.fontSize, total = sp * Mathf.Max(len, per);
        var a = text.alignment;
        bool centre = a == TextAnchor.UpperCenter || a == TextAnchor.MiddleCenter || a == TextAnchor.LowerCenter;
        bool right = a == TextAnchor.UpperRight || a == TextAnchor.MiddleRight || a == TextAnchor.LowerRight;
        float shift = centre ? -total * 0.5f : right ? -total : 0f;
        for (int q = 0; q < quads; q++)
        {
            int g = q % per;
            float dx = (mapped ? at[g] : g) * sp + shift;
            for (int k = 0; k < 6; k++)
            {
                var v = buf[q * 6 + k]; v.position.x += dx; buf[q * 6 + k] = v;
            }
        }
        vh.Clear(); vh.AddUIVertexTriangleStream(buf);
    }
}
