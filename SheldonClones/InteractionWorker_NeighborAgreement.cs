using RimWorld;
using Verse;
using System.Collections.Generic;

namespace SheldonClones
{
    public class InteractionWorker_NeighborAgreement : InteractionWorker
    {
        // Предложение подписать соглашение появляется с весом 0.9 (90%)
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (initiator.def.defName != "SheldonClone" || !initiator.Spawned || !recipient.Spawned)
                return 0f;

            var comp = initiator.TryGetComp<CompNeighborAgreement>();
            if (comp == null)
                return 0f;

            var nearby = comp.GetNearbyBedNeighbors();
            if (!nearby.Contains(recipient) || comp.HasAgreementWith(recipient))
                return 0f;

            return 0.9f;
        }

        public override void Interacted(
            Pawn initiator, Pawn recipient,
            List<RulePackDef> extraSentencePacks,
            out string letterText, out string letterLabel,
            out LetterDef letterDef, out LookTargets lookTargets)
        {
            base.Interacted(initiator, recipient, extraSentencePacks,
                            out letterText, out letterLabel, out letterDef, out lookTargets);

            letterText = letterLabel = null;
            letterDef = null;
            lookTargets = null;

            var comp = initiator.TryGetComp<CompNeighborAgreement>();
            if (comp == null || comp.HasAgreementWith(recipient))
                return;

            // рассчитываем шанс соглашения
            float agreeChance = 0.4f;
            bool initiatorIsClone = initiator.def == AlienDefOf.SheldonClone;
            bool recipientIsClone = recipient.def == AlienDefOf.SheldonClone;
            if (initiatorIsClone && recipientIsClone)
                agreeChance = 0.9f;

            bool success = Rand.Value < agreeChance;
            if (success)
            {
                comp.AddAgreement(recipient);
                Messages.Message(
                    $"{initiator.LabelShort} подписал соседское соглашение с {recipient.LabelShort}.",
                    initiator, MessageTypeDefOf.PositiveEvent);

                // мысль на настроение инициатора (только клонам)
                if (initiatorIsClone)
                    initiator.needs?.mood?.thoughts?.memories?.TryGainMemory(
                        ThoughtDef.Named("NeighborAgreementSignedMood"), recipient);

                // социальная мысль (только клонам)
                if (initiatorIsClone)
                    initiator.needs?.mood?.thoughts?.memories?.TryGainMemory(
                        ThoughtDef.Named("NeighborAgreementSignedSocial"), recipient);
                if (recipientIsClone)
                    recipient.needs?.mood?.thoughts?.memories?.TryGainMemory(
                        ThoughtDef.Named("NeighborAgreementSignedSocial"), initiator);
            }
            else
            {
                Messages.Message(
                    $"{recipient.LabelShort} отказался подписывать соседское соглашение с {initiator.LabelShort}.",
                    initiator, MessageTypeDefOf.NegativeEvent);

                // мысль на настроение инициатора (только клонам)
                if (initiatorIsClone)
                    initiator.needs?.mood?.thoughts?.memories?.TryGainMemory(
                        ThoughtDef.Named("NeighborAgreementRefusedMood"), recipient);

                // социальная мысль на отказ (только клонам)
                if (initiatorIsClone)
                    initiator.needs?.mood?.thoughts?.memories?.TryGainMemory(
                        ThoughtDef.Named("NeighborAgreementRefusedSocial"), recipient);
                if (recipientIsClone)
                    recipient.needs?.mood?.thoughts?.memories?.TryGainMemory(
                        ThoughtDef.Named("NeighborAgreementRefusedSocial"), initiator);
            }
        }
    }
}
