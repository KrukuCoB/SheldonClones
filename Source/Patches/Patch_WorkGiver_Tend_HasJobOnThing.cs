using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using System.Reflection;

namespace SheldonClones
{
    // Патч для предотвращения лечения игнорируемых пешек (кроме критических случаев)
    [HarmonyPatch(typeof(WorkGiver_Tend), "HasJobOnThing")]
    public static class Patch_WorkGiver_Tend_HasJobOnThing
    {
        static bool Prefix(WorkGiver_Tend __instance, Pawn pawn, Thing t, bool forced, ref bool __result)
        {
            // Проверяем, является ли врач клоном Шелдона
            if (pawn.def != AlienDefOf.SheldonClone)
                return true; // Обычное поведение для не-клонов

            if (!(t is Pawn patient))
                return true;

            var watcher = Find.World.GetComponent<CompSheldonWatcher>();
            if (watcher == null)
                return true;

            // Проверяем, игнорирует ли клон эту пешку
            if (!watcher.IsPawnIgnored(pawn, patient))
                return true; // Не игнорирует - обычное поведение

            // Игнорирует - проверяем критичность состояния
            if (IsPatientInDeadlyCondition(patient))
            {
                // Критическое состояние - лечим несмотря на игнор
                return true;
            }

            // Не критическое состояние - отказываемся лечить
            __result = false;
            return false;
        }

        // Находится ли пациент в критическом состоянии, угрожающем жизни.
        private static bool IsPatientInDeadlyCondition(Pawn patient)
        {
            if (patient.Dead || patient.health == null)
                return false;

            // Проверяем смертельные состояния
            if (patient.health.summaryHealth.SummaryHealthPercent <= 0.15f) // Менее 15% здоровья
                return true;

            // Проверяем наличие смертельных травм/болезней
            foreach (var hediff in patient.health.hediffSet.hediffs)
            {
                // Проверяем на смертельные стадии болезней
                if (hediff.CurStage != null && hediff.CurStage.deathMtbDays > 0)
                    return true;

                // Проверяем критические травмы
                if (hediff.Bleeding && hediff.BleedRate > 0.7f) // Сильное кровотечение
                    return true;

                // Проверяем инфекции в критической стадии
                if (hediff.def.defName.Contains("Infection") && hediff.Severity > 0.7f)
                    return true;

                // Проверяем критические состояния органов
                if (hediff.Part != null && hediff.Part.def.tags.Contains(BodyPartTagDefOf.BloodPumpingSource) &&
                    hediff.Severity > 0.7f) // Сердце сильно повреждено
                    return true;

                if (hediff.Part != null && hediff.Part.def.tags.Contains(BodyPartTagDefOf.BreathingSource) &&
                    hediff.Severity > 0.7f) // Легкие сильно повреждены
                    return true;
            }

            // Проверяем температуру тела
            var tempHediff = patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia) ??
                            patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Heatstroke);
            if (tempHediff != null && tempHediff.CurStageIndex >= 3) // Критическая стадия
                return true;

