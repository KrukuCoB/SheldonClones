using RimWorld;
using System.Collections.Generic;
using Verse;

namespace SheldonClones
{
    public class InteractionWorker_CloneSheldonOnly : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            // Проверяем, является ли инициатор клоном Шелдона
            if (initiator.def.defName == "SheldonClone")
            {
                return 1.0f; // Вероятность 100%, если клон Шелдона
            }
            return 0f; // Обычные люди не могут использовать это взаимодействие
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
            // Вызываем стандартную обработку взаимодействий
            base.Interacted(initiator, recipient, extraSentencePacks, out letterText, out letterLabel, out letterDef, out lookTargets);
        }
    }
}
