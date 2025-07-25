using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace SheldonClones
{
    public class CompSleepDisturbanceWatcher : ThingComp
    {
        private int lastCheckTick = 0;

        private List<Pawn> cachedNeighbors = new List<Pawn>();
        private Dictionary<Pawn, int> lastStrikeTicks = new Dictionary<Pawn, int>();    // Словарь, чтобы помнить, кому и когда последний раз выдавался страйк

        // Интервал между страйками одному и тому же нарушителю (в тиках)
        private const int StrikeCooldownTicks = 10000; // ~2.8 часа игрового времени

        public override void CompTick()
        {
            base.CompTick();

            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned) return;

            if (!pawn.Awake() && pawn.InBed() && pawn.CurJobDef == JobDefOf.LayDown)
            {
                if (Find.TickManager.TicksGame - lastCheckTick >= 300)
                {
                    lastCheckTick = Find.TickManager.TicksGame;
                    
                    TryFindDisturbersAndStrike(pawn);   // Пытаемся найти тех, кто мешает спать, и выдать страйк
                }
            }
        }

        private void TryFindDisturbersAndStrike(Pawn sleepingPawn)
        {
            // Получаем компонент соглашения о соседстве (должен быть прикреплён к этой же пешке)
            var neighborComp = sleepingPawn.TryGetComp<CompNeighborAgreement>();
            if (neighborComp == null) return;

            // Получаем список "согласованных соседей"
            var neighbors = neighborComp.CachedNeighbors;
            if (neighbors == null || neighbors.Count() == 0) return;

            // Получаем комнату, где спит клон
            Room room = sleepingPawn.GetRoom();
            if (room == null) return;

            // Получаем глобальный компонент, отвечающий за выдачу страйков
            CompSheldonWatcher watcher = Find.World.GetComponent<CompSheldonWatcher>();
            if (watcher == null) return;

            // Перебираем всех пешек в комнате (и смежных с ней)
            foreach (Pawn other in room.ContainedAndAdjacentThings.OfType<Pawn>())
            {
                // Пропускаем самого клона и тех, кто не является его соседом
                if (other == sleepingPawn || !neighbors.Contains(other)) continue;

                // Пропускаем, если пешка мертва или не на карте
                if (!other.Spawned || other.Dead) continue;

                // Проверяем, не спит ли пешка и не лежит ли в кровати (значит, она активна и мешает)
                if (other.Awake() && !other.InBed())
                {
                    int currentTick = Find.TickManager.TicksGame;

                    // Проверка кулдауна: если страйк уже недавно выдавался — пропускаем
                    if (lastStrikeTicks.TryGetValue(other, out int lastTick))
                    {
                        if (currentTick - lastTick < StrikeCooldownTicks)
                            continue; // ещё не прошло достаточно времени, чтобы снова выдать страйк
                    }

                    // Запоминаем, когда выдали страйк
                    lastStrikeTicks[other] = currentTick;

                    // Выдаем страйк
                    watcher.ApplyStrikeToInitiator(sleepingPawn, other);
                }
            }
        }
    }
}