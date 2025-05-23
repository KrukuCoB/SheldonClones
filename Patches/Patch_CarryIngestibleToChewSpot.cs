using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SheldonClones
{
    [HarmonyPatch(typeof(Toils_Ingest))]
    [HarmonyPatch(nameof(Toils_Ingest.CarryIngestibleToChewSpot))]
    public static class Patch_CarryIngestibleToChewSpot
    {
        static bool Prefix(Pawn pawn, TargetIndex ingestibleInd, ref Toil __result)
        {
            // Только для клонов Шелдона
            if (pawn.def != AlienDefOf.SheldonClone)
                return true;

            // Ищем компонент «моего» стула
            var myComp = pawn.Map.listerThings.AllThings
                .Select(t => t.TryGetComp<CompSheldonSeatAssignable>())
                .FirstOrDefault(c => c != null && c.AssignedAnything(pawn));
            if (myComp == null)
                return true;

            // Вспомогательная версия TryFindCell, которая отбрасывает
            // любое занятие клетки другим пешком
            bool TryFindCell(Thing thing, out IntVec3 c)
            {
                foreach (var pos in thing.OccupiedRect())
                {
                    // клетку надо резервировать
                    if (!pawn.CanReserveSittableOrSpot(pos))
                        continue;
                    // и на ней не должно быть другого Pawn
                    if (pos.GetThingList(pawn.Map).OfType<Pawn>().Any(p => p != pawn))
                        continue;
                    c = pos;
                    return true;
                }
                c = IntVec3.Invalid;
                return false;
            }
            // Не трогаем первый и второй блоки, потому что это именно та логика, которая должна происходить

            // === Блок 1: Если есть свободная и физически пустая клетка — садимся на неё
            if (TryFindCell(myComp.parent, out var freeSpot))
            {
                var toil = ToilMaker.MakeToil("Sheldon_SitAtOwnSeat");
                toil.initAction = () =>
                {
                    pawn.ReserveSittableOrSpot(freeSpot, pawn.jobs.curJob);
                    pawn.Map.pawnDestinationReservationManager.Reserve(pawn, pawn.jobs.curJob, freeSpot);
                    pawn.pather.StartPath(freeSpot, PathEndMode.OnCell);
                };
                toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                __result = toil;
                return false; // заменяем оригинальный Toil
            }

            // === Блок 2: Все клетки заняты — ищем, кто там сидит, и инициируем интеракцию-выселение
            Pawn occupant = null;
            foreach (var pos in myComp.parent.OccupiedRect())
            {
                occupant = pos.GetThingList(pawn.Map)
                              .OfType<Pawn>()
                              .FirstOrDefault(p => p != pawn);
                if (occupant != null)
                    break;
            }
            if (occupant != null)
            {
                var evictionDef = DefDatabase<InteractionDef>.GetNamed("SheldonWarnedForSittingInMySpot");

                // ── Если жертва в ментальном состоянии (например, в обжорстве) ──
                if (occupant.MentalState != null)
                {
                    // 1) Выдаём страйк через нашу механику
                    var watcher = Find.World.GetComponent<GameComponent_SheldonWatcher>();
                    watcher.ApplyStrikeToInitiator(pawn, occupant);

                    // 2) Принудительно выводим из любого MentalState
                    //    (RecoverFromState очистит и выведет пешку из обжорства)
                    occupant.MentalState.RecoverFromState();
                    Sheldon_Utils.ForceEvict(pawn, occupant);

                    // 3) Выкатываем интеракцию
                    pawn.interactions.TryInteractWith(occupant, evictionDef);
                    Log.Message($"[SheldonClones] {pawn.LabelShort} выгоняет {occupant.LabelShort} со своего места!");
                }

                // ── ДАЛЕЕ: стандартная эвикт-схема ──

                // 1) Евикт-интеракция
                pawn.interactions.TryInteractWith(occupant, evictionDef);
                Log.Message($"[SheldonClones] {pawn.LabelShort} выгоняет {occupant.LabelShort} со своего места!");

                // 2) Гарантированно прерываем текущее действие у сидящего
                Sheldon_Utils.ForceEvict(pawn, occupant);


                // 3) Новый Toil: сразу ведём клона к его собственному стулу
                var evictToil = ToilMaker.MakeToil("Sheldon_EvictAndSeat");
                evictToil.initAction = () =>
                {
                    // просто берём центральную клетку стула
                    var seatCell = myComp.parent.Position;

                    // резервируем именно эту клетку (ванильный Toil позже сядет)
                    pawn.ReserveSittableOrSpot(seatCell, pawn.jobs.curJob);
                    pawn.Map.pawnDestinationReservationManager.Reserve(pawn, pawn.jobs.curJob, seatCell);
                    pawn.pather.StartPath(seatCell, PathEndMode.OnCell);
                };
                evictToil.defaultCompleteMode = ToilCompleteMode.PatherArrival;

                __result = evictToil;
                return false;  // подменяем ванильный Toil
            }

            // === Блок 3: Обычное поведение первого поедания (не трогаем) ===
            return true;
        }

        // Постфикс оставляем без изменений — он один раз назначает стул при первом приёме пищи
        static void Postfix(Toil __result, Pawn pawn, TargetIndex ingestibleInd)
        {
            if (pawn.def != AlienDefOf.SheldonClone) return;
            if (HasAssignedSeat(pawn)) return;

            __result.AddFinishAction(() =>
            {
                var pos = pawn.Position;
                foreach (var thing in pos.GetThingList(pawn.Map))
                {
                    var comp = thing.TryGetComp<CompSheldonSeatAssignable>();
                    if (comp != null && comp.AssignedPawnsForReading.Count == 0)
                    {
                        comp.TryAssignPawn(pawn);
                        Log.Message($"[SheldonClones] {pawn.LabelShort} присвоил себе {thing.LabelShort} на {thing.Position}");
                        break;
                    }
                }
            });
        }

        private static bool HasAssignedSeat(Pawn pawn)
        {
            return pawn.Map.listerThings.AllThings
                       .Select(x => x.TryGetComp<CompSheldonSeatAssignable>())
                       .Where(c => c != null)
                       .Any(c => c.AssignedAnything(pawn));
        }
    }
}
