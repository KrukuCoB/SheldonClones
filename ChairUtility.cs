using RimWorld;
using Verse.AI;
using Verse;

namespace SheldonClones
{
    public static class ChairUtility
    {
        public static bool IsSomeoneAlreadySitting(Thing chair, Pawn askingPawn)
        {
            if (chair == null || chair.Map == null)
                return false;

            return IsSomeoneAlreadySitting(chair.Position, chair.Map, askingPawn);
        }

        public static bool IsSomeoneAlreadySitting(IntVec3 cell, Map map, Pawn askingPawn)
        {
            if (map == null) return false;

            var things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is Pawn otherPawn && otherPawn != askingPawn)
                {
                    Job job = otherPawn.CurJob;
                    if (job != null && job.targetA.Cell == cell)
                    {
                        JobDef jobDef = job.def;
                        if (jobDef == JobDefOf.Ingest ||
                            jobDef == JobDefOf.Lovin ||
                            jobDef == JobDefOf.Meditate ||
                            jobDef == JobDefOf.UseCommsConsole ||
                            jobDef == JobDefOf.LayDown ||
                            jobDef == JobDefOf.Wait_MaintainPosture)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // Новый метод: возвращает пешку, сидящую на клетке
        public static Pawn GetSittingPawnAt(IntVec3 cell, Map map, Pawn excluding = null)
        {
            if (map == null) return null;

            var things = cell.GetThingList(map);
            foreach (Thing thing in things)
            {
                if (thing is Pawn p && p != excluding)
                {
                    Job job = p.CurJob;
                    if (job != null && job.targetA.Cell == cell)
                    {
                        JobDef jobDef = job.def;
                        if (jobDef == JobDefOf.Ingest ||
                            jobDef == JobDefOf.Lovin ||
                            jobDef == JobDefOf.Meditate ||
                            jobDef == JobDefOf.UseCommsConsole ||
                            jobDef == JobDefOf.LayDown ||
                            jobDef == JobDefOf.Wait_MaintainPosture)
                        {
                            return p;
                        }
                    }
                }
            }
            return null;
        }
    }
}
