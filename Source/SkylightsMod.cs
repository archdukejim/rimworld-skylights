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
            if (map != null && SkylightGrid.Contains(map, map.cellIndices.IndexToCell(index)))
                return null;
            return grid.RoofAt(index);
        }

        public static bool RoofedForLight(RoofGrid grid, int index)
        {
            Map map = MapOf(grid);
            if (map != null && SkylightGrid.Contains(map, map.cellIndices.IndexToCell(index)))
                return false;
            return grid.Roofed(index);
        }
    }
}
