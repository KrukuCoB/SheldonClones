using Verse;
using Verse.AI;

namespace SheldonClones
{
    public class MentalStateWorker_CleaningFrenzy : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            return pawn.def.defName == "SheldonClone"; // Только для клонов
        }
    }

}
