using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SheldonClones
{
    [StaticConstructorOnStartup]
    public static class AutoAddSheldonSeatComp
    {
        static AutoAddSheldonSeatComp()
        {
            // Проходим по всем ThingDef после загрузки модов
            foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                // Проверяем, является ли объект сидячим строением
                if (thingDef.building?.isSittable == true)
                {
                    // Инициализируем список comps если его нет
                    if (thingDef.comps == null)
                        thingDef.comps = new List<CompProperties>();

                    // Проверяем, нет ли уже нашего компонента или других AssignableToPawn компонентов
                    bool hasAssignableComp = thingDef.comps.Any(c =>
                        c is CompProperties_SheldonSeatAssignable ||
                        c is CompProperties_AssignableToPawn);

                    if (!hasAssignableComp)
                    {
                        // Определяем количество мест на основе размера объекта
                        int seatCount = GetSeatCountForFurniture(thingDef);

                        // Добавляем наш компонент
                        var compProps = new CompProperties_SheldonSeatAssignable();
                        compProps.maxAssignedPawnsCount = seatCount;

                        thingDef.comps.Add(compProps);

                        // Log.Message($"[SheldonClones] Добавлен компонент SheldonSeatAssignable к {thingDef.defName} с {seatCount} местами");
                    }
                }
            }
        }

        // Определяет количество мест для сидения на основе характеристик мебели
        private static int GetSeatCountForFurniture(ThingDef thingDef)
        {
            // Если в описании есть специальные ключевые слова
            string label = thingDef.label?.ToLower() ?? "";
            string description = thingDef.description?.ToLower() ?? "";

            // Диваны, скамейки и другая многоместная мебель
            if (label.Contains("couch") || label.Contains("диван") ||
                label.Contains("sofa") || label.Contains("софа") ||
                label.Contains("bench") || label.Contains("скамейка") ||
                label.Contains("loveseat"))
            {
                return 2;
            }

            // Длинные скамьи
            if (label.Contains("pew") || label.Contains("скамья"))
            {
                return 3;
            }

            // Проверяем размер объекта - широкие объекты могут иметь больше мест
            if (thingDef.size.x >= 2 || thingDef.size.z >= 2)
            {
                // Если объект широкий (2+ клетки), скорее всего у него 2 места
                return thingDef.size.x >= 3 || thingDef.size.z >= 3 ? 3 : 2;
            }

            // По умолчанию - одно место (стулья, кресла)
            return 1;
        }
    }
}