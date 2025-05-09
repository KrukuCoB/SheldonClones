using Verse;
using RimWorld;
using UnityEngine;
using System.Collections.Generic;

namespace SheldonClones
{
    public class CompProperties_SheldonSeatAssignable : CompProperties
    {
        public CompProperties_SheldonSeatAssignable()
        {
            this.compClass = typeof(CompSheldonSeatAssignable);
        }
    }

    public class CompSheldonSeatAssignable : CompAssignableToPawn
    {
        private List<Pawn> _cachedAssignedPawns;
        private int _lastAssignedPawnsUpdateTick;

        // Публичный метод для получения списка назначенных пешек
        public List<Pawn> GetAssignedPawns()
        {
            if (_cachedAssignedPawns == null || Find.TickManager.TicksGame > _lastAssignedPawnsUpdateTick + 60)
            {
                _cachedAssignedPawns = new List<Pawn>(assignedPawns);
                _lastAssignedPawnsUpdateTick = Find.TickManager.TicksGame;
            }
            return _cachedAssignedPawns;
        }

        // Публичный метод для добавления пешки
        public void AddAssignedPawn(Pawn pawn)
        {
            assignedPawns.Add(pawn);
        }

        // Публичный метод для удаления пешки
        public void RemoveAssignedPawn(Pawn pawn)
        {
            assignedPawns.Remove(pawn);
        }

        // Переопределяем: скрываем GUI-кнопку назначения
        protected override bool ShouldShowAssignmentGizmo()
        {
            return false; // Убираем кнопку назначения вручную. Назначение будет автоматическим.
        }

        // Разрешаем показывать метку владельца для всех пешек
        protected override bool CanDrawOverlayForPawn(Pawn pawn)
        {
            return true;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            CompSheldonSeatingManager.AddSeatToCache(this);

            // При загрузке синхронизируем данные с PawnExtensions
            if (respawningAfterLoad && assignedPawns.Count > 0)
            {
                foreach (Pawn pawn in assignedPawns)
                {
                    if (pawn != null && pawn.def == AlienDefOf.SheldonClone)
                    {
                        // Обновляем сохраненное место в расширениях пешки
                        pawn.SetReservedSittingSpot(parent.Position);
                    }
                }
            }
        }

        // Метод, который вызывается, когда клон сядет на стул
        public void AssignSheldon(Pawn pawn)
        {
            if (pawn == null || pawn.def != AlienDefOf.SheldonClone)
                return;

            if (assignedPawns.Contains(pawn))
                return; // Уже назначен

            assignedPawns.Clear(); // Сбросить старые назначения (один хозяин на стул)
            assignedPawns.Add(pawn);
            SortAssignedPawns();

            // Отмечаем это место как забронированное для данного клона
            pawn.SetReservedSittingSpot(parent.Position);
        }

        // Удаление назначения
        public void UnassignSheldon()
        {
            // Снимаем бронь со всех назначенных пешек
            foreach (Pawn pawn in assignedPawns)
            {
                if (pawn != null)
                {
                    pawn.RemoveReservedSittingSpot();
                }
            }

            assignedPawns.Clear();
        }

        // Проверка, принадлежит ли место данному клону
        public bool BelongsToSheldon(Pawn pawn)
        {
            return assignedPawns.Contains(pawn);
        }

        // Строка для интерфейса инспекции объекта
        public override string CompInspectStringExtra()
        {
            if (assignedPawns.Count == 0)
                return "Свободно";

            return "Место " + assignedPawns[0].LabelShort;
        }

        // Отрисовка метки с именем владельца
        public override void PostDraw()
        {
            base.PostDraw();

            if (assignedPawns.Count > 0 && assignedPawns[0] != null)
            {
                Pawn pawn = assignedPawns[0];

                // Рисуем метку с именем, аналогично Building_Bed
                Vector3 drawPos = parent.DrawPos;
                drawPos.y += 0.35f; // Подбираем высоту, чтобы не перекрывать самого клона

                string label = pawn.LabelShort;
                Color labelColor = new Color(1f, 0.85f, 0f); // Желтый цвет для заметности

                GenMapUI.DrawThingLabel(drawPos, label, labelColor);
            }
        }

        // При уничтожении объекта снимаем бронь
        public override void PostDeSpawn(Map map)
        {
            CompSheldonSeatingManager.RemoveSeatFromCache(this);
            UnassignSheldon();
            base.PostDeSpawn(map);
        }
    }
}