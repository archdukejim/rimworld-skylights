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
    /// "hide installed skylights" checkbox (issue #21) and the architect-tab hide button — button on =
    /// skylights drawn, button off = hidden — so the controls never disagree, and the choice persists like
    /// any other mod setting. As of v3 the button does more than the sprite: when the mod-menu setting
    /// "Hiding also disables stained-glass light" is on, hiding also mutes the ship windows' coloured glow
    /// (see CompSkylight.RefreshWindowGlow). The daylight channel itself is never touched — hidden skylights
    /// keep lighting and growing exactly the same (see Patch_Thing_Print_HideSkylight).
    /// The button itself can be removed via the mod menu's master switch. Default: shown.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SkylightVisibilityButton
    {
        /// <summary>Button art: the dome sprite players already know from the build menu.</summary>
        public static readonly Texture2D ToggleIcon = ContentFinder<Texture2D>.Get("Things/Building/Skylight_Dome");
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
            // The sprite lives in the Buildings map-mesh (Thing.Print), so regenerate the skylight sections;
            // the ship windows' coloured glow follows the hide state (hideDisablesTintGlow).
            CompSkylight.DirtySkylightSections();
            CompSkylight.RefreshWindowGlow();
        }
    }
}
