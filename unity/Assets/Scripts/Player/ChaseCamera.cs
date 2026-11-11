// The shot: 4.9 feet over the water and eleven back, following his x, pulling back a little
// with speed and lifting with air, widening and leaning in at speed. The web game's numbers.
//
// Under the water the same geometry is measured off HIM instead of off the surface (web
// 47009-47107): a mounted shot through the launch, 4.6 up and 6.4 back aiming 2.6 below him,
// settling over his first two seconds under to 3.2 up, 6.9 back and 1.85 below, chased at 13
// then 3 a second, following all of his x and locked to it once it has caught up, and the lens
// held 1.15 feet under the sea once he is below it. The aim swings into the dive shot over the
// first four tenths of the arc rather than cutting (web 46921-46928).
//
// Ashore (web 46862-46874, 46898, 47108-47117) the chase camera has to actually chase: the
// ride's own geometry, 4.9 up and 11 back, measured off the SAND he is standing on and off HIM
// rather than the origin, all three axes eased at 3.4 a second, and the aim on him too (1.05 up
// off the sand, 14 past him). The fork's turn is applied last, to the shot (web 47123-47137):
// the eye and what it looks at are swung about the rider by forkYaw, so the camera keeps its
// distance and height and only its bearing changes. The chute tips the lens down the fall line
// (web 47188-47192): 5 up, 1.2 closer, 0.22 rad of pitch, all weighted by fallW.
using UnityEngine;

public class ChaseCamera : MonoBehaviour
{
    public BoardController rider;
    public Camera cam;
    public Dive dive;            // optional: the dive camera when he goes under
    public Island island;        // optional: the island's shot once he is ashore, and the fork's turn
    public Chute chute;          // optional: the lens tips down the fall line
    public float swCam;          // 0..1 while the set wave stands: lower, further back, aimed down the line
    public bool hold;            // the ring has the rider: the shot stops following and holds where it was
    float camX, camY = 4.9f, camZ = 11f, lookX, camSpd;
    bool held; Vector3 heldPos, heldAim;
    const float Fov = 62f, FovFast = 7.5f;

    // ---- the dive's own three numbers (web _dvCam/_dvX/_dvY/_dvZ) ----
    const float DiveCamUp = 4.6f, DiveCamBack = 6.4f, DiveAimUp = -2.6f, DiveAimAhead = 6.0f;
    const float SwimCamUp = 3.2f, SwimCamBack = 6.9f, SwimAimUp = -1.85f;
    const float DiveCamK = 13.0f, SwimCamK = 3.0f, DiveCamDip = 1.15f;
    int dvCam; float dvX, dvY, dvZ;

    // ---- the island's (web ISLE_CAM_*) and the chute's (web FALL_CAM) ----
    const float IsleCamBack = 11.0f, IsleCamUp = 4.9f, IsleAimUp = 1.05f, IsleAimAhead = 14.0f, IsleCamK = 3.4f;
    const float FallCamLift = 5.0f, FallCamBack = -1.2f, FallCamPitch = 0.22f;

    public void Snap()
    {
        camX = rider.px; lookX = rider.px; camY = rider.waterY + 4.9f; camZ = 11f; camSpd = 0; held = false; dvCam = 0;
    }

