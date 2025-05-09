using Verse;
using System.Collections.Generic;
using RimWorld;
using Verse.AI;
using System;
using System.Linq;

namespace SheldonClones
{
    public static class PawnExtensions
    {
        // Словарь для отслеживания забронированных мест для клонов Шелдона
        // Ключ: ID пешки, Значение: позиция стула
        private static Dictionary<int, IntVec3> reservedSeats = new Dictionary<int, IntVec3>();

        // Объект синхронизации для безопасного доступа к словарю из разных потоков
        private static readonly object lockObject = new object();

        // Вспомогательный метод для получения уникального ID пешки
        private static int GetPawnID(Pawn pawn)
        {
            return pawn?.thingIDNumber ?? -1;
        }

        // Проверка, есть ли у клона забронированное место
        public static bool IsReservedForSitting(this Pawn pawn)
        {
            if (pawn == null || pawn.def != AlienDefOf.SheldonClone)
                return false;

            int id = GetPawnID(pawn);
            return id != -1 && reservedSeats.ContainsKey(id);
        }

        // Установка забронированного места
        public static void SetReservedSittingSpot(this Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || pawn.def != AlienDefOf.SheldonClone || cell == IntVec3.Invalid)
                return;

            int id = GetPawnID(pawn);
            if (id == -1) return;

