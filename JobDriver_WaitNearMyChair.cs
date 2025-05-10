using Verse;
using Verse.AI;
using RimWorld;
using System.Collections.Generic;

namespace SheldonClones
{
    public class JobDriver_WaitNearMyChair : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Проверяем наличие координаты персонального места
            var mySpot = pawn.GetMySpot();
            if (mySpot == null || !mySpot.Value.InBounds(Map))
            {
                Log.Warning("[SheldonClones] JobDriver_WaitNearMyChair: нет mySpot или оно вне карты.");
                EndJobWith(JobCondition.Incompletable);
                yield break;
            }

            // Подходим к своей точке
            yield return Toils_Goto.GotoCell(mySpot.Value, PathEndMode.Touch);

            // Ожидание освобождения места + общение с захватчиком
            Toil wait = new Toil();
            wait.defaultCompleteMode = ToilCompleteMode.Never;

            int ticksSinceLastInteraction = 0;

            wait.tickAction = () =>
            {
                if (!SheldonSpotUtility.IsMySpotOccupied(pawn))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }

                Pawn occupier = SheldonSpotUtility.GetOccupantOfMySpot(pawn);
                if (occupier != null && occupier != pawn && pawn.interactions != null)
                {
                    InteractionDef def = DefDatabase<InteractionDef>.GetNamedSilentFail("SheldonWarnedForSittingInMySpot");
                    if (def != null && ticksSinceLastInteraction >= 300)
                    {
                        pawn.interactions.TryInteractWith(occupier, def);
                        ticksSinceLastInteraction = 0;
                    }
                    else
                    {
                        ticksSinceLastInteraction++;
                    }
                }
            };

            wait.WithProgressBarToilDelay(TargetIndex.A);
            wait.socialMode = RandomSocialMode.Off;
            yield return wait;
        }
    }
}
