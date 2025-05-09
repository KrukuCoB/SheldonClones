using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    /// <summary>
    /// Патч для ограничения выбора столов для социализации только теми, 
    /// рядом с которыми находится закрепленный стул клона Шелдона
    /// </summary>
    [HarmonyPatch]
    public static class Patch_JoyGiver_SocialRelax_Spots
    {
        // Получаем приватный метод TryGiveJobInt через рефлексию
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return typeof(JoyGiver_SocialRelax).GetMethod("TryGiveJobInt",
                BindingFlags.Instance | BindingFlags.NonPublic,
                Type.DefaultBinder,
                new Type[] { typeof(Pawn), typeof(Predicate<CompGatherSpot>) },
                null);
        }

        // Префикс для метода TryGiveJobInt перед его выполнением
        [HarmonyPrefix]
        public static bool Prefix(Pawn pawn, ref Predicate<CompGatherSpot> gatherSpotValidator, ref Job __result)
        {
            // Если не клон Шелдона или у него нет зарезервированного места - используем стандартную логику
            if (pawn.def != AlienDefOf.SheldonClone || !pawn.IsReservedForSitting())
                return true;

            // Сохраняем оригинальный валидатор (если он был)
            Predicate<CompGatherSpot> originalValidator = gatherSpotValidator;

            // Создаем новый валидатор, который будет проверять наличие стула Шелдона рядом с местом сбора
            gatherSpotValidator = (CompGatherSpot spot) => {
                // Сначала проверяем оригинальный валидатор, если он был
                if (originalValidator != null && !originalValidator(spot))
                    return false;

                // Получаем зарезервированное место клона
                IntVec3 reservedSpot = pawn.GetReservedSittingSpot();
                if (!reservedSpot.InBounds(pawn.Map))
                    return false;

                // Проверяем, что зарезервированный стул находится рядом с этим местом сбора
                Thing chair = pawn.Map.thingGrid.ThingAt(reservedSpot, ThingCategory.Building);
                if (chair == null || chair.def.building?.isSittable != true)
                    return false;

                // Проверяем расстояние между стулом и местом сбора
                float maxDistance = 3.9f; // Стандартное расстояние для социализации
                return chair.Position.InHorDistOf(spot.parent.Position, maxDistance) &&
                       GenSight.LineOfSight(chair.Position, spot.parent.Position, pawn.Map, true);
            };

            // Продолжаем выполнение оригинального метода с нашим модифицированным валидатором
            return true;
        }
    }
}