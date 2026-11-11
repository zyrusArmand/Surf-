// The shot: 4.9 feet over the water and eleven back, following his x, pulling back a little
// with speed and lifting with air, widening and leaning in at speed. The web game's numbers.
using UnityEngine;

public class ChaseCamera : MonoBehaviour
{
    public BoardController rider;
    public Camera cam;
    public float swCam;          // 0..1 while the set wave stands: lower, further back, aimed down the line
    float camX, camY = 4.9f, camZ = 11f, lookX, camSpd, camRoll;
    const float Fov = 62f, FovFast = 7.5f;

    public void Snap()
    {
        camX = rider.px; lookX = rider.px; camY = rider.waterY + 4.9f; camZ = 11f; camSpd = 0; camRoll = 0;
    }

    public void Step(float dt)
    {
        var r = rider;
        float airH = Mathf.Max(0f, r.py - r.waterY);
        camX += (r.px - camX) * Mathf.Min(1f, dt * 7f);
        lookX += (r.px - lookX) * Mathf.Min(1f, dt * 7f);
        float wantY = r.waterY + 4.9f - swCam * 2.15f + airH * 0.62f;
        camY += (wantY - camY) * Mathf.Min(1f, dt * (airH > 0.5f ? 4f : 2.5f));
        camY = Mathf.Max(camY, r.waterY + 2.3f);
        camZ += ((11.0f + swCam * 4.6f + r.eff * 0.05f + airH * 0.22f + r.pz * 0.42f) - camZ) * Mathf.Min(1f, dt * 2.4f);
        transform.position = new Vector3(camX, camY, camZ);
        var aim = new Vector3(lookX, r.waterY + 1.05f + swCam * 1.5f + airH * 0.62f, -14f - swCam * 10f);
        transform.LookAt(aim, Vector3.up);
        // speed on the lens
        float s = Mathf.Clamp01((r.eff - 16.2f) / (52f - 16.2f));
        camSpd = Mathf.Lerp(camSpd, s, Mathf.Min(1f, dt * 2f));
        cam.fieldOfView = Fov + FovFast * camSpd * camSpd;
        // no roll: the horizon stays level and only the rider leans into a carve
        camRoll = 0f;
    }

    public float Speed => camSpd;
}
