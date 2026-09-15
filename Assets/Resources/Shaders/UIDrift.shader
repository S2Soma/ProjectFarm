// Procedural particles in a quad: embers rising off lava, petals and seeds drifting down, frost and
// gold glints twinkling, static crackle, spray off the waterfall. No particle exists on the CPU: the
// quad is a lattice of cells, each cell holds at most one sprite at a hashed spot, and the lattice
// scrolls with _Vel. Each sprite stays inside its own cell (spot 0.3-0.7, size <= 0.22, wobble small),
// so one texture lookup per pixel is always enough.
//
//   _Grid     cells across x, y
//   _Vel      lattice scroll in cells per second (x, y); y > 0 rises
//   _Shape    x: sprite size as a share of a cell, y: size jitter, z: density (share of occupied cells),
//             w: spin (turns per second, signed by hash)
//   _Wobble   x: amplitude in cells, y: frequency
//   _Life     x: twinkle rate, y: twinkle sharpness (0 = steady), z: fade in near the source edge,
//             w: fade out near the far edge (shares of the quad along the travel)
//   _Edge     x: radial fade width at the quad's border (0..1)
//   _SrcBlend/_DstBlend  SrcAlpha,One for light (embers, glints); SrcAlpha,OneMinusSrcAlpha for petals
//
// Texcoord0 is 0..1 over the quad; the quad fades out towards its border as an ellipse, so a
// rectangle never shows. Texcoord1.y is a per-quad seed.
Shader "MiT/UI Drift"
{
    Properties
    {
        [PerRendererData] _MainTex ("Particle", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Grid ("Grid", Vector) = (8, 6, 0, 0)
        _Vel ("Velocity", Vector) = (0, 0.5, 0, 0)
        _Shape ("Size, jitter, density, spin", Vector) = (0.18, 0.4, 0.5, 0)
        _Wobble ("Wobble amp, freq", Vector) = (0.05, 1.3, 0, 0)
        _Life ("Twinkle rate, sharpness, fade in, fade out", Vector) = (1, 0, 0.2, 0.35)
        _Edge ("Border fade", Vector) = (0.35, 0, 0, 0)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
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
        Blend [_SrcBlend] [_DstBlend]
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
            fixed4 _Color;
            float4 _Grid, _Vel, _Shape, _Wobble, _Life, _Edge;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.mask = MiTClipMask(v.vertex, o.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                o.seed = v.texcoord1.y;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y + i.seed * 31.0;
                float2 uv = i.uv;
                float2 p = uv * _Grid.xy - float2(WrapScroll(t, _Vel.x), WrapScroll(t, _Vel.y)) + i.seed * 64.0;
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 id = WrapCell(cell);
                float h = Hash21(id);
                float2 h2 = Hash22(id + 11.3);

                float present = step(h, _Shape.z);
                float2 c = 0.3 + 0.4 * h2;
                c += _Wobble.x * float2(sin(t * _Wobble.y + h * 6.283), cos(t * _Wobble.y * 0.73 + h2.x * 6.283));
                float size = _Shape.x * (1.0 - _Shape.y * h2.y);
                float2 d = (f - c) / max(size, 1e-3);
                float ang = (h2.x - 0.5) * 2.0 * _Shape.w * t * 6.283 + h * 6.283 * step(0.001, abs(_Shape.w));
                float ca = cos(ang), sa = sin(ang);
                d = float2(d.x * ca - d.y * sa, d.x * sa + d.y * ca);
                float2 suv = d * 0.5 + 0.5;
                half inside = step(0.0, suv.x) * step(suv.x, 1.0) * step(0.0, suv.y) * step(suv.y, 1.0);
                fixed4 s = tex2D(_MainTex, saturate(suv));

                // twinkle / life
                half tw = sin(t * _Life.x * (0.7 + 0.6 * h2.y) + h * 40.0) * 0.5 + 0.5;
                tw = lerp(1.0, saturate((tw - 0.5) * (1.0 + _Life.y * 6.0) + 0.5), step(0.001, _Life.y));

                // fade along the direction of travel, and round the quad's border
                float2 dir = normalize(_Vel.xy + float2(1e-4, 1e-4));
                float along = dot(uv - 0.5, dir) + 0.5;
                half travel = smoothstep(0.0, max(_Life.z, 1e-3), along) * (1.0 - smoothstep(1.0 - max(_Life.w, 1e-3), 1.0, along));
                travel = lerp(1.0, travel, step(1e-3, length(_Vel.xy)));
                float2 e = (uv - 0.5) * 2.0;
                half border = 1.0 - smoothstep(1.0 - _Edge.x, 1.0, length(e));

                half k = present * inside * tw * travel * border;
                fixed4 col = s * i.color;
                col.a *= k;
                col.a *= MiTClip(i.mask);
                return col;
            }
        ENDCG
        }
    }
}
