using RimWorld;
using Verse;
using System.Collections.Generic;
using Verse.AI;

namespace SheldonClones
{
    public class InteractionWorker_EvictFromSpot : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            // Только клоны инициируют это взаимодействие
            return initiator.def == AlienDefOf.SheldonClone ? 0f : 0f;
        }

        public override void Interacted(
            Pawn initiator,
            Pawn recipient,
            List<RulePackDef> extraSentencePacks,
            out string letterText,
            out string letterLabel,
            out LetterDef letterDef,
            out LookTargets lookTargets)
        {
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);

            // Принудительно прерываем действие сидящего на месте клона
            Sheldon_Utils.ForceEvict(initiator, recipient);

        }
    }
}
