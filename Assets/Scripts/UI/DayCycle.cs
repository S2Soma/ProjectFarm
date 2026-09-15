using System;
using UnityEngine;

namespace LQFarm
{
    /// <summary>The colour of the world at one moment of the day.</summary>
    public struct SkyPalette
    {
        public Color skyTop, skyMid, skyLow, horizon;
        public Color horizonGlow; public float horizonGlowA;
        public Color seaTop, seaBottom;
        public Color cloudSeaLit, cloudSeaShade;
        public Color skyCloudLit, skyCloudShade; public float skyCloudA;
        /// <summary>Multiplied over the islands, beds, crops and props.</summary>
        public Color ambient;
        public Color sun, sunGlow; public float sunGlowA, sunScale;
        public Color moon, moonGlow; public float moonGlowA;
        public float stars;
        /// <summary>0 by day, 1 at full night. Lanterns, fireflies, sun and moon derive from it.</summary>
        public float night;
        public Color vignette; public float vignetteA;
        public float sheenA;
    }

    /// <summary>Day and night, following the device's own clock.
    ///
    /// Purely cosmetic: nothing in the economy reads it, so a player who changes their clock only
    /// changes the colour of the sky. It reads <c>DateTime.Now</c> directly — never GS.Now, which
    /// is UTC and guarded against clock tampering for the rules that DO matter. When visiting a
    /// friend the viewer's own clock is used: it is the viewer's evening.
    ///
    /// Five keyframes on a 24-hour ring, held at night and day and blended with smoothstep
    /// between:
    ///
    ///     night 19:45-04:30 · dawn 05:45 · day 07:15-16:00 · golden 17:15 · dusk 18:30</summary>
    public static class DayCycle
    {
        static Color H(string hex) { return Theme.Hex(hex); }

        static SkyPalette Key(string top, string mid, string low, string hor, string glow, float glowA,
                              string seaT, string seaB, string csLit, string csShade, string scLit, string scShade, float scA,
                              string ambient, string sun, string sunGlow, float sunGlowA, float sunScale,
                              string moonGlow, float moonGlowA, float stars, float night, string vig, float vigA, float sheen)
        {
            return new SkyPalette
            {
                skyTop = H(top), skyMid = H(mid), skyLow = H(low), horizon = H(hor),
                horizonGlow = H(glow), horizonGlowA = glowA,
                seaTop = H(seaT), seaBottom = H(seaB),
                cloudSeaLit = H(csLit), cloudSeaShade = H(csShade),
                skyCloudLit = H(scLit), skyCloudShade = H(scShade), skyCloudA = scA,
                ambient = H(ambient),
                sun = H(sun), sunGlow = H(sunGlow), sunGlowA = sunGlowA, sunScale = sunScale,
                moon = H("#F3F0DC"), moonGlow = H(moonGlow), moonGlowA = moonGlowA,
                stars = stars, night = night, vignette = H(vig), vignetteA = vigA, sheenA = sheen,
            };
        }

