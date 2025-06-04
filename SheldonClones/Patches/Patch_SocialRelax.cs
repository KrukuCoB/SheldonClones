using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    // Prefix для мгновенного прыжка к уже присвоенному стулу
    [HarmonyPatch(typeof(JoyGiver_SocialRelax), nameof(JoyGiver_SocialRelax.TryGiveJob))]
    [HarmonyPatch(new[] { typeof(Pawn) })]
    public static class Patch_SocialRelax_TryGiveJob
    {
        static void Postfix(ref Job __result, Pawn pawn)
        {
            if (__result == null)
                return;
            var processed = SocialRelaxSeatAssigner.ProcessSocialJob(__result, pawn);
            if (processed != null)
                __result = processed;
        }
    }

    [HarmonyPatch(typeof(JoyGiver_SocialRelax), nameof(JoyGiver_SocialRelax.TryGiveJobInGatheringArea))]
    [HarmonyPatch(new[] { typeof(Pawn), typeof(IntVec3), typeof(float) })]
    public static class Patch_SocialRelax_TryGiveJobInGatheringArea
    {
        static void Postfix(ref Job __result, Pawn pawn, IntVec3 gatheringSpot, float maxRadius)
        {
            if (__result == null)
                return;
            var processed = SocialRelaxSeatAssigner.ProcessSocialJob(__result, pawn);
            if (processed != null)
                __result = processed;
        }
    }

    static class SocialRelaxSeatAssigner
    {
        // Основная функция: возвращает новый Job, если нужно, или оригинал
        public static Job ProcessSocialJob(Job job, Pawn pawn)
        {
            if (job == null)
                return null;

            // ── 0) Эвикт только для клонов, если их стул занят ──
            if (pawn.def == AlienDefOf.SheldonClone)
            {
                var myComp = pawn.Map.listerThings.AllThings
                    .Select(t => t.TryGetComp<CompSheldonSeatAssignable>())
                    .FirstOrDefault(c => c != null && c.AssignedAnything(pawn));
                // Если мой стул **занят** (нет свободных клеток) — пытаемся выгнать
                if (myComp != null
                    && !TryFindFreeCell(myComp.parent, pawn, out _))
                {
                    // ищем, кто на нём сидит
                    Pawn occupant = null;
                    foreach (var pos in myComp.parent.OccupiedRect())
                    {
                        occupant = pos.GetThingList(pawn.Map)
                                      .OfType<Pawn>()
                                      .FirstOrDefault(x => x != pawn);
                        if (occupant != null) break;
                    }
                    if (occupant != null)
                    {
                        var def = DefDatabase<InteractionDef>.GetNamed("SheldonWarnedForSittingInMySpot");
                        pawn.interactions.TryInteractWith(occupant, def);
                        Log.Message($"[SheldonClones] {pawn.LabelShort} выгоняет {occupant.LabelShort} со своего места!");
                        // возвращаем null, чтобы ванильный соц.релакс отменился сразу
                        return job;
                    }
                }
            }

            // ── 1) Для клона со своим стулом: сразу своя комбинация стол+стул ──
            if (pawn.def == AlienDefOf.SheldonClone)
            {
                var myComp = pawn.Map.listerThings.AllThings
                    .Select(t => t.TryGetComp<CompSheldonSeatAssignable>())
                    .FirstOrDefault(c => c != null && c.AssignedAnything(pawn));
                if (myComp != null
                    // если стул сейчас свободен
                    && pawn.CanReserve(myComp.parent)
                    && TryFindFreeCell(myComp.parent, pawn, out _))
                {
                    // найдём стол рядом с этим стулом
                    var table = FindAdjacentTable(myComp.parent, pawn);
                    if (table != null)
                        job.SetTarget(TargetIndex.A, table);

                    // и назначим стул
                    job.targetB = myComp.parent;
                    var chairPos = myComp.parent.Position;
                    pawn.ReserveSittableOrSpot(chairPos, job);
                    pawn.Map.pawnDestinationReservationManager
                        .Reserve(pawn, job, chairPos);

                    return job;
                }
            }

            // ── 2) Любая пешка ищет свободный незанятый стул (AssignedPawnsForReading.Count==0)
            var freeComps = pawn.Map.listerThings.AllThings
                .Select(t => t.TryGetComp<CompSheldonSeatAssignable>())
                .Where(c => c != null && c.AssignedPawnsForReading.Count == 0);
            foreach (var comp in freeComps)
            {
                if (pawn.CanReserve(comp.parent) && TryFindFreeCell(comp.parent, pawn, out _))
                {
                    // только клоны Шелдона его сохраняют
                    if (pawn.def == AlienDefOf.SheldonClone)
                        comp.TryAssignPawn(pawn);

                    var table = FindAdjacentTable(comp.parent, pawn);
                    if (table != null)
                        job.SetTarget(TargetIndex.A, table);

                    job.targetB = comp.parent;
                    pawn.Reserve(comp.parent, job);
                    return job;
                }
            }

            // ── 3) Любая пешка ищет чужой, но сейчас свободный стул
            var otherComps = pawn.Map.listerThings.AllThings
                .Select(t => t.TryGetComp<CompSheldonSeatAssignable>())
                .Where(c => c != null && c.AssignedPawnsForReading.Count > 0);
            foreach (var comp2 in otherComps)
            {
                if (pawn.CanReserve(comp2.parent) && TryFindFreeCell(comp2.parent, pawn, out _))
                {
                    var table = FindAdjacentTable(comp2.parent, pawn);
                    if (table != null)
                        job.SetTarget(TargetIndex.A, table);

                    // временно садимся, никто не присваивает
                    job.targetB = comp2.parent;

                    // надёжно резервируем и сам стул, и клетку под ним
                    pawn.ReserveSittableOrSpot(comp2.parent.Position, job);
                    pawn.Map.pawnDestinationReservationManager
                        .Reserve(pawn, job, comp2.parent.Position);

                    return job;


                }
            }

            // ── 4) Ничего не нашли — возвращаем исходный Job ──
            return job;
        }

        // Ищет в OccupiedRect() первую клетку, где pawn.CanReserveSittableOrSpot == true
        private static bool TryFindFreeCell(Thing t, Pawn pawn, out IntVec3 cell)
        {
            foreach (var pos in t.OccupiedRect())
                if (pawn.CanReserveSittableOrSpot(pos))
                {
                    cell = pos;
                    return true;
                }
            cell = IntVec3.Invalid;
            return false;
        }

        // Ищет рядом со стулом первый стол (surfaceType == Eat)
        private static Thing FindAdjacentTable(Thing chair, Pawn pawn)
        {
            foreach (var pos in chair.OccupiedRect())
                foreach (var dir in GenAdj.CardinalDirections)
                {
                    var ed = (pos + dir).GetEdifice(pawn.Map);
                    if (ed != null && ed.def.surfaceType == SurfaceType.Eat)
                        return ed;
                }
            return null;
        }
    }
}