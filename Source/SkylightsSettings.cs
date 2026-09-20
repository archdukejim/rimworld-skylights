using UnityEngine;
using RimWorld;
using Verse;

namespace Skylights
{
    /// <summary>
    /// How the lighting overlay shades the soft edge where a roof meets open sky. Vanilla lets the roof's
    /// darkening bleed *outward* onto the lit tiles just outside the roof; the inward modes instead keep the
    /// open tiles fully lit to their edge and push the soft falloff *into* the roofed tiles.
    /// </summary>
    public enum RoofEdgeMode
    {
        /// <summary>Leave RimWorld's roof-edge shading untouched (soft edge spreads outward).</summary>
        Vanilla = 0,
        /// <summary>Inward soft edge at every roof edge on the map.</summary>
        Full = 1,
        /// <summary>Inward soft edge only around the mod's own skylight tiles.</summary>
        SkylightsOnly = 2,
    }

    /// <summary>
    /// How installed skylights present themselves on the map — cycled by the HUD button (circle /
    /// check / X), the architect-tab button, and the mod-menu radios, all driving the same state.
    /// </summary>
    public enum SkylightDisplayMode
    {
        /// <summary>Sprites drawn AND the buildings are directly clickable (inspect pane, deconstruct).
        /// Clicks no longer pass through to whatever is beneath. HUD badge: circle.</summary>
        Selectable = 0,
        /// <summary>Sprites drawn, clicks pass through to what's below (the classic behaviour).
        /// HUD badge: check.</summary>
        Visible = 1,
        /// <summary>Sprites hidden (and, per hideDisablesTintGlow, the stained-glass light muted);
        /// skylights keep channeling light exactly the same. HUD badge: X.</summary>
        Hidden = 2,
    }

    /// <summary>Player-tunable settings for the mod.</summary>
    public class SkylightsSettings : ModSettings
    {
        public const int MinDomeGlowRadius = 1;
        public const int MaxDomeGlowRadius = 10;
        /// <summary>Default is the shipped dome radius (~3) plus one tile.</summary>
        public const int DefaultDomeGlowRadius = 4;

        public int domeGlowRadius = DefaultDomeGlowRadius;

        /// <summary>Roof-edge shading mode. Default keeps vanilla behaviour so nothing changes unless opted in.</summary>
        public RoofEdgeMode roofEdgeMode = RoofEdgeMode.Vanilla;

        /// <summary>Three-state display mode for installed skylights (see <see cref="SkylightDisplayMode"/>).
        /// Default Visible — the classic sprites-drawn, click-through behaviour.</summary>
        public SkylightDisplayMode displayMode = SkylightDisplayMode.Visible;

        /// <summary>Legacy two-state toggle, kept only to migrate old configs into
        /// <see cref="displayMode"/> on load. Never read at runtime.</summary>
        private bool hideSkylights = false;

        /// <summary>When true, hiding installed skylights also switches off the ship windows' coloured
        /// light — the stained-glass wash on the surface and the tinted starlight in orbit. Default OFF
        /// (v3.0.0 fast-follow): hiding strips only the sprites and the coloured light keeps shining,
        /// which is what hiding has always meant for skylight function; muting is the opt-in.</summary>
        public bool hideDisablesTintGlow = false;

        /// <summary>Master switch for the skylight visibility HUD button (issue #20): the play-settings-row
        /// toggle that shows/hides installed skylight sprites in play (and, per
        /// <see cref="hideDisablesTintGlow"/>, mutes the stained-glass light with them). Off removes the
        /// button (the mod-menu hide checkbox and the architect-tab button still work). Default on.</summary>
        public bool skylightVisibilityButton = true;

        public const float MinOpacity = 0.1f;