        static readonly SkyPalette Night = Key("#0B1433", "#172556", "#2A3E73", "#3F5A8E", "#6D86C4", 0.30f,
            "#33497E", "#121C40", "#8C9FD2", "#22305E", "#7688BF", "#222C58", 0.85f,
            "#A7B6DB", "#FFF7D6", "#FFE49A", 0.0f, 1f, "#BFD4FF", 0.35f, 1.0f, 1.0f, "#070C24", 0.42f, 0.22f);
        static readonly SkyPalette Dawn = Key("#3B4F95", "#8C7FC0", "#F0A6A6", "#FFD6A0", "#FFBE7A", 0.85f,
            "#F3C7B5", "#7F7DB4", "#FFD9BE", "#8F86B8", "#FFCDB0", "#857FB4", 1f,
            "#F3DDD6", "#FFE6B8", "#FFA866", 0.75f, 1f, "#BFD4FF", 0.0f, 0.20f, 0.55f, "#2B1F45", 0.26f, 0.35f);
        static readonly SkyPalette Day = Key("#3D8FD9", "#6DB3E8", "#A8D6F2", "#DDF0F2", "#FFF4D8", 0.45f,
            "#D6E7F5", "#9FBDE0", "#FFFFFF", "#B8CCE8", "#FFFFFF", "#C9DAF0", 1f,
            "#FFFFFF", "#FFF7D6", "#FFE49A", 0.55f, 1f, "#BFD4FF", 0.0f, 0f, 0f, "#0E2A33", 0.16f, 0.25f);
        static readonly SkyPalette Golden = Key("#3A7CC7", "#7FAEDD", "#F2D2A2", "#FFC273", "#FFB257", 0.80f,
            "#F7DDB6", "#A99AC6", "#FFE3B3", "#B29FC6", "#FFE0A6", "#BFA3BC", 1f,
            "#FFEBD2", "#FFE9A8", "#FFB04A", 0.75f, 1.12f, "#BFD4FF", 0.0f, 0f, 0.10f, "#3B2412", 0.20f, 0.45f);
        static readonly SkyPalette Dusk = Key("#25346E", "#5E4F96", "#D0708F", "#FF9A66", "#FF8452", 0.80f,
            "#E6918A", "#4B4A84", "#F2A18A", "#56508A", "#F0977E", "#4F4B88", 1f,
            "#E7D0DA", "#FFB27A", "#FF7040", 0.70f, 1f, "#BFD4FF", 0.10f, 0.35f, 0.70f, "#1D1438", 0.32f, 0.50f);

        /// <summary>The ring, in hours. Each key holds its palette exactly at that hour; between two
        /// keys the palette blends with smoothstep. Night and day appear twice to hold.</summary>
        static readonly (float hour, SkyPalette p)[] Keys =
        {
            (4.50f, Night), (5.75f, Dawn), (7.25f, Day), (16.00f, Day), (17.25f, Golden), (18.50f, Dusk), (19.75f, Night),
        };

        static float _forced = -1f;

        /// <summary>The hour shown: the forced one if a dev tool or the screenshot pass is holding
        /// it, else the local clock.</summary>
        public static float Hour
        {
            get
            {
                if (_forced >= 0f) return _forced;
                var now = DateTime.Now;
                return now.Hour + now.Minute / 60f + now.Second / 3600f;
            }
        }

        public static bool Forced => _forced >= 0f;
        public static void ForceHour(float h) { _forced = Mathf.Repeat(h, 24f); }
        public static void Release() { _forced = -1f; }

        public static SkyPalette Sample(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            int n = Keys.Length;
            for (int i = 0; i < n - 1; i++)
                if (hour >= Keys[i].hour && hour < Keys[i + 1].hour)
                    return Blend(Keys[i].p, Keys[i + 1].p, (hour - Keys[i].hour) / (Keys[i + 1].hour - Keys[i].hour));

            // the segment that wraps past midnight: last key -> first key
            var last = Keys[n - 1];
            var first = Keys[0];
            float span = first.hour + 24f - last.hour;
            float into = hour >= last.hour ? hour - last.hour : hour + 24f - last.hour;
            return Blend(last.p, first.p, into / span);
        }

        static SkyPalette Blend(SkyPalette a, SkyPalette b, float t)
        {
            t = Mathf.Clamp01(t);
            return Lerp(a, b, t * t * (3f - 2f * t));
        }

