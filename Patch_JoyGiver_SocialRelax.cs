using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse.AI;
using Verse;

namespace SheldonClones
{
    [HarmonyPatch(typeof(JoyGiver_SocialRelax), "TryGiveJobInt")]
    public static class Patch_JoyGiver_SocialRelax
    {
        public static void Prefix(ref Job __result, Pawn pawn)
        {
            if (__result == null || pawn.def != AlienDefOf.SheldonClone)
                return;

            // Если в TargetB уже назначен стул — выходим
            if (__result.targetB.Thing is Building chair && chair.def.building?.isSittable == true)
                return;

            // Ищем забронированное место
            if (pawn.IsReservedForSitting())
            {
                IntVec3 spot = pawn.GetReservedSittingSpot();
                if (!spot.InBounds(pawn.Map)) return;

                Thing seat = spot.GetThingList(pawn.Map)
                    .FirstOrDefault(t => t.def.building?.isSittable == true);

                if (seat != null &&
                    !seat.IsForbidden(pawn) &&
                    pawn.CanReserve(seat) &&
                    seat.Position.InHorDistOf(__result.targetA.Cell, 3.9f) &&
                    GenSight.LineOfSight(__result.targetA.Cell, seat.Position, pawn.Map, true) &&
                    !ChairUtility.IsSomeoneAlreadySitting(seat, pawn))
                {
                    __result.targetB = seat;
                }
            }
        }
    }

    [HarmonyPatch(typeof(JoyGiver_SocialRelax), "TryFindChairNear")]
    public static class Patch_TryFindChairNear_Sheldon
    {
        public static bool Prefix(IntVec3 center, Pawn sitter, ref Thing chair, ref bool __result)
        {
            if (sitter.def != AlienDefOf.SheldonClone || !sitter.IsReservedForSitting())
                return true;

            IntVec3 reservedSpot = sitter.GetReservedSittingSpot();

            if (!reservedSpot.InBounds(sitter.Map))
                return true;

            Thing reservedChair = reservedSpot.GetThingList(sitter.Map).Find(t => t.def.building?.isSittable == true);

            if (reservedChair != null &&
                !reservedChair.IsForbidden(sitter) &&
                sitter.CanReserve(reservedChair) &&
                GenSight.LineOfSight(center, reservedChair.Position, sitter.Map, skipFirstCell: true) &&
                !ChairUtility.IsSomeoneAlreadySitting(reservedChair.Position, sitter.Map, sitter))
            {
                chair = reservedChair;
                __result = true;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(JoyGiver_SocialRelax), "TryFindChairBesideTable")]
    public static class Patch_TryFindChairBesideTable
    {
        public static bool Prefix(Thing table, Pawn sitter, ref Thing chair, ref bool __result)
        {
            if (sitter.def != AlienDefOf.SheldonClone)
                return true;

            if (sitter.IsReservedForSitting())
            {
                IntVec3 spot = sitter.GetReservedSittingSpot();
                Thing personalChair = spot.GetThingList(sitter.Map).FirstOrDefault(t => t.def.building?.isSittable == true);

                if (personalChair != null &&
                    !personalChair.IsForbidden(sitter) &&
                    sitter.CanReserve(personalChair) &&
                    GenSight.LineOfSight(table.Position, personalChair.Position, sitter.Map))
                {
                    chair = personalChair;
                    __result = true;
                    return false;
                }
            }

            return true;
        }
    }
}
