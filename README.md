# Skylights

Bring real daylight into a roofed room. A mod for RimWorld 1.6.

**Steam Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3780129860

Powerless and cool, skylights light a room by the actual sky above — bright at midday, fading through dusk, dark at night — and never make a spot brighter than the day outside.

## Buildings

- **Dome skylight** — spreads soft daylight over an adjustable radius (default ~4 tiles). Lights a room but never enough to grow crops. Drops in from a prefab skylight dome.
- **Paned skylight** — the tile below is lit *exactly as if there were no roof*: sun and shadows by day, moonlight by night, while staying sealed against rain, snow and cold. Grows crops by daylight. Passes **true sunlight** too, so Biotech sun‑genes register it — sun‑lovers gain their outdoor benefit indoors and sun‑sensitive pawns are exposed. Drag it out like laying floors.
- **Tinted paned skylight** — identical light and crops, but UV‑filtered: it blocks the sun for genes, so sun‑sensitive colonists are safe beneath it. Needs **Complex Furniture**.
- **Mountain dome skylight** — a reflection tube pipes daylight down through solid overhead mountain.
- **Weak glass skylight** — a low‑tech version unlocked at **Smithing**, built on‑site from raw wood and steel (no smelter or power). Soft light like the dome, but it only holds if a wall or pillar sits within **3 tiles**; lose that support and the roof caves in.

Skylights sit up in the roof: they can't be clicked, and are removed with the **Deconstruct** tool.

## Supply chain

The electric skylights are fabricated at an **electric smelter** (unlocked with **Electricity**):

| Item | Cost |
| --- | --- |
| Skylight dome | 2 steel → 1 |
| Reflection tube | 5 steel → 1 |
| Structural frame | 1 steel → 4 (single or bulk) |
| Structural glass | 1 steel → 1 (single or bulk) |
| Tinted glass | 1 steel → 1 (single or bulk; needs **Complex Furniture**) |

Structural frames and glass are inert and never rot outdoors. The weak glass skylight needs none of this — it's built straight from raw wood and steel.

## Mod settings

- **Dome skylight light radius** — a 1–10 slider (default 4) controlling how far the dome and mountain‑dome light spreads. Takes effect immediately.

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

## Building from source

C# lives in `Source/`. Build with `dotnet build -c Release` (outputs to `1.6/Assemblies/`).

## License

See [LICENSE](LICENSE).
