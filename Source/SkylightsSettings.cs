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

        /// <summary>When true, installed skylight building sprites are hidden (the pane/dome graphic is not
        /// drawn); the skylight keeps functioning — it still channels light and renders its cell as sky.
        /// Default off (skylights visible).</summary>
        public bool hideSkylights = false;

        public const float MinOpacity = 0.1f;

        /// <summary>How strongly installed skylight sprites are drawn, 0.1–1. Multiplies the sprite's own
        /// baked alpha via the graphic colour, so 1 shows the art as authored and 0.1 is barely-there glass.
        /// Adjustable from the Skylights architect tab's opacity button or the mod menu. Default full.</summary>
        public float skylightOpacity = 1f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref domeGlowRadius, "domeGlowRadius", DefaultDomeGlowRadius);
            Scribe_Values.Look(ref roofEdgeMode, "roofEdgeMode", RoofEdgeMode.Vanilla);
            Scribe_Values.Look(ref hideSkylights, "hideSkylights", false);
            Scribe_Values.Look(ref skylightOpacity, "skylightOpacity", 1f);
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

        /// <summary>Fast, null-safe read of the hide-skylights toggle for the Thing.Print hot path.</summary>
        public static bool HideSkylights => Settings?.hideSkylights ?? false;


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

            list.CheckboxLabeled("Skylights_HideInstalled".Translate(), ref Settings.hideSkylights,
                "Skylights_HideInstalledDesc".Translate());

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