        /// <summary>How strongly installed skylight sprites are drawn, 0.1–1. Multiplies the sprite's own
        /// baked alpha via the graphic colour, so 1 shows the art as authored and 0.1 is barely-there glass.
        /// Adjustable from the Skylights architect tab's opacity button or the mod menu. Default full.</summary>
        public float skylightOpacity = 1f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref domeGlowRadius, "domeGlowRadius", DefaultDomeGlowRadius);
            Scribe_Values.Look(ref roofEdgeMode, "roofEdgeMode", RoofEdgeMode.Vanilla);
            Scribe_Values.Look(ref displayMode, "displayMode", SkylightDisplayMode.Visible);
            Scribe_Values.Look(ref hideSkylights, "hideSkylights", false);
            Scribe_Values.Look(ref hideDisablesTintGlow, "hideDisablesTintGlow", false);
            Scribe_Values.Look(ref skylightVisibilityButton, "skylightVisibilityButton", true);
            Scribe_Values.Look(ref skylightOpacity, "skylightOpacity", 1f);
            // Migrate a pre-three-state config: the old hide toggle becomes the Hidden mode.
            if (Scribe.mode == LoadSaveMode.LoadingVars && hideSkylights
                && displayMode == SkylightDisplayMode.Visible)
                displayMode = SkylightDisplayMode.Hidden;
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

        /// <summary>Fast, null-safe read of the active roof-edge mode for the lighting-overlay hot path.</summary>
        public static RoofEdgeMode RoofEdge => Settings?.roofEdgeMode ?? RoofEdgeMode.Vanilla;

        /// <summary>Fast, null-safe read of the display mode.</summary>
        public static SkylightDisplayMode DisplayMode => Settings?.displayMode ?? SkylightDisplayMode.Visible;

        /// <summary>Fast, null-safe read of "sprites hidden" for the Thing.Print hot path.</summary>
        public static bool HideSkylights => DisplayMode == SkylightDisplayMode.Hidden;

        /// <summary>Advance the display mode one step (Selectable -> Visible -> Hidden -> ...), persist it,
        /// and push every consequence live: sprite sections, coloured glow, and def selectability. The one
        /// entry point shared by the HUD button and the architect-tab button.</summary>
        public static void CycleDisplayMode()
        {
            if (Settings == null) return;
            Settings.displayMode = (SkylightDisplayMode)(((int)Settings.displayMode + 1) % 3);
            Settings.Write();
            ApplyDisplayMode();
        }

        /// <summary>Push the current display mode's consequences onto the live game.</summary>
        public static void ApplyDisplayMode()
        {
            SkylightSelectability.Apply();
            CompSkylight.DirtySkylightSections();
            CompSkylight.RefreshWindowGlow();
        }


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

            list.Label("Skylights_RoofEdgeMode".Translate());
            list.Gap(2f);
            if (list.RadioButton("Skylights_RoofEdge_Vanilla".Translate(),
                    Settings.roofEdgeMode == RoofEdgeMode.Vanilla))
                Settings.roofEdgeMode = RoofEdgeMode.Vanilla;
            if (list.RadioButton("Skylights_RoofEdge_SkylightsOnly".Translate(),
                    Settings.roofEdgeMode == RoofEdgeMode.SkylightsOnly))
                Settings.roofEdgeMode = RoofEdgeMode.SkylightsOnly;
            if (list.RadioButton("Skylights_RoofEdge_Full".Translate(),
                    Settings.roofEdgeMode == RoofEdgeMode.Full))
                Settings.roofEdgeMode = RoofEdgeMode.Full;
            list.Gap(6f);
            list.Label("Skylights_RoofEdgeModeDesc".Translate());

            list.GapLine(12f);

            list.Label("Skylights_DisplayMode".Translate());
            list.Gap(2f);
            if (list.RadioButton("Skylights_Display_Selectable".Translate(),
                    Settings.displayMode == SkylightDisplayMode.Selectable))
                Settings.displayMode = SkylightDisplayMode.Selectable;
            if (list.RadioButton("Skylights_Display_Visible".Translate(),
                    Settings.displayMode == SkylightDisplayMode.Visible))
                Settings.displayMode = SkylightDisplayMode.Visible;
            if (list.RadioButton("Skylights_Display_Hidden".Translate(),
                    Settings.displayMode == SkylightDisplayMode.Hidden))
                Settings.displayMode = SkylightDisplayMode.Hidden;

            list.Gap(6f);

