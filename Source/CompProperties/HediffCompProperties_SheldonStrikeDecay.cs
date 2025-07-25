using Verse;

namespace SheldonClones
{
    public class HediffCompProperties_SheldonStrikeDecay : HediffCompProperties
    {
        public float severityDecayPerDay = 0.1f; // на сколько снижать severity в день, настраивается в xml

        public HediffCompProperties_SheldonStrikeDecay()
        {
            compClass = typeof(HediffComp_SheldonStrikeDecay);
        }
    }
}