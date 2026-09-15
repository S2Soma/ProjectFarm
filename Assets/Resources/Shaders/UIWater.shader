// Đảo Nước's river, flowing. Drawn over the painted river by IslandLife as one quad whose texcoords
// ARE grid cells (u along the river, v across), so the flow direction, the banks, the spring and the
// island's rim are all computed here from the same formulas Tools/gen_islands.py paints with:
//
//   meander(u) = 0.05 sin(1.7u + 0.6) + 0.02 sin(4.3u)     band |v - meander| < 0.44
//   spring at u = -2.25, radius 0.62 (v stretched 1.05)      clipped at the rim u = _RimU
//
// Look: depth-shaded water, two noise layers advected downstream with a two-phase crossfade (so a
// current faster in the middle never smears the texture), bright ripple lines, foam on the banks,
// sun glints drifting with the current, and ring ripples where a fish came down (_Splash0/1 = u, v,
// start time in _Time.y, strength). All _Time: the mesh never changes.
Shader "MiT/UI Water"
{
    Properties
    {
        [PerRendererData] _MainTex ("Noise (tileable)", 2D) = "gray" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _RimU ("Rim u", Float) = 3.0
        _Splash0 ("Splash 0 (u, v, t0, k)", Vector) = (0,0,-100,0)
        _Splash1 ("Splash 1 (u, v, t0, k)", Vector) = (0,0,-100,0)
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
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 grid : TEXCOORD0; float4 mask : TEXCOORD1; };

            sampler2D _MainTex;
            fixed4 _Color;
            float _RimU;
            float4 _Splash0, _Splash1;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.mask = MiTClipMask(v.vertex, o.vertex);
                o.grid = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            // two-phase advection of one noise layer downstream (+u)
            half Flow(float2 q, float speed, float2 scale, float2 seed, float t, float period)
            {
                float ph0 = frac(t / period);
                float ph1 = frac(t / period + 0.5);
                float w0 = 1.0 - abs(2.0 * ph0 - 1.0);
                half a = tex2D(_MainTex, (q - float2(speed * ph0 * period, 0)) * scale + seed).r;
                half b = tex2D(_MainTex, (q - float2(speed * ph1 * period, 0)) * scale + seed + float2(0.37, 0.61)).r;
                return lerp(b, a, w0);
            }

            half Ring(float2 q, float4 s, float t)
            {
                float age = t - s.z;
                if (age < 0.0 || age > 1.6 || s.w <= 0.0) return 0;
                float d = length((q - s.xy) * float2(1.0, 1.9));
                half r1 = smoothstep(0.035, 0.0, abs(d - age * 0.34));
                half r2 = smoothstep(0.03, 0.0, abs(d - max(0.0, age - 0.28) * 0.30));
                return (r1 + r2 * 0.6) * (1.0 - age / 1.6) * s.w;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                const float HALF = 0.44;
                const float SU = -2.25;
                float u = i.grid.x, v = i.grid.y;
                float t = _Time.y;

                float mean = 0.05 * sin(u * 1.7 + 0.6) + 0.02 * sin(u * 4.3);
                float dv = v - mean;
                float adv = abs(dv);
                float aa = max(fwidth(adv), 1e-4);
                half band = saturate((HALF - adv) / aa + 0.5) * saturate((u - SU) / max(fwidth(u), 1e-4) + 0.5);
                float sd = length(float2(u - SU, v * 1.05));
                half spring = saturate((0.62 - sd) / max(fwidth(sd), 1e-4) + 0.5);
                half rim = saturate((_RimU - u) / max(fwidth(u), 1e-4) + 0.5);
                half mask = max(band, spring) * rim;

                half depthBand = saturate(1.0 - adv / HALF) * step(SU, u);
                half depthSpring = saturate(1.0 - sd / 0.62);
                half depth01 = max(depthBand, depthSpring);
                half springness = saturate(1.0 - (u - SU) / 0.9);

                // the current: quick in the middle, lazy at the banks and in the spring pool
                float speed = lerp(0.07, 0.30, pow(depthBand, 0.6)) * (1.0 - springness * 0.75);
                float2 q = float2(u, dv);
                half n1 = Flow(q, speed, float2(0.55, 2.6), float2(0.11, 0.23), t, 2.6);
                half n2 = Flow(q, speed * 1.5, float2(1.5, 5.2), float2(0.53, 0.07), t + 1.3, 1.9);
                half ripple = n1 * 0.62 + n2 * 0.38;

                half3 shallow = MiTSRGB(half3(0.56, 0.88, 0.95));
                half3 deep = MiTSRGB(half3(0.16, 0.52, 0.77));
                half3 col = lerp(shallow, deep, pow(depth01, 0.8));
                col *= 0.84 + 0.30 * ripple;
                // bright ripple lines, the painted river's own motif, now moving
                half rippleLine = smoothstep(0.60, 0.635, ripple) * (1.0 - smoothstep(0.645, 0.69, ripple));
                col += rippleLine * lerp(0.20, 0.10, _MiTNight) * (0.4 + 0.6 * depthBand);

                // foam where the water meets the banks, broken and carried along
                half foamZone = smoothstep(HALF - 0.11, HALF - 0.015, adv) * band + smoothstep(0.50, 0.61, sd) * spring * springness;
                half foamN = Flow(q, speed * 1.2 + 0.03, float2(3.3, 9.0), float2(0.71, 0.29), t + 0.7, 2.2);
                col = lerp(col, MiTSRGB(half3(0.93, 0.985, 1.0)), saturate(foamZone * smoothstep(0.45, 0.66, foamN) * 0.75));

                // sun glints riding the current
                float2 gp = float2(u * 4.2 - WrapScroll(t, 0.24 * 4.2), dv * 9.0 + 7.0);
                float2 cell = floor(gp);
                float2 f = frac(gp);
                float h = Hash21(WrapCell(cell));
                float2 c = 0.3 + 0.4 * Hash22(WrapCell(cell) + 5.1);
                float gd = length((f - c) * float2(1.0, 1.6));
                half tw = saturate(sin(t * (1.3 + h * 2.2) + h * 43.0) * 2.2 - 1.25);
                half glint = smoothstep(0.16, 0.0, gd) * tw * step(0.45, h) * depthBand;
                // a thin cross on the brightest ones
                half cross = (smoothstep(0.02, 0.0, abs(f.y - c.y)) * smoothstep(0.32, 0.0, abs(f.x - c.x))) * tw * step(0.8, h) * depthBand;
                col += (glint + cross * 0.8) * lerp(1.0, 0.35, _MiTNight);

                // fish rings
                half rings = Ring(float2(u, v), _Splash0, t) + Ring(float2(u, v), _Splash1, t);
                col += rings * 0.45;

                col *= i.color.rgb;
                fixed4 o = fixed4(col, mask * i.color.a);
                o.a *= MiTClip(i.mask);
                return o;
            }
        ENDCG
        }
    }
}
