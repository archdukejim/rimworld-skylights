using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Skylights
{
    /// <summary>
    /// Skylight display HUD button (issue #20, three-state since v3): a button on the play-settings row
    /// (bottom right) that cycles how installed skylights present themselves —
    /// Selectable (circle badge: sprites drawn and the buildings are directly clickable),
    /// Visible (check badge: sprites drawn, clicks pass through — the classic behaviour), and
    /// Hidden (X badge: sprites hidden — daylight, crops and stained-glass light keep working).
    /// It drives the same persisted state as the mod-menu radios and the architect-tab button, so the
    /// controls never disagree. Whatever the mode, skylights keep channeling light exactly the same
    /// (see Patch_Thing_Print_HideSkylight). The button itself can be removed via the mod menu's master
    /// switch. Default: shown, mode Visible.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SkylightVisibilityButton
    {
        public static readonly Texture2D IconSelectable = ContentFinder<Texture2D>.Get("UI/SkylightHUD_Selectable");
        public static readonly Texture2D IconVisible = ContentFinder<Texture2D>.Get("UI/SkylightHUD_Visible");
        public static readonly Texture2D IconHidden = ContentFinder<Texture2D>.Get("UI/SkylightHUD_Hidden");

        public static Texture2D IconFor(SkylightDisplayMode mode)
        {
            switch (mode)
            {
                case SkylightDisplayMode.Selectable: return IconSelectable;
                case SkylightDisplayMode.Hidden: return IconHidden;
                default: return IconVisible;
            }
        }

        public static string TooltipFor(SkylightDisplayMode mode)
        {
            switch (mode)
            {
                case SkylightDisplayMode.Selectable: return "Skylights_HUD_Selectable".Translate();
                case SkylightDisplayMode.Hidden: return "Skylights_HUD_Hidden".Translate();
                default: return "Skylights_HUD_Visible".Translate();
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

            SkylightDisplayMode mode = settings.displayMode;
            if (row.ButtonIcon(SkylightVisibilityButton.IconFor(mode),
                SkylightVisibilityButton.TooltipFor(mode)))
            {
                SkylightsSettingsMod.CycleDisplayMode();
                SoundDefOf.Mouseover_ButtonToggle.PlayOneShotOnCamera();
            }
        }
    }
}