            return false;
        }
    }

    // Патч для фактического начала работы
    [HarmonyPatch(typeof(Pawn_JobTracker), "TryTakeOrderedJob")]
    public static class Patch_Pawn_JobTracker_TryTakeOrderedJob
    {
        static bool Prefix(Pawn_JobTracker __instance, Job job, ref bool __result)
        {
            // Получаем pawn через рефлексию безопасно
            var pawn = AccessTools.Field(typeof(Pawn_JobTracker), "pawn").GetValue(__instance) as Pawn;

            // Проверяем только клонов Шелдона
            if (pawn?.def != AlienDefOf.SheldonClone)
                return true;

            // Проверяем, является ли это медицинской работой
            if (job.def != JobDefOf.TendPatient && job.def != JobDefOf.Rescue)
                return true;

            var patient = job.targetA.Thing as Pawn;
            if (patient == null)
                return true;

            var watcher = Find.World.GetComponent<CompSheldonWatcher>();
            if (watcher == null)
                return true;

            // Проверяем игнор
            if (!watcher.IsPawnIgnored(pawn, patient))
                return true;

            // Если игнорирует, но состояние критическое - выполняем
            if (IsPatientInDeadlyCondition(patient))
            {
                // ЗДЕСЬ показываем сообщение только при ФАКТИЧЕСКОМ начале лечения
                Messages.Message($"{pawn.LabelShortCap} оказывает экстренную помощь {patient.LabelShortCap}, несмотря на игнорирование",
                    MessageTypeDefOf.NeutralEvent);
                return true;
            }

            // Отказываемся от работы
            Messages.Message($"{pawn.LabelShortCap} отказывается лечить {patient.LabelShortCap} из-за игнорирования",
                MessageTypeDefOf.NegativeEvent);
            __result = false;
            return false;
        }

        private static bool IsPatientInDeadlyCondition(Pawn patient)
        {
            if (patient.Dead || patient.health == null)
                return false;

            if (patient.health.summaryHealth.SummaryHealthPercent <= 0.15f)
                return true;

            foreach (var hediff in patient.health.hediffSet.hediffs)
            {
                if (hediff.CurStage != null && hediff.CurStage.deathMtbDays > 0)
                    return true;

                if (hediff.Bleeding && hediff.BleedRate > 0.7f)
                    return true;

                if (hediff.def.defName.Contains("Infection") && hediff.Severity > 0.7f)
                    return true;

                if (hediff.Part != null && hediff.Part.def.tags.Contains(BodyPartTagDefOf.BloodPumpingSource) &&
                    hediff.Severity > 0.7f)
                    return true;

                if (hediff.Part != null && hediff.Part.def.tags.Contains(BodyPartTagDefOf.BreathingSource) &&
                    hediff.Severity > 0.7f)
                    return true;
            }

            var tempHediff = patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia) ??
                            patient.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Heatstroke);
            if (tempHediff != null && tempHediff.CurStageIndex >= 3)
                return true;

            return false;
        }
    }

    // Патч для предотвращения назначения медицинских операций
    [HarmonyPatch(typeof(WorkGiver_DoBill), "JobOnThing")]
    public static class Patch_WorkGiver_DoBill_JobOnThing
    {
        static bool Prefix(WorkGiver_DoBill __instance, Pawn pawn, Thing thing, bool forced, ref Job __result)
        {
            // Проверяем только клонов Шелдона
            if (pawn?.def != AlienDefOf.SheldonClone)
                return true;

            if (!(thing is IBillGiver billGiver))
                return true;

            var watcher = Find.World.GetComponent<CompSheldonWatcher>();
            if (watcher == null)
                return true;

            // Проверяем медицинские операции на игнорируемых пешках
            foreach (var bill in billGiver.BillStack.Bills)
            {
                if (bill is Bill_Medical medicalBill && medicalBill.GiverPawn != null)
                {
                    if (watcher.IsPawnIgnored(pawn, medicalBill.GiverPawn))
                    {
                        // Проверяем критичность для медицинских операций
                        if (!IsPatientInDeadlyCondition(medicalBill.GiverPawn))
                        {
                            // Не критично - пропускаем
                            continue;
                        }
                        // Критично - выполняем операцию
                        // Сообщение показываем только при фактическом начале операции
                        Messages.Message($"{pawn.LabelShortCap} проводит экстренную операцию для {medicalBill.GiverPawn.LabelShortCap}",
                            MessageTypeDefOf.NeutralEvent);
                    }
                }
            }

            return true; // Продолжаем обычную логику
        }

        private static bool IsPatientInDeadlyCondition(Pawn patient)
        {
            if (patient.Dead || patient.health == null)
                return false;

            return patient.health.summaryHealth.SummaryHealthPercent <= 0.15f ||
                   patient.health.hediffSet.hediffs.Any(h =>
                       (h.CurStage != null && h.CurStage.deathMtbDays > 0) ||
                       (h.Bleeding && h.BleedRate > 0.7f) ||
                       (h.def.defName.Contains("Infection") && h.Severity > 0.7f));
        }
    }
}