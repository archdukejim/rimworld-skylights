using RimWorld;
using UnityEngine;
using Verse;

namespace Skylights
{
    /// <summary>
    /// Dev-only visualization of <see cref="ElevationGrid"/>: tints each cell darker the taller it is, so the
    /// height field (roof bases + stepped mountain growth) can be verified by eye before shadows are cast from
    /// it. Gated behind the "show elevation" dev toggle. Uses the EdgeShadow multiply material.
    /// </summary>
    public class SectionLayer_ElevationDebug : SectionLayer
    {
        public SectionLayer_ElevationDebug(Section section)
            : base(section)
        {
            relevantChangeTypes = (ulong)MapMeshFlagDefOf.Roofs | (ulong)MapMeshFlagDefOf.Buildings;
        }

        public override bool Visible => SkylightsSettingsMod.ShowElevationDebug;

        public override void Regenerate()
        {
            LayerSubMesh sm = GetSubMesh(MatBases.EdgeShadow);
            sm.Clear(MeshParts.All);

            Map map = base.Map;
            float y = AltitudeLayer.MoteOverhead.AltitudeFor();
            CellRect rect = new CellRect(section.botLeft.x, section.botLeft.z, 17, 17);
            rect.ClipInsideMap(map);
            CellIndices ci = map.cellIndices;

            for (int x = rect.minX; x <= rect.maxX; x++)
            {
                for (int z = rect.minZ; z <= rect.maxZ; z++)
                {
                    float h = ElevationGrid.HeightAt(map, ci.CellToIndex(x, z));
                    if (h <= 0f) continue;
                    // taller = darker; each height unit ≈ 40 grey steps, capped so it stays readable
                    byte g = (byte)Mathf.Clamp(255f - Mathf.Min(h * 40f, 210f), 0f, 255f);
                    Color32 col = new Color32(g, g, g, byte.MaxValue);
                    AddQuad(sm, y, x, z, x + 1, z + 1, col);
                }
            }

            if (sm.verts.Count > 0)
                sm.FinalizeMesh(MeshParts.Verts | MeshParts.Tris | MeshParts.Colors);
        }

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