            list.CheckboxLabeled("Skylights_HideTintGlow".Translate(), ref Settings.hideDisablesTintGlow,
                "Skylights_HideTintGlowDesc".Translate());

            list.Gap(6f);

            list.CheckboxLabeled("Skylights_VisibilityButton".Translate(), ref Settings.skylightVisibilityButton,
                "Skylights_VisibilityButtonDesc".Translate());

            list.Gap(6f);

            list.Label("Skylights_OpacitySetting".Translate(Mathf.RoundToInt(Settings.skylightOpacity * 100f)));
            Settings.skylightOpacity = list.Slider(Settings.skylightOpacity, SkylightsSettings.MinOpacity, 1f);
            list.Gap(2f);
            list.Label("Skylights_OpacitySettingDesc".Translate());

            list.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            DomeGlowRadius.Apply();
            SkylightOpacity.Apply();
            CompSkylight.ForceGlowRefresh();
            // Display mode: selectability, sprite sections, and the coloured glow all follow it.
            ApplyDisplayMode();
            RepaintAllMapLighting();
            // The whole-map repaint above regenerates lazily; hit the skylight sections directly so a
            // hide/show or opacity change is visible the moment the dialog closes.
            CompSkylight.DirtySkylightSections();
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
    /// Pushes the display mode's selectability onto every skylight building def — at startup and whenever
    /// the mode changes. Skylights ship with selectable=false (clicks pass through to what's beneath);
    /// the Selectable display mode flips the live defs so players can click a skylight directly (inspect
    /// pane, deconstruct). Selection reads def.selectable at click time, so the change is instant; things
    /// already selected when leaving the mode simply stay selected until deselected.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SkylightSelectability
    {
        static SkylightSelectability()
        {
            Apply();
        }

        public static void Apply()
        {
            bool selectable = SkylightsSettingsMod.DisplayMode == SkylightDisplayMode.Selectable;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.GetCompProperties<CompProperties_Skylight>() == null) continue;
                def.selectable = selectable;
            }
        }
    }

    /// <summary>
    /// Pushes the configured sprite opacity onto every skylight building def — at startup and whenever the
    /// setting changes. The opacity multiplies the graphic colour's alpha (the defs use the Transparent
    /// shader, whose material colour scales the texture's own baked alpha), so the art fades smoothly from
    /// as-authored down to barely-there glass. Cached graphics are rebuilt so a change shows immediately.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SkylightOpacity
    {
        private static readonly System.Reflection.FieldInfo CachedGraphicField =
            typeof(GraphicData).GetField("cachedGraphic",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        static SkylightOpacity()
        {
            Apply();
        }

        public static void Apply()
        {
            float a = Mathf.Clamp(SkylightsSettingsMod.Settings?.skylightOpacity ?? 1f,
                SkylightsSettings.MinOpacity, 1f);

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.GetCompProperties<CompProperties_Skylight>() == null || def.graphicData == null)
                    continue;
                Color c = def.graphicData.color;
                if (Mathf.Approximately(c.a, a)) continue;
                c.a = a;
                def.graphicData.color = c;
                // Drop the cached graphic so the next access rebuilds its material with the new colour.
                CachedGraphicField?.SetValue(def.graphicData, null);
                def.graphic = def.graphicData.Graphic;
            }

            // Spawned skylights cache a per-thing graphic; Notify_ColorChanged drops it and dirties their mesh.
            for (int i = 0; i < CompSkylight.SpawnedSkylights.Count; i++)
            {
                Thing t = CompSkylight.SpawnedSkylights[i].parent;
                if (t.Spawned) t.Notify_ColorChanged();
            }
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
        // 1x1 dome. The Wide/Quad variants have no glower of their own and get NO specialDisplayRadius:
        // vanilla pins that ring to the root cell, which reads off-centre on an even footprint, so
        // PlaceWorker_ShowFootprint draws them a footprint-centred light circle from the node's live
        // glowRadius instead.
        private static readonly string[] DomeDefNames =
            { "Skylight_Dome", "Skylight_MountainDome", "Skylight_DomeGlowNode" };

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
}
