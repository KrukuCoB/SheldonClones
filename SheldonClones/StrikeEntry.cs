using System.Collections.Generic;
using Verse;

namespace SheldonClones
{
    public class StrikeEntry : IExposable
    {
        public Pawn issuer;          // кто выдал страйки
        public List<Pawn> victims;   // кому именно он их выдал

        public StrikeEntry() { }     // нужен Scribe

        public StrikeEntry(Pawn issuer, List<Pawn> victims)
        {
            this.issuer = issuer;
            this.victims = victims;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref issuer, "issuer");
            Scribe_Collections.Look(ref victims, "victims", LookMode.Reference);
        }
    }
}
