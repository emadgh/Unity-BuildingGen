# Wall Spline Tool

`WallSpline` is an editor-time procedural wall tool for alleys, courtyards, garden walls and ruined adobe walls.

## Create a wall

Use:

`Tools > BuildingGen > Create Wall Spline`

Assign a `WallSplineStyle` asset, then edit the path in Scene view.

### Scene controls

- Drag `P0`, `P1`, ... handles to reshape the wall.
- Hold **Shift** and left-click on the wall object's ground plane to append a point.
- Use **Add Point**, **Remove Last**, and **Reverse** in the Inspector.
- `Polyline` is suited to angular alleys and old urban walls.
- `SmoothCatmullRom` creates a smooth path through the control points.
- `Closed Loop` can be used for enclosed yards or perimeter walls.

Moving a point regenerates the wall non-destructively under `__GeneratedWallSpline`.

## Terrain conform

Enable `Conform To Terrain` to project the sampled spline down onto scene colliders.

Controls:

- `Terrain Layer Mask`
- `Terrain Ray Height`
- `Terrain Ray Distance`
- `Terrain Offset`

The generated chunks remain vertical. When the two ends of a chunk sit at different terrain heights, the chunk extends down to the lower point so the wall does not visibly float above the terrain.

`Conform + Generate` enables terrain conform and regenerates immediately.

## Gates and wall openings

`WallSpline.openings` stores explicit openings by distance along the spline.

Each opening contains:

- `Enabled`
- `Distance`
- `Width`
- optional visible gate / arch prefab
- vertical offset
- local Euler offset

The wall chunks are omitted across the opening interval. Crown pieces, pillars, buttresses and niches also avoid the reserved opening range.

Use **Add Gate Marker** to add a default marker at the middle of the current path. Red Scene-view guides show the reserved opening width.

Recommended gate-prefab convention:

- pivot at bottom-center of the opening,
- local `+Y` up,
- local `+Z` points to the selected decoration/outward side of the spline,
- local `+X` therefore runs along the wall.

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
- crown prefabs,
- pillars,
- buttresses,
- niches,
- end caps.

### Construction modes

`SimpleMud`
: Regular adobe/mud wall made from deterministic chunks.

`RuinedMud`
: Per-chunk height/thickness variation, missing sections, base jitter and yaw variation. Useful for old or partially collapsed Yazd walls.

`DecoratedMud`
: Regular wall intended to use crown/detail prefab pools.

## Top / crown prefab pool

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

Top prefabs align their local `+Z` along the wall tangent.

## Pillars, buttresses and niches

All detail systems use weighted prefab pools and deterministic spacing/chance rules.

`Pillars`
: Repeated vertical structural/decorative modules centered on the wall path.

`Buttresses`
: Repeated modules placed on the selected side of the wall. `Flip Decoration Side` swaps them to the opposite side.

`Niches`
: Surface details placed at a configurable height and slight offset from the wall surface.

For buttress/niche prefabs, use bottom-center pivot and local `+Z` pointing away from the wall surface.

## End caps

For open splines, optional start/end prefab pools can add broken termination pieces or finished wall ends. Closed loops do not generate end caps.

## Demos

Basic demo:

`Tools > BuildingGen > Create Wall Spline Demo`

Advanced demo:

`Tools > BuildingGen > Create Advanced Wall Spline Demo`

The advanced sample demonstrates:

- terrain conform on a sloped collider,
- a real gate opening interval,
- crown scattering,
- pillars,
- buttresses,
- niches,
- broken end caps.

Generated sample assets are stored under:

`Assets/BuildingGen/Samples/Generated`

## Seed and baking

All damage and prefab variation is deterministic from `Seed`.

Use:

- `Randomize` for a new variation,
- `Seed >` for deterministic iteration,
- `Bake Wall Copy` when a result is accepted.

The source spline remains editable after baking.
