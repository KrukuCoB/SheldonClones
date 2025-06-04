using Verse;
using Verse.AI;

namespace SheldonClones
{
    public static class Sheldon_Utils
    {
        public static void ForceEvict(Pawn initiator, Pawn victim)
        {
            if (victim.jobs != null)
            {
                victim.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
                Log.Message($"[SheldonClones] {initiator.LabelShort} выгоняет {victim.LabelShort} со своего места!");
            }
        }
    }
}
