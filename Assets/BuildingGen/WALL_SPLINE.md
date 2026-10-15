# Wall Spline Tool

`WallSpline` is an editor-time procedural wall tool for alleys, courtyards, garden walls and ruined adobe walls.

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
- top/crown prefab scattering.

### Construction modes

`SimpleMud`
: Regular adobe/mud wall made from deterministic chunks.

`RuinedMud`
: Per-chunk height/thickness variation, missing sections, base jitter and yaw variation. This is useful for old or partially collapsed Yazd walls.

`DecoratedMud`
: Regular wall intended to use the top-prefab pool for brick caps, crenellations, wooden details, clay pieces or other repeated crown elements.

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

The generator aligns each prefab to the spline tangent and supports spawn probability, lateral jitter, yaw jitter and scale variation.

Production top prefabs can be authored in 3ds Max and used directly. Their root pivot should be near the bottom center of the object, with local `+Z` representing its forward direction along the wall.

## Demo

Run:

`Tools > BuildingGen > Create Wall Spline Demo`

It creates three editable samples:

- `WallSpline_SimpleDemo`
- `WallSpline_RuinedDemo`
- `WallSpline_DecoratedDemo`

and creates three reusable style assets plus simple crown prefabs under:

`Assets/BuildingGen/Samples/Generated`

## Seed and baking

All damage and prefab variation is deterministic from `Seed`.

Use:

- `Randomize` for a new variation,
- `Seed >` for deterministic iteration,
- `Bake Wall Copy` when a result is accepted.

The source spline remains editable after baking.
