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

        /// <summary>When true, daylight still reaches this cell through thick overhead mountain (e.g. piped down a reflection tube).</summary>
        public bool worksUnderThickRoof = false;

        /// <summary>Fraction of the outdoor sky glow this skylight channels indoors (1 = full sky, 0.5 = half). A dome only passes soft, partial light.</summary>
        public float glowFactor = 1f;

        /// <summary>When true the cell is rendered and lit as if there were no roof (full sky light, colour,
        /// moonlight, shadows) via the Harmony patches, instead of driving a CompGlower. Used by the paned skylight.</summary>
        public bool renderAsSky = false;

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
        private bool skyRegistered;   // renderAsSky: whether our cell is currently in the SkylightGrid
        private IntVec3 skyCell = IntVec3.Invalid;  // the cell we registered, so we can deregister on despawn

        public CompProperties_Skylight Props => (CompProperties_Skylight)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (Props.renderAsSky)
            {
                UpdateSkyChannel();
                return;
            }
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
            if (Props.renderAsSky)
                UpdateSkyChannel();
            else
                UpdateGlow();
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (Props.renderAsSky && skyRegistered)
            {
                SkylightGrid.Set(map, skyCell, false);
                skyRegistered = false;
            }
            base.PostDeSpawn(map, mode);
        }

        /// <summary>Keep our cell's "as if no roof" registration in sync with whether we're channeling.</summary>
        private void UpdateSkyChannel()
        {
            Map map = parent.Map;
            if (map == null) return;
            bool shouldChannel = RoofChannelsLight();
            IntVec3 cell = parent.Position;
            if (shouldChannel && skyRegistered && cell == skyCell) return;

            if (skyRegistered)
                SkylightGrid.Set(map, skyCell, false);
            if (shouldChannel)
            {
                SkylightGrid.Set(map, cell, true);
                skyCell = cell;
                skyRegistered = true;
            }
            else
            {
                skyRegistered = false;
            }
        }

        /// <summary>True when the skylight should channel daylight down: there is a roof above to
        /// channel through, and it isn't blocking. Open sky is excluded — it already lights the cell
        /// directly, so adding glow there would make the spot brighter than the sun outside.</summary>
        private bool RoofChannelsLight()
        {
            Map map = parent.Map;
            if (map == null) return false;
            RoofDef roof = map.roofGrid.RoofAt(parent.Position);
            // Open sky already lights this spot directly — channel nothing.
            if (roof == null) return false;
            // Thick overhead rock seals off the sky, unless a reflection tube pierces it.
            if (roof.isThickRoof) return Props.worksUnderThickRoof;
            // Constructed or thin roof: channel the daylight through.
            return true;
        }

        private void UpdateGlow()
        {
            if (glower == null) return;
            Map map = parent.Map;
            if (map == null) return;

            float target = 0f;
            if (RoofChannelsLight())
            {
                // Mirror the real sky, scaled by glowFactor: a dome passes only half the outdoor light.
                float glow = Mathf.Clamp01(map.skyManager.CurSkyGlow);
                if (glow >= Props.minChannelGlow)
                    target = Mathf.Clamp01(glow * Props.glowFactor);
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
            if (roof != null && roof.isThickRoof && !Props.worksUnderThickRoof)
                return "Skylight_ThickRoof".Translate();
            if (roof == null)
                return "Skylight_NoRoof".Translate();

            float glow = Mathf.Clamp01(map.skyManager.CurSkyGlow);
            if (glow < Props.minChannelGlow)
                return "Skylight_Dark".Translate();
            return "Skylight_Channeling".Translate(Mathf.RoundToInt(glow * 100f));
        }
    }
}
