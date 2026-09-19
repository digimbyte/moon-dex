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
using Alignment = sc.splines.spawner.runtime.DistributionSettings.OnCurve.Alignment;

namespace sc.splines.spawner.runtime
{
    [BurstCompile]
    public struct SpawnOnCurve : IJob
    {
        //Input
        private NativeSpline spline;
        private NativeArray<SplinePoint> splinePoints;
        private float splineLength;
        private float4x4 splineTransform;
        
        [ReadOnly]
        private NativeList<PrefabData> prefabData;

        private bool useInstanceCount;
        private float instanceCount;
        
        //Settings
        private DistributionSettings.OnCurve.SpacingMode spacingMode;
        private float spacing;
        private float2 spacingMinMax;
        private float2 trimming;
        private DistributionSettings.RotateToFitAxis rotateToFitAxis;

        private int lanes;
        private bool skipCenter;
        private float width;
        private DistributionSettings.OnCurve.Alignment alignment;
        private bool alignDirection;
        
        //Output
        [WriteOnly]
        public NativeList<SpawnPoint> spawnPoints;

        private float totalChanceWeights;
        Random random;
        
        public SpawnOnCurve(NativeSpline targetSpline, NativeArray<SplinePoint> splinePoints, float4x4 localToWorld, DistributionSettings distributionSettings, NativeList<PrefabData> prefabData, ref NativeList<SpawnPoint> spawnPoints)
        {
            DistributionSettings.OnCurve settings = distributionSettings.onCurve;
            this.spline = targetSpline;
            this.splinePoints = splinePoints;
            this.splineTransform = localToWorld;
            this.splineLength = spline.GetLength();
            
            this.prefabData = prefabData;
            this.spawnPoints = spawnPoints;

            totalChanceWeights = SplineFunctions.CalculateProbabilitySum(prefabData);

            this.spacingMode = settings.spacingMode;
            this.spacing = Mathf.Max(-5, settings.spacing);
            this.spacingMinMax.x = Mathf.Max(-5f, settings.spacingMinMax.x);
            this.spacingMinMax.y = Mathf.Max(settings.spacingMinMax.x, settings.spacingMinMax.y);
            this.trimming = settings.startEndTrimming;

            this.rotateToFitAxis = settings.rotateToFitAxis;
            
            this.lanes = settings.lanes;
            this.width = settings.width;
            this.alignment = settings.alignment;
            this.skipCenter = settings.skipCenter;
            this.alignDirection = settings.alignDirection;
            
            useInstanceCount = settings.instanceCountMode == DistributionSettings.InstanceCountMode.Specific;
            instanceCount = Mathf.Max(1, settings.instanceCount);

            random = new Random(distributionSettings.GetSeed());
        }

        
        public void Execute()
        {
            float trimLength = (trimming.x + trimming.y);
            if(trimLength > 0) splineLength -= trimLength;
            
            if (splineLength < 1f) return;
            
            //T-values of the trimming
            float2 trimRange = new float2((trimming.x / splineLength), 1f - (trimming.y / splineLength));
            
            float distanceTraveled = 0f;
            float m_spacing = spacing;
            
            //Instance count mode
            if(useInstanceCount) m_spacing = splineLength / (float)instanceCount;

            //Set a starting value, otherwise an infinite loop may occur if only 1 prefab is used with a very low probability.
            float lastOffset = 0f;
            float lengthAlongSpline = 0;
            
            while (distanceTraveled <= splineLength)
            {
                float t = distanceTraveled / this.splineLength;
                float remainingLength = (splineLength - distanceTraveled);
                
                t = math.clamp(t, 0.00001f, 0.99999f); //Ensure a tangent can always be derived
                //Remap normalized (0-1) t-range to trimmed range
                t = math.lerp(trimRange.x, trimRange.y, t);

                float m_width = width;
                if (alignment == Alignment.Center) m_width *= 0.5f;
                m_width = math.max(0.1f, m_width);
    
                for (int l = 0; l < lanes; l++)
                {
                    //0/1
                    float laneT = l / (float)(lanes-1);
                    //-1/+1
                    if (alignment == Alignment.Center)
                    {
                        laneT = laneT * 2f - 1f;
                    }
                    else if (alignment == Alignment.Left)
                    {
                        laneT = -laneT;
                    }
                    if (lanes == 1)
                    {
                        m_width = 0;
                        laneT = 1f;
                    }
                    
                    float laneOffset = (laneT * m_width);
                    if(lanes >= 2 && skipCenter && Mathf.Abs(laneOffset) < 0.02f) continue;

                    bool flip = laneOffset > 0 && alignDirection && lanes > 1;
                    
                    //Stable per instance
                    float r = random.NextFloat(0f, 1f);

                    int prefabIndex = SplineFunctions.GetRandomPrefabIndex(r, totalChanceWeights, prefabData);

                    if (prefabIndex >= 0)
                    {
                        PrefabData prefab = this.prefabData[prefabIndex];

                        if (!useInstanceCount && spacingMode == DistributionSettings.OnCurve.SpacingMode.RandomBetween)
                        {
                            m_spacing = random.NextFloat(spacingMinMax.x, spacingMinMax.y);
                        }

                        lengthAlongSpline = prefab.GetObjectLength() + m_spacing;
                        //In this case, spacing becomes irrelevant. Incorperating the spacing would yield fewer instances than specified
                        if (useInstanceCount) lengthAlongSpline = m_spacing;

                        //Current prefab no longer fits on the spline, end here
                        if (lengthAlongSpline > remainingLength) break;

                        float offset = 0;
                        /*
                        if (prefab.pivot == SplineSpawner.SpawnableObject.Pivot.Back)
                        {
                            offset += -(prefab.GetObjectBoundsMin());
                        }
                        else if (prefab.pivot == SplineSpawner.SpawnableObject.Pivot.Center)
                        {
                            offset += -prefab.GetForwardPivotOffset();
                        }
                        */

                        float offsetT = (offset / splineLength);
                        t += offsetT;

                        //Spline sampling
                        //spline.Evaluate(math.clamp(t, 0.00001f, 0.99999f), out float3 position, out float3 tangent, out float3 up);

                        SplineCache.Sample(ref splinePoints, t, out SplinePoint splinePoint);
                        float3 position = splinePoint.position;
                        
                        float3 tangent = splinePoint.tangent;
                        float3 up = splinePoint.up;
                        float3 forward = math.normalize(tangent);
                        float3 right = math.normalize(math.cross(forward, up));
                        position += right * laneOffset;
                        
                        //position += forward * (prefab.GetObjectLength() * 0.5f);

                        SpawnPoint spawnPoint = new SpawnPoint
                        {
                            isValid = true,
                            prefabIndex = prefabIndex,
                            position = position,
                            rotation = quaternion.identity,
                            pivotOffset = prefab.GetPivotOffset(),
                            scale = prefab.gameObjectScale
                        };

                        float stride = (lengthAlongSpline / splineLength);

                        //Calculate the turning factor
                        float3 currentTangentXZ = tangent;
                        float3 nextTangentXZ = spline.EvaluateTangent(t + stride);

                        currentTangentXZ = math.normalize(currentTangentXZ);
                        nextTangentXZ = math.normalize(nextTangentXZ);
                        float3 cross = (math.cross(currentTangentXZ, nextTangentXZ));

                        spawnPoint.context = new SpawnPoint.Context
                        {
                            t = t,
                            //Scale the noise, otherwise minute frequency values become the norm
                            noiseCoord = new float2(t * splineLength + r, t * splineLength),
                            splineLength = splineLength,
                            random01 = r,
                            curvature = math.abs(math.degrees(math.acos(cross.y)) - 90f),
                            position = position,
                            forward = forward,
                            right = right,
                            up = up,
                            invertDistance = false
                        };
                        
                        if (flip) spawnPoint.context.forward = -spawnPoint.context.forward;

                        spawnPoint.rotation = quaternion.LookRotationSafe(spawnPoint.context.forward, spawnPoint.context.up);
                        //WIP
                        //spawnPoint.rotation = prefab.GetForwardRotation(forward, right, up);

                        //Calculate the rotation needed to position the object so that both its tip and end sit on the spline
                        //Particularly useful for fences or other long objects
                        if (rotateToFitAxis != DistributionSettings.RotateToFitAxis.Disabled)
                        {
                            float3 startPosition = position;

                            float endT = t + (prefab.GetObjectLength() / splineLength);
                            float3 endPosition = spline.EvaluatePosition(endT);
                            endPosition += right * laneOffset;
                            
                            //High accuracy
                            //SplineUtility.GetNearestPoint(spline, endPosition, out endPosition, out var _, SplineUtility.PickResolutionDefault, 1);

                            float3 delta = endPosition - startPosition;
                            //Calculate direction from start to end position
                            float3 direction = math.normalize(delta);
                            if (flip) direction = -direction;
                            
                            spawnPoint.context.forward = direction;

                            //Full rotation (all axis)
                            quaternion rotation = quaternion.LookRotationSafe(direction, up);

                            if (rotateToFitAxis == DistributionSettings.RotateToFitAxis.Y)
                            {
                                rotation = SplineFunctions.LockRotationAngle(quaternion.identity, rotation,
                                    new bool3(true, false, true));
                            }

                            //spawnPoint.rotation = math.mul(spawnPoint.rotation, rotation);
                            spawnPoint.rotation = rotation;
                        }

                        spawnPoints.Add(spawnPoint);
                    }
                }
                
                //LengthAlongSpline represents the length of the last spawned object. If no object was picked (low probability) an empty space is correctly created.
                lastOffset = math.max(0.02f, lengthAlongSpline);
                distanceTraveled += lastOffset;
            }
            
            //Debug.Log($"Spawned {spawnPoints.Length} {lengthAlongSpline}m objects on a {splineLength}m spline with {(splineLength - distanceTraveled)}m space left");
        }
        
