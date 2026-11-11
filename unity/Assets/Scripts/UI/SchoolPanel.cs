// The trick sheet: every trick, the finger movement that does it, and the gesture picture
// from the buttons down the left of the screen, read sitting down. The words are the web
// game's HOWTO, one line each, with its closing note on landing square.
using UnityEngine;
using UnityEngine.UI;

public class SchoolPanel : MonoBehaviour
{
    // name, how, the gesture icon under Resources/UI (null for the note at the end)
    static readonly string[][] HowTo = {
        new[] { "Jump", "Tap anywhere on the water. Off a ramp or the lip it is a launch; flat it is a hop.", "ges_jump" },
        new[] { "Barrel roll", "Swipe up or down in the air. Hold the swipe to keep rolling.", "ges_roll" },
        new[] { "Flip", "Swipe left or right. Front flip one way, back flip the other.", "ges_flip" },
        new[] { "Spin", "Draw a circle with one finger. Half a turn lands you switch, and that counts.", "ges_spin" },
        new[] { "Handstand", "Hold a second finger down. It pays while it is held, and carving pays more — but your arms give out if you stay up there.", "ges_hand" },
        new[] { "Helicopter", "Grab the nose and roll the board three times over. The whole move is one press.", "ges_heli" },
        new[] { "Land it square", "However far you turned, the board has to be under you when you touch down. Yaw only has to finish on a half turn; a flip has to come all the way round.", null },
    };

    public System.Action onClose;
    GameObject pane; RectTransform content; ScrollRect scroll;
    public bool Open => pane != null && pane.activeSelf;

    public void Build(Transform canvas)
    {
        pane = Pane.Build(canvas, "school", "Tricks", () => { Show(false); onClose?.Invoke(); }, out content, out scroll);
    }

    public void Show(bool on) { pane.SetActive(on); if (on) Fill(); }

    void Fill()
    {
        Pane.Clear(content);
        Pane.Note(content, "Every one of these is a finger movement. The buttons down the left of the screen do the same thing, and carry the same picture, so one teaches the other.", 54);
        foreach (var t in HowTo)
        {
            float h = t[1].Length > 90 ? 104f : 84f;
            var c = Pane.Card(content, t[0], h);
            var sp = t[2] != null ? UiKit.Icon(t[2]) : null;
            if (sp != null)
            {
                var im = UiKit.Pic(c, "ges", sp, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(34, 34));
                im.raycastTarget = false; im.preserveAspect = true;
            }
            else UiKit.Label(c, "▼", 22, UiKit.GoldText, FontStyle.Bold, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(34, 34));
            UiKit.Label(c, t[0], 15, Color.white, FontStyle.Bold, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(64, -10), new Vector2(-80, 22), TextAnchor.MiddleLeft);
            var how = UiKit.Label(c, t[1], 12.5f, Pane.Dim, FontStyle.Normal, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(64, -34), new Vector2(-80, h - 40), TextAnchor.UpperLeft, 0.5f);
            how.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
        scroll.verticalNormalizedPosition = 1f;
    }
}
