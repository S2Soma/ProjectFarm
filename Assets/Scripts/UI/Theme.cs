using System;
using System.Collections.Generic;
using UnityEngine;

namespace LQFarm
{
    /// <summary>Palette and procedurally generated chrome. No UI art is loaded from disk,
    /// so the interface can be restyled by editing this one file.</summary>
    public static class Theme
    {
        // ---- palette ----
        public static readonly Color Ink       = Hex("#2A3A33");
        public static readonly Color InkSoft   = Hex("#5E7269");
        public static readonly Color Cream     = Hex("#FFFAF0");
        public static readonly Color Cream2    = Hex("#F3E9D6");
        public static readonly Color Cream3    = Hex("#E7D9BF");
        public static readonly Color Green     = Hex("#4FB36B");
        public static readonly Color GreenDeep = Hex("#2F7D4E");
        public static readonly Color GreenDark = Hex("#235F3C");
        public static readonly Color Amber     = Hex("#F5A524");
        public static readonly Color AmberDeep = Hex("#C77C12");
        public static readonly Color Blue      = Hex("#3E9BD8");
        public static readonly Color BlueDeep  = Hex("#2A74A6");
        public static readonly Color Red       = Hex("#E2574C");
        public static readonly Color RedDeep   = Hex("#B03A31");
        public static readonly Color Purple    = Hex("#8E64D8");
        public static readonly Color Teal      = Hex("#37B5A6");
        public static readonly Color Scrim     = new Color(0.04f, 0.09f, 0.07f, 0.62f);
        /// <summary>Rãnh thanh tiến độ — đủ tối để đọc được trên nền kem.</summary>
        public static readonly Color TrackDark = new Color(0.32f, 0.26f, 0.20f, 0.45f);

        /// <summary>The HUD's one glass tone.
        ///
        /// Every floating surface over the farm uses it, at one alpha. They were 0.60 and 0.62
        /// before, which is invisible as a difference but visible as INCONSISTENCY: the same
        /// translucency over pale sky and over dark sea reads as two different materials, and two
        /// chips side by side in the same cluster looked like one was disabled. 0.78 also lifts
        /// white lettering clear of a bright noon sky.</summary>
        public static readonly Color Glass      = new Color(0.07f, 0.17f, 0.14f, 0.78f);
        public static readonly Color GlassDeep  = new Color(0.05f, 0.13f, 0.11f, 0.88f);
        /// <summary>Track colour for a bar drawn ON glass — TrackDark is a warm brown built for
        /// cream panels and vanishes here.</summary>
        public static readonly Color TrackGlass = new Color(1f, 1f, 1f, 0.22f);

        // sky / ground
        public static readonly Color SkyTop    = Hex("#5FC2EE");
        public static readonly Color SkyBottom = Hex("#BFEBFF");
        public static readonly Color GrassTop  = Hex("#8FD46B");
        public static readonly Color GrassBot  = Hex("#5CA347");
        public static readonly Color Water     = Hex("#49B6E0");
        public static readonly Color SkyHigh   = Hex("#3AA6DE");
        public static readonly Color SkyMid    = Hex("#7FCCEF");
        public static readonly Color SkyLow    = Hex("#C6EBFB");
        public static readonly Color SkyHaze   = Hex("#E8F6FD");
        public static readonly Color WaterDeep = Hex("#1F6E9C");
        public static readonly Color Haze      = Hex("#BFE4F5");

        /// <summary>Rarity accents: thường / hiếm / sử thi / huyền thoại.</summary>
        public static readonly Color[] Rarity =
        {
            Hex("#6FB98F"), Hex("#4E8FD6"), Hex("#9A6BE0"), Hex("#E0713F"),
        };

        public static Color Hex(string s)
        {
            ColorUtility.TryParseHtmlString(s, out var c);
            return c;
        }

        public static Color Alpha(this Color c, float a) { c.a = a; return c; }

        // ---- chrome ----
        // Every shape below is a real asset in Resources/Art/chrome, baked by
        // Tools/gen_chrome.py. Nothing is synthesised into a texture at runtime any
        // more, so startup does no pixel work and each shape can be redrawn by hand
        // or replaced with commissioned art, one file at a time.
        static Sprite C(string n) { return Art.Load("Art/chrome/" + n); }

        /// <summary>Corner radii are quantised so the whole interface shares four plates.</summary>
        static readonly int[] Radii = { 6, 12, 18, 26 };

        static int Quantise(int radius)
        {
            int best = Radii[0];
            for (int i = 1; i < Radii.Length; i++)
                if (Mathf.Abs(Radii[i] - radius) < Mathf.Abs(best - radius)) best = Radii[i];
            return best;
        }

        public static Sprite Round(int radius) { return C("round_" + Quantise(radius)); }

        public static Sprite Shadow(int radius, int blur)
        {
            return C(Quantise(radius) >= 22 ? "shadow_26" : "shadow_18");
        }

