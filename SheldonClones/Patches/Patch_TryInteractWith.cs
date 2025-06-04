using HarmonyLib;
using RimWorld;
using Verse;

namespace SheldonClones
{
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractWith")]
    public static class Patch_TryInteractWith
    {
        public static bool Prefix(Pawn_InteractionsTracker __instance, Pawn recipient, InteractionDef intDef, ref bool __result)
        {
            Pawn initiator = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();

            var watcher = Find.World.GetComponent<GameComponent_SheldonWatcher>();
            if (watcher == null)
                return true;

            // Проверка: получатель игнорирует инициатора
            if (watcher.IsPawnIgnored(recipient, initiator))
            {
                __result = true;
                Log.Message($"[IGNORED] {initiator.Name} попытался взаимодействовать с {recipient.Name}, но {recipient.Name} его игнорирует");
                return false;
            }

            // Проверка: инициатор игнорирует получателя
            if (watcher.IsPawnIgnored(initiator, recipient))
            {
                __result = true;
                Log.Message($"[IGNORED] {initiator.Name} игнорирует {recipient.Name}, поэтому не взаимодействует с ним");
                return false;
            }

            return true;
        }
    }
}
