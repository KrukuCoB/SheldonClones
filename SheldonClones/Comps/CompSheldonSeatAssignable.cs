using System.Collections.Generic;
using System.Linq;
using RimWorld;
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

            // Проверяем, не назначен ли уже этот пешка на этот объект
            if (AssignedPawnsForReading.Contains(pawn))
                return new AcceptanceReport("AlreadyAssignedToThis");

            // Проверяем, есть ли свободные места
            if (!HasFreeSlot)
                return new AcceptanceReport("No free slots");

            return AcceptanceReport.WasAccepted;
        }

        // Кандидатами на назначение считаем только клонов Шелдона
        public override IEnumerable<Pawn> AssigningCandidates
        {
            get
            {
                if (!parent.Spawned)
                    yield break;
                foreach (var p in parent.Map.mapPawns.FreeColonists)
                {
                    if (p.def == AlienDefOf.SheldonClone && !AssignedPawnsForReading.Contains(p))
                        yield return p;
                }
            }
        }

        // Переопределяем методы назначения для работы с позициями
        public override void TryAssignPawn(Pawn pawn)
        {
            // Если пешка уже назначена, не делаем ничего
            if (AssignedPawnsForReading.Contains(pawn))
                return;

            // Определяем позицию, на которой сидит пешка
            IntVec3 currentPos = pawn.Position;

            // Проверяем, находится ли пешка на одной из клеток мебели
            if (parent.OccupiedRect().Contains(currentPos))
            {
                assignedPositions[pawn] = currentPos;
                // Log.Message($"[SheldonClones] {pawn.LabelShort} присвоил себе позицию {currentPos} на {parent.LabelShort}");
            }
            else
            {
                // Если пешка не на мебели, найдем первую свободную позицию
                IntVec3 freePos = FindFreePosition();
                if (freePos.IsValid)
                {
                    assignedPositions[pawn] = freePos;
                    // Log.Message($"[SheldonClones] {pawn.LabelShort} назначен на позицию {freePos} на {parent.LabelShort}");
                }
            }

            base.TryAssignPawn(pawn);
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

        // Новый метод для получения назначенной позиции пешки
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
        public void TryAssignPawnToPosition(Pawn pawn, IntVec3 position)
        {
            if (parent.OccupiedRect().Contains(position) && IsPositionFree(position))
            {
                assignedPositions[pawn] = position;
                base.TryAssignPawn(pawn);
                // Log.Message($"[SheldonClones] {pawn.LabelShort} назначен на конкретную позицию {position} на {parent.LabelShort}");
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