using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SheldonClones
{

    [HarmonyPatch(typeof(Toils_Tend), "FinalizeTend")]
    public static class Toils_Tend_FinalizeTend_Patch
    {
        static void Postfix(Pawn patient, Toil __result)
        {
            // Добавляем дополнительную логику к toil
            Toil originalToil = __result;

            // Сохраняем оригинальное действие
            System.Action originalAction = originalToil.initAction;

            // Заменяем на наше с дополнительной логикой
            originalToil.initAction = delegate
            {
                // Сначала выполняем оригинальное лечение
                originalAction();

                // Затем наша логика для клонов Шелдона
                Pawn doctor = originalToil.actor;

                // Проверяем, что пациент - клон Шелдона
                if (patient?.def != AlienDefOf.SheldonClone)
                    return;

                // Проверяем, что клон болеет
                if (!patient.health.HasHediffsNeedingTend())
                    return;

                // Проверяем, что у врача нет противного голоса
                if (doctor.story?.traits?.HasTrait(TraitDefOf.AnnoyingVoice) == true)
                    return;

                // Запускаем нашу логику с шансом
                if (Rand.Chance(0.75f)) // 75% шанс
                {
                    TriggerKittenSongRequest(doctor, patient);
                }
            };
        }

        private static void TriggerKittenSongRequest(Pawn doctor, Pawn patient)
        {
            // Добавляем мысль врачу о странной просьбе
            doctor.needs.mood.thoughts.memories.TryGainMemory(AlienDefOf.SheldonWeirdRequest);

            // Рассчитываем шанс согласия на песню с учетом черт характера
            float songChance = CalculateSongChance(doctor);

            // Запускаем интеракцию между пациентом (инициатор) и врачом (получатель)
            InteractionDef songRequestInteraction = DefDatabase<InteractionDef>.GetNamed("SheldonSickRequest");
            patient.interactions.TryInteractWith(doctor, songRequestInteraction);

            // Если врач "согласился" (с учетом черт характера)
            if (Rand.Chance(songChance))
            {
                // Положительная мысль Шелдону
                patient.needs.mood.thoughts.memories.TryGainMemory(AlienDefOf.SheldonKittenSongSatisfied);
                // Добавляем бафф к лечению
                Hediff healingBuff = HediffMaker.MakeHediff(AlienDefOf.SheldonKittenSongHealing, patient);
                healingBuff.Severity = 1f;
                patient.health.AddHediff(healingBuff);
                // Логируем событие
                Messages.Message($"{doctor.LabelShortCap} спел {patient.LabelShortCap} песню о пушистом котёнке во время лечения!", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                // Негативная мысль Шелдону за отказ
                patient.needs.mood.thoughts.memories.TryGainMemory(AlienDefOf.SheldonKittenSongRefused);
            }
        }

        private static float CalculateSongChance(Pawn doctor)
        {
            float baseChance = 0.5f; // Базовый шанс 50%

            // Проверяем черту Kind - добрые всегда согласятся
            if (doctor.story?.traits?.HasTrait(TraitDefOf.Kind) == true)
            {
                return 1.0f; // 100% шанс
            }

            // Проверяем черту Abrasive - грубые не согласятся, кроме клонов Шелдона
            if (doctor.story?.traits?.HasTrait(TraitDefOf.Abrasive) == true &&
                doctor.def != AlienDefOf.SheldonClone)
            {
                return 0.0f; // 0% шанс
            }

            // Дополнительные модификаторы для других черт характера
            float chanceModifier = 1.0f;

            // Психопаты менее склонны к эмпатии
            if (doctor.story?.traits?.HasTrait(TraitDefOf.Psychopath) == true)
            {
                chanceModifier *= 0.2f; // Снижаем шанс на 80%
            }

            // Проверяем черту NaturalMood (оптимист/пессимист)
            Trait naturalMoodTrait = doctor.story?.traits?.GetTrait(DefDatabase<TraitDef>.GetNamed("NaturalMood"));
            if (naturalMoodTrait != null)
            {
                switch (naturalMoodTrait.Degree)
                {
                    case 2: // сангвиник
                        chanceModifier *= 1.8f; // Увеличиваем шанс на 80%
                        break;
                    case 1: // оптимист
                        chanceModifier *= 1.5f; // Увеличиваем шанс на 50%
                        break;
                    case -1: // пессимист
                        chanceModifier *= 0.7f; // Снижаем шанс на 30%
                        break;
                    case -2: // депрессивный
                        chanceModifier *= 0.4f; // Снижаем шанс на 60%
                        break;
                }
            }

            // Проверяем социальные навыки - высокие социальные навыки увеличивают шанс
            if (doctor.skills?.GetSkill(SkillDefOf.Social)?.Level >= 10)
            {
                chanceModifier *= 1.3f; // Увеличиваем шанс на 30%
            }

            return Mathf.Clamp01(baseChance * chanceModifier);
        }
    }
}