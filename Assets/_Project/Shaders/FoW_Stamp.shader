Shader "Custom/FoW_Stamp"
{
    Properties
    {
        _Color ("Color", Color) = (0, 1, 0, 1)
        _FoWArenaMin ("Arena Min", Vector) = (-25, -25, 0, 0)
        _FoWArenaSize ("Arena Size", Vector) = (50, 50, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend One Zero

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _FoWArenaMin;
            float4 _FoWArenaSize;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float2 arenaSize = max(_FoWArenaSize.xy, float2(1.0, 1.0));
                float2 arenaUV = (worldPos.xz - _FoWArenaMin.xy) / arenaSize;
                float2 clipXY = arenaUV * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                clipXY.y = -clipXY.y;
                #endif
                o.vertex = float4(clipXY.x, clipXY.y, 0.5, 1.0);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Write Green = 1.0 for Active Vision
                return fixed4(0, 1, 0, 1);
            }
            ENDCG
        }
    }
}
