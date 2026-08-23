# Developer's Guide

Interop reference for **Skylights v2.2** (packageId `archdukejim.Skylights`, RimWorld 1.6). Everything on this page is a surface another mod can reference, patch, or order around.

**Quick facts**

| | |
|---|---|
| Assembly | `Skylights.dll` (namespace `Skylights`) |
| Harmony instance ID | `archdukejim.skylights` |
| Hard dependency | `brrainz.harmony` (Harmony) |
| Soft dependency | `ReBuild.COTR.DoorsAndCorners` (glass interop, XML-only via `MayRequire`) |
| Patch application | `[StaticConstructorOnStartup]` static class `Skylights.SkylightsMod` calls `PatchAll()` |

To soft-reference any def on this page from your own mod, gate it with the packageId:

```xml
<li MayRequire="archdukejim.Skylights">Skylight_Paned</li>
```

---

## Contents

1. [Core concept: the two grids](#1-core-concept-the-two-grids)
2. [C# API](#2-c-api)
3. [`CompProperties_Skylight` (XML-configurable comp)](#3-compproperties_skylight-xml-configurable-comp)
4. [Harmony patches (ordering reference)](#4-harmony-patches-ordering-reference)
5. [The InSunlight / gene-aware sunlight mechanism](#5-the-insunlight--gene-aware-sunlight-mechanism)
6. [Defs](#6-defs)
7. [ReBuild: Doors and Corners glass interop](#7-rebuild-doors-and-corners-glass-interop)
8. [Mod settings](#8-mod-settings)

---

## 1. Core concept: the two grids

Skylights keeps two static per-map cell sets. All of the mod's Harmony patches are just consumers of these sets, so they are the primary interop point: **register a cell in these grids and the game lights it (and, optionally, counts it as sunlit) as if it had no roof — while the real roof stays in place for weather and temperature.**

| Grid | Registered by | Consumed by | Effect |
|---|---|---|---|
| `SkylightGrid` | every pane with `renderAsSky` | indoor-mask prefix, `GroundGlowAt` postfix, lighting-overlay transpiler/prefix | cell is lit and rendered as open sky (brightness, colour, moonlight, shadows, plant growth); rain/snow/fog overlays stay hidden |
| `SunlightGrid` | only panes with `transmitsSun` (clear glass) | `SanguophageUtility.InSunlight` postfix | cell counts as real sunlight for Biotech genes/stats/thoughts |

Membership is maintained live by `CompSkylight` (registered on spawn/roof-change via `CompTickRare`, deregistered on despawn). Nothing is saved — the grids rebuild themselves from spawned comps after load.

---

## 2. C# API

All types are `public` in namespace `Skylights`. Reference `Skylights.dll` (or use `AccessTools` if you want to stay soft-linked).

### `SkylightGrid.Set`

```csharp
public static void SkylightGrid.Set(Map map, IntVec3 c, bool on)
```

| Parameter | Type | Description |
|---|---|---|
| `map` | `Verse.Map` | Map the cell is on. Null-safe (no-op). |
| `c` | `Verse.IntVec3` | Cell to (de)register as sky-channeling. |
| `on` | `bool` | `true` = add, `false` = remove. |

**Returns:** `void`. On an actual state change it dirties the map mesh (`Buildings | Roofs` flags) and the glow grid for that cell, so the visual change appears immediately.

**Example** — your own "glass roof" building lights its cell as open sky:

```csharp
public override void SpawnSetup(Map map, bool respawningAfterLoad)
{
    base.SpawnSetup(map, respawningAfterLoad);
    Skylights.SkylightGrid.Set(map, Position, true);
}

public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
{
    Map map = Map;
    base.DeSpawn(mode);
    Skylights.SkylightGrid.Set(map, Position, false);
}
```

> Registration is not persisted; re-register on spawn after load (as above), exactly like `CompSkylight` does. Prefer attaching `CompProperties_Skylight` (section 3) over calling this directly — the comp already handles roof checks, thick-rock blocking, and load.

### `SkylightGrid.Contains`

```csharp
public static bool SkylightGrid.Contains(Map map, IntVec3 c)
```

| Parameter | Type | Description |
|---|---|---|
| `map` | `Verse.Map` | Map to query. Null-safe (returns `false`). |
| `c` | `Verse.IntVec3` | Cell to test. |

**Returns:** `bool` — `true` if some skylight is currently channeling open sky into this cell.

**Example** — a mood/room mod checking whether a roofed cell is effectively daylit:

```csharp
bool daylit = !cell.Roofed(map) || Skylights.SkylightGrid.Contains(map, cell);
```

### `SunlightGrid.Set` / `SunlightGrid.Contains`

```csharp
public static void SunlightGrid.Set(Map map, IntVec3 c, bool on)
public static bool SunlightGrid.Contains(Map map, IntVec3 c)
```

Same signatures and semantics as `SkylightGrid`, but for **gene-visible sunlight** (see section 5). `Set` performs no mesh/glow dirtying — `InSunlight` is queried live each call, so membership alone is enough. Register a cell here *in addition to* `SkylightGrid` if your glass passes true sun; leave it out for UV-filtered glass.

**Example** — count a cell as sunlit for Biotech without any visual change:

```csharp
Skylights.SunlightGrid.Set(map, cell, true);
// later: SanguophageUtility.InSunlight(cell, map) now returns true while CurSkyGlow > 0.1
```

### `SkylightLightHelper.RoofAtForLight` / `RoofedForLight`

```csharp
public static RoofDef SkylightLightHelper.RoofAtForLight(RoofGrid grid, int index)
public static bool    SkylightLightHelper.RoofedForLight(RoofGrid grid, int index)
```

| Parameter | Type | Description |
|---|---|---|
| `grid` | `Verse.RoofGrid` | The map's roof grid (its private `map` field is read via `FieldRef`). |
| `index` | `int` | Cell index (`map.cellIndices` order). |

**Returns:** the vanilla `RoofAt(int)` / `Roofed(int)` result, **except** a `SkylightGrid` cell reads as `null` / `false` (open sky). These are the redirect targets of the lighting-overlay transpiler; they are public so another lighting mod can call the same "roof as the lighting overlay sees it" lookup and agree with Skylights about which cells are open.

**Example:**

```csharp
RoofDef roofForLighting = Skylights.SkylightLightHelper.RoofAtForLight(map.roofGrid, map.cellIndices.CellToIndex(c));
// null here means "light this like outdoors", even though map.roofGrid.RoofAt(c) may be non-null
```

### `CompSkylight`

```csharp
public class CompSkylight : ThingComp
```

The driver comp (see section 3 for its properties). Public members:

```csharp
public CompProperties_Skylight Props { get; }   // typed accessor for the comp's props
public static void ForceGlowRefresh()           // re-applies glow on every spawned glower-driven skylight
public override string CompInspectStringExtra() // "channeling N%" / blocked / dark status line
```

`ForceGlowRefresh()` takes no parameters and returns `void`. Call it after mutating a dome def's `CompProperties_Glower.glowRadius` at runtime so already-spawned domes re-register their glowers with the new radius (the mod's own settings window does exactly this).

**Example:**

```csharp
def.GetCompProperties<CompProperties_Glower>().glowRadius = 6f;
Skylights.CompSkylight.ForceGlowRefresh();
```

### `SkylightSupport.HasSupportWithin`

```csharp
public static bool SkylightSupport.HasSupportWithin(Map map, IntVec3 center, float radius)
```

| Parameter | Type | Description |
|---|---|---|
| `map` | `Verse.Map` | Map to scan. Null-safe (returns `false`). |
| `center` | `Verse.IntVec3` | Cell to scan around. |
| `radius` | `float` | Radial distance (uses `GenRadial`). |

**Returns:** `bool` — `true` if any edifice with `def.holdsRoof` (wall, pillar, natural rock) sits within `radius` of `center`.

### `SkylightSupport.SupportRadiusOf`

```csharp
public static float SkylightSupport.SupportRadiusOf(BuildableDef def)
```

**Returns:** the `supportRadius` declared by the def's `CompProperties_Skylight` when `requiresNearbySupport` is true; otherwise `3f`.

### `PlaceWorker_NearRoofSupport`

```csharp
public class PlaceWorker_NearRoofSupport : PlaceWorker
```

Blocks placement unless `HasSupportWithin` passes, and draws the support radius ring as a placement ghost. Reusable on your own defs:

```xml
<placeWorkers>
  <li>Skylights.PlaceWorker_NearRoofSupport</li>
</placeWorkers>
```

Rejection report uses the translation key `Skylight_NeedsSupport` (one `{0}` = radius). Pair it with `requiresNearbySupport` on the comp if you also want the built thing to cave in when support is removed — `CompSkylight.CompTickRare` calls `RoofCollapserImmediate.DropRoofInCells` on its own tile and destroys the parent (`DestroyMode.KillFinalize`).

### `PlaceWorker_ShowFootprint`

```csharp
public class PlaceWorker_ShowFootprint : PlaceWorker
```

Placement-time overlay for buildings whose installed graphic hides their true footprint (the 2x1/2x2 domes draw a centred 1x1 sprite). While the build designator is active it draws: the occupied tiles' field edges in the ghost's valid/blocked colour, vanilla-style selection brackets on the footprint corners, and — when a light radius can be resolved — one smooth `GenDraw.DrawCircleOutline` circle around the **true footprint centre** (a half-tile-offset point on even footprints that vanilla's cell-centred `specialDisplayRadius` ring cannot express). The circle radius = resolved glow radius + half the footprint extent.

Light radius resolution order: the `CompProperties_Glower.glowRadius` of the def named by `CompProperties_Skylight.glowNodeDef`, else the def's own `CompProperties_Glower.glowRadius`, else no circle. Reusable on your own defs:

```xml
<placeWorkers>
  <li>Skylights.PlaceWorker_ShowFootprint</li>
</placeWorkers>
```

Defs using it should declare **no** `specialDisplayRadius` (the off-centre vanilla ring would draw on top).

### `CompSkylight.SpawnedSkylights`

```csharp
public static readonly List<CompSkylight> CompSkylight.SpawnedSkylights
```

Every currently-spawned skylight comp, across all maps (registered in `PostSpawnSetup`, removed in `PostDeSpawn`). Read-only iteration is safe from the main thread; filter by `comp.parent.Map`. Used by the visibility toggle to dirty only skylight-holding map-mesh sections.

### `SkylightVisibilityButton`

```csharp
[StaticConstructorOnStartup]
public static class SkylightVisibilityButton
{
    public static readonly Texture2D ToggleIcon;   // the play-settings row button art
    public static void DirtySkylightSections();    // regen the Things map-mesh layer under every spawned skylight
}
```

`DirtySkylightSections()` marks the map-mesh section under each spawned skylight dirty with the **Things** flag (`MapMeshDirty(pos, Things, regenAdjacentCells: true, regenAdjacentSections: true)`), so a sprite show/hide applies the moment those sections redraw. Call it after changing anything that alters whether `Thing.Print` emits a skylight's sprite. Note the flag: building sprites are printed by `SectionLayer_ThingsGeneral` (`relevantChangeTypes = Things`) — dirtying `Buildings` regenerates the wrong layers.

### Settings types

See section 8 for `RoofEdgeMode`, `SkylightsSettings`, `SkylightsSettingsMod`, and `DomeGlowRadius`.

---

## 3. `CompProperties_Skylight` (XML-configurable comp)

```csharp
public class CompProperties_Skylight : CompProperties   // compClass = CompSkylight
```

Attach to any `ThingDef` to make it a skylight. Two mutually exclusive operating modes:

- **Glower mode** (default): the comp drives a sibling `CompProperties_Glower` (or hidden per-cell glow nodes), scaling its `glowColor` with the real sky glow each rare tick. The glower's XML `glowColor` is treated as the *full-daylight* colour.
- **Sky mode** (`renderAsSky = true`): no glower; the comp registers its cell in `SkylightGrid` (and optionally `SunlightGrid`) so the Harmony patches light it as literal open sky.

The parent def needs `<tickerType>Rare</tickerType>` for the comp to update.

| Field | Type | Default | Meaning |
|---|---|---|---|
| `minChannelGlow` | `float` | `0.06` | Sky glow below this counts as full dark (avoids glow-grid churn at night). |
| `glowSteps` | `int` | `12` | Brightness steps between dark and full sun. Higher = smoother, more glow-grid recomputes. |
| `worksUnderThickRoof` | `bool` | `false` | Channels daylight even under thick overhead mountain (the light tunnel). |
| `glowFactor` | `float` | `1` | Fraction of outdoor sky glow passed indoors (domes use `0.5`). Glower mode only. |
| `glowNodeDef` | `string` | `null` | DefName of a hidden glow-emitter `ThingDef` (must carry `CompProperties_Glower`, should be `isSaveable=false`). One node spawns per footprint cell so a multi-cell dome's light is centred. |
| `glowNodeStrength` | `float` | `1` | Per-node brightness fraction when `glowNodeDef` is set (nodes overlap; 2x1/2x2 domes use `0.5`). |
| `renderAsSky` | `bool` | `false` | Sky mode: register in `SkylightGrid`; cell is lit/rendered as unroofed. |
| `transmitsSun` | `bool` | `false` | Sky mode add-on: also register in `SunlightGrid` so the cell counts as true sun for Biotech (clear panes yes, tinted no). |
| `requiresNearbySupport` | `bool` | `false` | Needs a `holdsRoof` edifice within `supportRadius`; loses it and the tile's roof is dropped, destroying the parent. |
| `supportRadius` | `float` | `3` | Radius for the support rule (and for `PlaceWorker_NearRoofSupport`). |

Channeling condition (all modes): the cell must have a roof (`RoofAt != null`) that is not thick — open sky channels nothing (it already lights the cell), thick rock blocks unless `worksUnderThickRoof`.

**Example** — a full-sky, sun-transmitting glass roof tile in your own mod:

```xml
<ThingDef ParentName="BuildingBase">
  <defName>MyMod_GlassRoofTile</defName>
  <thingClass>Building</thingClass>
  <tickerType>Rare</tickerType>          <!-- required: CompSkylight updates in CompTickRare -->
  <altitudeLayer>MoteOverhead</altitudeLayer>
  <passability>Standable</passability>
  <comps>
    <li Class="Skylights.CompProperties_Skylight">
      <renderAsSky>true</renderAsSky>
      <transmitsSun>true</transmitsSun>
    </li>
  </comps>
</ThingDef>
```

**Example** — a soft glow-dome variant (glower mode):

```xml
<comps>
  <li Class="CompProperties_Glower">
    <overlightRadius>0</overlightRadius>   <!-- caps ground glow at 0.5: never grows crops -->
    <glowRadius>2.6</glowRadius>
    <glowColor>(120, 118, 106, 0)</glowColor>  <!-- this is the FULL-daylight colour -->
  </li>
  <li Class="Skylights.CompProperties_Skylight">
    <glowFactor>0.5</glowFactor>
  </li>
</comps>
```

---

## 4. Harmony patches (ordering reference)

All patches live in Harmony instance **`archdukejim.skylights`**, applied at static-constructor time via `PatchAll()`. Use that ID with `[HarmonyBefore("archdukejim.skylights")]` / `[HarmonyAfter("archdukejim.skylights")]` to order around them.

| # | Target method | Patch type | Patch class | What it changes |
|---|---|---|---|---|
| 1 | `RimWorld.SanguophageUtility.InSunlight(IntVec3 cell, Map map)` | **Postfix** | `Skylights.Patch_InSunlight` | If vanilla returned `false` and the cell is in `SunlightGrid`, sets `__result = map.skyManager.CurSkyGlow > 0.1f`. Never turns a `true` into `false`; never fires for tinted panes. |
| 2 | `Verse.SectionLayer_IndoorMask.GenerateSectionLayer(...)` | **Prefix, returns `false`** (full replacement) | `Skylights.Patch_IndoorMask_GenerateSectionLayer` | Reimplements indoor-mask baking. `SkylightGrid` cells go to the *roofed-outdoor* mesh (rain/snow/fog overlays hidden, cell still open to exterior sky lighting). Non-skylight cells reproduce vanilla behaviour exactly. |
| 3 | `Verse.GlowGrid.GroundGlowAt(IntVec3 c, bool ignoreSky)` | **Postfix** | `Skylights.Patch_GlowGrid_GroundGlowAt` | For `SkylightGrid` cells (and `ignoreSky == false`), raises `__result` to `CurSkyGlow` if that is higher — so plants grow and the cell counts as lit, matching but never exceeding the sky. |
| 4 | `Verse.SectionLayer_LightingOverlay.GenerateLightingOverlay(...)` (non-public) | **Transpiler** | `Skylights.Patch_LightingOverlay_Generate` | Rewrites every call to `RoofGrid.RoofAt(int)` → `SkylightLightHelper.RoofAtForLight` and `RoofGrid.Roofed(int)` → `SkylightLightHelper.RoofedForLight`, so the overlay's roofed-darkness clamp treats skylight cells as open sky. Lighting only — roof, weather, temperature untouched. |
| 5 | `Verse.SectionLayer_LightingOverlay.GenerateLightingOverlay(...)` (same method) | **Prefix**, conditional | `Skylights.Patch_LightingOverlay_Inward` | Only when the `roofEdgeMode` setting ≠ `Vanilla`: returns `false` and runs a faithful, direction-aware reimplementation that erodes the roof's soft shadow edge *inward* instead of outward. In `Vanilla` mode it returns `true` immediately and the (transpiled) original runs. |
| 6 | `Verse.Thing.Print(SectionLayer)` | **Prefix**, conditional skip | `Skylights.Patch_Thing_Print_HideSkylight` | Only when the `hideSkylights` setting is on: returns `false` for things carrying `CompSkylight`, so their sprite is omitted from the Things map-mesh. Off = a single bool read, then vanilla. |
| 7 | `RimWorld.PlaySettings.DoPlaySettingsGlobalControls(WidgetRow, bool)` | **Postfix** | `Skylights.Patch_PlaySettings_SkylightVisibility` | Adds the "show installed skylights" toggle to the play-settings row (map view only; removable via the `skylightVisibilityButton` setting). Flipping it writes `hideSkylights`, persists settings, and calls `SkylightVisibilityButton.DirtySkylightSections()`. |

**Interaction notes for other modders**

- Patches 4 and 5 target the same method. When `roofEdgeMode` is `Vanilla`, the transpiled original executes; otherwise the prefix's reimplementation runs (which itself routes roof lookups through `SkylightLightHelper`, so skylight cells behave identically in both paths).
- Patch 2 is a skipping prefix: a transpiler you write against `SectionLayer_IndoorMask.GenerateSectionLayer` will not execute while Skylights is active. Prefer a `[HarmonyBefore("archdukejim.skylights")]` prefix, or patch the public helper surfaces instead.
- Patches 1 and 3 are pure additive postfixes — safe to stack; order rarely matters unless you also rewrite `__result`.

**Example** — running your own `InSunlight` postfix after Skylights so you see its final result:

```csharp
[HarmonyPatch(typeof(SanguophageUtility), nameof(SanguophageUtility.InSunlight))]
[HarmonyAfter("archdukejim.skylights")]
static class MyPatch
{
    static void Postfix(ref bool __result, IntVec3 cell, Map map)
    {
        // __result here already includes skylight-transmitted sun
    }
}
```

---

## 5. The InSunlight / gene-aware sunlight mechanism

Every Biotech sun-gene, "in sunlight" stat condition, and the in-sunlight mood thought funnels through one vanilla method:

```csharp
public static bool SanguophageUtility.InSunlight(IntVec3 cell, Map map)   // vanilla: false for any roofed cell
```

Skylights postfixes it (patch 1). The full chain:

1. A pane def sets `transmitsSun = true` (clear glass: `Skylight_Paned`, `Skylight_Basic`).
2. `CompSkylight.UpdateSkyChannel()` registers the cell in **both** `SkylightGrid` (lighting) and `SunlightGrid` (genes) whenever the roof above is channeling; deregisters when the roof opens, turns thick, or the pane despawns.
3. `Patch_InSunlight` flips a vanilla-`false` result to `true` for `SunlightGrid` cells, but only while `map.skyManager.CurSkyGlow > 0.1f` — so night, eclipses, and heavy storms still read as no sun, exactly like standing outdoors.

Consequences: sun-loving xenotypes gain their outdoor benefit under a clear pane; sun-*sensitive* pawns (e.g. sanguophages) are burned under one. The tinted pane (`Skylight_Tinted`) deliberately leaves `transmitsSun` false — identical lighting and crop growth, zero gene-visible sun. Your mod can opt any cell into the same behaviour with `SunlightGrid.Set(map, cell, true)` — no Harmony required on your side.

---

## 6. Defs

### 6.1 DesignationCategoryDef

| defName | label | order | Notes |
|---|---|---|---|
| `Skylights` | skylights | 480 | Dedicated Architect tab (just below Furniture). Special designators: `Designator_Cancel`, `Designator_Deconstruct`. |

**Example** — put your own building on the Skylights tab when the mod is present:

```xml
<designationCategory MayRequire="archdukejim.Skylights">Skylights</designationCategory>
```

### 6.2 Building ThingDefs

All buildable skylights inherit the internal abstract parent `SkylightBase` (in `Defs/ThingDefs/Skylights_Buildings.xml`): `tickerType Rare`, `altitudeLayer MoteOverhead`, `passability Standable`, `isEdifice false`, **`selectable false`** (clicks pass through; removal is via the Deconstruct area tool), `designationCategory Skylights`. You can use `ParentName="SkylightBase"` for your own add-on skylight if you load after Skylights.

| defName | Label | Size | Kind | Key comp settings | Cost | Research |
|---|---|---|---|---|---|---|
| `Skylight_Dome` | dome skylight | 1x1 | glower dome | `glowFactor 0.5`; `CompProperties_Glower` radius 2.6, `overlightRadius 0` (never grows crops) | 1 `SkylightDome` | ComplexFurniture |
| `Skylight_Dome_Wide` | dome skylight (2x1) | 1x2 | node-pooled dome | `glowNodeDef Skylight_DomeGlowNode`, `glowNodeStrength 0.5`, `glowFactor 0.5`; `PlaceWorker_ShowFootprint`; no `specialDisplayRadius` | 1 `SkylightDome` | ComplexFurniture |
| `Skylight_Dome_Quad` | dome skylight (2x2) | 2x2 | node-pooled dome | same as Wide | 1 `SkylightDome` | ComplexFurniture |
| `Skylight_Paned` | industrial skylight | 1x1 | sky pane | `renderAsSky`, `transmitsSun` | 4 `StructuralFrame` + 1 `StructuralGlass` | Electricity + ComplexFurniture |
| `Skylight_Tinted` | tinted skylight | 1x1 | sky pane (UV-filtered) | `renderAsSky` only — no `transmitsSun` | 4 `StructuralFrame` + 1 `TintedGlass` | Electricity + ComplexFurniture |
| `Skylight_MountainDome` | light tunnel | 1x1 | glower dome | `worksUnderThickRoof`, `glowFactor 0.5` | 1 `SkylightDome` + 1 `ReflectionTube` | Electricity + ComplexFurniture |
| `Skylight_Basic` | basic skylight | 1x1 | sky pane (weak glass) | `renderAsSky`, `transmitsSun`, `requiresNearbySupport`, `supportRadius 3`; `PlaceWorker_NearRoofSupport`; `leaveResourcesWhenKilled false` | 4 `WoodLog` + 1 `BasicPane` | ComplexFurniture |
| `Skylight_DomeGlowNode` | skylight glow | 1x1 | hidden glow emitter | not buildable, `isSaveable false`, `drawerType None`, `tickerType Never`; spawned one-per-footprint-cell by multi-cell domes | — | — |

Legacy naming (save compat with v1): `Skylight_Paned` is the *industrial* skylight; `Skylight_MountainDome` is the *light tunnel*.

**Example** — Harmony-free XML patch making the industrial pane cheaper when your mod is loaded:

```xml
<Operation Class="PatchOperationConditional">
  <xpath>Defs/ThingDef[defName="Skylight_Paned"]</xpath>
  <match Class="PatchOperationReplace">
    <xpath>Defs/ThingDef[defName="Skylight_Paned"]/costList</xpath>
    <value>
      <costList>
        <StructuralFrame>2</StructuralFrame>
        <StructuralGlass>1</StructuralGlass>
      </costList>
    </value>
  </match>
</Operation>
```

### 6.3 Item (resource) ThingDefs

All are `ParentName="ResourceBase"`, `thingCategories Manufactured`, `DeteriorationRate 0` except the two prefab units.

| defName | Label | Market value | Stack | Notes |
|---|---|---|---|---|
| `StructuralFrame` | structural frame | 2.5 | 75 | Smelter-made mounting frame. |
| `StructuralGlass` | structural glass | 3.0 | 75 | Clear cast pane — the ReBuild interop currency. |
| `TintedGlass` | tinted structural glass | 4.5 | 75 | UV-filtering pane (excluded from ReBuild interop). |
| `BasicPane` | basic pane | 2.0 | 75 | Crude hand-worked pane, crafting-spot tier. |
| `SkylightDome` | skylight dome | 18 | 25 | Prefab dome unit (`DeteriorationRate 1.0`). |
| `ReflectionTube` | reflection tube | 32 | 25 | Mirror-lined tube for the light tunnel (`DeteriorationRate 1.0`). |

**Example** — accept structural glass in your own recipe's ingredient filter:

```xml
<li MayRequire="archdukejim.Skylights">StructuralGlass</li>
```

### 6.4 RecipeDefs

Two abstract bases you can parent your own recipes to (load after Skylights):

- `SkylightSmelterRecipeBase` — `recipeUsers: ElectricSmelter`, `workSpeedStat SmeltingSpeed`, `researchPrerequisite Electricity`, `workSkill Crafting`.
- `SkylightCraftSpotRecipeBase` — `recipeUsers: CraftingSpot`, `workSpeedStat GeneralLaborSpeed`, `workSkill Crafting`, no research.

| defName | Station | In → Out | Work | Extra research |
|---|---|---|---|---|
| `Make_BasicPane` | crafting spot | 1 Steel → 1 `BasicPane` | 210 | ComplexFurniture |
| `Make_BasicPane_Bulk` | crafting spot | 10 Steel → 10 `BasicPane` | 1875 | ComplexFurniture |
| `Make_SkylightDome_Hand` | crafting spot | 2 Steel → 1 `SkylightDome` | 1800 | — |
| `Make_StructuralFrame` | smelter | 1 Steel → 4 `StructuralFrame` | 210 | — |
| `Make_StructuralFrame_Bulk` | smelter | 10 Steel → 40 `StructuralFrame` | 1875 | — |
| `Make_StructuralGlass` | smelter | 1 Steel → 1 `StructuralGlass` | 210 | — |
| `Make_StructuralGlass_Bulk` | smelter | 10 Steel → 10 `StructuralGlass` | 1875 | — |
| `Make_TintedGlass` | smelter | 1 Steel → 1 `TintedGlass` | 260 | ComplexFurniture |
| `Make_TintedGlass_Bulk` | smelter | 10 Steel → 10 `TintedGlass` | 2300 | ComplexFurniture |
| `Make_SkylightDome` | smelter | 2 Steel → 1 `SkylightDome` | 1000 | — |
| `Make_ReflectionTube` | smelter | 5 Steel → 1 `ReflectionTube` | 1250 | ComplexFurniture |

**Example** — add a Skylights recipe to your own workbench:

```xml
<Operation Class="PatchOperationConditional">
  <xpath>Defs/RecipeDef[defName="Make_StructuralGlass"]</xpath>
  <match Class="PatchOperationAdd">
    <xpath>Defs/RecipeDef[defName="Make_StructuralGlass"]/recipeUsers</xpath>
    <value><li>MyMod_GlassFurnace</li></value>
  </match>
</Operation>
```

---

## 7. ReBuild: Doors and Corners glass interop

File: `Defs/RecipeDefs/Skylights_ReBuildCompat.xml`. **Pure XML, no C#, no mod setting, no hard dependency** — every RecipeDef carries `MayRequire="ReBuild.COTR.DoorsAndCorners"`, so when ReBuild is not active the defs are discarded during XML loading (before cross-reference resolution) and `RB_Glass` is never referenced.

Rationale: both mods model glass as a fixed costList *ingredient*, not a stuff, so the glasses can't substitute for each other in build costs. Instead the interop is always-on **bidirectional 1:1 conversion at a crafting spot** — no research, no equipment, and a flat `workAmount` of **60 regardless of batch size** (a x50 batch costs the same time as a x1; the batches exist purely to cut job overhead).

Shared abstract base: `Skylights_GlassConvertBase` (crafting spot, `GeneralLaborSpeed`, Crafting skill, `workAmount 60`).

| Direction | defNames | Batch sizes |
|---|---|---|
| `RB_Glass` → `StructuralGlass` | `Skylights_ToStructural_1x`, `_5x`, `_10x`, `_50x` | 1 / 5 / 10 / 50 |
| `StructuralGlass` → `RB_Glass` | `Skylights_ToRBGlass_1x`, `_5x`, `_10x`, `_50x` | 1 / 5 / 10 / 50 |

`TintedGlass` is intentionally excluded — the UV tint is a manufacturing step, not a trivial reshaping.

**Example** — bridging your own glass item the same way (works even if neither Skylights nor your target is a hard dep):

```xml
<RecipeDef ParentName="Skylights_GlassConvertBase"
           MayRequire="archdukejim.Skylights,my.glass.mod">
  <defName>MyMod_ToStructural_1x</defName>
  <label>rework my glass into structural glass</label>
  <jobString>Reworking glass.</jobString>
  <ingredients><li><filter><thingDefs><li>MyGlass</li></thingDefs></filter><count>1</count></li></ingredients>
  <fixedIngredientFilter><thingDefs><li>MyGlass</li></thingDefs></fixedIngredientFilter>
  <products><StructuralGlass>1</StructuralGlass></products>
</RecipeDef>
```

---

## 8. Mod settings

### `RoofEdgeMode` (enum)

```csharp
public enum RoofEdgeMode { Vanilla = 0, Full = 1, SkylightsOnly = 2 }
```

How the lighting overlay shades the soft edge where roof meets open sky. `Vanilla` = untouched (shadow bleeds half a tile outward onto lit tiles); `Full` = inward soft edge at *every* roof edge on the map; `SkylightsOnly` = inward edge only around the mod's skylight tiles. Non-Vanilla modes activate patch 5.

### `SkylightsSettings : ModSettings`

```csharp
public class SkylightsSettings : ModSettings
{
    public const int MinDomeGlowRadius = 1;
    public const int MaxDomeGlowRadius = 10;
    public const int DefaultDomeGlowRadius = 4;

    public int domeGlowRadius = DefaultDomeGlowRadius;     // 1–10 slider
    public RoofEdgeMode roofEdgeMode = RoofEdgeMode.Vanilla;
    public bool hideSkylights = false;                     // hide installed skylight sprites (v2.2)
    public bool skylightVisibilityButton = true;           // show the play-settings HUD button (v2.2)

    public override void ExposeData()
    {
        Scribe_Values.Look(ref domeGlowRadius, "domeGlowRadius", DefaultDomeGlowRadius);
        Scribe_Values.Look(ref roofEdgeMode, "roofEdgeMode", RoofEdgeMode.Vanilla);
        Scribe_Values.Look(ref hideSkylights, "hideSkylights", false);
        Scribe_Values.Look(ref skylightVisibilityButton, "skylightVisibilityButton", true);
        base.ExposeData();
    }
}
```

Scribed with `Scribe_Values.Look` under keys `domeGlowRadius`, `roofEdgeMode`, `hideSkylights`, and `skylightVisibilityButton` into RimWorld's standard per-mod config XML (`Config/Mod_..._SkylightsSettings.xml` in the save-data folder). All fields fall back to their defaults when absent, so the mod is safe to add mid-save.

### `SkylightsSettingsMod : Mod`

```csharp
public class SkylightsSettingsMod : Mod
{
    public static SkylightsSettings Settings;              // live settings instance
    public static RoofEdgeMode RoofEdge { get; }           // null-safe hot-path read, used by patch 5
    public static bool HideSkylights { get; }              // null-safe hot-path read, used by patch 6 (v2.2)
    public static void RepaintAllMapLighting()             // WholeMapChanged(Roofs|GroundGlow|Buildings) on every map
    public override void WriteSettings()                   // applies radius, ForceGlowRefresh, repaints lighting + skylight sections
}
```

**Example** — reading the live settings from another mod:

```csharp
int radius = Skylights.SkylightsSettingsMod.Settings?.domeGlowRadius ?? 4;
Skylights.RoofEdgeMode mode = Skylights.SkylightsSettingsMod.RoofEdge;
```

### `DomeGlowRadius` (settings applier)

```csharp
[StaticConstructorOnStartup]
public static class DomeGlowRadius
{
    public static void Apply();   // pushes domeGlowRadius onto the dome defs
}
```

`Apply()` (no parameters, returns `void`) writes `Settings.domeGlowRadius` into `CompProperties_Glower.glowRadius` and `specialDisplayRadius` of `Skylight_Dome`, `Skylight_MountainDome`, and `Skylight_DomeGlowNode`. The multi-cell `Skylight_Dome_Wide` / `Skylight_Dome_Quad` deliberately get **no** `specialDisplayRadius` — their placement circle is drawn footprint-centred by `PlaceWorker_ShowFootprint` from the node's live `glowRadius` (v2.2). Runs once at startup and again from `WriteSettings()`. **Heads-up:** if your mod patches a dome's glow radius, this applier overwrites it at startup and on any settings save — patch *after* startup or adjust the setting instead.

---

*Page generated from the `release/2.2.0` branch source of Skylights v2.2. File an issue at [archdukejim/rimworld-skylights](https://github.com/archdukejim/rimworld-skylights/issues) if a surface documented here changes.*
