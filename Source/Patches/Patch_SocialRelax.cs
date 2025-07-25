using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Collections.Generic;

namespace SheldonClones
{
    [HarmonyPatch(typeof(JoyGiver_SocialRelax), nameof(JoyGiver_SocialRelax.TryGiveJob))]
    [HarmonyPatch(new[] { typeof(Pawn) })]
    public static class Patch_SocialRelax_TryGiveJob
    {
        static void Postfix(ref Job __result, Pawn pawn)
        {
            if (__result == null)
                return;
            var processed = SocialRelaxSeatAssigner.ProcessSocialJob(__result, pawn);
            if (processed != null)
                __result = processed;
        }
    }

    [HarmonyPatch(typeof(JoyGiver_SocialRelax), nameof(JoyGiver_SocialRelax.TryGiveJobInGatheringArea))]
    [HarmonyPatch(new[] { typeof(Pawn), typeof(IntVec3), typeof(float) })]
    public static class Patch_SocialRelax_TryGiveJobInGatheringArea
    {
        static void Postfix(ref Job __result, Pawn pawn, IntVec3 gatheringSpot, float maxRadius)
        {
            if (__result == null)
                return;
            var processed = SocialRelaxSeatAssigner.ProcessSocialJob(__result, pawn);
            if (processed != null)
                __result = processed;
        }
    }

    static class SocialRelaxSeatAssigner
    {
        // Словарь для отслеживания попыток выселения при социальном отдыхе
        private static readonly Dictionary<Pawn, int> socialEvictionAttempts = new Dictionary<Pawn, int>();
        private const int MAX_SOCIAL_EVICTION_ATTEMPTS = 2;
        private static readonly Dictionary<IntVec3, int> temporaryReservations = new Dictionary<IntVec3, int>();
        private static int currentTick = 0;

        public static Job ProcessSocialJob(Job job, Pawn pawn)
        {
            if (job == null)
                return null;

            // Очищаем устаревшие записи о выселениях
            Sheldon_Utils.CleanupExpiredEvictions();

            // ── 1) Для клона со своим стулом: приоритет своему месту ──
            if (pawn.def == AlienDefOf.SheldonClone)
            {
                return HandleSheldonClone(job, pawn);
            }
            else
            {
                // ── 2) Для обычных пешек: улучшенная логика поиска ──
                return HandleRegularPawn(job, pawn);
            }
        }

        private static Job HandleSheldonClone(Job job, Pawn pawn)
        {
            var seatResult = Sheldon_SeatFinder.FindSeatForSheldonClone(pawn, requireTable: true);

            if (!seatResult.IsValid)
            {
                Log.Message($"[SocialRelax] Не найдено место со столом для {pawn.LabelShort}, ищем обычное место");
                return HandleRegularPawn(job, pawn);
            }

            if (seatResult.isOwnSeat && seatResult.needsEviction)
            {
                // Проверяем лимит попыток выселения
                int attempts = GetSocialEvictionAttempts(pawn);
                if (attempts >= MAX_SOCIAL_EVICTION_ATTEMPTS)
                {
                    Log.Message($"[SocialRelax] {pawn.LabelShort} превысил лимит попыток выселения, ищем альтернативу");
                    ResetSocialEvictionAttempts(pawn);
                    return HandleRegularPawn(job, pawn);
                }

                IncrementSocialEvictionAttempts(pawn);

                if (seatResult.occupant != null)
                {
                    // Выполняем выселение
                    var def = DefDatabase<InteractionDef>.GetNamed("SheldonWarnedForSittingInMySpot");
                    pawn.interactions.TryInteractWith(seatResult.occupant, def);
                    Sheldon_Utils.ForceEvict(pawn, seatResult.occupant);
                    Log.Message($"[SocialRelax] {pawn.LabelShort} выгоняет {seatResult.occupant.LabelShort} со своего места!");
                }
            }
            else if (seatResult.isOwnSeat)
            {
                Log.Message($"[SocialRelax] {pawn.LabelShort} использует свое свободное место");
                ResetSocialEvictionAttempts(pawn);
            }
            else
            {
                Log.Message($"[SocialRelax] {pawn.LabelShort} использует альтернативное место: {seatResult.chair.LabelShort}");
            }

            // Устанавливаем цели в job
            if (seatResult.table != null)
                job.SetTarget(TargetIndex.A, seatResult.table);

            job.targetB = new LocalTargetInfo(seatResult.position);
            return job;
        }

        private static Job HandleRegularPawn(Job job, Pawn pawn)
        {
            var seatResult = Sheldon_SeatFinder.FindSeatForRegularPawn(pawn, requireTable: true);

            if (!seatResult.IsValid)
                return job; // Возвращаем оригинальный job

            // Проверяем возможность резервации
            if (!CanReservePosition(pawn, seatResult.chair, seatResult.position))
            {
                Log.Warning($"[SocialRelax] Не удалось зарезервировать позицию {seatResult.position}");
                return null;
            }

            // Временно резервируем позицию
            ReserveTemporarily(seatResult.position);

            // Устанавливаем цели в job
            if (seatResult.table != null)
                job.SetTarget(TargetIndex.A, seatResult.table);

            job.targetB = new LocalTargetInfo(seatResult.position);
            return job;
        }

        // Предотвращает выбор уже забронированной позиции
        private static bool IsTemporarilyReserved(IntVec3 position)
        {
            return temporaryReservations.ContainsKey(position) &&
                   temporaryReservations[position] >= Find.TickManager.TicksGame - 180; // 3 секунды
        }

        // Временно резервируем позицию на 3 секунды
        private static void ReserveTemporarily(IntVec3 position)
        {
            temporaryReservations[position] = Find.TickManager.TicksGame;
        }

        // Автоматическая очистка устаревших резерваций
        private static void CleanupTemporaryReservations()
        {
            int currentTick = Find.TickManager.TicksGame;
            var expiredKeys = temporaryReservations.Where(kvp => kvp.Value < currentTick - 60).Select(kvp => kvp.Key).ToList();
            foreach (var key in expiredKeys)
            {
                temporaryReservations.Remove(key);
            }
        }

        // Проверка возможности резервации конкретной позиции
        private static bool CanReservePosition(Pawn pawn, Thing chair, IntVec3 position)
        {
            // Проверяем, что можем зарезервировать стул
            if (!pawn.CanReserve(chair))
                return false;

            // Проверяем, что можем зарезервировать конкретную позицию
            if (!pawn.CanReserveSittableOrSpot(position))
                return false;

            // Проверяем, что позиция не занята физически
            var occupants = position.GetThingList(pawn.Map).OfType<Pawn>().Where(p => p != pawn);
            if (occupants.Any())
                return false;

            return true;
        }

        // Управление счетчиком попыток выселения для социального отдыха
        private static int GetSocialEvictionAttempts(Pawn pawn)
        {
            return socialEvictionAttempts.TryGetValue(pawn, out int attempts) ? attempts : 0;
        }

        private static void IncrementSocialEvictionAttempts(Pawn pawn)
        {
            socialEvictionAttempts[pawn] = GetSocialEvictionAttempts(pawn) + 1;
        }

        private static void ResetSocialEvictionAttempts(Pawn pawn)
        {
            socialEvictionAttempts.Remove(pawn);
        }

        public static void ClearAllSocialEvictionAttempts() // Очищает все накопленные попытки выселения при SocialRelax
        {
            socialEvictionAttempts.Clear();
        }

        public static void OnPawnActivityChanged(Pawn pawn)
        {
            // Сбрасываем счетчики, если пешка переключилась на другую активность
            ResetSocialEvictionAttempts(pawn);
        }
    }
}