        private float3 TransformToWorld(float3 direction)
        {
            return math.normalize(math.mul(splineTransform, new float4(direction, 0.0f)).xyz);
        }

        private float RotatedLengthOnSpline(quaternion rotation, float3 scale, float3 forward, float3 right, float3 up)
        {
            //Rotate the local axes
            float3 globalAxisX = math.mul(rotation, right * scale.x);
            float3 globalAxisY = math.mul(rotation, up * scale.y);
            float3 globalAxisZ = math.mul(rotation, forward * scale.z);
                
            float length = math.abs(globalAxisX.z) + math.abs(globalAxisY.z) + math.abs(globalAxisZ.z);

            return length;
        }

        private quaternion RotateForward(SplineSpawner.SpawnableObject.ForwardDirection direction, float3 up)
        {
            float3 forward;

            switch (direction)
            {
                case SplineSpawner.SpawnableObject.ForwardDirection.PositiveX:
                    forward = math.right();
                    break;
                case SplineSpawner.SpawnableObject.ForwardDirection.NegativeX:
                    forward = -math.right();
                    break;
                case SplineSpawner.SpawnableObject.ForwardDirection.PositiveY:
                    forward = math.up();
                    break;
                case SplineSpawner.SpawnableObject.ForwardDirection.NegativeY:
                    forward = -math.up();
                    break;
                case SplineSpawner.SpawnableObject.ForwardDirection.PositiveZ:
                    forward = math.forward();
                    break;
                case SplineSpawner.SpawnableObject.ForwardDirection.NegativeZ:
                    forward = -math.forward();
                    break;
                default:
                    forward = math.forward(); // Default to +Z
                    break;
            }

            // Use up vector as Y+ unless forward is also Y+/- to avoid zero-cross issue
            //float3 up = math.abs(math.dot(forward, math.up())) > 0.99f ? math.forward() : math.up();
            return quaternion.LookRotationSafe(forward, up);
        }

        public void Dispose()
        {
            //spawnPoints.Dispose();
        }
    }
}