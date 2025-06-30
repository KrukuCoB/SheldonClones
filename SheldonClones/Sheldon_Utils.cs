using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    public static class Sheldon_Utils
    {
        // Словарь для отслеживания недавно выселенных пешек и мест, с которых их выселили
        private static readonly Dictionary<Pawn, HashSet<Thing>> recentEvictions = new Dictionary<Pawn, HashSet<Thing>>();
        private static readonly Dictionary<Pawn, int> evictionCooldowns = new Dictionary<Pawn, int>();
        private const int EVICTION_COOLDOWN_TICKS = 300; // 5 секунд

        public static void ForceEvict(Pawn initiator, Pawn victim)
        {
            if (victim?.jobs == null) return;

            // Запоминаем место, с которого выселяем
            var currentPosition = victim.Position;
            var chairsAtPosition = new List<Thing>();

            foreach (var thing in currentPosition.GetThingList(victim.Map))
            {
                if (thing.TryGetComp<CompSheldonSeatAssignable>() != null)
                {
                    chairsAtPosition.Add(thing);
                }
            }

            // Добавляем все стулья на этой позиции в список запрещенных для этой пешки
            // ИСПРАВЛЕНИЕ: Теперь запрещаем ВЕСЬ объект мебели, а не только конкретную позицию
            if (chairsAtPosition.Count > 0)
            {
                if (!recentEvictions.ContainsKey(victim))
                    recentEvictions[victim] = new HashSet<Thing>();

                foreach (var chair in chairsAtPosition)
                {
                    recentEvictions[victim].Add(chair);
                    Log.Message($"[Sheldon_Utils] {chair.LabelShort} из ткани (нормально) запрещен для {victim.LabelShort} из-за недавнего выселения");
                }

                evictionCooldowns[victim] = Find.TickManager.TicksGame + EVICTION_COOLDOWN_TICKS;
            }

            // Сначала пытаемся прервать текущее задание "мягко"
            if (victim.jobs.curJob != null)
            {
                // УСИЛЕНИЕ: Для заданий социального отдыха принудительно прерываем и сбрасываем резервации
                if (victim.jobs.curJob.def.defName == "SocialRelax")
                {
                    victim.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
                    // Сбрасываем резервации сразу
                    victim.Map.reservationManager.ReleaseAllClaimedBy(victim);
                    victim.Map.pawnDestinationReservationManager.ReleaseAllClaimedBy(victim);
                    Log.Message($"[Sheldon_Utils] Принудительно прерываем SocialRelax для {victim.LabelShort}");
                }
                // Если пешка ест - прерываем с возможностью повтора
                else if (victim.jobs.curJob.def == JobDefOf.Ingest ||
                         victim.jobs.curJob.def.defName == "WatchTelevision")
                {
                    victim.jobs.EndCurrentJob(JobCondition.InterruptOptional, true);
                }
                else
                {
                    // Для других заданий - принудительное прерывание
                    victim.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
                    // Сбрасываем сразу все её резервации
                    victim.Map.reservationManager.ReleaseAllClaimedBy(victim);
                    victim.Map.pawnDestinationReservationManager.ReleaseAllClaimedBy(victim);
                }

                Log.Message($"[Sheldon_Utils] {initiator.LabelShort} выгоняет {victim.LabelShort} со своего места!");

                // Увеличиваем задержку перед следующим заданием для SocialRelax
                if (victim.jobs.curJob?.def.defName == "SocialRelax")
                {
                    victim.stances.stunner.StunFor(90, initiator); // 1.5 секунды вместо 0.5
                }
                else
                {
                    victim.stances.stunner.StunFor(30, initiator); // 0.5 секунды
                }
            }
        }

        public static bool IsChairFree(Thing chair, Pawn pawn)
        {
            foreach (var pos in chair.OccupiedRect())
            {
                if (pawn.CanReserveSittableOrSpot(pos)
                    && !pos.GetThingList(chair.Map).OfType<Pawn>().Any(p => p != pawn))
                {
                    return true;
                }
            }
            return false;
        }

        // Проверяет, запрещен ли стул для данной пешки из-за недавнего выселения
        public static bool IsChairForbiddenDueToEviction(Pawn pawn, Thing chair)
        {
            if (!recentEvictions.ContainsKey(pawn))
                return false;

            // Проверяем, не истек ли кулдаун
            if (evictionCooldowns.TryGetValue(pawn, out int cooldownTick))
            {
                if (Find.TickManager.TicksGame >= cooldownTick)
                {
                    // Кулдаун истек - очищаем ограничения
                    recentEvictions.Remove(pawn);
                    evictionCooldowns.Remove(pawn);
                    // Log.Message($"[Sheldon_Utils] Кулдаун для {pawn.LabelShort} истек, очищаем ограничения");
                    return false;
                }
            }

            bool isForbidden = recentEvictions[pawn].Contains(chair);
            if (isForbidden)
            {
                Log.Message($"[Sheldon_Utils] Стул {chair.LabelShort} запрещен для {pawn.LabelShort} из-за недавнего выселения");
            }
            return isForbidden;
        }

        // Очищает устаревшие записи (вызывается периодически)
        public static void CleanupExpiredEvictions()
        {
            var currentTick = Find.TickManager.TicksGame;
            var expiredPawns = new List<Pawn>();

            foreach (var kvp in evictionCooldowns)
            {
                if (currentTick >= kvp.Value)
                {
                    expiredPawns.Add(kvp.Key);
                }
            }

            foreach (var pawn in expiredPawns)
            {
                recentEvictions.Remove(pawn);
                evictionCooldowns.Remove(pawn);
            }
        }
    }

    public static class Sheldon_SeatFinder
    {
        public struct SeatSearchResult
        {
            public Thing chair;
            public IntVec3 position;
            public Thing table;
            public bool isOwnSeat;
            public bool needsEviction;
            public Pawn occupant;

            public bool IsValid => chair != null && position.IsValid;
        }

        /// <summary>
        /// Универсальный поиск места для клона Шелдона
        /// </summary>
        public static SeatSearchResult FindSeatForSheldonClone(Pawn pawn, bool requireTable = true)
        {
            Log.Message($"[Debug] Ищем место для {pawn.LabelShort}");

            // Ищем свое назначенное место
            var myComp = pawn.Map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building != null && b.def.building.isSittable)
                .Select(b => b.TryGetComp<CompSheldonSeatAssignable>())
                .FirstOrDefault(c => c != null && c.AssignedAnything(pawn));

            if (myComp != null)
            {
                Log.Message($"[Debug] Найдено собственное место для {pawn.LabelShort}");
                IntVec3 myPosition = myComp.GetAssignedPosition(pawn);
                if (myPosition.IsValid)
                {
                    var result = ValidateOwnSeat(pawn, myComp, myPosition, requireTable);
                    if (result.IsValid)
                    Log.Message($"[Debug] Результат поиска альтернативы: {result.IsValid}");
                    return result;
                }
            }

            // Если своего места нет или оно недоступно, ищем альтернативы
            return FindAlternativeSeat(pawn, requireTable);
        }

        /// <summary>
        /// Поиск места для обычной пешки
        /// </summary>
        public static SeatSearchResult FindSeatForRegularPawn(Pawn pawn, bool requireTable = true)
        {
            return FindAlternativeSeat(pawn, requireTable);
        }

        private static SeatSearchResult ValidateOwnSeat(Pawn pawn, CompSheldonSeatAssignable comp, IntVec3 position, bool requireTable)
        {
            var result = new SeatSearchResult
            {
                chair = comp.parent,
                position = position,
                isOwnSeat = true
            };

            // Проверяем валидность позиции
            if (!IsValidChairPosition(comp.parent, pawn, position, requireTable))
            {
                return new SeatSearchResult(); // Невалидно
            }

            // Находим стол, если требуется
            if (requireTable)
            {
                result.table = FindAdjacentTable(comp.parent, position);
                if (result.table == null)
                    return new SeatSearchResult(); // Нет стола
            }

            // Проверяем, свободна ли позиция
            if (IsSpecificPositionFreeForOwner(position, pawn, comp))
            {
                result.needsEviction = false;
                return result;
            }

            // Позиция занята - нужно выселение
            result.needsEviction = true;
            result.occupant = GetOccupantAtPositionForOwner(position, pawn, comp);
            return result;
        }

        private static SeatSearchResult FindAlternativeSeat(Pawn pawn, bool requireTable)
        {
            var chairsWithFreePositions = new List<(Thing chair, IntVec3 position)>();

            // Ищем среди стульев с CompSheldonSeatAssignable
            var sheldonChairs = pawn.Map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building != null && b.def.building.isSittable)
                .Where(b => b.TryGetComp<CompSheldonSeatAssignable>() != null)
                .Where(c => !Sheldon_Utils.IsChairForbiddenDueToEviction(pawn, c))
                .Where(c => IsValidChair(c, pawn, requireTable));

            foreach (var chair in sheldonChairs)
            {
                var comp = chair.TryGetComp<CompSheldonSeatAssignable>();
                foreach (var cell in chair.OccupiedRect())
                {
                    bool isPositionFree = comp.IsPositionFree(cell) &&
                                         pawn.CanReserveSittableOrSpot(cell) &&
                                         !cell.GetThingList(pawn.Map).OfType<Pawn>().Any(p => p != pawn);

                    if (isPositionFree)
                    {
                        chairsWithFreePositions.Add((chair, cell));
                    }
                }
            }

            // ИСПРАВЛЕНИЕ: Если среди Sheldon-стульев ничего не найдено, 
            // ищем среди ВСЕХ обычных стульев на карте
            if (chairsWithFreePositions.Count == 0)
            {
                Log.Message($"[Debug] Для {pawn.LabelShort} не найдено свободных Sheldon-стульев, ищем среди обычных");

                var allChairs = pawn.Map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.building != null && b.def.building.isSittable)
                    .Where(c => !Sheldon_Utils.IsChairForbiddenDueToEviction(pawn, c))
                    .Where(c => IsValidChair(c, pawn, requireTable));

                foreach (var chair in allChairs)
                {
                    foreach (var cell in chair.OccupiedRect())
                    {
                        bool isPositionFree = pawn.CanReserveSittableOrSpot(cell) &&
                                             !cell.GetThingList(pawn.Map).OfType<Pawn>().Any(p => p != pawn);

                        if (isPositionFree)
                        {
                            chairsWithFreePositions.Add((chair, cell));
                        }
                    }
                }
            }

            // Сначала ищем среди неназначенных стульев
            var freeUnassignedChairs = chairsWithFreePositions
                .Where(cp => {
                    var comp = cp.chair.TryGetComp<CompSheldonSeatAssignable>();
                    return comp == null || comp.AssignedPawnsForReading.Count == 0;
                })
                .OrderBy(cp => cp.chair.Position.DistanceTo(pawn.Position))
                .ThenBy(cp => cp.position.x)
                .ThenBy(cp => cp.position.z);

            var targetChairPosition = freeUnassignedChairs.FirstOrDefault();
            if (targetChairPosition.chair != null)
            {
                Log.Message($"[Debug] Найден неназначенный стул для {pawn.LabelShort}: {targetChairPosition.chair.LabelShort}");
                return CreateResult(targetChairPosition.chair, targetChairPosition.position, requireTable);
            }

            // Если нет свободных неназначенных, ищем любые свободные позиции
            var anyFreeChairs = chairsWithFreePositions
                .OrderBy(cp => cp.chair.Position.DistanceTo(pawn.Position))
                .ThenBy(cp => cp.position.x)
                .ThenBy(cp => cp.position.z);

            targetChairPosition = anyFreeChairs.FirstOrDefault();
            if (targetChairPosition.chair != null)
            {
                Log.Message($"[Debug] Найден любой свободный стул для {pawn.LabelShort}: {targetChairPosition.chair.LabelShort}");
                return CreateResult(targetChairPosition.chair, targetChairPosition.position, requireTable);
            }

            Log.Message($"[Debug] Для {pawn.LabelShort} не найдено ни одного подходящего стула");
            return new SeatSearchResult(); // Ничего не найдено
        }

        private static SeatSearchResult CreateResult(Thing chair, IntVec3 position, bool requireTable)
        {
            var result = new SeatSearchResult
            {
                chair = chair,
                position = position,
                isOwnSeat = false,
                needsEviction = false
            };

            if (requireTable)
            {
                result.table = FindAdjacentTable(chair, position);
                if (result.table == null)
                    return new SeatSearchResult(); // Нет стола
            }

            return result;
        }

        private static bool IsValidChairPosition(Thing chair, Pawn pawn, IntVec3 position, bool requireTable)
        {
            if (chair.def.building == null || !chair.def.building.isSittable)
                return false;

            if (chair.IsForbidden(pawn))
                return false;

            if (pawn.IsColonist && position.Fogged(chair.Map))
                return false;

            if (!chair.IsSociallyProper(pawn))
                return false;

            if (chair.IsBurning())
                return false;

            if (chair.HostileTo(pawn))
                return false;

            if (requireTable)
            {
                var table = FindAdjacentTable(chair, position);
                if (table == null)
                    return false;
            }

            return true;
        }

        private static bool IsValidChair(Thing chair, Pawn pawn, bool requireTable)
        {
            if (chair.def.building == null || !chair.def.building.isSittable)
                return false;

            if (chair.IsForbidden(pawn))
                return false;

            if (pawn.IsColonist && chair.Position.Fogged(chair.Map))
                return false;

            if (!pawn.CanReserve(chair))
                return false;

            if (!chair.IsSociallyProper(pawn))
                return false;

            if (chair.IsBurning())
                return false;

            if (chair.HostileTo(pawn))
                return false;

            if (requireTable)
            {
                // Проверяем наличие стола рядом с любой позицией стула
                foreach (var cell in chair.OccupiedRect())
                {
                    var table = FindAdjacentTable(chair, cell);
                    if (table != null)
                        return true;
                }
                return false;
            }

            return true;
        }

        private static bool IsSpecificPositionFreeForOwner(IntVec3 position, Pawn owner, CompSheldonSeatAssignable comp)
        {
            if (!comp.IsPositionFree(position))
                return false;

            var occupants = position.GetThingList(comp.parent.Map).OfType<Pawn>().Where(p => p != owner);
            return !occupants.Any();
        }

        private static Pawn GetOccupantAtPositionForOwner(IntVec3 position, Pawn owner, CompSheldonSeatAssignable comp)
        {
            foreach (var assignedPawn in comp.AssignedPawnsForReading)
            {
                if (assignedPawn != owner && comp.GetAssignedPosition(assignedPawn) == position)
                {
                    var physicalOccupant = position.GetThingList(comp.parent.Map).OfType<Pawn>().FirstOrDefault(p => p != owner);
                    return physicalOccupant ?? assignedPawn;
                }
            }

            return position.GetThingList(comp.parent.Map).OfType<Pawn>().FirstOrDefault(p => p != owner);
        }

        private static Thing FindAdjacentTable(Thing chair, IntVec3 position)
        {
            foreach (var dir in GenAdj.CardinalDirections)
            {
                var edifice = (position + dir).GetEdifice(chair.Map);
                if (edifice != null && edifice.def.surfaceType == SurfaceType.Eat)
                    return edifice;
            }
            return null;
        }
    }
}