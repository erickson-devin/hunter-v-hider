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
            float4 _FoVMaskTex_TexelSize;

            float4x4 _FrustumCornersWS;
            float3 _CameraWS;
            float4 _MapBounds; // (minX, minZ, sizeX, sizeZ) e.g. (-150, -150, 300, 300)
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

                // Map world position X and Z to FoVMask UV space (300m x 300m combat arena)
                float2 fovUV = (worldPos.xz - _MapBounds.xy) / _MapBounds.zw;

                // Staging zones (Zone_Lobby at X=1000, Zone_PolicePrep at X=2000) remain clear of combat fog
                if (worldPos.x > 500.0)
                {
                    return sceneColor;
                }

                // Within Combat Arena region: outside 300x300 boundary renders solid pitch-black fog
                if (fovUV.x < 0.0 || fovUV.x > 1.0 || fovUV.y < 0.0 || fovUV.y > 1.0)
                {
                    return _FogColor;
                }

                // Multi-tap soft edge filter with bilinear kernel for smooth feathering
                float2 texel = _FoVMaskTex_TexelSize.xy * 1.5;
                fixed m0 = tex2D(_FoVMaskTex, fovUV).r;
                fixed m1 = tex2D(_FoVMaskTex, fovUV + float2(texel.x, texel.y)).r;
                fixed m2 = tex2D(_FoVMaskTex, fovUV + float2(-texel.x, texel.y)).r;
                fixed m3 = tex2D(_FoVMaskTex, fovUV + float2(texel.x, -texel.y)).r;
                fixed m4 = tex2D(_FoVMaskTex, fovUV + float2(-texel.x, -texel.y)).r;
                fixed mask = (m0 * 0.4) + ((m1 + m2 + m3 + m4) * 0.15);

                // Smooth feathered 3-tier transitions without blocky stair-stepping
                fixed4 memoryColor = sceneColor * _MemoryDarkness;
                float activeBlend = smoothstep(0.48, 0.88, mask);
                float memoryBlend = smoothstep(0.04, 0.38, mask);

                fixed4 baseFogAndMem = lerp(_FogColor, memoryColor, memoryBlend);
                return lerp(baseFogAndMem, sceneColor, activeBlend);
            }
            ENDCG
        }
    }
}
