using Verse;

namespace SheldonClones
{
    public class HediffCompProperties_SheldonStrikeDecay : HediffCompProperties
    {
        public float severityDecayPerDay = 0.1f; // на сколько снижать severity в день, настраивается в xml

        public HediffCompProperties_SheldonStrikeDecay()
        {
            this.compClass = typeof(HediffComp_SheldonStrikeDecay);
        }
    }

    public class HediffComp_SheldonStrikeDecay : HediffComp
    {
        public HediffCompProperties_SheldonStrikeDecay Props => (HediffCompProperties_SheldonStrikeDecay)this.props;

        private int ticksUntilDecay = 600000; // 10 дней по умолчанию

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            ticksUntilDecay--;

            if (ticksUntilDecay <= 0)
            {
                // Уменьшаем severity
                parent.Severity -= 1f; // Убираем один уровень страйка

                // Лог
                Log.Message($"[SheldonStrike] У {parent.pawn.LabelShortCap} закончилось время действия одного страйка.");

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
            ticksUntilDecay = 600000; // или сколько у тебя по дефолту
        }

    }
}
