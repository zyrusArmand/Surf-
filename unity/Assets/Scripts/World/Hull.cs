// The board's hull takes damage before it breaks, as the web's does: dropping onto the top
// of something cracks it and kicks you back up, and only the last crack snaps it. A board's
// lives come off its toughness (two to four), a rider's perk can add one. Driving into the
// side of a thing is still the end of the run. Kept here, off the controller, so the spawner
// can call it and the run card can read it.
using UnityEngine;

public static class Hull
{
    public static int Cracks, Lives = 2;

    public static void Reset()
    {
        Cracks = 0;
        Lives = RideStats.For(string.IsNullOrEmpty(SaveData.S.wheelLoan) ? SaveData.S.equipped : SaveData.S.wheelLoan,
                              string.IsNullOrEmpty(SaveData.S.wheelRider) ? SaveData.S.rider : SaveData.S.wheelRider).lives;
    }

    /// came down on top of it: a crack, the momentum gone, and a kick up off the thing
    public static void Hit(BoardController r, float topY)
    {
        if (r.wipe || !r.running) return;
        Cracks++;
        r.boost = 0f;
        r.vy = Mathf.Max(6.4f, -r.vy * 0.35f + 5.2f);
        r.airborne = true;
        r.py = Mathf.Max(r.py, topY + 0.28f);
        r.poseLand = 0.8f;
        Sfx.Land(1.4f); Sfx.Buzz(40);
        if (Cracks >= Lives) { r.Wipeout("snap"); r.Say("SNAPPED!"); return; }
        r.Say("CRACKED! " + (Lives - Cracks) + " LEFT");
    }
}
