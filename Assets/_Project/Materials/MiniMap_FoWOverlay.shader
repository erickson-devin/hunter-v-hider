Shader "HunterVsHider/UI/MiniMap_FoWOverlay"
{
    Properties
    {
        _MainTex ("Map Layout Texture", 2D) = "white" {}
        _FoWMaskTex ("Fog of War Mask", 2D) = "black" {}
        _FoVUVRect ("FoW UV Rect (xy=offset, zw=scale)", Vector) = (0.4166667, 0.4166667, 0.1666667, 0.1666667)
        _IsAssassin ("Is Assassin Bypass", Float) = 0.0
        _MemoryBrightness ("Explored Memory Boost", Float) = 1.0
        _Color ("Tint Color", Color) = (1, 1, 1, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _FoWMaskTex;
            float4 _MainTex_ST;
            float4 _FoVUVRect;
            float _IsAssassin;
            float _MemoryBrightness;
            fixed4 _Color;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample Map Layout Blueprint Texture
                fixed4 mapLayout = tex2D(_MainTex, i.uv);

                // Map arena [0, 1] UV to 300m FoW mask sub-region
                float2 fowUV = _FoVUVRect.xy + i.uv * _FoVUVRect.zw;
                fixed4 fowMask = tex2D(_FoWMaskTex, fowUV);

                // If Assassin role bypass, render full layout directly
                if (_IsAssassin > 0.5f)
                {
                    fixed4 assassinCol = mapLayout * i.color;
                    #ifdef UNITY_UI_CLIP_RECT
                    assassinCol.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                    #endif
                    return assassinCol;
                }

                // Sample fog mask level (max of alpha and red channels)
                float maskVal = max(fowMask.a, fowMask.r);

                // Hard Alpha Masking: If unexplored (fog alpha near 0), force solid pitch black
                if (maskVal < 0.05f)
                {
                    fixed4 blackCol = fixed4(0, 0, 0, 1);
                    #ifdef UNITY_UI_CLIP_RECT
                    blackCol.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                    #endif
                    return blackCol;
                }

                // Multiply layout color by fog memory visibility (explored memory vs active vision cone)
                fixed3 finalRGB = mapLayout.rgb * saturate(maskVal + 0.3f) * i.color.rgb; 
                fixed4 finalCol = fixed4(finalRGB, 1.0f);

                #ifdef UNITY_UI_CLIP_RECT
                finalCol.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalCol.a - 0.001);
                #endif

                return finalCol;
            }
            ENDCG
        }
    }
}
