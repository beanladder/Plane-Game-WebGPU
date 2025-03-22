Shader "Custom/LowPolyWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.65, 0.9, 1.0, 0.5)
        _DeepColor ("Deep Color", Color) = (0.1, 0.4, 0.7, 0.8)
        _WaveHeight ("Wave Height", Range(0, 2)) = 0.5
        _WaveFrequency ("Wave Frequency", Range(0, 5)) = 0.5
        _WaveLength ("Wave Length", Range(0, 2)) = 0.75
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1.0
        _FoamDistance ("Foam Distance", Range(0, 10)) = 1.0
        _PlaneProximity ("Plane Proximity", Range(0, 1)) = 0
        _PlanePosition ("Plane Position", Vector) = (0, 0, 0, 0)
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 0.7)
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        
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
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                float depth : TEXCOORD4;
            };
            
            float4 _ShallowColor;
            float4 _DeepColor;
            float _WaveHeight;
            float _WaveFrequency;
            float _WaveLength;
            float _WaveSpeed;
            float _WaveTime;
            float _FoamDistance;
            float _PlaneProximity;
            float4 _PlanePosition;
            float4 _FoamColor;
            
            v2f vert (appdata v)
            {
                v2f o;
                
                // Original vertex position (already animated by the script)
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // Calculate world position
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                
                // Calculate view direction
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                
                // Pass normal and depth info
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.depth = -mul(UNITY_MATRIX_MV, v.vertex).z;
                
                return o;
            }
            
            float4 frag (v2f i) : SV_Target
            {
                // Calculate low-poly visual effect
                // This creates the flat-shaded triangular appearance
                float3 fdx = ddx(i.worldPos);
                float3 fdy = ddy(i.worldPos);
                float3 normalFace = normalize(cross(fdx, fdy));
                
                // Calculate fake lighting based on flat normal
                float ndotl = max(0.4, dot(normalFace, normalize(_WorldSpaceLightPos0.xyz)));
                
                // Calculate fresnel effect for edge highlighting
                float fresnel = pow(1.0 - saturate(dot(normalFace, i.viewDir)), 3.0);
                
                // Create a base water color by depth
                float4 waterColor = lerp(_ShallowColor, _DeepColor, fresnel * 0.5 + 0.2);
                
                // Add some variation based on world position
                float noise = frac(sin(dot(i.worldPos.xz, float2(12.9898, 78.233))) * 43758.5453) * 0.1;
                
                // Add foam on edges based on fresnel
                float foam = smoothstep(0.6, 1.0, fresnel) * _FoamDistance;
                
                // Add plane interaction foam
                if (_PlaneProximity > 0)
                {
                    // Calculate distance from vertex to plane position
                    float distToPlane = length(i.worldPos.xz - _PlanePosition.xz);
                    
                    // Create a ring of foam around the plane
                    float planeFoam = smoothstep(2.0, 0.5, distToPlane) * _PlaneProximity;
                    
                    // Add to total foam
                    foam = max(foam, planeFoam);
                }
                
                // Apply foam to color
                waterColor.rgb = lerp(waterColor.rgb, _FoamColor.rgb, foam * _FoamColor.a);
                
                // Apply lighting
                waterColor.rgb *= ndotl;
                
                // Add some subtle variation
                waterColor.rgb += noise;
                
                // Ensure proper alpha
                waterColor.a = lerp(_ShallowColor.a, _DeepColor.a, fresnel * 0.5);
                waterColor.a = max(waterColor.a, foam * 0.7);
                
                return waterColor;
            }
            ENDCG
        }
    }
    
    FallBack "Transparent/VertexLit"
}