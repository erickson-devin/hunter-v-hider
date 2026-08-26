Shader "HunterVsHider/FlashlightBeam"
{
    Properties
    {
        _Color ("Beam Color", Color) = (0.75, 0.88, 1.0, 0.22)
        _RadialFalloff ("Radial Falloff Power", Range(0.5, 4.0)) = 1.5
        _EdgeSoftness ("Edge Softness", Range(0.01, 1.0)) = 0.3
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "IgnoreProjector"="True" 
        }

        Blend SrcAlpha One
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
            float _RadialFalloff;
            float _EdgeSoftness;

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
                // Local coordinate relative to wedge origin
                float dist = length(i.localPos.xz);
                float maxDist = 18.0; // Matches view radius
                float radialFactor = saturate(1.0 - (dist / maxDist));
                radialFactor = pow(radialFactor, _RadialFalloff);

                // Angular falloff (center is brightest, edges fade softly)
                float angleRad = atan2(i.localPos.x, i.localPos.z); // 0 at forward +Z
                float halfAngle = 40.0 * 0.0174532925; // 40 degrees in radians
                float normAngle = abs(angleRad) / max(0.001, halfAngle);
                float angularFactor = smoothstep(1.0, 1.0 - _EdgeSoftness, normAngle);

                float finalAlpha = _Color.a * radialFactor * angularFactor;
                return fixed4(_Color.rgb * finalAlpha, finalAlpha);
            }
            ENDCG
        }
    }
}
