// The thirsty plot's badge (IslandView): a painted water drop hopping on the ground at the front-left of
// the crop, its contact shadow shrinking as it rises, and a ripple ring spreading from its foot. Owner, 15/9:
// the cracked soil and blue rim alone were too faint to say "this needs water".
//
// Atlas Art/beds/thirst_badge.png, three cells in a row: drop, ring, shadow. Each quad of the
// LifeQuads mesh has texcoords 0..1 across the QUAD, and in TEXCOORD1:
//   x  part: 0 ring, 1 drop, 2 shadow
//   y  phase, seconds (plots hop out of step)
// The drop's quad is taller than its cell by _Hop.x, which is the room it hops into. All motion is a
// remap of the texture lookup inside the quad, so it is right at every zoom and the world canvas never
// re-batches for it (vertex positions here are canvas space, which the camera's zoom scales).
//
// A readout, not scenery: it is never dimmed by the hour (vertex colour stays white).
Shader "MiT/UI Thirst"
{
    Properties
    {
        [PerRendererData] _MainTex ("Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Hop ("Hop height (share of quad), period s, squash, ring period s", Vector) = (0.18, 1.1, 0.07, 1.6)
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
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 mask : TEXCOORD1;
                float4 data : TEXCOORD2;      // part, phase
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _Hop;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.mask = MiTClipMask(v.vertex, o.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                o.data = v.texcoord1;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y + i.data.y;
                float part = floor(i.data.x + 0.5);
                float2 local = i.uv;

                // the hop: quick up, a hang, a drop onto a little squash (period _Hop.y)
                float k = frac(t / max(_Hop.y, 0.1));
                float up = sin(saturate(k / 0.62) * 3.14159);
                float hop = up * up;                                   // 0 on the ground .. 1 at the top
                float land = saturate(1.0 - abs(k - 0.70) / 0.10) * step(0.62, k);

                // ---- drop: cell spans [h, h + 1/(1+H)] of the quad's height, squashed on landing ----
                float H = _Hop.x;
                float h = hop * H / (1.0 + H);
                float sx = 1.0 + _Hop.z * land, sy = 1.0 - _Hop.z * land;
                float2 drop = float2((local.x - 0.5) / sx + 0.5, (local.y - h) * (1.0 + H) / sy);

                // ---- ring: grows from the foot and fades (period _Hop.w) ----
                float r = frac(t / max(_Hop.w, 0.1) + 0.25);
                float rs = lerp(0.35, 1.0, r);
                float2 ring = (local - 0.5) / rs + 0.5;
                half ringA = (1.0 - r) * (1.0 - r) * saturate(r * 6.0);

                // ---- shadow: shrinks and fades as the drop rises ----
                float ss = 1.0 - 0.28 * hop;
                float2 shade = (local - 0.5) / ss + 0.5;
                half shadeA = 1.0 - 0.45 * hop;

                float isRing = step(part, 0.5);
                float isDrop = step(0.5, part) * step(part, 1.5);
                float isShade = step(1.5, part);
                float2 cl = ring * isRing + drop * isDrop + shade * isShade;
                half inside = step(0.0, cl.x) * step(cl.x, 1.0) * step(0.0, cl.y) * step(cl.y, 1.0);
                // the atlas cell of each part: drop 0, ring 1, shadow 2
                float cell = isRing * 1.0 + isShade * 2.0;
                float2 suv = float2((cell + saturate(cl.x)) / 3.0, saturate(cl.y));
                // a hair inside the cell, so mip bleeding never reaches the neighbour
                suv.x = clamp(suv.x, cell / 3.0 + 0.004, (cell + 1.0) / 3.0 - 0.004);

                fixed4 c = (tex2D(_MainTex, suv) + _TextureSampleAdd) * i.color;
                c.a *= inside * (isRing * ringA + isDrop + isShade * shadeA) * MiTClip(i.mask);
                return c;
            }
        ENDCG
        }
    }
}
