using RimWorld;
using Verse;

namespace SheldonClones
{
    [DefOf]
    public static class AlienDefOf
    {
        static AlienDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AlienDefOf));
        }

        public static ThingDef SheldonClone; // Определяем расу клонов Шелдона
        public static ThoughtDef SheldonAnnoyingInteraction;
        public static ThoughtDef SheldonWeirdRequest; // Мысль для лечащего о странной просьбе
        public static ThoughtDef SheldonKittenSongSatisfied; // Мысль Шелдона когда ему спели
        public static ThoughtDef SheldonKittenSongRefused; // Мысль Шелдона когда отказались петь
        public static InteractionDef SheldonSickRequest; // Interaction для просьбы о песне
        public static HediffDef SheldonKittenSongHealing; // Временный бафф лечения
    }
}