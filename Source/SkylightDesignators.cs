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
            useMouseIcon = false;
        }

        /// <summary>Live per-state icon: the same circle/check/X badges the HUD button shows. The icon is
        /// a plain field read at draw time, so refresh it just before the base draw.</summary>
        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            icon = SkylightVisibilityButton.IconFor(SkylightsSettingsMod.DisplayMode);
            return base.GizmoOnGUI(topLeft, maxWidth, parms);
        }

        public override string Label
        {
            get
            {
                switch (SkylightsSettingsMod.DisplayMode)
                {
                    case SkylightDisplayMode.Selectable: return "Skylights_Cmd_Selectable".Translate();
                    case SkylightDisplayMode.Hidden: return "Skylights_Cmd_Hidden".Translate();
                    default: return "Skylights_Cmd_Visible".Translate();
                }
            }
        }

        public override string Desc => "Skylights_VisibilityCmdDesc".Translate();

        public override AcceptanceReport CanDesignateCell(IntVec3 loc) => false;

        public override void ProcessInput(Event ev)
        {
            // Instant cycle — deliberately not calling base, which would select this as a targeting tool.
            SkylightsSettingsMod.CycleDisplayMode();
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
