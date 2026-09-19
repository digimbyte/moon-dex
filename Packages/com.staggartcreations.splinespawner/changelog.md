1.1.5 (December 14th 2025)

Changed:
- Object pools are now cleared when disabling the Spline Spawner component, or when closing its inspector.
- Prefab section now shows a warning if a model asset is used (as this cause the entire file to be instantiated).

Fixed:
- Remnant objects from object pools being unintentionally saved if the "Hide Instances" option was disabled.
- Instance Count Mode: Specific. Spacing will still being incorperated, resulting in a different instance count than specified.

1.1.4 (December 8th 2025)

Added:
- On Curve distribution: lanes feature, allowing to spawn objects in multiple parallel lines.
- Inside Area distribution: "Allow Overflow" option, if enabled objects can overlap the Spline's curve.
- Inside Area distribution: "Tightness" parameter, if set to 1 objects are placed in a hex pattern.
- Inspector UI: Warning message when nothing was spawned using the current configuration.

Changed:
- Mask layer names can now be edited in Project Settings->Tags and Layers.
- Inside Area distribution, improved overlap detection method (existing spawns will change slightly).

Fixed:
- Offset modifier: Z-axis noise offset not taking effect.

1.1.3 (December 1st 2025)

Added:
- Spline Spawner Mask inspector UI: list of all spawners being affected by it.

Changed:
- Distribution performance increased by 200-400% through key optimizations.

Fixed:
- Masking layers names reverting to default when closing the editor
- Masks being able to trigger respawns during the building process
- Script error when using a Mask with invalid splines (shorter than 1m or 0 knots)
- On Curve Distribution: Rotate To Fit "Y" option not correctly levelling the rotation of objects.
- Rotate To Fit: Not correctly orienting the objects if a spacing was used

1.1.2 (October 16th 2025)

Added:
- Startup behaviour options: None, Respawn and Respawn Randomized.
- Mask: Update On Enable option, for runtime spawning functionality

Changed:
- Toggling masks now only triggers respawns if they are selected in Edit mode.

Fixed:
- Issue with spawners as child objects of other spawners being misinterpreted as orphaned objects.

1.1.1 (October 6th 2025)

Fixed:
- Prefab spawning warning not refreshing when Root transform changes
- Spawning not taking effect for prefabbed spawners, even if the Root transform was an external (non-prefab) object.

1.1.0 (September 24th 2025)

Added:
- Look At modifier, rotates objects towards a specific target.
- SetSplineContainer() functions to correctly handle changing the spline container of a Spline Spawner component.
- Improved support for usage in prefabs. Respawning is now disallowed for prefab instances, but possible for prefab source objects.
- Radial distribution: Min Radius, Angle Range and Height Offset parameters
- Rotation modifier: Option to lock X/Y/Z rotation axis.
- Offset modifier: Noise-based offsetting.
- Radial distribution, added "Border Accuracy" performance balancing option.

Changed:
- The minimum required version of the Mathematics package is now 1.3.2 (will auto-upgrade).
- The 'splineContainer' field on the Spline Spawner and Spline Spawner Mask components can no longer be set manually. Use the SetSplineContainer() function instead.
- Spline Instance Container, polished inspector UI and added tooltips to options.
- Implemented safety check when attempting to spawn prefabs with one or more Spline Spawner Mask components.
- Optimized performance regarding masks. They'll now only respawn spawners physically intersecting with the mask.
- Radial distribution, changing the 'Center' parameter no longer clips objects from the opposite end.

Fixed:
- Leak Detection console messages cropping up in some cases.
- Grid distribution, default object rotation not aligned with row/column direction.
- Script error when no spawning Root transform was set.
- Duplicating a Spline Spawner without a Root transform assigned would not duplicate the objects correctly.
- Spawning prefab variants didn't correctly link the objects to the source prefab.
- Scaled child objects in prefabs did not correctly contribute to the calculated size of the prefab.

1.0.0 (August 11th 2025)
Note: Updating from the preview version is not supported, delete it before importing.
All component settings and configurations will be lost due to data structure changes.

Added:
- Masking functionality, other splines with a SplineSpawnerMask can define masking layers. Spline Spawners can filter by them.
- UI, drag & drop box for adding new prefabs
- Snap to Colliders modifier, added "Direction" option. Making it possible to snap down from the Spline curve.
- Height Filter modifier, restricts spawning within the configured height range.
- On Curve distribution, spacing can now be set between a minimum and maximum value.
- Scale, functionality to scale over a curve
- On Knot distribution, options to select which knots to spawn on (eg. first & last, specific range, etc).

Changed:
- Inspector UI has been overhauled and polished
- Distribution settings seed can now be randomized
- Distribution modes now have performance/accuracy preference options
- Prefabs array is now a List, for easier insertion and deletion of objects
- Performance of spawning inside a Spline's area has improved 400-1800%

Fixed:
- Duplicating a Spline Spawner component would inadvertently link the original and copy's modifier stack.
- Modifier stack not being saved to prefabs or supporting prefab overrides.
- Modifier stack not supporting undo/redo operations

== Preview version ==
1.0.2

Added:
- Scale: option to invert distance-based scaling

1.0.1

Changed:
- Randomization can now also be applied using alternating values
- Rotation modifier now has a min/max rotation field

1.0.0
Initial testing release