    public void Step(float dt)
    {
        var r = rider;
        if (hold)
        {
            // No barrel camera: the ride camera simply stops when the ring takes charge. Any
            // re-placement is a snap at the instant of takeover, which is what gets noticed.
            if (!held) { held = true; heldPos = transform.position; heldAim = new Vector3(lookX, r.waterY + 1.05f + swCam * 1.5f, -14f - swCam * 10f); }
            transform.position = heldPos; transform.LookAt(heldAim, Vector3.up);
            camX = heldPos.x; camY = heldPos.y; camZ = heldPos.z;
            return;
        }
        held = false;
        bool under = dive != null && dive.On;
        bool diveShot = dive != null && (dive.On || dive.Launch > Dive.LaunchLoad);   // the handover is the moment he leaves, not the crouch
        bool isle = island != null && island.Ashore;
        float airH = Mathf.Max(0f, r.py - r.waterY);
        camX += (r.px - camX) * Mathf.Min(1f, dt * 7f);
        lookX += (r.px - lookX) * Mathf.Min(1f, dt * 7f);
        if (!isle)
        {
            // ...and it stands down entirely once he is ashore: one owner of the camera at a time (web 46862)
            float wantY = r.waterY + 4.9f - swCam * 2.15f + airH * 0.62f;
            camY += (wantY - camY) * Mathf.Min(1f, dt * (airH > 0.5f ? 4f : 2.5f));
            if (!under) camY = Mathf.Max(camY, r.waterY + 2.3f);     // a rising face cannot close over the lens; wrong for a dive (web 46870)
            camZ += ((11.0f + swCam * 4.6f + r.eff * 0.05f + airH * 0.22f + r.pz * 0.42f) - camZ) * Mathf.Min(1f, dt * 2.4f);
        }
        if (diveShot)
        {
            float se = dive.Shot();
            // tight while he is going in, relaxing to the swimming lag over the two seconds the shot settles over
            float kd = Mathf.Min(1f, dt * (under ? (DiveCamK + (SwimCamK - DiveCamK) * se) : SwimCamK));
            if (dvCam == 0) { dvCam = 1; dvX = camX; dvY = camY; dvZ = camZ; }   // seeded from wherever the surf camera had got to
            float cUp = DiveCamUp + (SwimCamUp - DiveCamUp) * se, cBack = DiveCamBack + (SwimCamBack - DiveCamBack) * se;
            float lw = dive.Launch > Dive.LaunchLoad ? Mathf.Min(1f, (dive.Launch - Dive.LaunchLoad) / 0.40f) : 0f;
            float ry = r.py;                                        // the lift is inside py in this port
            dvY += ((ry + cUp) - dvY) * kd; dvZ += ((r.pz + cBack) - dvZ) * kd;
            if (lw > 0f) { dvY += ((ry + cUp) - dvY) * lw; dvZ += ((r.pz + cBack) - dvZ) * lw; }   // through the launch it is assigned, swung into over 0.4 s
            // the lens follows him under once HE is under: the sea sampled where the camera is
            float cs = WaveField.Height(dvX, WaveField.Scroll + dvZ, r.t);
            if (ry < cs) { float want = cs - DiveCamDip; if (dvY > want) dvY += (want - dvY) * Mathf.Min(1f, dt * 6.0f); }
            // all of his travel, eased for the entry alone and then locked to him
            if (dvCam == 1) { dvX += (r.px - dvX) * Mathf.Min(1f, dt * 6.0f); if (Mathf.Abs(dvX - r.px) < 0.15f) dvCam = 2; }
            else dvX = r.px;
            camX = dvX; camY = dvY; camZ = dvZ;
        }
        else dvCam = 0;
        if (isle)
        {
            // the ride's own geometry, measured off the sand and off him (web 47108-47117)
            float g = island.Ground(), k = Mathf.Min(1f, dt * IsleCamK);
            camX += (r.px - camX) * k;
            camZ += ((r.pz + IsleCamBack) - camZ) * k;
            camY += ((g + IsleCamUp) - camY) * k;
        }
        transform.position = new Vector3(camX, camY, camZ);
        var aim = new Vector3(lookX, r.waterY + 1.05f + swCam * 1.5f + airH * 0.62f, -14f - swCam * 10f);
        if (isle) { aim.y = island.Ground() + IsleAimUp; aim.z = r.pz - IsleAimAhead; }   // at HIM, not at a point fourteen feet in front of the origin (web 46898)
        if (diveShot)
        {
            // the aim swings round onto him on the same clock as the position, and is simply the aim once he is under
            float se = dive.Shot();
            float aw = under ? 1f : Mathf.Clamp01((dive.Launch - Dive.LaunchLoad) / 0.40f);
            float ae = aw * aw * (3f - 2f * aw);
            aim.x += (r.px - aim.x) * ae;
            aim.y += ((r.py + (DiveAimUp + (SwimAimUp - DiveAimUp) * se)) - aim.y) * ae;
            aim.z += ((r.pz - DiveAimAhead) - aim.z) * ae;
        }
        // the fork's turn, applied to the SHOT: eye and aim swung about the rider (web 47130-47137)
        float yaw = island != null ? island.Yaw : 0f;
        if (Mathf.Abs(yaw) > 0.001f)
        {
            float c = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);
            var p = transform.position;
            float dx = p.x - r.px, dz = p.z - r.pz;
            transform.position = new Vector3(r.px + dx * c - dz * sn, p.y, r.pz + dx * sn + dz * c);
            dx = aim.x - r.px; dz = aim.z - r.pz;
            aim = new Vector3(r.px + dx * c - dz * sn, aim.y, r.pz + dx * sn + dz * c);
        }
        transform.LookAt(aim, Vector3.up);
        // speed on the lens; the lens stays the lens under the water and on the island, which have their own cameras (web 47154)
        bool heldLens = under || isle;
        float s = Mathf.Clamp01((r.eff - 16.2f) / (52f - 16.2f));
        camSpd = Mathf.Lerp(camSpd, heldLens ? camSpd : s, Mathf.Min(1f, dt * 2f));
        cam.fieldOfView = Fov + FovFast * camSpd * camSpd;
        // no roll: the horizon stays level and only the rider leans into a carve
        // and the shot tips down the fall line, last, on the camera rather than the world (web 47172-47192)
        float fw = chute != null ? chute.W : 0f;
        if (fw > 0.001f)
        {
            transform.position += new Vector3(0f, FallCamLift * fw, FallCamBack * fw);
            transform.Rotate(FallCamPitch * fw * Mathf.Rad2Deg, 0f, 0f, Space.Self);
        }
    }

    public float Speed => camSpd;
}
