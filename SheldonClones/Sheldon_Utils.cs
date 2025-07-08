using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    public static class Sheldon_Utils
    {
        private static readonly Dictionary<Pawn, HashSet<Thing>> recentEvictions = new Dictionary<Pawn, HashSet<Thing>>(); // Словарь для отслеживания недавно выселенных пешек и мест, с которых их выселили
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

        // ═══ СИСТЕМА ПРИОРИТЕТОВ МЕБЕЛИ НА ОСНОВЕ СТАТИСТИК ═══

        /// <summary>
        /// Определяет базовый приоритет типа мебели на основе её характеристик
        /// </summary>
        public static int GetFurniturePriority(Thing furniture)
        {
            if (furniture?.def == null) return 0;

            var def = furniture.def;
            int priority = 100; // Базовый приоритет

            // Проверяем, является ли объект сидячим местом
            if (def.building == null || !def.building.isSittable)
                return 0;

            // Получаем базовые статистики из ThingDef
            float baseComfort = def.GetStatValueAbstract(StatDefOf.Comfort);
            float baseBeauty = def.GetStatValueAbstract(StatDefOf.Beauty);

            // Приоритет на основе комфорта (основной фактор для Шелдона)
            // Комфорт обычно от 0.5 до 0.85, умножаем на 1000 для значимости
            priority += (int)(baseComfort * 1000);

            // Дополнительный приоритет на основе красоты
            // Красота может быть от 0 до 15+, добавляем как есть
            priority += (int)(baseBeauty * 10);

            // Особые бонусы для определенных типов мебели
            if (def.building.multiSittable) // Диваны и скамьи
            {
                priority += 200; // Бонус за возможность сидеть нескольким людям
            }

            // Бонус за размер (большая мебель обычно комфортнее)
            int furnitureSize = def.size.x * def.size.z;
            if (furnitureSize > 1)
            {
                priority += furnitureSize * 50;
            }

            // Штраф за низкую прочность (хлипкая мебель)
            float maxHitPoints = def.GetStatValueAbstract(StatDefOf.MaxHitPoints);
            if (maxHitPoints < 80)
            {
                priority -= 100;
            }

            // Дополнительные проверки по defName для особых случаев
            string defName = def.defName.ToLower();

            // Особые бонусы для премиальной мебели
            if (defName.Contains("royal") || defName.Contains("throne"))
            {
                priority += 500;
            }
            else if (defName.Contains("luxury") || defName.Contains("masterwork"))
            {
                priority += 300;
            }

            return priority;
        }

        /// <summary>
        /// Проверяет, может ли пешка смотреть телевизор с данного места
        /// </summary>
        public static bool CanWatchTVFromPosition(IntVec3 position, Map map, Thing chair = null)
        {
            // Ищем телевизоры в радиусе просмотра
            var allTVs = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building != null &&
                           b.def.building.joyKind?.defName == "Television" &&
                           !b.IsBrokenDown() &&
                           b.TryGetComp<CompPowerTrader>()?.PowerOn != false);

            foreach (var tv in allTVs)
            {
                // Проверяем расстояние до телевизора
                float distance = position.DistanceTo(tv.Position);

                // Получаем максимальную дистанцию просмотра из ThingDef телевизора
                float maxWatchDistance = tv.def.building.watchBuildingStandDistanceRange.max;

                if (distance <= maxWatchDistance)
                {
                    // Проверяем, что между позицией и телевизором нет препятствий
                    if (GenSight.LineOfSight(position, tv.Position, map))
                    {
                        // Проверяем, что позиция находится перед телевизором (не сзади)
                        if (IsInFrontOfTV(position, tv))
                        {
                            // Если передан стул, проверяем его ориентацию
                            if (chair != null)
                            {
                                return IsChairOrientedForTV(chair, tv);
                            }
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Проверяет, находится ли позиция перед телевизором (учитывая его поворот)
        /// </summary>
        private static bool IsInFrontOfTV(IntVec3 position, Thing tv)
        {
            var tvRotation = tv.Rotation;
            var directionToViewer = (position - tv.Position).ToVector3().normalized;
            var tvFacingDirection = tvRotation.FacingCell.ToVector3().normalized;

            // Проверяем, что угол между направлением телевизора и направлением к зрителю меньше 90 градусов
            float dotProduct = Vector3.Dot(tvFacingDirection, directionToViewer);
            return dotProduct > 0f;
        }

        // <summary>
        /// Проверяет, правильно ли ориентирован стул для просмотра телевизора
        /// </summary>
        private static bool IsChairOrientedForTV(Thing chair, Thing tv)
        {
            // Если стул не поворачивается, считаем что он подходит
            if (!chair.def.rotatable)
                return true;

            // Получаем направление, куда "смотрит" стул
            var chairFacingDirection = chair.Rotation.FacingCell.ToVector3().normalized;

            // Получаем направление от стула к телевизору
            var directionToTV = (tv.Position - chair.Position).ToVector3().normalized;

            // Проверяем, что стул повернут в сторону телевизора (угол меньше 45 градусов)
            float dotProduct = Vector3.Dot(chairFacingDirection, directionToTV);
            return dotProduct > 0.707f; // cos(45°) ≈ 0.707
        }

        /// <summary>
        /// Подсчитывает количество телевизоров, которые можно смотреть с данной позиции
        /// </summary>
        public static int CountViewableTVsFromPosition(IntVec3 position, Map map, Thing chair = null)
        {
            int count = 0;

            var allTVs = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building != null &&
                           b.def.building.joyKind?.defName == "Television" &&
                           !b.IsBrokenDown() &&
                           b.TryGetComp<CompPowerTrader>()?.PowerOn != false);

            foreach (var tv in allTVs)
            {
                float distance = position.DistanceTo(tv.Position);

                float maxWatchDistance = tv.def.building.watchBuildingStandDistanceRange.max;

                if (distance <= maxWatchDistance &&
                    GenSight.LineOfSight(position, tv.Position, map) &&
                    IsInFrontOfTV(position, tv))
                {
                    // Если передан стул, проверяем его ориентацию
                    if (chair != null)
                    {
                        if (IsChairOrientedForTV(chair, tv))
                        {
                            count++;
                        }
                    }
                    else
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        // <summary>
        /// Получает качество предмета как числовое значение для сравнения
        /// </summary>
        public static int GetQualityValue(Thing thing)
        {
            var qualityComp = thing.TryGetComp<CompQuality>();
            if (qualityComp != null)
            {
                return (int)qualityComp.Quality;
            }
            return (int)QualityCategory.Normal; // Если качества нет, считаем нормальным
        }

        /// <summary>
        /// Получает значение материала для мебели (некоторые материалы лучше других)
        /// </summary>
        public static float GetMaterialValue(Thing thing)
        {
            if (thing.Stuff == null) return 1f;

            // Получаем множитель красоты от материала
            float beautyFactor = thing.Stuff.GetStatValueAbstract(StatDefOf.Beauty);

            // Получаем множитель комфорта от материала (если есть)
            float comfortFactor = 1f;
            if (thing.Stuff.stuffProps != null)
            {
                comfortFactor = thing.Stuff.stuffProps.statFactors
                    ?.FirstOrDefault(f => f.stat == StatDefOf.Comfort)?.value ?? 1f;
            }

            // Возвращаем комбинированный бонус от материала
            return Math.Max(beautyFactor * 0.1f, comfortFactor);
        }

        /// <summary>
        /// Вычисляет общий рейтинг мебели для клонов Шелдона
        /// </summary>
        public static float CalculateFurnitureRating(Thing furniture)
        {
            if (furniture?.def == null) return 0f;

            // Базовый приоритет на основе типа мебели и её характеристик
            float rating = GetFurniturePriority(furniture);

            // Добавляем бонус за качество (качество умножается на 50 для значимости)
            rating += GetQualityValue(furniture) * 50;

            // Получаем актуальные статистики предмета (с учетом материала и качества)
            float actualComfort = furniture.GetStatValue(StatDefOf.Comfort);
            float actualBeauty = furniture.GetStatValue(StatDefOf.Beauty);

            // Дополнительный бонус за реальный комфорт
            rating += actualComfort * 200;

            // Дополнительный бонус за реальную красоту
            rating += actualBeauty * 5;

            // Бонус за материал
            rating += GetMaterialValue(furniture) * 30;

            // Небольшой штраф за износ
            float healthPercent = (float)furniture.HitPoints / furniture.MaxHitPoints;
            if (healthPercent < 0.8f)
            {
                rating *= healthPercent; // Чем больше изношена мебель, тем меньше рейтинг
            }

            // Дополнительная проверка на стиль мебели (если есть мод Ideology)
            var styleComp = furniture.TryGetComp<CompStyleable>();
            if (styleComp != null && styleComp.SourcePrecept != null)
            {
                // Небольшой бонус за стильную мебель
                rating += 25;
            }

            // Проверяем каждую позицию мебели на возможность просмотра ТВ
            bool canWatchTV = false;
            int totalTVsViewable = 0;
            int properlyOrientedTVs = 0; // Новая переменная для подсчета правильно ориентированных ТВ

            foreach (var cell in furniture.OccupiedRect())
            {
                if (CanWatchTVFromPosition(cell, furniture.Map))
                {
                    canWatchTV = true;
                    totalTVsViewable += CountViewableTVsFromPosition(cell, furniture.Map);

                    // Подсчитываем ТВ с правильной ориентацией стула
                    properlyOrientedTVs += CountViewableTVsFromPosition(cell, furniture.Map, furniture);
                }
            }

            if (canWatchTV)
            {
                // Базовый бонус за возможность просмотра ТВ
                rating += 1000;

                // Дополнительный бонус за каждый доступный телевизор
                rating += totalTVsViewable * 100;

                // БОЛЬШОЙ бонус за правильную ориентацию стула к ТВ
                rating += properlyOrientedTVs * 500;

                Log.Message($"[Sheldon_Utils] Мебель {furniture.LabelShort} получает TV бонус: +{1000 + totalTVsViewable * 100 + properlyOrientedTVs * 500} (базовый +1000, за {totalTVsViewable} ТВ +{totalTVsViewable * 100}, за правильную ориентацию к {properlyOrientedTVs} ТВ +{properlyOrientedTVs * 500})");
            }

            return rating;
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
            var chairsWithFreePositions = new List<(Thing chair, IntVec3 position, float rating)>();

            // Ищем среди стульев с CompSheldonSeatAssignable
            var sheldonChairs = pawn.Map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building != null && b.def.building.isSittable)
                .Where(c => !Sheldon_Utils.IsChairForbiddenDueToEviction(pawn, c))
                .Where(c => IsValidChair(c, pawn, requireTable));

            foreach (var chair in sheldonChairs)
            {
                var comp = chair.TryGetComp<CompSheldonSeatAssignable>();
                float chairRating = Sheldon_Utils.CalculateFurnitureRating(chair);

                foreach (var cell in chair.OccupiedRect())
                {
                    bool isPositionFree;

                    if (comp != null)
                    {
                        // Для стульев с компонентом используем его логику
                        isPositionFree = comp.IsPositionFree(cell) &&
                                       pawn.CanReserveSittableOrSpot(cell) &&
                                       !cell.GetThingList(pawn.Map).OfType<Pawn>().Any(p => p != pawn);
                    }
                    else
                    {
                        // Для обычных стульев используем стандартную проверку
                        isPositionFree = pawn.CanReserveSittableOrSpot(cell) &&
                                       !cell.GetThingList(pawn.Map).OfType<Pawn>().Any(p => p != pawn);
                    }

                    if (isPositionFree)
                    {
                        chairsWithFreePositions.Add((chair, cell, chairRating));
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
                    float chairRating = Sheldon_Utils.CalculateFurnitureRating(chair);

                    foreach (var cell in chair.OccupiedRect())
                    {
                        bool isPositionFree = pawn.CanReserveSittableOrSpot(cell) &&
                                             !cell.GetThingList(pawn.Map).OfType<Pawn>().Any(p => p != pawn);

                        if (isPositionFree)
                        {
                            chairsWithFreePositions.Add((chair, cell, chairRating));
                        }
                    }
                }
            }

            // ═══ НОВАЯ ЛОГИКА СОРТИРОВКИ ПО ПРИОРИТЕТАМ ═══

            // Для клонов Шелдона используем приоритетную сортировку
            if (pawn.def == AlienDefOf.SheldonClone)
            {
                // Сначала ищем среди неназначенных стульев, отсортированных по рейтингу
                var freeUnassignedChairs = chairsWithFreePositions
                    .Where(cp => {
                        var comp = cp.chair.TryGetComp<CompSheldonSeatAssignable>();
                        return comp == null || comp.AssignedPawnsForReading.Count == 0;
                    })
                    .OrderByDescending(cp => cp.rating) // Сначала лучшие по рейтингу
                    .ThenBy(cp => cp.chair.Position.DistanceTo(pawn.Position)) // Потом по расстоянию
                    .ThenBy(cp => cp.position.x)
                    .ThenBy(cp => cp.position.z);

                var targetChairPosition = freeUnassignedChairs.FirstOrDefault();
                if (targetChairPosition.chair != null)
                {
                    Log.Message($"[Debug] Найден лучший неназначенный стул для {pawn.LabelShort}: {targetChairPosition.chair.LabelShort} (рейтинг: {targetChairPosition.rating})");
                    return CreateResult(targetChairPosition.chair, targetChairPosition.position, requireTable);
                }

                // Если нет свободных неназначенных, ищем любые свободные позиции по рейтингу
                var anyFreeChairs = chairsWithFreePositions
                    .OrderByDescending(cp => cp.rating) // Сначала лучшие по рейтингу
                    .ThenBy(cp => cp.chair.Position.DistanceTo(pawn.Position)) // Потом по расстоянию
                    .ThenBy(cp => cp.position.x)
                    .ThenBy(cp => cp.position.z);

                targetChairPosition = anyFreeChairs.FirstOrDefault();
                if (targetChairPosition.chair != null)
                {
                    Log.Message($"[Debug] Найден лучший свободный стул для {pawn.LabelShort}: {targetChairPosition.chair.LabelShort} (рейтинг: {targetChairPosition.rating})");
                    return CreateResult(targetChairPosition.chair, targetChairPosition.position, requireTable);
                }
            }
            else
            {
                // Для обычных пешек используем старую логику (по близости)
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