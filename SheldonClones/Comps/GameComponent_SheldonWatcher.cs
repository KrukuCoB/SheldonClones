using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;

namespace SheldonClones
{
    public class GameComponent_SheldonWatcher : WorldComponent
    {
        private HashSet<ThoughtReference> processedThoughts = new HashSet<ThoughtReference>();      // Список обработанных мыслей
        private List<IgnoredEntry> ignoredEntries = new List<IgnoredEntry>();
        [Unsaved(false)]                                                                            // сам словарь, его хранить в памяти, но не сериализовать
        private Dictionary<Pawn, List<Pawn>> ignoredPawns = new Dictionary<Pawn, List<Pawn>>();     // Игнорируемые пешки
        private List<StrikeEntry> strikeEntries = new List<StrikeEntry>();                          // список для сериализации страйков у клона
        [Unsaved(false)]
        private Dictionary<Pawn, List<Pawn>> victimsByIssuer = new Dictionary<Pawn, List<Pawn>>();  // в рантайме — словарь «автор → список жертв»


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

        public List<Pawn> GetVictimsByIssuer(Pawn sheldon)
        {
            if (sheldon == null)
                return new List<Pawn>();
            if (victimsByIssuer != null && victimsByIssuer.TryGetValue(sheldon, out var list))
                return list;
            return new List<Pawn>();
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

            Hediff_SheldonStrike strike;
            if (existingStrike != null)
            {
                if (existingStrike.Severity >= 3)
                {
                    // сброс таймера на 3-м уровне
                    existingStrike.TryGetComp<HediffComp_SheldonStrikeDecay>()?.ResetDecayTimer();
                    return;
                }
                existingStrike.Severity += 1;
                if (existingStrike.Severity >= 3)
                    IgnorePawn(sheldonClone, initiator);

                strike = existingStrike;
            }
            else
            {
                // Создаем новый страйк
                var newStrike = (Hediff_SheldonStrike)HediffMaker.MakeHediff(strikeHediff, initiator);
                newStrike.Severity = 1;
                newStrike.sheldonName = sheldonClone.Label; // Указываем, кто выдал страйк
                initiator.health.AddHediff(newStrike);
                strike = newStrike;
            }

            // --- ЗАПИСЬ в victimsByIssuer и strikeEntries ---
            if (!victimsByIssuer.TryGetValue(sheldonClone, out var list))
            {
                list = new List<Pawn>();
                victimsByIssuer[sheldonClone] = list;
                strikeEntries.Add(new StrikeEntry(sheldonClone, list));
            }
            if (!list.Contains(initiator))
                list.Add(initiator);

            Log.Warning($"{initiator.Name} получил Страйк (Текущая стадия: {existingStrike?.Severity ?? 1}) от {sheldonClone.Name}!");
            Messages.Message($"{initiator.LabelShortCap} получил страйк от {sheldonClone.LabelShortCap}. Текущая стадия: {existingStrike?.Severity ?? 1}", MessageTypeDefOf.NeutralEvent);
        }

        public void IgnorePawn(Pawn sheldonClone, Pawn target)
        {
            if (!ignoredPawns.ContainsKey(sheldonClone))
            {
                ignoredPawns[sheldonClone] = new List<Pawn>();
            }
            if (!ignoredPawns[sheldonClone].Contains(target))
            {
                ignoredPawns[sheldonClone].Add(target);
            }

            // --- Синхронизация для сериализации ---
            var entry = ignoredEntries.FirstOrDefault(e => e.key == sheldonClone);
            if (entry == null)
            {
                entry = new IgnoredEntry(sheldonClone, new List<Pawn>());
                ignoredEntries.Add(entry);
            }
            if (!entry.values.Contains(target))
                entry.values.Add(target);
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

            // 1) Сохраняем processedThoughts
            Scribe_Collections.Look(ref processedThoughts, "processedThoughts", LookMode.Deep);

            // 2) Сохраняем/загружаем список обёрток ignoredEntries
            Scribe_Collections.Look(ref ignoredEntries, "ignoredPawns", LookMode.Deep);

            // 3) Cохраняем/загружаем нашe обёрткe StrikeEntry
            Scribe_Collections.Look(ref strikeEntries, "strikeEntries", LookMode.Deep);

            // 4) после загрузки восстанавливаем словарь victimsByIssuer из strikeEntries
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // инициализируем на всякий случай
                if (processedThoughts == null)
                    processedThoughts = new HashSet<ThoughtReference>();

                // восстановление ignoredPawns, если ещё нужно
                ignoredPawns = new Dictionary<Pawn, List<Pawn>>();
                if (ignoredEntries != null)
                    foreach (var e in ignoredEntries)
                        if (e.key != null)
                            ignoredPawns[e.key] = e.values ?? new List<Pawn>();

                // теперь строим victimsByIssuer
                victimsByIssuer = new Dictionary<Pawn, List<Pawn>>();
                if (strikeEntries != null)
                    foreach (var e in strikeEntries)
                        if (e.issuer != null)
                            victimsByIssuer[e.issuer] = e.victims ?? new List<Pawn>();
            }
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
