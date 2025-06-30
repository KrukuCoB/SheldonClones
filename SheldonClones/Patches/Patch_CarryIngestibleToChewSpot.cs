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

            // Только для клонов Шелдона
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
            Log.Message($"[Debug] HandleSheldonClone вызван для {pawn.LabelShort}");

            var seatResult = Sheldon_SeatFinder.FindSeatForSheldonClone(pawn, requireTable: true);
            Log.Message($"[Debug] Результат поиска места с столом: {seatResult.IsValid}");

            if (!seatResult.IsValid)
            {
                Log.Message($"[Debug] Место с столом не найдено, ищем без стола");
                // Попробуем найти место БЕЗ требования стола
                seatResult = Sheldon_SeatFinder.FindSeatForSheldonClone(pawn, requireTable: false);
                Log.Message($"[Debug] Результат поиска места без стола: {seatResult.IsValid}");

                if (!seatResult.IsValid)
                {
                    Log.Message($"[Debug] Место для Sheldon не найдено, пробуем как обычную пешку");
                    // Попробуем найти место как для обычной пешки
                    seatResult = Sheldon_SeatFinder.FindSeatForRegularPawn(pawn, requireTable: true);
                    Log.Message($"[Debug] Результат поиска как обычная пешка: {seatResult.IsValid}");
                }
            }

            if (seatResult.isOwnSeat && seatResult.needsEviction)
            {
                // Проверяем лимит попыток выселения
                int attempts = GetEvictionAttempts(pawn);
                if (attempts >= MAX_EVICTION_ATTEMPTS)
                {
                    Log.Warning($"[Sheldon] {pawn.LabelShort} превысил лимит попыток выселения");
                    ResetEvictionAttempts(pawn);
                    return HandleRegularPawn(pawn, ingestibleInd);
                }

                IncrementEvictionAttempts(pawn);

                if (seatResult.occupant != null)
                {
                    // Выполняем выселение
                    var evictionDef = DefDatabase<InteractionDef>.GetNamed("SheldonWarnedForSittingInMySpot");
                    pawn.interactions.TryInteractWith(seatResult.occupant, evictionDef);
                    Sheldon_Utils.ForceEvict(pawn, seatResult.occupant);
                    Log.Message($"[Sheldon] {pawn.LabelShort} выгоняет {seatResult.occupant.LabelShort}!");
                }
            }
            else if (seatResult.isOwnSeat)
            {
                ResetEvictionAttempts(pawn);
            }

            if (seatResult.IsValid)
            {
                Log.Message($"[Debug] Создаем Toil для {pawn.LabelShort} на позицию {seatResult.position}");
                return CreateModifiedCarryToilForPosition(pawn, seatResult.chair, seatResult.position, ingestibleInd);
            }
            else
            {
                Log.Message($"[Debug] Место не найдено, возвращаем null - будет использована оригинальная логика");
                return null; // Используем оригинальную логику
            }
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
                        Log.Warning($"[Sheldon] Ошибка при резервировании собственной позиции: {ex.Message}");
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
                        Log.Warning($"[Sheldon] Ошибка при резервировании позиции {targetPosition}: {ex.Message}");
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
                        comp.TryAssignPawnToPosition(pawn, pos);
                        Log.Message($"[SheldonClones] {pawn.LabelShort} присвоил себе {thing.LabelShort} на {thing.Position}");
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