        public static Sprite Circle()   { return C("circle"); }
        public static Sprite Ring(float thickness01) { return C(thickness01 < 0.14f ? "ring_12" : "ring_17"); }
        public static Sprite Glow()     { return C("glow"); }
        public static Sprite Vignette() { return C("vignette"); }
        public static Sprite Island()   { return C("island"); }
        public static Sprite Sky()      { return C("sky"); }
        public static Sprite Sea()      { return C("sea"); }

        // ---- art skin ----
        /// <summary>Structural chrome comes from the Kenney CC0 packs in Resources/Art/ui2.
        /// Only the load-bearing pieces are skinned — cards, wells, buttons and bars. Plain
        /// tinted plates stay procedural, because a tinted wooden panel reads as a mistake.</summary>
        public enum Tone { Green, Amber, Blue, Red, Grey }

        public static class Skin
        {
            static Sprite S(string n) { return Art.Load("Art/ui2/" + n); }

            public static Sprite Panel      => S("panel_beige");
            public static Sprite PanelLight => S("panel_cream");
            public static Sprite Header     => S("panel_brown");
            public static Sprite Inset      => S("panel_inset_brown");
            public static Sprite Banner     => S("banner");

            public static Sprite BarTrack   => S("bar_track");
            public static Sprite BarGreen   => S("bar_green");
            public static Sprite BarBlue    => S("bar_blue");
            public static Sprite BarWhite   => S("bar_white");

            public static Sprite IconCross  => S("icon_cross");
            public static Sprite IconCheck  => S("icon_check");
            public static Sprite ArrowLeft  => S("arrow_left");
            public static Sprite ArrowRight => S("arrow_right");
            public static Sprite StarGold   => S("star_gold");
            public static Sprite Coin       => S("coin");
            public static Sprite Lock       => S("lock");

            // bộ icon điều hướng, CC0 — thay cho sheet gốc chưa rõ nguồn gốc
            public static Sprite NavShop    => S("nav_shop");
            public static Sprite NavMagic   => S("nav_magic");   // bình năng lượng
            public static Sprite Chest      => S("chest");
            public static Sprite ChestOpen  => S("chest_open");

            // drawn for this project — no CC0 pack carried a droplet, and the pack's tick
            // was a hairline while its exclamation was an 8x16 source
            public static Sprite Droplet    => Art.Load("Art/gen/droplet");
            public static Sprite Check      => Art.Load("Art/gen/check");
            public static Sprite Alert      => Art.Load("Art/gen/alert");
            public static Sprite More       => Art.Load("Art/gen/more");
            public static Sprite NavQuest   => S("nav_quest");
            public static Sprite NavSeeds   => S("nav_seeds");
            public static Sprite NavStore   => S("nav_store");
            public static Sprite NavFriends => S("nav_friends");
            public static Sprite NavAlbum   => S("nav_album");
            public static Sprite Crown      => S("crown");
            public static Sprite AvatarRing => S("avatar_ring");
            /// <summary>Original farmer avatar, generated by Tools/gen_avatar.py.</summary>
            public static Sprite Farmer     => Art.Load("Art/gen/farmer");
            public static Sprite Farmhouse  => S("farmhouse");

            static readonly string[] Names = { "green", "amber", "blue", "red", "grey" };

            public static Sprite Button(Tone t) { return S("btn_" + Names[(int)t]); }
            public static Sprite Round(Tone t)  { return S("round_" + Names[(int)t]); }

            /// <summary>Maps a palette colour onto the nearest button tone.</summary>
            public static Tone ToneOf(Color c)
            {
                Color[] anchors = { Green, Amber, Blue, Red, Cream3 };
                int best = 0;
                float bestD = float.MaxValue;
                for (int i = 0; i < anchors.Length; i++)
                {
                    var a = anchors[i];
                    float dr = a.r - c.r, dg = a.g - c.g, db = a.b - c.b;
                    float dist = dr * dr + dg * dg + db * db;
                    if (dist < bestD) { bestD = dist; best = i; }
                }
                return (Tone)best;
            }

            /// <summary>The fill sprite that best matches a bar colour.</summary>
            public static Sprite BarFor(Color c)
            {
                switch (ToneOf(c))
                {
                    case Tone.Green: return BarGreen;
                    case Tone.Blue:  return BarBlue;
                    default:         return BarWhite;
                }
            }
        }

        // ---- font ----
        static Font _font;
        /// <summary>A dynamic OS font, so Vietnamese diacritics always render.</summary>
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = UnityEngine.Font.CreateDynamicFontFromOSFont(
                        new[] { "Arial", "Helvetica", "Helvetica Neue", "Roboto", "Noto Sans",
                                "DejaVu Sans", "Liberation Sans", "sans-serif" }, 42);
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }
    }
}
