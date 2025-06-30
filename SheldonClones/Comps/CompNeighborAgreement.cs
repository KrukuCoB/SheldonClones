using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
        // Согласованные (подписанные) соглашения (ThingID соседей)
        public HashSet<string> agreedNeighbors = new HashSet<string>();
        // Кеш соседей и флаг обновления за текущий сон
        private List<Pawn> cachedNeighbors = new List<Pawn>();
        private bool neighborsUpdatedThisSleep = false;
        public List<Pawn> CachedNeighbors => cachedNeighbors;

        // Сериализация: сохраняем соглашения, а также кеш соседей и флаг
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref agreedNeighbors, "agreedNeighbors", LookMode.Value);
            Scribe_Collections.Look(ref cachedNeighbors, "cachedNeighbors", LookMode.Reference);
            Scribe_Values.Look(ref neighborsUpdatedThisSleep, "neighborsUpdatedThisSleep", false);
        }

        // Редкий тик: обновляем соседей во время сна
        public override void CompTickRare()
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned)
                return;

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

            // Сброс флага при пробуждении
            if (neighborsUpdatedThisSleep && (!pawn.InBed() || pawn.CurJobDef != JobDefOf.LayDown))
            {
                neighborsUpdatedThisSleep = false;
            }
        }


        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // сначала отдаём все гизмо от базовых компонентов
            foreach (var g in base.CompGetGizmosExtra())
                yield return g;

            var pawn = parent as Pawn;
            if (pawn != null && pawn.def == AlienDefOf.SheldonClone)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Информация о клоне",
                    defaultDesc = "Показать соседей, страйки и соглашения клона.",
                    icon = ContentFinder<Texture2D>.Get("UI/Icons/Info", true),
                    action = () => Find.WindowStack.Add(new Dialog_SheldonInfo(pawn))
                };
            }
        }

        // Получить текущий список соседей
        public List<Pawn> GetNearbyBedNeighbors() => cachedNeighbors ?? new List<Pawn>();


        // Проверка соглашения
        public bool HasAgreementWith(Pawn other) => agreedNeighbors.Contains(other.ThingID);

        // Добавить соглашение
        public void AddAgreement(Pawn other)
        {
            var pawn = parent as Pawn;
            // Добавляем соглашение для этого клона
            if (!HasAgreementWith(other))
            {
                agreedNeighbors.Add(other.ThingID);
                // И добавляем взаимное соглашение для другого клона
                var otherComp = other.TryGetComp<CompNeighborAgreement>();
                if (otherComp != null && pawn != null && !otherComp.HasAgreementWith(pawn))
                {
                    otherComp.AddAgreement(pawn);
                }
            }
        }

        // Удалить соглашение
        public void RemoveAgreement(Pawn other)
        {
            if (HasAgreementWith(other))
                agreedNeighbors.Remove(other.ThingID);
        }
    }

    // Утилита для поиска соседей по комнатам
    public static class NeighborAgreementUtility
    {
        public static List<Pawn> FindNearbyBedNeighbors(Pawn pawn)
        {
            List<Pawn> neighbors = new List<Pawn>();
            Building_Bed myBed = pawn.ownership?.OwnedBed;
            if (myBed == null || !myBed.Spawned) return neighbors;

            Room startRoom = myBed.GetRoom();
            if (startRoom == null) return neighbors;

            if (startRoom.PsychologicallyOutdoors)
            {
                Log.Message($"[DEBUG] {pawn.LabelShortCap} спит на улице — соседи не ищутся.");
                return neighbors;
            }

            Log.Message($"[DEBUG] Старт поиска соседей для: {pawn.LabelShortCap} (комната: {startRoom.ID})");

            var queue = new Queue<Room>();
            var visited = new HashSet<Room> { startRoom };
            queue.Enqueue(startRoom);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var region in current.Regions)
                {
                    foreach (var neighborRegion in region.Neighbors)
                    {
                        var neighborRoom = neighborRegion.Room;
                        if (neighborRoom == null || visited.Contains(neighborRoom)) continue;
                        if (neighborRoom.PsychologicallyOutdoors) continue;
                        visited.Add(neighborRoom);
                        queue.Enqueue(neighborRoom);
                    }
                }
            }

            foreach (var other in pawn.Map.mapPawns.FreeColonistsSpawned)
            {
                if (other == pawn) continue;
                var otherBed = other.ownership?.OwnedBed;
                if (otherBed == null || !otherBed.Spawned) continue;
                var otherRoom = otherBed.GetRoom();
                if (otherRoom != null && visited.Contains(otherRoom))
                    neighbors.Add(other);
            }

            return neighbors;
        }
    }
}
