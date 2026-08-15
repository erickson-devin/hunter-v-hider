Shader "Custom/FogOfWarBlit"
{
    Properties
    {
        _UnexploredColor ("Unexplored Blackout Shroud", Color) = (0.0, 0.0, 0.0, 1.0)
        _ExploredColor ("Explored Memory Ambient Tint", Color) = (0.05, 0.07, 0.09, 0.70)
        _UnexploredAlpha ("Unexplored Alpha", Range(0, 1)) = 1.0
        _ExploredAlpha ("Explored Alpha", Range(0, 1)) = 0.70
        _FogOfWarTex ("Fog of War Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+500" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

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
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 ray : TEXCOORD3;
            };

            sampler2D _CameraDepthTexture;
            sampler2D _FogOfWarTex;

            float4 _FoWArenaMin;   // (-25, -25, 0, 0)
            float4 _FoWArenaSize;  // (50, 50, 0, 0)

            fixed4 _UnexploredColor;
            fixed4 _ExploredColor;
            float _UnexploredAlpha;
            float _ExploredAlpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.pos);

                // Compute world space view ray from camera position to quad vertex
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.ray = worldPos - _WorldSpaceCameraPos;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float3 rayDir = normalize(i.ray);

                // 1. Determine world position on ground / scene geometry
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV);
                float linear01 = Linear01Depth(rawDepth);
                float eyeDepth = LinearEyeDepth(rawDepth);

                float3 worldPos;
                if (linear01 < 0.999 && rawDepth > 0.0001)
                {
                    float3 camForward = -UNITY_MATRIX_V[2].xyz;
                    float cosAngle = max(dot(rayDir, camForward), 0.001);
                    float dist = eyeDepth / cosAngle;
                    worldPos = _WorldSpaceCameraPos + rayDir * dist;
                }
                else
                {
                    // Fallback to ground plane y = 0
                    float t = -_WorldSpaceCameraPos.y / min(rayDir.y, -0.0001);
                    worldPos = _WorldSpaceCameraPos + rayDir * t;
                }

                // 2. Map world position to arena UV [0, 1]
                float2 arenaSize = max(_FoWArenaSize.xy, float2(1.0, 1.0));
                float2 arenaUV = (worldPos.xz - _FoWArenaMin.xy) / arenaSize;

                // Out of arena bounds -> Unexplored Pitch Black
                if (arenaUV.x < 0.0 || arenaUV.x > 1.0 || arenaUV.y < 0.0 || arenaUV.y > 1.0)
                {
                    return fixed4(_UnexploredColor.rgb, _UnexploredAlpha);
                }

                // 3. Sample Fog of War Texture
                half4 fow = tex2D(_FogOfWarTex, arenaUV);
                float discovery = fow.r;
                float activeVision = fow.g;

                // 4. Tiered visibility:
                // Active Vision (Green > 0.05): Alpha = 0.0 (Clear vision)
                // Explored Memory (Red > 0.05): Alpha = ~0.70 (Faint memory overlay)
                // Unexplored (Red & Green <= 0.05): Alpha = 1.0 (Pitch Black)
                if (activeVision > 0.05)
                {
                    float t = saturate((activeVision - 0.05) / 0.4);
                    float alpha = lerp(_ExploredAlpha, 0.0, t);
                    return fixed4(_ExploredColor.rgb, alpha);
                }
                else if (discovery > 0.05)
                {
                    float t = saturate((discovery - 0.05) / 0.4);
                    float alpha = lerp(_UnexploredAlpha, _ExploredAlpha, t);
                    fixed3 col = lerp(_UnexploredColor.rgb, _ExploredColor.rgb, t);
                    return fixed4(col, alpha);
                }

                return fixed4(_UnexploredColor.rgb, _UnexploredAlpha);
            }
            ENDCG
        }
    }
}
