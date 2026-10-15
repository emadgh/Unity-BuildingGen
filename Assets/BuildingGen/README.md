# Unity BuildingGen — Yazd Architecture

Editor-time procedural building generator for Unity, aimed at traditional Yazd / central-Iran urban fabric.

## v0.2 features

- Deterministic seed-based generation.
- `Rectangle`, `LShape`, `UShape`, and `Courtyard` footprints.
- Generic wall-segment generation for concave and courtyard plans.
- Courtyard-facing opening rules and open courtyard roof.
- Weighted door/window prefabs with separate low-poly Boolean cutter meshes.
- Recessed entrance visuals while keeping cutters aligned to the wall.
- Flat roof slabs and perimeter-following parapets.
- Reusable `YazdModuleDefinition` prefabs.
- Seed-driven iwan and badgir placement.
- One-click simple Yazd courtyard demo.

## Install

Keep this folder at `Assets/BuildingGen` in the Unity project.

Install ProBuilder from Package Manager. The current CSG backend uses ProBuilder Experimental Features:

`Edit > Preferences/Settings > ProBuilder > Experimental Features Enabled`

Without CSG, massing and visible prefabs still generate, but walls are not cut.

## Demo

Run:

`Tools > BuildingGen > Create Simple Yazd Demo`

Generated sample assets are written under:

`Assets/BuildingGen/Samples/Generated`

The scene object `YazdBuilding_Demo` starts as a courtyard house and includes primitive arch door/window, iwan, and badgir samples.

## Opening prefab contract

```text
PF_Window_Arch_01
├── OpeningDefinition
├── Visual
│   └── imported 3ds Max art
└── Cutter
    └── closed low-poly cutter mesh
```

Authoring convention:

- Pivot: bottom-center of opening.
- `+Y`: up.
- `+Z`: outward from facade.
- Cutter must be closed/manifold and deeper than the maximum wall thickness.
- Keep visual complexity separate from cutter complexity.

## Architectural module contract

```text
PF_Badgir_01
├── YazdModuleDefinition
└── Visual
```

Use the same bottom-center / `+Z` outward orientation. `YazdModuleDefinition` stores kind, nominal size, selection weight and edge clearance.

## Non-destructive generation

`BuildingGenerator` remains the source of truth. Every Generate/Randomize pass deletes `__GeneratedBuilding` and rebuilds from the source parameters and Seed. `Bake Copy` creates a detached accepted result.

## Current limitations

- Boolean backend is still ProBuilder experimental CSG.
- Iwan v0.2 is a visual module and does not yet carve a deep architectural recess.
- Sample assets are intentionally primitive placeholders.
- v0.2 still needs compile/geometry validation inside the target Unity Editor version.

## Next targets

- Real 3ds Max Yazd asset set.
- Iwan recess CSG.
- Hashti/entrance corridor generation.
- Barrel vaults and domes.
- Stepped roof volumes.
- Courtyard arcades and repeated bays.
- Buttresses, niches, brick bands, aging/damage.
- LOD/collider/lightmap bake stage.
