# Unity BuildingGen — Yazd Architecture

Editor-time procedural building and wall tools for Unity, focused on traditional Yazd / central-Iran urban fabric.

## Current v0.5 scope

### Building Generator

- Deterministic seed-based generation.
- `Rectangle`, `LShape`, `UShape`, `Courtyard` footprints.
- Generic polygon wall-segment pipeline.
- True open central courtyard.
- Weighted door/window prefab pools.
- Door/window prefabs can contain low-poly closed Boolean cutters.
- Recessed entrance art.
- Hashti + straight corridor + deterministic 90-degree bent corridor prototype.
- Reusable `YazdModuleDefinition` modules.
- Iwan modules can carry their own CSG recess cutter.
- Courtyard arcade bays.
- Badgir placement.
- Dome and barrel-vault roof-form pools.
- Flat roofs and perimeter parapets.
- Automatic generated-module conflict resolution: iwan wins over overlapping arcade bays; badgir wins over overlapping dome/vault modules.
- Non-destructive Generate / Randomize / Bake workflow.

### Wall Spline Tool

- Editable Scene-view wall path.
- `Polyline` and `SmoothCatmullRom` modes.
- Closed-loop support.
- Shift + click point append workflow.
- `SimpleMud`, `RuinedMud`, `DecoratedMud` styles.
- Deterministic chunk-level damage and missing wall sections.
- Terrain conform using editor-time raycasts.
- Gate/opening markers stored by distance along the spline.
- Optional visible gate/arch prefab at each opening.
- Crown/top prefab scattering.
- Pillar prefab pools.
- Buttress prefab pools.
- Niche/detail prefab pools.
- Broken/finished end-cap prefabs.
- Detail systems avoid gate intervals.
- Bake Wall Copy workflow.

## Install

Copy:

`Assets/BuildingGen`

into the target Unity project's `Assets` folder.

For building Boolean operations, install **ProBuilder** through Package Manager and enable its Experimental Features:

`Edit > Preferences/Settings > ProBuilder > Experimental Features Enabled`

Without ProBuilder CSG, massing and visible prefabs still generate; cutters simply do not modify the wall meshes.

## Main demos

Building demo:

`Tools > BuildingGen > Create Simple Yazd Demo`

Basic spline-wall demo:

`Tools > BuildingGen > Create Wall Spline Demo`

Advanced spline-wall demo:

`Tools > BuildingGen > Create Advanced Wall Spline Demo`

Generated sample assets are written under:

`Assets/BuildingGen/Samples/Generated`

## 3ds Max opening convention

Door/window prefab:

```text
PF_Window_Arch_01
├── OpeningDefinition
├── Visual
│   └── imported final art
└── Cutter
    └── closed low-poly cutter mesh
```

Recommended root convention:

- Pivot = bottom-center of opening.
- `+Y` = up.
- `+Z` = outward from facade.
- Cutter depth exceeds maximum supported wall thickness.
- Keep visual complexity separate from cutter complexity.

## Yazd architectural modules

Reusable modules use `YazdModuleDefinition`.

Current kinds include:

- `Iwan`
- `Badgir`
- `ArcadeBay`
- `Dome`
- `BarrelVault`
- `Hashti`
- generic facade / roof props

An iwan can use:

```text
PF_Iwan_01
├── YazdModuleDefinition
│   └── Cut Host Surface = true
├── Visual
└── Cutter
```

The generated facade conflict resolver currently enforces these initial priority rules:

```text
Iwan      > ArcadeBay
Badgir    > Dome
Badgir    > BarrelVault
```

This is the first layer of the planned facade-bay zoning system and prevents the most obvious generated overlaps without coupling conflict logic into the CSG builder.

Manual resolver command:

`Tools > BuildingGen > Resolve Generated Facade Conflicts`

It is also queued automatically after hierarchy changes in edit mode.

## Wall Spline authoring

Create a wall with:

`Tools > BuildingGen > Create Wall Spline`

The source `WallSpline` stores control points and a `WallSplineStyle`. Generated geometry lives below:

```text
WallSpline
└── __GeneratedWallSpline
    ├── Wall
    ├── Openings
    ├── TopPrefabs
    └── WallDetails
```

For detailed controls and 3ds Max conventions see:

`Assets/BuildingGen/WALL_SPLINE.md`

### Terrain conform

When enabled, the sampled path raycasts downward to scene colliders using:

- layer mask,
- ray height,
- ray distance,
- vertical offset.

Wall chunks remain vertical and extend down to the lower end of each terrain sample so they do not visibly float on slopes.

### Gates

A gate marker stores distance + width along the spline. The generator skips wall chunks inside that interval and can instantiate a gate/arch prefab there.

Recommended gate prefab convention:

- bottom-center pivot,
- `+Y` up,
- `+Z` points to the selected outward/detail side,
- `+X` runs along the wall.

### Repeated wall details

`WallSplineStyle` can independently enable weighted prefab pools for:

- crown/top pieces,
- pillars,
- buttresses,
- niches,
- end caps.

All randomness remains deterministic from `Seed`.

## Non-destructive workflow

Building generation rebuilds `__GeneratedBuilding` from source parameters on every pass.

Wall spline generation rebuilds `__GeneratedWallSpline` from source points/style on every pass.

Use `Bake Copy` / `Bake Wall Copy` only after accepting a result.

## Validation status

This repository is intentionally a drop-in `Assets/BuildingGen` toolset rather than a complete Unity project. Static review is performed in the repository, but compile/SceneView/ProBuilder CSG validation must still be run in the target Unity Editor.

## Next production targets

- Full facade-bay planner before placement rather than only post-generation conflict resolution.
- Multi-height / stepped Yazd building masses.
- Real 3ds Max door/window/iwan/badgir/arcade kit.
- Terrain-aware alleys and connected building/wall generation.
- Wall gates with optional CSG/door logic and navigation links.
- Better ruined wall silhouettes using dedicated end/break meshes.
- LOD, collider, lightmap and batching bake stage.
- Production-tested replacement/augmentation for experimental ProBuilder CSG if required.
