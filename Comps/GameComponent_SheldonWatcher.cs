using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;

namespace SheldonClones
{
    public class GameComponent_SheldonWatcher : WorldComponent
    {
        private HashSet<ThoughtReference> processedThoughts = new HashSet<ThoughtReference>();  // Список обработанных мыслей
        private Dictionary<Pawn, List<Pawn>> ignoredPawns = new Dictionary<Pawn, List<Pawn>>(); // Игнорируемые пешки

        public GameComponent_SheldonWatcher(World world) : base(world) { }

        public override void FinalizeInit()
        {
            if (processedThoughts == null)
                processedThoughts = new HashSet<ThoughtReference>();

            processedThoughts.Clear();

            if (ignoredPawns == null)
                ignoredPawns = new Dictionary<Pawn, List<Pawn>>();

            ignoredPawns.Clear();
        }

        public override void WorldComponentTick()
        {
            if (Find.TickManager.TicksGame % 500 == 0) // Раз в 500 тиков (~8 секунд)
            {
                CheckThoughts();
            }
        }

        private void CheckThoughts()
        {
            foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonistsSpawned)
            {
                if (pawn.def == AlienDefOf.SheldonClone) // Проверяем только клонов Шелдона
                {
                    var thoughts = pawn.needs?.mood?.thoughts?.memories;
                    if (thoughts != null)
                    {
                        List<Thought_Memory> thoughtsToRemove = new List<Thought_Memory>(); // Список для удаления

                        foreach (var thought in thoughts.Memories)
                        {
                            if ((thought.def == ThoughtDef.Named("Insulted") ||
                                 thought.def == ThoughtDef.Named("ForcedMeToTakeDrugs") ||
                                 thought.def == ThoughtDef.Named("ForcedMeToTakeLuciferium") ||
                                 thought.def == ThoughtDef.Named("HarmedMe") ||
                                 thought.def == ThoughtDef.Named("HadAngeringFight")) &&
                                !processedThoughts.Contains(new ThoughtReference(thought, pawn))) // Проверяем, обрабатывали ли уже этот инцидент
                            {
                                ApplyStrikeToInitiator(pawn, thought.otherPawn);
                                processedThoughts.Add(new ThoughtReference(thought, pawn)); // Помечаем как обработанное
                                thoughtsToRemove.Add(thought);  // Добавляем в список на удаление
                            }
                        }

                        // Удаляем мысли после обработки, чтобы они больше не учитывались
                        foreach (var thought in thoughtsToRemove)
                        {
                            thoughts.RemoveMemory(thought);
                        }
                    }
                }
            }

            // Очищаем processedThoughts от тех, которых уже нет у пешек
            processedThoughts.RemoveWhere(tr =>
            {
                Pawn owner = Find.Maps.SelectMany(m => m.mapPawns.AllPawnsSpawned)
                                          .FirstOrDefault(p => p.ThingID == tr.pawnId);
                if (owner == null || owner.needs?.mood?.thoughts?.memories == null)
                    return true;

                return !owner.needs.mood.thoughts.memories.Memories.Any(th =>
                    th.def.defName == tr.thoughtDefName &&
                    th.otherPawn?.ThingID == tr.otherPawnId);
            });
        }

        public void ApplyStrikeToInitiator(Pawn sheldonClone, Pawn initiator)
        {
            if (initiator == null || sheldonClone == null)
                return;

            HediffDef strikeHediff = HediffDef.Named("SheldonStrike");

            // Ищем, есть ли уже страйк от этого конкретного клона
            List<Hediff_SheldonStrike> existingStrikes = new List<Hediff_SheldonStrike>();
            initiator.health.hediffSet.GetHediffs(ref existingStrikes, h => h is Hediff_SheldonStrike);

            Hediff_SheldonStrike existingStrike = existingStrikes
                .FirstOrDefault(h => h.sheldonName == sheldonClone.Label);

            if (existingStrike != null)
            {
                // Если страйк уже есть, увеличиваем уровень (не больше 3)
                if (existingStrike.Severity < 3)
                {
                    existingStrike.Severity += 1;
                }

                // Если страйков 3, добавляем в список игнорируемых
                if (existingStrike.Severity >= 3)
                {
                    IgnorePawn(sheldonClone, initiator);
                }
            }
            else
            {
                // Создаем новый страйк с указанием имени Шелдона
                Hediff_SheldonStrike newStrike = (Hediff_SheldonStrike)HediffMaker.MakeHediff(strikeHediff, initiator);
                newStrike.Severity = 1;
                newStrike.sheldonName = sheldonClone.Label; // Указываем, кто выдал страйк
                initiator.health.AddHediff(newStrike);
            }

            Log.Warning($"{initiator.Name} получил Страйк (Текущая стадия: {existingStrike?.Severity ?? 1}) от {sheldonClone.Name}!");
            Messages.Message($"{initiator.LabelShortCap} получил страйк от {sheldonClone.LabelShortCap}. Текущая стадия: {existingStrike?.Severity ?? 1}", MessageTypeDefOf.NeutralEvent);
        }

