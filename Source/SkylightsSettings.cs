using UnityEngine;
using RimWorld;
using Verse;

namespace Skylights
{
    /// <summary>When the visual roof overlay (a soft dark tint over roofed cells) is drawn.</summary>
    public enum RoofOverlayMode
    {
        /// <summary>No overlay — vanilla roof rendering only.</summary>
        Vanilla = 0,
        /// <summary>Interior tint appears only where the roof is dark (lit cells show through, like vanilla);
        /// roofs over walls/doors are always shadowed. The default.</summary>
        LitAware = 1,
        /// <summary>Tint every roofed cell always.</summary>
        AlwaysOn = 2,
    }

    /// <summary>Player-tunable settings for the mod.</summary>
    public class SkylightsSettings : ModSettings
    {
        public const int MinDomeGlowRadius = 1;
        public const int MaxDomeGlowRadius = 10;
        /// <summary>Default is the shipped dome radius (~3) plus one tile.</summary>
        public const int DefaultDomeGlowRadius = 4;

        public int domeGlowRadius = DefaultDomeGlowRadius;

        /// <summary>Draw the roof-edge border LINES (issue #18). Now primarily a tuning tool for dialing the
        /// vertical offset; the shipped visual is the roof overlay below. Default off.</summary>
        public bool customRoofShadows = false;

        /// <summary>Visual roof overlay (issue #19): a soft ~20% black tint over roofed cells so roofs read at a
        /// glance. Skylight cells penetrate it (open sky shows through). Default LitAware.</summary>
        public RoofOverlayMode roofOverlayMode = RoofOverlayMode.LitAware;

        /// <summary>Overlay tint strength as an EdgeShadow multiply grey (0=black, 255=none). 204 ≈ 20% black.</summary>
        public float roofOverlayDark = 204f;

        /// <summary>Cosmetically nudge the installed skylight sprite by the wall-top offset so it lines up with
        /// the offset roof-edge shadow. Purely visual; the cell it occupies and lights is unchanged. Default off.</summary>
        public bool offsetSkylightSprite = false;

        // --- Roof-shadow live tuning (issue #18). Exposed as sliders so the wall-top placement can be dialed
        //     in-game; the final values get baked back into constants before release. ---
        /// <summary>Thickness of the shadow line (tiles).</summary>
        public float rsDepth = 0.2f;
        /// <summary>Deprecated (old band model); retained for save compatibility.</summary>
        public float rsLip = 0.08f;
        /// <summary>Band darkness as an EdgeShadow multiply grey (0=black, 255=none); lower = darker.</summary>
        public float rsDark = 150f;
        /// <summary>Uniform up-shift (+z / north) of the whole roof-shadow/overlay, to sit on the wall-top in
        /// RimWorld's perspective. Default 0.20 tile (tuned in-game). Shared by the border lines and overlay.</summary>
        public float rsVertOffset = 0.20f;
        /// <summary>Deprecated per-orientation offsets (old model); retained for save compatibility.</summary>
        public float rsOffN = 0f, rsOffS = 0f, rsOffE = 0f, rsOffW = 0f;
        /// <summary>Vertical nudge (tiles, +z = up/north) applied to skylight sprites when the offset toggle is on.</summary>
        public float spriteOffZ = 0.35f;

