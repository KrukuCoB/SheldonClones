using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SheldonClones
{
    // Менеджер для управления системой резервирования мест
    public class CompSheldonSeatingManager : GameComponent
    {
        // Кэш всех стульев с CompSheldonSeatAssignable
        private static ConcurrentDictionary<Map, List<CompSheldonSeatAssignable>> cachedSeats =
            new ConcurrentDictionary<Map, List<CompSheldonSeatAssignable>>();

        public CompSheldonSeatingManager(Game game) : base() { }

        public override void FinalizeInit()
        {
            base.FinalizeInit();

            PawnExtensions.SynchronizeAllReservations();
            PawnExtensions.ClearReservedSeatsCache();
            RefreshSeatsCache();
            RestoreReservedSeats();
        }

        // Обновляет кэш стульев для текущей карты
        public static void RefreshSeatsCache()
        {
            if (Current.Game?.Maps == null) return;

            cachedSeats.Clear();

            foreach (Map map in Current.Game.Maps)
            {
                var seats = new List<CompSheldonSeatAssignable>(32); // Предполагаемый размер для оптимизации

                // Используем LINQ для более чистого и потенциально быстрого кода
                seats.AddRange(map.listerBuildings.allBuildingsColonist
                    .Select(b => b.TryGetComp<CompSheldonSeatAssignable>())
                    .Where(comp => comp != null));

                cachedSeats[map] = seats;
            }
        }


        // Возвращает кэшированный список стульев для карты
        public static List<CompSheldonSeatAssignable> GetSeatsForMap(Map map)
        {
            if (map == null) return new List<CompSheldonSeatAssignable>();

            // Если кэш для этой карты не существует, создаем его
            if (!cachedSeats.ContainsKey(map))
            {
                RefreshSeatsCache();
            }

            return cachedSeats.ContainsKey(map) ? cachedSeats[map] : new List<CompSheldonSeatAssignable>();
        }

        // Добавляет стул в кэш
        public static void AddSeatToCache(CompSheldonSeatAssignable seat)
        {
            if (seat == null || seat.parent == null || seat.parent.Map == null) return;

            Map map = seat.parent.Map;

            if (!cachedSeats.ContainsKey(map))
            {
                cachedSeats[map] = new List<CompSheldonSeatAssignable>();
            }

            if (!cachedSeats[map].Contains(seat))
            {
                cachedSeats[map].Add(seat);
            }
        }

        // Удаляет стул из кэша
        public static void RemoveSeatFromCache(CompSheldonSeatAssignable seat)
        {
            if (seat == null || seat.parent == null || seat.parent.Map == null) return;

            Map map = seat.parent.Map;

            if (cachedSeats.ContainsKey(map))
            {
                cachedSeats[map].Remove(seat);
            }
        }

        // Восстанавливаем забронированные места из компонентов
        private void RestoreReservedSeats()
        {
            if (Current.Game?.CurrentMap == null)
                return;

            Map map = Current.Game.CurrentMap;

            // Находим все объекты с компонентом резервирования
            List<Thing> allSittableThings = map.listerThings.AllThings.FindAll(
                thing => thing.TryGetComp<CompSheldonSeatAssignable>() != null);

            foreach (Thing thing in allSittableThings)
            {
                CompSheldonSeatAssignable comp = thing.TryGetComp<CompSheldonSeatAssignable>();

                if (comp != null)
                {
                    List<Pawn> assignedPawns = comp.GetAssignedPawns();

                    if (assignedPawns.Count > 0)
                    {
                        Pawn assignedPawn = assignedPawns[0];

                        if (assignedPawn != null && assignedPawn.def == AlienDefOf.SheldonClone)
                        {
                            // Восстанавливаем забронированное место для клона
                            assignedPawn.SetReservedSittingSpot(thing.Position);
                        }
                    }
                }
            }
        }
    }
}
