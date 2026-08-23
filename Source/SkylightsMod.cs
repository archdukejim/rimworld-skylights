using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Skylights
{
    /// <summary>Applies the Harmony patches on game start.</summary>
    [StaticConstructorOnStartup]
    public static class SkylightsMod
    {
        static SkylightsMod()
        {
            new Harmony("archdukejim.skylights").PatchAll();
        }
    }

    /// <summary>
    /// Per-map set of cells that a paned skylight is channeling the open sky into. The Harmony
    /// patches below make the game light and render these cells as if unroofed — full sky brightness,
    /// sky colour, moonlight and shadows — while the roof itself stays in place for weather and
    /// temperature. Maintained by <see cref="CompSkylight"/> as skylights are built, roofed, or removed.
    /// </summary>
    public static class SkylightGrid
    {
        private static readonly Dictionary<Map, HashSet<IntVec3>> byMap = new Dictionary<Map, HashSet<IntVec3>>();

        public static void Set(Map map, IntVec3 c, bool on)
        {
            if (map == null) return;
            if (on)
            {
                if (!byMap.TryGetValue(map, out HashSet<IntVec3> set))
                {
                    set = new HashSet<IntVec3>();
                    byMap[map] = set;
                }
                if (set.Add(c)) Dirty(map, c);
            }
            else if (byMap.TryGetValue(map, out HashSet<IntVec3> set) && set.Remove(c))
            {
                Dirty(map, c);
            }
        }

        public static bool Contains(Map map, IntVec3 c)
        {
            return map != null && byMap.TryGetValue(map, out HashSet<IntVec3> set) && set.Contains(c);
        }

        // Force the indoor-mask mesh and the glow grid to rebuild for this cell so the change shows at once.
        private static void Dirty(Map map, IntVec3 c)
        {
            if (map.mapDrawer != null)
                map.mapDrawer.MapMeshDirty(c, (ulong)MapMeshFlagDefOf.Buildings | (ulong)MapMeshFlagDefOf.Roofs);
            if (map.glowGrid != null)
                map.glowGrid.DirtyCell(c);
        }
    }

    /// <summary>
    /// Per-map set of cells that a *clear* paned skylight is channeling real sunlight into for Biotech
    /// gene/stat/thought checks. Unlike <see cref="SkylightGrid"/> (which drives lighting for every pane,
    /// clear and tinted), only sun-transmitting panes register here — the tinted pane lights the room but
    /// filters the sun out for genes. Read by the <see cref="Patch_InSunlight"/> postfix. No mesh/glow
    /// dirtying: InSunlight is queried live each call, so membership alone is enough.
    /// </summary>
    public static class SunlightGrid
    {
        private static readonly Dictionary<Map, HashSet<IntVec3>> byMap = new Dictionary<Map, HashSet<IntVec3>>();

        public static void Set(Map map, IntVec3 c, bool on)
        {
            if (map == null) return;
            if (on)
            {
                if (!byMap.TryGetValue(map, out HashSet<IntVec3> set))
                {
                    set = new HashSet<IntVec3>();
                    byMap[map] = set;
                }
                set.Add(c);
            }
            else if (byMap.TryGetValue(map, out HashSet<IntVec3> set))
            {
                set.Remove(c);
            }
        }

        public static bool Contains(Map map, IntVec3 c)
        {
            return map != null && byMap.TryGetValue(map, out HashSet<IntVec3> set) && set.Contains(c);
        }
    }

    /// <summary>
    /// Per-map set of cells a skylight brightens for <em>display only</em>. Unlike <see cref="SkylightGrid"/>
    /// (which also feeds the glow grid and the sun-gene checks), these cells are read by the lighting <em>overlay</em>
    /// alone — via <see cref="SkylightLightHelper"/> — so they render at full open-sky brightness, matching the
    /// outdoors and tracking dawn, dusk and eclipses. Nothing here touches gameplay:
    /// <see cref="Patch_GlowGrid_GroundGlowAt"/> and <see cref="Patch_InSunlight"/> never consult this grid, so plant
    /// growth, lit/dark checks and Biotech genes are all left exactly as they were. Maintained by
    /// <see cref="CompSkylight"/> for two cases: a dome's lit pool (<c>matchOutdoorGlow</c>) and the cosmetic ring
    /// around a square pane (<c>glowHaloRadius</c>), which reads a little wider than the single sky-lit tile.
    /// </summary>
    public static class VisualSkyGrid
    {
        private static readonly Dictionary<Map, HashSet<IntVec3>> byMap = new Dictionary<Map, HashSet<IntVec3>>();

        public static void Set(Map map, IntVec3 c, bool on)
        {
            if (map == null) return;
            if (on)
            {
                if (!byMap.TryGetValue(map, out HashSet<IntVec3> set))
                {
                    set = new HashSet<IntVec3>();
                    byMap[map] = set;
                }
                if (set.Add(c)) Dirty(map, c);
            }
            else if (byMap.TryGetValue(map, out HashSet<IntVec3> set) && set.Remove(c))
            {
                Dirty(map, c);
            }
        }

        public static bool Contains(Map map, IntVec3 c)
        {
            return map != null && byMap.TryGetValue(map, out HashSet<IntVec3> set) && set.Contains(c);
        }

        // Regenerate only the lighting overlay for this cell — its relevantChangeTypes are Roofs | GroundGlow.
        // Deliberately NOT glowGrid.DirtyCell: the gameplay glow value is left untouched, keeping this display-only.
        private static void Dirty(Map map, IntVec3 c)
        {
            map.mapDrawer?.MapMeshDirty(c, (ulong)MapMeshFlagDefOf.Roofs | (ulong)MapMeshFlagDefOf.GroundGlow);
        }
    }

    /// <summary>
    /// Every Biotech sun-gene, "in sunlight" stat condition, and the in-sunlight mood thought funnels through
    /// <c>SanguophageUtility.InSunlight</c>, which returns false for any roofed cell. Make a clear paned
    /// skylight cell read as in sunlight — exactly as bright as the real sky — so those genes fire indoors
    /// (sun-lovers benefit, sun-sensitive pawns are exposed), matching the mod's "as if there were no roof"
    /// philosophy. Only fires for cells registered in <see cref="SunlightGrid"/> (clear panes), never tinted.
    /// </summary>
    [HarmonyPatch(typeof(SanguophageUtility), nameof(SanguophageUtility.InSunlight))]
    public static class Patch_InSunlight
    {
        public static void Postfix(ref bool __result, IntVec3 cell, Map map)
        {
            if (__result || map == null) return;
            if (SunlightGrid.Contains(map, cell))
                __result = map.skyManager.CurSkyGlow > 0.1f;
        }
    }

    /// <summary>
    /// Reimplements the indoor-mask baking so a channeling skylight cell is treated as "roofed
    /// outdoor": it goes to the RoofedOutdoorMask, which hides rain/snow/fog while leaving the cell
    /// open to the exterior sky lighting (day, dusk, moonlight, shadows). Non-skylight cells keep
    /// vanilla behaviour exactly.
    /// </summary>
    [HarmonyPatch(typeof(SectionLayer_IndoorMask), "GenerateSectionLayer")]
    public static class Patch_IndoorMask_GenerateSectionLayer
    {
        public static bool Prefix(Map map, IEnumerable<IntVec3> cells, int cellCount,
            LayerSubMesh indoorMesh, LayerSubMesh outdoorMesh, LayerSubMesh filledMesh, LayerSubMesh debugMesh,
            Vector3 meshOffset, bool debugIndoor, bool debugOutdoor)
        {
            ClearSubMesh(indoorMesh, cellCount);
            ClearSubMesh(outdoorMesh, cellCount);
            ClearSubMesh(filledMesh, cellCount);
            ClearSubMesh(debugMesh, cellCount);
            CellIndices cellIndices = map.cellIndices;
            foreach (IntVec3 cell in cells)
            {
                float x = (float)cell.x + meshOffset.x;
                float z = (float)cell.z + meshOffset.z;
                IntVec3 c = new IntVec3(cell.x, 0, cell.z);
                if (!c.InBounds(map)) continue;

                // Skylight cell: roofed-outdoor — rain/snow/fog hidden, but still sky-lit.
                if (SkylightGrid.Contains(map, c))
                {
                    SectionLayer_IndoorMask.AppendQuadToMesh(outdoorMesh, x, z, 0f);
                    if (!debugIndoor && debugOutdoor)
                        SectionLayer_IndoorMask.AppendQuadToMesh(debugMesh, x, z, 0f);
                    continue;
                }

                bool hide = HideCommon(map, c);
                bool hideRain = HideRainFogOverlay(map, c);
                if (!hide && !hideRain) continue;
                Building building = map.edificeGrid.InnerArray[cellIndices.CellToIndex(c.x, c.z)];
                float overage = ((building == null || (building.def.passability != Traversability.Impassable && !building.def.IsDoor)) ? 0.16f : 0f);
                if (hide)
                {
                    Room room = c.GetRoom(map);
                    if (room == null || !room.ProperRoom)
                    {
                        Building edifice = c.GetEdifice(map);
                        RoofDef roof = c.GetRoof(map);
                        if ((edifice == null || edifice.def.Fillage != FillCategory.Full) && (roof == null || !roof.isThickRoof))
                        {
                            SectionLayer_IndoorMask.AppendQuadToMesh(outdoorMesh, x, z, 0f);
                            if (!debugIndoor && debugOutdoor)
                                SectionLayer_IndoorMask.AppendQuadToMesh(debugMesh, x, z, 0f);
                            continue;
                        }
                    }
                }
                SectionLayer_IndoorMask.AppendQuadToMesh(indoorMesh, x, z, overage);
                if (debugIndoor)
                    SectionLayer_IndoorMask.AppendQuadToMesh(debugMesh, x, z, overage);
            }
            FinalizeIfNeeded(indoorMesh);
            FinalizeIfNeeded(outdoorMesh);
            FinalizeIfNeeded(filledMesh);
            FinalizeIfNeeded(debugMesh);
            return false;
        }

        private static void ClearSubMesh(LayerSubMesh mesh, int cellCount)
        {
            if (mesh == null) return;
            mesh.Clear(MeshParts.All);
            if (DebugViewSettings.drawIndoorMask || DebugViewSettings.drawOutdoorMask)
            {
                mesh.verts.Capacity = cellCount * 2;
                mesh.tris.Capacity = cellCount * 4;
            }
        }

        private static void FinalizeIfNeeded(LayerSubMesh mesh)
        {
            if (mesh != null && !mesh.finalized && mesh.verts.Count > 0)
                mesh.FinalizeMesh(MeshParts.Verts | MeshParts.Tris);
        }

        private static bool HideRainFogOverlay(Map map, IntVec3 c)
        {
            Building edifice = c.GetEdifice(map);
            return edifice?.def.building != null && edifice.def.building.isNaturalRock;
        }

        private static bool HideCommon(Map map, IntVec3 c)
        {
            if (map.fogGrid.IsFogged(c)) return true;
            if (c.Roofed(map))
            {
                Building edifice = c.GetEdifice(map);
                if (edifice == null) return true;
                if (edifice.def.Fillage != FillCategory.Full) return true;
                if (edifice.def.size.x > 1 || edifice.def.size.z > 1) return true;
                if (edifice.def.holdsRoof) return true;
                if (edifice.def.blockWeather) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Feed the real sky glow into a channeling skylight cell so plants grow by daylight and the cell
    /// counts as lit — matching the open sky, and never brighter than it.
    /// </summary>
    [HarmonyPatch(typeof(GlowGrid), nameof(GlowGrid.GroundGlowAt))]
    public static class Patch_GlowGrid_GroundGlowAt
    {
        public static void Postfix(ref float __result, IntVec3 c, bool ignoreSky, Map ___map)
        {
            if (ignoreSky || ___map == null) return;
            if (SkylightGrid.Contains(___map, c))
            {
                float sky = ___map.skyManager.CurSkyGlow;
                if (sky > __result) __result = sky;
            }
        }
    }

    /// <summary>
    /// The lighting overlay clamps every roofed cell to a minimum darkness (RoofedAreaMinSkyCover) via
    /// its roof lookups, which dims a skylight cell below the real outdoor level even in a sealed room.
    /// Redirect those lookups so a skylight cell reads as open sky for lighting only — the overlay then
    /// lights it exactly like the outdoors, matching brightness, colour, moonlight and shadows.
    /// </summary>
    [HarmonyPatch(typeof(SectionLayer_LightingOverlay), "GenerateLightingOverlay")]
    public static class Patch_LightingOverlay_Generate
    {
        private static readonly MethodInfo RoofAtInt = AccessTools.Method(typeof(RoofGrid), nameof(RoofGrid.RoofAt), new[] { typeof(int) });
        private static readonly MethodInfo RoofedInt = AccessTools.Method(typeof(RoofGrid), nameof(RoofGrid.Roofed), new[] { typeof(int) });
        private static readonly MethodInfo RoofAtRepl = AccessTools.Method(typeof(SkylightLightHelper), nameof(SkylightLightHelper.RoofAtForLight));
        private static readonly MethodInfo RoofedRepl = AccessTools.Method(typeof(SkylightLightHelper), nameof(SkylightLightHelper.RoofedForLight));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction ci in instructions)
            {
                if ((ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Call) && ci.operand is MethodInfo mi)
                {
                    if (mi == RoofAtInt) { yield return new CodeInstruction(OpCodes.Call, RoofAtRepl); continue; }
                    if (mi == RoofedInt) { yield return new CodeInstruction(OpCodes.Call, RoofedRepl); continue; }
                }
                yield return ci;
            }
        }
    }

    /// <summary>Roof lookups used only by the lighting overlay: a channeling skylight cell reads as
    /// open sky, so the overlay lights it like the outdoors. Roof/weather/temperature are untouched.</summary>
    public static class SkylightLightHelper
    {
        private static readonly AccessTools.FieldRef<RoofGrid, Map> MapOf =
            AccessTools.FieldRefAccess<RoofGrid, Map>("map");

        public static RoofDef RoofAtForLight(RoofGrid grid, int index)
        {
            Map map = MapOf(grid);
            if (map != null)
            {
                IntVec3 c = map.cellIndices.IndexToCell(index);
                // SkylightGrid: paned/atrium cells lit as open sky. VisualSkyGrid: dome pool cells brightened
                // for display only. Either way the overlay should light the cell like the outdoors.
                if (SkylightGrid.Contains(map, c) || VisualSkyGrid.Contains(map, c))
                    return null;
            }
            return grid.RoofAt(index);
        }

        public static bool RoofedForLight(RoofGrid grid, int index)
        {
            Map map = MapOf(grid);
            if (map != null)
            {
                IntVec3 c = map.cellIndices.IndexToCell(index);
                if (SkylightGrid.Contains(map, c) || VisualSkyGrid.Contains(map, c))
                    return false;
            }
            return grid.Roofed(index);
        }
    }

    /// <summary>
    /// Reshapes the roof's soft edge from outward to inward when the player opts in (<see cref="RoofEdgeMode"/>).
    /// Vanilla dilates the roof's darkening half a tile outward — a corner is darkened if *any* touching tile is
    /// roofed — so the shadow bleeds onto the lit tiles just outside the roof. Here a boundary corner that touches
    /// an open (or skylight) tile is instead left *uncovered*, and the adjacent roofed tile's forced-dark clamp is
    /// skipped, which erodes the shadow half a tile inward: the open/skylight tile stays fully lit to its edge and
    /// the soft falloff lands on the roofed ring. Only runs when a mode is active; Vanilla falls through to the
    /// real (transpiled) method so nothing changes when the setting is off. A faithful reimplementation of
    /// <c>SectionLayer_LightingOverlay.GenerateLightingOverlay</c> with those two rules made direction-aware.
    /// </summary>
    [HarmonyPatch(typeof(SectionLayer_LightingOverlay), "GenerateLightingOverlay")]
    public static class Patch_LightingOverlay_Inward
    {
        private static readonly MethodInfo MakeBaseGeometryMI =
            AccessTools.Method(typeof(SectionLayer_LightingOverlay), "MakeBaseGeometry");

        public static bool Prefix(Map map, LayerSubMesh subMesh, CellRect rect, ref int firstCenterInd,
            bool centered, System.Predicate<int> filter)
        {
            RoofEdgeMode mode = SkylightsSettingsMod.RoofEdge;
            if (mode == RoofEdgeMode.Vanilla) return true;   // untouched: let the real method run.

            rect.ClipInsideMap(map);
            if (subMesh.verts.Count == 0)
            {
                object[] geoArgs = { map, subMesh, rect, 0, centered };
                MakeBaseGeometryMI.Invoke(null, geoArgs);
                firstCenterInd = (int)geoArgs[3];
            }

            int sizeX = map.Size.x, sizeZ = map.Size.z;
            int minX = rect.minX, minZ = rect.minZ, maxX = rect.maxX, maxZ = rect.maxZ;
            int W = rect.Width;
            RoofGrid roofGrid = map.roofGrid;
            CellIndices ci = map.cellIndices;
            Thing[] edifice = map.edificeGrid.InnerArray;
            GlowGrid glow = map.glowGrid;

            Color32[] array = new Color32[subMesh.verts.Count];

            // Corner pass: one vertex per tile corner, coloured from the (up to) 4 tiles meeting there.
            for (int gz = minZ; gz <= maxZ + 1; gz++)
            {
                for (int gx = minX; gx <= maxX + 1; gx++)
                {
                    int vBotLeft = (gz - minZ) * (W + 1) + (gx - minX);

                    if (filter != null && gx >= 0 && gx < sizeX && gz >= 0 && gz < sizeZ
                        && !filter(ci.CellToIndex(gx, gz)))
                    {
                        array[vBotLeft] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    int cr = 0, cg = 0, cb = 0, ca = 0, n = 0;
                    bool covered = false;
                    bool touchesInwardOpen = false;
                    bool touchesBlocker = false;
                    for (int k = 0; k < 4; k++)
                    {
                        int cx = gx - (k == 0 || k == 2 ? 1 : 0);
                        int cz = gz - (k == 0 || k == 1 ? 1 : 0);
                        if (cx < 0 || cx >= sizeX || cz < 0 || cz >= sizeZ) continue;
                        int idx = ci.CellToIndex(cx, cz);
                        Thing thing = edifice[idx];
                        RoofDef roofDef = SkylightLightHelper.RoofAtForLight(roofGrid, idx);
                        if (roofDef != null && (roofDef.isThickRoof || thing == null || !thing.def.holdsRoof
                                || thing.def.altitudeLayer == AltitudeLayer.DoorMoveable))
                            covered = true;
                        if (IsInwardOpen(map, mode, idx, roofDef))
                            touchesInwardOpen = true;
                        if (thing != null && thing.def.blockLight)
                            touchesBlocker = true;
                        if (thing == null || !thing.def.blockLight)
                        {
                            Color32 g = glow.VisualGlowAt(idx);
                            cr += g.r; cg += g.g; cb += g.b; ca += g.a; n++;
                        }
                    }

                    Color32 col = n > 0
                        ? new Color32((byte)(cr / n), (byte)(cg / n), (byte)(cb / n), (byte)(ca / n))
                        : new Color32(0, 0, 0, 0);
                    // Inward: leave a boundary corner uncovered so the roofed side lights up — but only at a clean
                    // roof/open boundary. If a light-blocking wall meets at this corner, keep vanilla darkening so
                    // outdoor light can't leak through the wall into a sealed room.
                    bool uncover = touchesInwardOpen && !touchesBlocker;
                    if (covered && !uncover && col.a < 100) col.a = 100;
                    array[vBotLeft] = col;
                }
            }

            // Center pass: one vertex per tile, averaged from its 4 corners, then the roof-dark clamp.
            for (int tz = minZ; tz <= maxZ; tz++)
            {
                for (int tx = minX; tx <= maxX; tx++)
                {
                    int centerIdx = firstCenterInd + (tz - minZ) * W + (tx - minX);
                    int tileIdx = ci.CellToIndex(tx, tz);

                    if (filter != null && !filter(tileIdx))
                    {
                        array[centerIdx] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    int bl = (tz - minZ) * (W + 1) + (tx - minX);
                    Color32 a0 = array[bl], a1 = array[bl + 1], a2 = array[bl + W + 1], a3 = array[bl + W + 2];
                    Color32 center = new Color32(
                        (byte)((a0.r + a1.r + a2.r + a3.r) / 4),
                        (byte)((a0.g + a1.g + a2.g + a3.g) / 4),
                        (byte)((a0.b + a1.b + a2.b + a3.b) / 4),
                        (byte)((a0.a + a1.a + a2.a + a3.a) / 4));

                    if (center.a < 100 && SkylightLightHelper.RoofedForLight(roofGrid, tileIdx))
                    {
                        Thing thing = edifice[tileIdx];
                        if ((thing == null || !thing.def.holdsRoof) && !NearInwardOpen(map, mode, tx, tz))
                            center.a = 100;
                    }
                    array[centerIdx] = center;
                }
            }

            subMesh.mesh.colors32 = array;
            return false;
        }

        /// <summary>Is this tile the "open" side the soft edge should fall away from? In Full mode any tile the
        /// overlay reads as unroofed (real sky or a skylight); in SkylightsOnly mode only the mod's skylight tiles,
        /// so real roof edges keep vanilla shading.</summary>
        private static bool IsInwardOpen(Map map, RoofEdgeMode mode, int idx, RoofDef roofDefForLight)
        {
            if (mode == RoofEdgeMode.Full) return roofDefForLight == null;
            IntVec3 c = map.cellIndices.IndexToCell(idx);
            return SkylightGrid.Contains(map, c) || VisualSkyGrid.Contains(map, c);
        }

        /// <summary>True if any of the tile's 8 neighbours is an inward-open tile — i.e. this roofed tile sits on
        /// the boundary ring and should keep its soft (unclamped) center so the falloff lands inward on it.</summary>
        private static bool NearInwardOpen(Map map, RoofEdgeMode mode, int tx, int tz)
        {
            int sizeX = map.Size.x, sizeZ = map.Size.z;
            CellIndices ci = map.cellIndices;
            RoofGrid roofGrid = map.roofGrid;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = tx + dx, nz = tz + dz;
                    if (nx < 0 || nx >= sizeX || nz < 0 || nz >= sizeZ) continue;
                    int nidx = ci.CellToIndex(nx, nz);
                    if (IsInwardOpen(map, mode, nidx, SkylightLightHelper.RoofAtForLight(roofGrid, nidx)))
                        return true;
                }
            }
            return false;
        }
    }
}
