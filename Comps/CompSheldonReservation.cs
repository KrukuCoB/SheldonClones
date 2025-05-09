using Verse;

namespace SheldonClones
{
    public class CompSheldonReservation : GameComponent
    {
        private int cleanupTicks = 0;
        private const int CLEANUP_INTERVAL = 5000;

        public CompSheldonReservation(Game game) : base() { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            cleanupTicks++;
            if (cleanupTicks >= CLEANUP_INTERVAL)
            {
                PawnExtensions.CleanupStaleReservations();
                cleanupTicks = 0;
            }
        }
    }
}
