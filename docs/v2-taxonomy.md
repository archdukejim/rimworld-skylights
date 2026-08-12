# Skylights v2 — Taxonomy Spec (RECONCILED with your edits)

Your table edits + the Basic Pane addition are baked in below. Same rules as before:
edit any `field: value` line, answer the `ANSWER:` lines, then say "taxonomy updated".
Nothing changes in code until you give the go. **Read the DECISIONS block first** — a few
choices affect existing players' saves.

---

## DECISIONS I NEED (answer these)

**DEC-1 — defName renames vs save safety.** Renaming a building's *label* is free and safe.
Renaming its internal *defName* breaks existing v1 saves that already placed one. v1 shipped
`Skylight_Paned`, `Skylight_MountainDome`, `Skylight_Dome`. My default = keep those defNames,
change only labels; give brand-new v2 things clean defNames. Proposed mapping:
- Basic Skylight  -> defName `Skylight_Basic` (new)
- Industrial Skylight -> KEEP defName `Skylight_Paned`, label "industrial skylight"
- Tinted Skylight -> defName `Skylight_Tinted` (new; replaces the uncommitted `Skylight_PanedTinted`)
- Dome Skylight -> KEEP `Skylight_Dome`
- Light Tunnel -> KEEP defName `Skylight_MountainDome`, label "light tunnel"
- Basic Pane item -> `BasicPane` (new); Tinted glass item -> KEEP `TintedGlass`, label "tinted structural glass"
ANSWER (keep-defNames-change-labels / rename-everything-and-break-v1-saves):

**DEC-2 — "smithing table" station.** Vanilla has no generic smithing table; the metal bench is
the **Smithy** (FueledSmithy + ElectricSmithy). I'll host the hand recipes (Basic Pane, dome hand
recipe) there. OK, or a Crafting spot instead?
ANSWER:

**DEC-3 — Dome hand recipe research = "none".** The recipe needs no research, but the Smithy
station itself needs Smithing to build — so it's effectively Smithing-gated. Fine?
ANSWER:

---

## FORM 1 — PANES (flat glass tile, drag-placed, lit as open sky, grows crops)

### P1. Basic Skylight
- defName: Skylight_Basic
- label: basic skylight
- research: Smithing
- smelter_required: no
- install_materials: 4 WoodLog + 1 BasicPane
- install_work: 350
- light: full sky on its tile
- crops: yes
- gene_sun: yes — both positive AND negative effects (sun-lovers gain, sun-sensitive burn)
- structural_rule: weak — needs a wall/pillar within 3 tiles; if support is lost the roof caves in on this tile AND destroys the materials (no salvage)
- max_hp: 40

### P2. Industrial Skylight
- defName: Skylight_Paned   // keep for save safety, see DEC-1
- label: industrial skylight
- research: Electricity
- smelter_required: yes
- install_materials: 4 StructuralFrame + 1 StructuralGlass
- install_work: 210
- light: full sky on its tile
- crops: yes
- gene_sun: yes — both positive AND negative effects
- structural_rule: sturdy, no special rule (vanilla roof collapse only)
- max_hp: 50

### P3. Tinted Skylight
- defName: Skylight_Tinted
- label: tinted skylight
- research: ComplexFurniture + Electricity (both)
- smelter_required: yes
- install_materials: 4 StructuralFrame + 1 TintedGlass
- install_work: 210
- light: full sky on its tile
- crops: yes
- gene_sun: no effects (UV-filtered)
- structural_rule: sturdy, no special rule (vanilla roof collapse only)
- max_hp: 50

---

## FORM 2 — DOMES (spread soft glow over a radius, no crops)

### D1. Dome skylight
- defName: Skylight_Dome
- label: dome skylight
- research: Smithing
- smelter_required: no (smelter only makes the prefab cheaper to craft — see C1)
- install_materials: 1 SkylightDome (prefab)
- install_work: 180
- light: soft glow, radius from mod setting (default 4)
- crops: no
- gene_sun: no
- structural_rule: sturdy, no special rule (vanilla roof collapse only)
- notes: blocked by thick overhead mountain
- max_hp: 60

### D2. Light tunnel (mountain dome)
- defName: Skylight_MountainDome   // keep for save safety, see DEC-1
- label: light tunnel
- research: ComplexFurniture + Electricity (both)
- smelter_required: yes
- install_materials: 1 SkylightDome + 1 ReflectionTube
- install_work: 2500
- light: soft glow radius (same as dome)
- crops: no
- gene_sun: no
- structural_rule: sturdy, no special rule (vanilla mountain collapse only)
- notes: pierces thick overhead mountain
- max_hp: 60

---

## CRAFTED COMPONENTS (manufacturing)

### C1. Skylight dome (prefab) — TWO recipes ("dome doesn't need a smelter")
- item_defName: SkylightDome
- recipe_a_station: Smithy (see DEC-2)
- recipe_a_research: none (station gates it — see DEC-3)
- recipe_a_mfg_work: 1800
- recipe_a_cost: 2 Steel -> 1
- recipe_a_note: pre-industrial
- recipe_b_station: Electric smelter
- recipe_b_research: Electricity
- recipe_b_mfg_work: 1000
- recipe_b_cost: 2 Steel -> 1
- recipe_b_note: efficiency

### C2. Basic Pane   // NEW — tribal glazing for the Basic Skylight
- item_defName: BasicPane
- label: basic pane
- station: Smithy (see DEC-2)
- research: Smithing
- mfg_work: 210 (single) / 1875 (bulk)
- yield: 1 Steel -> 1 (single) ; 10 Steel -> 10 (bulk)
- notes: inert, no rot

### C3. Structural frame
- item_defName: StructuralFrame
- station: Electric smelter
- research: Electricity
- mfg_work: 210 / 1875 bulk
- yield: 1 Steel -> 4 ; 10 Steel -> 40
- notes: inert, no rot

### C4. Structural glass
- item_defName: StructuralGlass
- station: Electric smelter
- research: Electricity
- mfg_work: 210 / 1875 bulk
- yield: 1 Steel -> 1 ; 10 Steel -> 10
- notes: inert, no rot

### C5. Tinted structural glass
- item_defName: TintedGlass   // keep defName, label -> "tinted structural glass"
- label: tinted structural glass
- station: Electric smelter
- research: Electricity + ComplexFurniture (both)
- mfg_work: 260 / 2300 bulk
- yield: 1 Steel -> 1 ; 10 Steel -> 10
- notes: inert, no rot (UV-filtering)

### C6. Reflection tube
- item_defName: ReflectionTube
- station: Electric smelter
- research: Electricity + ComplexFurniture (both)
- mfg_work: 1250
- yield: 5 Steel -> 1

---

## EFFECTS ON PAWNS (edit if wrong)
- Clear panes (P1 Basic, P2 Industrial): tile is real open sky — grows crops by day, dark at night, moonlight + shadows, weather-sealed. Biotech sun-genes fire both ways: sun-lovers gain, sun-sensitive burn / take the mood hit.
- Tinted pane (P3): identical light + crops, sun-genes do NOT fire.
- Domes (D1, D2): soft ambient glow over a radius, no crops, no gene sun, no shadows; weather-sealed; Beauty +1.
- Basic Skylight (P1) only: roof caves in on its tile if the supporting wall/pillar is removed, and the build materials are destroyed (no salvage).

---

## ANY OTHER CHANGES
NOTES:
