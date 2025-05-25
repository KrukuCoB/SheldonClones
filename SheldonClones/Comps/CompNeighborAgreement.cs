using RimWorld;
using System.Collections.Generic;
using Verse;

namespace SheldonClones
{
    public class CompProperties_NeighborAgreement : CompProperties
    {
        public CompProperties_NeighborAgreement()
        {
            this.compClass = typeof(CompNeighborAgreement);
        }
    }

    public class CompNeighborAgreement : ThingComp
    {
        public HashSet<string> agreedNeighbors = new HashSet<string>();

        private List<Pawn> cachedNeighbors = new List<Pawn>();
        private bool neighborsUpdatedThisSleep = false;
        public List<Pawn> CachedNeighbors => cachedNeighbors;


        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref agreedNeighbors, "agreedNeighbors", LookMode.Value);
            // neighbors кеш не сохраняем — пересчитаем
        }

        public override void CompTickRare()
        {
            base.CompTickRare();

            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned) return;

            // Проверка: спит ли пешка (лежит, выполняет работу "LayDown", и не принудительно)
            if (pawn.InBed() &&
                pawn.CurJobDef == JobDefOf.LayDown &&
                !pawn.CurJob.forceSleep &&
                !neighborsUpdatedThisSleep)
            {
                cachedNeighbors = NeighborAgreementUtility.FindNearbyBedNeighbors(pawn);
                neighborsUpdatedThisSleep = true;
                if (pawn.ownership?.OwnedBed != null
                    && pawn.ownership.OwnedBed.Spawned
                    && pawn.ownership.OwnedBed.GetRoom() != null
                    && !pawn.ownership.OwnedBed.GetRoom().PsychologicallyOutdoors)
                {
                    Log.Message($"[DEBUG] 💤 Обновлены соседи для {pawn.LabelShortCap} во сне: {cachedNeighbors.Count}");
                }
            }

            // Сброс, когда проснулась или вышла из кровати
            if (neighborsUpdatedThisSleep &&
                (!pawn.InBed() || pawn.CurJobDef != JobDefOf.LayDown))
            {
                neighborsUpdatedThisSleep = false;
            }
        }

        public override string CompInspectStringExtra()
        {
            if (cachedNeighbors == null || cachedNeighbors.Count == 0)
                return "Соседи: отсутствуют.";

            string text = "Соседи:";
            foreach (var neighbor in cachedNeighbors)
            {
                bool agreed = HasAgreementWith(neighbor);
                text += $"\n- {neighbor.LabelShort} {(agreed ? "(соглашение)" : "")}";
            }
            return text;
        }



        public List<Pawn> GetNearbyBedNeighbors()
        {
            return cachedNeighbors ?? new List<Pawn>();
        }


        public bool HasAgreementWith(Pawn other)
        {
            return agreedNeighbors.Contains(other.ThingID);
        }

        public void AddAgreement(Pawn other)
        {
            if (!HasAgreementWith(other))
                agreedNeighbors.Add(other.ThingID);
        }
    }


    public static class NeighborAgreementUtility
    {
        public static List<Pawn> FindNearbyBedNeighbors(Pawn pawn)
        {
            List<Pawn> neighbors = new List<Pawn>();
            Building_Bed myBed = pawn.ownership?.OwnedBed;
            if (myBed == null || !myBed.Spawned) return neighbors;

            Room startRoom = myBed.GetRoom();
            if (startRoom == null) return neighbors;

            // ВАЖНО: если кровать на улице — соседи не ищутся!
            if (startRoom.PsychologicallyOutdoors)
            {
                Log.Message($"[DEBUG] {pawn.LabelShortCap} спит на улице — соседи не ищутся.");
                return neighbors;
            }

            Log.Message($"[DEBUG] Старт поиска соседей для: {pawn.LabelShortCap} (комната: {startRoom.ID})");

            // Очередь для обхода
            Queue<Room> queue = new Queue<Room>();
            HashSet<Room> visited = new HashSet<Room>();

            queue.Enqueue(startRoom);
            visited.Add(startRoom);

            while (queue.Count > 0)
            {
                Room current = queue.Dequeue();

                foreach (var region in current.Regions)
                {
                    foreach (var neighborRegion in region.Neighbors)
                    {
                        Room neighborRoom = neighborRegion.Room;
                        if (neighborRoom == null || visited.Contains(neighborRoom)) continue;

                        // Пропускаем улицу
                        if (neighborRoom.PsychologicallyOutdoors)
                        {
                            continue;
                        }

                        visited.Add(neighborRoom);
                        queue.Enqueue(neighborRoom);
                    }
                }
            }

            // Теперь visited содержит все внутренние комнаты здания

            foreach (Pawn other in pawn.Map.mapPawns.FreeColonistsSpawned)
            {
                if (other == pawn) continue;

                Building_Bed otherBed = other.ownership?.OwnedBed;
                if (otherBed == null || !otherBed.Spawned) continue;

                Room otherRoom = otherBed.GetRoom();
                if (otherRoom != null && visited.Contains(otherRoom))
                {
                    neighbors.Add(other);
                }
            }

            return neighbors;
        }
    }
}
