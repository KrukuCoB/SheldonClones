using System.Linq;
using Verse;

namespace SheldonClones
{
    public class HediffComp_SheldonStrikeDecay : HediffComp
    {
        public HediffCompProperties_SheldonStrikeDecay Props => (HediffCompProperties_SheldonStrikeDecay)this.props;

        public int ticksUntilDecay = 600000; // 10 дней по умолчанию

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            ticksUntilDecay--;

            if (ticksUntilDecay <= 0)
            {
                // Уменьшаем severity
                parent.Severity -= 1f;

                // Лог
                Log.Message($"[SheldonStrike] У {parent.pawn.LabelShortCap} закончилось время действия одного страйка.");

                // Проверяем, нужно ли убрать из списка игнорируемых (если уровень стал меньше 3)
                if (parent.Severity < 3f && parent is Hediff_SheldonStrike sheldonStrike)
                {
                    // Находим клона по имени и убираем пешку из списка игнорируемых
                    var sheldonClone = Find.Maps.SelectMany(m => m.mapPawns.AllPawnsSpawned)
                        .FirstOrDefault(p => p.def == AlienDefOf.SheldonClone && p.Label == sheldonStrike.sheldonName);

                    if (sheldonClone != null)
                    {
                        var watcher = Find.World.GetComponent<CompSheldonWatcher>();
                        watcher?.RemoveIgnoredPawn(sheldonClone, parent.pawn);
                    }
                }

                // Удаляем, если уровень меньше 1
                if (parent.Severity < 1f)
                {
                    parent.pawn.health.RemoveHediff(parent);
                }

                // Сбрасываем таймер
                ticksUntilDecay = 600000;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksUntilDecay, "ticksUntilDecay", 600000);
        }

        public void ResetDecayTimer()
        {
            ticksUntilDecay = 600000;
        }

    }
}
