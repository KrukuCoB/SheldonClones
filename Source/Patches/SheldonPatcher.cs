using HarmonyLib;
using Verse;

namespace SheldonClones
{
    [StaticConstructorOnStartup]
    public static class SheldonPatcher
    {
        static SheldonPatcher()
        {
            var harmony = new Harmony("com.sheldonclones.patch");
            harmony.PatchAll();
            Log.Message("[SheldonClones] Harmony патчи загружены!");
        }
    }
}
