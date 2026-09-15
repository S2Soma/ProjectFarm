// Đảo Băng's aurora: a soft green-to-violet curtain hanging in the sky behind the island, only at
// night (_MiTNight). One quad behind the island painting (texcoord x across, y up); the island covers
// its foot. Folds drift sideways, rays shimmer along the lower hem, everything fades toward the quad's
// sides and top. Additive light, all _Time.
Shader "MiT/UI Aurora"
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
        Blend One One
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
                float x = i.uv.x, y = i.uv.y;
                float drift = frac(t * 0.004) * 64.0;
                half fold = tex2D(_MainTex, float2(x * 0.7 + drift * 0.3, 0.31)).r;
                float xw = x + (fold - 0.5) * 0.3;
                half rays = tex2D(_MainTex, float2(xw * 5.5 - drift, 0.12)).g;
                rays = rays * rays * 1.9;
                half hemN = tex2D(_MainTex, float2(xw * 1.2 + drift * 0.5, 0.73)).b;
                float hem = 0.16 + (hemN - 0.5) * 0.28;
                half lower = smoothstep(hem - 0.04, hem + 0.03, y);
                half upper = 1.0 - smoothstep(hem + 0.12, 0.95, y);
                half sides = smoothstep(0.0, 0.2, x) * (1.0 - smoothstep(0.8, 1.0, x));
                half breathe = 0.65 + 0.35 * sin(t * 0.35 + x * 4.0 + fold * 3.0);
                half k = lower * upper * sides * (0.25 + rays) * breathe;
                half up = saturate((y - hem) * 1.5);
                half3 col = lerp(MiTSRGB(half3(0.35, 1.0, 0.62)), MiTSRGB(half3(0.55, 0.42, 1.0)), up);
                col += MiTSRGB(half3(0.8, 1.0, 0.9)) * smoothstep(hem + 0.05, hem, y) * lower * 0.5;
                col *= k * saturate(_MiTNight * 1.6 - 0.4) * 0.44 * i.color.rgb * i.color.a;
                col *= MiTClip(i.mask);
                return fixed4(col, 0);
            }
        ENDCG
        }
    }
}
