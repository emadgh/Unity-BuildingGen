# Unity BuildingGen — Yazd Architecture

Editor-time procedural building generator for Unity, focused on traditional Yazd / central-Iran urban fabric.

## v0.3 features

- Deterministic seed-based generation.
- `Rectangle`, `LShape`, `UShape`, and `Courtyard` footprints.
- Generic wall-segment generation for concave and courtyard plans.
- Courtyard-facing opening rules and open courtyard roof.
- Weighted door/window prefabs with separate low-poly Boolean cutter meshes.
- Recessed entrance visuals while keeping cutters aligned to the wall.
- Simple hashti + straight corridor + deterministic 90-degree bent corridor generated from the actual door position.
- Reusable `YazdModuleDefinition` prefabs.
- Iwan modules with optional host-wall Cutter/CSG recess.
- Repeated courtyard arcade bays.
- Seed-driven badgir placement.
- Dome and barrel-vault roof-form pools.
- Flat roof slabs and perimeter-following parapets.
- `Traditional Yazd Courtyard` Inspector preset.
- One-click primitive demo asset factory.

## Install

Keep this folder at `Assets/BuildingGen` in the Unity project.

Install ProBuilder from Package Manager. The current CSG backend uses ProBuilder Experimental Features:

`Edit > Preferences/Settings > ProBuilder > Experimental Features Enabled`

Without CSG, massing and visible prefabs still generate, but opening/iwan cutters do not modify walls.

## Demo

Run:

`Tools > BuildingGen > Create Simple Yazd Demo`

Generated test assets are written under:

`Assets/BuildingGen/Samples/Generated`

The scene object `YazdBuilding_Demo` starts as a two-floor courtyard house and includes primitive arched door/window, recessed iwan, arcade bay, badgir, dome and barrel-vault samples plus a generated hashti/bent entrance route.

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

- Pivot: bottom-center.
- `+Y`: up.
- `+Z`: outward from facade.
- Cutter must be closed/manifold and deeper than the maximum wall thickness.
- Keep visual complexity separate from cutter complexity.

## Architectural module contract

```text
PF_Iwan_01
├── YazdModuleDefinition
├── Visual
└── Cutter             # optional
```

`YazdModuleDefinition` supports Iwan, Badgir, ArcadeBay, Dome, BarrelVault and generic module kinds. Facade modules use bottom-center / `+Z` outward orientation. Roof modules use bottom-center / `+Y` up.

For an iwan recess, enable `Cut Host Surface`, assign `Cutter Root`, and enable `BuildingGenerator > Cut Iwan Recess`.

## Non-destructive generation

`BuildingGenerator` remains the source of truth. Every Generate/Randomize pass deletes `__GeneratedBuilding` and rebuilds from source parameters and Seed. `Bake Copy` creates a detached accepted result and removes cutter/module metadata.

## Current limitations

- Boolean backend is still ProBuilder experimental CSG.
- Hashti/corridor geometry is a rule/placement prototype made from primitives, not a production interior kit.
- Arcade bays do not yet reserve the bay occupied by an iwan.
- Wall height is still uniform across the whole building.
- Sample assets are intentionally primitive placeholders.
- v0.3 still needs compile/geometry validation inside the target Unity Editor version.

## Next targets

- Real 3ds Max Yazd asset set.
- Modular hashti/corridor/interior connection kit.
- Iwan/arcade bay zoning.
- Stepped and multi-height massing.
- Production domes, vaults and squinches.
- Buttresses, niches, brick bands, aging/damage.
- LOD/collider/lightmap bake stage.
