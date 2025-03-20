Shader "Custom/PropellerBlur"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (0.8, 0.8, 0.8, 1.0)
        _ShimmerColor ("Shimmer Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        _ShimmerSpeed ("Shimmer Speed", Range(0, 10)) = 2
        _ShimmerScale ("Shimmer Scale", Range(1, 50)) = 20
        _ShimmerIntensity ("Shimmer Intensity", Range(0, 1)) = 0.3
        _RotationSpeed ("Rotation Speed", Range(0, 10)) = 2
        _BladeCount ("Blade Count", Range(2, 8)) = 4
        [Toggle] _EmissiveBlur ("Emissive Blur", Float) = 1
        _EmissiveStrength ("Emissive Strength", Range(0, 3)) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        // No culling or depth writing for transparency
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Make fog work
            #pragma multi_compile_fog
            
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
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                float3 normal : NORMAL;
            };
            
            float4 _MainColor;
            float4 _ShimmerColor;
            float _Opacity;
            float _ShimmerSpeed;
            float _ShimmerScale;
            float _ShimmerIntensity;
            float _RotationSpeed;
            float _BladeCount;
            float _EmissiveBlur;
            float _EmissiveStrength;
            
            // Improved noise function
            float noise(float2 p)
            {
                float2 ip = floor(p);
                float2 u = frac(p);
                
                // Improved Smoothstep
                u = u * u * (3.0 - 2.0 * u);
                
                float res = lerp(
                    lerp(dot(sin(ip * 754.4), float2(127.1, 311.7)),
                         dot(sin((ip + float2(1.0, 0.0)) * 754.4), float2(127.1, 311.7)), u.x),
                    lerp(dot(sin((ip + float2(0.0, 1.0)) * 754.4), float2(127.1, 311.7)),
                         dot(sin((ip + float2(1.0, 1.0)) * 754.4), float2(127.1, 311.7)), u.x),
                    u.y);
                    
                return res * 0.5 + 0.5; // Scale to 0-1 range
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                o.normal = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Base color - unaffected by lighting
                fixed4 col = _MainColor;
                
                // Get coordinates relative to center
                float2 center = float2(0.5, 0.5);
                float2 centered_uv = i.uv - center;
                
                // Calculate angle for radial effects and distance from center
                float angle = atan2(centered_uv.y, centered_uv.x);
                float distance_from_center = length(centered_uv) * 2.0;
                
                // Create blade pattern based on angle
                float blade_pattern = (sin(_BladeCount * angle + _Time.y * _RotationSpeed) * 0.5 + 0.5);
                
                // Animate the shimmer
                float time_factor = _Time.y * _ShimmerSpeed;
                
                // Generate noise-based shimmer
                float noise_val = noise((i.uv * _ShimmerScale) + time_factor);
                
                // Create radial streaks that move outward
                float radial = frac(angle / (3.14159 * 2.0) + 0.5);
                float radial_streaks = frac(radial * _BladeCount + time_factor * 0.2) * distance_from_center;
                
                // Combine blade pattern, noise and radial streaks for final shimmer effect
                float shimmer = blade_pattern * (noise_val + radial_streaks) * _ShimmerIntensity;
                
                // Apply shimmer by blending with shimmer color
                col.rgb = lerp(col.rgb, _ShimmerColor.rgb, shimmer);
                
                // Add a subtle glow at the edges
                float edge_glow = 1.0 - pow(distance_from_center, 2);
                edge_glow = saturate(edge_glow);
                
                // Fresnel effect for better visibility from different angles
                float fresnel = pow(1.0 - saturate(dot(i.normal, i.viewDir)), 2.0);
                
                // Apply emissive effect to make it unaffected by scene lighting when enabled
                if (_EmissiveBlur > 0.5) {
                    col.rgb *= _EmissiveStrength;
                }
                
                // Final opacity calculation - fade in from center
                float final_opacity = _Opacity * saturate(distance_from_center) * (1.0 + fresnel * 0.5);
                
                // Set the alpha
                col.a = final_opacity;
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
    }
    
    Fallback "Transparent/VertexLit"
}