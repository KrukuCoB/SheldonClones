using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using static SheldonClones.TryFindFreeSittingSpotOnThingPatch;

namespace SheldonClones
{
    public class JobDriver_WaitNearMyChair : JobDriver
    {
        private const int WaitTicks = 600;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 🧱 Проверка на корректность цели
            if (!TargetA.IsValid || !TargetA.Cell.InBounds(Map))
            {
                Log.Warning("[SheldonClones] JobDriver_WaitNearMyChair: TargetA невалиден или вне карты.");
                yield break;
            }

            // 🔹 Подходим к месту
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);

            // ⏳ Ждём с интеракцией
            Toil wait = new Toil
            {
                initAction = () =>
                {
                    Pawn initiator = pawn;
                    Pawn recipient = ChairUtility.GetSittingPawnAt(TargetA.Cell, Map, initiator);
                    if (recipient != null && recipient != initiator)
                    {
                        InteractionDef def = DefDatabase<InteractionDef>.GetNamed("SheldonWarnedForSittingInMySpot", false);
                        if (def != null)
                        {
                            initiator.interactions.TryInteractWith(recipient, def);
                        }
                    }
                },
                defaultCompleteMode = ToilCompleteMode.Delay,
                defaultDuration = WaitTicks
            };

            wait.WithProgressBarToilDelay(TargetIndex.A); // Визуализация ожидания

            yield return wait;
        }
    }
}
