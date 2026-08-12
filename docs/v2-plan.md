# Skylights v2 — Plan

## Status — all four features built & verified in-game (branch `v2-skylights`)

1. **Gene-aware clear pane** — `InSunlight` postfix bound; `SunlightGrid` holds only the clear
   pane's cell (tinted excluded), confirmed live via reflection.
2. **Tinted pane** — lights/grows identically (both panes admit full sky glow under a roof), but
   stays out of `SunlightGrid` so genes see no sun. Gated Electricity + Complex Furniture.
3. **Tribal weak-glass skylight** — collapses (single-tile roof drop + salvage) when no wall/pillar
   is within 3 tiles; PlaceWorker blocks install out of range (shared `HasSupportWithin` helper).
4. **Dome radius slider** — mod-settings slider (1–10, default 4 = +1 tile); dome lights to ~radius 4;
   closing settings live-refreshes spawned domes.

**Bonus fix:** `SkylightBase` had no `tickerType` (defaulted to `Never`), so `CompSkylight.CompTickRare`
never ran — the tribal collapse never fired *and* the dome's day/night glow tracking was silently broken
in v1. Added `<tickerType>Rare</tickerType>`. Perf with the mod active: ~1.1–1.4 ms/tick, no GC pressure.

Remaining before release: dedicated tinted-glass item art (reuses structural-glass texture for now);
update About.xml/README; optional Steam replies to Rain & ne_propheta (bridge was offline earlier).

---



Driven by Steam Workshop feedback (users **Rain** and **ne_propheta**). Two thrusts:
deeper mechanics (gene-aware sunlight) and more content (a tinted variant + a
low-tech tier).

## Summary of player asks

- **Rain** — Biotech sun-genes (sunlight sensitivity, and outdoor/sun-loving
  workshop xenotypes) should acknowledge a paned skylight as real sunlight.
  Confirmed by their testing that skylights currently block these even at 100%
  daylight.
- **ne_propheta** — needing **Electricity** to unlock is a pity for tribal /
  low-tech runs; something like **Smithing** would fit.

## Feature 1 — Clear paned skylight becomes gene-aware ("the Crux ask")

The existing structural-glass paned skylight becomes **fully symmetric** real sun:
sun-lovers gain their outdoor benefit indoors, and sun-sensitive pawns are exposed.

**Hook:** all Biotech sun-genes/stat-factors and the "in sunlight" thought funnel
through one vanilla method, `IntVec3.InSunlight(Map)`, which returns false for any
roofed cell. See memory `insunlight-gene-hook`.

- Harmony **postfix on `IntVec3.InSunlight`**: if `__result` is false and the cell
  is a clear (sun-transmitting) skylight cell, return `map.skyManager.CurSkyGlow > 0.1f`.
- Covers `ThoughtWorker_InSunlight` and `ConditionalStatAffecter_InSunlight`
  (the standard conditional custom xenotype sun-genes use) in one shot.
- Lighting / crops / moonlight unchanged. Only the clear pane transmits sun.
- NOT the target: Anomaly's `Hediff_LightExposure` keys off `PsychGlowAt`, already
  fed by the existing `GlowGrid.GroundGlowAt` patch — leave alone.

## Feature 2 — Tinted glass paned skylight (new variant)

Identical once installed (same texture, light, crops, moonlight, weather-seal) but
**filters out gene sunlight** — the safe choice for colonies with sun-sensitive
pawns. Lore: tinted glass blocks UV.

- New **tinted glass** resource + electric-smelter recipe (mirrors structural glass).
- New `Skylight_PanedTinted` building; **gated behind Complex Furniture** (on top of
  Electricity). Clear pane stays at Electricity.
- **Grid split:** keep `SkylightGrid` for lighting/render/glow — BOTH panes register
  (both light + grow crops). Add a second per-map set, e.g. `SunlightGrid`, that
  ONLY the clear pane registers; the `InSunlight` postfix keys off that set. Tinted
  stays out of it → `InSunlight` false → genes fully blocked.
- `CompProperties_Skylight`: add a `transmitsSun` bool (clear pane = true).

## Feature 3 — Low-tech / tribal skylight tier

A simpler skylight usable **without Electricity**, so tribal and low-tech colonies
aren't locked out.

- Soft dome-style light (no crops) — so it needs none of the gene machinery.
- **Unlocked at Smithing.**
- **Built on-site directly from raw materials** (wood/steel), NOT from
  electric-smelter components — sidesteps the "smelter needs power" problem.
- Balanced down: weaker light; snow-storm vulnerability (author's notes).

### Structural-support mechanic (tribal skylight only)

The reinforced electric panes have no support limit; the crude tribal one does.

- **Prevent install** > 3 tiles from a wall/pillar: a `PlaceWorker` returns a
  failed placement report ("must be within 3 tiles of a wall or pillar") when no
  `holdsRoof` edifice is within 3 tiles of the target cell.
- **Collapse on lost support:** if an already-built tribal skylight later ends up
  with no wall/pillar within 3 tiles (a supporting wall was deconstructed or
  destroyed), its roof tile caves in — `RoofCollapserImmediate.DropRoofInCells`
  on the skylight's **own single tile**, no warning. Destroys the skylight and
  crushes what's under it. Checked on `CompTickRare`.
- **Flavor:** the item description states the 3-tile requirement; the tile reads
  **"weak glass"** on mouseover.
- **Vanilla APIs:** roof support = `holdsRoof` edifices within
  `RoofCollapseUtility.RoofMaxSupportDistance` (6.9). We use a custom 3-tile check
  (radial `holdsRoof` scan), and vanilla `DropRoofInCells` for the collapse. See
  memory `roof-support-and-collapse`.
- **Open detail:** skylights are currently `selectable=false`; decide how the
  "weak glass" readout surfaces (mouseover label vs. making the tribal one
  minimally inspectable).

## Build order

1. **Feature 1** first — smallest, highest-value, de-risked. Add `transmitsSun` to
   `CompProperties_Skylight`, register clear-pane cells in a new `SunlightGrid`,
   postfix `InSunlight`. In-game test with a sun-gene xenotype under a clear pane.
2. **Feature 2** — tinted glass resource + recipe + building def, Complex Furniture
   research prereq, wire `transmitsSun=false`. Verify genes fire under clear but not
   tinted; both still light + grow.
3. **Feature 3** — low-tech building def, Smithing prereq, raw-material cost, on-site
   build. Balance pass.

## Open / deferred

- Exact tinted texture treatment (installed look is identical; any tint tell in the
  build menu / uninstalled item?).
- Low-tech tier: final light radius, distance-from-wall limit, storm behavior.
- Steam replies to Rain / ne_propheta are pending (bridge was offline at planning
  time); GitHub tracking issues can be filed independently.
