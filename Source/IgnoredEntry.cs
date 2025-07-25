using System.Collections.Generic;
using Verse;

namespace SheldonClones
{
    // Обёртка одной записи для словаря ignoredPawns
    public class IgnoredEntry : IExposable
    {
        public Pawn key;              // кто игнорирует
        public List<Pawn> values;     // кого именно он игнорирует

        // Пустой конструктор обязателен для Scribe
        public IgnoredEntry() { }

        public IgnoredEntry(Pawn key, List<Pawn> values)
        {
            this.key = key;
            this.values = values;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref key, "key");
            Scribe_Collections.Look(ref values, "values", LookMode.Reference);
        }
    }
}
