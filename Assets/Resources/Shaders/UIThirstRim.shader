// The thirsty bed's rim light (Art/beds/bed_glow_thirst.png, IslandView): a slow breath plus two bright
// highlights running round the bed's diamond, so a thirsty plot is caught from the corner of the eye —
// the ripe rim only breathes. Animated from _Time on a fixed Image, so it rebuilds nothing.
//
// The sprite is the bed frame of Tools/gen_beds.py (780 x 640): the diamond's centre is at uv (0.5, 0.475),
// its half-extents 380/780 and 190/640.
Shader "MiT/UI Thirst Rim"
{
    Properties
    {
        [PerRendererData] _MainTex ("Rim", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Pulse ("Breath speed, depth, run speed, run strength", Vector) = (3.0, 0.35, 1.4, 0.9)
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
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 mask : TEXCOORD1; };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _Pulse;

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
                fixed4 c = tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                half breath = 1.0 - _Pulse.y + _Pulse.y * (0.5 + 0.5 * sin(t * _Pulse.x));

                // angle round the diamond, measured in its own (square) space
                float2 d = float2((i.uv.x - 0.5) / 0.487, (i.uv.y - 0.475) / 0.297);
                float ang = atan2(d.y, d.x);
                half run = pow(saturate(cos(ang - t * _Pulse.z)), 10.0) + pow(saturate(cos(ang - t * _Pulse.z + 3.14159)), 10.0);
                // only on the bright band, not on the faint inner wash
                half band = smoothstep(0.35, 0.9, c.a);
                c.rgb = lerp(c.rgb, MiTSRGB(half3(1.0, 1.0, 1.0)), saturate(run * band * _Pulse.w * 0.8));
                c.a = saturate(c.a * breath + run * band * _Pulse.w * 0.35);
                c *= i.color;
                c.a *= MiTClip(i.mask);
                return c;
            }
        ENDCG
        }
    }
}
