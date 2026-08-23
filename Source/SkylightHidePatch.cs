using HarmonyLib;
using Verse;

namespace Skylights
{
    /// <summary>
    /// Player toggle "hide installed skylights": omit the skylight building's sprite while leaving it fully
    /// functional. Skylights are MapMeshOnly buildings (BuildingBase), so their graphic is baked into the
    /// things map-mesh through <see cref="Thing.Print"/>; skipping that call drops the sprite from the mesh
    /// without touching the CompSkylight logic (light channelling, sky rendering, glow all keep working).
    ///
    /// Toggling the setting runs <see cref="SkylightsSettingsMod.WriteSettings"/>, which repaints the
    /// Buildings map-mesh, so hiding/showing takes effect immediately with no reload. When the setting is off
    /// the prefix is a single bool read, so there is no cost on the normal draw path.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Print))]
    public static class Patch_Thing_Print_HideSkylight
    {
        public static bool Prefix(Thing __instance)
        {
            if (!SkylightsSettingsMod.HideSkylights) return true;
            if (__instance is ThingWithComps twc && twc.GetComp<CompSkylight>() != null)
                return false;   // skip printing this skylight's sprite
            return true;
        }
    }
}
