Shader "ProjectRetrace/DevGrid"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.16, 0.17, 0.18, 1)
        _LineColor ("Line Color", Color) = (0.45, 0.48, 0.5, 1)
        _MajorLineColor ("Major Line Color", Color) = (0.75, 0.55, 0.2, 1)
        _CellSize ("Cell Size (m)", Float) = 1
        _MajorEvery ("Major Line Every N Cells", Float) = 5
        _LineWidth ("Line Width", Range(0.5, 4)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _LineColor;
                half4 _MajorLineColor;
                float _CellSize;
                float _MajorEvery;
                float _LineWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            // Anti-aliased line mask for a repeating grid at the given spacing.
            float GridMask(float2 worldXZ, float spacing, float width)
            {
                float2 coords = worldXZ / spacing;
                float2 derivative = fwidth(coords);
                float2 grid = abs(frac(coords - 0.5) - 0.5) / (derivative * width);
                return 1.0 - saturate(min(grid.x, grid.y));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 xz = input.positionWS.xz;
                float minor = GridMask(xz, _CellSize, _LineWidth);
                float major = GridMask(xz, _CellSize * _MajorEvery, _LineWidth * 1.5);

                half4 color = lerp(_BaseColor, _LineColor, minor);
                color = lerp(color, _MajorLineColor, major);
                return color;
            }
            ENDHLSL
        }
    }
}
