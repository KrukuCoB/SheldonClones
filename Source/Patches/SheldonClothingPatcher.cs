using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using HarmonyLib;

namespace SheldonClones
{

    // Патч для автоматического одевания при генерации пешки
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new Type[] { typeof(PawnGenerationRequest) })]
    public static class PawnGenerator_GeneratePawn_Patch
    {
        public static void Postfix(Pawn __result, PawnGenerationRequest request)
        {
            // Проверяем, что это клон Шелдона
            if (__result?.def?.defName == "SheldonClone" && __result.RaceProps.Humanlike)
            {
                SheldonClothingHelper.EnsureSheldonHasShirt(__result);
            }
        }
    }

    public static class SheldonClothingHelper
    {
        // Список доступных футболок Шелдона
        private static readonly List<string> SheldonShirts = new List<string>
        {
            "KC_GreenLantern",
            "KC_BestNumber",
            "KC_Flash",
            "KC_Hawkman",
            "KC_PrehistoricMonsters",
            "KC_RubiksCubeMelting",
            "KC_TVTestPattern",
            "KC_GreatestAmericanHero",
            "KC_Batman",
            "KC_Superman",
            "KC_Bazinga",
            "KC_DopplerEffect",
            "KC_BazingaDB"
        };

        public static void EnsureSheldonHasShirt(Pawn pawn)
        {
            if (pawn?.apparel?.WornApparel == null) return;

            // Проверяем, есть ли уже футболка Шелдона
            bool hasSheldonShirt = pawn.apparel.WornApparel.Any(apparel =>
                SheldonShirts.Contains(apparel.def.defName));

            if (!hasSheldonShirt)
            {
                // Создаем случайную футболку Шелдона
                CreateAndEquipSheldonShirt(pawn);
            }
        }

        private static void CreateAndEquipSheldonShirt(Pawn pawn)
        {
            try
            {
                // Выбираем случайную футболку
                string randomShirtDef = SheldonShirts.RandomElement();
                ThingDef shirtDef = DefDatabase<ThingDef>.GetNamedSilentFail(randomShirtDef);

                if (shirtDef == null)
                {
                    Log.Warning($"SheldonClothingHelper: Could not find shirt def: {randomShirtDef}");
                    return;
                }

                // Определяем материал для футболки
                ThingDef fabric = GetBestAvailableFabric();

                // Создаем футболку
                Apparel shirt = (Apparel)ThingMaker.MakeThing(shirtDef, fabric);

                if (shirt != null)
                {
                    // Устанавливаем качество
                    shirt.TryGetComp<CompQuality>()?.SetQuality(GetRandomQuality(), ArtGenerationContext.Colony);

                    // Снимаем конфликтующую одежду
                    RemoveConflictingApparel(pawn, shirt);

                    // Одеваем футболку
                    pawn.apparel.Wear(shirt, false, false);

                    // Log.Message($"SheldonClothingHelper: Equipped {randomShirtDef} on {pawn.Name}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"SheldonClothingHelper: Error equipping shirt on {pawn.Name}: {ex}");
            }
        }

        private static ThingDef GetBestAvailableFabric()
        {
            // Приоритетный список материалов
            var preferredFabrics = new List<string>
            {
                "Synthread",
                "DevilstrandCloth",
                "Cloth",
                "WoolMegasloth",
                "WoolMuffalo"
            };

            foreach (string fabricName in preferredFabrics)
            {
                ThingDef fabric = DefDatabase<ThingDef>.GetNamedSilentFail(fabricName);
                if (fabric != null)
                    return fabric;
            }

            // Если ничего не найдено, возвращаем обычную ткань
            return ThingDefOf.Cloth;
        }

        private static QualityCategory GetRandomQuality()
        {
            // Шелдон предпочитает качественные вещи
            float rand = Rand.Value;

            if (rand < 0.05f) return QualityCategory.Legendary;
            if (rand < 0.15f) return QualityCategory.Masterwork;
            if (rand < 0.35f) return QualityCategory.Excellent;
            if (rand < 0.60f) return QualityCategory.Good;
            return QualityCategory.Normal;
        }

        private static void RemoveConflictingApparel(Pawn pawn, Apparel newApparel)
        {
            if (newApparel?.def?.apparel?.bodyPartGroups == null) return;

            var conflictingApparel = pawn.apparel.WornApparel
                .Where(worn => ApparelUtility.CanWearTogether(newApparel.def, worn.def, pawn.RaceProps.body) == false)
                .ToList();

            foreach (var conflicting in conflictingApparel)
            {
                pawn.apparel.Remove(conflicting);
                conflicting.Destroy();
            }
        }
    }
}