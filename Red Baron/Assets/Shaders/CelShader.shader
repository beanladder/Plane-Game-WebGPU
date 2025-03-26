Shader "Custom/CompleteToonShader"
{
    // Properties block remains the same
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.005
        
        [Header(Toon Shading)]
        _RampThreshold ("Ramp Threshold", Range(0,1)) = 0.5
        _RampSmoothing ("Ramp Smoothing", Range(0,1)) = 0.1
        _ShadowIntensity ("Shadow Intensity", Range(0,1)) = 0.5
        
        [Header(Emission)]
        [HDR]_EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionIntensity ("Emission Intensity", Range(0,10)) = 1
        
        [Header(Specular)]
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularSmoothness ("Specular Smoothness", Range(0,1)) = 0.1
        _SpecularIntensity ("Specular Intensity", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _RampThreshold;
            float _RampSmoothing;
            float _ShadowIntensity;
            float4 _EmissionColor;
            float _EmissionIntensity;
            float4 _SpecularColor;
            float _SpecularSmoothness;
            float _SpecularIntensity;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 positionWS : TEXCOORD2;
            float4 shadowCoord : TEXCOORD3;
            float fogCoord : TEXCOORD4;
            float3 viewDirWS : TEXCOORD5;
        };
        ENDHLSL

        // Main Pass
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Sample base texture
                float4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;

                // Get lighting data
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                
                // Calculate diffuse lighting
                float NdotL = dot(normalWS, mainLight.direction);
                float diffuse = NdotL * 0.5 + 0.5;
                
                // Apply toon ramp
                float ramp = saturate((diffuse - _RampThreshold) / _RampSmoothing);
                
                // Calculate shadows
                float shadow = mainLight.shadowAttenuation;
                float lightIntensity = lerp(1 - _ShadowIntensity, 1.0, ramp * shadow);
                
                // Calculate specular
                float3 halfVector = normalize(mainLight.direction + viewDirWS);
                float NdotH = dot(normalWS, halfVector);
                float specular = pow(saturate(NdotH), _SpecularSmoothness * 100);
                specular = step(0.5, specular) * _SpecularIntensity;
                
                // Combine lighting
                float3 lighting = mainLight.color * lightIntensity;
                float3 specularColor = _SpecularColor.rgb * specular;
                
                // Final color calculation
                float3 finalColor = baseColor.rgb * lighting + specularColor;
                
                // Add emission for bloom
                finalColor += _EmissionColor.rgb * _EmissionIntensity * baseColor.rgb;
                
                // Apply fog
                finalColor = MixFog(finalColor, input.fogCoord);
                
                return float4(finalColor, baseColor.a);
            }
            ENDHLSL
        }

        // Outline Pass
        Pass
        {
            Name "Outline"
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct OutlineVaryings
            {
                float4 positionCS : SV_POSITION;
                float fogCoord : TEXCOORD0;
            };

            OutlineVaryings vert(Attributes input)
            {
                OutlineVaryings output;

                float3 normalOS = normalize(input.normalOS);
                float3 posOS = input.positionOS.xyz + normalOS * _OutlineWidth;
                
                output.positionCS = TransformObjectToHClip(posOS);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }

            float4 frag(OutlineVaryings input) : SV_Target
            {
                float4 outlineColor = _OutlineColor;
                outlineColor.rgb = MixFog(outlineColor.rgb, input.fogCoord);
                return outlineColor;
            }
            ENDHLSL
        }

        // Shadow Cast Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            ShadowVaryings vert(Attributes input)
            {
                ShadowVaryings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            float4 frag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Depth Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings vert(Attributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 frag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}