using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace sc.splines.spawner.runtime
{
    [BurstCompile]
    public struct SplineCache : IJobParallelFor
    {
        private int sampleCount;
        private NativeSpline spline;
        private bool closed;

        private NativeArray<SplinePoint> points;
        public NativeArray<SplinePoint> Points => points;

        public void Execute(int i)
        {
            float t = i / (float)sampleCount;
            
            if (closed && i == sampleCount) t = 0f;

            //Clamp to ensure a valid tangent is sampled
            t = math.clamp(t, 0.0001f, 0.9999f);
            
            spline.Evaluate(t, out float3 position, out float3 tangent, out float3 up);
            
            points[i] = new SplinePoint(position, tangent, up);
        }

        public void Dispose()
        {
            if (points.IsCreated) points.Dispose();
        }

        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
        public static void Sample(ref NativeArray<SplinePoint> points, float t, out SplinePoint output)
        {
            int pointCount = points.Length;
            if (pointCount == 0)
            {
                output = default;
                return;
            }
            
            float i = t * (float)(pointCount - 1);

            int prev = (int)math.floor(i);
            int next = (int)math.floor(i + 1);

            if (next >= pointCount)
            {
                output = points[pointCount - 1];
                return;
            }

            if (prev < 0)
            {
                output = points[0];
                return;
            }

            SplinePoint a = points[prev];
            SplinePoint b = points[next];
            SplinePoint.Lerp(in a, in b, i - prev, out var result);
            
            output = result;
        }

        public void Create(NativeSpline nativeSpline, float sampleDistance, DistributionSettings.Accuracy accuracy)
        {
            float searchIntervalScalar = 1;
            searchIntervalScalar = accuracy switch
            {
                DistributionSettings.Accuracy.BestPerformance => 6f,
                DistributionSettings.Accuracy.PreferPerformance => 4f,
                DistributionSettings.Accuracy.Balanced => 3f,
                DistributionSettings.Accuracy.PreferAccuracy => 1f,
                DistributionSettings.Accuracy.HighestAccuracy => 0.5f,
                _ => searchIntervalScalar
            };
            
            this.spline = nativeSpline;
            this.closed = nativeSpline.Closed;
            
            sampleCount = Mathf.CeilToInt(nativeSpline.GetLength() / (sampleDistance * searchIntervalScalar));
            sampleCount = Mathf.Max(4, sampleCount);
            
            //Add 1 extra position for closed splines to store the closing point
            int arraySize = closed ? sampleCount + 1 : sampleCount;
            points = new NativeArray<SplinePoint>(arraySize, Allocator.Persistent);

            //Schedule jobs for all positions (including the extra closing point for closed splines)
            int jobCount = points.Length;

            JobHandle jobHandle = this.Schedule(jobCount, 64);
            jobHandle.Complete();
        }
    }
}