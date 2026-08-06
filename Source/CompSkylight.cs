using RimWorld;
using UnityEngine;
using Verse;

namespace Skylights
{
    /// <summary>
    /// Properties for <see cref="CompSkylight"/>. Attach alongside a
    /// <c>CompProperties_Glower</c> whose <c>glowColor</c> is the full-daylight colour.
    /// </summary>
    public class CompProperties_Skylight : CompProperties
    {
        /// <summary>Sky glow below this counts as full dark (no light, no needless grid churn).</summary>
        public float minChannelGlow = 0.06f;

        /// <summary>Number of brightness steps between dark and full sun. Higher = smoother, more glow-grid recomputes.</summary>
        public int glowSteps = 12;

        public CompProperties_Skylight()
        {
            compClass = typeof(CompSkylight);
        }
    }

    /// <summary>
    /// Drives a building's <see cref="CompGlower"/> so its light mirrors the real sky above:
    /// bright at midday, dark at night, dimmed by storms and eclipses. Only channels light when
    /// the cell's roof is open sky-facing (constructed or thin rock); overhead mountain blocks it.
    /// No power, no heat — the honest trade-off against a sun lamp is that it is only as bright as
    /// the day outside.
    /// </summary>
    public class CompSkylight : ThingComp
    {
        private CompGlower glower;
        private ColorInt fullColor;   // full-daylight colour, taken from the glower's props
        private int lastBucket = -1;  // last applied brightness step, so we only touch the grid on change

        public CompProperties_Skylight Props => (CompProperties_Skylight)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            glower = parent.GetComp<CompGlower>();
            if (glower != null)
            {
                fullColor = glower.Props.glowColor;
            }
            lastBucket = -1;
            UpdateGlow();
        }

        public override void CompTickRare()
        {
            UpdateGlow();
        }

        /// <summary>True when the sky can reach this cell (no roof, or a non-thick roof above).</summary>
        private bool RoofChannelsLight()
        {
            Map map = parent.Map;
            if (map == null) return false;
            RoofDef roof = map.roofGrid.RoofAt(parent.Position);
            // Thick overhead rock (mountain) seals off the sky entirely.
            return roof == null || !roof.isThickRoof;
        }

        private void UpdateGlow()
        {
            if (glower == null) return;
            Map map = parent.Map;
            if (map == null) return;

            float target = 0f;
            if (RoofChannelsLight())
            {
                target = Mathf.Clamp01(map.skyManager.CurSkyGlow);
                if (target < Props.minChannelGlow) target = 0f;
            }

            int steps = Mathf.Max(1, Props.glowSteps);
            int bucket = Mathf.RoundToInt(target * steps);
            if (bucket == lastBucket) return;
            lastBucket = bucket;

            float b = (float)bucket / steps;
            // Setting GlowColor runs the game's RefreshGlower(), which re-registers the glower
            // and dirties the glow grid, so lighting and indoor plant growth both track the sun.
            glower.GlowColor = new ColorInt(
                Mathf.RoundToInt(fullColor.r * b),
                Mathf.RoundToInt(fullColor.g * b),
                Mathf.RoundToInt(fullColor.b * b),
                fullColor.a);
        }

        public override string CompInspectStringExtra()
        {
            Map map = parent.Map;
            if (map == null) return null;

            RoofDef roof = map.roofGrid.RoofAt(parent.Position);
            if (roof == null)
                return "Skylight_NoRoof".Translate();
            if (roof.isThickRoof)
                return "Skylight_ThickRoof".Translate();

            float glow = Mathf.Clamp01(map.skyManager.CurSkyGlow);
            if (glow < Props.minChannelGlow)
                return "Skylight_Dark".Translate();
            return "Skylight_Channeling".Translate(Mathf.RoundToInt(glow * 100f));
        }
    }
}
