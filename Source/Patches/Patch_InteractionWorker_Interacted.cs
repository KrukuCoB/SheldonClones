using HarmonyLib;
using RimWorld;
using Verse;

namespace SheldonClones
{
    [HarmonyPatch(typeof(InteractionWorker), "Interacted")]
    public static class InteractionWorker_Interacted_Patch
    {
        public static void Postfix(Pawn initiator, Pawn recipient)
        {
            // Проверяем, участвует ли клон Шелдона
            if (initiator.def.defName == "SheldonClone" || recipient.def.defName == "SheldonClone")
            {
                ApplySheldonEffects(initiator, recipient);
            }
        }

        private static void ApplySheldonEffects(Pawn initiator, Pawn recipient)
        {
            if (Rand.Chance(0.8f)) // 80% шанс
            {
                if (initiator.def.defName == "SheldonClone" && recipient.def.defName != "SheldonClone")
                {
                    recipient.needs.mood.thoughts.memories.TryGainMemory(
                        AlienDefOf.SheldonAnnoyingInteraction,
                        initiator
                    );
                }
                else if (recipient.def.defName == "SheldonClone" && initiator.def.defName != "SheldonClone")
                {
                    initiator.needs.mood.thoughts.memories.TryGainMemory(
                        AlienDefOf.SheldonAnnoyingInteraction,
                        recipient
                    );
                }
            }
        }
    }
}
