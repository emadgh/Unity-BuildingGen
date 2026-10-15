# Unity BuildingGen — Yazd Architecture

Editor-time procedural building generator for Unity, aimed at traditional Yazd / central-Iran urban fabric.

## Current v0.1 scope

- Deterministic seed-based building generation.
- Rectangular building mass with 1–5 floors.
- Separate front/back/left/right wall meshes.
- Weighted door/window prefab libraries through `YazdBuildingStyle`.
- Each opening prefab contains visible art plus a dedicated closed CSG cutter mesh.
- Automatic prefab placement and wall subtraction.
- Flat roof and parapet generation.
- Front/back/side facade probabilities.
- Inspector actions: Generate, Randomize, previous/next Seed, Clear, Bake Copy.
- One-click simple Yazd demo that creates sample arch-window/arch-door prefabs and a sample style asset.

## Install into an existing Unity project

Copy this repository's `Assets/BuildingGen` folder into your Unity project's `Assets` folder.

Install **ProBuilder** from Unity Package Manager. The v0.1 boolean backend uses ProBuilder's experimental CSG. In Unity enable:

`Edit > Preferences/Settings > ProBuilder > Experimental Features Enabled`

If experimental CSG is disabled, BuildingGen still creates the massing and places the visible prefabs, but does not cut the wall. The Inspector displays a warning.

## First demo

After Unity recompiles scripts:

`Tools > BuildingGen > Create Simple Yazd Demo`

The command creates sample assets under:

`Assets/BuildingGen/Samples/Generated`

and creates `YazdBuilding_Demo` in the active scene.

Select `YazdBuilding_Demo` and use **Randomize** or the Seed buttons.

## 3ds Max opening-prefab authoring convention

Build the final door/window art in 3ds Max. The Unity prefab root uses this convention:

- Pivot: bottom-center of the architectural opening.
- Local `+Z`: points out of the facade.
- Local `+Y`: up.
- `Visual`: imported final art; frame, glass, wood, decorations, etc.
- `Cutter`: separate, closed/manifold mesh representing the void that must be removed from the wall.
- Cutter depth should exceed the largest supported wall thickness on both sides. For example, a 1.0–1.2 m cutter depth is safe for a ~0.35–0.50 m adobe wall.
- Keep the cutter low-poly. The visual asset can be high-detail; boolean complexity should come only from the opening silhouette.
- For arches, the cutter itself should contain the arch profile. BuildingGen does not approximate it with a rectangular hole.

Prefab hierarchy:

```text
PF_Window_Arch_01
├── Visual
│   └── imported art
└── Cutter
    └── closed cutter mesh
```

Add `OpeningDefinition` to the prefab root and assign `Visual Root` and `Cutter Root`.

## Non-destructive workflow

`BuildingGenerator` is the source of truth. Regeneration deletes `__GeneratedBuilding` and rebuilds from the original parameters and Seed. It never applies another round of booleans to an already repeatedly edited source wall.

Use **Bake Copy** only when a generated building is accepted. The baked copy is detached from the generator and has cutter objects/components removed.

## Architecture

```text
BuildingGenerator
    |
    +-- YazdBuildingStyle
    |       +-- door prefab pool
    |       +-- window prefab pool
    |       +-- facade probabilities
    |       +-- roof/parapet rules
    |
    +-- BuildingBuilder (Editor)
            +-- massing
            +-- opening placement
            +-- CSG backend
            +-- roof/parapet
            +-- bake

Opening prefab
    +-- OpeningDefinition
    +-- Visual
    +-- Cutter
```

The CSG implementation is isolated behind `ProBuilderBooleanBackend`; later versions can replace it without rewriting facade placement or the prefab format.

## Planned next steps

- Real Yazd asset library imported from 3ds Max.
- Multiple building footprints: L, U, courtyard, narrow alley house.
- Iwan / recessed entrance rules.
- Badgir (windcatcher) modules.
- Dome and barrel-vault roof modules.
- Wall buttresses and stepped rooflines.
- Facade bays, niches, brick bands, and decorative arches.
- Controlled age/damage variation.
- Courtyard doors and internal facade generation.
- LOD/collider/lightmap bake stage.
- Replace/augment experimental ProBuilder CSG if production testing requires a more robust backend.
