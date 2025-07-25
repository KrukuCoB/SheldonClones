using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    [HarmonyPatch(typeof(Toils_Ingest))]
    [HarmonyPatch(nameof(Toils_Ingest.CarryIngestibleToChewSpot))]
    public static class Patch_CarryIngestibleToChewSpot
    {
        // Добавляем словарь для отслеживания попыток выселения
        private static readonly Dictionary<Pawn, int> evictionAttempts = new Dictionary<Pawn, int>();
        private const int MAX_EVICTION_ATTEMPTS = 3;

        static bool Prefix(Pawn pawn, TargetIndex ingestibleInd, ref Toil __result)
        {
            if (pawn == null || pawn.Map == null || pawn.jobs?.curJob == null)
                return true;

            // Очищаем устаревшие записи об выселениях
            Sheldon_Utils.CleanupExpiredEvictions();

            // Только для клонов Шелдонов-поселенцев
            if (pawn.def == AlienDefOf.SheldonClone)
            {
                var result = HandleSheldonClone(pawn, ingestibleInd);
                if (result != null)
                {
                    __result = result;
                    return false;
                }
                return true;
            }
            else
            {
                // Для обычных пешек - используем улучшенную логику поиска
                var result = HandleRegularPawn(pawn, ingestibleInd);
                if (result != null)
                {
                    __result = result;
                    return false;
                }
                return true;
            }
        }

        private static Toil HandleSheldonClone(Pawn pawn, TargetIndex ingestibleInd)
        {
            Log.Message($"[CarryIngestibleToChewSpot] HandleSheldonClone вызван для {pawn.LabelShort}");

            // ═══ ЭТАП 1: Поиск собственного назначенного места ═══
            var seatResult = Sheldon_SeatFinder.FindSeatForSheldonClone(pawn, requireTable: true);
            Log.Message($"[CarryIngestibleToChewSpot] Поиск своего места с столом: {seatResult.IsValid}");

            // Если свое место найдено
            if (seatResult.IsValid && seatResult.isOwnSeat)
            {
                Log.Message($"[CarryIngestibleToChewSpot] Найдено собственное место для {pawn.LabelShort}");

                // Проверяем, нужно ли выселение
                if (seatResult.needsEviction)
                {
                    // Проверяем лимит попыток выселения
                    int attempts = GetEvictionAttempts(pawn);
                    if (attempts >= MAX_EVICTION_ATTEMPTS)
                    {
                        Log.Warning($"[CarryIngestibleToChewSpot] {pawn.LabelShort} превысил лимит попыток выселения, ищем альтернативу");
                        ResetEvictionAttempts(pawn);
                        // Переходим к поиску альтернативы (этап 2)
                    }
                    else
                    {
                        // Выполняем выселение
                        IncrementEvictionAttempts(pawn);

                        if (seatResult.occupant != null)
                        {
                            var evictionDef = DefDatabase<InteractionDef>.GetNamed("SheldonWarnedForSittingInMySpot");
                            pawn.interactions.TryInteractWith(seatResult.occupant, evictionDef);
                            Sheldon_Utils.ForceEvict(pawn, seatResult.occupant);
                            Log.Message($"[CarryIngestibleToChewSpot] {pawn.LabelShort} выгоняет {seatResult.occupant.LabelShort}!");
                        }

                        // Используем свое место
                        return CreateModifiedCarryToilForPosition(pawn, seatResult.chair, seatResult.position, ingestibleInd);
                    }
                }
                else
                {
                    // Свое место свободно - используем его
                    ResetEvictionAttempts(pawn);
                    Log.Message($"[CarryIngestibleToChewSpot] Собственное место свободно, используем его");
                    return CreateModifiedCarryToilForPosition(pawn, seatResult.chair, seatResult.position, ingestibleInd);
                }
            }

            // ═══ ЭТАП 2: Поиск альтернативного места с столом ═══
            Log.Message($"[CarryIngestibleToChewSpot] Ищем альтернативное место со столом");
            var altSeatResult = Sheldon_SeatFinder.FindSeatForRegularPawn(pawn, requireTable: true);

            if (altSeatResult.IsValid)
            {
                Log.Message($"[CarryIngestibleToChewSpot] Найдено альтернативное место со столом");
                return CreateModifiedCarryToilForSpecificPosition(pawn, altSeatResult.chair, altSeatResult.position, ingestibleInd);
            }

            // ═══ ЭТАП 3: Поиск своего места БЕЗ стола ═══
            Log.Message($"[CarryIngestibleToChewSpot] Ищем своё место без требования стола");
            seatResult = Sheldon_SeatFinder.FindSeatForSheldonClone(pawn, requireTable: false);

            if (seatResult.IsValid && seatResult.isOwnSeat)
            {
                if (seatResult.needsEviction)
                {
                    // Те же проверки лимита выселения
                    int attempts = GetEvictionAttempts(pawn);
                    if (attempts < MAX_EVICTION_ATTEMPTS)
                    {
                        IncrementEvictionAttempts(pawn);

                        if (seatResult.occupant != null)
                        {
                            var evictionDef = DefDatabase<InteractionDef>.GetNamed("SheldonWarnedForSittingInMySpot");
                            pawn.interactions.TryInteractWith(seatResult.occupant, evictionDef);
                            Sheldon_Utils.ForceEvict(pawn, seatResult.occupant);
                            Log.Message($"[CarryIngestibleToChewSpot] {pawn.LabelShort} выгоняет {seatResult.occupant.LabelShort} (без стола)!");
                        }

                        return CreateModifiedCarryToilForPosition(pawn, seatResult.chair, seatResult.position, ingestibleInd);
                    }
                }
                else
                {
                    Log.Message($"[CarryIngestibleToChewSpot] Собственное место без стола свободно");
                    return CreateModifiedCarryToilForPosition(pawn, seatResult.chair, seatResult.position, ingestibleInd);
                }
            }

            // ═══ ЭТАП 4: Поиск любого места БЕЗ стола ═══
            Log.Message($"[CarryIngestibleToChewSpot] Ищем любое альтернативное место без стола");
            altSeatResult = Sheldon_SeatFinder.FindSeatForRegularPawn(pawn, requireTable: false);

            if (altSeatResult.IsValid)
            {
                Log.Message($"[CarryIngestibleToChewSpot] Найдено альтернативное место без стола");
                return CreateModifiedCarryToilForSpecificPosition(pawn, altSeatResult.chair, altSeatResult.position, ingestibleInd);
            }

            // ═══ ЭТАП 5: Ничего не найдено - используем оригинальную логику ═══
            Log.Message($"[CarryIngestibleToChewSpot] Для {pawn.LabelShort} не найдено подходящих мест - используем оригинальную логику игры");
            return null; // Это позволит выполниться оригинальной логике игры
        }

        private static Toil CreateModifiedCarryToilForPosition(Pawn pawn, Thing targetChair, IntVec3 targetPosition, TargetIndex ingestibleInd)
        {
            Toil toil = ToilMaker.MakeToil("Sheldon_CarryToSpecificPosition");

            toil.initAction = delegate
            {
                Pawn actor = toil.actor;

                // Проверяем, является ли этот пешка владельцем этой позиции
                var myComp = targetChair?.TryGetComp<CompSheldonSeatAssignable>();
                bool isOwner = myComp != null && myComp.AssignedAnything(actor) &&
                              myComp.GetAssignedPosition(actor) == targetPosition;

                if (isOwner)
                {
                    // Для владельца - принудительно используем его позицию
                    try
                    {
                        actor.ReserveSittableOrSpot(targetPosition, actor.CurJob);
                        actor.Map.pawnDestinationReservationManager.Reserve(actor, actor.CurJob, targetPosition);
                        actor.pather.StartPath(targetPosition, PathEndMode.OnCell);
                        return;
                    }
                    catch (System.Exception ex)
                    {
                         Log.Warning($"[CarryIngestibleToChewSpot] Ошибка при резервировании собственной позиции: {ex.Message}");
                    }
                }

                // Fallback логика...
                IntVec3 cell = RCellFinder.SpotToChewStandingNear(actor, actor.CurJob.GetTarget(ingestibleInd).Thing,
                    (IntVec3 c) => actor.CanReserveSittableOrSpot(c));

                if (cell.IsValid)
                {
                    actor.ReserveSittableOrSpot(cell, actor.CurJob);
                    actor.Map.pawnDestinationReservationManager.Reserve(actor, actor.CurJob, cell);
                    actor.pather.StartPath(cell, PathEndMode.OnCell);
                }
                else
                {
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
            };

            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            return toil;
        }

        private static Toil HandleRegularPawn(Pawn pawn, TargetIndex ingestibleInd)
        {
            var seatResult = Sheldon_SeatFinder.FindSeatForRegularPawn(pawn, requireTable: true);

            if (!seatResult.IsValid)
                return null; // Используем оригинальную логику

            return CreateModifiedCarryToilForSpecificPosition(pawn, seatResult.chair, seatResult.position, ingestibleInd);
        }

        // Управление счетчиком попыток выселения
        private static int GetEvictionAttempts(Pawn pawn)
        {
            return evictionAttempts.TryGetValue(pawn, out int attempts) ? attempts : 0;
        }

        private static void IncrementEvictionAttempts(Pawn pawn)
        {
            evictionAttempts[pawn] = GetEvictionAttempts(pawn) + 1;
        }

        private static void ResetEvictionAttempts(Pawn pawn)
        {
            evictionAttempts.Remove(pawn);
        }

        public static void ClearAllEvictionAttempts() // Очищает все накопленные попытки выселения
        {
            evictionAttempts.Clear();
        }

        private static Toil CreateModifiedCarryToilForSpecificPosition(Pawn pawn, Thing targetChair, IntVec3 targetPosition, TargetIndex ingestibleInd)
        {
            Toil toil = ToilMaker.MakeToil("Sheldon_CarryToSpecificPosition_Regular");

            toil.initAction = delegate
            {
                Pawn actor = toil.actor;

                // Проверяем, что позиция все еще свободна
                bool positionStillFree = actor.CanReserveSittableOrSpot(targetPosition) &&
                                        !targetPosition.GetThingList(actor.Map).OfType<Pawn>().Any(p => p != actor);

                if (positionStillFree)
                {
                    try
                    {
                        actor.ReserveSittableOrSpot(targetPosition, actor.CurJob);
                        actor.Map.pawnDestinationReservationManager.Reserve(actor, actor.CurJob, targetPosition);
                        actor.pather.StartPath(targetPosition, PathEndMode.OnCell);
                        return;
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning($"[CarryIngestibleToChewSpot] Ошибка при резервировании позиции {targetPosition}: {ex.Message}");
                    }
                }

                // Fallback логика...
                IntVec3 cell = RCellFinder.SpotToChewStandingNear(actor, actor.CurJob.GetTarget(ingestibleInd).Thing,
                    (IntVec3 c) => actor.CanReserveSittableOrSpot(c));

                if (cell.IsValid)
                {
                    actor.ReserveSittableOrSpot(cell, actor.CurJob);
                    actor.Map.pawnDestinationReservationManager.Reserve(actor, actor.CurJob, cell);
                    actor.pather.StartPath(cell, PathEndMode.OnCell);
                }
                else
                {
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
            };

            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            return toil;
        }

        // Постфикс для назначения стула при первом приёме пищи
        static void Postfix(Toil __result, Pawn pawn, TargetIndex ingestibleInd)
        {
            if (pawn.def != AlienDefOf.SheldonClone) return;
            if (HasAssignedSeat(pawn)) return;

            __result.AddFinishAction(() =>
            {
                var pos = pawn.Position;
                foreach (var thing in pos.GetThingList(pawn.Map))
                {
                    var comp = thing.TryGetComp<CompSheldonSeatAssignable>();
                    if (comp != null && comp.HasFreeSlot)
                    {
                        comp.TryAssignPawnToSpecificPosition(pawn, pos);
                        break;
                    }
                }
            });
        }

        private static bool HasAssignedSeat(Pawn pawn)
        {
            return pawn.Map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building != null && b.def.building.isSittable)
                .Select(b => b.TryGetComp<CompSheldonSeatAssignable>())
                .Any(c => c != null && c.AssignedAnything(pawn));
        }
    }
}