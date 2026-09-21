Shader "hologram"
{
    Properties
    {
        _Color ("Interaction Tint", Color) = (1,1,1,1)
        _ActiveFill ("Active Fill", Color) = (0.08,0.62,0.88,1)
        _ActiveEdge ("Active Edge", Color) = (0.65,1,1,1)
        _InactiveFill ("Inactive Fill", Color) = (0.38,0.40,0.72,1)
        _InactiveEdge ("Inactive Edge", Color) = (0.78,0.76,1,1)
        [HideInInspector] _RingActive ("Ring Active", Float) = 1
        [HideInInspector] _WedgeShape ("Wedge Shape", Vector) = (0.25,0.15,0.02,0.3927)
        [HideInInspector] _WedgeCenter ("Wedge Center", Float) = 0
        [HideInInspector] _ZTest ("Depth Test", Float) = 4
        [HideInInspector] _ZWrite ("Depth Write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Hologram"
            Tags { "LightMode"="SRPDefaultUnlit" }
            // The generated wedge winding is already inverted.
            Cull Back
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _ActiveFill, _ActiveEdge, _InactiveFill, _InactiveEdge;
                float4 _WedgeShape;
                float _RingActive, _WedgeCenter;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                float radial : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.radial = input.color.a;
                return output;
            }
            float Line(float distance, float pixelSize)
            {
                return 1.0 - smoothstep(0.65, 1.65, abs(distance) / max(pixelSize, 0.00001));
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float angle = atan2(input.positionOS.y, input.positionOS.x);
                float delta = atan2(sin(angle - _WedgeCenter), cos(angle - _WedgeCenter));
                float angular = delta / max(2.0 * _WedgeShape.w, 0.00001) + 0.5;
                float radial = input.radial;
                float depth = input.positionOS.z / max(_WedgeShape.z, 0.00001);
                float2 direction = float2(cos(angle), sin(angle));
                bool face = abs(input.normalOS.z) > 0.5;
                bool wall = abs(dot(input.normalOS.xy, direction)) > 0.5;
                // Face: arc/radius. Curved wall: arc/depth. End cap: radius/depth.
                float2 surface = face ? float2(angular, radial)
                    : (wall ? float2(angular, depth) : float2(radial, depth));
                float2 pixel = max(fwidth(surface), 0.00001);
                float2 boundary = min(surface, 1.0 - surface);
                float edge = max(Line(boundary.x, pixel.x), Line(boundary.y, pixel.y));
                float detail = 0;
                if (face)
                {
                    float inset = Line(radial - 0.88, pixel.y);
                    float tickPhase = (delta + _WedgeShape.w) / radians(10.0);
                    float tick = Line(frac(tickPhase + 0.5) - 0.5, fwidth(tickPhase));
                    detail = max(inset * 0.45, tick * step(0.88, radial) * 0.65);
                }
                half4 fill = lerp(_InactiveFill, _ActiveFill, saturate(_RingActive));
                half4 ink = lerp(_InactiveEdge, _ActiveEdge, saturate(_RingActive));
                return half4(lerp(fill.rgb, ink.rgb, max(edge, detail)) * _Color.rgb, 1);
            }
            ENDHLSL
        }
    }
}
