// The puka counter's "+": the web's bundles, with their real arithmetic and no way to take
// money. The game has no payment provider, so every button here is disabled and the sheet says
// why, rather than pretending to charge and quietly handing out currency.
using UnityEngine;
using UnityEngine.UI;

public class PukaPanel : MonoBehaviour
{
    public System.Action onClose;
    GameObject pane; RectTransform content; ScrollRect scroll;
    public bool Open => pane != null && pane.activeSelf;

    // the web's PUKA_PACKS: shells, price, the bonus flash on the bigger bundles
    static readonly (int n, string p, string tag)[] Packs = { (1200, "$0.99", ""), (6500, "$4.99", "+8%"), (14000, "$9.99", "+17%"), (75000, "$39.99", "+56%") };

    public void Build(Transform canvas)
    {
        pane = Pane.Build(canvas, "puka", "Get puka", () => { Show(false); onClose?.Invoke(); }, out content, out scroll);
    }

    // the tap is answered by the button that opened it (the bar's level pill or its +), once
    public void Show(bool on) { pane.SetActive(on); if (on) Fill(); }

    void Fill()
    {
        Pane.Clear(content);
        Pane.Row(content, "You have", Pane.N(SaveData.Coins));
        foreach (var k in Packs)
        {
            var c = Pane.Card(content, "pack:" + k.n, 44);
            UiKit.Label(c, Pane.N(k.n) + " puka", 14, Pane.Dim, FontStyle.Normal, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(-32, 0), TextAnchor.MiddleLeft, 0.5f);
            if (k.tag != "")
            {
                float w = (Pane.N(k.n) + " puka").Length * 7.6f;      // the flash sits just after the count
                UiKit.Label(c, k.tag, 11.5f, Solid.Rgba(143, 240, 165), FontStyle.Bold, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16 + w + 6, 0), new Vector2(60, 16), TextAnchor.MiddleLeft, 0f);
            }
            // the price, greyed: nothing here is wired to a store
            var btn = Pane.Button(c, k.p, 84, false, null);
            btn.color = new Color(1, 1, 1, 0.45f);
            var t = btn.GetComponentInChildren<Text>(); if (t != null) t.color = new Color(1, 1, 1, 0.45f);
        }
        Pane.Note(content, "These are not connected to a store yet, so nothing here can be bought. Puka is earned by surfing in the meantime — distance, tricks and goals all pay.", 56);
        scroll.verticalNormalizedPosition = 1f;
    }
}
