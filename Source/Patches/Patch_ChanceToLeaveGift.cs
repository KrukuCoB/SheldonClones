using HarmonyLib;
using RimWorld;
using Verse;

namespace SheldonClones
{

    [HarmonyPatch(typeof(VisitorGiftForPlayerUtility), "ChanceToLeaveGift")]
    public static class ChanceToLeaveGift_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Faction faction, Map map, ref float __result)
        {
            if (faction == null || faction.IsPlayer)
            {
                __result = 0f;
                return false; // Пропускаем оригинальный метод
            }

            // Проверяем на фракцию клонов Шелдона
            if (faction.def.defName == "SheldonClone_Faction")
            {
                __result = 0f;
                Log.Message($"[SheldonClones] Клоны Шелдона не дарят подарки");
                return false; // Клоны Шелдона не дарят подарки
            }

            return true; // Выполняем оригинальный метод
        }
    }
}