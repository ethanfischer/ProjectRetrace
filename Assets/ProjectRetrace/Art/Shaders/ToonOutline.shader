// Screen-space ink lines for the Full Screen Pass renderer feature. Edges come from the
// depth and normals buffers rather than an inverted hull, because the pack's hard-edged
// low-poly meshes have split normals: an extruded hull tears open at every crease, while a
// depth/normal edge finds exactly those creases and every silhouette in one pass.
Shader "ProjectRetrace/ToonOutline"
{
    // No material properties: every knob is a global set by LookSettings from
    // retrace-config.json, so the outline material asset never needs editing.
    Properties { }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ToonOutline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half _ToonOutlineEnabled;
            half4 _ToonOutlineColor;
            float _ToonOutlineThickness;
            float _ToonOutlineDepthThreshold;
            float _ToonOutlineNormalThreshold;
            float _ToonOutlineFadeDistance;

            bool Configured() { return _ToonOutlineThickness > 0; }
            half4 OutlineColor() { return Configured() ? _ToonOutlineColor : half4(0.08, 0.06, 0.1, 1); }
            float Thickness() { return Configured() ? _ToonOutlineThickness : 1.2; }
            float DepthThreshold() { return Configured() ? _ToonOutlineDepthThreshold : 0.2; }
            float NormalThreshold() { return Configured() ? _ToonOutlineNormalThreshold : 0.4; }
            float FadeDistance() { return Configured() ? _ToonOutlineFadeDistance : 30; }

            float LinearDepthAt(float2 uv)
            {
                return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (Configured() && _ToonOutlineEnabled < 0.5)
                    return scene;
                float2 texel = Thickness() / _ScreenParams.xy;

                // Roberts cross: two diagonal differences catch edges in every direction with
                // four taps, which is cheap enough to run every frame at full resolution.
                float2 uvA = uv + float2(-texel.x, -texel.y);
                float2 uvB = uv + float2( texel.x,  texel.y);
                float2 uvC = uv + float2( texel.x, -texel.y);
                float2 uvD = uv + float2(-texel.x,  texel.y);

                float dA = LinearDepthAt(uvA);
                float dB = LinearDepthAt(uvB);
                float dC = LinearDepthAt(uvC);
                float dD = LinearDepthAt(uvD);
                float depthCentre = LinearDepthAt(uv);

                float depthEdge = sqrt(pow(dA - dB, 2) + pow(dC - dD, 2));
                // Scale the threshold with distance: a fixed metre value would draw seams
                // across every distant floor tile and miss nothing nearby.
                float depthCutoff = DepthThreshold() * (1 + depthCentre * 0.5);
                float depthLine = step(depthCutoff, depthEdge);

                float3 nA = SampleSceneNormals(uvA);
                float3 nB = SampleSceneNormals(uvB);
                float3 nC = SampleSceneNormals(uvC);
                float3 nD = SampleSceneNormals(uvD);
                float normalEdge = sqrt(dot(nA - nB, nA - nB) + dot(nC - nD, nC - nD));
                float normalLine = step(NormalThreshold(), normalEdge);

                float line_ = max(depthLine, normalLine);
                // Lines thin out with distance so far walls stay clean instead of crawling.
                half4 ink = OutlineColor();
                line_ *= saturate(1 - depthCentre / FadeDistance());
                line_ *= ink.a;
                return half4(lerp(scene.rgb, ink.rgb, line_), scene.a);
            }
            ENDHLSL
        }
    }
}
