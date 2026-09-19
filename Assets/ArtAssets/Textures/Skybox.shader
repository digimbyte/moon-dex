Shader "Custom/Skydome Gradient (URP - NoDither)"
{
    Properties
    {
        _TopColor    ("Top Color", Color)    = (0.20, 0.35, 0.70, 1)
        _BottomColor ("Bottom Color", Color) = (0.02, 0.02, 0.05, 1)

        _Exponent      ("Exponent", Range(0.2, 8)) = 1.6
        _HorizonOffset ("Horizon Offset", Range(-1, 1)) = 0.0
        _HorizonScale  ("Horizon Scale", Range(0.1, 4)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Background"
            "RenderType"="Opaque"
        }

        Pass
        {
            Name "SkydomeGradient_NoDither"
            Cull Front
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                half  _Exponent;
                half  _HorizonOffset;
                half  _HorizonScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3  normalWS    : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS    = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // camera-locked sphere: normalWS is effectively view direction
                half ny = i.normalWS.y * 0.5h + 0.5h;
                half t  = saturate((ny + _HorizonOffset) * _HorizonScale);
                t = pow(t, max(_Exponent, 0.0001h));

                half3 rgb = lerp(_BottomColor.rgb, _TopColor.rgb, t);
                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }
}
