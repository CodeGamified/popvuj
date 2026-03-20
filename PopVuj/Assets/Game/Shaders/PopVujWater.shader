// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
//
// Gerstner wave water shader for URP (Universal Render Pipeline).
// 3-wave superposition with Jacobian-based foam, fresnel, subsurface scattering,
// and sun specular. Adapted for PopVuj's 2.5D side-view perspective.
//
// Math Reference: GPU Gems Chapter 1 — Effective Water Simulation from Physical Models
//
// Gerstner Wave Displacement:
//   P.x = x + Σ(A_i * D_i.x * cos(k_i * dot(D_i, P0) - ω_i * t))
//   P.y = Σ(A_i * sin(k_i * dot(D_i, P0) - ω_i * t))
//   P.z = z + Σ(A_i * D_i.z * cos(k_i * dot(D_i, P0) - ω_i * t))
//
//   Where: k = 2π/λ (wavenumber), ω = √(g*k) (deep water dispersion)
//          A = steepness/k (amplitude), D = wave direction (normalized)

Shader "PopVuj/Water"
{
    Properties
    {
        [Header(Wave Colors)]
        _ShallowColor ("Shallow Color", Color) = (0.12, 0.30, 0.38, 1)
        _DeepColor ("Deep Color", Color) = (0.05, 0.14, 0.22, 1)
        _FoamColor ("Foam Color", Color) = (0.85, 0.90, 0.95, 1)
        _FresnelColor ("Horizon Sky Color", Color) = (0.30, 0.45, 0.60, 1)

        [Header(Waves)]
        _Wave0 ("Wave 0: Primary Swell (dirX, dirZ, steepness, wavelength)", Vector) = (1, 0, 0.15, 4)
        _Wave1 ("Wave 1: Cross Swell", Vector) = (0.87, 0.5, 0.10, 2.2)
        _Wave2 ("Wave 2: Chop", Vector) = (-0.5, 0.87, 0.05, 1.0)
        _WaveScale ("Global Wave Scale", Range(0, 3)) = 1

        [Header(Sun Lighting)]
        _SunDir ("Sun Direction", Vector) = (0.5, 0.7, 0.3, 0)
        _SunColor ("Sun Color", Color) = (1, 0.95, 0.85, 1)
        _SpecularPower ("Specular Power", Range(1, 512)) = 128
        _SpecularIntensity ("Specular Intensity", Range(0, 5)) = 1.5

        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 4
        _FresnelIntensity ("Fresnel Intensity", Range(0, 1)) = 0.5

        [Header(Foam)]
        _FoamThreshold ("Foam Fold Threshold", Range(0, 1)) = 0.3
        _FoamSharpness ("Foam Sharpness", Range(1, 20)) = 8

        [Header(Subsurface Scattering)]
        _SSSColor ("SSS Color", Color) = (0.06, 0.22, 0.18, 1)
        _SSSIntensity ("SSS Intensity", Range(0, 2)) = 0.5
        _SSSPower ("SSS Power", Range(1, 16)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry-10"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ================================================================
            // DATA STRUCTURES
            // ================================================================

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float2 waveData : TEXCOORD3; // x = height, y = jacobian
            };

            // ================================================================
            // UNIFORMS (SRP Batcher compatible)
            // ================================================================

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _FresnelColor;
                float4 _SunColor;
                float4 _SSSColor;

                float4 _Wave0;
                float4 _Wave1;
                float4 _Wave2;
                float _WaveScale;

                float4 _SunDir;
                float _SpecularPower;
                float _SpecularIntensity;

                float _FresnelPower;
                float _FresnelIntensity;

                float _FoamThreshold;
                float _FoamSharpness;

                float _SSSIntensity;
                float _SSSPower;
            CBUFFER_END

            // ================================================================
            // GERSTNER WAVE FUNCTION
            // ================================================================
            //
            // Computes displacement for a single Gerstner wave and accumulates:
            //   - Analytical surface normal
            //   - Jacobian partial derivatives (for foam/crest detection)
            //
            // Jacobian determinant:
            //   det(J) = 1.0 → flat water
            //   det(J) → 0.0 → wave cresting (foam appears)
            //   det(J) < 0.0 → surface folded (breaking wave → heavy foam)
            //
            float3 GerstnerWave(float4 wave, float2 position, float time,
                                inout float3 normal,
                                inout float3 jacobian)
            {
                float steepness = wave.z * _WaveScale;
                float wavelength = wave.w;

                if (wavelength < 0.001) return float3(0, 0, 0);

                float k = TWO_PI / wavelength;
                float c = sqrt(9.81 / k);
                float2 d = normalize(wave.xy);
                float f = k * (dot(d, position) - c * time);
                float a = steepness / k;

                float sinF = sin(f);
                float cosF = cos(f);

                // Displacement (GPU Gems Eq. 8)
                float3 displacement;
                displacement.x = d.x * a * cosF;
                displacement.y = a * sinF;
                displacement.z = d.y * a * cosF;

                // Analytical normal (GPU Gems Eq. 11)
                normal.x -= d.x * k * a * cosF;
                normal.y -= steepness * sinF;
                normal.z -= d.y * k * a * cosF;

                // Jacobian accumulation
                float ka_sinF = k * a * sinF;
                jacobian.x += d.x * d.x * ka_sinF;
                jacobian.y += d.y * d.y * ka_sinF;
                jacobian.z += d.x * d.y * ka_sinF;

                return displacement;
            }

            // ================================================================
            // VERTEX SHADER
            // ================================================================

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 xz = posWS.xz;
                float time = _Time.y;

                // Accumulators
                float3 normal = float3(0, 1, 0);
                float3 jac = float3(0, 0, 0);

                // Sum 3 Gerstner waves
                float3 displacement = float3(0, 0, 0);
                displacement += GerstnerWave(_Wave0, xz, time, normal, jac);
                displacement += GerstnerWave(_Wave1, xz, time, normal, jac);
                displacement += GerstnerWave(_Wave2, xz, time, normal, jac);

                // Jacobian determinant
                float J_xx = 1.0 - jac.x;
                float J_zz = 1.0 - jac.y;
                float J_xz = -jac.z;
                float jacobian = J_xx * J_zz - J_xz * J_xz;

                posWS += displacement;

                output.positionWS = posWS;
                output.positionCS = TransformWorldToHClip(posWS);
                output.normalWS = normalize(normal);
                output.uv = input.uv;
                output.waveData = float2(displacement.y, jacobian);

                return output;
            }

            // ================================================================
            // FRAGMENT SHADER
            // ================================================================

            float4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float3 sunDir = normalize(_SunDir.xyz);

                float waveHeight = input.waveData.x;
                float jacobian = input.waveData.y;

                // Surface fold: 0 = calm, 1 = fully folded
                float fold = saturate(1.0 - jacobian);

                // ----------------------------------------------------------
                // 1. FRESNEL (Schlick approximation)
                // ----------------------------------------------------------
                float NdotV = saturate(dot(normal, viewDir));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);

                // ----------------------------------------------------------
                // 2. BASE WATER COLOR
                //    View-angle driven. Fold brightens where surface compresses.
                // ----------------------------------------------------------
                float3 waterColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, fresnel);
                waterColor = lerp(waterColor, _ShallowColor.rgb, fold * 0.35);

                // ----------------------------------------------------------
                // 3. SKY / ENVIRONMENT REFLECTION
                // ----------------------------------------------------------
                float3 reflectDir = reflect(-viewDir, normal);
                float skyGradient = saturate(reflectDir.y * 0.5 + 0.5);
                float3 skyColor = lerp(_FresnelColor.rgb * 0.5, _FresnelColor.rgb, skyGradient);

                float3 surfaceColor = lerp(waterColor, skyColor, fresnel * _FresnelIntensity);

                // ----------------------------------------------------------
                // 4. AMBIENT — water is never fully black
                // ----------------------------------------------------------
                float sunAboveHorizon = saturate(sunDir.y * 2.0);
                float3 ambientLight = lerp(
                    _DeepColor.rgb * 0.25,
                    _ShallowColor.rgb * 0.12 + float3(0.08, 0.08, 0.1),
                    sunAboveHorizon
                );
                surfaceColor += ambientLight;

                // ----------------------------------------------------------
                // 5. WRAP DIFFUSE — subtle sun warmth
                // ----------------------------------------------------------
                float NdotL = dot(normal, sunDir);
                float wrapDiffuse = saturate(NdotL * 0.35 + 0.65);
                surfaceColor *= wrapDiffuse * lerp(float3(0.85, 0.85, 0.9), _SunColor.rgb, sunAboveHorizon);

                // ----------------------------------------------------------
                // 6. SPECULAR — tight sun glints
                // ----------------------------------------------------------
                float3 halfDir = normalize(sunDir + viewDir);
                float NdotH = saturate(dot(normal, halfDir));
                float spec = pow(NdotH, _SpecularPower) * _SpecularIntensity;
                spec *= sunAboveHorizon;
                float3 specular = spec * _SunColor.rgb;

                // ----------------------------------------------------------
                // 7. SUBSURFACE SCATTERING
                //    Light through thin compressed crests.
                // ----------------------------------------------------------
                float sssDot = pow(saturate(dot(viewDir, -sunDir)), _SSSPower);
                float3 sss = _SSSColor.rgb * sssDot * fold * _SSSIntensity * sunAboveHorizon;

                // ----------------------------------------------------------
                // 8. FOAM — Jacobian-driven whitecaps
                //    Appears where the surface folds (wave breaking).
                // ----------------------------------------------------------
                float foamMask = saturate((fold - _FoamThreshold) * _FoamSharpness);

                // ----------------------------------------------------------
                // 9. COMBINE
                // ----------------------------------------------------------
                float3 finalColor = surfaceColor + specular + sss;
                finalColor = lerp(finalColor, _FoamColor.rgb, foamMask);

                return float4(finalColor, 1.0);
            }

            ENDHLSL
        }

        // ====================================================================
        // DEPTH ONLY — ensures ocean writes to depth buffer
        // ====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _FoamColor;
                float4 _FresnelColor;
                float4 _SunColor;
                float4 _SSSColor;

                float4 _Wave0;
                float4 _Wave1;
                float4 _Wave2;
                float _WaveScale;

                float4 _SunDir;
                float _SpecularPower;
                float _SpecularIntensity;

                float _FresnelPower;
                float _FresnelIntensity;

                float _FoamThreshold;
                float _FoamSharpness;

                float _SSSIntensity;
                float _SSSPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 GerstnerWaveDepth(float4 wave, float2 position, float time)
            {
                float steepness = wave.z * _WaveScale;
                float wavelength = wave.w;
                if (wavelength < 0.001) return float3(0, 0, 0);

                float k = TWO_PI / wavelength;
                float c = sqrt(9.81 / k);
                float2 d = normalize(wave.xy);
                float f = k * (dot(d, position) - c * time);
                float a = steepness / k;

                return float3(d.x * a * cos(f), a * sin(f), d.y * a * cos(f));
            }

            Varyings vertDepth(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float time = _Time.y;

                float3 disp = float3(0, 0, 0);
                disp += GerstnerWaveDepth(_Wave0, posWS.xz, time);
                disp += GerstnerWaveDepth(_Wave1, posWS.xz, time);
                disp += GerstnerWaveDepth(_Wave2, posWS.xz, time);

                posWS += disp;
                output.positionCS = TransformWorldToHClip(posWS);
                return output;
            }

            float4 fragDepth(Varyings input) : SV_Target
            {
                return 0;
            }

            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
