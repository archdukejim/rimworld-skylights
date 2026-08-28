using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Skylights
{
    /// <summary>
    /// Architect > Skylights tab button that shows/hides every installed skylight's sprite (issue #20, v3
    /// form: replaces the old play-settings HUD icon). It drives the same state as the mod menu's "hide
    /// installed skylights" checkbox, so the two controls never disagree, and the choice persists like any
    /// other mod setting. Hiding only skips the sprite; the skylights keep channeling light exactly the same
    /// (see Patch_Thing_Print_HideSkylight). It acts instantly — clicking never selects a targeting tool.
    /// </summary>
    public class Designator_SkylightVisibility : Designator
    {
        public Designator_SkylightVisibility()
        {
            icon = ContentFinder<Texture2D>.Get("Things/Building/Skylight_Dome");
            useMouseIcon = false;
        }

        public override string Label =>
            (SkylightsSettingsMod.HideSkylights ? "Skylights_ShowInstalledCmd" : "Skylights_HideInstalledCmd")
            .Translate();

        public override string Desc => "Skylights_VisibilityCmdDesc".Translate();

        /// <summary>Dim the icon while skylights are hidden, so the tab shows the state at a glance.</summary>
        public override Color IconDrawColor =>
            SkylightsSettingsMod.HideSkylights ? new Color(1f, 1f, 1f, 0.4f) : Color.white;

        public override AcceptanceReport CanDesignateCell(IntVec3 loc) => false;

        public override void ProcessInput(Event ev)
        {
            // Instant toggle — deliberately not calling base, which would select this as a targeting tool.
            SkylightsSettings settings = SkylightsSettingsMod.Settings;
            if (settings == null) return;
            settings.hideSkylights = !settings.hideSkylights;
            settings.Write();
            CompSkylight.DirtySkylightSections();
            SoundDefOf.Mouseover_ButtonToggle.PlayOneShotOnCamera();
        }
    }

    /// <summary>
    /// Architect > Skylights tab button that opens a slider for the global skylight sprite opacity
    /// (10–100%). The value lands in <see cref="SkylightsSettings.skylightOpacity"/> and is pushed onto the
    /// defs by <see cref="SkylightOpacity"/>, fading every installed skylight's glass/frame art so players
    /// can keep it as subtle or as visible as they like.
    /// </summary>
    public class Designator_SkylightOpacity : Designator
    {
        public Designator_SkylightOpacity()
        {
            icon = ContentFinder<Texture2D>.Get("Things/Building/Skylight_Paned");
            useMouseIcon = false;
        }

        public override string Label => "Skylights_OpacityCmd".Translate();

        public override string Desc =>
            "Skylights_OpacityCmdDesc".Translate(CurrentPercent());

        public override AcceptanceReport CanDesignateCell(IntVec3 loc) => false;

        private static int CurrentPercent() =>
            Mathf.RoundToInt((SkylightsSettingsMod.Settings?.skylightOpacity ?? 1f) * 100f);

        public override void ProcessInput(Event ev)
        {
            // Opens the slider dialog — deliberately not calling base, which would select a targeting tool.
            SoundDefOf.Click.PlayOneShotOnCamera();
            Find.WindowStack.Add(new Dialog_Slider(
                v => "Skylights_OpacityLabel".Translate(v),
                Mathf.RoundToInt(SkylightsSettings.MinOpacity * 100f), 100,
                delegate (int v)
                {
                    SkylightsSettings settings = SkylightsSettingsMod.Settings;
                    if (settings == null) return;
                    settings.skylightOpacity = v / 100f;
                    settings.Write();
                    SkylightOpacity.Apply();
                    CompSkylight.DirtySkylightSections();
                },
                CurrentPercent()));
        }
    }
}
