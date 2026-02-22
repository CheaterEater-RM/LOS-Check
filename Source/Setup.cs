using HarmonyLib;
using Verse;

namespace LOSOverlay
{
    [StaticConstructorOnStartup]
    public static class LOSOverlay_Init
    {
        static LOSOverlay_Init()
        {
            var harmony = new HarmonyLib.Harmony("com.cheatereater.losoverlay");
            harmony.PatchAll();
            Log.Message("[LOS Overlay] Harmony patches applied.");
        }
    }
}