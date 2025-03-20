Shader "Custom/PropellerMotionBlur"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Base Texture", 2D) = "white" {}
        _RotationSpeed ("Rotation Speed", Range(0,1)) = 0.5
        _BlurAmount ("Blur Amount", Range(0,2)) = 0.5
        _BlurSamples ("Blur Samples", Range(4,32)) = 16
        _Transparency ("Transparency", Range(0,1)) = 0.5
        _RotationDirection ("Rotation Direction", Float) = 1
        _SmearLength ("Smear Length", Range(0,3)) = 1.0
        _SmearFalloff ("Smear Falloff", Range(0,5)) = 2.0
    }
    
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        LOD 100
        
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
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
                float3 tangentDir : TEXCOORD2;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _RotationSpeed;
            float _BlurAmount;
            int _BlurSamples;
            float _Transparency;
            float _RotationDirection;
            float _SmearLength;
            float _SmearFalloff;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                // Calculate tangent direction in world space
                float3 dirFromCenter = normalize(o.worldPos - mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz);
                o.tangentDir = normalize(float3(dirFromCenter.z, 0, -dirFromCenter.x)) * _RotationDirection;
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                
                if (_RotationSpeed > 0.05)
                {
                    float effectiveBlur = _BlurAmount * _RotationSpeed;
                    float smearLength = _SmearLength * effectiveBlur;
                    
                    fixed4 blurColor = fixed4(0,0,0,0);
                    float totalWeight = 0;
                    
                    // Convert tangent direction to view space
                    float3 viewTangent = mul((float3x3)UNITY_MATRIX_V, i.tangentDir);
                    float2 smearDir = normalize(viewTangent.xz);
                    
                    for (int j = 0; j < _BlurSamples; j++)
                    {
                        float t = (j / (float)(_BlurSamples - 1)) * 2.0 - 1.0;
                        float offset = t * smearLength;
                        
                        float weight = exp(-_SmearFalloff * abs(t));
                        float2 uvOffset = smearDir * offset * 0.1;
                        
                        blurColor += tex2D(_MainTex, i.uv + uvOffset) * _Color * weight;
                        totalWeight += weight;
                    }
                    
                    col = lerp(col, blurColor / totalWeight, effectiveBlur);
                    
                    // Velocity lines effect
                    float velocityStripes = sin(i.uv.x * 50 - _Time.y * 30 * _RotationSpeed);
                    col.rgb += smoothstep(0.7, 0.9, velocityStripes) * effectiveBlur * 0.3;
                    
                    col.a = (1.0 - _Transparency) * (1.0 - effectiveBlur * 0.7);
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