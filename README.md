# Unity BuildingGen — Yazd Architecture

Editor-time procedural building generator for Unity, focused on traditional Yazd / central-Iran urban fabric.

## Current v0.3 scope

- Deterministic seed-based generation.
- Four footprint modes: `Rectangle`, `LShape`, `UShape`, `Courtyard`.
- Generic polygon wall-segment pipeline for concave and courtyard plans.
- True central courtyard with an open roof and separate courtyard-facing rules.
- Weighted door/window prefab libraries through `YazdBuildingStyle`.
- Opening prefabs contain visible art plus a dedicated closed CSG cutter mesh.
- Automatic prefab placement and editor-time wall subtraction.
- Recessed entrance art while the cutter stays aligned to the host wall.
- Simple generated hashti + straight corridor + 90-degree bent corridor guide tied to the actual generated door position.
- Reusable architectural modules through `YazdModuleDefinition`.
- Iwan modules can optionally contain a `Cutter` root and carve the host wall.
- Repeated courtyard arcade bays.
- Badgir prefab pool with deterministic roof placement.
- Dome and barrel-vault roof-form prefab pools.
- Flat roof slabs and parapets following the generated perimeter.
- Inspector footprint presets plus a one-click `Traditional Yazd Courtyard` preset.
- Generate, Randomize, previous/next Seed, Clear and Bake Copy actions.
- One-click sample factory for primitive test assets.

## Install into an existing Unity project

Copy this repository's `Assets/BuildingGen` folder into your Unity project's `Assets` folder.

Install **ProBuilder** from Unity Package Manager. The current Boolean backend uses ProBuilder's experimental CSG. Enable:

`Edit > Preferences/Settings > ProBuilder > Experimental Features Enabled`

If experimental CSG is unavailable, BuildingGen still creates massing and places visible prefabs/modules. Door, window and iwan cutters simply do not modify the walls and the Inspector shows a warning.

## First demo

After Unity recompiles scripts, run:

`Tools > BuildingGen > Create Simple Yazd Demo`

The command creates sample assets under:

`Assets/BuildingGen/Samples/Generated`

and creates `YazdBuilding_Demo` in the active scene.

The v0.3 demo starts as a two-floor courtyard house with:

- arched door and window samples,
- separate low-poly arch cutters,
- a recessed iwan with its own arch cutter,
- repeated courtyard arcade bays,
- a procedural hashti / bent entrance-route guide,
- a simple badgir,
- simple dome and barrel-vault roof modules,
- open central courtyard,
- roof parapets,
- deterministic seed `1403`.

Select `YazdBuilding_Demo` and use Randomize, Seed navigation, the footprint buttons, or `Traditional Yazd Courtyard`.

## 3ds Max opening-prefab authoring convention

Build the final door/window art in 3ds Max. The Unity prefab root uses this convention:

- Pivot: bottom-center of the architectural opening.
- Local `+Z`: points outward from the facade.
- Local `+Y`: up.
- `Visual`: final art: frame, glass, wood, decorations, etc.
- `Cutter`: separate, closed/manifold mesh representing the void removed from the wall.
- Cutter depth should exceed the largest supported wall thickness on both sides. A 1.0–1.2 m cutter is suitable for roughly 0.35–0.50 m adobe walls.
- Keep the cutter low-poly. Visual complexity stays independent from Boolean complexity.
- For arches, the cutter itself contains the arch silhouette; BuildingGen does not replace it with a rectangular opening.

```text
PF_Window_Arch_01
├── OpeningDefinition
├── Visual
│   └── imported art
└── Cutter
    └── closed cutter mesh
```

Assign `Visual Root` and `Cutter Root` in `OpeningDefinition`.

## Architectural module convention

All reusable Yazd modules use `YazdModuleDefinition`.

Facade modules use:

- Pivot: bottom-center.
- `+Y`: up.
- `+Z`: outward from the facade.

Roof modules use bottom-center / `+Y` up; their yaw can be randomized by the generator.

Supported module kinds currently include:

- `Iwan`
- `Badgir`
- `ArcadeBay`
- `Dome`
- `BarrelVault`
- `Hashti`
- generic roof/facade props

### Iwan with CSG recess

