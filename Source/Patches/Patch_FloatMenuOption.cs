using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    [HarmonyPatch(typeof(FloatMenuMakerMap), "GetOptions")]
    public static class Patch_FloatMenuOption
    {
        public static void Postfix(List<Pawn> selectedPawns, Vector3 clickPos, ref List<FloatMenuOption> __result)
        {
            if (!selectedPawns.Any()) return;

            Pawn pawn = selectedPawns.First(); // Берем первую выбранную пешку

            // Пешка, по которой кликнули ПКМ
            Pawn targetPawn = clickPos.ToIntVec3().GetFirstPawn(pawn.Map);

            if (targetPawn == null || targetPawn == pawn) return;

            // Проверить: у инициатора (pawn) есть страйк от targetPawn (клон Шелдона)?
            foreach (var h in pawn.health.hediffSet.hediffs)
            {
                if (h is Hediff_SheldonStrike strike && strike.sheldonName == targetPawn.Label)
                {
                    // Добавить пункт меню
                    __result.Add(new FloatMenuOption("Пройти курс у Шелдона", () =>
                    {
                        Job job = JobMaker.MakeJob(Sheldon_JobDefOf.SheGoToClass, targetPawn); // цель — преподаватель
                        pawn.jobs.TryTakeOrderedJob(job); // инициатор — нарушитель
                        Messages.Message($"{pawn.LabelShort} начал курс у {targetPawn.LabelShort}", MessageTypeDefOf.PositiveEvent);
                    }));

                    break;
                }
            }
        }
    }
}
