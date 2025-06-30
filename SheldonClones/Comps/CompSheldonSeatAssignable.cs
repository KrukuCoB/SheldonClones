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

        // Кандидатами на назначение считаем только свободных колонистов-клонов Шелдона
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
                Log.Message($"[SheldonClones] {pawn.LabelShort} присвоил себе позицию {currentPos} на {parent.LabelShort}");
            }
            else
            {
                // Если пешка не на мебели, найдем первую свободную позицию
                IntVec3 freePos = FindFreePosition();
                if (freePos.IsValid)
                {
                    assignedPositions[pawn] = freePos;
                    Log.Message($"[SheldonClones] {pawn.LabelShort} назначен на позицию {freePos} на {parent.LabelShort}");
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
                Log.Message($"[SheldonClones] {pawn.LabelShort} назначен на конкретную позицию {position} на {parent.LabelShort}");
            }
        }

        public override string CompInspectStringExtra()
        {
            var assignedPawns = AssignedPawnsForReading;
            var totalSlots = TotalSlots;

            if (totalSlots <= 1)
            {
                // Для одноместной мебели - старая логика
                if (!assignedPawns.Any())
                    return "Свободно";
                return "Место " + assignedPawns[0].LabelShort;
            }
            else
            {
                // Для многоместной мебели - показываем позиции
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

        public override void PostDraw()
        {
            base.PostDraw();

            // Для многоместной мебели показываем метки над каждой позицией
            if (TotalSlots > 1)
            {
                foreach (var kvp in assignedPositions)
                {
                    if (kvp.Key != null && !kvp.Key.Dead)
                    {
                        Vector3 drawPos = kvp.Value.ToVector3();
                        drawPos.y += 0.35f;
                        GenMapUI.DrawThingLabel(drawPos, kvp.Key.LabelShort, Color.yellow);
                    }
                }
                return;
            }

            // Для одноместной мебели - старая логика
            if (AssignedPawnsForReading.Any())
            {
                var pawn = AssignedPawnsForReading[0];
                Vector3 drawPos = parent.DrawPos;
                drawPos.y += 0.35f;
                GenMapUI.DrawThingLabel(drawPos, pawn.LabelShort, Color.yellow);
            }
        }

        // Сохранение/загрузка данных
        public override void PostExposeData()
        {
            base.PostExposeData();

            // Сохраняем словарь позиций
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var pawns = assignedPositions.Keys.ToList();
                var positions = assignedPositions.Values.ToList();
                Scribe_Collections.Look(ref pawns, "assignedPositionsPawns", LookMode.Reference);
                Scribe_Collections.Look(ref positions, "assignedPositionsValues", LookMode.Value);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<Pawn> pawns = null;
                List<IntVec3> positions = null;
                Scribe_Collections.Look(ref pawns, "assignedPositionsPawns", LookMode.Reference);
                Scribe_Collections.Look(ref positions, "assignedPositionsValues", LookMode.Value);

                if (pawns != null && positions != null && pawns.Count == positions.Count)
                {
                    assignedPositions.Clear();
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        if (pawns[i] != null)
                        {
                            assignedPositions[pawns[i]] = positions[i];
                        }
                    }
                }
            }

            if (assignedPositions == null)
                assignedPositions = new Dictionary<Pawn, IntVec3>();
        }
    }
}