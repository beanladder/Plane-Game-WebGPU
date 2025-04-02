Shader "PolygonArsenal/URP/PolyRimLightTransparent"
{
    Properties
    {
        _InnerColor("Inner Color", Color) = (1,1,1,1)
        _RimColor("Rim Color", Color) = (0.26,0.19,0.16,0)
        _RimWidth("Rim Width", Range(0.2,20)) = 3
        _RimGlow("Rim Glow Multiplier", Range(0,9)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One One
        Cull Back
        ZWrite Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _InnerColor;
                half4 _RimColor;
                half _RimWidth;
                half _RimGlow;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.normalWS = normalInput.normalWS;
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDir = normalize(input.viewDirWS);
                
                half rim = 1.0 - saturate(dot(viewDir, normalWS));
                half3 emission = _RimColor.rgb * _RimGlow * pow(rim, _RimWidth);
                
                return half4(_InnerColor.rgb + emission, 1);
            }
            ENDHLSL
        }
    }
}