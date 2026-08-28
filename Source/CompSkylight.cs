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

        /// <summary>Display-only: when true, this (glow-driven dome) skylight also renders its lit pool at full
        /// open-sky brightness. Every roofed cell within the dome light radius that the dome can see is drawn as
        /// if unroofed — matching the outdoors and tracking dawn, dusk and eclipses — via <c>VisualSkyGrid</c>,
        /// so the pool no longer reads dimmer than the sky outside. Purely cosmetic: the glow grid that drives
        /// plant growth and lit/dark checks stays at the dome's half-strength CompGlower, so gameplay is
        /// unchanged. The bright reach follows the mod-menu dome radius. Set on the dome family.</summary>
        public bool matchOutdoorGlow = false;

        /// <summary>Display-only, for a <see cref="renderAsSky"/> pane: how many extra tiles of a cosmetic ring to
        /// render as open sky around the pane's own sky-lit cell, so its lit patch reads a little wider than a
        /// lone tile. A square box (1 = a 3x3 around a 1x1 pane), clipped to roofed cells the pane can see, so it
        /// never spills through a wall. Purely visual: unlike the pane's own cell, the ring does NOT grow crops or
        /// transmit sun for genes — it only brightens the look. 0 = off (just the single sky-lit tile).</summary>
        public float glowHaloRadius = 0f;

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
        // renderAsSky: the footprint cells currently registered in the SkylightGrid. A 1x1 pane holds one cell;
        // a multi-cell atrium holds its whole footprint, so every tile beneath it renders and lights as open sky.
        private readonly HashSet<IntVec3> skyCells = new HashSet<IntVec3>();

        // glowNodeDef domes: one hidden glower per footprint cell, spawned fresh each time we spawn (the node
        // def is isSaveable=false, so they never persist and can't duplicate on load). Driven together below.
        private List<Thing> glowNodes;
        private ThingDef glowNodeDefResolved;

        // matchOutdoorGlow domes: the display-only cells this dome currently has registered in VisualSkyGrid,
        // so we can diff on change and drop them on despawn. Two static scratch containers, reused each recompute
        // so the rare-tick refresh allocates nothing (the game is single-threaded, one comp recomputes at a time).
        private readonly HashSet<IntVec3> visualCells = new HashSet<IntVec3>();
        private static readonly HashSet<IntVec3> tmpDesired = new HashSet<IntVec3>();
        private static readonly List<IntVec3> staleTmp = new List<IntVec3>();

        // Spawned glower-driven skylights (domes), so a mod-settings radius change can re-register them live.
        private static readonly HashSet<CompSkylight> glowDriven = new HashSet<CompSkylight>();

        // Every spawned skylight of any kind, so the visibility toggle can dirty just the map-mesh sections
        // that actually hold a skylight (a whole-map repaint regenerates lazily and lags for seconds).
        public static readonly List<CompSkylight> SpawnedSkylights = new List<CompSkylight>();

        /// <summary>Dirty just the map-mesh sections that hold a skylight, so a visibility or opacity flip
        /// shows the moment those sections redraw. Building sprites are printed by SectionLayer_ThingsGeneral,
        /// whose relevantChangeTypes is the Things flag — Buildings would regenerate the wrong layers. A
        /// whole-map repaint is also avoided: it regenerates lazily and can lag seconds on a large map.</summary>
        public static void DirtySkylightSections()
        {
            for (int i = 0; i < SpawnedSkylights.Count; i++)
            {
                Thing t = SpawnedSkylights[i].parent;
                if (t.Spawned)
                    t.Map.mapDrawer.MapMeshDirty(t.Position, (ulong)MapMeshFlagDefOf.Things,
                        regenAdjacentCells: true, regenAdjacentSections: true);
            }
        }

        public CompProperties_Skylight Props => (CompProperties_Skylight)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            SpawnedSkylights.Add(this);
            if (Props.renderAsSky)
            {
                UpdateSkyChannel();
                UpdateSkyHalo();
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
                UpdateDomeVisual();
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
            UpdateDomeVisual();
        }

        /// <summary>Re-apply every spawned dome skylight's glow so a changed glow radius takes effect at once.
        /// Setting GlowColor re-registers the glower, which re-reads the (now updated) glowRadius from its props.</summary>
        public static void ForceGlowRefresh()
        {
            foreach (CompSkylight c in glowDriven)
            {
                c.lastBucket = -1;
                c.UpdateGlow();
                // The dome radius may have changed with the setting; re-fit the display-only bright pool to it.
                c.UpdateDomeVisual();
            }
        }

        public override void CompTickRare()
        {
            if (Props.requiresNearbySupport && CollapseIfUnsupported())
                return; // caved in; parent is gone

            if (Props.renderAsSky)
            {
                UpdateSkyChannel();
                // Recompute the cosmetic sky-lit ring so it self-heals when a nearby wall or roof changes.
                UpdateSkyHalo();
            }
            else
            {
                UpdateGlow();
                // Recompute the display-only bright pool so it self-heals when a nearby wall or roof changes.
                UpdateDomeVisual();
            }
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
            if (Props.renderAsSky && skyCells.Count > 0)
            {
                foreach (IntVec3 c in skyCells)
                {
                    SkylightGrid.Set(map, c, false);
                    if (Props.transmitsSun)
                        SunlightGrid.Set(map, c, false);
                }
                skyCells.Clear();
            }
            if (visualCells.Count > 0)
            {
                foreach (IntVec3 c in visualCells)
                    VisualSkyGrid.Set(map, c, false);
                visualCells.Clear();
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

        /// <summary>Keep our footprint's "as if no roof" registration in sync with which cells are channeling.
        /// Works for a single-tile pane and for a multi-tile atrium alike: every occupied cell that has a roof
        /// to channel through renders and lights as open sky.</summary>
        private void UpdateSkyChannel()
        {
            Map map = parent.Map;
            if (map == null) return;

            HashSet<IntVec3> desired = new HashSet<IntVec3>();
            foreach (IntVec3 c in parent.OccupiedRect())
                if (RoofChannelsLightAt(map, c))
                    desired.Add(c);

            if (skyCells.SetEquals(desired)) return;

            // Drop cells that no longer channel.
            List<IntVec3> stale = new List<IntVec3>();
            foreach (IntVec3 c in skyCells)
                if (!desired.Contains(c)) stale.Add(c);
            foreach (IntVec3 c in stale) SetSkyCell(map, c, false);

            // Add newly channeling cells.
            foreach (IntVec3 c in desired)
                if (!skyCells.Contains(c)) SetSkyCell(map, c, true);
        }

        private void SetSkyCell(Map map, IntVec3 c, bool on)
        {
            SkylightGrid.Set(map, c, on);
            if (Props.transmitsSun)
                SunlightGrid.Set(map, c, on);
            if (on) skyCells.Add(c);
            else skyCells.Remove(c);
        }

        /// <summary>True when this cell should channel daylight down: there is a roof above to channel
        /// through, and it isn't blocking. Open sky is excluded — it already lights the cell directly.</summary>
        private bool RoofChannelsLightAt(Map map, IntVec3 cell)
        {
            RoofDef roof = map.roofGrid.RoofAt(cell);
            // Open sky already lights this spot directly — channel nothing.
            if (roof == null) return false;
            // Thick overhead rock seals off the sky, unless a reflection tube pierces it.
            if (roof.isThickRoof) return Props.worksUnderThickRoof;
            // Constructed or thin roof: channel the daylight through.
            return true;
        }

        /// <summary>Whether the building's root cell is channeling — used by the glow-dome path and inspect text.</summary>
        private bool RoofChannelsLight()
        {
            Map map = parent.Map;
            return map != null && RoofChannelsLightAt(map, parent.Position);
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

        /// <summary>Keep this dome's display-only bright pool (see <c>VisualSkyGrid</c>) in sync with the mod-menu
        /// radius and the surrounding walls and roofs. Every roofed cell within the dome light radius that the
        /// dome can see (line of sight, so the brightness never leaks through a wall into a sealed neighbour) is
        /// rendered at full open-sky brightness — matching the outdoors and tracking the sky — while the glow grid
        /// that drives plant growth and lit/dark checks is left untouched. Recomputed each rare tick, so it
        /// self-heals when a nearby wall or roof is built or removed. Only the dome family opts in
        /// (<see cref="CompProperties_Skylight.matchOutdoorGlow"/>); the soft CompGlower still lights the room.</summary>
        private void UpdateDomeVisual()
        {
            if (!Props.matchOutdoorGlow) return;
            bool hasNodes = glowNodes != null && glowNodes.Count > 0;
            if (glower == null && !hasNodes) return;   // no dome light source — nothing to brighten
            Map map = parent.Map;
            if (map == null) return;

            tmpDesired.Clear();
            // Only while the dome is actually channeling (open-facing roof above, or thick rock for the light
            // tunnel). A dome sealed under thick mountain adds no glow, so it shows no bright pool either.
            if (RoofChannelsLight())
            {
                float radius = SkylightsSettingsMod.Settings?.domeGlowRadius ?? SkylightsSettings.DefaultDomeGlowRadius;
                foreach (IntVec3 src in parent.OccupiedRect())
                {
                    foreach (IntVec3 c in GenRadial.RadialCellsAround(src, radius, useCenter: true))
                    {
                        if (tmpDesired.Contains(c) || !c.InBounds(map)) continue;
                        // Open-sky cells already light at the sky's brightness; only roofed (indoor) cells,
                        // which the overlay would otherwise darken, need brightening to match the outdoors.
                        if (!c.Roofed(map)) continue;
                        // Respect walls: a clear straight line from the dome to the cell, so the bright pool
                        // can't spill through a wall into an adjacent sealed room.
                        if (!GenSight.LineOfSight(src, c, map, skipFirstCell: true)) continue;
                        tmpDesired.Add(c);
                    }
                }
            }

            ApplyVisualCells(map, tmpDesired);
        }

        /// <summary>Keep a square pane's cosmetic sky-lit ring (see <c>VisualSkyGrid</c>) in sync with the
        /// surrounding walls and roofs. Every roofed cell within <see cref="CompProperties_Skylight.glowHaloRadius"/>
        /// tiles of the pane that the pane can see is rendered as open sky, so the lit patch reads a little wider
        /// than the single tile — display only: the ring never grows crops or transmits sun (that stays the pane's
        /// own cell in the SkylightGrid). Recomputed each rare tick, so it self-heals when a wall or roof changes.</summary>
        private void UpdateSkyHalo()
        {
            if (Props.glowHaloRadius <= 0f) return;
            Map map = parent.Map;
            if (map == null) return;

            tmpDesired.Clear();
            // Only while at least one footprint cell is channeling as open sky: a sealed or roofless pane
            // channels nothing, so it shows no ring either.
            if (skyCells.Count > 0)
            {
                int r = Mathf.Max(1, Mathf.RoundToInt(Props.glowHaloRadius));
                // Square box around the pane (r = 1 gives a 3x3 around a 1x1 pane), matching the square glass.
                foreach (IntVec3 c in parent.OccupiedRect().ExpandedBy(r))
                {
                    if (tmpDesired.Contains(c) || !c.InBounds(map)) continue;
                    // The pane's own tile (and any neighbouring pane's) already renders as full sky; skip it.
                    if (SkylightGrid.Contains(map, c)) continue;
                    // Only roofed (indoor) cells need brightening; respect walls so the ring can't spill through
                    // one into an adjacent sealed room.
                    if (!c.Roofed(map)) continue;
                    if (!GenSight.LineOfSight(parent.Position, c, map, skipFirstCell: true)) continue;
                    tmpDesired.Add(c);
                }
            }

            ApplyVisualCells(map, tmpDesired);
        }

        /// <summary>Diff this skylight's display-only bright cells (<paramref name="desired"/>) against what it last
        /// registered in <c>VisualSkyGrid</c>, touching the grid only where membership actually changed so an
        /// unchanged frame costs nothing. Shared by the dome pool and the square-pane ring.</summary>
        private void ApplyVisualCells(Map map, HashSet<IntVec3> desired)
        {
            if (visualCells.SetEquals(desired)) return;

            // Drop cells that are no longer lit.
            staleTmp.Clear();
            foreach (IntVec3 c in visualCells)
                if (!desired.Contains(c)) staleTmp.Add(c);
            foreach (IntVec3 c in staleTmp)
            {
                VisualSkyGrid.Set(map, c, false);
                visualCells.Remove(c);
            }
            // Register newly lit cells.
            foreach (IntVec3 c in desired)
                if (visualCells.Add(c)) VisualSkyGrid.Set(map, c, true);
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
