Shader "Custom/FogOfWarBlit"
{
    Properties
    {
        _UnexploredColor ("Unexplored Blackout Shroud", Color) = (0.04, 0.04, 0.05, 1.0) // #0A0A0C
        _ExploredColor ("Explored Memory Ambient Tint", Color) = (0.10, 0.13, 0.17, 0.75) // #1B222C ambient tint
        _UnexploredAlpha ("Unexplored Alpha", Range(0, 1)) = 1.0
        _ExploredAlpha ("Explored Alpha", Range(0, 1)) = 0.72
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
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            sampler2D _CameraDepthTexture;
            sampler2D _FogOfWarTex;

            float4 _FoWArenaMin;   // (minX, minZ, 0, 0) e.g. (-25, -25, 0, 0)
            float4 _FoWArenaSize;  // (sizeX, sizeZ, 0, 0) e.g. (50, 50, 0, 0)
            float4x4 _FoW_InvVP;   // Inverse View-Projection Matrix

            fixed4 _UnexploredColor;
            fixed4 _ExploredColor;
            float _UnexploredAlpha;
            float _ExploredAlpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / i.screenPos.w;

                // 1. Sample Scene Depth
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV);
                float linear01 = Linear01Depth(rawDepth);

                // 2. Reconstruct World Position from Depth
                #if defined(UNITY_REVERSED_Z)
                    float clipDepth = rawDepth;
                #else
                    float clipDepth = rawDepth * 2.0 - 1.0;
                #endif

                float2 clipXY = screenUV * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                if (_ProjectionParams.x < 0)
                    clipXY.y = -clipXY.y;
                #endif

                float4 clipPos = float4(clipXY, clipDepth, 1.0);
                float4 worldPos4 = mul(_FoW_InvVP, clipPos);
                float3 worldPos = worldPos4.xyz / worldPos4.w;

                // If depth is skybox/far plane, project camera ray down to ground plane y = 0
                if (linear01 >= 0.999 || rawDepth <= 0.0001)
                {
                    float3 camPos = _WorldSpaceCameraPos;
                    float3 rayDir = normalize(worldPos - camPos);
                    if (abs(rayDir.y) > 0.001)
                    {
                        float t = (0.0 - camPos.y) / rayDir.y;
                        if (t > 0.0)
                        {
                            worldPos = camPos + rayDir * t;
                        }
                    }
                }

                // 3. Map World Position to Arena UV [0, 1]
                float2 arenaSize = max(_FoWArenaSize.xy, float2(1.0, 1.0));
                float2 arenaUV = (worldPos.xz - _FoWArenaMin.xy) / arenaSize;

                // Out of arena bounds -> Unexplored
                if (arenaUV.x < 0.0 || arenaUV.x > 1.0 || arenaUV.y < 0.0 || arenaUV.y > 1.0)
                {
                    return fixed4(_UnexploredColor.rgb, _UnexploredAlpha);
                }

                // 4. Sample Fog of War Texture
                // Red = Discovery / Persistent Explored Memory
                // Green = Active Vision
                half4 fow = tex2D(_FogOfWarTex, arenaUV);
                float discovery = fow.r;
                float activeVision = fow.g;

                // 5. Tiered Blending:
                // Value 1.0: Active Vision -> Full clarity (0 alpha)
                // Value 0.5: Explored Memory -> Ambient tint
                // Value 0.0: Unexplored -> Blackout shroud overlay
                float activeSmooth = smoothstep(0.05, 0.95, activeVision);
                float discoverySmooth = smoothstep(0.05, 0.95, discovery);

                if (activeSmooth >= 0.99)
                {
                    // Active Vision: Full clarity
                    return fixed4(0, 0, 0, 0);
                }

                if (activeSmooth > 0.0)
                {
                    // Transition between Explored / Active Vision
                    float alpha = lerp(_ExploredAlpha, 0.0, activeSmooth);
                    return fixed4(_ExploredColor.rgb, alpha);
                }

                if (discoverySmooth > 0.0)
                {
                    // Transition between Unexplored / Explored
                    float alpha = lerp(_UnexploredAlpha, _ExploredAlpha, discoverySmooth);
                    fixed3 col = lerp(_UnexploredColor.rgb, _ExploredColor.rgb, discoverySmooth);
                    return fixed4(col, alpha);
                }

                // Completely Unexplored: Full blackout
                return fixed4(_UnexploredColor.rgb, _UnexploredAlpha);
            }
            ENDCG
        }
    }
}
