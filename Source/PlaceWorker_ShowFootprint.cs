using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Skylights
{
    /// <summary>
    /// Placement-time footprint overlay for the multi-cell domes (2x1, 2x2). Their installed graphic is a
    /// single 1x1 dome sprite drawn centred on the footprint, so while placing, the ghost alone gives no
    /// hint of which tiles the building will actually occupy. This place worker draws the occupied tiles'
    /// edges (tinted with the ghost's valid/blocked colour) plus vanilla-style selection brackets on the
    /// footprint corners, every frame the placement ghost is being moved or rotated.
    ///
    /// It also replaces the def's specialDisplayRadius ring (which vanilla centres on the root cell, so it
    /// sits off-centre on an even footprint) with a single circle around the true footprint centre — a
    /// half-tile-offset point vanilla's cell-centred ring can't express. Its radius is the glow-node reach
    /// plus the footprint half-extent, approximating how far the pooled per-cell glowers light once built.
    /// The defs therefore declare no specialDisplayRadius.
    /// </summary>
    [StaticConstructorOnStartup]
    public class PlaceWorker_ShowFootprint : PlaceWorker
    {
        // Same corner-bracket material the selection overlay uses, so the footprint reads instantly.
        private static readonly Material BracketMat =
            MaterialPool.MatFrom("UI/Overlays/SelectionBracket", ShaderDatabase.MetaOverlay);

        private static readonly List<IntVec3> cellsTmp = new List<IntVec3>();
        private static readonly Vector3[] bracketLocs = new Vector3[4];

        /// <summary>The radius each footprint cell will actually glow at once built: the glow node's radius
        /// for pooled multi-cell domes, else the def's own glower radius. 0 when the def has neither.</summary>
        private static float LightRadiusOf(ThingDef def)
        {
            CompProperties_Skylight sk = def.GetCompProperties<CompProperties_Skylight>();
            if (sk?.glowNodeDef != null)
            {
                ThingDef node = DefDatabase<ThingDef>.GetNamedSilentFail(sk.glowNodeDef);
                CompProperties_Glower nodeGlow = node?.GetCompProperties<CompProperties_Glower>();
                if (nodeGlow != null) return nodeGlow.glowRadius;
            }
            return def.GetCompProperties<CompProperties_Glower>()?.glowRadius ?? 0f;
        }

        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            CellRect rect = GenAdj.OccupiedRect(center, rot, def.Size);

            // Tile edges around the whole footprint, in the ghost's colour (green = placeable, red = blocked)
            // at full opacity so the outline stays readable over any floor.
            cellsTmp.Clear();
            foreach (IntVec3 c in rect)
                cellsTmp.Add(c);
            GenDraw.DrawFieldEdges(cellsTmp, new Color(ghostCol.r, ghostCol.g, ghostCol.b, 1f));

            // The true footprint centre in world space — for even footprints this is a tile corner, the
            // point vanilla's cell-centred radius ring can't centre on.
            float cx = rect.minX + rect.Width / 2f;
            float cz = rect.minZ + rect.Height / 2f;

            // Light-reach circle: one smooth ring around the true centre (the same line style vanilla uses
            // for mortar ranges), radius = per-cell glow reach plus the footprint half-extent. A single
            // circle symmetric about the footprint — no overlapping per-node lobes, no cell stair-steps.
            float lightRadius = LightRadiusOf(def);
            if (lightRadius > 0f)
            {
                float reach = lightRadius + 0.5f * (Mathf.Max(rect.Width, rect.Height) - 1f);
                GenDraw.DrawCircleOutline(
                    new Vector3(cx, AltitudeLayer.MetaOverlays.AltitudeFor(), cz), reach, SimpleColor.White);
            }

            // Corner brackets, laid out exactly like SelectionDrawerUtility.CalculateSelectionBracketPositionsWorld
            // (a 1x1 bracket quad centred on each corner tile, rotated -90 degrees per corner) but without the
            // select-time jump animation, since the ghost is never "selected".
            float y = AltitudeLayer.MetaOverlays.AltitudeFor();
            float dx = 0.5f * (rect.Width - 1f);
            float dz = 0.5f * (rect.Height - 1f);
            bracketLocs[0] = new Vector3(cx - dx, y, cz - dz);
            bracketLocs[1] = new Vector3(cx + dx, y, cz - dz);
            bracketLocs[2] = new Vector3(cx + dx, y, cz + dz);
            bracketLocs[3] = new Vector3(cx - dx, y, cz + dz);
            int angle = 0;
            for (int i = 0; i < 4; i++)
            {
                Graphics.DrawMesh(MeshPool.plane10,
                    Matrix4x4.TRS(bracketLocs[i], Quaternion.AngleAxis(angle, Vector3.up), Vector3.one),
                    BracketMat, 0);
                angle -= 90;
            }
        }
    }
}
