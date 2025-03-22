Shader "Custom/StylizedLowPolyWater"
{
    Properties
    {
        _ShallowColor("Shallow Color", Color) = (0.4, 0.8, 1.0, 0.6)
        _DeepColor("Deep Color", Color) = (0.1, 0.3, 0.8, 0.8)
        _WaveSpeed("Wave Speed", Range(0, 2)) = 0.5
        _WaveHeight("Wave Height", Range(0, 0.5)) = 0.1
        _WaveFrequency("Wave Frequency", Range(0, 20)) = 5.0
        _EdgeFoam("Edge Foam", Range(0, 1)) = 0.3
        _FresnelPower("Fresnel Power", Range(0, 5)) = 2.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float depth : TEXCOORD3;
            };

            float4 _ShallowColor;
            float4 _DeepColor;
            float _WaveSpeed;
            float _WaveHeight;
            float _WaveFrequency;
            float _EdgeFoam;
            float _FresnelPower;

            v2f vert (appdata v)
            {
                v2f o;
                
                // Wave displacement using sine waves
                float wave = sin(_Time.y * _WaveSpeed + v.vertex.x * _WaveFrequency) * 
                           cos(_Time.y * _WaveSpeed * 0.8 + v.vertex.z * _WaveFrequency) * 
                           _WaveHeight;
                           
                v.vertex.y += wave;
                
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(WorldSpaceViewDir(v.vertex));
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.depth = length(ObjSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Depth-based color blending
                float depthFactor = saturate(i.depth * 0.5);
                fixed4 waterColor = lerp(_ShallowColor, _DeepColor, depthFactor);

                // Fresnel effect for edge highlight
                float fresnel = pow(1.0 - saturate(dot(i.normal, i.viewDir)), _FresnelPower);
                
                // Foam at wave peaks
                float foam = smoothstep(0.7, 1.0, fresnel) * _EdgeFoam;
                
                // Combine effects
                waterColor.rgb += foam;
                waterColor.a = lerp(_ShallowColor.a, _DeepColor.a, depthFactor);
                
                return waterColor;
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}