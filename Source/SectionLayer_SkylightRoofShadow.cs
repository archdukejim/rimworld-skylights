using RimWorld;
using UnityEngine;
using Verse;

namespace Skylights
{
    /// <summary>
    /// Draws a crisp roof-edge shadow along every roof→open boundary — a room's outer walls and the rim of a
    /// skylight opening (issue #18), replacing the old soft roof-glow edge.
    ///
    /// Approach ("yellow" / wall-top band): rather than computing the wall sprite's exact outer and inner top
    /// edges (hard, and fighting RimWorld's inverted wall perspective), we offset a single shadow BAND onto the
    /// wall-top area and draw it ABOVE the wall so it is not occluded, with a small lip spilling onto the open
    /// side to read as the roof's cast edge. Skylight openings have no wall, so their rim is a thin ground-level
    /// line instead of the full wall-top band.
    ///
    /// The game auto-discovers every non-abstract SectionLayer subclass, so this registers and draws with no
    /// Harmony patch. It rebuilds on Roofs|Buildings changes and is gated by the "Custom roof shadows" setting.
    /// </summary>
    public class SectionLayer_SkylightRoofShadow : SectionLayer
    {
        // ---- Tunable geometry (tiles). Seeded from the wall-top measurement (~0.4–0.45); tuned live. ----
        /// <summary>Depth the wall-top shadow band covers, from the boundary edge into the roofed (wall) cell.</summary>
        private const float BandDepth = 0.45f;
        /// <summary>Small shadow lip spilling onto the open side — the wall's cast roof edge.</summary>
        private const float Lip = 0.08f;
        /// <summary>Thin rim width for skylight openings (no wall there).</summary>
        private const float RimWidth = 0.14f;

        // Per-orientation nudge of the wall-top band, into the roofed cell (tiles). Lets N/S/E/W be tuned
        // separately to sit on the wall-top for each side. Seeded 0.
        private static readonly float OffNorth = 0f;
        private static readonly float OffSouth = 0f;
        private static readonly float OffEast = 0f;
        private static readonly float OffWest = 0f;

        // Shadow darkness via the EdgeShadow multiply material: lower = darker.
        private static readonly Color32 BandCol = new Color32(150, 150, 150, byte.MaxValue); // wall-top band
        private static readonly Color32 RimCol = new Color32(135, 135, 135, byte.MaxValue);  // skylight rim

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
            float yWall = AltitudeLayer.MoteOverhead.AltitudeFor(); // above the wall sprite
            float yGround = AltitudeLayer.Shadows.AltitudeFor();    // ground rim for skylight openings
            CellRect rect = new CellRect(section.botLeft.x, section.botLeft.z, 17, 17);
            rect.ClipInsideMap(map);

            for (int x = rect.minX; x <= rect.maxX; x++)
            {
                for (int z = rect.minZ; z <= rect.maxZ; z++)
                {
                    IntVec3 c = new IntVec3(x, 0, z);
                    if (!IsShadowSide(map, c)) continue;

                    Edge(map, sm, x, z, 0, 1, z + 1, OffNorth);  // north neighbour
                    Edge(map, sm, x, z, 0, -1, z, OffSouth);     // south neighbour
                    Edge(map, sm, x, z, 1, 0, x + 1, OffEast);   // east neighbour
                    Edge(map, sm, x, z, -1, 0, x, OffWest);      // west neighbour
                }
            }

            if (sm.verts.Count > 0)
                sm.FinalizeMesh(MeshParts.Verts | MeshParts.Tris | MeshParts.Colors);
        }

        /// <summary>Emit the shadow for one cardinal edge of roofed cell (x,z) toward neighbour (x+dx,z+dz).
        /// <paramref name="edge"/> is the world coordinate of the shared boundary on the relevant axis.</summary>
        private void Edge(Map map, LayerSubMesh sm, int x, int z, int dx, int dz, float edge, float off)
        {
            int nx = x + dx, nz = z + dz;
            if (nx < 0 || nz < 0 || nx >= map.Size.x || nz >= map.Size.z) return;
            IntVec3 n = new IntVec3(nx, 0, nz);

            bool nRoofed = map.roofGrid.Roofed(n);
            bool nSkylight = SkylightGrid.Contains(map, n);
            if (nRoofed && !nSkylight) return; // neighbour is also roof shadow-side: no boundary here

            float yWall = AltitudeLayer.MoteOverhead.AltitudeFor();
            float yGround = AltitudeLayer.Shadows.AltitudeFor();

            if (nSkylight)
            {
                // Skylight opening: no wall — a thin rim on the roofed side of the boundary, at ground level.
                if (dz != 0)
                {
                    float z0 = dz > 0 ? edge - RimWidth : edge;
                    float z1 = dz > 0 ? edge : edge + RimWidth;
                    AddQuad(sm, yGround, x, z0, x + 1, z1, RimCol);
                }
                else
                {
                    float x0 = dx > 0 ? edge - RimWidth : edge;
                    float x1 = dx > 0 ? edge : edge + RimWidth;
                    AddQuad(sm, yGround, x0, z, x1, z + 1, RimCol);
                }
                return;
            }

            // Exterior (unroofed) boundary: wall-top band drawn above the wall, plus a lip onto the open side.
            if (dz != 0)
            {
                // north/south wall: band runs east–west, spans one tile in x
                float inner = dz > 0 ? edge - BandDepth - off : edge + BandDepth + off; // into the roofed cell
                float outer = dz > 0 ? edge + Lip : edge - Lip;                          // onto the open side
                float z0 = Mathf.Min(inner, outer), z1 = Mathf.Max(inner, outer);
                AddQuad(sm, yWall, x, z0, x + 1, z1, BandCol);
            }
            else
            {
                float inner = dx > 0 ? edge - BandDepth - off : edge + BandDepth + off;
                float outer = dx > 0 ? edge + Lip : edge - Lip;
                float x0 = Mathf.Min(inner, outer), x1 = Mathf.Max(inner, outer);
                AddQuad(sm, yWall, x0, z, x1, z + 1, BandCol);
            }
        }

        /// <summary>A cell is on the shadow-casting (roofed) side if it is roofed and is NOT a sky-rendered
        /// skylight cell.</summary>
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
