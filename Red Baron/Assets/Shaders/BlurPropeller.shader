Shader "Custom/PropellerMotionBlur"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _RotationSpeed ("Rotation Speed", Range(0,1)) = 0.5
        _BlurAmount ("Blur Amount", Range(0,2)) = 0.5
        _BlurSamples ("Blur Samples", Range(4,16)) = 8
        _Transparency ("Transparency", Range(0,1)) = 0.5
        _RotationDirection ("Rotation Direction", Float) = 1
    }
    
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        LOD 100
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        GrabPass { "_PropellerGrabTexture" }
        
        Pass
        {
            CGPROGRAM
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float4 grabPos : TEXCOORD3;
            };
            
            float4 _Color;
            float _RotationSpeed;
            float _BlurAmount;
            int _BlurSamples;
            float _Transparency;
            float _RotationDirection;
            sampler2D _PropellerGrabTexture;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = _Color;
                float3 centerWorldPos = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 dirFromCenter = normalize(i.worldPos - centerWorldPos);
                
                if (_RotationSpeed > 0.05)
                {
                    // Calculate tangent direction for motion blur
                    float3 tangentDir = normalize(float3(dirFromCenter.z, 0, -dirFromCenter.x)) * _RotationDirection;
                    float effectiveBlur = _BlurAmount * _RotationSpeed;
                    
                    fixed4 blurColor = fixed4(0,0,0,0);
                    float2 grabUV = i.grabPos.xy / i.grabPos.w;
                    
                    // Sample along tangent direction in screen space
                    for (int j = 0; j < _BlurSamples; j++)
                    {
                        float t = (j / (float)(_BlurSamples - 1)) - 0.5;
                        float2 offset = mul((float2x3)UNITY_MATRIX_V, tangentDir).xy * t * effectiveBlur;
                        
                        // Sample grab texture with offset
                        blurColor += tex2D(_PropellerGrabTexture, grabUV + offset * 0.1);
                    }
                    
                    col = blurColor / _BlurSamples;
                    
                    // Apply base color mix
                    col = lerp(col, _Color, 0.3);
                    
                    // Velocity-based transparency
                    col.a = 1.0 - (_Transparency * _RotationSpeed);
                }
                else
                {
                    col.a = 1.0 - _Transparency;
                }

                return col;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}