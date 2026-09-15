// UI/Default with its geometry moved in the vertex shader, for sky things that move every frame
// (SkyView's far islets bobbing and sliding with the camera, the sun's slowly turning rays).
// Moving their RectTransforms re-batched the whole sky canvas every frame; setting a material
// property re-batches nothing.
//   _Offset  canvas units added to the vertex position (xy)
//   _Spin    radians per second the texture turns about the quad's centre (from _Time)
Shader "MiT/UI Cloud Float"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Offset ("Offset (canvas units)", Vector) = (0, 0, 0, 0)
        _Spin ("Spin (rad/s)", Float) = 0

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
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _Offset;
            float _Spin;

            v2f vert(appdata_t v)
            {
                v2f o;
                v.vertex.xy += _Offset.xy;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                float ang = fmod(_Time.y * _Spin, 6.2831853);
                float c = cos(ang), s = sin(ang);
                float2 d = v.texcoord - 0.5;
                o.uv = _Spin != 0 ? float2(c * d.x - s * d.y, s * d.x + c * d.y) + 0.5 : v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;
                return col;
            }
        ENDCG
        }
    }
}
