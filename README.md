# Unity BuildingGen — Yazd Architecture

Editor-time procedural building generator for Unity, aimed at traditional Yazd / central-Iran urban fabric.

## Current v0.2 scope

- Deterministic seed-based generation.
- Four footprint modes: `Rectangle`, `LShape`, `UShape`, `Courtyard`.
- Generic wall-segment pipeline instead of four hard-coded facade meshes.
- True central courtyard mode with an open roof and courtyard-facing facade rules.
- Weighted door/window prefab libraries through `YazdBuildingStyle`.
- Each opening prefab contains visible art plus a dedicated closed CSG cutter mesh.
- Automatic prefab placement and wall subtraction.
- Recessed entrance option: the visible door art moves inward while the cutter remains aligned to the wall.
- Flat roof slabs and parapets that follow the generated footprint perimeter.
- Reusable Yazd architectural modules via `YazdModuleDefinition`.
- Iwan and badgir prefab pools with deterministic seed-based placement.
- Front/back/side/courtyard opening probabilities.
- Inspector footprint preset buttons plus Generate, Randomize, previous/next Seed, Clear and Bake Copy.
- One-click demo creates simple arch window/door, iwan and badgir prefabs and a courtyard building.

## Install into an existing Unity project

Copy this repository's `Assets/BuildingGen` folder into your Unity project's `Assets` folder.

Install **ProBuilder** from Unity Package Manager. The current boolean backend uses ProBuilder's experimental CSG. In Unity enable:

`Edit > Preferences/Settings > ProBuilder > Experimental Features Enabled`

If experimental CSG is disabled, BuildingGen still creates the massing and places visible prefabs, but does not cut the walls. The Inspector displays a warning.

## First demo

After Unity recompiles scripts:

`Tools > BuildingGen > Create Simple Yazd Demo`

The command creates sample assets under:

`Assets/BuildingGen/Samples/Generated`

and creates `YazdBuilding_Demo` in the active scene.

The demo starts as a two-floor courtyard house with:

- arched door and window samples,
- separate low-poly arch cutters,
- an iwan placeholder,
- a simple badgir placeholder,
- open central courtyard,
- roof parapets,
- deterministic seed `1403`.

Select `YazdBuilding_Demo` and try the four footprint buttons or change the Seed.

## 3ds Max opening-prefab authoring convention

Build the final door/window art in 3ds Max. The Unity prefab root uses this convention:

- Pivot: bottom-center of the architectural opening.
- Local `+Z`: points out of the facade.
- Local `+Y`: up.
- `Visual`: imported final art; frame, glass, wood, decorations, etc.
- `Cutter`: separate, closed/manifold mesh representing the void removed from the wall.
- Cutter depth should exceed the largest supported wall thickness on both sides. A 1.0–1.2 m cutter is suitable for roughly 0.35–0.50 m adobe walls.
- Keep the cutter low-poly. The visual asset can be detailed; boolean complexity should come only from the opening silhouette.
- For arches, the cutter itself contains the arch profile. BuildingGen does not replace it with a rectangular hole.

Prefab hierarchy:

```text
PF_Window_Arch_01
├── Visual
│   └── imported art
└── Cutter
    └── closed cutter mesh
```

Add `OpeningDefinition` to the prefab root and assign `Visual Root` and `Cutter Root`.

## Yazd module convention

Iwans, badgirs and future reusable modules use the same root orientation:

- Pivot: bottom-center.
- `+Y`: up.
- `+Z`: outward / facade-facing direction.
- Add `YazdModuleDefinition` to the prefab root.
- Set `Kind`, nominal dimensions and selection weight.

Example:

```text
PF_Badgir_01
├── YazdModuleDefinition
└── Visual
    ├── Tower
    ├── Openings
    └── Cap
```

The sample modules are deliberately primitive. Replace them with production FBX prefabs from 3ds Max without changing the generator code.

## Footprints

### Rectangle

Standard enclosed mass.

### L Shape

Rear bar plus one side wing. `Wing Width` controls the solid wing thickness.

### U Shape

Two side wings plus a rear bar with an open court/recess toward the front. `U Recess Depth` controls how deeply the open space cuts into the footprint.

### Courtyard

Enclosed outer footprint with a true open central court. `Courtyard Width` and `Courtyard Depth` control the void. Courtyard-facing walls use `Courtyard Window Chance` from the style.

## Non-destructive workflow

`BuildingGenerator` is the source of truth. Regeneration deletes `__GeneratedBuilding` and rebuilds geometry from parameters and Seed. It never applies another generation pass to already-booleaned wall geometry.

Use **Bake Copy** only after accepting a generated building. The baked copy is detached from the generator and removes opening cutters and module metadata components.

## Architecture

```text
BuildingGenerator
    |
    +-- footprint parameters
    |       +-- Rectangle
    |       +-- L
    |       +-- U
    |       +-- Courtyard
    |
    +-- YazdBuildingStyle
    |       +-- door prefab pool
    |       +-- window prefab pool
    |       +-- iwan prefab pool
    |       +-- badgir prefab pool
    |       +-- facade probabilities
    |       +-- roof/parapet rules
    |
    +-- BuildingBuilder (Editor)
            +-- polygon wall segments
            +-- roof mass rectangles
            +-- opening placement
            +-- CSG backend
            +-- Yazd modules
            +-- bake
```

The CSG implementation remains isolated behind `ProBuilderBooleanBackend`; later versions can replace it without rewriting footprint generation, facade placement or the prefab contract.

## Next production steps

- Test v0.2 inside the target Unity version with ProBuilder experimental CSG enabled.
- Replace sample modules with real Yazd doors/windows/badgirs/iwans from 3ds Max.
- Add proper iwan recess cutting instead of the current visual module placement.
- Add entrance corridor / hashti rules for courtyard houses.
- Add dome and barrel-vault roof modules.
- Add stepped roof heights and multi-volume buildings.
- Add wall buttresses, niches, brick bands and decorative arches.
- Add controlled age/damage variation and facade decals.
- Add courtyard arcades and repeated bay rules.
- Add LOD/collider/lightmap bake stage.
- Replace or augment experimental ProBuilder CSG if production tests require a more robust backend.
