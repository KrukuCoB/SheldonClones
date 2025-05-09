using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    [HarmonyPatch]
    public static class TryFindFreeSittingSpotOnThingPatch
    {
        [HarmonyPatch(typeof(Toils_Ingest), "TryFindFreeSittingSpotOnThing")]
        public static class Patch_TryFindFreeSittingSpotOnThing
        {
            public static void Postfix(ref bool __result, Thing t, Pawn pawn, out IntVec3 cell)
            {
                cell = IntVec3.Invalid;

                if (!__result)
                    return;

                // === 1. Если есть зарезервированное место, пробуем его ===
                if (pawn.IsReservedForSitting())
                {
                    IntVec3 reservedSpot = pawn.GetReservedSittingSpot();
                    if (reservedSpot.IsValid && reservedSpot.InBounds(pawn.Map) && pawn.CanReserveSittableOrSpot(reservedSpot))
                    {
                        if (!ChairUtility.IsSomeoneAlreadySitting(reservedSpot, pawn.Map, pawn))
                        {
                            // Место свободно — садимся
                            cell = reservedSpot;
                            return;
                        }
                        else
                        {
                            __result = false;
                            return;
                        }
                    }
                    else
                    {
                        // Место стало недоступным или повреждено — очищаем
                        pawn.RemoveReservedSittingSpot();
                    }
                }



                // === 2. Ищем свободное не назначенное место ===
                List<CompSheldonSeatAssignable> allSeats = CompSheldonSeatingManager.GetSeatsForMap(pawn.Map);
                foreach (var seatComp in allSeats)
                {
                    if (seatComp.GetAssignedPawns().Count == 0)
                    {
                        foreach (IntVec3 spot in seatComp.parent.OccupiedRect())
                        {
                            if (spot.InBounds(pawn.Map) &&
                                pawn.CanReserveSittableOrSpot(spot) &&
                                pawn.CanReserve(seatComp.parent) &&
                                !ChairUtility.IsSomeoneAlreadySitting(spot, pawn.Map, pawn))
                            {
                                seatComp.AssignSheldon(pawn);
                                pawn.SetReservedSittingSpot(spot);
                                cell = spot;
                                return;
                            }
                        }
                    }
                }

                // === 3. Пробуем временно использовать чужое место ===
                foreach (var seatComp in allSeats)
                {
                    if (seatComp.GetAssignedPawns().Count > 0 && !seatComp.BelongsToSheldon(pawn))
                    {
                        foreach (IntVec3 spot in seatComp.parent.OccupiedRect())
                        {
                            if (spot.InBounds(pawn.Map) &&
                                pawn.CanReserveSittableOrSpot(spot) &&
                                pawn.CanReserve(seatComp.parent) &&
                                !ChairUtility.IsSomeoneAlreadySitting(spot, pawn.Map, pawn))
                            {
                                cell = spot;
                                return;
                            }
                        }
                    }
                }

                // === 4. Ничего не найдено — будет есть стоя ===
                __result = false;
            }
        }

        public static class ChairUtility
        {
            public static bool IsSomeoneAlreadySitting(Thing chair, Pawn askingPawn)
            {
                if (chair == null || chair.Map == null)
                    return false;

                return IsSomeoneAlreadySitting(chair.Position, chair.Map, askingPawn);
            }

            public static bool IsSomeoneAlreadySitting(IntVec3 cell, Map map, Pawn askingPawn)
            {
                if (map == null) return false;

                var things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i] is Pawn otherPawn && otherPawn != askingPawn)
                    {
                        Job job = otherPawn.CurJob;
                        if (job != null && job.targetA.Cell == cell)
                        {
                            JobDef jobDef = job.def;
                            if (jobDef == JobDefOf.Ingest || 
                                jobDef == JobDefOf.Lovin ||
                                jobDef == JobDefOf.Meditate || 
                                jobDef == JobDefOf.UseCommsConsole ||
                                jobDef == JobDefOf.LayDown || 
                                jobDef == JobDefOf.Wait_MaintainPosture)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            // Новый метод: возвращает пешку, сидящую на клетке
            public static Pawn GetSittingPawnAt(IntVec3 cell, Map map, Pawn excluding = null)
            {
                if (map == null) return null;

                var things = cell.GetThingList(map);
                foreach (Thing thing in things)
                {
                    if (thing is Pawn p && p != excluding)
                    {
                        Job job = p.CurJob;
                        if (job != null && job.targetA.Cell == cell)
                        {
                            JobDef jobDef = job.def;
                            if (jobDef == JobDefOf.Ingest || 
                                jobDef == JobDefOf.Lovin ||
                                jobDef == JobDefOf.Meditate || 
                                jobDef == JobDefOf.UseCommsConsole ||
                                jobDef == JobDefOf.LayDown || 
                                jobDef == JobDefOf.Wait_MaintainPosture)
                            {
                                return p;
                            }
                        }
                    }
                }
                return null;
            }
        }
    }
}
