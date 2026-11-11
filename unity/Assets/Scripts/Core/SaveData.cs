// What survives between runs. PlayerPrefs for now; the full shop state comes across with the
// shop itself.
using UnityEngine;

public static class SaveData
{
    public static int Best { get => PlayerPrefs.GetInt("best", 0); set => PlayerPrefs.SetInt("best", value); }
    public static int BestDist { get => PlayerPrefs.GetInt("bestDist", 0); set => PlayerPrefs.SetInt("bestDist", value); }
    public static int Coins { get => PlayerPrefs.GetInt("coins", 0); set => PlayerPrefs.SetInt("coins", value); }
    public static void Flush() => PlayerPrefs.Save();
}
