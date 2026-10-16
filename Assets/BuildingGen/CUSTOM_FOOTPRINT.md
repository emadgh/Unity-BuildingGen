# Custom First-Floor Footprint

`BuildingFootprint.CustomSpline` lets the house body start from an editable closed first-floor/base outline instead of only the Rectangle, L, U or Courtyard presets.

The result is still a **single generated SolidBuildingMesh**. Control points are authoring data only; they do not create modular wall GameObjects.

## Workflow

1. Select a `BuildingGenerator` using `Geometry Mode = SolidMesh`.
2. Click `Custom From Current Preset` to turn the current Rectangle/L/U outline into editable points, or click `Custom Rectangle` to start from a clean rectangle.
3. In Scene View, drag `P0`, `P1`, ... to reshape the building.
4. Hold **Shift + Left Click** on the generator's local ground plane to append a new point.
5. Use `Add Point`, `Remove Last`, `Reverse`, or `Center` from the Inspector.
6. Keep `Live Custom Footprint Preview` enabled for immediate rebuilding, or disable it and use `Generate` manually for heavier scenes.

The loop is always closed automatically between the last and first control point.

## Interpolation

### Polyline

Recommended for architecture. Every control-point pair becomes a straight facade segment.

```text
P3 ---------- P2
|              \
|               P1
P4             /
 \             /
  P5 -------- P0
```

### Smooth Catmull-Rom

Samples a closed Catmull-Rom spline through the same control points. `Custom Footprint Subdivisions` controls sampling density.

Use this for organic or intentionally curved masses. Because sampled curved edges create many logical facade segments, Polyline is usually preferable for doors/windows and traditional Yazd buildings.

## Geometry validation

Before generating, BuildingGen checks:

- at least three unique points,
- usable polygon area,
- neighboring samples are not collapsed,
- the loop does not intersect itself,
- the inward wall-thickness offset remains valid.

Invalid footprints are shown as an Inspector error and generation is stopped instead of emitting broken topology.

## Presets remain available

The preset buttons are not removed:

- Rectangle
- L Shape
- U Shape
- Courtyard

They remain fast parametric modes. Rectangle/L/U can be converted into a custom footprint and then edited freely.

The current v0.7 custom spline controls the **outer building perimeter**. The existing `Courtyard` preset still owns the central courtyard-hole workflow. Arbitrary custom inner courtyard/hole splines are intentionally deferred until the custom outer-loop topology is validated in the target Unity project.

## Openings and facade planner

Every sampled outer edge becomes logical `SolidFacadeSurface` metadata. These surfaces do not render geometry; they only provide position, width and orientation for:

- legacy door/window pools,
- the reservation-based Facade Bay Planner,
- CSG cutters,
- entrance selection.

All cuts continue to target the one `SolidBuildingMesh`.

## Roof modules

Badgir, dome and vault placement samples candidate points inside the custom footprint rather than using a rectangular bounding footprint.

## Demo

Run:

`Tools > BuildingGen > Create Custom Footprint Demo`

Then select `YazdBuilding_Demo` and edit `P0...Pn` in Scene View.

## Current limitations

- Custom inner holes/courtyards are not yet supported in `CustomSpline` mode.
- Interior room partitions do not yet follow the custom plan.
- Smooth spline mode can create many short facade planning segments.
- Unity Editor compilation and ProBuilder CSG still need validation inside the target project because this repository has no Unity Editor CI runner.