            try
            {
                lock (lockObject)
                {
                    if (!reservedSeats.ContainsKey(id))
                    {
                        reservedSeats.Add(id, cell);

                        // Найдем и обновим компонент стула
                        UpdateSeatComponent(pawn, cell);
                    }
                    else
                    {
                        // Если место меняется, обновляем компоненты
                        if (reservedSeats[id] != cell)
                        {
                            // Сначала очищаем старое место
                            IntVec3 oldCell = reservedSeats[id];
                            Thing oldSeat = FindSittableThing(oldCell, pawn.Map);
                            if (oldSeat != null)
                            {
                                CompSheldonSeatAssignable oldComp = oldSeat.TryGetComp<CompSheldonSeatAssignable>();
                                if (oldComp != null && oldComp.BelongsToSheldon(pawn))
                                {
                                    oldComp.UnassignSheldon();
                                }
                            }

                            // Затем устанавливаем новое
                            reservedSeats[id] = cell;
                            UpdateSeatComponent(pawn, cell);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Ошибка при установке резервации места: {ex}");
            }
        }

        // Получение забронированного места
        public static IntVec3 GetReservedSittingSpot(this Pawn pawn)
        {
            int id = GetPawnID(pawn);

            lock (lockObject)
            {
                return (id != -1 && reservedSeats.ContainsKey(id)) ? reservedSeats[id] : IntVec3.Invalid;
            }
        }

        // Обновляет компонент стула при резервации
        private static void UpdateSeatComponent(Pawn pawn, IntVec3 cell)
        {
            if (pawn == null || pawn.Map == null || cell == IntVec3.Invalid)
                return;

            Thing seat = FindSittableThing(cell, pawn.Map);
            if (seat != null)
            {
                CompSheldonSeatAssignable comp = seat.TryGetComp<CompSheldonSeatAssignable>();
                if (comp != null)
                {
                    comp.AssignSheldon(pawn);
                }
            }
        }

        // Находит сидячий объект в указанной позиции
        private static Thing FindSittableThing(IntVec3 pos, Map map)
        {
            if (map == null || !pos.InBounds(map))
                return null;

            List<Thing> things = pos.GetThingList(map);
            foreach (Thing thing in things)
            {
                if (thing.def.building?.isSittable == true)
                {
                    return thing;
                }
            }
            return null;
        }

        // Удаление забронированного места
        public static void RemoveReservedSittingSpot(this Pawn pawn)
        {
            int id = GetPawnID(pawn);
            if (id == -1)
                return;

            lock (lockObject)
            {
                if (reservedSeats.ContainsKey(id))
                {
                    // Находим и очищаем компонент стула перед удалением
                    IntVec3 oldCell = reservedSeats[id];
                    if (pawn.Map != null && oldCell.InBounds(pawn.Map))
                    {
                        Thing oldSeat = FindSittableThing(oldCell, pawn.Map);
                        if (oldSeat != null)
                        {
                            CompSheldonSeatAssignable oldComp = oldSeat.TryGetComp<CompSheldonSeatAssignable>();
                            if (oldComp != null && oldComp.BelongsToSheldon(pawn))
                            {
                                oldComp.UnassignSheldon();
                            }
                        }
                    }

                    // Удаляем запись из словаря
                    reservedSeats.Remove(id);
                }
            }
        }

        // Метод для проверки, может ли пешка зарезервировать место для сидения
        public static bool CanReserveSittableOrSpot(this Pawn pawn, IntVec3 cell)
        {
            // Проверяем, что клетка свободна или может быть зарезервирована
            return cell.Walkable(pawn.Map) && !cell.IsForbidden(pawn) &&
                   pawn.CanReserve(cell);
        }

        // Метод для очистки кэша при смене карты/загрузке игры
        public static void ClearReservedSeatsCache()
        {
            lock (lockObject)
            {
                reservedSeats.Clear();
            }
        }

        // Удаляет все резервации конкретной карты (полезно при смене карты)
        public static void ClearReservationsForMap(Map map)
        {
            if (map == null)
                return;

            lock (lockObject)
            {
                // Временный список для ключей, которые нужно удалить
                List<int> keysToRemove = new List<int>();

                // Проходим по всем пешкам и ищем те, что на данной карте
                foreach (KeyValuePair<int, IntVec3> entry in reservedSeats)
                {
                    Pawn pawn = FindPawnByID(entry.Key);
                    if (pawn == null || pawn.Map == map)
                    {
                        keysToRemove.Add(entry.Key);
                    }
                }

                // Удаляем все найденные ключи
                foreach (int key in keysToRemove)
                {
                    reservedSeats.Remove(key);
                }
            }
        }

        // Проверяет и очищает записи "мертвых" пешек 
        public static void CleanupDeadPawns()
        {
            lock (lockObject)
            {
                List<int> keysToRemove = new List<int>();

                foreach (int pawnId in reservedSeats.Keys)
                {
                    Pawn pawn = FindPawnByID(pawnId);
                    // Удаляем записи, если пешка не существует или мертва
                    if (pawn == null || pawn.Destroyed || pawn.Dead)
                    {
                        keysToRemove.Add(pawnId);
                    }
                }

                foreach (int key in keysToRemove)
                {
                    reservedSeats.Remove(key);
                }
            }
        }

        // Находит пешку по ID (вспомогательный метод)
        private static Pawn FindPawnByID(int id)
        {
            if (Current.Game == null)
                return null;

            // Проверяем все карты
            foreach (Map map in Current.Game.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned
                    .Where(p => p.def == AlienDefOf.SheldonClone))
                {
                    if (pawn.thingIDNumber == id)
                        return pawn;
                }
            }
            return null;
        }

        // Сохраняет словарь между сессиями игры
        public static void ExposeReservationData()
        {
            lock (lockObject)
            {
                if (Scribe.mode == LoadSaveMode.Saving)
                {
                    // Очистка "мертвых" записей перед сохранением
                    CleanupStaleReservations();
                    // Сохранение данных
                    Scribe_Collections.Look(ref reservedSeats, "sheldonReservedSeats", LookMode.Value, LookMode.Value);
                }
                else if (Scribe.mode == LoadSaveMode.LoadingVars)
                {
                    // Загрузка данных
                    if (reservedSeats == null)
                    {
                        reservedSeats = new Dictionary<int, IntVec3>();
                    }
                    Scribe_Collections.Look(ref reservedSeats, "sheldonReservedSeats", LookMode.Value, LookMode.Value);
                }
            }
        }

        // Метод для периодической очистки недействительных записей
        public static void CleanupStaleReservations()
        {
            if (Current.Game == null) return;

            List<int> keysToRemove = new List<int>();

            lock (lockObject)
            {
                foreach (int pawnId in reservedSeats.Keys)
                {
                    Pawn pawn = FindPawnByID(pawnId);
                    if (pawn == null || pawn.Destroyed || pawn.Dead)
                    {
                        keysToRemove.Add(pawnId);
                    }
                }

                foreach (int key in keysToRemove)
                {
                    reservedSeats.Remove(key);
                }
            }
        }

        public static void SynchronizeAllReservations()
        {
            if (Current.Game == null) return;

            // Очистка записей для несуществующих пешек
            CleanupStaleReservations();

            // Синхронизация компонентов стульев с данными пешек
            foreach (Map map in Current.Game.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawns)
                {
                    if (pawn.def == AlienDefOf.SheldonClone && pawn.IsReservedForSitting())
                    {
                        IntVec3 spot = pawn.GetReservedSittingSpot();
                        if (spot.InBounds(map))
                        {
                            // Обновляем компоненты стульев
                            UpdateSeatComponent(pawn, spot);
                        }
                    }
                }
            }
        }
    }
}