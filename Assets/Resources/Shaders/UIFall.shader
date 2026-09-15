// The waterfall off Đảo Nước's front edge. One quad hung from the river's lip (IslandLife): texcoord x
// runs across the fall (0..1 is the water column, the quad is a little wider so the fall can fan out),
// y runs down it (0 at the lip, 1 where it has thinned to nothing).
//
// Streaks: two noise layers stretched along the fall and scrolled down, accelerating with the drop;
// white churn where the water leaves the lip; the column fans out and frays into the mist below.
Shader "MiT/UI Fall"
{
    Properties
    {
        [PerRendererData] _MainTex ("Noise (tileable)", 2D) = "gray" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
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
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "IslandLife.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 mask : TEXCOORD1; };

            sampler2D _MainTex;
            fixed4 _Color;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.mask = MiTClipMask(v.vertex, o.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float y = saturate(i.uv.y);
                float spread = 1.0 + y * 0.22;
                float xc = (i.uv.x - 0.5) / spread + 0.5;
                // the edges wander a little, so the column is not a ruled strip
                half wob = (tex2D(_MainTex, float2(y * 0.8 - t * 0.35, 0.37)).g - 0.5) * 0.06 * y;
                float xe = xc + wob;
                half edge = smoothstep(-0.02, 0.07, xe) * smoothstep(1.02, 0.93, xe);

                // falling streaks, accelerating as the water drops
                float drop = y * 1.5 + y * y * 1.1;
                half s1 = tex2D(_MainTex, float2(xc * 2.6 + 0.13, drop * 0.45 - t * 1.05)).r;
                half s2 = tex2D(_MainTex, float2(xc * 5.9 + 0.71, drop * 0.80 - t * 1.75)).g;
                half s3 = tex2D(_MainTex, float2(xc * 11.0 + 0.4, drop * 1.40 - t * 2.60)).b;
                half streak = saturate((s1 * 0.55 + s2 * 0.35 + s3 * 0.25 - 0.38) * 2.4);

                half core = 1.0 - abs(xc - 0.5) * 2.0;
                half3 col = lerp(MiTSRGB(half3(0.30, 0.64, 0.88)), MiTSRGB(half3(0.72, 0.92, 1.0)), streak);
                col = lerp(col, MiTSRGB(half3(0.96, 0.99, 1.0)), smoothstep(0.62, 0.95, streak) * 0.85);
                col *= 0.86 + 0.14 * core;
                // shade where the water column turns away from the light (right side)
                col *= 1.0 - smoothstep(0.55, 1.0, xc) * 0.16;

                // white churn leaving the lip
                half lip = smoothstep(0.16, 0.0, y);
                half churn = tex2D(_MainTex, float2(xc * 7.0, y * 3.0 - t * 2.2)).b;
                col = lerp(col, MiTSRGB(half3(0.97, 1.0, 1.0)), saturate(lip * smoothstep(0.30, 0.62, churn) * 1.1));

                // thins and frays into mist toward the bottom
                half fray = tex2D(_MainTex, float2(xc * 4.0, y * 1.2 - t * 0.9)).r;
                half a = edge * (0.80 + 0.20 * streak);
                a *= 1.0 - smoothstep(0.50 + fray * 0.25, 1.0, y);
                a *= smoothstep(0.0, 0.015, y);

                col *= i.color.rgb;
                fixed4 o = fixed4(col, a * i.color.a);
                o.a *= MiTClip(i.mask);
                return o;
            }
        ENDCG
        }
    }
}