        public void IgnorePawn(Pawn sheldonClone, Pawn target)
        {
            if (!ignoredPawns.ContainsKey(sheldonClone))
            {
                ignoredPawns[sheldonClone] = new List<Pawn>();
            }
            ignoredPawns[sheldonClone].Add(target);
        }

        public bool IsPawnIgnored(Pawn sheldonClone, Pawn target)
        {
            if (sheldonClone == null || target == null)
                return false;

            return ignoredPawns.TryGetValue(sheldonClone, out var ignoredList) && ignoredList.Contains(target);
        }


        private List<Pawn> tmpIgnoredPawnsKeys;
        private List<List<Pawn>> tmpIgnoredPawnsValues;

        public override void ExposeData()
        {
            base.ExposeData();

            // Сохраняем обработанные мысли
            Scribe_Collections.Look(ref processedThoughts, "processedThoughts", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var keys = ignoredPawns?.Keys.ToList() ?? new List<Pawn>();
                var values = ignoredPawns?.Values.Select(list => list.ToList()).ToList() ?? new List<List<Pawn>>();

                bool hasKeys = keys.Count > 0;
                bool hasValues = values.Count > 0;

                Scribe_Values.Look(ref hasKeys, "ignoredPawns_keys_present", false);
                Scribe_Values.Look(ref hasValues, "ignoredPawns_values_present", false);

                if (hasKeys && hasValues)
                {
                    Scribe_Collections.Look(ref keys, "ignoredPawns_keys", LookMode.Reference);
                    Scribe_Collections.Look(ref values, "ignoredPawns_values", LookMode.Reference);
                }
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                bool hasKeys = false;
                bool hasValues = false;

                Scribe_Values.Look(ref hasKeys, "ignoredPawns_keys_present", false);
                Scribe_Values.Look(ref hasValues, "ignoredPawns_values_present", false);

                if (hasKeys && hasValues)
                {
                    Scribe_Collections.Look(ref tmpIgnoredPawnsKeys, "ignoredPawns_keys", LookMode.Reference);
                    Scribe_Collections.Look(ref tmpIgnoredPawnsValues, "ignoredPawns_values", LookMode.Reference);
                }
            }
            else if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ignoredPawns = new Dictionary<Pawn, List<Pawn>>();

                if (tmpIgnoredPawnsKeys != null && tmpIgnoredPawnsValues != null &&
                    tmpIgnoredPawnsKeys.Count == tmpIgnoredPawnsValues.Count && tmpIgnoredPawnsKeys.Count > 0)
                {
                    for (int i = 0; i < tmpIgnoredPawnsKeys.Count; i++)
                    {
                        if (tmpIgnoredPawnsKeys[i] != null && tmpIgnoredPawnsValues[i] != null)
                        {
                            ignoredPawns[tmpIgnoredPawnsKeys[i]] = new List<Pawn>(tmpIgnoredPawnsValues[i]);
                        }
                    }
                }

                tmpIgnoredPawnsKeys = null;
                tmpIgnoredPawnsValues = null;
            }
            if (ignoredPawns == null)
                ignoredPawns = new Dictionary<Pawn, List<Pawn>>();
        }

        public class ThoughtReference : IExposable
        {
            public string thoughtDefName;
            public string pawnId;
            public string otherPawnId;

            public ThoughtReference() { }

            public ThoughtReference(Thought_Memory thought, Pawn owner)
            {
                thoughtDefName = thought.def.defName;
                pawnId = owner?.ThingID;
                otherPawnId = thought.otherPawn?.ThingID;
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref thoughtDefName, "thoughtDefName");
                Scribe_Values.Look(ref pawnId, "pawnId");
                Scribe_Values.Look(ref otherPawnId, "otherPawnId");
            }

            public override bool Equals(object obj)
            {
                if (obj is ThoughtReference other)
                    return thoughtDefName == other.thoughtDefName && pawnId == other.pawnId && otherPawnId == other.otherPawnId;
                return false;
            }

            public override int GetHashCode()
            {
                return Gen.HashCombine(
                    Gen.HashCombine(thoughtDefName?.GetHashCode() ?? 0, pawnId?.GetHashCode() ?? 0),
                    otherPawnId?.GetHashCode() ?? 0
                );
            }
        }
    }
}
