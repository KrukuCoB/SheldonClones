using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    public static class Patch_JobGiver_GetFood
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn.def != AlienDefOf.SheldonClone || !pawn.IsReservedForSitting())
                return true; // стандартная логика

            IntVec3 reservedSpot = pawn.GetReservedSittingSpot();
            if (!reservedSpot.IsValid || !reservedSpot.InBounds(pawn.Map))
                return true;

            if (TryFindFreeSittingSpotOnThingPatch.ChairUtility.IsSomeoneAlreadySitting(reservedSpot, pawn.Map, pawn))
            {
                // Назначаем ожидание возле стула
                Job waitJob = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("WaitNearMyChair"), reservedSpot);
                __result = waitJob;
                return false; // не продолжаем назначение еды
            }

            return true; // место свободно — продолжить выдачу еды
        }
    }
}
