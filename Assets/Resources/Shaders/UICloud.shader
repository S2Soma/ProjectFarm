// A painted cloud that lights itself and breathes (SkyView, StartScreen).
//
// The sprite's grey value is LIGHT, not colour (Tools/gen_sky.py): 1 the sunlit top, 0 the deepest
// shadow. It is mapped onto a ramp from _Shade to _Lit — the hour's lavender shadow and warm light —
// instead of multiplying one tint over it, which turned every shadow brown at golden hour. Alpha is
// soft density; its feathered edge swells and thins with two slow octaves of noise that move WITH
// the cloud, and the lookup is warped a few texels by the same noise, so the outline billows.
//
// Nothing here costs a canvas rebuild: the drift is in the shader too.
//   _Scroll.x   texture-width offset added to u (a band scrolling through its tile)
//   _Scroll.y   canvas-unit offset added to x (a cumulus drifting across the sky)
//   _Scroll.z   1 = wrap u (a band that repeats), 0 = a sprite (clamped)
//   _Scroll.w   how many tiles the quad spans horizontally (1 for a sprite)
//   _Drift      tiles per second of autonomous scroll, from _Time (the start screen; SkyView leaves 0)
//   _Billow     x edge breathing (alpha), y warp in texels, z noise tiles per texel, w noise speed
//   _MiTCloudTime  seconds SkyView adds to _Time.y for the noise (a storm runs its clouds' clock fast)
//   _Ramp       x grey at which the shade colour is reached, y grey of full light, z inner shimmer
//   _Rim        silver lining on thin sunlit edges: rgb colour, a strength
//
// Grey is sampled from an sRGB texture in a Linear project, so it is re-encoded before the ramp.
Shader "MiT/UI Cloud"
{
    Properties
    {
        [PerRendererData] _MainTex ("Cloud (grey = light, a = density)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _NoiseTex ("Noise (R coarse, G fine)", 2D) = "gray" {}
        _Lit ("Light colour", Color) = (1,1,1,1)
        _Shade ("Shade colour", Color) = (0.72,0.78,0.90,1)
        _Rim ("Rim light", Color) = (1,0.95,0.85,0.25)
        _Ramp ("Ramp (shade at, lit at, shimmer)", Vector) = (0.1, 0.95, 0.05, 0)
        _Scroll ("Scroll (u, x, wrap, tiles)", Vector) = (0, 0, 0, 1)
        _Billow ("Billow (edge, warp px, noise/px, speed)", Vector) = (0.35, 3, 0.004, 0.02)
        _TexSize ("Texture size", Vector) = (1024, 512, 0, 0)
        _Drift ("Drift (tiles/s)", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 noise : TEXCOORD1;      // xy coarse octave, zw fine octave, in noise tiles
            };

            sampler2D _MainTex, _NoiseTex;
            fixed4 _Color, _Lit, _Shade, _Rim;
            float4 _Ramp, _Scroll, _Billow, _TexSize;
            float _Drift;
            float _MiTCloudTime;

            v2f vert(appdata_t v)
            {
                v2f o;
                v.vertex.x += _Scroll.y;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                float u = v.texcoord.x * _Scroll.w + _Scroll.x + frac(_Time.y * _Drift);
                o.uv = float2(u, v.texcoord.y);
                // noise lives in texels, so it moves with the cloud; two octaves wander apart
                float2 px = o.uv * _TexSize.xy;
                float t = (_Time.y + _MiTCloudTime) * _Billow.w;
                o.noise.xy = px * _Billow.z + float2(t * 0.61, t * 0.23);
                o.noise.zw = px * _Billow.z * 3.0 + float2(-t * 0.47, t * 0.53) + 0.37;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half2 na = tex2D(_NoiseTex, frac(i.noise.xy)).rg;
                half2 nb = tex2D(_NoiseTex, frac(i.noise.zw)).rg;
                half n = na.r * 0.62 + nb.g * 0.38;

                float2 uv = i.uv + (half2(na.g, nb.r) - 0.5) * (_Billow.y / _TexSize.xy);
                uv.x = lerp(uv.x, frac(uv.x), _Scroll.z);
                fixed4 tex = tex2D(_MainTex, uv);

                // the soft edge swells where the noise is high and thins where it is low; the solid
                // core (a = 1) and clear sky (a = 0) never change
                half a = tex.a;
                a = saturate(a + (n - 0.5) * _Billow.x * saturate(a * (1.0 - a) * 4.0));

                #ifdef UNITY_COLORSPACE_GAMMA
                half g = tex.r;
                #else
                half g = LinearToGammaSpaceExact(tex.r);
                #endif
                half t = saturate((g - _Ramp.x) / max(_Ramp.y - _Ramp.x, 1e-3) + (nb.r - 0.5) * _Ramp.z);
                t = t * t * (3.0 - 2.0 * t) * 0.35 + t * 0.65;
                half3 col = lerp(_Shade.rgb, _Lit.rgb, t);
                // silver lining: thin edges on the lit side glow
                half thin = saturate(a * (1.0 - a) * 4.0);
                col += _Rim.rgb * (_Rim.a * thin * t);

                fixed4 o = fixed4(col, a) * i.color;
                return o;
            }
        ENDCG
        }
    }
}
