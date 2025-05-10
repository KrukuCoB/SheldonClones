using Verse;
using Verse.AI;

namespace SheldonClones
{
    public static class SheldonSpotUtility
    {
        // Проверяет, занято ли "место Шелдона" (его персональная точка)
        public static bool IsMySpotOccupied(Pawn pawn)
        {
            var spot = pawn.GetMySpot();
            if (spot == null)
                return false;

            return spot.Value.GetFirstPawn(pawn.Map) is Pawn other && other != pawn;
        }

        // Получает пешку, которая заняла "место Шелдона"
        public static Pawn GetOccupantOfMySpot(Pawn pawn)
        {
            var spot = pawn.GetMySpot();
            if (spot == null)
                return null;

            var other = spot.Value.GetFirstPawn(pawn.Map);
            return other != pawn ? other : null;
        }

        // Проверяет, может ли пешка дойти до своего места (безопасный путь)
        public static bool CanReachMySpot(Pawn pawn)
        {
            var spot = pawn.GetMySpot();
            if (spot == null)
                return false;

            return pawn.CanReach(spot.Value, PathEndMode.OnCell, Danger.Some);
        }
    }
}