```text
PF_Iwan_01
├── YazdModuleDefinition
│   └── Cut Host Surface = true
├── Visual
│   └── imported iwan art
└── Cutter
    └── closed arch/deep-recess mesh
```

Assign the module's `Cutter Root`. If `BuildingGenerator > Cut Iwan Recess` is enabled, the cutter is subtracted from the chosen host wall and then hidden.

### Arcade bay

```text
PF_ArcadeBay_01
├── YazdModuleDefinition (ArcadeBay)
└── Visual
```

The generator repeats these bays along courtyard-facing wall segments. `Arcade Bay Width`, `Arcade Depth`, `Arcade Side Chance`, and `Arcade Bay Chance` control the layout.

### Roof forms

```text
PF_Dome_01
└── YazdModuleDefinition (Dome)

PF_Vault_01
└── YazdModuleDefinition (BarrelVault)
```

Nominal width/depth and edge clearance are used to reject modules that do not fit a roof mass.

## Entrance sequence / hashti

v0.3 records the actual generated entrance position. The simple procedural guide is built from that point, so a non-centered seeded door produces a matching route.

Current generated guide:

```text
Street / facade
      |
    Door
      |
Straight corridor
      |
    Hashti
      └──── 90° bent corridor
```

The guide uses simple wall/floor primitives on purpose. It establishes the placement/rule system first; production hashti and corridor kits can replace those primitives later.

Controls:

- `Generate Hashti And Corridor`
- `Hashti Size`
- `Entrance Corridor Length`
- `Entrance Corridor Width`
- `Entrance Turn Length`
- `Entrance Turn = Auto / Left / Right`

`Auto` is deterministic from the building Seed.

## Footprints

### Rectangle

Standard enclosed mass.

### L Shape

Rear bar plus one side wing. `Wing Width` controls wing thickness.

### U Shape

Two side wings plus a rear bar with an open front recess. `U Recess Depth` controls how deeply the open space cuts into the footprint.

### Courtyard

Enclosed outer footprint with a true open central court. `Courtyard Width` and `Courtyard Depth` control the void. Courtyard-facing walls use their own opening probability and can receive repeated arcade modules and an iwan.

## Non-destructive workflow

`BuildingGenerator` is the source of truth. Every Generate/Randomize pass deletes `__GeneratedBuilding` and rebuilds from parameters and Seed. Boolean operations are always reapplied to fresh source wall primitives rather than repeatedly cutting an already-modified mesh.

Use **Bake Copy** only after accepting a generated result. The baked copy is detached from the generator and has opening/module cutters plus generator metadata removed.

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
    |       +-- door/window pools
    |       +-- iwan/badgir pools
    |       +-- arcade pool
    |       +-- dome/vault pools
    |       +-- facade/module probabilities
    |       +-- roof/parapet rules
    |
    +-- BuildingBuilder (Editor)
            +-- polygon wall segments
            +-- opening placement
            +-- Boolean backend
            +-- entrance sequence
            +-- courtyard arcade
            +-- iwan CSG
            +-- roof forms / badgir
            +-- bake
```

The CSG implementation remains isolated behind `ProBuilderBooleanBackend`; it can be replaced without rewriting footprint generation, module placement or the prefab contracts.

## Validation status

The repository is still a drop-in `Assets/BuildingGen` tool rather than a complete Unity project with Editor CI. Static review is performed in the repository, but the target Unity version still needs to compile/run the scripts and exercise ProBuilder CSG in-editor.

## Next production steps

- Compile and geometry-test v0.3 in the target Unity project.
- Replace generated sample prefabs with real Yazd assets from 3ds Max.
- Replace the primitive hashti/corridor guide with modular room/corridor prefabs and courtyard connection rules.
- Prevent arcade bays from occupying an iwan bay and add deliberate facade bay zoning.
- Add stepped/multi-height massing instead of one uniform wall height.
- Add proper dome/vault shells, roof transitions and squinches.
- Add wall buttresses, niches, brick bands, decorative arches and recessed panels.
- Add controlled age/damage variation and facade decals.
- Add LOD/collider/lightmap bake stage.
- Replace or augment experimental ProBuilder CSG if production tests require a more robust backend.
