using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    /// <summary>
    /// Класс для "уборочного психоза" клона: убирает вне зоны дома.
    /// Копия JobDriver_CleanFilth без проверки HomeArea.
    /// </summary>
    public class JobDriver_CleanFrenzy : JobDriver
    {

        private float cleaningWorkDone;
        private float totalCleaningWorkDone;
        private float totalCleaningWorkRequired;
        private const TargetIndex FilthInd = TargetIndex.A;
        private Filth Filth => (Filth)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(FilthInd), job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Инициализационные тоилы
            var initExtractTargetFromQueue = Toils_JobTransforms.ClearDespawnedNullOrForbiddenQueuedTargets(TargetIndex.A);
            yield return initExtractTargetFromQueue;
            yield return Toils_JobTransforms.SucceedOnNoTargetInQueue(TargetIndex.A);
            yield return Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A);

            // Переход к грязи без JumpIfOutsideHomeArea
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
                .JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, initExtractTargetFromQueue);

            // Тойл очистки
            var clean = ToilMaker.MakeToil("CleanFilthFrenzy");
            clean.initAction = delegate
            {
                cleaningWorkDone = 0f;
                totalCleaningWorkDone = 0f;
                totalCleaningWorkRequired = Filth.def.filth.cleaningWorkToReduceThickness * Filth.thickness;
            };
            clean.tickAction = delegate
            {
                var filth = Filth;
                float timeFactor = filth.Position.GetTerrain(filth.Map).GetStatValueAbstract(StatDefOf.CleaningTimeFactor);
                float speed = pawn.GetStatValue(StatDefOf.CleaningSpeed);
                if (timeFactor != 0f)
                    speed /= timeFactor;

                cleaningWorkDone += speed;
                totalCleaningWorkDone += speed;
                if (cleaningWorkDone > filth.def.filth.cleaningWorkToReduceThickness)
                {
                    filth.ThinFilth();
                    cleaningWorkDone = 0f;
                    if (filth.Destroyed)
                    {
                        clean.actor.records.Increment(RecordDefOf.MessesCleaned);
                        ReadyForNextToil();
                    }
                }
            };
            clean.defaultCompleteMode = ToilCompleteMode.Never;
            clean.WithEffect(EffecterDefOf.Clean, TargetIndex.A, null);
            clean.WithProgressBar(TargetIndex.A, () => totalCleaningWorkDone / totalCleaningWorkRequired, true);
            clean.PlaySustainerOrSound(() =>
            {
                var def = Filth.def;
                return !def.filth.cleaningSound.NullOrUndefined()
                    ? def.filth.cleaningSound
                    : SoundDefOf.Interact_CleanFilth;
            });

            clean.JumpIfDespawnedOrNullOrForbidden(TargetIndex.A, initExtractTargetFromQueue);
            // Убраны оба JumpIfOutsideHomeArea
            clean.JumpIf(() => clean.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing?.Destroyed ?? false,
                         initExtractTargetFromQueue);
            yield return clean;

            // Возврат к началу очереди
            yield return Toils_Jump.Jump(initExtractTargetFromQueue);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cleaningWorkDone, "cleaningWorkDone", 0f);
            Scribe_Values.Look(ref totalCleaningWorkDone, "totalCleaningWorkDone", 0f);
            Scribe_Values.Look(ref totalCleaningWorkRequired, "totalCleaningWorkRequired", 0f);
        }
    }
}
