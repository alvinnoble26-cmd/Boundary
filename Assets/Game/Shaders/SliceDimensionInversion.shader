Shader "Boundary/Slice Dimension Inversion"
{
    Properties
    {
        _Tint ("Nebula Tint", Color) = (0.22, 0.06, 0.65, 1)
        _Distortion ("Distortion", Range(0, 0.04)) = 0.012
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+40" "RenderType"="Transparent" }
        Pass
        {
            Name "DimensionInversion"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float4 color : COLOR;
                float2 uv : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Distortion;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float movingNoise = sin(input.uv.x * 47.0 + _Time.y * 5.0) *
                    cos(input.uv.x * 19.0 - _Time.y * 7.0);
                float2 offset = float2(movingNoise, sin(input.uv.x * 31.0 + _Time.y * 4.0)) *
                    _Distortion;
                half3 scene = SampleSceneColor(screenUV + offset);
                half3 inverted = 1.0h - scene;
                float across = saturate(1.0 - abs(input.uv.y * 2.0 - 1.0));
                float softRibbon = smoothstep(0.02, 0.24, across);
                float star = pow(saturate(sin(input.uv.x * 173.0 + 1.4) *
                    sin(input.uv.x * 97.0 - 0.7)), 28.0);
                half3 nebula = _Tint.rgb * (0.20h + 0.35h * movingNoise) +
                    half3(0.08h, 0.28h, 0.75h) * (0.28h + 0.25h * movingNoise);
                half3 dimension = lerp(inverted, inverted * 0.52h + nebula, 0.42h) + star;
                return half4(dimension, softRibbon * input.color.a * 0.96h);
            }
            ENDHLSL
        }
    }
}
