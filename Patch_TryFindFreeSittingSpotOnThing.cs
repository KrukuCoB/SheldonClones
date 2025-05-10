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
        // Патч метода Toils_Ingest.TryFindFreeSittingSpotOnThing
        [HarmonyPatch(typeof(Toils_Ingest), "TryFindFreeSittingSpotOnThing")]
        public static class Patch_TryFindFreeSittingSpotOnThing
        {
            public static void Postfix(ref bool __result, Thing t, Pawn pawn, out IntVec3 cell)
            {
                // По умолчанию место не найдено
                cell = IntVec3.Invalid;

                // Если оригинальный метод не нашел место – сразу выходим
                if (!__result || pawn.def != AlienDefOf.SheldonClone)
                    return;

                // Если не клон Шелдона – используем стандартную логику (любой свободный спот на объекте t)
                if (pawn.def != AlienDefOf.SheldonClone)
                {
                    foreach (IntVec3 spot in t.OccupiedRect())
                    {
                        if (spot.InBounds(pawn.Map) && pawn.CanReserveSittableOrSpot(spot))
                        {
                            cell = spot;
                            return;
                        }
                    }
                    // Если не найдено – есть в виду стоячий режим (но т.к. результат уже true, 
                    // в оригинале клон бы нашел место на полу; здесь делаем __result=false)
                    __result = false;
                    return;
                }

                // 1. Если у клона уже есть свое место (резерв) – пытаемся сесть туда
                if (pawn.IsReservedForSitting())
                {
                    IntVec3 reservedSpot = pawn.GetReservedSittingSpot();
                    // Проверяем доступность клетки
                    if (reservedSpot.InBounds(pawn.Map) && pawn.CanReserveSittableOrSpot(reservedSpot))
                    {
                        // Если там никого нет – садимся
                        if (!ChairUtility.IsSomeoneAlreadySitting(reservedSpot, pawn.Map, pawn))
                        {
                            cell = reservedSpot;
                            return;
                        }
                        // Если на месте кто-то сидит – пропускаем, НЕ снимая бронь
                    }
                    else
                    {
                        // Если место недоступно (за пределами карты или не резервируемо) – снимаем бронь
                        pawn.RemoveReservedSittingSpot();
                    }
                }

                // Получаем кэшированный список стульев
                List<CompSheldonSeatAssignable> allSeats = CompSheldonSeatingManager.GetSeatsForMap(pawn.Map);

                // 2. Ищем любое свободное место (стул без владельца)
                foreach (CompSheldonSeatAssignable seatComp in allSeats)
                {
                    List<Pawn> owners = seatComp.GetAssignedPawns();
                    // Если стул свободен (никому не принадлежит)
                    if (owners.Count == 0)
                    {
                        // Проверяем каждую клетку, занимаемую стулом
                        foreach (IntVec3 spot in seatComp.parent.OccupiedRect())
                        {
                            if (spot.InBounds(pawn.Map) &&
                                pawn.CanReserveSittableOrSpot(spot) &&
                                pawn.CanReserve(seatComp.parent) &&
                                !ChairUtility.IsSomeoneAlreadySitting(spot, pawn.Map, pawn))
                            {
                                // Присваиваем стул клону и резервируем место
                                seatComp.AssignSheldon(pawn);
                                pawn.SetReservedSittingSpot(spot);
                                cell = spot;
                                return;
                            }
                        }
                    }
                }

                // 3. Нет свободных не назначенных мест – пробуем временно использовать чужой стул
                foreach (CompSheldonSeatAssignable seatComp in allSeats)
                {
                    List<Pawn> owners = seatComp.GetAssignedPawns();
                    // Стул назначен другому клону
                    if (owners.Count > 0 && !seatComp.BelongsToSheldon(pawn))
                    {
                        foreach (IntVec3 spot in seatComp.parent.OccupiedRect())
                        {
                            if (spot.InBounds(pawn.Map) &&
                                pawn.CanReserveSittableOrSpot(spot) &&
                                pawn.CanReserve(seatComp.parent) &&
                                !ChairUtility.IsSomeoneAlreadySitting(spot, pawn.Map, pawn))
                            {
                                // Используем место без присвоения
                                cell = spot;
                                return;
                            }
                        }
                    }
                }

                // 4. Никакого свободного места нет – клон будет есть стоя
                __result = false;
            }
        }
    }
}