using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Skylights
{
    /// <summary>
    /// How Skylights glass and "ReBuild: Doors and Corners" glass (RB_Glass) interoperate. Default keeps the
    /// two mods fully separate; the other modes attach conversion recipes to the electric smelter so a player
    /// can settle on one glass economy. Only meaningful when ReBuild is active (see <see cref="ReBuildGlassInterop.Available"/>).
    /// </summary>
    public enum GlassInteropMode
    {
        /// <summary>No cross-recipes — each mod uses its own glass.</summary>
        Separate = 0,
        /// <summary>Add a recipe to make ReBuild glass from structural glass ("use my recipes for RB_Glass").</summary>
        SkylightsToReBuild = 1,
        /// <summary>Add recipes to make structural &amp; tinted glass from ReBuild glass ("use RB_Glass recipes").</summary>
        ReBuildToSkylights = 2,
    }

    /// <summary>
    /// Attaches or detaches the ReBuild glass-conversion recipes on the electric smelter at runtime to match
    /// <see cref="SkylightsSettings.glassInterop"/>. The recipes themselves are defined in
    /// Skylights_ReBuildCompat.xml with <c>MayRequire="ReBuild.COTR.DoorsAndCorners"</c> and no
    /// <c>recipeUsers</c>, so without ReBuild they never load and this class no-ops (<see cref="Available"/> is
    /// false). Nothing here hard-depends on ReBuild.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ReBuildGlassInterop
    {
        private const string ToRBGlass     = "Skylights_Convert_StructuralToRBGlass"; // StructuralGlass -> RB_Glass
        private const string ToStructural  = "Skylights_Convert_RBGlassToStructural"; // RB_Glass -> StructuralGlass
        private const string ToTinted      = "Skylights_Convert_RBGlassToTinted";     // RB_Glass -> TintedGlass

        private static readonly AccessTools.FieldRef<ThingDef, List<RecipeDef>> AllRecipesCached =
            AccessTools.FieldRefAccess<ThingDef, List<RecipeDef>>("allRecipesCached");

        static ReBuildGlassInterop()
        {
            Apply();
        }

        /// <summary>True only when ReBuild is active: the MayRequire'd conversion recipes loaded. Used to gate
        /// both this pass and whether the setting is shown at all.</summary>
        public static bool Available => DefDatabase<RecipeDef>.GetNamedSilentFail(ToRBGlass) != null;

        /// <summary>Sync the smelter's recipe list to the current setting. Safe to call any time; no-ops without ReBuild.</summary>
        public static void Apply()
        {
            if (!Available) return;
            ThingDef smelter = DefDatabase<ThingDef>.GetNamedSilentFail("ElectricSmelter");
            if (smelter == null) return;

            GlassInteropMode mode = SkylightsSettingsMod.Settings?.glassInterop ?? GlassInteropMode.Separate;
            bool toReBuild = mode == GlassInteropMode.SkylightsToReBuild;
            bool toSkylights = mode == GlassInteropMode.ReBuildToSkylights;

            bool changed = SetRecipe(smelter, ToRBGlass, toReBuild);
            changed |= SetRecipe(smelter, ToStructural, toSkylights);
            changed |= SetRecipe(smelter, ToTinted, toSkylights);

            if (changed)
                AllRecipesCached(smelter) = null; // force ThingDef.AllRecipes to rebuild from recipeUsers

            if (Prefs.DevMode)
                Log.Message($"[Skylights] ReBuild glass interop: available=true, mode={mode}, "
                    + $"smelter conversion recipes = {AttachedList(smelter)}");
        }

        /// <summary>Dev-only: which conversion recipes are currently on the smelter (for verifying the attach).</summary>
        private static string AttachedList(ThingDef bench)
        {
            List<string> on = new List<string>();
            foreach (string n in new[] { ToRBGlass, ToStructural, ToTinted })
            {
                RecipeDef r = DefDatabase<RecipeDef>.GetNamedSilentFail(n);
                if (r?.recipeUsers != null && r.recipeUsers.Contains(bench)) on.Add(n);
            }
            return on.Count == 0 ? "(none)" : string.Join(", ", on);
        }

        /// <summary>Add or remove the smelter from a recipe's recipeUsers. Returns true if it changed anything.</summary>
        private static bool SetRecipe(ThingDef bench, string recipeName, bool enable)
        {
            RecipeDef r = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeName);
            if (r == null) return false;
            if (r.recipeUsers == null) r.recipeUsers = new List<ThingDef>();

            bool has = r.recipeUsers.Contains(bench);
            if (enable && !has) { r.recipeUsers.Add(bench); return true; }
            if (!enable && has) { r.recipeUsers.Remove(bench); return true; }
            return false;
        }
    }
}
