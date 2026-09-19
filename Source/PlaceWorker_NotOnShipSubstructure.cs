using Verse;

namespace Skylights
{
    /// <summary>
    /// Keeps ordinary skylights off Odyssey gravships: their glass isn't rated for a ship's flight
    /// stresses, so installing over ship substructure is blocked. Ship windows — the structurally
    /// supported glazing, marked by <see cref="CompProperties_Skylight.spaceAware"/> — are exempt and
    /// remain the one way to glaze a gravship roof. Attached to the shared skylight base def, so every
    /// current and future skylight is covered without opting in.
    ///
    /// A cell counts as ship deck when its foundation layer is substructure (FoundationAt survives
    /// flooring built on top) or its live terrain itself is substructure; multi-cell buildings
    /// (atriums) are rejected if any footprint cell touches the ship.
    /// </summary>
    public class PlaceWorker_NotOnShipSubstructure : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot,
            Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            ThingDef def = checkingDef as ThingDef;
            CompProperties_Skylight skylight = def?.GetCompProperties<CompProperties_Skylight>();
            // Ship windows are built for this — structurally supported glazing passes.
            if (skylight != null && skylight.spaceAware) return true;

            foreach (IntVec3 c in GenAdj.OccupiedRect(loc, rot, def?.size ?? IntVec2.One))
            {
                if (!c.InBounds(map)) continue;
                TerrainDef foundation = map.terrainGrid.FoundationAt(c);
                if ((foundation != null && foundation.IsSubstructure) || map.terrainGrid.TerrainAt(c).IsSubstructure)
                    return new AcceptanceReport("Skylights_NotOnShip".Translate());
            }
            return true;
        }
    }
}