        static SkyPalette Lerp(SkyPalette a, SkyPalette b, float t)
        {
            return new SkyPalette
            {
                skyTop = Color.Lerp(a.skyTop, b.skyTop, t), skyMid = Color.Lerp(a.skyMid, b.skyMid, t),
                skyLow = Color.Lerp(a.skyLow, b.skyLow, t), horizon = Color.Lerp(a.horizon, b.horizon, t),
                horizonGlow = Color.Lerp(a.horizonGlow, b.horizonGlow, t), horizonGlowA = Mathf.Lerp(a.horizonGlowA, b.horizonGlowA, t),
                seaTop = Color.Lerp(a.seaTop, b.seaTop, t), seaBottom = Color.Lerp(a.seaBottom, b.seaBottom, t),
                cloudSeaLit = Color.Lerp(a.cloudSeaLit, b.cloudSeaLit, t), cloudSeaShade = Color.Lerp(a.cloudSeaShade, b.cloudSeaShade, t),
                skyCloudLit = Color.Lerp(a.skyCloudLit, b.skyCloudLit, t), skyCloudShade = Color.Lerp(a.skyCloudShade, b.skyCloudShade, t),
                skyCloudA = Mathf.Lerp(a.skyCloudA, b.skyCloudA, t),
                ambient = Color.Lerp(a.ambient, b.ambient, t),
                sun = Color.Lerp(a.sun, b.sun, t), sunGlow = Color.Lerp(a.sunGlow, b.sunGlow, t),
                sunGlowA = Mathf.Lerp(a.sunGlowA, b.sunGlowA, t), sunScale = Mathf.Lerp(a.sunScale, b.sunScale, t),
                moon = Color.Lerp(a.moon, b.moon, t), moonGlow = Color.Lerp(a.moonGlow, b.moonGlow, t),
                moonGlowA = Mathf.Lerp(a.moonGlowA, b.moonGlowA, t),
                stars = Mathf.Lerp(a.stars, b.stars, t), night = Mathf.Lerp(a.night, b.night, t),
                vignette = Color.Lerp(a.vignette, b.vignette, t), vignetteA = Mathf.Lerp(a.vignetteA, b.vignetteA, t),
                sheenA = Mathf.Lerp(a.sheenA, b.sheenA, t),
            };
        }

        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Sun position as a share of the canvas: x from 0 to 1, y in reference units above
        /// the horizon (negative = below it), and how visible it is at the ends of its arc. Rises
        /// 05:30, sets 18:40, noon at the top.</summary>
        public static Vector3 SunArc(float hour)
        {
            float u = (hour - 5.5f) / 13.17f;
            return Arc(u, 0.10f, 0.80f, -34f, 234f);
        }

        /// <summary>Moon from 18:00 to 06:00.</summary>
        public static Vector3 MoonArc(float hour)
        {
            float u = Mathf.Repeat(hour - 18f, 24f) / 12f;
            return Arc(u, 0.12f, 0.76f, -30f, 200f);
        }

        static Vector3 Arc(float u, float x0, float xSpan, float y0, float lift)
        {
            float vis = Smooth(-0.02f, 0.04f, u) * (1f - Smooth(0.96f, 1.02f, u));
            float x = x0 + xSpan * Mathf.Clamp01(u);
            float y = y0 + lift * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u));
            return new Vector3(x, y, vis);
        }

        public static float LanternLight(float night) { return Smooth(0.30f, 0.90f, night); }
        public static float SunAlpha(float night) { return 1f - Smooth(0.6f, 0.9f, night); }
        public static float MoonAlpha(float night) { return Smooth(0.5f, 0.9f, night); }

        /// <summary>Rec. 709 luma on gamma values — the readability rules are written in it.</summary>
        public static float Luma(Color c) { return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; }

        /// <summary>The readability floor: the land never drops below luma 0.50 once the weather's
        /// own tint and wash are counted, and crops take 30% less of the darkness and never drop
        /// below 0.62. Night has to look like night without hiding the one thing on screen the
        /// player came to look at.</summary>
        public static void FloorAmbient(Color ambient, Color landTint, Color wash, out Color land, out Color crop)
        {
            float washLoss = wash.a * (1f - Luma(wash));
            float L = Luma(ambient) * Luma(landTint) * (1f - washLoss);
            land = ambient;
            if (L < 0.50f && L > 0.0001f) land = Scale(ambient, 0.50f / L);

            crop = Color.Lerp(land, Color.white, 0.30f);
            float Lc = Luma(crop) * Luma(landTint) * (1f - washLoss);
            if (Lc < 0.62f && Lc > 0.0001f) crop = Scale(crop, 0.62f / Lc);
            land.a = crop.a = 1f;
        }

        static Color Scale(Color c, float k)
        {
            return new Color(Mathf.Min(1f, c.r * k), Mathf.Min(1f, c.g * k), Mathf.Min(1f, c.b * k), 1f);
        }
    }
}
