# Unity BuildingGen — Yazd Architecture

Editor-time procedural architecture tools for Unity, focused on traditional Yazd / central-Iran urban fabric.

## Current direction — v0.7

The building body is **not modular wall geometry**. `BuildingGenerator` defaults to `SolidMesh`, which produces one procedural `MeshFilter` / `MeshRenderer` body with real wall thickness.

v0.7 adds a custom editable first-floor/base footprint alongside the existing Rectangle, L, U and Courtyard presets.

```text
YazdBuilding
└── __GeneratedBuilding
    ├── SolidBuildingMesh       one generated building mesh
    ├── FacadeSurfaces          metadata-only placement anchors
    ├── ArchitecturalDetails
    │   ├── Openings
    │   └── RoofModules
    └── FacadeBays
```

`FacadeSurfaces` do not render walls. They only describe facade width/orientation/role so door, window, iwan, niche and arcade rules can place their art and cutters against the single body mesh.

### Solid building support

- deterministic Seed generation;
- `Rectangle`, `LShape`, `UShape`, `Courtyard`, and `CustomSpline` footprints;
- editable closed first-floor footprint handles in Scene View;
- Polyline or smooth Catmull-Rom custom footprint interpolation;
- preset-to-custom conversion for Rectangle/L/U starting shapes;
- self-intersection and wall-offset validation for custom footprints;
- hollow wall-shell construction with physical wall thickness;
- one generated mesh for walls + roof;
- separate wall and roof submeshes/materials;
- open central courtyard with courtyard-facing wall shell and roof ring in the dedicated Courtyard preset;
- optional `MeshCollider` on the generated body;
- metadata-only logical facade surfaces;
- door/window visual prefabs with closed cutter meshes;
- reservation-based facade bays: `Window / Door / Iwan / Arcade / Niche / Blank`;
- every opening/FacadeBay Boolean targets the same `SolidBuildingMesh`;
- Badgir and roof-form detail prefabs can remain separate visual assets;
- non-destructive regenerate / randomize / seed navigation / bake workflow.

The older per-wall prototype is still available temporarily through:

`Building Geometry Mode = LegacySegmented`

It exists only as a comparison/fallback while the solid backend is tested with real production assets. `CustomSpline` always uses the SolidMesh path.

## Install into an existing Unity project

Copy:

`Assets/BuildingGen`

into your Unity project's `Assets` folder.

Install **ProBuilder** from Unity Package Manager. The current CSG backend uses ProBuilder experimental CSG. Enable:

`Edit > Preferences/Settings > ProBuilder > Experimental Features Enabled`

If CSG is unavailable, the generated solid body and visible prefab art still appear, but cutter meshes do not subtract from the building.

## Demos

### Solid building demo

Run:

`Tools > BuildingGen > Create Solid Yazd Demo`

This creates/reuses the primitive test assets and regenerates `YazdBuilding_Demo` through the one-mesh backend.

### Custom footprint demo

Run:

`Tools > BuildingGen > Create Custom Footprint Demo`

Select `YazdBuilding_Demo` and edit `P0...Pn` directly in Scene View. Hold **Shift + Left Click** on the generator ground plane to append points.

See `Assets/BuildingGen/CUSTOM_FOOTPRINT.md`.

### Facade bay demo

The reservation planner supports coherent, non-overlapping facade decisions for:

- windows,
- doors,
- iwans,
- arcade bays,
- niches,
- intentional blank bays.

In `SolidMesh` mode the planner uses logical facade anchors but all cuts are applied to the single building body.

### Wall spline demos

Run:

`Tools > BuildingGen > Create Wall Spline Demo`

or:

`Tools > BuildingGen > Create Advanced Wall Spline Demo`

The spline-wall tool currently supports:

- Polyline and smooth Catmull-Rom paths;
- Scene View control-point editing;
- terrain conform;
- gate/opening markers by distance along the spline;
- simple, ruined and decorated adobe wall styles;
- crown/top prefab pools;
- pillars;
- buttresses;
- niches;
- broken end caps;
- deterministic Seed variation;
- bake copy.

See `Assets/BuildingGen/WALL_SPLINE.md`.

## 3ds Max opening authoring

Doors/windows remain DCC-authored art even though the building body itself is procedural.

Recommended prefab structure:

```text
PF_Window_Arch_01
├── OpeningDefinition
├── Visual
│   └── imported Max art
└── Cutter
    └── closed low-poly cutter mesh
```

Convention:

- root pivot = bottom-center of the architectural opening;
- local `+Y` = up;
- local `+Z` = outward from the facade;
- `Visual` can be high-detail art;
- `Cutter` should be simple, closed/manifold, and deep enough to cross the full supported wall thickness.

The visual mesh remains separate. The cutter modifies the generated solid body.

For reservation-planner elements use `FacadeBayDefinition` with the same pivot/orientation convention.

## Solid mesh construction

For a normal or custom footprint:

```text
outer footprint
    ↓
inward offset by Wall Thickness
    ↓
closed exterior/interior wall shell
    +
roof top + interior ceiling
    ↓
one generated Mesh
```

For a courtyard building:

```text
outer wall shell
+
courtyard-facing wall shell
+
open courtyard
+
roof ring / ceiling ring
=
one generated Mesh
```

The interior is not filled with a giant block; it is an architectural shell with real wall thickness and an enclosed roof volume. This keeps door/window cutter depth local to the wall rather than requiring cutters to cross the entire building.

See `Assets/BuildingGen/SOLID_MESH.md` for implementation details.

## Footprints

### Rectangle

Standard enclosed footprint.

### L Shape

Concave L-shaped mass controlled by `Wing Width`.

### U Shape

Concave U-shaped mass with `U Recess Depth`.

### Courtyard

Outer rectangular footprint plus a true open central courtyard. `Courtyard Width` and `Courtyard Depth` control the void.

### Custom Spline

Editable closed first-floor/base footprint. Use `Polyline` for architectural walls or `SmoothCatmullRom` for intentionally curved masses.

Scene View controls:

- drag `P0`, `P1`, ...;
- Shift + Left Click to append;
- Add Point / Remove Last / Reverse / Center;
- optional live regeneration while moving handles.

Rectangle/L/U presets can be converted to CustomSpline and then edited freely. The current v0.7 custom mode controls the outer perimeter; arbitrary custom inner courtyard/hole splines are deferred until the outer-loop workflow is validated in the target Unity project.

## Non-destructive workflow

`BuildingGenerator` is the source of truth.

Each Generate/Randomize pass removes `__GeneratedBuilding` and reconstructs the clean source mesh before applying cutters again. Boolean operations are therefore not repeatedly applied to an already-damaged mesh.

Use **Bake Copy** after accepting a result. The baked copy duplicates the generated body mesh, removes cutter metadata, and removes logical facade anchors while preserving imported visual prefab meshes as shared assets.

## Architecture

```text
BuildingGenerator
    |
    +-- Geometry Mode
    |       +-- SolidMesh        default
    |       +-- LegacySegmented  temporary fallback
    |
    +-- Footprint
    |       +-- Rectangle / L / U / Courtyard
    |       +-- CustomSpline control points
    |
    +-- SolidBuildingMeshBuilder / CustomFootprintMeshBuilder
    |       +-- footprint sampling
    |       +-- wall thickness
    |       +-- wall shell
    |       +-- roof / ceiling
    |       +-- UV0 / vertex color initialization
    |
    +-- SolidFacadeSurface       metadata only
    |
    +-- opening / facade planner
    |       +-- Visual prefab
    |       +-- Cutter prefab
    |       +-- CSG on SolidBuildingMesh
    |
    +-- roof/detail placement
    |
    +-- bake
```

The CSG implementation remains isolated behind `ProBuilderBooleanBackend`, so a more robust production Boolean backend can replace it without changing the authoring contracts.

## Validation status

This repository is still a drop-in `Assets/BuildingGen` toolset rather than a complete Unity project with Editor CI.

The branch/repository receives static code and geometry-topology review, but final validation still requires the target Unity Editor to:

- compile the scripts;
- generate the preset and custom footprint types;
- edit custom handles in Scene View;
- run ProBuilder experimental CSG against real cutters;
- inspect normals/UVs/colliders;
- test Bake Copy and imported 3ds Max assets.

## Next production steps

- custom courtyard / inner-hole spline support;
- stepped / multi-height building massing while remaining one generated mesh;
- per-floor footprint variation above the first-floor/base spline;
- solid parapet and roof-edge geometry;
- room/hashti/interior-plan generation instead of primitive legacy guides;
- production Boolean backend evaluation;
- vertex-color channels for plaster wear, dirt and exposed adobe/brick;
- UV2 / lightmap bake stage;
- LOD and collider bake stage;
- convert the spline-wall body from chunk primitives to one continuous generated wall mesh.
