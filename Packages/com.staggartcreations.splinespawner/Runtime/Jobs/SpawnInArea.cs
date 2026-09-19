// Spline Spawner by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//  • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//  • Uploading this file to a public GitHub repository will subject it to an automated DMCA takedown request.

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Random = Unity.Mathematics.Random;

namespace sc.splines.spawner.runtime
{
    [BurstCompile(FloatPrecision.Medium, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance, CompileSynchronously = true)]
    public struct SpawnInArea : IJob
    {
        //Input
        private NativeSpline spline;
        private NativeArray<SplinePoint> splinePoints;
        private float splineLength;
        
        private float centerheight;
        private NativeBounds bounds;

        [ReadOnly] private NativeList<PrefabData> prefabData;

        //Output
        [WriteOnly] public NativeList<SpawnPoint> spawnPoints;

        private float totalChanceWeights;

        private float spawnRadius;
        private float spawnRadiusSqr;
        private float maxRadius;
        private bool useAnnulus;
        private float areaPadding;
        private readonly bool allowOverflow;

        private Random random;
        private NativeArray<float> searchAngles;
        
        //Poisson
        private NativeList<float3> samples;
        private NativeList<float3> points;
        private NativeArray<int> grid;
        private int2 gridResolution;
        private float cellSize;

        private DistributionSettings.Accuracy overlapAccuracy;
        private DistributionSettings.Accuracy borderAccuracy;
        private int searchAttempts;
        private float searchIntervalScalar;

        public SpawnInArea(NativeSpline targetSpline, NativeArray<SplinePoint> splinePoints, NativeBounds bounds,
            DistributionSettings distributionSettings, NativeList<PrefabData> prefabData,
            ref NativeList<SpawnPoint> spawnPoints)
        {
            DistributionSettings.InsideArea settings = distributionSettings.insideArea;
            
            this.spline = targetSpline;
            this.splinePoints = splinePoints;
            this.splineLength = spline.GetLength();

            this.allowOverflow = settings.allowOverflow;
            this.overlapAccuracy = settings.overlapAccuracy;
            this.borderAccuracy = settings.borderAccuracy;
            
            searchAttempts = 1;
            searchIntervalScalar = 1f;
            //Determines in how many different directions neighboring cells are searched for occupancy
            searchAttempts = overlapAccuracy switch
            {
                DistributionSettings.Accuracy.BestPerformance => 3,
                DistributionSettings.Accuracy.PreferPerformance => 5,
                DistributionSettings.Accuracy.Balanced => 6,
                DistributionSettings.Accuracy.PreferAccuracy => 8,
                DistributionSettings.Accuracy.HighestAccuracy => 32,
                _ => searchAttempts
            };

            //Spline curve analysis interval distance multiplier
            searchIntervalScalar = borderAccuracy switch
            {
                DistributionSettings.Accuracy.BestPerformance => 10f,
                DistributionSettings.Accuracy.PreferPerformance => 4f,
                DistributionSettings.Accuracy.Balanced => 2f,
                DistributionSettings.Accuracy.PreferAccuracy => 1f,
                DistributionSettings.Accuracy.HighestAccuracy => 0.5f,
                _ => searchIntervalScalar
            };

            float minSize = 0.25f;

            totalChanceWeights = 0;
            for (int i = 0; i < prefabData.Length; i++)
            {
                if (prefabData[i].probability > 0)
                {
                    minSize = math.max(minSize, prefabData[i].GetRadiusXZ());
                }

                //Also sum the probabilities
                totalChanceWeights += prefabData[i].probability;
            }
            
            //Ensure that the spacing is never smaller than the smallest object
            this.spawnRadius = Mathf.Max(minSize * 0.5f, settings.spacing);
            this.spawnRadiusSqr = this.spawnRadius * this.spawnRadius;
            this.maxRadius = math.lerp(spawnRadius * 2f, spawnRadius * 1.05f, settings.tightness);
            useAnnulus = settings.tightness < 1f;
            
            this.areaPadding = settings.padding;
            if (allowOverflow == false)
            {
                //Pad the area by the radius of the largest object
                this.areaPadding += this.spawnRadius;
            }

            bounds.Expand(-areaPadding);
            this.bounds = bounds;
            this.centerheight = this.bounds.center.y;

            this.prefabData = prefabData;
            this.spawnPoints = spawnPoints;

            random = new Random(distributionSettings.GetSeed());

            //Setup
            {
                samples = new NativeList<float3>(4096, Allocator.TempJob);
                points = new NativeList<float3>(4096, Allocator.TempJob);
                
                //Grid setup
                cellSize = spawnRadius / Mathf.Sqrt(2);
                gridResolution = new int2(
                    (int)math.ceil(this.bounds.size.x / cellSize),
                    (int)math.ceil(this.bounds.size.z / cellSize)
                );

                grid = new NativeArray<int>(gridResolution.x * gridResolution.y, Allocator.TempJob);

                //Debug.Log($"Spawn radius: {spawnRadius}. Grid resolution: {gridResolution.x}x{gridResolution.y}. Grid scale: {width}. Cell count:{grid.Length}");
            }

            //Use a set of fixed angles rather than random, to ensure optimal coverage and avoid under- or oversampling
            searchAngles = GetSearchAngles(searchAttempts);
        }

        public static NativeArray<float> GetSearchAngles(int samples, Allocator allocator = Allocator.TempJob)
        {
            NativeArray<float> searchAngles = new NativeArray<float>(samples, allocator);

            float angleStep = Mathf.CeilToInt(360f / samples);
            for (int i = 0; i < samples; i++)
            {
                searchAngles[i] = i * angleStep;
            }

            return searchAngles;
        }

