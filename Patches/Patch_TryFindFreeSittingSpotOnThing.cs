using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    [HarmonyPatch(typeof(Toils_Ingest))]
    [HarmonyPatch(nameof(Toils_Ingest.TryFindFreeSittingSpotOnThing))]
    public static class Patch_TryFindFreeSittingSpotOnThing
    {
        static bool Prefix(Thing t, Pawn pawn, out IntVec3 cell, ref bool __result)
        {
            cell = IntVec3.Invalid;

            // Применяем ко всем у кого нет своего стула
            if (!HasAssignedSeat(pawn))
            {
                // Локальная замена оригинального TryFind: ищем среди OccupiedRect()
                bool TryFindCell(Thing thing, out IntVec3 c)
                {
                    foreach (var pos in thing.OccupiedRect())
                        if (pawn.CanReserveSittableOrSpot(pos) &&
                            !pos.GetThingList(pawn.Map).OfType<Pawn>().Any(p => p != pawn))
                        {
                            c = pos;
                            return true;
                        }
                    c = IntVec3.Invalid;
                    return false;
                }

                // Шаг 2: ЧИСТЫЙ СТУЛ (ни кем не присвоен)
                foreach (var comp in pawn.Map.listerThings.AllThings
                    .Select(x => x.TryGetComp<CompSheldonSeatAssignable>())
                    .Where(c => c != null && c.AssignedPawnsForReading.Count == 0))
                {
                    // проверяем, что весь стул можно зарезервировать
                    if (pawn.CanReserve(comp.parent) && TryFindCell(comp.parent, out var spot2))
                    {
                        cell = spot2;
                        __result = true;
                        return false;
                    }
                }


                // Шаг 3: ЧУЖОЙ СТУЛ, НО СЕЙЧАС СВОБОДЕН
                foreach (var comp in pawn.Map.listerThings.AllThings
                    .Select(x => x.TryGetComp<CompSheldonSeatAssignable>())
                    .Where(c => c != null && c.AssignedPawnsForReading.Count > 0))
                {
                    if (pawn.CanReserve(comp.parent) && TryFindCell(comp.parent, out var spot3))
                    {
                        cell = spot3;
                        __result = true;
                        return false;
                    }
                }

                // Шаг 4: ничего подходящего не нашли — оригинальный метод
            }

            return true;
        }

        private static bool HasAssignedSeat(Pawn pawn)
        {
            return pawn.Map.listerThings.AllThings
                       .Select(x => x.TryGetComp<CompSheldonSeatAssignable>())
                       .Where(c => c != null)
                       .Any(c => c.AssignedAnything(pawn));
        }
    }
}
