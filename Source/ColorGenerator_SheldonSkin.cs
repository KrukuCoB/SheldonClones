using UnityEngine;
using Verse;

namespace SheldonClones
{
    public class ColorGenerator_SheldonSkin : ColorGenerator
    {
        public override Color NewRandomizedColor()
        {
            // Cветлый цвет кожи
            float r = Rand.Range(0.85f, 0.95f);
            float g = Rand.Range(0.75f, 0.85f);
            float b = Rand.Range(0.65f, 0.75f);

            return new Color(r, g, b, 1f); // Альфа = 1 (полностью непрозрачный)
        }
    }
}
