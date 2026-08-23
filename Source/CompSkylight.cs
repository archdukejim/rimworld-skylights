using System.Collections.Generic;
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

        /// <summary>If set, this (multi-cell) dome lights its room with hidden glow-emitter nodes — one per
        /// footprint cell — instead of a single corner-mounted CompGlower, so the light is centred on an even
        /// footprint. The named def must carry a <c>CompProperties_Glower</c> whose <c>glowColor</c> is the
        /// full-daylight colour. Leave null for a normal single-cell glower dome.</summary>
        public string glowNodeDef = null;

        /// <summary>Per-node brightness for <see cref="glowNodeDef"/> domes, as a fraction of a full dome:
        /// each footprint node emits <c>fullColor * skyFraction * glowNodeStrength</c>. The nodes overlap, so
        /// e.g. 0.5 makes a 2x1 read like one dome and a 2x2 a little brighter. Tune to taste.</summary>
        public float glowNodeStrength = 1f;

        /// <summary>When true the cell is rendered and lit as if there were no roof (full sky light, colour,
        /// moonlight, shadows) via the Harmony patches, instead of driving a CompGlower. Used by the paned skylight.</summary>
        public bool renderAsSky = false;

        /// <summary>When true a channeling cell also counts as being in real sunlight for Biotech gene/stat/thought
        /// checks (via the InSunlight patch): sun-loving xenotypes gain their outdoor benefit indoors, and
        /// sun-sensitive pawns are exposed. The clear paned skylight sets this; the tinted one leaves it false
        /// (UV-filtered — lights the room but registers no sun for genes).</summary>
        public bool transmitsSun = false;

        /// <summary>When true this skylight needs a roof-holding edifice (wall or pillar) within
        /// <see cref="supportRadius"/> tiles: a PlaceWorker blocks installing it out of range, and if that
        /// support is later removed the weak glass caves in (its own roof tile collapses and crushes it).
        /// The reinforced electric panes leave this false; the low-tech tribal skylight sets it.</summary>
        public bool requiresNearbySupport = false;

        /// <summary>Radius (tiles) within which a wall/pillar must sit for a support-requiring skylight to hold.</summary>
        public float supportRadius = 3f;

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

        // glowNodeDef domes: one hidden glower per footprint cell, spawned fresh each time we spawn (the node
        // def is isSaveable=false, so they never persist and can't duplicate on load). Driven together below.
        private List<Thing> glowNodes;
        private ThingDef glowNodeDefResolved;

        // Spawned glower-driven skylights (domes), so a mod-settings radius change can re-register them live.
        private static readonly HashSet<CompSkylight> glowDriven = new HashSet<CompSkylight>();

        // Every spawned skylight of any kind, so the visibility toggle can dirty just the map-mesh sections
        // that actually hold a skylight (a whole-map repaint regenerates lazily and lags for seconds).
        public static readonly List<CompSkylight> SpawnedSkylights = new List<CompSkylight>();

        public CompProperties_Skylight Props => (CompProperties_Skylight)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            SpawnedSkylights.Add(this);
            if (Props.renderAsSky)
            {
                UpdateSkyChannel();
                return;
            }
            if (Props.glowNodeDef != null)
            {
                glowNodeDefResolved = DefDatabase<ThingDef>.GetNamedSilentFail(Props.glowNodeDef);
                CompProperties_Glower nodeGlow = glowNodeDefResolved?.GetCompProperties<CompProperties_Glower>();
                if (nodeGlow != null)
                {
                    fullColor = nodeGlow.glowColor;
                    SpawnGlowNodes();
                    glowDriven.Add(this);
                }
                lastBucket = -1;
                UpdateGlow();
                return;
            }
            glower = parent.GetComp<CompGlower>();
            if (glower != null)
            {
                fullColor = glower.Props.glowColor;
                glowDriven.Add(this);
            }
            lastBucket = -1;
            UpdateGlow();
        }

        /// <summary>Re-apply every spawned dome skylight's glow so a changed glow radius takes effect at once.
        /// Setting GlowColor re-registers the glower, which re-reads the (now updated) glowRadius from its props.</summary>
        public static void ForceGlowRefresh()
        {
            foreach (CompSkylight c in glowDriven)
            {
                c.lastBucket = -1;
                c.UpdateGlow();
            }
        }

        public override void CompTickRare()
        {
            if (Props.requiresNearbySupport && CollapseIfUnsupported())
                return; // caved in; parent is gone

            if (Props.renderAsSky)
                UpdateSkyChannel();
            else
                UpdateGlow();
        }

        /// <summary>Weak-glass skylights are held up by a nearby wall or pillar. If that support has been
        /// removed (deconstructed or destroyed) so none sits within the support radius, the roof over this
        /// tile caves in — the falling roof crushes the weak glass. Returns true if it collapsed.</summary>
        private bool CollapseIfUnsupported()
        {
            Map map = parent.Map;
            if (map == null) return false;
            if (SkylightSupport.HasSupportWithin(map, parent.Position, Props.supportRadius))
                return false;

            Thing p = parent;
            IntVec3 pos = p.Position;
            // Drop the roof on just this tile; the collapse damages whatever is beneath it.
            RoofCollapserImmediate.DropRoofInCells(pos, map);
            if (p.Spawned && !p.Destroyed)
                p.Destroy(DestroyMode.KillFinalize);
            return true;
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (Props.renderAsSky && skyRegistered)
            {
                SkylightGrid.Set(map, skyCell, false);
                if (Props.transmitsSun)
                    SunlightGrid.Set(map, skyCell, false);
                skyRegistered = false;
            }
            DespawnGlowNodes();
            glowDriven.Remove(this);
            SpawnedSkylights.Remove(this);
            base.PostDeSpawn(map, mode);
        }

        /// <summary>Spawn one hidden glower node on every cell of our footprint, so a multi-cell dome's light
        /// is centred on the footprint instead of leaning to its root corner. Nodes are non-persistent.</summary>
        private void SpawnGlowNodes()
        {
            Map map = parent.Map;
            if (map == null || glowNodeDefResolved == null) return;
            glowNodes = new List<Thing>();
            foreach (IntVec3 c in parent.OccupiedRect())
            {
                Thing node = ThingMaker.MakeThing(glowNodeDefResolved);
                GenSpawn.Spawn(node, c, map);
                glowNodes.Add(node);
            }
        }

        private void DespawnGlowNodes()
        {
            if (glowNodes == null) return;
            foreach (Thing n in glowNodes)
                if (n != null && !n.Destroyed) n.Destroy();
            glowNodes = null;
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
            {
                SkylightGrid.Set(map, skyCell, false);
                if (Props.transmitsSun)
                    SunlightGrid.Set(map, skyCell, false);
            }
            if (shouldChannel)
            {
                SkylightGrid.Set(map, cell, true);
                if (Props.transmitsSun)
                    SunlightGrid.Set(map, cell, true);
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
            bool hasNodes = glowNodes != null && glowNodes.Count > 0;
            if (glower == null && !hasNodes) return;
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

            if (hasNodes)
            {
                // Each footprint node emits a fraction of a full dome; the nodes overlap into a pool that is
                // centred on the footprint. Setting GlowColor re-registers each node's glower.
                float per = b * Props.glowNodeStrength;
                ColorInt nodeColor = new ColorInt(
                    Mathf.RoundToInt(fullColor.r * per),
                    Mathf.RoundToInt(fullColor.g * per),
                    Mathf.RoundToInt(fullColor.b * per),
                    fullColor.a);
                foreach (Thing n in glowNodes)
                {
                    CompGlower g = n?.TryGetComp<CompGlower>();
                    if (g != null) g.GlowColor = nodeColor;
                }
                return;
            }

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
