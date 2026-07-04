Shader "Custom/URP/Sea"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.15, 0.65, 0.75, 0.75)
        _DeepColor ("Deep Color", Color) = (0.02, 0.10, 0.22, 0.85)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)

        [NoScaleOffset]_NormalA ("Normal A", 2D) = "bump" {}
        [NoScaleOffset]_NormalB ("Normal B", 2D) = "bump" {}

        _NormalTiling ("Normal Tiling", Float) = 0.08
        _NormalScale ("Normal Strength", Range(0, 2)) = 0.75
        _NormalSpeedA ("Normal Speed A", Vector) = (0.04, 0.02, 0, 0)
        _NormalSpeedB ("Normal Speed B", Vector) = (-0.025, 0.035, 0, 0)

        _WaveAmp1 ("Wave Amp 1", Range(0, 3)) = 0.35
        _WaveFreq1 ("Wave Freq 1", Range(0, 5)) = 0.85
        _WaveSpeed1 ("Wave Speed 1", Range(0, 5)) = 1.0

        _WaveAmp2 ("Wave Amp 2", Range(0, 3)) = 0.18
        _WaveFreq2 ("Wave Freq 2", Range(0, 5)) = 1.45
        _WaveSpeed2 ("Wave Speed 2", Range(0, 5)) = 1.6

        _DepthDistance ("Depth Color Distance", Float) = 8.0
        _FoamDistance ("Foam Distance", Float) = 1.2
        _FoamIntensity ("Foam Intensity", Range(0, 1)) = 0.8

        _FresnelPower ("Fresnel Power", Range(1, 8)) = 4
        _SpecularPower ("Specular Power", Range(8, 256)) = 64
        _SpecularStrength ("Specular Strength", Range(0, 2)) = 0.6

        _Alpha ("Alpha", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardSea"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            // ------------------------------------------------------------
            // STENCIL WATER CUT
            // ------------------------------------------------------------
            // Ref 1 olan piksellerde deniz çizilmez.
            // WaterStencilMask shader'ý stencil buffer'a 1 yazar.
            // Bu shader ise stencil 1 gördüðü yerde suyu renderlamaz.
            // ------------------------------------------------------------
            Stencil
            {
                Ref 1
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            TEXTURE2D(_NormalA);
            SAMPLER(sampler_NormalA);

            TEXTURE2D(_NormalB);
            SAMPLER(sampler_NormalB);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;

                float _NormalTiling;
                float _NormalScale;
                float4 _NormalSpeedA;
                float4 _NormalSpeedB;

                float _WaveAmp1;
                float _WaveFreq1;
                float _WaveSpeed1;

                float _WaveAmp2;
                float _WaveFreq2;
                float _WaveSpeed2;

                float _DepthDistance;
                float _FoamDistance;
                float _FoamIntensity;

                float _FresnelPower;
                float _SpecularPower;
                float _SpecularStrength;

                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            float GetWaveHeight(float2 worldXZ)
            {
                float wave1 = sin(dot(worldXZ, normalize(float2(1.0, 0.35))) * _WaveFreq1 + _Time.y * _WaveSpeed1) * _WaveAmp1;
                float wave2 = sin(dot(worldXZ, normalize(float2(-0.45, 1.0))) * _WaveFreq2 + _Time.y * _WaveSpeed2) * _WaveAmp2;

                return wave1 + wave2;
            }

            float3 GetWaveNormal(float2 worldXZ)
            {
                float offset = 0.15;

                float hL = GetWaveHeight(worldXZ - float2(offset, 0));
                float hR = GetWaveHeight(worldXZ + float2(offset, 0));
                float hD = GetWaveHeight(worldXZ - float2(0, offset));
                float hU = GetWaveHeight(worldXZ + float2(0, offset));

                float3 normalWS = normalize(float3(hL - hR, 2.0 * offset, hD - hU));
                return normalWS;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                float waveHeight = GetWaveHeight(positionWS.xz);
                positionWS.y += waveHeight;

                OUT.positionWS = positionWS;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                OUT.normalWS = GetWaveNormal(positionWS.xz);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                float rawSceneDepth = SampleSceneDepth(screenUV);
                float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                float waterEyeDepth = IN.screenPos.w;

                float depthDifference = max(0, sceneEyeDepth - waterEyeDepth);
                float depth01 = saturate(depthDifference / max(_DepthDistance, 0.001));

                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);

                float foamMask = saturate(1.0 - depthDifference / max(_FoamDistance, 0.001));
                foamMask *= _FoamIntensity;

                float2 uvA = IN.positionWS.xz * _NormalTiling + _NormalSpeedA.xy * _Time.y;
                float2 uvB = IN.positionWS.xz * (_NormalTiling * 0.65) + _NormalSpeedB.xy * _Time.y;

                half3 normalA = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalA, sampler_NormalA, uvA), _NormalScale);
                half3 normalB = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalB, sampler_NormalB, uvB), _NormalScale);

                half3 normalTS = normalize(half3(normalA.xy + normalB.xy, normalA.z * normalB.z));

                // Tangent-space normal'i yatay su düzlemine yaklaþýk world-space olarak çeviriyoruz.
                half3 detailNormalWS = normalize(half3(normalTS.x, normalTS.z, normalTS.y));
                half3 normalWS = normalize(IN.normalWS + detailNormalWS * 0.65);

                Light mainLight = GetMainLight();

                half3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half3 lightDirWS = normalize(mainLight.direction);

                half ndotl = saturate(dot(normalWS, lightDirWS));
                half3 diffuse = waterColor * (0.35 + ndotl * 0.65) * mainLight.color;

                half3 halfDir = normalize(lightDirWS + viewDirWS);
                half specular = pow(saturate(dot(normalWS, halfDir)), _SpecularPower) * _SpecularStrength;

                half fresnel = pow(1.0 - saturate(dot(viewDirWS, normalWS)), _FresnelPower);

                half3 finalColor = diffuse;
                finalColor += specular * mainLight.color;
                finalColor += fresnel * 0.25;

                finalColor = lerp(finalColor, _FoamColor.rgb, foamMask);

                half alpha = lerp(_Alpha, 1.0, foamMask);

                return half4(finalColor, alpha);
            }

            ENDHLSL
        }
    }
}