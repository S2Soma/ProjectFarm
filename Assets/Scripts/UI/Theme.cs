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

        public static Sprite Circle()   { return C("circle"); }
        public static Sprite Ring(float thickness01) { return C(thickness01 < 0.14f ? "ring_12" : "ring_17"); }
        public static Sprite Glow()     { return C("glow"); }
        public static Sprite Vignette() { return C("vignette"); }

        // ---- art skin ----
        /// <summary>Surfaces are the material system now (Surface.cs, <see cref="Looks"/>); the
        /// Kenney CC0 pack in Resources/Art/ui2 supplies only glyphs — navigation icons, arrows,
        /// the cross. A tone names which button palette a colour maps to.</summary>
        public enum Tone { Green, Amber, Blue, Red, Grey }

        public static class Skin
        {
            static Sprite S(string n) { return Art.Load("Art/ui2/" + n); }

            public static Sprite IconCross  => S("icon_cross");
            public static Sprite ArrowLeft  => S("arrow_left");
            public static Sprite ArrowRight => S("arrow_right");
            public static Sprite StarGold   => S("star_gold");
            /// <summary>Tools/gen_items.py. The Kenney coin glyph read as a fidget spinner.</summary>
            public static Sprite Coin       => Art.Item("coin");
            public static Sprite Lock       => S("lock");

            // bộ icon điều hướng, CC0 — thay cho sheet gốc chưa rõ nguồn gốc
            public static Sprite NavShop    => S("nav_shop");
            public static Sprite NavMagic   => S("nav_magic");   // bình năng lượng

            // drawn for this project — no CC0 pack carried a droplet, and the pack's tick
            // was a hairline while its exclamation was an 8x16 source
            public static Sprite Droplet    => Art.Load("Art/gen/droplet");
            public static Sprite Check      => Art.Load("Art/gen/check");
            public static Sprite Alert      => Art.Load("Art/gen/alert");
            public static Sprite More       => Art.Load("Art/gen/more");
            public static Sprite NavQuest   => S("nav_quest");
            public static Sprite NavSeeds   => S("nav_seeds");
            /// <summary>The warehouse is the barn. The pack's "store" glyph was a stack of gold bars,
            /// which in a game with coins read as money, not as the place produce goes.</summary>
            public static Sprite NavStore   => S("farmhouse");
            public static Sprite NavFriends => S("nav_friends");
            /// <summary>Tools/gen_icons.py: a book with a star. The pack's album was an empty card
            /// frame that read as a box at menu size.</summary>
            public static Sprite NavAlbum   => Art.Load("Art/gen/nav_album2");
            /// <summary>Tools/gen_icons.py: an arrow up. Upgrade used the barn, so the menu had two
            /// buildings and no way to tell "level up" from "warehouse".</summary>
            public static Sprite NavUpgrade => Art.Load("Art/gen/nav_upgrade");
            public static Sprite Crown      => S("crown");
            public static Sprite AvatarRing => S("avatar_ring");
            /// <summary>Original farmer avatar, generated by Tools/gen_avatar.py.</summary>
            public static Sprite Farmer     => Art.Load("Art/gen/farmer");
            public static Sprite Farmhouse  => S("farmhouse");

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

        }

        // ---- font ----
        // Baloo 2 (SIL OFL, Resources/Fonts), cut into three static weights by Tools/make_fonts.py,
        // which also rescales it to Nunito's size and line metrics — every label was laid out for
        // Nunito, the face it replaced. A rounded, chunky face is most of what separates a friendly
        // farm game from a settings screen. It carries every Vietnamese stacked diacritic; the
        // script's subset keeps Latin and Vietnamese only.
        //
        // Weights are chosen per label rather than via FontStyle.Bold: a legacy dynamic font with
        // no bold face gets SYNTHETIC bold, which smears the counters of a heavy rounded face.
        static Font _body, _strong, _heavy, _os;

        static Font OsFallback()
        {
            if (_os == null)
            {
                _os = UnityEngine.Font.CreateDynamicFontFromOSFont(
                    new[] { "Arial", "Helvetica", "Roboto", "Noto Sans", "sans-serif" }, 42);
                if (_os == null) _os = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return _os;
        }

        static Font LoadFont(ref Font slot, string name)
        {
            if (slot == null) slot = Resources.Load<Font>("Fonts/" + name);
            return slot != null ? slot : OsFallback();
        }

        /// <summary>Body copy: Baloo 2 SemiBold. Regular is too thin to survive on painted art.</summary>
        public static Font Font => LoadFont(ref _body, "Baloo2-SemiBold");
        /// <summary>Small emphasis: Baloo 2 Bold.</summary>
        public static Font FontStrong => LoadFont(ref _strong, "Baloo2-Bold");
        /// <summary>Titles, numbers and buttons: Baloo 2 ExtraBold.</summary>
        public static Font FontHeavy => LoadFont(ref _heavy, "Baloo2-ExtraBold");

        /// <summary>Bold below 20 px is Bold, not ExtraBold: the heaviest cut closes the counters of
        /// "e" and "a" at caption sizes.</summary>
        public static Font FontFor(FontStyle style, int size)
        {
            if (style == FontStyle.Bold || style == FontStyle.BoldAndItalic)
                return size >= 20 ? FontHeavy : FontStrong;
            return Font;
        }

        /// <summary>Outline for white lettering on the HUD glass — the glass's own darkest tone
        /// rather than black, so the letter edge reads as part of the surface.</summary>
        public static readonly Color GlassInk = Hex("#0E2A2A");
    }

    /// <summary>The material table (see Surface.cs). Six materials, and every surface in the game
    /// is one of them — which is the whole point: the old HUD had five different treatments for
    /// "a thing floating over the farm".</summary>
    public static class Looks
    {
        static Color H(string hex, float a = 1f) { return Theme.Hex(hex).Alpha(a); }

        /// <summary>M1 — HUD glass. Near-opaque so its hue no longer depends on the sky or grass
        /// behind it; a teal gradient, a dark 2 px edge and a white rim along the top.</summary>
        public static readonly Look Glass = new Look
        {
            top = H("#2E6A6B", 0.95f), bottom = H("#17393C", 0.96f),
            edge = H("#0A2224", 0.62f), edgeW = 2f,
            rim = new Color(1f, 1f, 1f, 0.26f), rimW = 2f, rimFade = 0.5f,
            shadow = new Color(0f, 0f, 0f, 0.28f), blur = 12f, drop = new Vector2(0f, -4f),
            ink = Color.white, inkLine = Theme.GlassInk,
        };

        /// <summary>M3 — paper: sheets, modal cards, popups, the tray bubble.</summary>
        public static readonly Look Paper = new Look
        {
            top = H("#FFF9EC"), bottom = H("#F4E6CB"),
            edge = H("#D6B98A"), edgeW = 3f,
            rim = new Color(1f, 1f, 1f, 0.9f), rimW = 2f, rimFade = 0.22f,
            shadow = new Color(0f, 0f, 0f, 0.34f), blur = 28f, drop = new Vector2(0f, -10f),
            ink = Theme.Ink,
        };

        /// <summary>M4 — the header ribbon docked across the top of a card.</summary>
        public static readonly Look Ribbon = new Look
        {
            top = H("#B8804F"), bottom = H("#8E5B33"),
            edge = H("#62391C"), edgeW = 0f,
            rim = H("#FFD9A8", 0.55f), rimW = 2f, rimFade = 0.35f,
            lip = 4f, lipColor = H("#62391C"),
            ink = H("#FFF4DC"), inkLine = H("#5A3218"),
        };

        /// <summary>M4 in green, for the reward card: a celebration, not a destination.</summary>
        public static readonly Look RibbonGreen = new Look
        {
            top = H("#5CC46F"), bottom = H("#2F8F4C"),
            rim = H("#D2F7D8", 0.5f), rimW = 2f, rimFade = 0.35f,
            lip = 4f, lipColor = H("#1C6A35"),
            ink = Color.white, inkLine = H("#1C5E33"),
        };

        /// <summary>M5 — a recessed well or track. The dark crescent along the top is the inner
        /// shadow; the 1 px light lip along the bottom is the edge catching light.</summary>
        public static readonly Look Well = new Look
        {
            top = H("#E6D5B5"), bottom = H("#F0E4CB"),
            edge = H("#6B4A2A", 0.26f), edgeW = 0f, inTop = 4f,
            lip = 1f, lipColor = new Color(1f, 1f, 1f, 0.7f),
        };

        /// <summary>A list row sitting in a well: flat cream, no border, a 2 px contact shadow.</summary>
        public static readonly Look Row = new Look
        {
            top = H("#FFFDF8"), bottom = H("#FBF2E1"),
            shadow = new Color(0.30f, 0.20f, 0.08f, 0.16f), blur = 5f, drop = new Vector2(0f, -2f),
            ink = Theme.Ink,
        };

        /// <summary>A cell selected inside a well or on a card (tabs, chest tiers).</summary>
        public static readonly Look Segment = new Look
        {
            top = H("#FFFDF8"), bottom = H("#F7EBD3"),
            edge = H("#D9BF93"), edgeW = 2f,
            shadow = new Color(0.30f, 0.20f, 0.08f, 0.18f), blur = 6f, drop = new Vector2(0f, -2f),
            ink = Theme.Ink,
        };

        static Look Btn(string top, string bottom, string dark)
        {
            var d = H(dark);
            return new Look
            {
                top = H(top), bottom = H(bottom),
                edge = d, edgeW = 2f,
                rim = new Color(1f, 1f, 1f, 0.45f), rimW = 2f, rimFade = 0.5f,
                lip = 6f, lipColor = d * new Color(0.86f, 0.86f, 0.86f, 1f),
                shadow = new Color(0f, 0f, 0f, 0.25f), blur = 8f, drop = new Vector2(0f, -3f),
                ink = Color.white, inkLine = d,
            };
        }

        /// <summary>M6 — button faces. Lettering is always white with an outline in the lip's
        /// colour: dark ink on amber ("Gieo") was the one label on the bar that looked disabled.</summary>
        public static readonly Look BtnGreen = Btn("#6BDB85", "#2FA956", "#1C7439");
        public static readonly Look BtnAmber = Btn("#FFCF52", "#F0961C", "#AE620B");
        public static readonly Look BtnBlue  = Btn("#62C6F6", "#2B8BD3", "#1B5C96");
        public static readonly Look BtnRed   = Btn("#FF7D70", "#DC4337", "#98281F");
        public static readonly Look BtnOff   = Btn("#DAD3C5", "#BFB6A5", "#8F8573");
        /// <summary>A cream disc for neutral controls (the menu button, the island arrows): light
        /// enough for a dark glyph, and not the disabled grey.</summary>
        public static readonly Look BtnCream = Btn("#FFFDF6", "#E6DCC7", "#9C8E74");

        public static Look Button(Theme.Tone t)
        {
            switch (t)
            {
                case Theme.Tone.Green: return BtnGreen;
                case Theme.Tone.Amber: return BtnAmber;
                case Theme.Tone.Blue:  return BtnBlue;
                case Theme.Tone.Red:   return BtnRed;
                default:               return BtnOff;
            }
        }
    }
}
