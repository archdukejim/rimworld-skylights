using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Skylights
{
    /// <summary>
    /// Per-map height field (in wall-height units) for the height-based roof/mountain shadow system.
    ///
    /// height(cell) =
    ///   • roof-type base — none 0, constructed roof 0.15, thin rock roof 1, thick rock roof 2; plus
    ///   • mountain growth — inside a thick-roof mass the height climbs +1 per concentric solid ring
    ///     ("constant ring" 45° stepped pyramid), i.e. the Chebyshev distance to the edge of the mass:
    ///     the thick-roof edge = 2, one ring in = 3 ("mountain 3 to start"), two rings in = 4, …
    ///
    /// Rebuilt lazily; a roof change marks it dirty (see <see cref="Patch_RoofGrid_SetRoof_Elevation"/>).
    /// </summary>
    public static class ElevationGrid
    {
        private static readonly Dictionary<Map, float[]> byMap = new Dictionary<Map, float[]>();
        private static readonly HashSet<Map> dirty = new HashSet<Map>();

        public static void MarkDirty(Map map)
        {
            if (map != null) dirty.Add(map);
        }

        public static float HeightAt(Map map, int index)
        {
            float[] g = Grid(map);
            return (index >= 0 && index < g.Length) ? g[index] : 0f;
        }

        public static float HeightAt(Map map, IntVec3 c)
        {
            return HeightAt(map, map.cellIndices.CellToIndex(c));
        }

        private static float[] Grid(Map map)
        {
            if (byMap.TryGetValue(map, out float[] g) && !dirty.Contains(map))
                return g;
            g = Recompute(map);
            byMap[map] = g;
            dirty.Remove(map);
            return g;
        }

        private static float RoofBase(RoofDef r)
        {
            if (r == null) return 0f;
            if (r.isThickRoof) return 2f;                 // thick rock roof (mountain base)
            if (r == RoofDefOf.RoofConstructed) return 0.15f;
            return 1f;                                    // thin natural rock roof
        }

        private static float[] Recompute(Map map)
        {
            CellIndices ci = map.cellIndices;
            RoofGrid rg = map.roofGrid;
            int sx = map.Size.x, sz = map.Size.z;
            int n = ci.NumGridCells;
            float[] h = new float[n];

            // Chebyshev distance-to-edge for thick-roof cells → constant-ring mountain growth.
            const int INF = 1 << 20;
            int[] dist = new int[n];
            for (int i = 0; i < n; i++)
            {
                RoofDef r = rg.RoofAt(i);
                bool thick = r != null && r.isThickRoof;
                dist[i] = thick ? INF : 0;
                h[i] = RoofBase(r);
            }

            // Forward pass (W, S, SW, SE already-processed neighbours).
            for (int z = 0; z < sz; z++)
                for (int x = 0; x < sx; x++)
                {
                    int idx = ci.CellToIndex(x, z);
                    if (dist[idx] == 0) continue;
                    int best = dist[idx];
                    if (x > 0) best = Math.Min(best, dist[ci.CellToIndex(x - 1, z)] + 1);
                    if (z > 0) best = Math.Min(best, dist[ci.CellToIndex(x, z - 1)] + 1);
                    if (x > 0 && z > 0) best = Math.Min(best, dist[ci.CellToIndex(x - 1, z - 1)] + 1);
                    if (x < sx - 1 && z > 0) best = Math.Min(best, dist[ci.CellToIndex(x + 1, z - 1)] + 1);
                    dist[idx] = best;
                }

            // Backward pass (E, N, NE, NW).
            for (int z = sz - 1; z >= 0; z--)
                for (int x = sx - 1; x >= 0; x--)
                {
                    int idx = ci.CellToIndex(x, z);
                    if (dist[idx] == 0) continue;
                    int best = dist[idx];
                    if (x < sx - 1) best = Math.Min(best, dist[ci.CellToIndex(x + 1, z)] + 1);
                    if (z < sz - 1) best = Math.Min(best, dist[ci.CellToIndex(x, z + 1)] + 1);
                    if (x < sx - 1 && z < sz - 1) best = Math.Min(best, dist[ci.CellToIndex(x + 1, z + 1)] + 1);
                    if (x > 0 && z < sz - 1) best = Math.Min(best, dist[ci.CellToIndex(x - 1, z + 1)] + 1);
                    dist[idx] = best;
                    if (best > 0 && best < INF)
                        h[idx] = 1f + best;   // thick edge (dist 1) = 2; each ring inward +1
                }

            return h;
        }
    }

    /// <summary>A roof change invalidates the cached elevation grid for that map.</summary>
    [HarmonyPatch(typeof(RoofGrid), nameof(RoofGrid.SetRoof))]
    public static class Patch_RoofGrid_SetRoof_Elevation
    {
        private static readonly AccessTools.FieldRef<RoofGrid, Map> MapOf =
            AccessTools.FieldRefAccess<RoofGrid, Map>("map");

        public static void Postfix(RoofGrid __instance)
        {
            ElevationGrid.MarkDirty(MapOf(__instance));
        }
    }
}
