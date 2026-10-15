# Solid Building Mesh Backend (v0.6)

`SolidMesh` is now the default `BuildingGenerator` geometry mode.

The building body is not assembled from rendered wall modules. BuildingGen generates one `MeshFilter` / `MeshRenderer` body and uses lightweight, non-rendered facade metadata only for placement.

## Generated hierarchy

```text
BuildingGenerator
└── __GeneratedBuilding
    ├── SolidBuildingMesh
    │   ├── MeshFilter
    │   ├── MeshRenderer
    │   └── MeshCollider
    │
    ├── FacadeSurfaces
    │   ├── Facade_00          metadata only
    │   ├── Facade_01          metadata only
    │   └── CourtyardFacade_*  metadata only
    │
    ├── ArchitecturalDetails
    │   ├── Openings
    │   └── RoofModules
    │
    └── FacadeBays             when the bay planner is enabled
```

`FacadeSurfaces` contain no wall renderers. They only provide width, orientation, role and host-body references to the opening planner.

## Mesh construction

The v0.6 body generator supports:

- Rectangle
- L-shaped footprint
- U-shaped footprint
- Central courtyard

Walls are generated as polygon wall shells with the configured physical `Wall Thickness`.

For non-courtyard footprints:

```text
outer footprint
    ↓ inward polygon offset by Wall Thickness
wall shell
    +
roof slab
    =
one generated Mesh
```

For courtyard footprints:

```text
outer wall shell
+
courtyard-facing wall shell
+
roof ring
=
one generated Mesh
```

The courtyard remains open.

The generated mesh uses two submeshes:

1. wall surfaces
2. roof surfaces

so `YazdBuildingStyle.Wall Material` and `Roof Material` remain separate.

## Openings

Door/window art can still be authored in 3ds Max as prefabs:

```text
PF_Window_Arch_01
├── OpeningDefinition
├── Visual
└── Cutter
```

The visible prefab remains separate art, but the cutter subtracts from `SolidBuildingMesh`.

The same applies to reservation-planner facade bays:

```text
PF_Iwan_01
├── FacadeBayDefinition
├── Visual
└── Cutter
```

In `SolidMesh` mode every Boolean operation targets the same building mesh instead of a separate wall object.

This means the main architecture stays non-modular while high-detail DCC-authored doors, windows, iwans and decorative pieces remain replaceable assets.

## Facade Bay Planner

The v0.5 reservation planner is preserved.

`Window / Door / Iwan / Arcade / Niche / Blank` bays use `SolidFacadeSurface` metadata for placement and collision/reservation rules.

No facade surface creates geometry.

## Legacy backend

`Building Geometry Mode` provides:

- `SolidMesh` — current production direction and default.
- `LegacySegmented` — older per-wall prototype retained temporarily for comparison and regression testing.

The legacy backend should be removed only after the solid CSG path has been validated with real production assets.

## Demo

Run:

`Tools > BuildingGen > Create Solid Yazd Demo`

This reuses the existing primitive Yazd sample assets but regenerates the house using the `SolidMesh` backend.

With ProBuilder Experimental CSG enabled, the sample arch door/window cutters subtract directly from the single generated body.

## Current v0.6 limitations

The solid backend intentionally focuses first on the body/opening architecture.

- hashti and interior room partitions are not generated yet in `SolidMesh` mode;
- facade Iwan/Arcade behavior should use the `FacadeBayCatalog` rather than the older module placement path;
- parapet geometry has not yet been merged into the solid mesh;
- lightmap UV2 generation is still a later bake-stage task;
- ProBuilder experimental CSG still needs validation against the final 3ds Max cutter assets.

## Next solid-mesh steps

1. exact interior room / hashti plan generation;
2. stepped and multi-height massing as one mesh;
3. parapet and roof-edge geometry directly in the mesh builder;
4. stable production Boolean backend abstraction;
5. vertex-color wear/dirt channels;
6. UV2 / LOD / collider bake pipeline;
7. convert `WallSpline` from chunk primitives to one continuous generated wall mesh.
