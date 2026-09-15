// Light that lives in a painting: Đảo Hoả's lava veins breathing, Đảo Lôi's crystals charging, gold
// and ice catching a travelling glint and brief stars. The sprite is a light mask painted by
// Tools/gen_islands.py (over the island's rect, half resolution) or Tools/gen_life.py (over a prop):
//   rgb = steady light, already its colour and amount (black where there is none) — it pulses;
//   a   = where glints and twinkles may appear, in _GlintColor: twinkles wherever a > 0 (scaled by a),
//         the travelling glint only where a > 0.6 — a gold nugget or a crystal, never a whole field.
// Imported without alphaIsTransparency (FarmTextureImporter), so rgb under a = 0 is kept as painted.
// Drawn additively.
//
//   _Pulse    x: speed (rad/s), y: depth 0..1, z: noise scale for per-feature phase
//   _Glint    x: strength, y: speed (sweeps per second), z: band width, w: band spacing
//   _Twinkle  x: strength, y: cells across, z: rate, w: density
//   _NightBoost  light multiplier at full night (the same lava is brighter against a dark sky)
Shader "MiT/UI Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Glow mask", 2D) = "black" {}
        _Noise ("Noise (tileable)", 2D) = "gray" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlintColor ("Glint colour", Color) = (1,1,1,1)
        _Pulse ("Pulse speed, depth, scale", Vector) = (1.2, 0.5, 3, 0)
        _Glint ("Glint strength, speed, width, spacing", Vector) = (0, 0.15, 0.04, 0.6)
        _Twinkle ("Twinkle strength, cells, rate, density", Vector) = (0, 60, 2, 0.25)
        _NightBoost ("Night boost", Float) = 1.5
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

            sampler2D _MainTex, _Noise;
            fixed4 _Color, _GlintColor;
            float4 _Pulse, _Glint, _Twinkle;
            float _NightBoost;

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
                fixed4 g = tex2D(_MainTex, i.uv);
                half ph = tex2D(_Noise, i.uv * _Pulse.z).r;
                half pulse = 1.0 - _Pulse.y + _Pulse.y * (0.5 + 0.5 * sin(t * _Pulse.x + ph * 12.566));

                // a band of light travelling across the island on the diagonal
                float band = frac((i.uv.x * 1.0 - i.uv.y * 0.55) / max(_Glint.w, 1e-3) - frac(t * _Glint.y));
                half glint = smoothstep(0.0, _Glint.z, band) * (1.0 - smoothstep(_Glint.z, _Glint.z * 2.5, band));

                // twinkles: brief stars in a lattice, only where the mask allows
                float2 p = i.uv * float2(_Twinkle.y, _Twinkle.y * 0.64);
                float2 cell = floor(p);
                float2 f = frac(p);
                float h = Hash21(WrapCell(cell));
                float2 c = 0.25 + 0.5 * Hash22(WrapCell(cell) + 3.7);
                float2 d = abs(f - c);
                half star = smoothstep(0.16, 0.0, length(d)) + smoothstep(0.03, 0.0, d.y) * smoothstep(0.34, 0.0, d.x)
                          + smoothstep(0.03, 0.0, d.x) * smoothstep(0.34, 0.0, d.y);
                half life = saturate(sin(t * _Twinkle.z * (0.6 + h) + h * 60.0) * 3.0 - 2.0);
                half tw = star * life * step(h, _Twinkle.w);

                half night = lerp(1.0, _NightBoost, _MiTNight);
                half glintZone = smoothstep(0.6, 0.9, g.a);
                fixed3 col = g.rgb * pulse + _GlintColor.rgb * (glintZone * glint * _Glint.x + g.a * tw * _Twinkle.x);
                col *= night * i.color.rgb * i.color.a;
                col *= MiTClip(i.mask);
                return fixed4(col, 0);
            }
        ENDCG
        }
    }
}
