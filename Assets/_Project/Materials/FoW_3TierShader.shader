Shader "HunterVsHider/FoW_3TierShader"
{
    Properties
    {
        _MainTex ("FoV Mask Texture", 2D) = "black" {}
        _FogColor ("Fog Color", Color) = (0.02, 0.02, 0.03, 1)
        _UnexploredAlpha ("Unexplored Alpha", Range(0, 1)) = 1.0
        _MemoryAlpha ("Memory Alpha", Range(0, 1)) = 0.50
        _ActiveAlpha ("Active Alpha", Range(0, 1)) = 0.0
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent+50" 
            "RenderType"="Transparent" 
            "IgnoreProjector"="True" 
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _FogColor;
            float _UnexploredAlpha;
            float _MemoryAlpha;
            float _ActiveAlpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample red channel of FoVMask_RT
                fixed mask = tex2D(_MainTex, i.uv).r;

                // 3-Tier Mapping:
                // mask >= 0.95 -> Pure White / Active Vision (Alpha = 0.0, transparent)
                // 0.35 <= mask < 0.95 -> Transition to Memory / Explored (Alpha = 0.5, 50% opacity overlay)
                // mask < 0.35 -> Decayed or Unexplored Black (Alpha = 1.0, 100% opaque overlay)
                fixed alpha = _UnexploredAlpha;

                if (mask >= 0.95)
                {
                    alpha = _ActiveAlpha; // 0.0
                }
                else if (mask >= 0.35)
                {
                    // Smooth transition from active to memory state
                    float t = (mask - 0.35) / (0.95 - 0.35);
                    alpha = lerp(_MemoryAlpha, _ActiveAlpha, t);
                }
                else
                {
                    // Transition from memory to unexplored
                    float t = saturate(mask / 0.35);
                    alpha = lerp(_UnexploredAlpha, _MemoryAlpha, t);
                }

                return fixed4(_FogColor.rgb, alpha);
            }
            ENDCG
        }
    }
}
