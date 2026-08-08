# Skylights

Bring real daylight into a roofed room. A mod for RimWorld 1.6.

**Steam Workshop:** https://steamcommunity.com/sharedfiles/filedetails/?id=3780129860

Powerless and cool, skylights light a room by the actual sky above — bright at midday, fading through dusk, dark at night — and never make a spot brighter than the day outside.

## Buildings

- **Dome skylight** — spreads soft daylight over a ~2‑tile radius. Lights a room but never enough to grow crops. Drops in from a prefab skylight dome.
- **Paned skylight** — the tile below is lit *exactly as if there were no roof*: sun and shadows by day, moonlight by night, while staying sealed against rain, snow and cold. Grows crops by daylight. Drag it out like laying floors.
- **Mountain dome skylight** — a reflection tube pipes daylight down through solid overhead mountain.

Skylights sit up in the roof: they can't be clicked, and are removed with the **Deconstruct** tool.

## Supply chain

All fabricated at an **electric smelter** (unlocked with **Electricity**):

| Item | Cost |
| --- | --- |
| Skylight dome | 2 steel → 1 |
| Reflection tube | 5 steel → 1 |
| Structural frame | 1 steel → 4 (single or bulk) |
| Structural glass | 1 steel → 1 (single or bulk) |

Structural frames and glass are inert and never rot outdoors.

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)

## Building from source

C# lives in `Source/`. Build with `dotnet build -c Release` (outputs to `1.6/Assemblies/`).

## License

See [LICENSE](LICENSE).
