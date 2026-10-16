# Custom First-Floor Footprint

`BuildingFootprint.CustomSpline` lets the house body start from an editable closed first-floor/base outline instead of only the Rectangle, L, U or Courtyard presets.

The result is still a **single generated SolidBuildingMesh**. Control points are authoring data only; they do not create modular wall GameObjects.

## Workflow

1. Select a `BuildingGenerator` using `Geometry Mode = SolidMesh`.
2. Click `Custom From Current Preset` to turn the current Rectangle/L/U outline into editable points, or click `Custom Rectangle` to start from a clean rectangle.
3. In Scene View, drag `P0`, `P1`, ... to reshape the building.
4. Hold **Shift + Left Click** on the generator ground plane to insert a new control point into the nearest footprint edge.
5. `Add Point` splits the current longest edge, which is more useful for a closed architectural plan than appending points at the end.
6. Use `Remove Last`, `Reverse`, `Center`, or `Reset Custom Rectangle` as needed.
7. Keep `Live Custom Footprint Preview` enabled for immediate rebuilding, or disable it and use `Generate` manually for heavier scenes.

The loop is always closed automatically between the last and first control point.

A cyan Scene View arrow labelled `Front +Z` shows the local front/street direction used by facade classification and primary-entrance selection.

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

Before generating, BuildingGen checks both the visible outline and the wall-thickness result:

- at least three unique points,
- usable polygon area,
- neighboring samples are not collapsed,
- the outer loop does not intersect itself,
- the inward wall-thickness offset has usable area,
- the inward offset does not self-intersect,
- tight concave corners do not push the inner wall loop outside the authored footprint.

This last stage matters because a plan may look valid as a 2D spline but still be impossible to build with the requested wall thickness.

Invalid footprints are shown as an Inspector error and generation is stopped before the generated hierarchy is replaced.

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

For strongly curved spline sections, use moderate subdivision values: each sampled segment is a locally planar facade anchor, so very dense sampling can make individual facade spans too short for doors or wide windows.

## Roof modules

Badgir, dome and vault placement samples candidate points inside the custom footprint rather than assuming a rectangular roof.

## Demo

Run:

`Tools > BuildingGen > Create Custom Footprint Demo`

Then select `YazdBuilding_Demo` and edit `P0...Pn` in Scene View.

## Current limitations

- Custom inner holes/courtyards are not yet supported in `CustomSpline` mode.
- Interior room partitions do not yet follow the custom plan.
- Upper floors currently inherit the same base footprint; per-floor/stepped footprint editing is a later massing stage.
- Smooth spline mode can create many short facade planning segments.
- Unity Editor compilation and ProBuilder CSG still need validation inside the target project because this repository has no Unity Editor CI runner.
