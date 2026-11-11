// The HUD and the controls, built in code so there is no prefab to wire: the four figures
// across the top, the trick buttons down the left, JUMP bottom right, a steering strip, and
// the run card. Touch and keyboard both work.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Hud : MonoBehaviour
{
    public BoardController rider;
    public System.Action onPlay;

    Canvas canvas; Font font;
    Text tScore, tDist, tMph, tCoins, tMsg, tCardTitle, tCardBody;
    GameObject card; GameObject bar;
    int steerPointer = -1; float steerX0; float steerVal;

    public void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var cgo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(430, 932); sc.matchWidthOrHeight = 0.5f;
        if (FindObjectOfType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // top row
        var top = Panel(cgo.transform, "top", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -60), new Vector2(0, 0), new Color(0.01f, 0.08f, 0.16f, 0.25f));
        tScore = Cell(top.transform, "TRICKS", 0.125f); tDist = Cell(top.transform, "DIST", 0.375f);
        tMph = Cell(top.transform, "MPH", 0.625f); tCoins = Cell(top.transform, "PUKA", 0.875f);
        // the trick message
        tMsg = Label(cgo.transform, "", 30, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(400, 50));
        tMsg.color = new Color(1f, 0.87f, 0.18f); tMsg.fontStyle = FontStyle.Bold;
        // trick bar: three held buttons down the left
        bar = new GameObject("bar"); bar.transform.SetParent(cgo.transform, false);
        var brt = bar.AddComponent<RectTransform>(); brt.anchorMin = new Vector2(0, 0.5f); brt.anchorMax = new Vector2(0, 0.5f);
        brt.pivot = new Vector2(0, 0.5f); brt.anchoredPosition = new Vector2(0, -80); brt.sizeDelta = new Vector2(76, 200);
        HoldButton(bar.transform, "ROLL", 0, "roll"); HoldButton(bar.transform, "FLIP", 1, "flip"); HoldButton(bar.transform, "SPIN", 2, "spin");
        // jump
        var jump = Button(cgo.transform, "JUMP", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 60), new Vector2(110, 70));
        var jt = jump.AddComponent<EventTrigger>();
        Add(jt, EventTriggerType.PointerDown, _ => rider.Jump());
        // the card
        card = Panel(cgo.transform, "card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 300), new Color(0.03f, 0.13f, 0.24f, 0.94f));
        tCardTitle = Label(card.transform, "Wipeout!", 40, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(320, 60));
        tCardBody = Label(card.transform, "", 22, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(320, 120));
        var again = Button(card.transform, "AGAIN", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(200, 56));
        again.GetComponent<Button>().onClick.AddListener(() => onPlay?.Invoke());
        card.SetActive(false);
    }

    GameObject Panel(Transform p, string n, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform), typeof(Image)); go.transform.SetParent(p, false);
        var rt = go.GetComponent<RectTransform>(); rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = size;
        if (aMin.y == 1 && aMax.y == 1 && aMin.x == 0 && aMax.x == 1) { rt.pivot = new Vector2(0.5f, 1); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(0, 110); }
        go.GetComponent<Image>().color = c; return go;
    }
    Text Cell(Transform p, string lab, float ax)
    {
        Label(p, lab, 13, new Vector2(ax, 1), new Vector2(ax, 1), new Vector2(0, -28), new Vector2(100, 20)).color = new Color(1, 1, 1, 0.7f);
        return Label(p, "0", 30, new Vector2(ax, 1), new Vector2(ax, 1), new Vector2(0, -66), new Vector2(110, 40));
    }
    Text Label(Transform p, string s, int size, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject("label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(p, false);
        var rt = go.GetComponent<RectTransform>(); rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = sz;
        var t = go.GetComponent<Text>(); t.font = font; t.fontSize = size; t.text = s; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; return t;
    }
    GameObject Button(Transform p, string s, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject(s, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(p, false);
        var rt = go.GetComponent<RectTransform>(); rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(aMin.x, aMin.y); rt.anchoredPosition = pos; rt.sizeDelta = sz;
        go.GetComponent<Image>().color = new Color(0.16f, 0.45f, 0.70f, 0.85f);
        Label(go.transform, s, 20, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return go;
    }
    void HoldButton(Transform p, string s, int i, string trick)
    {
        var go = Button(p, s, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 66), new Vector2(72, 58));
        go.GetComponent<RectTransform>().pivot = new Vector2(0, 1);
        var et = go.AddComponent<EventTrigger>();
        Add(et, EventTriggerType.PointerDown, _ => rider.Hold(trick, true));
        Add(et, EventTriggerType.PointerUp, _ => rider.Hold(trick, false));
        Add(et, EventTriggerType.PointerExit, _ => rider.Hold(trick, false));
    }
    static void Add(EventTrigger et, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> cb)
    {
        var e = new EventTrigger.Entry { eventID = type }; e.callback.AddListener(cb); et.triggers.Add(e);
    }

    public void ShowCard(bool on, int dist, int score, int best)
    {
        card.SetActive(on); bar.SetActive(!on);
        if (on) tCardBody.text = dist + " m\n" + score + " trick points\nbest " + best;
    }

    void Update()
    {
        if (rider == null) return;
        tScore.text = rider.score.ToString("N0");
        tDist.text = Mathf.RoundToInt(rider.dist) + " m";
        tMph.text = ((int)(rider.eff * 0.62f)).ToString();
        tCoins.text = rider.coins.ToString("N0");
        tMsg.text = rider.msgT > 0 ? rider.lastMsg : "";

        // steering: keys, or a finger dragged on the right half of the screen below the HUD
        float k = (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? -1 : 0) + (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0);
        float touch = 0f;
        for (int i = 0; i < Input.touchCount; i++)
        {
            var tc = Input.GetTouch(i);
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(tc.fingerId);
            if (tc.phase == TouchPhase.Began && !overUi && steerPointer < 0) { steerPointer = tc.fingerId; steerX0 = tc.position.x; }
            if (tc.fingerId == steerPointer)
            {
                if (tc.phase == TouchPhase.Ended || tc.phase == TouchPhase.Canceled) { steerPointer = -1; steerVal = 0; }
                else steerVal = Mathf.Clamp((tc.position.x - steerX0) / (Screen.width * 0.18f), -1f, 1f);
            }
        }
        if (Input.touchCount == 0) { steerPointer = -1; steerVal = 0; }
        touch = steerVal;
        // mouse drag for the editor
        if (Input.touchCount == 0 && Input.GetMouseButton(0) && !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            if (steerPointer < 0) { steerPointer = 99; steerX0 = Input.mousePosition.x; }
            touch = Mathf.Clamp((Input.mousePosition.x - steerX0) / (Screen.width * 0.18f), -1f, 1f);
        }
        else if (Input.touchCount == 0 && steerPointer == 99) { steerPointer = -1; touch = 0; }
        rider.steer = Mathf.Clamp(k + touch, -1f, 1f);
        if (Input.GetKeyDown(KeyCode.Space)) rider.Jump();
        if (Input.GetKeyDown(KeyCode.J)) rider.Hold("spin", true); if (Input.GetKeyUp(KeyCode.J)) rider.Hold("spin", false);
        if (Input.GetKeyDown(KeyCode.K)) rider.Hold("flip", true); if (Input.GetKeyUp(KeyCode.K)) rider.Hold("flip", false);
        if (Input.GetKeyDown(KeyCode.L)) rider.Hold("roll", true); if (Input.GetKeyUp(KeyCode.L)) rider.Hold("roll", false);
    }
}
