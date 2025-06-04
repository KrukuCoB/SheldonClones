using System.Collections.Generic;
using UnityEngine;
using Verse;
using System.Linq;

namespace SheldonClones
{
    public class Dialog_SheldonInfo : Window
    {
        private Pawn owner;

        public override Vector2 InitialSize => new Vector2(500f, 600f);

        public Dialog_SheldonInfo(Pawn pawn)
        {
            owner = pawn;
            forcePause = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), $"Информация о клоне {owner.LabelShort}");
            Text.Font = GameFont.Small;

            float curY = 40f;
            var neighborComp = owner.TryGetComp<CompNeighborAgreement>();
            if (neighborComp != null)
            {
                Widgets.Label(new Rect(inRect.x, inRect.y + curY, inRect.width, 24f), "Соседи:");
                curY += 22f;
                foreach (var neighbor in neighborComp.CachedNeighbors)
                {
                    bool agreed = neighborComp.HasAgreementWith(neighbor);
                    string label = $"{neighbor.LabelShort} {(agreed ? "(соглашение)" : "(нет соглашения)")}";
                    Widgets.Label(new Rect(inRect.x + 20f, inRect.y + curY, inRect.width - 20f, 20f), label);
                    curY += 18f;
                }
            }
            else
            {
                Widgets.Label(new Rect(inRect.x, inRect.y + curY, inRect.width, 30f), "Нет данных о соседях.");
            }

            // Секция: Страйки
            var watcher = Find.World.GetComponent<GameComponent_SheldonWatcher>();
            var victims = watcher?.GetVictimsByIssuer(owner) ?? new List<Pawn>();
            if (victims.Count > 0)
            {
                curY += 10f;
                Widgets.Label(new Rect(inRect.x, inRect.y + curY, inRect.width, 24f), "Страйки, выданные клоном:");
                curY += 22f;

                foreach (var victim in victims)
                {
                    // Ищем страйк по ссылке на автора
                    Hediff_SheldonStrike strike = null;
                    foreach (var hd in victim.health.hediffSet.hediffs)
                    {
                        if (hd is Hediff_SheldonStrike s && s.sheldonName == owner.Label)
                        {
                            strike = s;
                            break;
                        }
                    }
                    if (strike == null)
                        continue;

                    int severity = (int)strike.Severity;
                    var decayComp = strike.TryGetComp<HediffComp_SheldonStrikeDecay>();
                    float daysLeft = decayComp != null ? decayComp.ticksUntilDecay / 60000f : 0f;
                    string dayText;
                    if (daysLeft >= 1f)
                        dayText = $"{(int)daysLeft} д.";
                    else
                        dayText = $"{daysLeft:F1} д.";
                    string line = $"{victim.LabelShort}: уровень {severity}, истечёт через {dayText}";
                    Widgets.Label(new Rect(inRect.x + 20f, inRect.y + curY, inRect.width - 20f, 20f), line);
                    curY += 18f;
                }
            }
        }
    }
}
