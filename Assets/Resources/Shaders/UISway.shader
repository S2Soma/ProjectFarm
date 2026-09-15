// Plants, flags and blades that move without their mesh moving. Each quad of a LifeQuads mesh shows
// one cell of an atlas laid out on a regular grid (_Cells = columns, rows) and carries in TEXCOORD1:
//
//   x  weight: 0 at the anchored end of the quad, 1 at the free end (interpolated across the quad)
//   y  phase, seconds
//   z  kind + 4 * cell index      kind 0 sway (grass, flowers, hanging vines)
//                                 kind 1 wave (a flag's cloth, anchored at its pole)
//                                 kind 2 spin (windmill blades, pinwheels) about the cell's centre
//   w  amplitude: share of a cell's width (sway, wave) or turns per second (spin)
//
// The movement is a shear of the texture lookup inside the quad, so it scales with the quad and is
// right at every zoom — displacing vertices in canvas space would not be. Cells need transparent
// padding on the side a plant leans into. Wind strength comes from the _MiTWind global.
Shader "MiT/UI Sway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cells ("Atlas columns, rows", Vector) = (4, 2, 0, 0)
        _Speed ("Sway speed", Float) = 1.6
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
                float4 data : TEXCOORD2;      // weight, phase, kind, amp
                float4 cell : TEXCOORD3;      // cell min (xy), cell size (zw), in atlas uv
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _Cells;
            float _Speed;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.mask = MiTClipMask(v.vertex, o.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                float code = v.texcoord1.z + 0.5;
                float kind = floor(fmod(code, 4.0));
                float index = floor(code / 4.0);
                float cols = max(_Cells.x, 1.0), rows = max(_Cells.y, 1.0);
                float cx = fmod(index, cols);
                float cy = floor(index / cols);
                float2 size = float2(1.0 / cols, 1.0 / rows);
                // row 0 is the TOP row of the atlas image; uv y runs bottom-up
                o.cell = float4(cx * size.x, 1.0 - (cy + 1.0) * size.y, size.x, size.y);
                o.data = float4(v.texcoord1.x, v.texcoord1.y, kind, v.texcoord1.w);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float wind = MiTWind();
                float w = saturate(i.data.x);
                float kind = i.data.z;
                float2 uv = i.uv;
                float2 local = (uv - i.cell.xy) / i.cell.zw;

                if (kind < 0.5)
                {
                    // sway: a lean that grows toward the tip, with a small quicker flutter on top
                    float s = sin(t * _Speed * lerp(1.0, 1.7, saturate(wind - 1.0)) + i.data.y)
                            + 0.35 * sin(t * _Speed * 2.3 + i.data.y * 1.7);
                    float lean = (0.25 * saturate(wind - 1.0)) + s * 0.5;
                    local.x -= w * w * i.data.w * lean * lerp(0.6, 1.4, saturate(wind * 0.5));
                }
                else if (kind < 1.5)
                {
                    // wave: travelling ripple along the cloth, stronger toward the free edge
                    float ph = local.x * 7.0 - t * 5.5 * lerp(0.8, 1.4, saturate(wind - 0.5)) + i.data.y;
                    local.y += sin(ph) * i.data.w * w;
                    local.x += (cos(ph * 0.5) * 0.5 - 0.5) * i.data.w * 0.35 * w;
                }
                else
                {
                    // spin about the cell's centre
                    float ang = -(t * i.data.w * lerp(0.6, 1.6, saturate(wind * 0.5))) * 6.283 + i.data.y;
                    float2 d = local - 0.5;
                    float ca = cos(ang), sa = sin(ang);
                    local = float2(d.x * ca - d.y * sa, d.x * sa + d.y * ca) + 0.5;
                }

                // stay inside the cell: a lean never pulls the neighbouring cell's art in
                half inside = step(0.0, local.x) * step(local.x, 1.0) * step(0.0, local.y) * step(local.y, 1.0);
                float2 suv = i.cell.xy + saturate(local) * i.cell.zw;
                fixed4 c = (tex2D(_MainTex, suv) + _TextureSampleAdd) * i.color;
                c.a *= inside * MiTClip(i.mask);
                return c;
            }
        ENDCG
        }
    }
}
