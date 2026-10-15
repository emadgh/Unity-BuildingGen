# Unity BuildingGen — Yazd Architecture

Editor-time procedural building generator for Unity, focused on traditional Yazd / central-Iran urban fabric.

## v0.6 — Advanced WallSpline

- Precise distance-based gate / gap openings on `WallSpline`.
- Exact opening-edge breakpoints instead of dropping an arbitrary whole wall chunk.
- Configurable opening width and clear height.
- Automatic lintel/wall strip above openings when clear height is below wall height.
- Optional 3ds Max / DCC gate-frame prefab at each opening.
- Optional generated edge pillars at gate boundaries.
- Opening distance clamped to the actual sampled Polyline or Catmull-Rom path length.
- Terrain conforming by downward raycast against selectable collider layers.
- Broken wall-top profile variation independent from full missing chunks.
- Repeated buttresses with spacing, probability, height/depth and Left / Right / Both side controls.
- Open-spline start/end caps.
- Crown-prefab and buttress candidates avoid gate-opening zones.
- SceneView cyan gate markers.
- Inspector helpers: `Add Gate At Midpoint` and `Remove Last Gate`.
- Advanced demo command: `Tools > BuildingGen > Create Wall Spline v0.6 Advanced Demo`.

See `WALL_SPLINE.md` for the complete authoring and terrain workflow.

## v0.5 — Facade Bay Planner

- Opt-in reservation-based facade layout.
- Unified `FacadeBayDefinition` metadata for 3ds Max / DCC prefabs.
- Supported bay kinds: `Window`, `Door`, `Iwan`, `Arcade`, `Niche`, and intentional `Blank` bays.
- One overlap map per wall instead of independent random systems.
- Multi-floor reservations, so a two-floor iwan blocks conflicting windows above it.
- Per-prefab width, height, sill, edge clearance, neighbor clearance and floor-span rules.
- Per-prefab allowed facade surfaces: Front / Back / Side / Courtyard.
- Weighted prefab selection and deterministic blank bays.
- Configurable candidate density, fill chance, horizontal jitter and special-bay limit.
- Optional compatibility with the legacy primary entrance so the existing hashti/bent-corridor route can remain active.
- CSG cutters use the existing ProBuilder Boolean backend.
- `Bake Copy` strips facade-bay cutter objects and metadata.
- Demo command: `Tools > BuildingGen > Create Facade Bay v0.5 Demo`.

See `FACADE_BAYS.md` for the production authoring contract.

## v0.4 — WallSpline foundation

- Editable `WallSpline` component with local control points.
- Polyline and Catmull-Rom paths.
- Closed loops.
- SceneView point editing and Shift + click point creation.
- SimpleMud, RuinedMud and DecoratedMud wall styles.
- Deterministic ruined-wall chunk variation and missing sections.
- Weighted top/crown prefab scattering.
- Bake-copy workflow and three sample wall styles.

## v0.3 — Building Massing

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

Install ProBuilder from Package Manager. The current building/facade CSG backend uses ProBuilder Experimental Features:

`Edit > Preferences/Settings > ProBuilder > Experimental Features Enabled`

Without CSG, massing and visible facade prefabs still generate, but opening/iwan/facade-bay cutters do not modify building walls. WallSpline v0.6 gate gaps do not depend on CSG; they are produced directly by interval generation.

## Demos

Legacy building demo:

`Tools > BuildingGen > Create Simple Yazd Demo`

Basic WallSpline demo:

`Tools > BuildingGen > Create Wall Spline Demo`

Advanced WallSpline v0.6 demo:

`Tools > BuildingGen > Create Wall Spline v0.6 Advanced Demo`

Facade Bay v0.5 demo:

`Tools > BuildingGen > Create Facade Bay v0.5 Demo`

## Facade Bay prefab contract

```text
PF_Window_Arch_01
├── FacadeBayDefinition
├── Visual
│   └── imported 3ds Max art
└── Cutter
    └── closed low-poly cutter mesh
```

Authoring convention:

- Pivot: bottom-center of the opening/bay.
- `+Y`: up.
- `+Z`: outward from facade.
- Cutter must be closed/manifold and deeper than the maximum wall thickness.
- Keep visual complexity separate from cutter complexity.
- Use `Floor Span > 1` for iwans or other features that occupy multiple floors.

## WallSpline gate-frame contract

A `WallSplineOpening` may reference a production gate/arch frame prefab. Keep its pivot at the bottom-center with `+Y` up. The frame is aligned to the wall tangent/outward basis at the requested distance; the actual wall gap is generated independently from the prefab and does not require Boolean CSG.

## Legacy opening prefab contract

```text
PF_Window_Arch_01
├── OpeningDefinition
├── Visual
└── Cutter
```

`OpeningDefinition` remains supported for the original opening workflow and the primary entrance compatibility path.

## Architectural module contract

```text
PF_Iwan_01
├── YazdModuleDefinition
├── Visual
└── Cutter             # optional
```

`YazdModuleDefinition` supports Iwan, Badgir, ArcadeBay, Dome, BarrelVault and generic module kinds. Facade modules use bottom-center / `+Z` outward orientation. Roof modules use bottom-center / `+Y` up.

## Non-destructive generation

`BuildingGenerator` remains the source of truth. Every Generate/Randomize pass deletes `__GeneratedBuilding` and rebuilds from source parameters and Seed. `Bake Copy` creates a detached accepted result and removes cutter/module metadata.

`Facade Bay Planner` is opt-in. When enabled, legacy random windows, legacy iwan placement and legacy courtyard arcade placement are temporarily bypassed while the base building is generated, then the reservation-based facade pass is applied.

`WallSpline` behaves similarly: every generation replaces `__GeneratedWallSpline`, while the source control points, openings, style and seed remain editable. `Bake Wall Copy` creates a detached accepted wall result.

## Current limitations

- Building/facade Boolean backend is still ProBuilder experimental CSG.
- Hashti/corridor geometry is a rule/placement prototype made from primitives, not a production interior kit.
- Main `BuildingGenerator` wall height is still uniform across the whole building.
- Facade Bay v0.5 targets straight wall segments produced by `BuildingGenerator`; its bay catalog is separate from WallSpline gate openings.
- WallSpline terrain conform uses sampled collider hit heights but does not rotate wall geometry to terrain normals.
- WallSpline buttresses, opening pillars and end caps are currently generated primitive geometry rather than prefab pools.
- Dedicated WallSpline niches are not implemented yet.
- Sample assets are intentionally primitive placeholders.
- v0.6 still needs compile, physics, SceneView and ProBuilder validation inside the target Unity Editor/project.

## Next targets

- WallSpline niche/recess system and prefab pools for buttresses, pillars and end caps.
- Direct SceneView gate-position handles.
- Real 3ds Max Yazd asset set.
- Modular hashti/corridor/interior connection kit.
- Stepped and multi-height building massing.
- Production domes, vaults and squinches.
- LOD/collider/lightmap bake stage.
