// Shared pieces for the island ambience shaders (MiT/UI Water, Fall, Sway, Glow, Drift, Plume, Arc).
//
// Everything that moves on an island without costing a canvas rebuild lives in a shader driven by
// _Time: the mesh never changes, so the world canvas never re-batches for it. Globals set from C#
// (ArchipelagoView.SetEnvironment) only when they change:
//   _MiTWind   0.4 calm … 2.6 storm — how hard things sway (FieldAnimator.SwayScale)
//   _MiTNight  0 day … 1 full night
//
// Hashes are sine-free (Dave Hoskins) and every scrolling lattice wraps on a 64-cell period, so a
// session several hours long never pushes a GPU float into visible stepping.

#ifndef MIT_ISLAND_LIFE_INCLUDED
#define MIT_ISLAND_LIFE_INCLUDED

#include "UnityCG.cginc"
#include "UnityUI.cginc"

float _MiTWind;
float _MiTNight;

float4 _ClipRect;
float _UIMaskSoftnessX;
float _UIMaskSoftnessY;
fixed4 _TextureSampleAdd;

float MiTWind() { return _MiTWind <= 0.0 ? 1.0 : _MiTWind; }

// The project renders in Linear space: a colour written as a literal in a shader must be converted
// from the sRGB it was picked in (material Color properties are converted by Unity already).
half3 MiTSRGB(half3 c)
{
    #ifdef UNITY_COLORSPACE_GAMMA
    return c;
    #else
    return GammaToLinearSpace(c);
    #endif
}

float Hash21(float2 p)
{
    p = frac(p * float2(0.1031, 0.1030));
    p += dot(p, p.yx + 33.33);
    return frac((p.x + p.y) * p.x);
}

float2 Hash22(float2 p)
{
    float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.xx + q.yz) * q.zy);
}

// a lattice cell id folded into 0..63 on both axes, so a wrapped scroll is seamless
float2 WrapCell(float2 c) { return c - 64.0 * floor(c / 64.0); }

// time wrapped to a period, for lattice scrolls: t*speed stays below 64 cells
float WrapScroll(float t, float cellsPerSecond) { return fmod(t * cellsPerSecond, 64.0); }

// RectMask2D clipping, exactly as UI/Default does it
float4 MiTClipMask(float4 vertex, float4 clipPos)
{
    float2 pixelSize = clipPos.w;
    pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
    return float4(vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
}

half MiTClip(float4 mask)
{
    #ifdef UNITY_UI_CLIP_RECT
    half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(mask.xy)) * mask.zw);
    return m.x * m.y;
    #else
    return 1;
    #endif
}

#endif
