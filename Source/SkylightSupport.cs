using RimWorld;
using UnityEngine;
using Verse;

namespace Skylights
{
    /// <summary>
    /// Roof-support test for weak-glass (tribal) skylights: is there a roof-holding edifice — a wall or
    /// pillar (<c>def.holdsRoof</c>) — within a given radius of a cell? The reinforced electric panes are
    /// self-supporting and never call this; only the low-tech skylight does.
    /// </summary>
    public static class SkylightSupport
    {
        public static bool HasSupportWithin(Map map, IntVec3 center, float radius)
        {
            if (map == null) return false;
            int numCells = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < numCells; i++)
            {
                IntVec3 c = center + GenRadial.RadialPattern[i];
                if (!c.InBounds(map)) continue;
                Building ed = c.GetEdifice(map);
                if (ed != null && ed.def.holdsRoof) return true;
            }
            return false;
        }

        /// <summary>The support radius declared by a def's <see cref="CompProperties_Skylight"/>, or 3 as a fallback.</summary>
        public static float SupportRadiusOf(BuildableDef def)
        {
            if (def is ThingDef td)
            {
                CompProperties_Skylight props = td.GetCompProperties<CompProperties_Skylight>();
                if (props != null && props.requiresNearbySupport) return props.supportRadius;
            }
            return 3f;
        }
    }

    /// <summary>
    /// Blocks installing a weak-glass skylight unless a wall or pillar sits within its support radius, and
    /// draws that radius as a ring while placing. Once built, <see cref="CompSkylight"/> caves the tile in
    /// if the support is later removed.
    /// </summary>
    public class PlaceWorker_NearRoofSupport : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot,
            Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            float radius = SkylightSupport.SupportRadiusOf(checkingDef);
            if (SkylightSupport.HasSupportWithin(map, loc, radius))
                return true;
            return new AcceptanceReport("Skylight_NeedsSupport".Translate(radius.ToString("0")));
        }

        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            GenDraw.DrawRadiusRing(center, SkylightSupport.SupportRadiusOf(def));
        }
    }
}
