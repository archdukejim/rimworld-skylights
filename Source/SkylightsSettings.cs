using UnityEngine;
using RimWorld;
using Verse;

namespace Skylights
{
    /// <summary>Player-tunable settings for the mod. Currently just the dome skylight light radius.</summary>
    public class SkylightsSettings : ModSettings
    {
        public const int MinDomeGlowRadius = 1;
        public const int MaxDomeGlowRadius = 10;
        /// <summary>Default is the shipped dome radius (~3) plus one tile.</summary>
        public const int DefaultDomeGlowRadius = 4;

        public int domeGlowRadius = DefaultDomeGlowRadius;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref domeGlowRadius, "domeGlowRadius", DefaultDomeGlowRadius);
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
            list.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            DomeGlowRadius.Apply();
            CompSkylight.ForceGlowRefresh();
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
        private static readonly string[] DomeDefNames = { "Skylight_Dome", "Skylight_MountainDome" };

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
