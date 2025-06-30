using HarmonyLib;
using Verse;

namespace SheldonClones
{/*
    // ДЛЯ ПРОВЕРКИ: печатает все компоненты на объекте

    [HarmonyPatch(typeof(Thing), "SpawnSetup")]
    public static class DebugLogComps
    {
        static void Postfix(Thing __instance)
        {
            if (__instance.def.defName == "Armchair" || __instance.def.defName == "DiningChair" || __instance.def.defName == "Stool")
            {
                // Проверяем: это именно объект с компонентами
                if (__instance is ThingWithComps thingWithComps)
                {
                    Log.Message($"[DEBUG] {thingWithComps.LabelCap} comps:");
                    foreach (var comp in thingWithComps.AllComps)
                    {
                        Log.Message($"- {comp.GetType().Name}");
                    }
                }
                else
                {
                    Log.Message($"[DEBUG] {__instance.LabelCap} не ThingWithComps, компонентов нет.");
                }
            }
        }
    }*/
}