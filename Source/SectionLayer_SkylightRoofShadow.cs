using RimWorld;
using UnityEngine;
using Verse;

namespace Skylights
{
    /// <summary>
    /// Draws a crisp, hard shadow line along every roof→open boundary — the perimeter where a roofed area
    /// meets open sky (a room's outer walls) and the rim of a skylight opening (a sky-rendered cell inside a
    /// roof). This replaces the old soft roof-glow edge (issue #18) with an architectural line aligned to
    /// where the top of the wall visually sits.
    ///
    /// The game auto-discovers every non-abstract SectionLayer subclass (Verse.Section iterates
    /// <c>typeof(SectionLayer).AllSubclassesNonAbstract()</c> and instantiates each with the Section), so this
    /// class registers and draws with no Harmony patch. It rebuilds whenever roofs or buildings change and is
    /// gated behind the "Custom roof shadows" setting via <see cref="Visible"/>.
    ///
    /// NOTE (offset tuning): the per-orientation offsets below are seeded from an in-game measurement of the
    /// vanilla wall-top (the lit top strip sits ~0.4 tile in from the wall base, biased toward the forward /
    /// south edge, and its position differs by orientation). They are deliberately separate, signed constants
    /// so the line can be nudged onto the wall-top for each of N/S/E/W independently while tuning live.
    /// </summary>
    public class SectionLayer_SkylightRoofShadow : SectionLayer
    {
        // ---- Tunable geometry (tiles). Seeded from the wall-top measurement; fine-tuned live. ----
        /// <summary>Thickness of the hard shadow line.</summary>
        private const float LineWidth = 0.12f;

        // Signed offset of the line from the boundary edge, per orientation. Positive = toward the OPEN side,
        // negative = into the ROOFED side. Seeded at the measured ~0.4-tile wall-top inset.
        private static readonly float OffNorth = 0.40f;
        private static readonly float OffSouth = 0.40f;
        private static readonly float OffEast = 0.40f;
        private static readonly float OffWest = 0.40f;

        /// <summary>Line darkness via the EdgeShadow multiply material: lower = darker. 195 = vanilla edge
        /// shadow; we go darker for a crisp line.</summary>
        private const byte ShadowGray = 140;
        private static readonly Color32 ShadowCol = new Color32(ShadowGray, ShadowGray, ShadowGray, byte.MaxValue);

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
            float y = AltitudeLayer.Shadows.AltitudeFor();
            CellRect rect = new CellRect(section.botLeft.x, section.botLeft.z, 17, 17);
            rect.ClipInsideMap(map);

            for (int x = rect.minX; x <= rect.maxX; x++)
            {
                for (int z = rect.minZ; z <= rect.maxZ; z++)
                {
                    IntVec3 c = new IntVec3(x, 0, z);
                    if (!IsShadowSide(map, c)) continue;

                    // North boundary: open cell to the north (higher z). Line runs east–west at the top edge.
                    if (IsOpen(map, x, z + 1))
                    {
                        float zc = (z + 1) + OffNorth;
                        AddQuad(sm, y, x, zc - LineWidth * 0.5f, x + 1, zc + LineWidth * 0.5f);
                    }
                    // South boundary: open cell to the south (lower z).
                    if (IsOpen(map, x, z - 1))
                    {
                        float zc = z - OffSouth;
                        AddQuad(sm, y, x, zc - LineWidth * 0.5f, x + 1, zc + LineWidth * 0.5f);
                    }
                    // East boundary: open cell to the east. Line runs north–south at the right edge.
                    if (IsOpen(map, x + 1, z))
                    {
                        float xc = (x + 1) + OffEast;
                        AddQuad(sm, y, xc - LineWidth * 0.5f, z, xc + LineWidth * 0.5f, z + 1);
                    }
                    // West boundary: open cell to the west.
                    if (IsOpen(map, x - 1, z))
                    {
                        float xc = x - OffWest;
                        AddQuad(sm, y, xc - LineWidth * 0.5f, z, xc + LineWidth * 0.5f, z + 1);
                    }
                }
            }

            if (sm.verts.Count > 0)
                sm.FinalizeMesh(MeshParts.Verts | MeshParts.Tris | MeshParts.Colors);
        }

        /// <summary>A cell is on the shadow-casting (roofed) side if it is roofed and is NOT a sky-rendered
        /// skylight cell — a skylight cell reads as open sky, so it belongs to the open side.</summary>
        private static bool IsShadowSide(Map map, IntVec3 c)
        {
            return map.roofGrid.Roofed(c) && !SkylightGrid.Contains(map, c);
        }

        /// <summary>Open side = anything the roof shadow should butt up against: unroofed sky, or a
        /// sky-rendered skylight cell. Out-of-bounds counts as closed (no line at the map border).</summary>
        private static bool IsOpen(Map map, int x, int z)
        {
            if (x < 0 || z < 0 || x >= map.Size.x || z >= map.Size.z) return false;
            IntVec3 c = new IntVec3(x, 0, z);
            return !map.roofGrid.Roofed(c) || SkylightGrid.Contains(map, c);
        }

        /// <summary>Append one flat, uniformly-shaded quad (two triangles) spanning [x0,x1]×[z0,z1] at
        /// altitude y.</summary>
        private static void AddQuad(LayerSubMesh sm, float y, float x0, float z0, float x1, float z1)
        {
            int baseIdx = sm.verts.Count;
            sm.verts.Add(new Vector3(x0, y, z0));
            sm.verts.Add(new Vector3(x0, y, z1));
            sm.verts.Add(new Vector3(x1, y, z1));
            sm.verts.Add(new Vector3(x1, y, z0));
            sm.colors.Add(ShadowCol);
            sm.colors.Add(ShadowCol);
            sm.colors.Add(ShadowCol);
            sm.colors.Add(ShadowCol);
            sm.tris.Add(baseIdx);
            sm.tris.Add(baseIdx + 1);
            sm.tris.Add(baseIdx + 2);
            sm.tris.Add(baseIdx);
            sm.tris.Add(baseIdx + 2);
            sm.tris.Add(baseIdx + 3);
        }
    }
}
