// A mutated crop: the painting's own light and shade, recoloured into the tier's colour and LIFTED,
// with a slow sheen sweeping across it and a glow that the night does not dim.
//
// A plain multiply tint can only darken, which is why every mutation used to look like a sick
// plant. The tier colour and strength arrive per vertex in TEXCOORD1 (MutationTint), so every
// mutated crop on the field shares this one material. Otherwise the same as UI/Default: stencil,
// RectMask2D clipping, premultiplied output.
Shader "MiT/UI Mutation"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
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
        Blend One OneMinusSrcAlpha
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
                float4 mut      : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 mask     : TEXCOORD2;
                float4 mut      : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;
                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
                OUT.color = v.color * _Color;
                OUT.mut = v.mut;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                half3 tint = IN.mut.rgb;
                half amount = IN.mut.a;

                half l = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                // the tier colour, carrying the drawing's shading, brighter than the drawing itself;
                // the brightest parts go toward white like light on crystal
                // A jewel, not a wash. The tier colour is pushed MORE saturated, the painting's light
                // and shade (contrast-stretched, so every leaf keeps its shape) are remapped onto it:
                // deep saturated shadows, the vivid colour in the middle, near-white highlights.
                // A pastel version of this read as faded, a multiply tint as rotten.
                half lc = saturate((l - 0.5) * 1.5 + 0.5);
                half tl = dot(tint, half3(0.299, 0.587, 0.114));
                half3 ts = saturate(tl + (tint - tl) * 1.6);
                half3 dark = ts * ts * 0.55;
                half3 mid = saturate(ts * 1.1);
                half3 lo = lerp(dark, mid, saturate(lc * 2));
                half3 hi = lerp(mid, half3(1, 1, 1), saturate((lc - 0.5) * 2) * 0.8);
                half3 hue = lerp(lo, hi, step(0.5, lc));
                half3 painted = lerp(tex.rgb, hue, saturate(amount));

                // a sheen band sweeping diagonally, every ~2.5 s
                half sweep = frac((IN.texcoord.x * 0.7 + (1 - IN.texcoord.y)) * 0.6 - _Time.y * 0.4);
                half band = smoothstep(0.0, 0.07, sweep) * (1 - smoothstep(0.07, 0.18, sweep));

                // the ambient light multiplies the painting, not the glow: a mutation shines at night
                half3 glow = (ts * 0.08 + band * 0.42 * lc) * saturate(amount);
                half4 color;
                color.rgb = painted * IN.color.rgb + glow;
                color.a = tex.a * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                color.rgb *= color.a;
                return color;
            }
        ENDCG
        }
    }
}
