// Spline Spawner by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//  • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//  • Uploading this file to a public GitHub repository will subject it to an automated DMCA takedown request.

using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace sc.splines.spawner.runtime
{
    [Serializable]
    public class DistributionSettings
    {
        [Tooltip("Random seed for distribution calculations, ensuring reproducible randomness.")]
        [Min(0)]
        public int seed;

        public void SetSeed(int value)
        {
            seed = value;
        }

        public void RandomizeSeed()
        {
            SetSeed(Random.Range(0,99999));
        }
        
        public uint GetSeed()
        {
            return (uint)seed+1;
        }
        
        [Tooltip("Selects the spatial distribution method for instances.")]
        public enum DistributionMode
        {
            [Tooltip("Distribute instances along the spline curve.")]
            OnCurve,
            [Tooltip("Distribute instances uniformly inside a defined area.")]
            InsideArea,
            [Tooltip("Place instances exactly on spline knot points.")]
            OnKnots,
            [Tooltip("Arrange instances radially from a center point.")]
            Radial,
            [Tooltip("Arrange instances in a grid pattern.")]
            Grid
        }

        [Tooltip("Distribution method to use.")]
        public DistributionMode mode = DistributionMode.OnCurve;

        [Serializable]
        public class OnCurve
        {
            [Tooltip("Controls automatic or explicit instance count when distributing on curve.")]
            public InstanceCountMode instanceCountMode = InstanceCountMode.Auto;

            [Tooltip("Number of instances to spawn if Instance Count Mode is set to Specific.")]
            [Min(1)]
            public int instanceCount = 10;
            
            [Tooltip("Defines how spacing between instances on the curve is calculated.")]
            public enum SpacingMode
            {
                [Tooltip("Spacing is constant and exact.")]
                Exact,
                [Tooltip("Spacing is randomized between minimum and maximum values.")]
                RandomBetween
            }

            [Tooltip("Method used to space instances along the curve.")]
            public SpacingMode spacingMode;

            [Tooltip("Exact spacing between instances when using Exact SpacingMode.")]
            public float spacing = 1f;

            [Tooltip("Minimum and maximum spacing used when using RandomBetween SpacingMode.")]
            public Vector2 spacingMinMax = new Vector2(3f, 6f);

            [Tooltip("Amount to trim from start and end of the curve for spawning instances.")]
            public Vector2 startEndTrimming = new Vector2(0f, 0f);
            
            [Tooltip("If enabled, rotates objects at their center to align perfectly with the curve's axis.")]
            public RotateToFitAxis rotateToFitAxis = RotateToFitAxis.Disabled;

            [Min(1)]
            public int lanes = 1;
            [Tooltip("Enable to avoid creating a lane on the source Spline")]
            public bool skipCenter = true;
            public float width = 10f;
            public enum Alignment
            {
                Left,
                Center,
                Right
            }
            public Alignment alignment = Alignment.Center;
            
            [Tooltip("Enable to flip the orientation on the left side, so that the \"Right\" vector sampled from the spline actually points outwards")]
            public bool alignDirection = true;
        }
        [Tooltip("Settings for distributing instances along the spline curve.")]
        public OnCurve onCurve = new OnCurve();

        [Serializable]
        public class InsideArea
        {
            [Tooltip("Spacing between instances within the area. Value used is never lower than half the size of the smallest prefab.")]
            [Min(0.2f)]
            public float spacing = 5f;
            [Tooltip("Controls how tightly objects are packed together.")]
            [Range(0f, 1f)]
            public float tightness = 0f;
            
            [Tooltip("Padding inside the area's boundaries where no instances are spawned.")]
            [Min(0f)]
            public float padding = 0f;
            [Tooltip("If enabled, objects can reach outside the area's boundaries (including the padding).")]
            public bool allowOverflow;
            
            [Tooltip("Controls accuracy of overlap checking of a spawn point against nearby spawn points. Lower accuracy may result in a few empty gaps.")]
            public Accuracy overlapAccuracy = Accuracy.Balanced;

            [Tooltip(
                "Controls accuracy of checking if a spawn point is still within the spline curve. Lower accuracy may result in spawn points just outside of the curve.")]
            public Accuracy borderAccuracy = Accuracy.Balanced;
        }
        [Tooltip("Settings for uniform distribution inside a bounded area.")]
        public InsideArea insideArea = new InsideArea();
        
        [Serializable]
        public class OnKnots
        {
            public enum Selection
            {
                All,
                [InspectorName("First only")]
                FirstOnly,
                [InspectorName("Last only")]
                LastOnly,
                FirstAndLast,
                AllExceptFirstAndLast,
                [InspectorName("Custom Range")]
                Range
            }
            public Selection selection;
            public bool linearOnly;
            [Tooltip("If enabled, the rotation of the last knot is turned around 180 degrees")]
            public bool mirrorLastRotation;
            public Vector2Int range = new Vector2Int(0, 1);

            public enum LinkedKnotFilter
            {
                None,
                Exclude,
                Exclusive
            }
            public LinkedKnotFilter linkedKnotFilter;
        }
        public OnKnots onKnots = new OnKnots();

        [Serializable]
        public class Radial
        {
            [Tooltip("Spacing radius between instances in radial distribution.")]
            [Min(0f)]
            public float minRadialSpacing = 0f;
            [Tooltip("Spacing radius between instances in radial distribution.")]
            public float radialSpacing = 3f;

            [Modifier.MinMaxSlider(0f, 360f)]
            public Vector2 angleRange = new Vector2(0f, 360f);
            
            [Tooltip("Angular offset in degrees applied between instances.")]
            public float offset = 0;
            [Tooltip("Max height of objects as it gradually increases with every ring")]
            public float heightOffset = 0;

            [Tooltip("General spacing multiplier for radial arrangement.")]
            public float spacing = 5;

            [Tooltip("Center point of the radial distribution.")]
            public Vector2 center;

            [Tooltip("Controls accuracy of checking if a spawn point is still within the spline curve. Lower accuracy may result in spawn points just outside of the curve.")]
            public Accuracy borderAccuracy = Accuracy.Balanced;
        }
        [Tooltip("Settings for radial pattern object distribution.")]
        public Radial radial = new Radial();

        [Serializable]
        public class Grid
        {
            [Tooltip("Enable to create rows in the grid.")]
            public bool createRows = true;

            [Tooltip("Spacing between rows.")]
            [Min(0.5f)]
            public float rowSpacing = 5f;

            [Tooltip("Spacing between instances on rows.")]
            [Min(0.25f)]
            public float spacingOnRows = 3f;
            
            [Tooltip("Enable to create columns in the grid.")]
            public bool createColumns;

            [Tooltip("Spacing between columns.")]
            [Min(0.5f)]
            public float columnSpacing = 5f;

            [Tooltip("Spacing between instances on columns.")]
            [Min(0.25f)]
            public float spacingOnColumns = 3f;

            [Tooltip("Margin applied around the grid area.")]
            [Min(0f)]
            public float margin;

            [Tooltip("Rotation angle applied to the entire grid in degrees.")]
            [Range(0f, 360f)]
            public float angle = 0f;

            [Tooltip("Minimum length required for rows/columns to be created.")]
            [Min(0f)]
            public float minimumLength = 5f;
            
            [Tooltip("More precise but computationally expensive support for concave spline shapes.")]
            public bool concaveSupport = true;

            [Tooltip("Accuracy level for grid calculations; higher accuracy requires more compute time.")]
            public Accuracy accuracy = Accuracy.Balanced;
        }
        [Tooltip("Settings for placing instances in a grid formation.")]
        public Grid grid = new Grid();
        
        [Tooltip("Controls how the instance count is determined.")]
        public enum InstanceCountMode
        {
            [Tooltip("Instance count is automatically calculated.")]
            Auto,
            [Tooltip("Instance count is explicitly specified.")]
            Specific
        }

        [Tooltip("Defines how instances are rotated to align with curve axes.")]
        public enum RotateToFitAxis
        {
            [Tooltip("Rotation to fit is disabled.")]
            Disabled,

            [Tooltip("Rotate around the center on the Y axis to fit the curve.")]
            Y,

            [Tooltip("Rotate on all axes to fit the curve.")]
            AllAxis
        }

        [Tooltip("Accuracy level of calculations affecting performance and precision.")]
        public enum Accuracy
        {
            [Tooltip("Optimized for best performance, lowest accuracy.")]
            BestPerformance,
            [Tooltip("Optimized for improved performance, lower accuracy.")]
            PreferPerformance,
            [Tooltip("Balanced between performance and accuracy.")]
            Balanced,
            PreferAccuracy,
            [Tooltip("Highest accuracy with potentially higher computational cost.")]
            HighestAccuracy,
        }
    }
}