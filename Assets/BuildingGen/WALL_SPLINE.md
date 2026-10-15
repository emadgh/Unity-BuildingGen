# Wall Spline Tool

`WallSpline` is an editor-time procedural wall tool for alleys, courtyards, garden walls and ruined adobe walls. v0.6 adds precise gate/gap openings, structural details, broken wall-top variation and terrain conforming.

## Create a wall

Use:

`Tools > BuildingGen > Create Wall Spline`

Assign a `WallSplineStyle` asset, then edit the path in the Scene view.

### Scene controls

- Drag `P0`, `P1`, ... handles to reshape the wall.
- Hold **Shift** and left-click on the wall object's ground plane to append a point.
- Use **Add Point**, **Remove Last**, and **Reverse** in the Inspector.
- `Polyline` is suited to angular alleys and old urban walls.
- `SmoothCatmullRom` creates a smooth path through the control points.
- `Closed Loop` can be used for enclosed yards or perimeter walls.

Moving a point regenerates the wall non-destructively under `__GeneratedWallSpline`.

## Gate / gap openings — v0.6

A `WallSplineOpening` is positioned by **Distance**, measured in meters along the sampled wall path from the beginning of the spline.

Each opening has:

- `Distance` — center position along the path,
- `Width` — clear horizontal span,
- `Clear Height` — clear vertical opening height,
- optional `Frame Prefab`,
- optional edge pillars,
- pillar width / extra depth.

Opening start and end positions are inserted as exact wall-generation breakpoints. The generator therefore does not remove an arbitrary whole chunk around a gate. It splits the wall exactly at the opening edges.

If `Clear Height` is lower than the wall height, the remaining wall strip above the opening becomes a simple lintel/over-door wall section. If the clear height reaches the full wall height, the opening becomes a complete gap.

`Distance` is clamped to the actual sampled path length, including the Catmull-Rom interpolation length.

### Gate frame prefab convention

Production gate and arch frames can be modeled in 3ds Max and assigned directly to `Frame Prefab`.

Recommended root convention:

- pivot: bottom-center,
- `+Y`: up,
- local horizontal axis follows the wall,
- local `+Z`: facade/outward direction.

The sample uses a primitive rectangular frame only as a placeholder. Production arch geometry remains in the prefab.

### Opening edge pillars

Enable `Generate Edge Pillars` to place simple structural pillars at both opening boundaries. These are useful as placeholders for thick adobe gate jambs. Production pillar-prefab pools are a later extension.

## Terrain conform — v0.6

Enable `Conform To Terrain` on `WallSpline` to project sampled path points downward onto scene colliders before wall geometry is generated.

Settings:

- `Terrain Layers` — collider layers accepted by the raycast,
- `Terrain Raycast Height` — ray origin height above each spline point,
- `Terrain Raycast Depth` — maximum distance below the ray origin,
- `Terrain Vertical Offset` — optional offset above/below the hit point.

For production scenes, use a dedicated ground/terrain layer instead of `Everything`. This prevents the ray from snapping the wall to unrelated props or building colliders.

Terrain conform changes generated geometry only; it does not rewrite the original control points.

## Wall styles

Create styles through:

`Assets > Create > BuildingGen > Wall Spline Style`

A style controls:

- wall material,
- wall height,
- wall thickness,
- chunk length,
- construction mode,
- ruined-wall variation,
- missing chunks,
- broken top profile,
- buttresses,
- open-wall end caps,
- top/crown prefab scattering.

### Construction modes

`SimpleMud`
: Regular adobe/mud wall made from deterministic chunks.

`RuinedMud`
: Per-chunk height/thickness variation, missing sections, base jitter and yaw variation. This is useful for old or partially collapsed Yazd walls.

`DecoratedMud`
: Regular wall intended to use the top-prefab pool for brick caps, crenellations, wooden details, clay pieces or other repeated crown elements.

## Broken top profile — v0.6

Enable `Generate Broken Top Profile` to randomly lower selected wall intervals while retaining a continuous wall base.

Controls:

- `Broken Top Chance`,
- `Broken Top Drop Range`.

This is separate from `Missing Chunk Chance`: broken top produces an eroded/irregular wall silhouette, while missing chunks can remove complete ruined-wall sections.

## Buttresses — v0.6

Enable `Generate Buttresses` to place structural projections repeatedly along the spline.

Controls include:

- spacing,
- spawn chance,
- width,
- extra depth,
- height scale,
- side: `Right`, `Left`, or `Both`.

Buttress candidates are skipped when they intersect a gate opening.

## End caps — v0.6

For non-closed splines, `Generate End Caps` creates enlarged terminal wall pieces at both path ends. These are useful for old garden walls and alley walls whose exposed ends should not look like a raw procedural cut.

Closed loops do not generate end caps.

## Top prefab pool

Each `WallSplineStyle` can contain weighted top prefabs.

Example:

```text
WallStyle_YazdGarden
├── Wall Material = Adobe_01
├── Wall Height = 2.6
├── Top Spacing = 1.2
└── Top Prefabs
    ├── PF_BrickCap_A      weight 4
    ├── PF_BrickCap_B      weight 2
    ├── PF_BrokenCap       weight 1
    └── PF_WoodBeamEnd     weight 0.5
```

The generator aligns each prefab to the spline tangent and supports spawn probability, lateral jitter, yaw jitter and scale variation. v0.6 skips crown-prefab candidates that overlap gate openings.

Production top prefabs can be authored in 3ds Max and used directly. Their root pivot should be near the bottom center of the object, with local `+Z` representing its forward direction along the wall.

## Demos

Basic v0.4 wall examples:

`Tools > BuildingGen > Create Wall Spline Demo`

These create:

- `WallSpline_SimpleDemo`,
- `WallSpline_RuinedDemo`,
- `WallSpline_DecoratedDemo`.

Advanced v0.6 example:

`Tools > BuildingGen > Create Wall Spline v0.6 Advanced Demo`

The advanced demo creates:

- a smooth Yazd-style ruined wall,
- stepped ground colliders,
- terrain conforming,
- one precise gate opening,
- a simple gate-frame prefab,
- opening edge pillars,
- repeated buttresses,
- broken top variation,
- start/end caps.

Advanced sample assets are written under:

`Assets/BuildingGen/Samples/WallSplineV06`

## Seed and baking

Damage, broken-top selection, buttresses and top-prefab variation are deterministic from `Seed`.

Use:

- `Randomize` for a new variation,
- `Seed >` for deterministic iteration,
- `Bake Wall Copy` when a result is accepted.

The source spline remains editable after baking.

## Current v0.6 limitations / next wall targets

- Wall niches are not yet a dedicated WallSpline feature.
- Buttresses, opening pillars and end caps are currently primitive generated geometry; production prefab pools can replace them later.
- Terrain conform follows sampled hit heights but does not rotate the wall to terrain normals.
- Gate openings are distance-driven; direct SceneView dragging of gate centers is not implemented yet.
- Final compile/physics behavior must still be validated inside the target Unity Editor/project.
