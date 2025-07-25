using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SheldonClones
{
    public class CompSheldonSeatAssignable : CompAssignableToPawn
    {
        // Словарь для запоминания конкретных позиций каждого клона
        private Dictionary<Pawn, IntVec3> assignedPositions = new Dictionary<Pawn, IntVec3>();

        // Временные поля для загрузки
        private List<int> tempLoadedPawnIds;
        private List<IntVec3> tempLoadedPositions;

        // Разрешаем назначать только клонам Шелдона
        public override AcceptanceReport CanAssignTo(Pawn pawn)
        {
            if (pawn.def != AlienDefOf.SheldonClone)
                return new AcceptanceReport("NotSheldonClone");

            // Проверяем фракцию
            if (!CanPawnUseFurniture(pawn))
                return new AcceptanceReport("WrongFaction");


            // Проверяем, не назначен ли уже этот пешка на этот объект
            if (AssignedPawnsForReading.Contains(pawn))
                return new AcceptanceReport("AlreadyAssignedToThis");

            // Проверяем, есть ли свободные места
            if (!HasFreeSlot)
                return new AcceptanceReport("No free slots");

            return AcceptanceReport.WasAccepted;
        }

        // Метод для проверки прав на мебель
        private bool CanPawnUseFurniture(Pawn pawn)
        {
            // Если мебель принадлежит игроку (колонии)
            if (parent.Faction != null && parent.Faction.IsPlayer)
            {
                // Только поселенцы игрока могут использовать мебель игрока
                bool canUse = pawn.IsColonist;
                Log.Message($"[SheldonClones] Мебель игрока {parent.LabelShort}: {pawn.LabelShort} (IsColonist: {pawn.IsColonist}) -> {canUse}");
                return canUse;
            }

            // Если мебель принадлежит другой фракции
            if (parent.Faction != null)
            {
                // Только члены той же фракции могут использовать мебель
                bool canUse = pawn.Faction == parent.Faction;
                Log.Message($"[SheldonClones] Мебель фракции {parent.Faction.Name}: {pawn.LabelShort} (фракция: {pawn.Faction?.Name ?? "нет"}) -> {canUse}");
                return canUse;
            }

            // Если мебель без фракции - может использовать любой клон
            Log.Message($"[SheldonClones] Мебель без фракции {parent.LabelShort}: {pawn.LabelShort} -> true");
            return true;
        }

        // Кандидатами на назначение считаем только клонов Шелдона
        public override IEnumerable<Pawn> AssigningCandidates
        {
            get
            {
                if (!parent.Spawned)
                    yield break;

                // Получаем всех пешек на карте, принадлежащих к той же фракции
                foreach (var p in parent.Map.mapPawns.AllPawns)
                {
                    if (p.def == AlienDefOf.SheldonClone &&
                        CanPawnUseFurniture(p) &&
                        !AssignedPawnsForReading.Contains(p))
                        yield return p;
                }
            }
        }

        // Метод для проверки близости к рабочим станциям
        private bool IsNearWorkBenchArea()
        {
            if (parent?.Map == null) return false;

            Map map = parent.Map;

            // Собираем все клетки, занимаемые мебелью + соседние
            HashSet<IntVec3> cellsToCheck = new HashSet<IntVec3>();

            // Добавляем клетки самой мебели
            foreach (var cell in parent.OccupiedRect())
            {
                cellsToCheck.Add(cell);
            }

            // Добавляем соседние клетки (8 направлений)
            foreach (var cell in parent.OccupiedRect())
            {
                for (int i = 0; i < 8; i++)
                {
                    IntVec3 adjacent = cell + GenAdj.AdjacentCells[i];
                    if (adjacent.InBounds(map))
                        cellsToCheck.Add(adjacent);
                }
            }

            foreach (var cell in cellsToCheck)
            {
                var things = cell.GetThingList(map);
                foreach (var thing in things)
                {
                    if (IsWorkBench(thing))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsWorkBench(Thing thing)
        {
            if (thing == null || thing.def == null) return false;

            // Проверяем по тегам зданий (основной способ)
            if (thing.def.building?.buildingTags != null)
            {
                var buildingTags = thing.def.building.buildingTags;

                // Проверяем тег на наличие "Production"
                var workBenchTags = new HashSet<string> { "Production" };

                foreach (string tag in buildingTags)
                {
                    if (workBenchTags.Contains(tag))
                    {
                        Log.Message($"[SheldonClones] Найден рабочий объект {thing.def.defName} с тегом {tag}");
                        return true;
                    }
                }
            }

            if (thing.def.hasInteractionCell)
            {
                // Если имеет место взаимодействия - вероятно рабочая станция
                Log.Message($"[SheldonClones] Найден рабочий объект поблизости {thing.def.defName}");
                return true;
            }

            return false;
        }


        // Переопределяем методы назначения для работы с позициями
        public override void TryAssignPawn(Pawn pawn)
        {
            // Проверяем права на мебель
            if (!CanPawnUseFurniture(pawn))
                return;

            // Если пешка уже назначена, не делаем ничего
            if (AssignedPawnsForReading.Contains(pawn))
                return;

            if (IsNearWorkBenchArea())
            {
                Log.Message($"[SheldonClones] {parent.LabelShort} стоит у станка — запрещено присваивать");
                return;
            }

            // Определяем позицию для назначения
            IntVec3 targetPosition = DetermineAssignmentPosition(pawn);

            if (targetPosition.IsValid)
            {
                AssignPawnToPosition(pawn, targetPosition);
            }
        }

        // Новый приватный метод для определения позиции
        private IntVec3 DetermineAssignmentPosition(Pawn pawn)
        {
            // Определяем позицию, на которой сидит пешка
            IntVec3 currentPos = pawn.Position;

            // Проверяем, находится ли пешка на одной из клеток мебели
            if (parent.OccupiedRect().Contains(currentPos))
            {
                return currentPos;
            }

            // Если пешка не на мебели, найдем первую свободную позицию
            return FindFreePosition();
        }

        // Новый приватный метод для фактического назначения
        private void AssignPawnToPosition(Pawn pawn, IntVec3 position)
        {
            assignedPositions[pawn] = position;
            base.TryAssignPawn(pawn); // Вызываем базовый метод для корректной работы
        }


        public override void TryUnassignPawn(Pawn pawn, bool sort = true, bool uninstall = false)
        {
            assignedPositions.Remove(pawn);
            base.TryUnassignPawn(pawn, sort, uninstall);
        }

        public override void ForceRemovePawn(Pawn pawn)
        {
            assignedPositions.Remove(pawn);
            base.ForceRemovePawn(pawn);
        }

        // Метод для получения назначенной позиции пешки
        public IntVec3 GetAssignedPosition(Pawn pawn)
        {
            return assignedPositions.TryGetValue(pawn, out IntVec3 pos) ? pos : IntVec3.Invalid;
        }

        // Метод для проверки, свободна ли конкретная позиция
        public bool IsPositionFree(IntVec3 position)
        {
            // Проверяем, не назначена ли эта позиция кому-то другому
            foreach (var kvp in assignedPositions)
            {
                if (kvp.Value == position && kvp.Key != null && !kvp.Key.Dead)
                    return false;
            }
            return true;
        }

        // Метод для поиска свободной позиции на мебели
        private IntVec3 FindFreePosition()
        {
            foreach (var cell in parent.OccupiedRect())
            {
                if (IsPositionFree(cell))
                    return cell;
            }
            return IntVec3.Invalid;
        }

        // Метод для назначения пешки на конкретную позицию (для использования в патчах)
        public void TryAssignPawnToSpecificPosition(Pawn pawn, IntVec3 position)
        {
            // Проверяем права на мебель
            if (!CanPawnUseFurniture(pawn))
            {
                Log.Message($"[SheldonClones] {pawn.LabelShort} не может использовать мебель {parent.LabelShort}");
                return;
            }

            if (IsNearWorkBenchArea())
            {
                Log.Message($"[SheldonClones] {parent.LabelShort} стоит у станка — запрещено присваивать");
                return;
            }

            if (parent.OccupiedRect().Contains(position) && IsPositionFree(position))
            {
                AssignPawnToPosition(pawn, position);
            }
        }

        public override string CompInspectStringExtra()
        {
            var assignedPawns = AssignedPawnsForReading;
            var totalSlots = TotalSlots;

            if (totalSlots <= 1)
            {
                // Для одноместной мебели
                if (!assignedPawns.Any())
                    return "Свободно";
                return "Место " + assignedPawns[0].LabelShort;
            }
            else
            {
                // Для многоместной мебели
                var lines = new List<string>();
                var occupiedPositions = new HashSet<IntVec3>(assignedPositions.Values);

                foreach (var cell in parent.OccupiedRect())
                {
                    var occupant = assignedPositions.FirstOrDefault(kvp => kvp.Value == cell);
                    if (occupant.Key != null)
                    {
                        lines.Add($"Место " + occupant.Key.LabelShort);
                    }
                    else
                    {
                        lines.Add($"Свободно");
                    }
                }

                return string.Join("\n", lines);
            }
        }


        protected override string GetAssignmentGizmoDesc()
        {
            if (TotalSlots > 1)
            {
                int occupied = AssignedPawnsForReading.Count;
                int free = TotalSlots - occupied;
                return $"Назначить клона Шелдона на это место. Занято: {occupied}/{TotalSlots}";
            }
            return "Назначить клона Шелдона на это место";
        }

        // Сохранение/загрузка данных
        public override void PostExposeData()
        {
            base.PostExposeData();

            // Инициализируем словарь если он null
            if (assignedPositions == null)
                assignedPositions = new Dictionary<Pawn, IntVec3>();

            // Сохранение
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // Очищаем словарь от мертвых/null пешек перед сохранением
                var toRemove = assignedPositions.Keys.Where(p => p == null || p.Dead || p.Destroyed).ToList();
                foreach (var pawn in toRemove)
                {
                    assignedPositions.Remove(pawn);
                }

                // Сохраняем ID пешек и позиции
                var pawnIds = assignedPositions.Keys.Select(p => p.thingIDNumber).ToList();
                var positions = assignedPositions.Values.ToList();

                Scribe_Collections.Look(ref pawnIds, "assignedPawnIds", LookMode.Value);
                Scribe_Collections.Look(ref positions, "assignedPositionsValues", LookMode.Value);

                // Log.Message($"[SheldonClones] Сохранено {pawnIds.Count} назначений для {parent.LabelShort}");
            }
            // Загрузка
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<int> pawnIds = null;
                List<IntVec3> positions = null;

                Scribe_Collections.Look(ref pawnIds, "assignedPawnIds", LookMode.Value);
                Scribe_Collections.Look(ref positions, "assignedPositionsValues", LookMode.Value);

                // Сохраняем для восстановления в PostSpawnSetup
                tempLoadedPawnIds = pawnIds ?? new List<int>();
                tempLoadedPositions = positions ?? new List<IntVec3>();

                // Log.Message($"[SheldonClones] Загружено {tempLoadedPawnIds.Count} ID пешек для {parent.LabelShort}");
            }
        }

        // Метод для дополнительной безопасности
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            // Инициализируем словарь если он null
            if (assignedPositions == null)
                assignedPositions = new Dictionary<Pawn, IntVec3>();

            // После загрузки восстанавливаем данные
            if (respawningAfterLoad)
            {
                RestorePositionsFromIds();
                ValidateAssignedPositions();
            }
        }

        // Воостанавливаем позицию клона по ID
        private void RestorePositionsFromIds()
        {
            if (tempLoadedPawnIds == null || tempLoadedPositions == null)
                return;

            assignedPositions.Clear();

            if (tempLoadedPawnIds.Count > 0 && tempLoadedPositions.Count > 0)
            {
                int minCount = Mathf.Min(tempLoadedPawnIds.Count, tempLoadedPositions.Count);
                var assignedPawns = AssignedPawnsForReading;

                for (int i = 0; i < minCount; i++)
                {
                    // Ищем пешку по ID среди назначенных
                    var pawn = assignedPawns.FirstOrDefault(p => p.thingIDNumber == tempLoadedPawnIds[i]);

                    if (pawn != null && !pawn.Dead && !pawn.Destroyed)
                    {
                        // Проверяем, что позиция все еще валидна
                        if (parent.OccupiedRect().Contains(tempLoadedPositions[i]))
                        {
                            assignedPositions[pawn] = tempLoadedPositions[i];
                            // Log.Message($"[SheldonClones] Восстановлено: {pawn.LabelShort} -> {tempLoadedPositions[i]}");
                        }
                    }
                    else
                    {
                        // Log.Message($"[SheldonClones] Пешка с ID {tempLoadedPawnIds[i]} не найдена среди назначенных");
                    }
                }
            }
            // Очищаем временные данные
            tempLoadedPawnIds = null;
            tempLoadedPositions = null;
        }

        // Метод для проверки корректности назначенных позиций
        private void ValidateAssignedPositions()
        {
            if (assignedPositions == null)
            {
                Log.Message($"[SheldonClones] ValidateAssignedPositions: assignedPositions is null for {parent.LabelShort}");
                return;
            }

            // Log.Message($"[SheldonClones] ValidateAssignedPositions: проверяем {assignedPositions.Count} назначений для {parent.LabelShort}");

            var invalidEntries = new List<Pawn>();

            foreach (var kvp in assignedPositions)
            {
                // Проверяем, что пешка существует и жива
                if (kvp.Key == null)
                {
                    Log.Warning($"[SheldonClones] Найдена null пешка в assignedPositions для {parent.LabelShort}");
                    invalidEntries.Add(kvp.Key);
                    continue;
                }

                if (kvp.Key.Dead || kvp.Key.Destroyed)
                {
                    Log.Message($"[SheldonClones] Пешка {kvp.Key.LabelShort} мертва/уничтожена, удаляем из assignedPositions");
                    invalidEntries.Add(kvp.Key);
                    continue;
                }

                // Проверяем, что позиция находится в пределах мебели
                if (!parent.OccupiedRect().Contains(kvp.Value))
                {
                    Log.Warning($"[SheldonClones] Позиция {kvp.Value} для {kvp.Key.LabelShort} вне пределов {parent.LabelShort}");
                    invalidEntries.Add(kvp.Key);
                    continue;
                }

                // Проверяем, что пешка действительно назначена в базовом компоненте
                if (!AssignedPawnsForReading.Contains(kvp.Key))
                {
                    Log.Warning($"[SheldonClones] Пешка {kvp.Key.LabelShort} есть в assignedPositions, но нет в AssignedPawnsForReading");
                    invalidEntries.Add(kvp.Key);
                    continue;
                }

                // Log.Message($"[SheldonClones] Валидное назначение: {kvp.Key.LabelShort} -> {kvp.Value}");
            }

            // Удаляем неверные записи
            foreach (var pawn in invalidEntries)
            {
                assignedPositions.Remove(pawn);
            }

            // Log.Message($"[SheldonClones] ValidateAssignedPositions завершена: осталось {assignedPositions.Count} валидных назначений");
        }
    }
}