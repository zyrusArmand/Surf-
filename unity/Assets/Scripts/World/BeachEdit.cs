// Moving the title beach about by hand, on the phone as well as in the editor: hold a finger (or the
// mouse) on the shop, the signpost, the chest, the palm or the rider for two seconds and it comes
// loose with a buzz; drag it over the sand; let go and the layout is saved (Beach.SaveLayout: on the
// device, and in the editor into the project as well). A short tap still presses the signpost --
// on release now, so that holding the post to move it does not start a run.
using UnityEngine;

public class BeachEdit : MonoBehaviour
{
    public Beach beach; public Camera cam; public Hud hud;

    const float Hold = 2f, Slop = 18f, TapMax = 0.45f;
    bool down, editing, moved;
    int finger = -1;
    Vector2 start; float downT, planeY;
    Transform target, candidate; Vector3 grab; string label;

    /// moving something now: the title's other input stands down
    public bool Editing => editing;

    /// each frame the title is up; `blocked` when a pane, the intro or the quest board has the screen
    public void Step(bool blocked)
    {
        if (beach == null || cam == null) return;
        if (blocked && !editing) { down = false; return; }
        bool isDown, began, ended; Vector2 pos;
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (finger >= 0) for (int i = 0; i < Input.touchCount; i++) if (Input.GetTouch(i).fingerId == finger) t = Input.GetTouch(i);
            pos = t.position; began = t.phase == TouchPhase.Began; ended = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            isDown = !ended; if (began) finger = t.fingerId;
        }
        else
        {
            // a phone reports every touch as a mouse click as well: the mouse only when there is no touch
            pos = Input.mousePosition; began = Input.GetMouseButtonDown(0); ended = Input.GetMouseButtonUp(0); isDown = Input.GetMouseButton(0);
            if (!isDown && !ended && down && !editing) ended = true;          // a release the frame missed
        }

        if (began)
        {
            if (UiKit.OverUI(pos)) { down = false; return; }
            down = true; moved = false; start = pos; downT = Time.unscaledTime;
            candidate = beach.EditTarget(cam, pos, out label);
        }
        if (!down) return;

        if (!editing)
        {
            if ((pos - start).magnitude > Slop) moved = true;
            if (isDown && !moved && candidate != null && Time.unscaledTime - downT >= Hold)
            {
                editing = true; target = candidate;
                planeY = target.position.y;
                grab = target.position - OnPlane(pos);
                Sfx.Buzz(35);
                if (hud != null) hud.Toast("Moving " + label + " -- drag, then let go");
            }
            if (ended)
            {
                down = false; finger = -1;
                if (!moved && Time.unscaledTime - downT <= TapMax) beach.Tap(cam, start);   // a plain tap: the signpost, as before
            }
            return;
        }

        // moving: over the sand at the height it stands, under the finger
        var p = OnPlane(pos) + grab;
        target.position = new Vector3(p.x, target.position.y, p.z);
        if (ended)
        {
            editing = false; down = false; finger = -1; target = null;
            beach.SaveLayout();
            Sfx.Buzz(12);
            if (hud != null) hud.Toast("Saved");
        }
    }

    // where the finger's ray meets the level the thing stands at (or the thing itself, looking up)
    Vector3 OnPlane(Vector2 screen)
    {
        var ray = cam.ScreenPointToRay(screen);
        if (ray.direction.y > -1e-3f) return target != null ? target.position - grab : Vector3.zero;
        float t = (planeY - ray.origin.y) / ray.direction.y;
        return ray.origin + ray.direction * t;
    }
}
