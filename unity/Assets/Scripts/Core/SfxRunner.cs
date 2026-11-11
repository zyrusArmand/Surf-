// The one component behind Sfx: it lives on the hidden Sfx object and gives the static class a
// frame to ramp its loops, run the keep-alive and finish its fades. Sfx.Init() adds it; nothing
// else should.
using UnityEngine;

public class SfxRunner : MonoBehaviour
{
    void Update() { Sfx.Tick(Time.unscaledDeltaTime); }
}
