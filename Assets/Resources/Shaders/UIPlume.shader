// A soft rising (or drifting) plume: waterfall mist, a volcanic vent's smoke and steam, cold mist
// curling off the ice. One quad; texcoord y runs along the plume (0 at its source), x across it.
// The quad can lie in any direction (IslandLife lays it out), so the same shader drifts sideways.
//
//   _Spread  x: half-width at the source, y: half-width at the far end (fractions of the quad)
//   _Rise    how fast the puffs travel along the plume, per second
//   _Puff    noise scale (bigger = smaller puffs)
//   _ColA/_ColB colour at the source / far end; alpha of _ColA is the plume's density
Shader "MiT/UI Plume"
{
    Properties
    {
        [PerRendererData] _MainTex ("Noise (tileable)", 2D) = "gray" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ColA ("Colour at source", Color) = (1,1,1,0.8)
        _ColB ("Colour at end", Color) = (1,1,1,0.5)
        _Spread ("Spread (source, end)", Vector) = (0.25, 0.5, 0, 0)
        _Rise ("Rise", Float) = 0.12
        _Puff ("Puff scale", Float) = 1.6
        _Fade ("Fade in, fade out", Vector) = (0.12, 0.45, 0, 0)
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

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; float4 texcoord1 : TEXCOORD1; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 mask : TEXCOORD1; float seed : TEXCOORD2; };

            sampler2D _MainTex;
            fixed4 _Color, _ColA, _ColB;
            float4 _Spread, _Fade;
            float _Rise, _Puff;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.mask = MiTClipMask(v.vertex, o.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                o.seed = v.texcoord1.y;          // per-plume phase, so neighbouring vents do not puff in step
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y + i.seed * 17.0;
                float y = i.uv.y;
                float w = lerp(_Spread.x, _Spread.y, y);
                float sway = sin(t * 0.37 + y * 2.6) * 0.06 * y + sin(t * 0.91 + y * 5.1) * 0.025 * y;
                float x = (i.uv.x - 0.5 - sway) / max(w, 1e-3);

                float scroll = frac(t * _Rise / 4.0) * 4.0;
                half n1 = tex2D(_MainTex, float2(x * 0.30 * _Puff + i.seed, y * 0.55 * _Puff - scroll)).r;
                half n2 = tex2D(_MainTex, float2(x * 0.62 * _Puff - i.seed, y * 1.10 * _Puff - scroll * 1.7)).g;
                half n = n1 * 0.62 + n2 * 0.48;

                half shape = saturate(1.0 - x * x);
                shape *= smoothstep(0.0, _Fade.x, y) * (1.0 - smoothstep(1.0 - _Fade.y, 1.0, y));
                half a = saturate((n - (1.0 - shape) * 0.62 - 0.20) * 2.3) * shape;

                fixed4 c = lerp(_ColA, _ColB, y);
                // puff tops catch light
                c.rgb *= 0.92 + 0.16 * n2;
                c.rgb *= i.color.rgb;
                fixed4 o = fixed4(c.rgb, a * c.a * i.color.a);
                o.a *= MiTClip(i.mask);
                return o;
            }
        ENDCG
        }
    }
}
