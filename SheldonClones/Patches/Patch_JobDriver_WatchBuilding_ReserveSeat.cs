using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    [HarmonyPatch(typeof(JobDriver_WatchBuilding), nameof(JobDriver_WatchBuilding.TryMakePreToilReservations))]
    static class Patch_JobDriver_WatchBuilding_ReserveSeat
    {
        static bool Prefix(JobDriver_WatchBuilding __instance, bool errorOnFailed, ref bool __result)
        {
            var pawn = __instance.pawn;
            var job = __instance.job;

            // Только для клонов Шелдона и именно просмотр ТВ
            if (pawn.def == AlienDefOf.SheldonClone 
                && job.def.defName == "WatchTelevision")
            {
                // Ищем его CompSheldonSeatAssignable
                var myComp = pawn.Map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.building != null && b.def.building.isSittable)
                    .Select(b => b.TryGetComp<CompSheldonSeatAssignable>())
                    .FirstOrDefault(c => c != null && c.AssignedAnything(pawn));

                if (myComp != null && myComp.parent.Spawned)
                {
                    var chair = myComp.parent;
                    // 1) Если кто-то уже сидит на моем стуле — выгоним
                    var occupant = chair.Map.thingGrid.ThingAt<Pawn>(chair.Position);
                    if (occupant != null && occupant != pawn)
                    {
                        // взаимодействие, страйк и эвикт из утилит
                        var defEvict = DefDatabase<InteractionDef>.GetNamed("SheldonWarnedForSittingInMySpot");
                        pawn.interactions.TryInteractWith(occupant, defEvict);
                        Sheldon_Utils.ForceEvict(pawn, occupant);
                    }

                    // 2) Резервируем телевизор
                    if (!pawn.Reserve(job.targetA, job, job.def.joyMaxParticipants, 0, null, errorOnFailed))
                    {
                        __result = false;
                        return false;
                    }
                    // 3) Резервируем ячейку на стуле
                    var seatCell = chair.OccupiedRect().First();
                    if (!pawn.ReserveSittableOrSpot(seatCell, job, errorOnFailed))
                    {
                        // не смогли — отдадим ванильный механизм выбору
                        return true;
                    }

                    // 4) Подставляем цели и говорим Harmony, что мы всё сделали
                    job.targetB = new LocalTargetInfo(seatCell);
                    __result = true;
                    return false;
                }
            }
            // иначе — отдать управление ванильному методу
            return true;
        }
    }
}
