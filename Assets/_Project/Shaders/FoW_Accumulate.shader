Shader "Custom/FoW_Accumulate"
{
    Properties
    {
        _MainTex ("Active Vision Texture", 2D) = "black" {}
        _PrevCombinedTex ("Previous Combined FoW", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _PrevCombinedTex;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float activeVision = tex2D(_MainTex, i.uv).g;
                float prevDiscovery = tex2D(_PrevCombinedTex, i.uv).r;

                // Red = Persistent Discovery, Green = Active Vision
                float newDiscovery = max(prevDiscovery, activeVision);

                return fixed4(newDiscovery, activeVision, 0.0, 1.0);
            }
            ENDCG
        }
    }
}
