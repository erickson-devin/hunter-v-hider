Shader "HunterVsHider/FoW_MemoryDecay"
{
    Properties
    {
        _MainTex ("Accumulated Memory Buffer", 2D) = "black" {}
        _CurrentVisTex ("Current Frame Visibility", 2D) = "black" {}
        _MemoryFloor ("Memory Value", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZTest Always Cull Off ZWrite Off

        CGINCLUDE
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
        sampler2D _CurrentVisTex;
        float _MemoryFloor;

        v2f vert (appdata v)
        {
            v2f o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;
            return o;
        }
        ENDCG

        // Pass 0: Instant Memory Accumulation (Stores strictly accumulated memory: 0.0 or _MemoryFloor)
        Pass
        {
            Name "AccumulateMemory"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAccumulate

            fixed4 fragAccumulate (v2f i) : SV_Target
            {
                fixed currentVis = tex2D(_CurrentVisTex, i.uv).r;
                fixed prevMem = tex2D(_MainTex, i.uv).r;

                // If pixel was seen in current frame (> 0.1), immediately record _MemoryFloor (0.5).
                // Otherwise retain previously accumulated memory.
                fixed newMem = max(prevMem, (currentVis > 0.1 ? _MemoryFloor : 0.0));

                return fixed4(newMem, newMem, newMem, 1.0);
            }
            ENDCG
        }

        // Pass 1: Output Combined Mask (1.0 for active vision, 0.5 for instant memory, 0.0 for unexplored)
        Pass
        {
            Name "CombineMask"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragCombine

            fixed4 fragCombine (v2f i) : SV_Target
            {
                fixed currentVis = tex2D(_CurrentVisTex, i.uv).r;
                fixed mem = tex2D(_MainTex, i.uv).r;

                // Active vision (1.0) takes highest priority; otherwise output instant memory (0.5) or unexplored (0.0)
                fixed finalMask = max(currentVis, mem);

                return fixed4(finalMask, finalMask, finalMask, 1.0);
            }
            ENDCG
        }
    }
}