        /// <summary>Dev-only: tint every cell by its computed elevation (taller = darker) to verify the height field.</summary>
        public bool showElevationDebug = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref domeGlowRadius, "domeGlowRadius", DefaultDomeGlowRadius);
            Scribe_Values.Look(ref customRoofShadows, "customRoofShadows", false);
            Scribe_Values.Look(ref roofOverlayMode, "roofOverlayMode", RoofOverlayMode.LitAware);
            Scribe_Values.Look(ref roofOverlayDark, "roofOverlayDark", 204f);
            Scribe_Values.Look(ref offsetSkylightSprite, "offsetSkylightSprite", false);
            Scribe_Values.Look(ref rsDepth, "rsDepth", 0.2f);
            Scribe_Values.Look(ref rsLip, "rsLip", 0.08f);
            Scribe_Values.Look(ref rsDark, "rsDark", 150f);
            Scribe_Values.Look(ref rsVertOffset, "rsVertOffset", 0.20f);
            Scribe_Values.Look(ref spriteOffZ, "spriteOffZ", 0.35f);
            Scribe_Values.Look(ref showElevationDebug, "showElevationDebug", false);
            base.ExposeData();
        }
    }

    /// <summary>
    /// Mod entry that holds <see cref="SkylightsSettings"/> and draws the settings window (a 1–10 slider for
    /// the dome skylight light radius). Applying the value lives in <see cref="DomeGlowRadius"/>.
    /// </summary>
    public class SkylightsSettingsMod : Mod
    {
        public static SkylightsSettings Settings;

        public SkylightsSettingsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<SkylightsSettings>();
        }

        /// <summary>Fast, null-safe read of the custom-roof-shadows toggle for the render layer's Visible check.</summary>
        public static bool CustomRoofShadows => Settings?.customRoofShadows ?? true;

        /// <summary>Null-safe read of the skylight-sprite-offset toggle.</summary>
        public static bool OffsetSkylightSprite => Settings?.offsetSkylightSprite ?? false;

        /// <summary>Null-safe read of the roof-overlay mode.</summary>
        public static RoofOverlayMode RoofOverlay => Settings?.roofOverlayMode ?? RoofOverlayMode.LitAware;

        /// <summary>Null-safe read of the elevation-debug toggle.</summary>
        public static bool ShowElevationDebug => Settings?.showElevationDebug ?? false;

        public override string SettingsCategory() => "Skylights";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            list.Label("Skylights_DomeRadius".Translate(Settings.domeGlowRadius));
            Settings.domeGlowRadius = Mathf.RoundToInt(list.Slider(
                Settings.domeGlowRadius, SkylightsSettings.MinDomeGlowRadius, SkylightsSettings.MaxDomeGlowRadius));
            list.Gap(6f);
            list.Label("Skylights_DomeRadiusDesc".Translate());

            list.GapLine(12f);

            // --- Roof overlay (issue #19): the shipped visual roof rendering. ---
            list.Label("Skylights_RoofOverlay".Translate());
            list.Gap(2f);
            if (list.RadioButton("Skylights_RoofOverlay_LitAware".Translate(), Settings.roofOverlayMode == RoofOverlayMode.LitAware))
                Settings.roofOverlayMode = RoofOverlayMode.LitAware;
            if (list.RadioButton("Skylights_RoofOverlay_AlwaysOn".Translate(), Settings.roofOverlayMode == RoofOverlayMode.AlwaysOn))
                Settings.roofOverlayMode = RoofOverlayMode.AlwaysOn;
            if (list.RadioButton("Skylights_RoofOverlay_Vanilla".Translate(), Settings.roofOverlayMode == RoofOverlayMode.Vanilla))
                Settings.roofOverlayMode = RoofOverlayMode.Vanilla;
            list.Gap(4f);
            list.Label("Skylights_RoofOverlayDesc".Translate());
            if (Settings.roofOverlayMode != RoofOverlayMode.Vanilla)
                Settings.roofOverlayDark = Mathf.Round(TuneSlider(list, "Overlay tint (lower=darker)", Settings.roofOverlayDark, 120f, 255f));

            list.GapLine(12f);

            list.CheckboxLabeled("Dev: show elevation (height debug tint)", ref Settings.showElevationDebug);

            list.CheckboxLabeled("Skylights_CustomRoofShadows".Translate(), ref Settings.customRoofShadows);
            list.Gap(4f);
            list.Label("Skylights_CustomRoofShadowsDesc".Translate());

            if (Settings.customRoofShadows)
            {
                list.Gap(4f);
                Settings.rsDepth = TuneSlider(list, "Line thickness", Settings.rsDepth, 0.02f, 1f);
                Settings.rsDark = Mathf.Round(TuneSlider(list, "Darkness (lower=darker)", Settings.rsDark, 60f, 255f));
                Settings.rsVertOffset = TuneSlider(list, "Vertical offset (up)", Settings.rsVertOffset, 0f, 1f);
            }

            list.GapLine(12f);

            list.CheckboxLabeled("Skylights_OffsetSprite".Translate(), ref Settings.offsetSkylightSprite);
            list.Gap(4f);
            list.Label("Skylights_OffsetSpriteDesc".Translate());
            if (Settings.offsetSkylightSprite)
                Settings.spriteOffZ = TuneSlider(list, "Sprite nudge (up)", Settings.spriteOffZ, -1f, 1f);

            list.End();
        }

        /// <summary>A labelled value slider for the live tuning controls.</summary>
        private static float TuneSlider(Listing_Standard list, string label, float value, float min, float max)
        {
            list.Label($"{label}: {value:0.00}");
            return list.Slider(value, min, max);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            DomeGlowRadius.Apply();
            SkylightSpriteOffset.Apply();
            CompSkylight.ForceGlowRefresh();
            RepaintAllMapLighting();
        }

        /// <summary>Rebuild every loaded map's lighting so a roof-edge mode change shows immediately.</summary>
        public static void RepaintAllMapLighting()
        {
            if (Current.Game?.Maps == null) return;
            foreach (Map map in Current.Game.Maps)
                map.mapDrawer?.WholeMapChanged(
                    (ulong)MapMeshFlagDefOf.Roofs | (ulong)MapMeshFlagDefOf.GroundGlow | (ulong)MapMeshFlagDefOf.Buildings);
        }
    }

    /// <summary>
    /// Pushes the configured dome light radius onto the dome skylight defs — at startup (StaticConstructorOnStartup)
    /// and again whenever the setting changes. Only the soft-dome skylights (plain dome and mountain dome) are
    /// affected; the paned and weak-glass skylights keep their own light behaviour.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DomeGlowRadius
    {
        // Skylight_DomeGlowNode drives the multi-cell variants' light, so it must track the same radius as the
        // 1x1 dome. The Wide/Quad variants have no glower of their own — including them just fixes their build
        // preview ring (specialDisplayRadius) to match.
        private static readonly string[] DomeDefNames =
            { "Skylight_Dome", "Skylight_MountainDome", "Skylight_DomeGlowNode", "Skylight_Dome_Wide", "Skylight_Dome_Quad" };

        static DomeGlowRadius()
        {
            Apply();
        }

        public static void Apply()
        {
            int r = SkylightsSettingsMod.Settings?.domeGlowRadius ?? SkylightsSettings.DefaultDomeGlowRadius;
            foreach (string name in DomeDefNames)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                CompProperties_Glower g = def.GetCompProperties<CompProperties_Glower>();
                if (g != null) g.glowRadius = r;
                def.specialDisplayRadius = r;
            }
        }
    }

    /// <summary>
    /// Applies the optional cosmetic sprite nudge to every skylight def (any ThingDef carrying
    /// <see cref="CompProperties_Skylight"/>) by writing <c>graphicData.drawOffset</c>. Runs at startup and
    /// whenever the setting changes; a mesh repaint (WriteSettings → RepaintAllMapLighting) makes it show at
    /// once. When the toggle is off the offset is cleared back to zero.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SkylightSpriteOffset
    {
        static SkylightSpriteOffset()
        {
            Apply();
        }

        public static void Apply()
        {
            bool on = SkylightsSettingsMod.OffsetSkylightSprite;
            float z = SkylightsSettingsMod.Settings?.spriteOffZ ?? 0.35f;
            Vector3 v = on ? new Vector3(0f, 0f, z) : Vector3.zero;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.graphicData == null) continue;
                if (def.GetCompProperties<CompProperties_Skylight>() == null) continue;
                def.graphicData.drawOffset = v;
            }
        }
    }
}
