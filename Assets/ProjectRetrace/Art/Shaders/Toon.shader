// Cel-shaded replacement for URP/Lit on the house materials. Same atlas texture, but the
// main light is quantised into bands instead of a smooth Lambert falloff, so the flat-colour
// low-poly pack reads as deliberate illustration rather than under-lit PBR.
Shader "ProjectRetrace/Toon"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _RimColor ("Rim Color", Color) = (0, 0, 0, 0)
        _RimSize ("Rim Size", Range(0, 1)) = 0.25
        _SpecColor ("Specular Color", Color) = (0, 0, 0, 0)
        _SpecSize ("Specular Size", Range(0.01, 1)) = 0.1

        // Blend state as properties so one shader serves the opaque house and the fading
        // ghosts; a transparent material just flips these and its render queue.
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1

        // Inverted-hull ink line for smooth meshes (the sentry capsule). The house gets its
        // lines from the screen-space pass instead, which transparent objects never reach.
        [Toggle(_HULL_OUTLINE)] _HullOutline ("Hull Outline", Float) = 0
        _HullWidth ("Hull Width (m)", Range(0, 0.1)) = 0.03
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _RimColor;
            half _RimSize;
            half4 _SpecColor;
            half _SpecSize;
            half _Cutoff;
            half _HullWidth;
        CBUFFER_END

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

        // Banding and tint come from retrace-config.json via LookSettings, as globals, so a
        // player's tweak reaches every material at once and no .mat carries a stale copy.
        // _ToonSteps is 0 until something applies the config (edit mode with no sync, a scene
        // without LookSettings), and the fallbacks below are the config defaults.
        half _ToonFlat;
        half _ToonSteps;
        half _ToonBandSoftness;
        half4 _ToonShadowTint;
        half _ToonAmbientBoost;
        half _ToonOutlineEnabled;
        half4 _ToonOutlineColor;

        bool ToonConfigured() { return _ToonSteps >= 1; }
        half ToonSteps() { return ToonConfigured() ? _ToonSteps : 2; }
        half ToonBandSoftness() { return ToonConfigured() ? _ToonBandSoftness : 0.04; }
        half3 ToonShadowTint() { return ToonConfigured() ? _ToonShadowTint.rgb : half3(0.42, 0.40, 0.62); }
        half ToonAmbientBoost() { return ToonConfigured() ? _ToonAmbientBoost : 0.9; }
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = GetVertexNormalInputs(input.normalOS).normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            // Quantises a 0..1 lighting term into the configured number of bands. The ramp inside each band is
            // a narrow smoothstep rather than a hard floor so the band edges anti-alias
            // instead of shimmering as the camera moves.
            half Band(half x)
            {
                half steps = ToonSteps();
                half scaled = saturate(x) * steps;
                half cell = floor(scaled);
                half edge = smoothstep(1.0 - ToonBandSoftness(), 1.0, scaled - cell);
                return saturate((cell + edge) / steps);
            }

            half3 ShadeLight(Light light, half3 normalWS, half3 viewDirWS, out half band)
            {
                half ndl = dot(normalWS, light.direction);
                // Cast shadows get their own, wider smoothstep instead of going through Band:
                // the shadow map's filtered edge is only a few texels wide, and snapping it
                // with a hard floor turns every shadow border into a staircase.
                half shadow = smoothstep(0.25, 0.75, light.shadowAttenuation);
                band = Band(ndl * light.distanceAttenuation) * shadow;
                half3 halfDir = normalize(light.direction + viewDirWS);
                half spec = step(1.0 - _SpecSize, saturate(dot(normalWS, halfDir))) * (band > 0);
                return light.color * (band + spec * _SpecColor.rgb * _SpecColor.a);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                if (_ToonFlat > 0.5)
                    return half4(MixFog(albedo, input.fogFactor), alpha);
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionHCS);

                half mainBand;
                Light mainLight = GetMainLight(shadowCoord);
                half3 radiance = ShadeLight(mainLight, normalWS, viewDirWS, mainBand);

                half ambientOcclusion = 1;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    ambientOcclusion = GetScreenSpaceAmbientOcclusion(screenUV).indirectAmbientOcclusion;
                #endif
                half3 ambient = SampleSH(normalWS) * ToonAmbientBoost() * ambientOcclusion;

                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalWS = normalWS;
                    inputData.viewDirectionWS = viewDirWS;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        half extraBand;
                        radiance += ShadeLight(light, normalWS, viewDirWS, extraBand);
                        mainBand = max(mainBand, extraBand);
                    LIGHT_LOOP_END
                #endif

                // Unlit areas take the tint instead of going to black-times-ambient, which is
                // where most of the "cool shadow, warm light" illustration feel comes from.
                half unlit = 1 - saturate(mainBand);
                half3 color = albedo * (ambient + radiance + unlit * ToonShadowTint());

                half rim = 1 - saturate(dot(viewDirWS, normalWS));
                rim = step(1 - _RimSize, rim) * (mainBand > 0.001);
                color += rim * _RimColor.rgb * _RimColor.a;

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "HullOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _HULL_OUTLINE

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                #if defined(_HULL_OUTLINE)
                    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalWS * _HullWidth;
                    output.positionHCS = TransformWorldToHClip(positionWS);
                    output.normalWS = normalWS;
                    output.positionWS = positionWS;
                #else
                    // Collapse to nothing so an untoggled material pays only the vertex cost.
                    output.positionHCS = float4(0, 0, 0, 0);
                    output.normalWS = 0;
                    output.positionWS = 0;
                #endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Only the rim survives: interior back faces would otherwise show through a
                // transparent body as a dark core. Near the silhouette the normal is
                // perpendicular to the view, so the facing term is what selects the rim.
                float facing = abs(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)));
                clip(0.35 - facing);
                half4 ink = _ToonOutlineEnabled > 0.5 || _ToonSteps < 1 ? _ToonOutlineColor : half4(0, 0, 0, 0);
                if (_ToonSteps < 1) ink = half4(0.08, 0.06, 0.1, 1);
                return half4(ink.rgb, ink.a * _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // The outline pass reads this. Face normals, not smoothed ones, so the edge detector
        // sees the crease where two low-poly faces meet.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    return half4(packedNormalWS, 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
