Shader "HunterVsHider/AssassinRadius"
{
    Properties
    {
        _Color ("Ring Color", Color) = (1.0, 0.25, 0.35, 0.6)
        _FillAlpha ("Interior Fill Alpha", Range(0, 0.5)) = 0.04
        _RingThickness ("Ring Thickness", Range(0.01, 0.3)) = 0.06
        _Radius ("Visual Radius", Float) = 7.0
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
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
                float3 localPos : TEXCOORD1;
            };

            fixed4 _Color;
            float _FillAlpha;
            float _RingThickness;
            float _Radius;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float dist = length(i.localPos.xz);
                float normDist = dist / max(0.001, _Radius);

                if (normDist > 1.0)
                {
                    discard;
                }

                // Outer ring edge highlight
                float ringInner = 1.0 - _RingThickness;
                float edgeFactor = smoothstep(ringInner, 1.0, normDist);

                float alpha = lerp(_FillAlpha, _Color.a, edgeFactor);
                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