        public void Execute()
        {
            if (splineLength < 1f) return;
            
            //First point, center of bounds
            AddValidPoint(new float3(bounds.center.x, 0f, bounds.center.z));

            //Sampling loop
            while (samples.Length > 0)
            {
                int i = random.NextInt(0, samples.Length);
                var sampleCenter = samples[i];
                bool valid = false;

                for (int s = 0; s < searchAttempts; s++)
                {
                    float3 sample = GetSamplePointOnAnnulus(sampleCenter, s);

                    if (IsPointValid(sample))
                    {
                        AddValidPoint(sample);

                        float intervalDistance = spawnRadius * searchIntervalScalar;

                        //Check spline area
                        bool insideSpline = spline.IsInsideSpline(splinePoints, splineLength, sample, intervalDistance, areaPadding, out float3 nearestPosition);

                        /*
                        //Move points outside of the curve (but within acceptable distance) to the nearest point on the curve
                        if (insideSpline == false)
                        {
                            float distToCurve = math.distancesq(sample, nearestPosition);

                            if (distToCurve < spawnRadiusSqr)
                            {
                                //Straight snap to curve
                                sample = nearestPosition;
                                insideSpline = true;
                            }
                        }
                        */
                        
                        //insideSpline = true;
                        if (insideSpline)
                        {
                            float r = random.NextFloat(0, 1f);

                            int prefabIndex = SplineFunctions.GetRandomPrefabIndex(r, totalChanceWeights, prefabData, math.distance(nearestPosition, sample));

                            if (prefabIndex >= 0)
                            {
                                SpawnPoint spawnPoint = CreateSpawnPoint(sample, r, prefabIndex);
                                spawnPoint.context.position = nearestPosition;

                                spawnPoints.Add(spawnPoint);
                            }
                        }

                        valid = true;
                        break;
                    }
                }

                if (!valid)
                {
                    samples.RemoveAtSwapBack(i);
                }
            }

        }

        [BurstCompile]
        private void AddValidPoint(float3 position)
        {
            samples.Add(position);
            points.Add(position);
            SetGridCell(position, points.Length - 1);
        }

        [BurstCompile]
        private bool IsPointValid(float3 point)
        {
            if (!SplineFunctions.IsInsideBounds(point, bounds.min, bounds.max))
                return false;

            int2 gridPos = PositionToGridCoord(point);

            int xMin = math.max(gridPos.x - 2, 0);
            int xMax = math.min(gridPos.x + 2, gridResolution.x - 1);

            int yMin = math.max(gridPos.y - 2, 0);
            int yMax = math.min(gridPos.y + 2, gridResolution.y - 1);

            //Check cells around current grid cell (3x3)
            for (int x = xMin; x <= xMax; x++)
            {
                for (int y = yMin; y <= yMax; y++)
                {
                    int index = (y * gridResolution.x + x);
                    int gridValue = grid[index];

                    if (gridValue > 0 && InsideRadius(point, points[gridValue]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        [BurstCompile]
        private int2 PositionToGridCoord(float3 pos)
        {
            int x = (int)((pos.x - bounds.min.x) / cellSize);
            int y = (int)((pos.z - bounds.min.z) / cellSize);
            
            return new int2(x, y);
        }

        [BurstCompile]
        private void SetGridCell(float3 point, int pointIndex)
        {
            int2 cell = PositionToGridCoord(point);
            int index = cell.y * gridResolution.x + cell.x;

            #if UNITY_EDITOR || UNITY_ENABLE_CHECKS
            if (index > grid.Length)
            {
                Debug.LogError($"Grid Cell Index ({index}) out of bounds ({bounds.size.x}x{bounds.size.z})");
                return;
            }
            #endif

            grid[index] = pointIndex;
        }
            
        [BurstCompile]
        private float3 GetSamplePointOnAnnulus(float3 center, int index)
        {
            float angle = math.radians(searchAngles[index]);

            //Test using random angle
            //angle = random.NextFloat(0f, math.PI * 2f);
            
            //Allowing the maximum radius to be scaled to increase packing efficiency
            float distance = random.NextFloat(spawnRadius, maxRadius);
            if(useAnnulus == false) distance = maxRadius;
            
            float3 direction = new float3(math.cos(angle), 0f, math.sin(angle));
            
            return center + (direction * distance);
        }
        
        //Check if position falls within annulus
        [BurstCompile]
        private bool InsideRadius(float3 center, float3 position)
        {
            return math.distancesq(center, position) <= spawnRadiusSqr;
        }

        [BurstCompile]
        private SpawnPoint CreateSpawnPoint(float3 newPoint, float noise, int prefabIndex)
        {
            PrefabData data = prefabData[prefabIndex];

            newPoint.y = centerheight;
            
            SpawnPoint spawnPoint = new SpawnPoint
            {
                isValid = true,
                //spawnPoint.position = math.mul(splineTransform, new float4(newPoint, 1.0f)).xyz;
                prefabIndex = prefabIndex,
                position = newPoint,
                rotation = quaternion.identity,
                scale = data.gameObjectScale
            };

            spawnPoint.context = new SpawnPoint.Context()
            {
                noiseCoord = new float2(spawnPoint.position.x * 0.1f, spawnPoint.position.z * 0.1f),
                splineLength = splineLength,
                t = noise,
                curvature = 0,
                random01 = noise,
                forward = math.forward(),
                right = math.right(),
                up = math.up(),
                position = spawnPoint.position,
                invertDistance = true
            };

            return spawnPoint;
        }

        public void Dispose()
        {
            samples.Dispose();
            points.Dispose();
            grid.Dispose();
            searchAngles.Dispose();
        }
    }
}