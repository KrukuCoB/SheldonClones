using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

    namespace SheldonClones
    {
        public class ThoughtWorker_SheldonNeedsSchedule : ThoughtWorker
        {
            protected override ThoughtState CurrentStateInternal(Pawn p)
            {
                // Проверяем, является ли пешка клоном Шелдона
                if (p.def != AlienDefOf.SheldonClone)
                    return false;

                // Проверяем, есть ли у пешки расписание
                if (p.timetable == null || p.timetable.times == null)
                    return false;

                // Получаем текущее расписание пешки
                List<TimeAssignmentDef> schedule = p.timetable.times;

                // Проверяем, состоит ли расписание только из "Свободного времени"
                bool hasOnlyAnything = schedule.All(t => t == TimeAssignmentDefOf.Anything);

                // Проверяем, есть ли в расписании работа, сон и развлечения
                bool hasWork = schedule.Any(t => t == TimeAssignmentDefOf.Work);
                bool hasSleep = schedule.Any(t => t == TimeAssignmentDefOf.Sleep);
                bool hasJoy = schedule.Any(t => t == TimeAssignmentDefOf.Joy);

                // Если расписание полностью свободное — сильное беспокойство (-10)
                if (hasOnlyAnything)
                    return ThoughtState.ActiveAtStage(0);

                // Если нет работы и развлечений, но есть сон — среднее беспокойство (-8)
                if (!hasWork && !hasJoy && hasSleep)
                    return ThoughtState.ActiveAtStage(1);

                // Если нет работы, но есть развлечения — легкое беспокойство (-5)
                if (!hasWork && hasJoy)
                    return ThoughtState.ActiveAtStage(2);

                return false;
            }
        }
    }

