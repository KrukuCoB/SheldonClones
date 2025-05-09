using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;

namespace SheldonClones
{
    public class JobDriver_SheGoToClass : JobDriver
    {
        public Pawn Sheldon => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDowned(TargetIndex.A);

            // Подойти к Шелдону
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            bool courseCompleted = false; // <- Добавили переменную

            // Ожидание у Шелдона
            Toil courseToil = new Toil();
            courseToil.initAction = () =>
            {
                Pawn pawn = courseToil.actor;
                Pawn target = (Pawn)pawn.CurJob.targetA.Thing;

                // Прервать текущую работу у цели
                if (target.CurJob != null)
                {
                    target.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }

                // Заставить цель просто стоять
                target.jobs.StartJob(
                    JobMaker.MakeJob(JobDefOf.Wait, 1800),
                    JobCondition.InterruptForced,
                    null,
                    resumeCurJobAfterwards: false,
                    cancelBusyStances: true);
            };

            courseToil.defaultCompleteMode = ToilCompleteMode.Delay;
            courseToil.defaultDuration = 1800;
            courseToil.WithProgressBarToilDelay(TargetIndex.None);

            // <- Вот здесь помечаем, что курс завершён
            courseToil.AddPreTickAction(() =>
            {
                if (courseToil.actor.jobs.curDriver.ticksLeftThisToil <= 1)
                {
                    courseCompleted = true;
                }
            });

            courseToil.AddFinishAction(() =>
            {
                if (!courseCompleted) return; // <--- Проверка!

                Pawn pawn = courseToil.actor;
                Pawn target = (Pawn)pawn.CurJob.targetA.Thing;

                // Удалить страйки
                var strikes = pawn.health.hediffSet.hediffs
                    .OfType<Hediff_SheldonStrike>()
                    .Where(strike => strike.sheldonName == target.Label)
                    .ToList();

                foreach (var strike in strikes)
                    {
                        if (strike.Severity > 1)
                            {
                                 strike.Severity--;
                                 strike.Severity = strike.Severity; // Если используется для отображения уровня
                                 Messages.Message($"{pawn.LabelShort} прошёл курс у {target.LabelShort} и снял один страйк.", MessageTypeDefOf.PositiveEvent);
                                 pawn.needs.mood?.thoughts.memories.TryGainMemory(ThoughtDef.Named("SheldonCourseExhausted"));
                                 target.needs.mood?.thoughts.memories.TryGainMemory(ThoughtDef.Named("SheldonTaughtCourse"));
                    }
                        else
                            {
                                 pawn.health.RemoveHediff(strike);
                                 Messages.Message($"{pawn.LabelShort} прошёл курс у {target.LabelShort} и снял все страйки!", MessageTypeDefOf.PositiveEvent);
                                 pawn.needs.mood?.thoughts.memories.TryGainMemory(ThoughtDef.Named("SheldonCourseExhausted"));
                                 target.needs.mood?.thoughts.memories.TryGainMemory(ThoughtDef.Named("SheldonTaughtCourse"));
                            }
                    }   
            });

            yield return courseToil;
        }
    }
}
