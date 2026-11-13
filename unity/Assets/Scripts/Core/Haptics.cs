// Vibration on the phone, the way the web's buzz() does it: a pulse of so many milliseconds, or a
// pattern (on, off, on, ...). Handheld.Vibrate, which this replaces, has one length only -- about
// four tenths of a second on Android and a full buzz on iPhone -- so every trick tap felt like a
// crash. Here:
//   - Android: the system vibrator with the exact lengths and patterns (VibrationEffect from
//     Android 8, the plain timed call before it).
//   - iPhone: the system's impact taps (light, medium, heavy by length) through a tiny native
//     plugin (Plugins/iOS/SurfHaptics.mm); a pattern is played as a tap at each "on".
//   - The editor, and so Unity Remote, which runs the game on the computer: nothing to vibrate.
// Settings' Vibration switch is honoured by the caller (Sfx.HapticsOn).
using System.Collections;
using UnityEngine;

public static class Haptics
{
    public static void Pulse(int ms)
    {
        ms = Mathf.Clamp(ms, 1, 1000);
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!AndroidPulse(ms)) Fallback();
#elif UNITY_IOS && !UNITY_EDITOR
        Tap(ms);
#endif
    }

    /// web navigator.vibrate([on, off, on, ...])
    public static void Pattern(int[] p)
    {
        if (p == null || p.Length == 0) return;
        if (p.Length == 1) { Pulse(p[0]); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!AndroidPattern(p)) Fallback();
#elif UNITY_IOS && !UNITY_EDITOR
        Runner.Get().StartCoroutine(TapPattern(p));
#endif
    }

    // keeps the VIBRATE permission in the Android manifest (Unity adds it when it sees this call),
    // and is the last resort if the vibrator service cannot be reached
    static void Fallback()
    {
#if UNITY_ANDROID || UNITY_IOS
        try { Handheld.Vibrate(); } catch { }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaObject vib; static int sdk = -1; static bool broken;
    static bool Ready()
    {
        if (broken) return false;
        if (vib != null) return true;
        try
        {
            using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                vib = act.Call<AndroidJavaObject>("getSystemService", "vibrator");
            using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) sdk = ver.GetStatic<int>("SDK_INT");
            if (vib == null || !vib.Call<bool>("hasVibrator")) { broken = true; vib = null; return false; }
            return true;
        }
        catch { broken = true; vib = null; return false; }
    }
    static bool AndroidPulse(int ms)
    {
        if (!Ready()) return false;
        try
        {
            if (sdk >= 26)
                using (var ve = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var eff = ve.CallStatic<AndroidJavaObject>("createOneShot", (long)ms, -1))   // -1: the device's default strength
                    vib.Call("vibrate", eff);
            else vib.Call("vibrate", (long)ms);
            return true;
        }
        catch { return false; }
    }
    static bool AndroidPattern(int[] p)
    {
        if (!Ready()) return false;
        try
        {
            // Android's timings start with a wait; the web's start with a vibration
            var t = new long[p.Length + 1]; t[0] = 0;
            for (int i = 0; i < p.Length; i++) t[i + 1] = Mathf.Clamp(p[i], 0, 1000);
            if (sdk >= 26)
                using (var ve = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var eff = ve.CallStatic<AndroidJavaObject>("createWaveform", t, -1))         // -1: play once
                    vib.Call("vibrate", eff);
            else vib.Call("vibrate", t, -1);
            return true;
        }
        catch { return false; }
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")] static extern void SurfHapticImpact(int style);
    // a short buzz is a light tap, a landing a medium one, a crash or a bite a heavy one
    static void Tap(int ms) { try { SurfHapticImpact(ms <= 14 ? 0 : ms <= 28 ? 1 : 2); } catch { Fallback(); } }
    static IEnumerator TapPattern(int[] p)
    {
        for (int i = 0; i < p.Length; i++)
        {
            if (i % 2 == 0) Tap(p[i]);
            yield return new WaitForSecondsRealtime(p[i] / 1000f);
        }
    }
#endif

    // somewhere to run a pattern from on iPhone
    class Runner : MonoBehaviour
    {
        static Runner inst;
        public static Runner Get()
        {
            if (inst == null) { var go = new GameObject("Haptics"); Object.DontDestroyOnLoad(go); inst = go.AddComponent<Runner>(); }
            return inst;
        }
    }
}
