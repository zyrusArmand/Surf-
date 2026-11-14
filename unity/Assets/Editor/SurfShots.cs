// Surf > Take screenshots: one click, and the editor plays the game by itself and saves the title
// and a ride at sunrise, noon and sunset into unity/Shots~ (a folder Unity ignores and git keeps),
// then stops. Commit them as usual and they can be looked at without anyone sending pictures.
// The shots are the Game view's size, so set it to a phone shape (e.g. 1170x2532) first.
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SurfShots
{
    const string Pending = "SurfShots.pending", Dir = "Shots~";
    static readonly (string name, float hour)[] Hours = { ("sunrise", 0.14f), ("noon", 0.5f), ("sunset", 0.88f) };
    static double t0; static int step;

    // entering play mode reloads the scripts, so the request is carried across in SessionState
    static SurfShots()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            {
                SessionState.SetBool(Pending, false);
                t0 = EditorApplication.timeSinceStartup; step = 0;
                EditorApplication.update += Tick;
            }
        };
    }

    [MenuItem("Surf/Take screenshots")]
    static void Take()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Surf shots: stop the game first, then take them."); return; }
        Directory.CreateDirectory(Dir);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        double t = EditorApplication.timeSinceStartup - t0;
        var surf = GameObject.Find("Surf");
        // 0: the world has built and the title is up; 1: a run started; then one shot per hour
        if (step == 0 && t > 10) { Shot("title"); step = 1; }
        else if (step == 1 && t > 12) { if (surf != null) surf.SendMessage("ShotPlay", SendMessageOptions.DontRequireReceiver); step = 2; }
        else if (step >= 2 && step < 2 + Hours.Length * 2)
        {
            int k = (step - 2) / 2; bool shoot = (step - 2) % 2 == 1;
            double at = 13 + k * 8 + (shoot ? 6 : 0);              // pin the hour, let it settle six seconds, shoot
            if (t > at)
            {
                // the start again too (it does nothing once a run is going), in case the world was slow to build
                if (shoot) Shot(Hours[k].name);
                else if (surf != null) { surf.SendMessage("ShotPlay", SendMessageOptions.DontRequireReceiver); surf.SendMessage("ShotDay", Hours[k].hour, SendMessageOptions.DontRequireReceiver); }
                step++;
            }
        }
        else if (step == 2 + Hours.Length * 2 && t > 13 + Hours.Length * 8 + 1)
        {
            step++;
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
            Debug.Log("Surf shots: saved to " + Path.GetFullPath(Dir) + " -- commit them in GitHub Desktop.");
            EditorUtility.RevealInFinder(Path.GetFullPath(Path.Combine(Dir, "noon.png")));
        }
    }

    static void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(Dir, name + ".png"));
}
