# Skylights

Bring real daylight into a roofed room. A mod for RimWorld 1.6.

**Steam Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3780129860

Powerless and cool, skylights light a room by the actual sky above — bright at midday, fading through dusk, dark at night — and never make a spot brighter than the day outside.

## Buildings

Two forms: **panes** open one roof tile to the full sky (grow crops; clear ones pass true sunlight to Biotech genes), and **domes** spread a soft glow over a radius (no crops).

**Panes**

- **Basic skylight** — the pre‑industrial pane, unlocked at **Smithing**. Hand‑built on‑site from wood + one basic pane, no smelter. Full‑sky lighting, grows crops, passes true sunlight to genes. **Weak**: only holds if a wall or pillar sits within **3 tiles**; lose that support and the roof caves in and destroys the materials.
- **Industrial skylight** — the sturdy pane (**Electricity**). Cast structural glass, drag‑placed like floors, no support limit. Same full‑sky lighting, crops and true sunlight.
- **Tinted skylight** — same sturdy pane in UV‑filtering tinted glass (**Complex Furniture**). Identical light and crops, but blocks the sun for genes — sun‑sensitive colonists are safe beneath it.

**Domes**

- **Dome skylight** — spreads soft daylight over an adjustable radius (default 4). Never enough to grow crops. Unlocked at **Smithing**: hand‑build the dome at a crafting spot, or cast it faster at a smelter.
- **Light tunnel** — a reflection tube pipes daylight down through solid overhead mountain (**Electricity + Complex Furniture**).

Skylights sit up in the roof: they can't be clicked, and are removed with the **Deconstruct** tool.

## Supply chain

**Pre‑industrial** — at a crafting spot:

| Item | Cost | Research |
| --- | --- | --- |
| Basic pane | 1 steel → 1 (single or bulk) | Smithing |
| Skylight dome (by hand) | 2 steel → 1 | — |

**Industrial** — at an **electric smelter**:

| Item | Cost | Research |
| --- | --- | --- |
| Skylight dome | 2 steel → 1 (faster than by hand) | Electricity |
| Structural frame | 1 steel → 4 (single or bulk) | Electricity |
| Structural glass | 1 steel → 1 (single or bulk) | Electricity |
| Tinted structural glass | 1 steel → 1 (single or bulk) | Electricity + Complex Furniture |
| Reflection tube | 5 steel → 1 | Electricity + Complex Furniture |

Frames and glass are inert and never rot outdoors.

## Mod settings

- **Dome skylight light radius** — a 1–10 slider (default 4) controlling how far the dome and light‑tunnel glow spreads. Takes effect immediately.

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

## Building from source

C# lives in `Source/`. Build with `dotnet build -c Release` (outputs to `1.6/Assemblies/`).

## License

See [LICENSE](LICENSE).
