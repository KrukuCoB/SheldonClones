using System;
using System.Collections.Generic;
using Verse;

namespace SheldonClones
{
    public class Hediff_SheldonStrike : HediffWithComps
    {
        public string sheldonName;

        public override string Label => $"{base.Label} ({sheldonName})";

        public override void ExposeData()
        {
            // НЕ вызываем base.ExposeData(), чтобы не сериализовался combatLogEntry
            // Вместо этого — сохраняем всё вручную

            Scribe_Values.Look(ref sheldonName, "sheldonName");
            Scribe_Values.Look(ref severityInt, "severity");
            Scribe_Values.Look(ref ageTicks, "ageTicks");
            Scribe_Values.Look(ref loadID, "loadID", -1);
            Scribe_Defs.Look(ref def, "def"); // def обязателен

            // Обязательно сериализуем comps, иначе всё сломается!
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                comps?.ForEach(c => c.CompExposeData());
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // При загрузке сначала создаём comps по def, затем сериализуем их
                this.comps = new List<HediffComp>();
                if (this.def.comps != null)
                {
                    foreach (var compProps in def.comps)
                    {
                        var comp = (HediffComp)Activator.CreateInstance(compProps.compClass);
                        comp.parent = this;
                        comp.props = compProps;
                        this.comps.Add(comp);
                    }

                    foreach (var comp in this.comps)
                    {
                        comp.CompExposeData();
                    }
                }
            }
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() ^ (sheldonName?.GetHashCode() ?? 0);
        }

        public override bool TryMergeWith(Hediff other)
        {
            if (other is Hediff_SheldonStrike otherStrike && otherStrike.sheldonName == this.sheldonName)
            {
                this.Severity = Math.Min(this.Severity + 1, 3);
                return true;
            }
            return false;
        }
    }
}
