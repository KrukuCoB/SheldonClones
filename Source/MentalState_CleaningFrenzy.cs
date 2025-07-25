using System.Linq;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    public class MentalState_CleaningFrenzy : MentalState
    {
        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);

            if (pawn.CurJob == null || pawn.CurJob.def != Sheldon_JobDefOf.CleanFrenzy)
            {
                Job job = TryGetCleaningJob();
                if (job != null)
                {
                    // принудительно переключаем на наш джоб, не добавляя в очередь
                    pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                }
            }
        }

        private Job TryGetCleaningJob()
        {
            // 1) Получаем все объекты грязи, до которых можем добраться
            var allFilth = pawn.Map.listerThings
                .ThingsInGroup(ThingRequestGroup.Filth)
                .Where(f => pawn.CanReach(f, PathEndMode.Touch, Danger.Deadly))
                .Cast<Thing>()
                .OrderBy(f => pawn.Position.DistanceTo(f.Position))
                .ToList();

            if (!allFilth.Any())
                return null;

            // 2) Создаём единый Job без целевого filth
            var job = JobMaker.MakeJob(Sheldon_JobDefOf.CleanFrenzy);

            // 3) Кладём в очередь до 15 целей (как ванильный CleanFilth)
            foreach (var filth in allFilth.Take(15))
            {
                job.AddQueuedTarget(TargetIndex.A, filth);
            }

            return job;
        }


        public override bool AllowRestingInBed => false;
    }
}
