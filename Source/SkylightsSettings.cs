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

        public override void ExposeData()
        {
            Scribe_Values.Look(ref domeGlowRadius, "domeGlowRadius", DefaultDomeGlowRadius);
            Scribe_Values.Look(ref roofEdgeMode, "roofEdgeMode", RoofEdgeMode.Vanilla);
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

            list.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            DomeGlowRadius.Apply();
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
