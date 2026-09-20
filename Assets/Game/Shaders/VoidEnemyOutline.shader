Shader "Boundary/Void Enemy Outline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (1.8, 0.01, 0.01, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.12)) = 0.025
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" "RenderType"="Transparent" }

        Pass
        {
            Name "Player Red Outline"
            Cull Front
            ZWrite Off
            ZTest [_ZTest]
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 expanded = input.positionOS.xyz + normalize(input.normalOS) * _OutlineWidth;
                output.positionHCS = TransformObjectToHClip(expanded);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
