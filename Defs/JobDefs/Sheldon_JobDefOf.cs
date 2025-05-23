using RimWorld;
using Verse;

namespace SheldonClones
{
    [DefOf]
    public static class Sheldon_JobDefOf
    {
        public static JobDef SheGoToClass;

        static Sheldon_JobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(Sheldon_JobDefOf));
        }
    }
}
