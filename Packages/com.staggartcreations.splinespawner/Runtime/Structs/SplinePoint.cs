using Unity.Burst;
using Unity.Mathematics;

namespace sc.splines.spawner.runtime
{
    [BurstCompile]
    public struct SplinePoint
    {
        public float3 position;
        public float3 tangent;
        public float3 up;

        public SplinePoint(float3 position, float3 tangent, float3 up)
        {
            this.position = position;
            this.tangent = tangent;
            this.up = up;
        }

        [BurstCompile]
        public static void Lerp(in SplinePoint a, in SplinePoint b, float t, out SplinePoint result)
        {
            result = new SplinePoint(
                math.lerp(a.position, b.position, t),
                math.lerp(a.tangent, b.tangent, t),
                math.lerp(a.up, b.up, t)
            );
        }

        public float3 CalculateRightVector(bool useUp = true)
        {
            return math.cross(math.normalize(tangent), useUp ? up : math.up());
        }
    }
}