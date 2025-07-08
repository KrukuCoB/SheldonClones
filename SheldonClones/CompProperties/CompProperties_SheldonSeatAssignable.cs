using RimWorld;

namespace SheldonClones
{
    public class CompProperties_SheldonSeatAssignable : CompProperties_AssignableToPawn
    {
        public CompProperties_SheldonSeatAssignable()
        {
            compClass = typeof(CompSheldonSeatAssignable);
            maxAssignedPawnsCount = 1;
            drawAssignmentOverlay = true;
            drawUnownedAssignmentOverlay = true;
        }
    }
}
