using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SheldonClones
{
    public class CompSheldonSeatAssignable : CompAssignableToPawn
    {
        // Разрешаем назначать только клонам Шелдона
        public override AcceptanceReport CanAssignTo(Pawn pawn)
        {
            if (pawn.def == AlienDefOf.SheldonClone)
                return AcceptanceReport.WasAccepted;
            return new AcceptanceReport("NotSheldonClone".Translate());
        }

        // Кандидатами на назначение считаем только свободных колонистов-клонов Шелдона
        public override IEnumerable<Pawn> AssigningCandidates
        {
            get
            {
                if (!parent.Spawned)
                    yield break;
                foreach (var p in parent.Map.mapPawns.FreeColonists)
                    if (p.def == AlienDefOf.SheldonClone)
                        yield return p;
            }
        }

        public override string CompInspectStringExtra()
        {
            // если никто не присвоен
            if (!AssignedPawnsForReading.Any())
                return "Свободно";
            // иначе — имя первого (и единственного) хозяина
            return "Место " + AssignedPawnsForReading[0].LabelShort;
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (AssignedPawnsForReading.Any())
            {
                var pawn = AssignedPawnsForReading[0];
                Vector3 drawPos = parent.DrawPos;
                drawPos.y += 0.35f; // чуть повыше
                GenMapUI.DrawThingLabel(drawPos, pawn.LabelShort, Color.yellow);
            }
        }


    }
}
