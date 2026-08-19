using RimWorld;
using UnityEngine;
using Verse;

namespace Skylights
{
    /// <summary>
    /// Draws a crisp roof-edge shadow line along every roof→open boundary — a room's outer walls and the rim
    /// of a skylight opening (issue #18), replacing the old soft roof-glow edge.
    ///
    /// Model (per user tuning): the line is drawn ON the tile edge between a roofed cell and an open one.
    /// <b>Offset 0 = the line sits exactly on that edge.</b> The per-orientation offset then slides the line
    /// perpendicular to the edge, from −1 tile (a full tile into the OPEN side) to +1 tile (a full tile into the
    /// ROOFED side), so it can be placed wherever the roof graphic's edge should read — including outside the
    /// cell it belongs to. Drawn above the wall (MoteOverhead) so it is never occluded by the wall sprite.
    ///
    /// The game auto-discovers every non-abstract SectionLayer subclass, so this registers and draws with no
    /// Harmony patch. It rebuilds on Roofs|Buildings changes and is gated by the "Custom roof shadows" setting.
    /// Geometry (thickness, darkness, per-orientation offset) is read live from the mod-menu sliders.
    /// </summary>
    public class SectionLayer_SkylightRoofShadow : SectionLayer
    {
        // Live-tuning values, read from the mod settings each regenerate.
        private static float Thickness => SkylightsSettingsMod.Settings?.rsDepth ?? 0.2f;
        /// <summary>Uniform up-shift (+z / north) applied to the whole shadow to match the wall-top perspective.</summary>
        private static float VertOffset => SkylightsSettingsMod.Settings?.rsVertOffset ?? 0.37f;

        private static Color32 ShadowCol
        {
            get { byte g = (byte)Mathf.Clamp(SkylightsSettingsMod.Settings?.rsDark ?? 150f, 0f, 255f); return new Color32(g, g, g, byte.MaxValue); }
        }

        public SectionLayer_SkylightRoofShadow(Section section)
            : base(section)
        {
            relevantChangeTypes = (ulong)MapMeshFlagDefOf.Roofs | (ulong)MapMeshFlagDefOf.Buildings;
        }

        public override bool Visible => SkylightsSettingsMod.CustomRoofShadows;

        public override void Regenerate()
        {
            LayerSubMesh sm = GetSubMesh(MatBases.EdgeShadow);
            sm.Clear(MeshParts.All);

            Map map = base.Map;
            CellRect rect = new CellRect(section.botLeft.x, section.botLeft.z, 17, 17);
            rect.ClipInsideMap(map);

            for (int x = rect.minX; x <= rect.maxX; x++)
            {
                for (int z = rect.minZ; z <= rect.maxZ; z++)
                {
                    IntVec3 c = new IntVec3(x, 0, z);
                    if (!IsShadowSide(map, c)) continue;

                    Edge(map, sm, x, z, 0, 1, z + 1);  // north neighbour
                    Edge(map, sm, x, z, 0, -1, z);     // south neighbour
                    Edge(map, sm, x, z, 1, 0, x + 1);  // east neighbour
                    Edge(map, sm, x, z, -1, 0, x);     // west neighbour
                }
            }

            if (sm.verts.Count > 0)
                sm.FinalizeMesh(MeshParts.Verts | MeshParts.Tris | MeshParts.Colors);
        }

        /// <summary>Draw the shadow line for one cardinal edge of roofed cell (x,z) toward neighbour
        /// (x+dx,z+dz). The line sits on the boundary <paramref name="edge"/>, then the whole shadow is shifted
        /// up (+z) by <see cref="VertOffset"/> to sit on the wall-top in RimWorld's perspective.</summary>
        private void Edge(Map map, LayerSubMesh sm, int x, int z, int dx, int dz, float edge)
        {
            int nx = x + dx, nz = z + dz;
            if (nx < 0 || nz < 0 || nx >= map.Size.x || nz >= map.Size.z) return;
            IntVec3 n = new IntVec3(nx, 0, nz);
            if (map.roofGrid.Roofed(n) && !SkylightGrid.Contains(map, n)) return; // neighbour also roofed: no boundary

            float y = AltitudeLayer.MoteOverhead.AltitudeFor();
            float half = Mathf.Max(0.01f, Thickness) * 0.5f;
            float vo = VertOffset;
            Color32 col = ShadowCol;

            if (dz != 0)   // north/south edge: horizontal line at z = edge
                AddQuad(sm, y, x, edge - half + vo, x + 1, edge + half + vo, col);
            else           // east/west edge: vertical line at x = edge
                AddQuad(sm, y, edge - half, z + vo, edge + half, z + 1 + vo, col);
        }

        /// <summary>Roofed and not a sky-rendered skylight cell.</summary>
        private static bool IsShadowSide(Map map, IntVec3 c)
        {
            return map.roofGrid.Roofed(c) && !SkylightGrid.Contains(map, c);
        }

        /// <summary>Append one flat, uniformly-shaded quad (two triangles) spanning [x0,x1]×[z0,z1] at
        /// altitude y.</summary>
        private static void AddQuad(LayerSubMesh sm, float y, float x0, float z0, float x1, float z1, Color32 col)
        {
            int baseIdx = sm.verts.Count;
            sm.verts.Add(new Vector3(x0, y, z0));
            sm.verts.Add(new Vector3(x0, y, z1));
            sm.verts.Add(new Vector3(x1, y, z1));
            sm.verts.Add(new Vector3(x1, y, z0));
            sm.colors.Add(col);
            sm.colors.Add(col);
            sm.colors.Add(col);
            sm.colors.Add(col);
            sm.tris.Add(baseIdx);
            sm.tris.Add(baseIdx + 1);
            sm.tris.Add(baseIdx + 2);
            sm.tris.Add(baseIdx);
            sm.tris.Add(baseIdx + 2);
            sm.tris.Add(baseIdx + 3);
        }
    }
}
