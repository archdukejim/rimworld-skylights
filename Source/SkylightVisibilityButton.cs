using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Skylights
{
    /// <summary>
    /// Skylight visibility HUD button (issue #20): a toggle on the play-settings row (bottom right) that
    /// shows or hides every installed skylight's sprite in play. It drives the same state as the mod menu's
    /// "hide installed skylights" checkbox (issue #21) — button on = skylights drawn, button off = hidden —
    /// so the two controls never disagree, and the choice persists like any other mod setting. Hiding only
    /// skips the sprite; the skylights keep channeling light exactly the same (see Patch_Thing_Print_HideSkylight).
    /// The button itself can be removed via the mod menu's master switch. Default: shown.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SkylightVisibilityButton
    {
        /// <summary>Button art: the dome sprite players already know from the build menu.</summary>
        public static readonly Texture2D ToggleIcon = ContentFinder<Texture2D>.Get("Things/Building/Skylight_Dome");

        /// <summary>Dirty just the map-mesh sections that hold a skylight, so a visibility flip shows the
        /// moment those sections redraw. Building sprites are printed by SectionLayer_ThingsGeneral, whose
        /// relevantChangeTypes is the Things flag — Buildings would regenerate the wrong layers. A whole-map
        /// repaint is also avoided: it regenerates lazily and can lag seconds on a large map.</summary>
        public static void DirtySkylightSections()
        {
            for (int i = 0; i < CompSkylight.SpawnedSkylights.Count; i++)
            {
                Thing t = CompSkylight.SpawnedSkylights[i].parent;
                if (t.Spawned)
                    t.Map.mapDrawer.MapMeshDirty(t.Position, (ulong)MapMeshFlagDefOf.Things,
                        regenAdjacentCells: true, regenAdjacentSections: true);
            }
        }
    }

    [HarmonyPatch(typeof(PlaySettings), nameof(PlaySettings.DoPlaySettingsGlobalControls))]
    public static class Patch_PlaySettings_SkylightVisibility
    {
        public static void Postfix(WidgetRow row, bool worldView)
        {
            SkylightsSettings settings = SkylightsSettingsMod.Settings;
            if (worldView || row == null || settings == null || !settings.skylightVisibilityButton) return;

            bool visible = !settings.hideSkylights;
            bool wasVisible = visible;
            row.ToggleableIcon(ref visible, SkylightVisibilityButton.ToggleIcon,
                "Skylights_VisibilityToggle".Translate(), SoundDefOf.Mouseover_ButtonToggle);
            if (visible == wasVisible) return;

            settings.hideSkylights = !visible;
            settings.Write();
            // The sprite lives in the Buildings map-mesh (Thing.Print), so regenerate the skylight sections.
            SkylightVisibilityButton.DirtySkylightSections();
        }
    }
}
