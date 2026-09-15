// The veil of cloud the start-to-farm cinematic dives into and bursts out of (UI/EnterCinematic.cs).
//
// The sprite (Art/cine/cine_veil, Tools/gen_cinematic.py) keeps the painted light in RGB and the
// cloud's DENSITY in alpha. Vertex alpha is not opacity here but COVER: 1 hides the screen
// completely, 0 is gone. In between, the veil burns away where the field is lowest — the field is
// mostly distance from the centre, and partly the density — so the hole opens in the middle first
// and its edge follows the outline of the billows instead of a circle. A soft light spills along
// that edge (premultiplied blending: the rim is added, the body is laid over).
//
// Colour: highlights take _Light, shadows take _Shade (so a warm gold dive keeps cool lavender
// bellies instead of turning beige), then the whole body is lifted toward _Light by _Lift — inside a
// sunlit cloud there are no deep shadows — times the vertex colour, a little brighter in the middle.
// The edge is kept fairly crisp: a wide soft edge left translucent billows over the farm, which read
// as a double exposure rather than as cloud.
Shader "MiT/UI Cloud Veil"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Light ("Highlight", Color) = (1,0.96,0.88,1)
        _Shade ("Shadow", Color) = (0.95,0.94,1,1)
        _Rim ("Edge light (a = strength)", Color) = (1,0.9,0.7,0.25)
        _Lift ("Lift toward the highlight", Range(0,1)) = 0.3
        _Aspect ("Screen aspect (w/h)", Float) = 1.7778
        _Radial ("Share of distance in the field", Range(0,1)) = 0.7
        _Soft ("Edge softness", Range(0.01,0.3)) = 0.04
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

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 mask     : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            fixed4 _Light, _Shade, _Rim;
            float _Aspect, _Radial, _Soft, _Lift;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.vertex = vPosition;
                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;

                // 0 in the middle of the screen, 1 in its corners
                float2 d = (IN.texcoord - 0.5) * float2(_Aspect, 1.0);
                float r = length(d) / (0.5 * sqrt(_Aspect * _Aspect + 1.0));
                float field = lerp(tex.a, r, _Radial);

                float cover = IN.color.a;
                float s = _Soft;
                float edge = (1.0 - cover) * (1.0 + 2.0 * s) - s;
                float k = smoothstep(edge - s, edge + s, field);

                // the painted light runs from ~0.70 (deep belly) to 1.0 (lit top)
                float lum = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                half3 grade = lerp(_Shade.rgb, _Light.rgb, saturate((lum - 0.70) / 0.30));
                half3 body = lerp(tex.rgb * grade, _Light.rgb, _Lift) * IN.color.rgb * (1.04 - 0.10 * r * r);

                // light spilling round the edge, on both sides of it and wider than the edge itself
                float band = saturate(1.0 - abs(field - edge) / (5.0 * s));
                // and none at either end, so nothing pops when the veil completes or goes
                float rim = band * band * saturate((1.0 - cover) * 6.0) * saturate(cover * 6.0);
                half4 color = half4(body * k + _Rim.rgb * (_Rim.a * rim), k);

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color *= m.x * m.y;
                #endif
                return color;
            }
        ENDCG
        }
    }
}
