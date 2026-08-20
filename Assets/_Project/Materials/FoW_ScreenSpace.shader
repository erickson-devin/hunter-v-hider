Shader "HunterVsHider/FoW_ScreenSpace"
{
    Properties
    {
        _MainTex ("Source Texture", 2D) = "white" {}
        _FoVMaskTex ("FoV Mask Texture", 2D) = "black" {}
        _FogColor ("Fog Color", Color) = (0.02, 0.02, 0.03, 1.0)
        _MemoryDarkness ("Memory Brightness Multiplier", Range(0.1, 1.0)) = 0.5
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 interpolatedRay : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _CameraDepthTexture;
            sampler2D _FoVMaskTex;

            float4x4 _FrustumCornersWS;
            float3 _CameraWS;
            float4 _MapBounds; // (minX, minZ, sizeX, sizeZ) e.g. (-25, -25, 50, 50)
            fixed4 _FogColor;
            float _MemoryDarkness;

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;

                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0)
                {
                    o.uv.y = 1.0 - o.uv.y;
                }
                #endif

                // Map vertex UV to frustum corner ray:
                // (0, 1) -> Top-Left (row 0)
                // (1, 1) -> Top-Right (row 1)
                // (1, 0) -> Bottom-Right (row 2)
                // (0, 0) -> Bottom-Left (row 3)
                int cornerIndex = 0;
                if (v.texcoord.x < 0.5 && v.texcoord.y > 0.5)
                    cornerIndex = 0; // Top-Left
                else if (v.texcoord.x > 0.5 && v.texcoord.y > 0.5)
                    cornerIndex = 1; // Top-Right
                else if (v.texcoord.x > 0.5 && v.texcoord.y < 0.5)
                    cornerIndex = 2; // Bottom-Right
                else
                    cornerIndex = 3; // Bottom-Left

                o.interpolatedRay = _FrustumCornersWS[cornerIndex].xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sceneColor = tex2D(_MainTex, i.uv);

                // Sample depth and linearize
                float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float linearDepth = LinearEyeDepth(depth);

                // Reconstruct world position from camera position, frustum ray, and depth
                float3 worldPos = _CameraWS + i.interpolatedRay * linearDepth;

                // Map world position X and Z to FoVMask UV space (e.g. 250m x 250m combat arena)
                float2 fovUV = (worldPos.xz - _MapBounds.xy) / _MapBounds.zw;

                // Out of arena bounds -> Render standard scene lighting (Zone_Lobby and Zone_PolicePrep remain clear of fog)
                if (fovUV.x < 0.0 || fovUV.x > 1.0 || fovUV.y < 0.0 || fovUV.y > 1.0)
                {
                    return sceneColor;
                }

                fixed mask = tex2D(_FoVMaskTex, fovUV).r;

                // 3-Tier Evaluation:
                // 1. Mask >= 0.95 -> Visible active wedge: Output original scene color
                // 2. 0.35 <= Mask < 0.95 -> Explored memory: Output scene color darkened by 50%
                // 3. Mask < 0.35 -> Unexplored: Output opaque black fog
                if (mask >= 0.95)
                {
                    return sceneColor;
                }
                else if (mask >= 0.35)
                {
                    float t = (mask - 0.35) / (0.95 - 0.35);
                    fixed4 memoryColor = sceneColor * _MemoryDarkness;
                    return lerp(memoryColor, sceneColor, t);
                }
                else
                {
                    float t = saturate(mask / 0.35);
                    fixed4 memoryColor = sceneColor * _MemoryDarkness;
                    return lerp(_FogColor, memoryColor, t);
                }
            }
            ENDCG
        }
    }
}
