// What a board and a rider add up to on the water: the web's applyBoardStats and RIDER_PERK,
// number for number. The four bars set speed, turn, air and lives; grip, glide and float come
// off the spec sheet itself (length, width, thickness, tail width, rocker); a motor's top
// speed, the wake and a foil's lift are the sheet's own numbers; and the rider's one small
// perk rides on top. The pug is the honest baseline.
using UnityEngine;

public class RideStats
{
    public float speed = 1f, turn = 1f, air = 1f, grip = 1f, glide = 1f, flo = 1f, top, wake = 1f, lift, trick = 1f, luck = 1f;
    public int lives = 2;

    static float Soft(float x, float k) => k * (float)System.Math.Tanh(x / k);

    public static RideStats For(string boardId, string riderId)
    {
        var b = BoardCatalog.Get(boardId);
        float L = b.L, W = b.W, T = b.T, tw = b.tailW, rock = b.rockN + b.rockT;
        var s = new RideStats
        {
            speed = 1 + (b.speed - 5) * 0.040f,
            turn = 1 + (b.turn - 5) * 0.060f,
            air = 1 + (b.air - 5) * 0.080f,
            lives = Mathf.Max(2, Mathf.Min(4, Mathf.RoundToInt(2 + (b.tough - 2) / 4f))),
            grip = 1 + Soft((L / (2 * W) - 3.90f) * 0.16f + (L - 6.30f) * 0.05f - (tw - 0.45f) * 0.55f, 0.30f),
            glide = 1 + Soft((L - 6.30f) * 0.16f + (T - 0.193f) * 1.55f - (rock - 0.53f) * 0.95f, 0.45f),
            flo = 1 + Soft((T - 0.193f) * 1.80f + (W - 0.830f) * 0.50f, 0.32f),
            top = b.top, wake = b.wake, lift = b.lift,
        };
        // the rider's perk rides on top of the board
        switch (riderId)
        {
            case "cat": s.trick = 1.10f; break;
            case "pig": s.flo *= 1.12f; break;
            case "otter": s.speed *= 1.06f; break;
            case "penguin": s.grip *= 1.12f; break;
            case "frog": s.air *= 1.12f; break;
            case "monkey": s.air *= 1.10f; break;
            case "rat": s.flo *= 1.10f; break;
            case "panda": s.lives = Mathf.Min(5, s.lives + 1); break;
            case "bunny": s.air *= 1.12f; break;
            case "chicken": s.luck = 1.25f; break;
            case "corgi": s.speed *= 1.05f; break;
            case "raccoon": s.luck = 1.35f; break;
            case "squirrel": s.trick = 1.08f; break;
            case "sloth": s.grip *= 1.15f; break;
            case "lizard": s.grip *= 1.10f; break;
            case "alien": s.trick = 1.12f; break;
            case "angler": s.luck = 1.20f; break;
            case "ant": s.flo *= 1.08f; break;
            case "porcupine": s.lives = Mathf.Min(5, s.lives + 1); break;
            case "rex": s.air *= 1.08f; break;
        }
        return s;
    }
}
