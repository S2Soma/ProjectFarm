// Đảo Lôi's static: a crackling arc between the glass orbs of two lightning rods, now and then, with
// a rare fat bolt. One quad from orb A (texcoord x = 0) to orb B (x = 1); y = 0.5 is the straight
// line between them. The path is re-rolled 14 times a second, which is what makes it crackle, and it
// only fires in bursts (hashed on 2-second slots), so most of the time the orbs just glow.
//
//   _Arc   x: bow (share of the quad's height), y: jaggedness, z: burst chance per slot, w: width
Shader "MiT/UI Arc"
{
    Properties
    {
        [PerRendererData] _MainTex ("Noise (tileable)", 2D) = "gray" {}
        _Color ("Tint", Color) = (0.6,0.95,1,1)
        _Arc ("Bow, jag, chance, width", Vector) = (0.16, 0.22, 0.45, 0.025)
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

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; float4 texcoord1 : TEXCOORD1; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 mask : TEXCOORD1; float seed : TEXCOORD2; };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _Arc;

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

            half Strand(float x, float y, float frame, float jag, float width, float bow, float s)
            {
                float env = sin(3.14159 * saturate(x));
                half n1 = tex2D(_MainTex, float2(x * 1.6 + frame * 0.137 + s, frame * 0.071)).r - 0.5;
                half n2 = tex2D(_MainTex, float2(x * 4.1 - frame * 0.093, frame * 0.113 + s)).g - 0.5;
                float yc = 0.5 + bow * env + (n1 * 0.8 + n2 * 0.45) * jag * env;
                float d = abs(y - yc);
                return smoothstep(width, 0.0, d) + smoothstep(width * 7.0, 0.0, d) * 0.35;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y + i.seed * 13.0;
                float slot = floor(t / 2.0);
                float inSlot = frac(t / 2.0);
                float hs = Hash21(float2(fmod(slot, 64.0), i.seed * 7.0));
                // a burst: 0.35 s of crackle at a hashed moment inside the slot
                float start = 0.15 + 0.5 * Hash21(float2(fmod(slot, 64.0) + 9.0, i.seed));
                half active = step(hs, _Arc.z) * step(start, inSlot) * step(inSlot, start + 0.28);
                // the rare big one
                half big = step(hs, _Arc.z * 0.18);

                float frame = floor(t * 14.0);
                frame = fmod(frame, 64.0);
                float flick = step(0.25, Hash21(float2(frame, 3.0)));
                float x = i.uv.x, y = i.uv.y;
                float w = _Arc.w * (1.0 + big * 1.4);
                half s = Strand(x, y, frame, _Arc.y, w, _Arc.x * (Hash21(float2(frame, 1.0)) - 0.5) * 2.0, 0.0)
                       + Strand(x, y, frame + 17.0, _Arc.y * 1.3, w * 0.6, _Arc.x * (Hash21(float2(frame, 2.0)) - 0.5) * 2.0, 0.5) * 0.6;

                // the orbs: always a soft charge, flaring while it arcs
                // the quad reaches past both orbs (texcoord x from -0.12 to 1.12), so each glow is whole
                float2 qa = (i.uv - float2(0.0, 0.5)) * float2(1.0, 0.35);
                float2 qb = (i.uv - float2(1.0, 0.5)) * float2(1.0, 0.35);
                half orb = smoothstep(0.13, 0.0, length(qa)) + smoothstep(0.13, 0.0, length(qb));
                orb = orb * orb;
                half charge = 0.45 + 0.2 * sin(t * 3.1) + active * 1.2;

                s *= step(0.0, x) * step(x, 1.0);
                half light = s * active * flick * (1.5 + big) + orb * charge * 0.7;
                fixed3 col = lerp(i.color.rgb, fixed3(1, 1, 1), saturate(s * 0.6)) * light * i.color.a;
                col *= MiTClip(i.mask);
                return fixed4(col, 0);
            }
        ENDCG
        }
    }
}
