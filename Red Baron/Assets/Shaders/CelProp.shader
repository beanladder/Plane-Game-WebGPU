Shader "Custom/PropellerBlur_CelShaded"
{
    Properties
    {
        // Original Properties
        [Header(Blade Settings)]
        _MainColor ("Main Color", Color) = (0.8, 0.8, 0.8, 1.0)
        _ShimmerColor ("Shimmer Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Opacity ("Opacity", Range(0, 1)) = 0.5
        _RotationSpeed ("Rotation Speed", Range(0, 10)) = 2.5
        _BladeCount ("Blade Count", Range(2, 8)) = 4
        _EmissiveStrength ("Emissive Strength", Range(0, 3)) = 1.0

        // Cel-Shading Properties
        [Header(Cel Shading)]
        _RampThreshold ("Shadow Threshold", Range(0,1)) = 0.5
        _RampSmoothing ("Shadow Smoothness", Range(0,0.2)) = 0.05
        _ShadowColor ("Shadow Color", Color) = (0.3, 0.3, 0.3, 1)
        
        [Header(Specular)]
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularSize ("Specular Size", Range(0,1)) = 0.1
        _SpecularIntensity ("Specular Intensity", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

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
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD2;
                float3 viewDir : TEXCOORD3;
                float bladePattern : TEXCOORD4;
            };

            // Original Properties
            float4 _MainColor;
            float4 _ShimmerColor;
            float _Opacity;
            float _RotationSpeed;
            float _BladeCount;
            float _EmissiveStrength;

            // Cel-Shading Properties
            float _RampThreshold;
            float _RampSmoothing;
            float4 _ShadowColor;
            float4 _SpecularColor;
            float _SpecularSize;
            float _SpecularIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                
                // Fixed blade pattern calculation
                float2 center = float2(0.5, 0.5);
                float2 centered_uv = v.uv - center;
                float rotation_angle = _Time.y * _RotationSpeed;
                float sin_rot, cos_rot;
                sincos(rotation_angle, sin_rot, cos_rot);
                float2 rotated_uv;
                rotated_uv.x = centered_uv.x * cos_rot - centered_uv.y * sin_rot;
                rotated_uv.y = centered_uv.x * sin_rot + centered_uv.y * cos_rot;
                float angle = atan2(rotated_uv.y, rotated_uv.x);
                o.bladePattern = (sin(_BladeCount * angle) * 0.5 + 0.5); // Fixed parenthesis

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Blade pattern calculations
                float2 center = float2(0.5, 0.5);
                float2 centered_uv = i.uv - center;
                float distance_from_center = length(centered_uv) * 2.0;
                float edge_glow = 1.0 - pow(distance_from_center, 2);

                // Lighting calculations
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                
                // Diffuse lighting
                float NdotL = dot(normal, lightDir);
                float diffuse = smoothstep(_RampThreshold - _RampSmoothing, 
                                        _RampThreshold + _RampSmoothing, 
                                        NdotL * 0.5 + 0.5);
                float3 diffuseColor = lerp(_ShadowColor.rgb, _MainColor.rgb, diffuse);
                
                // Specular highlights
                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = dot(normal, halfVector);
                float specular = pow(saturate(NdotH), _SpecularSize * 100);
                specular = step(0.5, specular) * _SpecularIntensity;
                float3 specularColor = _SpecularColor.rgb * specular;

                // Final color composition
                float motion_blur = smoothstep(0.4, 0.6, i.bladePattern);
                float final_opacity = _Opacity * saturate(distance_from_center) * motion_blur;
                fixed4 col = fixed4((diffuseColor + specularColor) * _MainColor.rgb, final_opacity);
                col.rgb += _ShimmerColor.rgb * _EmissiveStrength * i.bladePattern;
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback "Transparent/VertexLit"
}