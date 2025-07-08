using RimWorld;
using Verse;

namespace SheldonClones
{
    [DefOf]
    public static class AlienDefOf
    {
        static AlienDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AlienDefOf));
        }

        public static ThingDef SheldonClone; // Определяем расу клонов Шелдона
    }
}