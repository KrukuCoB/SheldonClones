using RimWorld;
using Verse;

namespace SheldonClones
{
    public class ThoughtWorker_SheldonVsGeniuses : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            // Проверяем, является ли пешка Шелдоном
            if (p.def != AlienDefOf.SheldonClone)
                return false;

            // Проверяем интеллект у другой пешки
            SkillRecord intellectual = otherPawn.skills?.GetSkill(SkillDefOf.Intellectual);
            if (intellectual == null)
                return false; // Если скилл отсутствует, просто игнорируем

            if (intellectual.Level >= 12)
            {
                return ThoughtState.ActiveAtStage(0); // Восхищается интеллектом
            }
            else if (intellectual.Level <= 4)
            {
                return ThoughtState.ActiveAtStage(1); // Презирает тупость
            }

            return false; // Если интеллект средний, мысли нет
        }
    }
}

