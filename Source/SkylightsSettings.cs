using UnityEngine;
using RimWorld;
using Verse;

namespace Skylights
{
    /// <summary>Player-tunable settings for the mod.</summary>
    public class SkylightsSettings : ModSettings
    {
        public const int MinDomeGlowRadius = 1;
        public const int MaxDomeGlowRadius = 10;
        /// <summary>Default is the shipped dome radius (~3) plus one tile.</summary>
        public const int DefaultDomeGlowRadius = 4;

        public int domeGlowRadius = DefaultDomeGlowRadius;

        /// <summary>Draw the custom hard roof-edge shadow line (issue #18): a crisp architectural shadow
        /// along roof→open boundaries and skylight rims, replacing the old soft roof-glow edge. Default on;
        /// off reverts to vanilla roof shading.</summary>
        public bool customRoofShadows = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref domeGlowRadius, "domeGlowRadius", DefaultDomeGlowRadius);
            Scribe_Values.Look(ref customRoofShadows, "customRoofShadows", true);
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

            list.CheckboxLabeled("Skylights_CustomRoofShadows".Translate(), ref Settings.customRoofShadows);
            list.Gap(6f);
            list.Label("Skylights_CustomRoofShadowsDesc".Translate());

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
}
