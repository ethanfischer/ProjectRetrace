// The visual for a RoomGate: a frosted, dimmed pane over a sealed doorway. It reads the
// camera's opaque colour buffer rather than rendering a colour of its own, so the room
// behind stays legible as "a room you cannot enter yet" instead of a wall. Blur and
// darkening are combined because either alone misreads: blur alone is glass, dark alone
// is a light switch. _Strength is driven per instance so the pane can fade away on the
// unlock round.
Shader "ProjectRetrace/GateVeil"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.05, 0.06, 0.09, 1)
        _Darken ("Darken", Range(0, 1)) = 0.55
        _BlurRadius ("Blur Radius (screen fraction)", Range(0, 0.05)) = 0.012
        _Strength ("Strength", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Darken;
                float _BlurRadius;
                float _Strength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionHCS);
                // Correct for aspect so the taps form a circle on screen, not an ellipse.
                float2 radius = _BlurRadius * float2(_ScreenParams.y / _ScreenParams.x, 1.0);

                // Two rings of eight plus the centre: enough to hide the opaque texture's
                // 2x downsample blockiness without a second pass.
                half3 sum = SampleSceneColor(uv);
                float weight = 1.0;
                [unroll]
                for (int ring = 1; ring <= 2; ring++)
                {
                    float r = ring * 0.5;
                    [unroll]
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * 0.785398 + ring * 0.3927;
                        float2 offset = float2(cos(angle), sin(angle)) * radius * r;
                        sum += SampleSceneColor(uv + offset);
                        weight += 1.0;
                    }
                }
                half3 blurred = sum / weight;
                half3 dimmed = lerp(blurred, _Tint.rgb, _Darken);
                return half4(dimmed, _Strength);
            }
            ENDHLSL
        }
    }
}
