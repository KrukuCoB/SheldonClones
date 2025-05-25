using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SheldonClones
{
    public class InteractionWorker_NeighborAgreement : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (initiator.def.defName != "SheldonClone" || !initiator.Spawned || !recipient.Spawned)
                return 0f;

            var comp = initiator.TryGetComp<CompNeighborAgreement>();
            if (comp == null)
                return 0f;

            List<Pawn> nearby = comp.GetNearbyBedNeighbors();
            if (!nearby.Contains(recipient))
                return 0f;

            if (comp.HasAgreementWith(recipient))
                return 0f;

            return 0.4f;
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

            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;

            var comp = initiator.TryGetComp<CompNeighborAgreement>();
            if (comp != null && !comp.HasAgreementWith(recipient))
            {
                comp.AddAgreement(recipient);
                Messages.Message($"{initiator.LabelShort} подписал соседское соглашение с {recipient.LabelShort}.", initiator, MessageTypeDefOf.PositiveEvent);
            }
        }
    }
}
