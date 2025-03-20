Shader "Custom/PropellerBlur"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (0.8, 0.8, 0.8, 1.0)
        _ShimmerColor ("Shimmer Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        _ShimmerSpeed ("Shimmer Speed", Range(0, 10)) = 2
        _ShimmerScale ("Shimmer Scale", Range(1, 50)) = 20
        _ShimmerIntensity ("Shimmer Intensity", Range(0, 1)) = 0.0
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
                // Base color
                fixed4 col = _MainColor;
                
                // Calculate UV coordinates relative to center
                float2 center = float2(0.5, 0.5);
                float2 centered_uv = i.uv - center;
                
                // Calculate continuous rotation angle
                float rotation_angle = _Time.y * _RotationSpeed * 10.0; // Speed multiplier
                float2 rotated_uv;
                float sin_rot, cos_rot;
                sincos(rotation_angle, sin_rot, cos_rot);
                
                // Rotate UV coordinates
                rotated_uv.x = centered_uv.x * cos_rot - centered_uv.y * sin_rot;
                rotated_uv.y = centered_uv.x * sin_rot + centered_uv.y * cos_rot;
                
                // Calculate blade pattern using rotated coordinates
                float angle = atan2(rotated_uv.y, rotated_uv.x);
                float blade_pattern = (sin(_BladeCount * angle) * 0.5 + 0.5);
                
                // Distance from center and edge glow
                float distance_from_center = length(centered_uv) * 2.0;
                float edge_glow = 1.0 - pow(distance_from_center, 2);
                edge_glow = saturate(edge_glow);
                
                // Emissive effect
                if (_EmissiveBlur > 0.5) {
                    col.rgb *= _EmissiveStrength * (1.0 + blade_pattern * 0.3);
                }
                
                // Enhanced opacity calculation with smooth blade rotation
                float final_opacity = _Opacity * saturate(distance_from_center) * blade_pattern;
                
                // Add rotational motion blur effect
                float motion_blur = smoothstep(0.4, 0.6, blade_pattern);
                final_opacity *= motion_blur;
                
                // Set final alpha
                col.a = final_opacity * (1.0 - (distance_from_center * 0.2));
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
    }
    Fallback "Transparent/VertexLit"
}