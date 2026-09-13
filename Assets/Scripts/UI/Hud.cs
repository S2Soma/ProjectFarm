using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>The permanent on-screen furniture: player card, currency chips,
    /// the two side rails of shortcuts and the primary action bar.</summary>
    public class Hud
    {
        public RectTransform Root;
        RectTransform _warehouseBtn;

        Text _coin, _energy, _mission, _level, _xpText;
        Image _xpFill, _energyFill, _chestIcon;
        Text _chestCount;
        RectTransform _missionChip;

        long _lastCoin = -1;
        string _lastMission = "";

        public Vector3 WarehouseWorld => _warehouseBtn != null ? _warehouseBtn.position : Vector3.zero;

        public void Build(RectTransform parent, GameApp app)
        {
            Root = UIKit.Node("hud", parent);
            Root.Stretch();

            BuildPlayerCard(app);
            BuildSeasonBar(app);
            BuildCurrencies(app);
            BuildMissionChip(app);
            BuildRails(app);
            BuildActionBar(app);
        }

        // ------------------------------------------------------------
        // Thanh Mùa Vụ — weather and crop tags, in the one free band on screen
        // ------------------------------------------------------------
        RectTransform _season;
        Image _wIcon, _wProg;
        Text _wClock, _wName, _wMulA, _wMulB, _tCount, _tClock;
        readonly List<Image> _tPips = new List<Image>();
        RectTransform _wNext, _greenBadge;
        Text _greenText;
        Image _wNextIcon;
        Text _wNextMark;
        string _lastWClock = "", _lastWName = "";

        /// <summary>Weather and tags share one frame but stay two cells.
        ///
        /// They must read as one combined rate — they multiply together — but collapsing them
        /// into a single number would be a lie: weather multiplies all twenty-eight crops, a tag
        /// multiplies six. One "x2.4" on the HUD is correct for six crops and wrong for
        /// twenty-two, and a permanent readout that is wrong most of the time is worse than two
        /// honest cells. The clocks differ too (one hour against twelve), and one number under
        /// one countdown asserts one expiry.
        ///
        /// The actual product appears in the three places where the crop is known and the number
        /// is therefore true: the plant popup, the season sheet, and the harvest.
        ///
        /// Anchored TopLeft against the player card, never Top. The notch inset applies to one
        /// side only, so a Top-anchored bar drifts up to 54 px off centre on a notched phone and
        /// lands lopsided between two clusters that did not move with it.</summary>
        void BuildSeasonBar(GameApp app)
        {
            _season = UIKit.Node("season", Root);
            _season.Anchor(UIKit.TopLeft, new Vector2(376, -14), new Vector2(480, 76));
            _season.pivot = new Vector2(0, 1);

            var bg = UIKit.Round(_season, Theme.Glass, 26, "bg");
            bg.rectTransform.Stretch();

            // 76 tall visually, 96 tall to the finger: the overhang lands in empty sky where
            // nothing competes for it, and it clears the 88 px minimum target.
            var hit = UIKit.Node("hit", _season);
            hit.Anchor(UIKit.Center, Vector2.zero, new Vector2(480, 96));
            var hitImg = hit.gameObject.AddComponent<Image>();
            hitImg.color = new Color(0, 0, 0, 0);
            var btn = hit.gameObject.AddComponent<Button>();
            btn.targetGraphic = hitImg;
            btn.onClick.AddListener(() => app.Open(new SeasonPanel(app)));

            // ---- cell A: weather ----
            _wIcon = UIKit.Img(_season, Art.WeatherIcon(Weather.Sunny), Color.white, "wIcon");
            _wIcon.preserveAspect = true;
            _wIcon.rectTransform.Anchor(UIKit.Left, new Vector2(34, -4), new Vector2(42, 42));

            // Greenhouse charges ride on the weather glyph, because bad weather is the only thing
            // they do anything about. Same corner-badge idiom as the chest count on the flask, and
            // the bar has no spare width for a cell of its own.
            _greenBadge = UIKit.Node("green", _season);
            // Straddles the bar's top edge above the glyph. Parked at y 22 it covered the top of
            // the weather icon — the one thing on the bar it is meant to annotate.
            _greenBadge.Anchor(UIKit.Left, new Vector2(24, 36), new Vector2(50, 22));
            UIKit.Round(_greenBadge, Theme.GreenDeep, 12, "bg").rectTransform.Stretch();
            var gi = UIKit.Img(_greenBadge, Theme.Skin.Farmhouse, Color.white, "ic");
            gi.preserveAspect = true;
            gi.rectTransform.Anchor(UIKit.Left, new Vector2(13, 0), new Vector2(18, 18));
            _greenText = UIKit.LabelOutlined(_greenBadge, "", 14, Color.white, TextAnchor.MiddleRight);
            _greenText.rectTransform.Stretch(24, 0, 5, 0);
            _greenBadge.gameObject.SetActive(false);

            // The countdown is the largest glyph in the bar because it is the only live variable.
            // The six multiplier sets are a static table a player memorises in a week; the clock
            // is what drives the one decision weather creates — plant now, or wait for the reroll.
            _wClock = UIKit.Label(_season, "--:--", 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            _wClock.rectTransform.Anchor(UIKit.Left, new Vector2(110, 15), new Vector2(100, 38));

            var track = UIKit.Round(_season, new Color(1, 1, 1, 0.18f), 6, "wTrack");
            track.rectTransform.Anchor(UIKit.Left, new Vector2(108, -23), new Vector2(96, 12));
            _wProg = UIKit.Round(_season, Color.white, 6, "wProg");
            _wProg.rectTransform.Anchor(UIKit.Left, new Vector2(108, -23), new Vector2(96, 12));
            _wProg.type = Image.Type.Filled;
            _wProg.fillMethod = Image.FillMethod.Horizontal;

            _wName = UIKit.LabelOutlined(_season, "", 19, Color.white, TextAnchor.MiddleLeft);
            _wName.rectTransform.Anchor(UIKit.Left, new Vector2(216, 15), new Vector2(96, 28));

            // Percentages, not multipliers: "chín ↓15%" fits the 138 px budget where
            // "chín x0.85 · giá x1.10" does not. And the arrow is coloured by BENEFIT rather than
            // by direction — x0.85 on grow time is good, x0.85 on price is bad, and a bare
            // multiplier cannot say which.
            _wMulA = UIKit.Label(_season, "", 15, Theme.Cream2, TextAnchor.MiddleLeft);
            _wMulA.rectTransform.Anchor(UIKit.Left, new Vector2(216, -23), new Vector2(72, 22));
            _wMulB = UIKit.Label(_season, "", 15, Theme.Cream2, TextAnchor.MiddleLeft);
            _wMulB.rectTransform.Anchor(UIKit.Left, new Vector2(290, -23), new Vector2(74, 22));

            _wNext = UIKit.Node("wNext", _season);
            _wNext.Anchor(UIKit.Left, new Vector2(324, 15), new Vector2(28, 28));
            UIKit.Img(_wNext, Theme.Circle(), new Color(1, 1, 1, 0.16f), "disc").rectTransform.Stretch();
            _wNextIcon = UIKit.Img(_wNext, Art.WeatherIcon(Weather.Sunny), Color.white, "ic");
            _wNextIcon.preserveAspect = true;
            _wNextIcon.rectTransform.Stretch(5, 5, 5, 5);
            _wNextIcon.enabled = false;
            _wNextMark = UIKit.Label(_wNext, "?", 17, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, FontStyle.Bold);
            _wNextMark.rectTransform.Stretch();

            var divider = UIKit.Round(_season, new Color(1, 1, 1, 0.16f), 3, "div");
            divider.rectTransform.Anchor(UIKit.Left, new Vector2(358, 4), new Vector2(2, 48));

            // ---- cell B: tagged crops ----
            var tIcon = UIKit.Img(_season, Theme.Skin.StarGold, Color.white, "tIcon");
            tIcon.preserveAspect = true;
            tIcon.rectTransform.Anchor(UIKit.Left, new Vector2(387, 15), new Vector2(30, 30));

            // Six coloured dots say the SHAPE of today's set at a glance — "today is mostly
            // price" — without a word of text.
            for (int i = 0; i < TagSys.TaggedCount; i++)
            {
                var pip = UIKit.Round(_season, Color.white, 3, "pip");
                pip.rectTransform.Anchor(UIKit.Left, new Vector2(374 + i * 6f, -25), new Vector2(4, 4));
                _tPips.Add(pip);
            }

            _tCount = UIKit.LabelOutlined(_season, "", 19, Color.white, TextAnchor.MiddleLeft);
            _tCount.rectTransform.Anchor(UIKit.Left, new Vector2(414, 15), new Vector2(70, 28));
            _tClock = UIKit.Label(_season, "", 15, Theme.Cream2, TextAnchor.MiddleLeft);
            _tClock.rectTransform.Anchor(UIKit.Left, new Vector2(414, -23), new Vector2(70, 22));
        }

        static string Pct(float mul, bool higherIsBetter)
        {
            int p = Mathf.RoundToInt((mul - 1f) * 100f);
            if (p == 0) return "<color=#C9C2B4>—</color>";
            bool good = higherIsBetter ? p > 0 : p < 0;
            string hex = good ? "#7ED08A" : "#E88A7A";
            return $"<color={hex}>{(p > 0 ? "↑" : "↓")}{Mathf.Abs(p)}%</color>";
        }

        void RenderSeason()
        {
            if (_season == null) return;
            var s = GS.Local;
            long now = GS.Now;

            var w = WeatherSys.Now(s);
            var d = WeatherSys.Def(w);

            long left = WeatherSys.MsLeft(now);
            string clock = Fmt.Time((int)(left / 1000L));
            if (clock != _lastWClock) { _wClock.text = clock; _lastWClock = clock; }
            _wProg.fillAmount = 1f - Mathf.Clamp01(left / (float)WeatherSys.HourMs);
            _wProg.color = Theme.Hex(d.hex);

            if (d.name != _lastWName)
            {
                _wName.text = d.name;
                _wIcon.sprite = Art.WeatherIcon(w);
                _wIcon.color = Theme.Hex(d.hex);
                // Sunny is the reference state: every multiplier is 1.0, so the honest readout
                // is two dashes — which looks like missing data rather than "nothing applies".
                // Its own rule is more useful there than a pair of blanks.
                bool neutral = Mathf.Approximately(d.grow, 1f) && Mathf.Approximately(d.sell, 1f);
                if (neutral)
                {
                    _wMulA.text = "<color=#C9C2B4>bình thường</color>";
                    _wMulB.text = "";
                }
                else
                {
                    _wMulA.text = "chín " + Pct(d.grow, false);
                    _wMulB.text = "giá " + Pct(d.sell, true);
                }
                _lastWName = d.name;
            }

            bool reveal = WeatherSys.Revealed(s, now);
            _wNextMark.enabled = !reveal;
            _wNextIcon.enabled = reveal;
            if (reveal)
            {
                var nx = WeatherSys.Next(s);
                _wNextIcon.sprite = Art.WeatherIcon(nx);
                _wNextIcon.color = Theme.Hex(WeatherSys.Def(nx).hex);
            }

            // The greenhouse counter lives on the weather bar, because bad weather is the only
            // thing it does anything about. It is not on the wallet: it is not a currency, and it
            // only exists for a few minutes at a time.
            if (_greenBadge != null)
            {
                bool has = s.greenhouse > 0;
                if (_greenBadge.gameObject.activeSelf != has) _greenBadge.gameObject.SetActive(has);
                if (has) _greenText.text = "×" + s.greenhouse;
            }

            var set = TagSys.Now(s);
            _tCount.text = set.Count + " cây";
            _tClock.text = Fmt.Time((int)(TagSys.MsLeft(now) / 1000L));
            for (int i = 0; i < _tPips.Count; i++)
            {
                bool on = i < set.Count;
                _tPips[i].enabled = on;
                if (on) _tPips[i].color = Theme.Hex(TagSys.Def(set[i].tag).hex);
            }
        }

        // ------------------------------------------------------------
        // top-left cluster: who you are, and what you are working on
        // ------------------------------------------------------------
        void BuildPlayerCard(GameApp app)
        {
            var cluster = UIKit.Node("playerCluster", Root);
            cluster.Anchor(UIKit.TopLeft, new Vector2(16, -14), new Vector2(344, 122));

            // --- identity card ---
            var card = UIKit.Node("card", cluster);
            card.Anchor(UIKit.TopLeft, Vector2.zero, new Vector2(344, 74));
            card.pivot = new Vector2(0, 1);
            var bg = UIKit.Round(card, Theme.Glass, 26, "bg");
            bg.rectTransform.Stretch();

            var av = UIKit.Node("avatar", card);
            av.Anchor(UIKit.Left, new Vector2(37, 0), new Vector2(64, 64));
            UIKit.Img(av, Theme.Circle(), Theme.Cream, "disc").rectTransform.Stretch(3, 3, 3, 3);
            var pic = UIKit.Img(av, Theme.Skin.Farmer, Color.white, "pic");
            pic.preserveAspect = true;
            pic.rectTransform.Stretch(6, 6, 6, 6);
            var ring = UIKit.Img(av, Theme.Skin.AvatarRing, Color.white, "ring");
            ring.preserveAspect = true;
            ring.rectTransform.Stretch(-6, -6, -6, -6);

            var lvBadge = UIKit.Node("lv", av);
            lvBadge.Anchor(UIKit.BottomRight, new Vector2(6, -2), new Vector2(34, 26));
            UIKit.Round(lvBadge, Theme.AmberDeep, 13, "bg").rectTransform.Stretch();
            _level = UIKit.LabelOutlined(lvBadge, "1", 19, Color.white);
            _level.rectTransform.Stretch();

            var name = UIKit.LabelOutlined(card, "Nông Trại Của Bạn", 19, Color.white, TextAnchor.MiddleLeft);
            // The ring is drawn 6 px proud of the 64 px avatar, so its right edge is at 107.
            // Starting the name at 96 put the "N" underneath it.
            name.rectTransform.Anchor(UIKit.Left, new Vector2(116, 15), new Vector2(214, 22));
            name.rectTransform.pivot = new Vector2(0, 0.5f);

            var bar = UIKit.Node("xp", card);
            bar.Anchor(UIKit.Left, new Vector2(116, -12), new Vector2(214, 22));
            bar.pivot = new Vector2(0, 0.5f);
            // TrackDark is a warm brown meant for a cream panel; on the dark glass card it was
            // invisible, so the bar read as a green sliver floating in nothing.
            _xpFill = UIKit.Bar(bar, Theme.TrackGlass, Theme.Green, 11);
            _xpFill.transform.parent.GetComponent<RectTransform>().Stretch();
            _xpText = UIKit.LabelOutlined(bar, "0/0", 15, Color.white);
            _xpText.rectTransform.Stretch();

            // --- the mission you are on, docked to the card so they read as one block ---
            var strip = UIKit.Node("missionStrip", cluster);
            strip.Anchor(UIKit.TopLeft, new Vector2(0, -80), new Vector2(344, 40));
            strip.pivot = new Vector2(0, 1);
            _missionChip = strip;

            var sbg = UIKit.Round(strip, Theme.Glass, 20, "bg");
            sbg.rectTransform.Stretch();
            sbg.raycastTarget = true;

            var ic = UIKit.Img(strip, Theme.Skin.Alert, Theme.Amber, "ic");
            ic.preserveAspect = true;
            ic.rectTransform.Anchor(UIKit.Left, new Vector2(24, 0), new Vector2(24, 24));

            _mission = UIKit.LabelOutlined(strip, "", 17, Color.white, TextAnchor.MiddleLeft);
            _mission.rectTransform.Stretch(44, 0, 14, 0);

            var b = strip.gameObject.AddComponent<Button>();
            b.targetGraphic = sbg;
            b.onClick.AddListener(() => app.Open(new MissionsPanel(app)));
            strip.gameObject.AddComponent<PressFx>();
        }

        // ------------------------------------------------------------
        // top-right cluster: every resource in one aligned block
        // ------------------------------------------------------------
        void BuildCurrencies(GameApp app)
        {
            var cluster = UIKit.Node("wallet", Root);
            cluster.Anchor(UIKit.TopRight, new Vector2(-16, -14), new Vector2(392, 52));
            cluster.pivot = new Vector2(1, 1);

            // --- row 1: magic energy, then coins ---
            var enChip = UIKit.Node("energyChip", cluster);
            enChip.Anchor(UIKit.TopLeft, Vector2.zero, new Vector2(186, 52));
            enChip.pivot = new Vector2(0, 1);
            var enBg = UIKit.Round(enChip, Theme.Glass, 26, "bg");
            enBg.rectTransform.Stretch();
            enBg.raycastTarget = true;

            var chestHolder = UIKit.Node("chest", enChip);
            chestHolder.Anchor(UIKit.Left, new Vector2(28, 0), new Vector2(38, 38));
            _chestIcon = UIKit.Img(chestHolder, Theme.Skin.NavMagic, Color.white, "ic");
            _chestIcon.preserveAspect = true;
            _chestIcon.rectTransform.Stretch();

            _chestCount = UIKit.LabelOutlined(chestHolder, "", 15, Color.white, TextAnchor.LowerRight);
            _chestCount.rectTransform.Anchor(UIKit.BottomRight, new Vector2(6, -4), new Vector2(36, 18));

            var enBar = UIKit.Node("bar", enChip);
            enBar.Anchor(UIKit.Right, new Vector2(-12, -11), new Vector2(112, 12));
            // TrackGlass, not TrackDark: the brown track is built for cream panels and vanished
            // on the glass chip, so a low energy reading looked like a stray blue pixel.
            _energyFill = UIKit.Bar(enBar, Theme.TrackGlass, Theme.Purple, 6);
            _energyFill.transform.parent.GetComponent<RectTransform>().Stretch();

            _energy = UIKit.LabelOutlined(enChip, "0/300", 16, Color.white, TextAnchor.MiddleRight);
            _energy.rectTransform.Anchor(UIKit.Right, new Vector2(-12, 10), new Vector2(112, 20));

            var enBtn = enChip.gameObject.AddComponent<Button>();
            enBtn.targetGraphic = enBg;
            enBtn.onClick.AddListener(() => app.Open(new ChestPanel(app)));
            enChip.gameObject.AddComponent<PressFx>();

            var coinChip = UIKit.Node("coinChip", cluster);
            coinChip.Anchor(UIKit.TopRight, Vector2.zero, new Vector2(186, 52));
            coinChip.pivot = new Vector2(1, 1);
            _coin = UIKit.Chip(coinChip, Theme.Skin.Coin, "0", Theme.Glass, 206,
                               () => app.Open(new ShopPanel(app)));
            coinChip.GetChild(0).GetComponent<RectTransform>().Stretch();
            // The chip is 186 wide but Chip() lays its label out for the 206 it was asked for,
            // leaving 88 px — and "186.400" at 24 px bold is ~95, so every balance past six digits
            // ran left underneath the coin. Best-fit shrinks the digits instead; best-fit only
            // works with Wrap/Truncate, which is harmless for a single number.
            _coin.horizontalOverflow = HorizontalWrapMode.Wrap;
            _coin.verticalOverflow = VerticalWrapMode.Truncate;
            _coin.resizeTextForBestFit = true;
            _coin.resizeTextMinSize = 15;
            _coin.resizeTextMaxSize = 24;

            // The collection chip is gone. It was a permanent readout of a number that moves
            // a few times a week, sitting in the one cluster a player checks every few seconds,
            // and it was the third way to reach a panel that already had two. It lives in the
            // tray now, where a rarely-read number belongs.
        }

        /// <summary>Kept so Build() reads in layout order; the strip lives in the player cluster.</summary>
        void BuildMissionChip(GameApp app) { }

        // ------------------------------------------------------------
        // side rails
        // ------------------------------------------------------------
        static void RailPlate(RectTransform rail)
        {
            var bg = UIKit.Round(rail, new Color(0.08f, 0.17f, 0.14f, 0.34f), 26, "plate");
            bg.rectTransform.Stretch(-4, -8, -4, -8);
        }

        /// <summary>One rail, on the right, five slots.
        ///
        /// The old HUD had eleven buttons for eight destinations: shop reachable from the left
        /// rail AND the coin chip, chests from the rail AND the energy chip, collection from the
        /// rail AND its own chip, missions from the rail AND the strip. Three destinations were
        /// free to remove before cutting a single feature.
        ///
        /// Deleting the LEFT rail specifically buys three things. Its 112x346 block sat on the
        /// field's left edge, and once FitFarm accounts for it that width becomes sea gutter — the
        /// "there is more over there" affordance the island system needs, for free. Two symmetric
        /// rails is a mobile-web idiom anyway: look at the old screenshot and try to say why
        /// Rương was on the left and Kho on the right. And a COUNT on a rail button is worth
        /// strictly more than a second button to the same place, which is how eight destinations
        /// live behind five buttons with nothing going unnoticed.</summary>
        void BuildRails(GameApp app)
        {
            var right = UIKit.Node("railR", Root);
            right.Anchor(UIKit.Right, new Vector2(-58, -6), new Vector2(104, 476));
            RailPlate(right);

            var btns = Rail(right, new (Sprite art, string cap, Color col, Action act)[]
            {
                // With the captions gone, colour IS the label — so all five must differ. The skin
                // has exactly five button faces (green/amber/blue/red/grey) and Theme.Skin.ToneOf
                // snaps anything else to the nearest of them, so Amber and AmberDeep one slot
                // apart produced two identical yellow discs, and so would Purple or Teal. One
                // tone each, no near-misses.
                (Theme.Skin.NavSeeds, null, Theme.Green,  () => app.Open(new SeedShopPanel(app))),
                (Theme.Skin.NavStore, null, Theme.Amber,  () => app.Open(new WarehousePanel(app))),
                (Theme.Skin.NavQuest, null, Theme.Blue,   () => app.Open(new MissionsPanel(app))),
                (Theme.Skin.NavShop,  null, Theme.Red,    () => app.Open(new ShopPanel(app))),
                (Theme.Skin.More,     null, Theme.Cream3, ToggleTray),
            });
            _warehouseBtn = btns[1];
            _railDots = new RectTransform[btns.Length];
            for (int i = 0; i < btns.Length; i++) _railDots[i] = Dot(btns[i]);

            BuildTray(app, right);
        }

        /// <summary>A count on a rail button, so a destination can say it needs attention without
        /// needing a second entry point to say it.</summary>
        static RectTransform Dot(RectTransform slot)
        {
            var dot = UIKit.Node("dot", slot);
            dot.Anchor(UIKit.TopRight, new Vector2(2, 2), new Vector2(26, 26));
            UIKit.Round(dot, Theme.Red, 13, "bg").rectTransform.Stretch();
            var t = UIKit.Label(dot, "", 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch();
            dot.gameObject.SetActive(false);
            return dot;
        }

        RectTransform[] _railDots;
        RectTransform _tray, _trayScrim;

        /// <summary>Everything that is a destination rather than a verb, and is visited rarely
        /// enough not to earn a permanent slot.</summary>
        void BuildTray(GameApp app, RectTransform rail)
        {
            // An invisible full-screen catcher, so tapping the farm closes the tray instead of
            // planting a seed under it. It is a sibling BEFORE the tray, so the tray stays on top.
            _trayScrim = UIKit.Node("trayScrim", Root);
            _trayScrim.Stretch();
            var scrim = UIKit.Img(_trayScrim, null, new Color(0, 0, 0, 0.28f), "bg");
            scrim.rectTransform.Stretch();
            scrim.raycastTarget = true;
            var sb = _trayScrim.gameObject.AddComponent<Button>();
            sb.targetGraphic = scrim;
            sb.transition = Selectable.Transition.None;
            sb.onClick.AddListener(HideTray);
            _trayScrim.gameObject.SetActive(false);

            // rowH 104 put the caption baseline (-103..-124) across the top of the next row's
            // icon (-122). Vietnamese needs 21 px of line box for a 14 px font, so the row has to
            // be cell + caption + air, not cell + 20.
            const float cell = 84f, gap = 12f, pad = 18f, rowH = 114f;
            var items = new (Sprite art, string cap, Color col, Action act)[]
            {
                // One tone each again — ToneOf only has five to give.
                (Theme.Skin.Chest,      "Rương",    Theme.Amber, () => app.Open(new ChestPanel(app))),
                (Theme.Skin.NavFriends, "Bạn bè",   Theme.Green, () => app.Open(new FriendsPanel(app))),
                (Theme.Skin.NavAlbum,   "Sưu tập",  Theme.Red,   () => app.Open(new CollectionPanel(app))),
                (Theme.Skin.Farmhouse,  "Nâng cấp", Theme.Blue,  () => app.Open(new UpgradePanel(app))),
                // No "Mùa vụ" here: the season bar across the top already opens that panel, and
                // adding a second door to it would reintroduce exactly the duplication that
                // deleting the left rail was about.
            };

            const int cols = 3;
            int rows = (items.Length + cols - 1) / cols;
            float w = pad * 2 + cols * cell + (cols - 1) * gap;
            float h = pad + rows * rowH + 8f;

            _tray = UIKit.Node("tray", Root);
            // The rail's left edge is at -110; -118 left the tray touching it. -130 reads as a
            // panel that opened NEXT TO the rail rather than out of it.
            _tray.Anchor(UIKit.Right, new Vector2(-130, -6), new Vector2(w, h));
            UIKit.Round(_tray, Theme.GlassDeep, 24, "bg").rectTransform.Stretch();

            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                int col = i % cols, row = i / cols;
                float x = pad + col * (cell + gap);
                float y = -pad - row * rowH;

                var slot = UIKit.Node("t" + i, _tray);
                slot.Anchor(UIKit.TopLeft, new Vector2(x, y), new Vector2(cell, cell));
                var b = UIKit.IconBtn(slot, it.art, it.col, 0.52f, () => { HideTray(); it.act(); }, null,
                                      Theme.Skin.ToneOf(it.col) == Theme.Tone.Grey ? Theme.Ink : (Color?)null);
                b.GetComponent<RectTransform>().Stretch();

                // 20 px of line box for a 14 px font: Vietnamese stacks two diacritics, so a box
                // sized to the font clips the top of every "ươ" and "ậ" on the row.
                var cap = UIKit.Label(_tray, it.cap, 14, Color.white, TextAnchor.UpperCenter);
                cap.rectTransform.Anchor(UIKit.TopLeft, new Vector2(x - 6, y - cell - 1), new Vector2(cell + 12, 21));
            }

            _tray.gameObject.SetActive(false);
        }

        void ToggleTray() { SetTray(!_tray.gameObject.activeSelf); }
        public void HideTray() { SetTray(false); }
        /// <summary>Opened by the screenshot pass; the tray has no other scripted entry point.</summary>
        public void ShowTrayForAudit() { SetTray(true); }

        void SetTray(bool on)
        {
            if (_tray == null) return;
            _tray.gameObject.SetActive(on);
            _trayScrim.gameObject.SetActive(on);
            if (on) _tray.SetAsLastSibling();
        }

        RectTransform[] Rail(RectTransform holder, (Sprite art, string cap, Color col, Action act)[] items)
        {
            // 104 -> 92 once the captions are gone. That 12 px per slot is exactly what
            // makes a fifth slot fit between the wallet cluster and the action bar.
            const float step = 92f;
            float total = items.Length * step;
            var outp = new RectTransform[items.Length];

            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var slot = UIKit.Node("slot", holder);
                slot.Anchor(UIKit.Center, new Vector2(0, total / 2f - step * (i + 0.5f)), new Vector2(80, 80));
                // No captions. They overhung the button below by 7 px, pushed the rail to 6 px
                // from the safe edge, and are read exactly once per player lifetime.
                // Dark ink on the grey face; white everywhere else. See IconBtn's iconTint.
                var b = UIKit.IconBtn(slot, it.art, it.col, 0.52f, it.act, null,
                                      Theme.Skin.ToneOf(it.col) == Theme.Tone.Grey ? Theme.Ink : (Color?)null);
                b.GetComponent<RectTransform>().Stretch();
                outp[i] = slot;
            }
            return outp;
        }

        // ------------------------------------------------------------
        // bottom: primary actions
        // ------------------------------------------------------------
        Text _harvestLbl, _waterLbl, _plantLbl, _islandLbl;
        Color _harvestInk, _waterInk, _plantInk;
        Button _harvestBtn, _waterBtn, _plantBtn, _islePrev, _isleNext;
        GameApp _app;

        /// <summary>Four slots: three verbs and the island pager.
        ///
        /// **Each verb carries its own count**, which is the change that matters. "Thu hoạch tất
        /// cả" is a promise the game cannot keep — a player taps it with nothing ripe and learns
        /// the button lies. "Thu hoạch · 7" is a readout first and a button second, so the bottom
        /// of the screen answers "is there anything to do" without being tapped, and a farm with
        /// nothing pending says so by going quiet instead of by doing nothing when pressed.
        ///
        /// **Tưới joins them.** Watering was the one mechanic with no bulk verb, so it was the
        /// one players skipped, which made the whole timed-window system decorative. It sits in
        /// the middle because that is where the thumb is.
        ///
        /// **Nâng cấp leaves.** It is a destination, not a verb, and it held a permanent slot
        /// next to two actions taken hundreds of times a session for one taken once an hour.
        /// It is in the tray.
        ///
        /// **The pager is here and not on the map** because the farm the player is looking at is
        /// the farm the three verbs act on. Naming it next to them is what keeps "Thu hoạch · 7"
        /// unambiguous when there are six islands.</summary>
        RectTransform _bar, _pagerRt;
        const float VerbW = 196f, PagerW = 268f, BarGap = 12f;
        string _barLayout = "";

        /// <summary>Builds the three bulk verbs and the pager. Which verbs are SHOWN is decided
        /// every render — see <see cref="RenderActions"/>.</summary>
        void BuildActionBar(GameApp app)
        {
            _app = app;
            _bar = UIKit.Node("actions", Root);
            _bar.Anchor(UIKit.Bottom, new Vector2(0, 14), new Vector2(PagerW, 72));

            // a plate so the row reads as one bar instead of buttons floating on the island edge
            var plate = UIKit.Round(_bar, new Color(0.08f, 0.17f, 0.14f, 0.32f), 28, "plate");
            plate.rectTransform.Stretch(-14, -10, -14, -10);

            _harvestBtn = UIKit.Btn(_bar, "Thu hoạch", Theme.Green, Theme.GreenDark, 23, 26,
                                    () => { HideTray(); app.HarvestAll(); });
            _harvestLbl = UIKit.BtnLabel(_harvestBtn);
            _harvestInk = _harvestLbl.color;

            _waterBtn = UIKit.Btn(_bar, "Tưới", Theme.Blue, Theme.BlueDeep, 23, 26,
                                  () => { HideTray(); app.WaterAll(); });
            _waterLbl = UIKit.BtnLabel(_waterBtn);
            _waterInk = _waterLbl.color;

            _plantBtn = UIKit.Btn(_bar, "Gieo", Theme.Amber, Theme.AmberDeep, 23, 26,
                                  () => { HideTray(); app.PlantAll(); });
            _plantLbl = UIKit.BtnLabel(_plantBtn);
            _plantInk = _plantLbl.color;

            foreach (var b in new[] { _harvestBtn, _waterBtn, _plantBtn })
            {
                b.GetComponent<RectTransform>().Anchor(UIKit.Center, Vector2.zero, new Vector2(VerbW, 72));
                b.gameObject.SetActive(false);
            }

            BuildPager(_bar, app, Vector2.zero, PagerW);
            _pagerRt = (RectTransform)_bar.Find("pager");
        }

        public void SetActionBarVisible(bool on)
        {
            if (_bar != null && _bar.gameObject.activeSelf != on) _bar.gameObject.SetActive(on);
        }

        void BuildPager(RectTransform bar, GameApp app, Vector2 pos, float w)
        {
            var pager = UIKit.Node("pager", bar);
            pager.Anchor(UIKit.Center, pos, new Vector2(w, 72));
            var bg = UIKit.Round(pager, Theme.Glass, 26, "bg");
            bg.rectTransform.Stretch();

            _islePrev = UIKit.IconBtn(pager, Theme.Skin.ArrowLeft, Theme.Cream3, 0.44f,
                                      () => { HideTray(); app.StepIsland(-1); });
            _islePrev.GetComponent<RectTransform>().Anchor(UIKit.Left, new Vector2(5, 0), new Vector2(56, 56));

            _isleNext = UIKit.IconBtn(pager, Theme.Skin.ArrowRight, Theme.Cream3, 0.44f,
                                      () => { HideTray(); app.StepIsland(1); });
            _isleNext.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-5, 0), new Vector2(56, 56));

            // The name is a button too: it opens the whole archipelago, which is what a player
            // reaching for the island control at level 20 actually wants.
            var mid = UIKit.Node("name", pager);
            // 268 - 2x(56 + 5) - 8 of air = 138: room for eight Vietnamese characters at 20 px.
            mid.Anchor(UIKit.Center, Vector2.zero, new Vector2(w - 130, 60));
            var midBg = UIKit.Round(mid, new Color(1, 1, 1, 0.001f), 20, "hit");
            midBg.rectTransform.Stretch();
            midBg.raycastTarget = true;
            _islandLbl = UIKit.LabelOutlined(mid, "Vườn Nhà", 20, Color.white);
            _islandLbl.rectTransform.Stretch();
            var mb = mid.gameObject.AddComponent<Button>();
            mb.targetGraphic = midBg;
            mb.onClick.AddListener(() => { HideTray(); app.ShowArchipelago(); });
            mid.gameObject.AddComponent<PressFx>();
        }

        /// <summary>A verb that has nothing to act on stays visible but goes quiet — dimmed and
        /// uninteractable rather than hidden. A bar that changes its button count as the farm
        /// changes would move the other three under a thumb already on its way down.</summary>
        /// <summary>Dims by fading the label's OWN colour, never by forcing white.
        ///
        /// The first version forced white and "Gieo" — dark ink on amber by design — became white
        /// lettering on a washed-out amber plate, i.e. invisible exactly when it was disabled.
        /// The ink a button was built with is captured once at build time and faded from there.</summary>
        static void SetVerb(Button b, Text lbl, Color ink, string word, int n)
        {
            string want = n > 0 ? word + " · " + n : word;
            if (lbl.text != want) lbl.text = want;
            bool on = n > 0;
            if (b.interactable != on)
            {
                b.interactable = on;
                lbl.color = on ? ink : ink.Alpha(0.5f);
            }
        }

        // ------------------------------------------------------------
        // refresh
        // ------------------------------------------------------------
        public void Render(bool snap = false)
        {
            _level.text = GS.Local.lv.ToString();

            if (snap || _lastCoin < 0) _coin.text = Fmt.N(GS.Local.coin);
            else if (_lastCoin != GS.Local.coin) Tween.Count(_coin, GS.Local.coin);
            _lastCoin = GS.Local.coin;

            int need = GS.Local.XpNeed;
            _xpFill.fillAmount = Mathf.Clamp01(GS.Local.xp / (float)need);
            _xpText.text = Fmt.N(Math.Min(GS.Local.xp, need)) + " / " + Fmt.N(need);

            int goal = GS.Local.EnergyGoal;
            _energyFill.fillAmount = Mathf.Clamp01(GS.Local.energy / (float)goal);
            _energy.text = Fmt.N(GS.Local.energy) + "/" + Fmt.N(goal);

            int owned = 0;
            for (int i = 0; i < GS.Local.chests.Length; i++) owned += GS.Local.chests[i];
            _chestCount.text = owned > 0 ? "×" + owned : "";
            _chestIcon.color = owned > 0 ? Color.white : new Color(1, 1, 1, 0.72f);

            RenderSeason();
            RenderActions();
            RenderDots(owned);

            // --- the mission strip is conditional ---
            // A permanent "Đã hoàn thành mọi nhiệm vụ" is a 344 px banner whose only job is to
            // say there is nothing to say. When there is no active mission the strip goes away
            // and the player card sits alone, which is itself the message.
            var t = GS.Local.ActiveMission(out var pr);
            bool show = t != null;
            if (_missionChip.gameObject.activeSelf != show) _missionChip.gameObject.SetActive(show);
            if (show)
            {
                string mt = t.t + "  (" + pr.p + "/" + t.need + ")";
                if (mt != _lastMission) { _mission.text = mt; _lastMission = mt; }
            }
        }

        /// <summary>The bulk verbs appear only when unlocked AND when there is something for them
        /// to do.
        ///
        /// A dimmed "Tưới" with nothing to water was a button the player learned to ignore — and
        /// a learned-to-ignore button is also ignored on the one hour it matters. Now the bar is
        /// just the island pager most of the time, and a verb stepping into it IS the
        /// notification: crops are ripe, a window opened, a bed is empty. The pager stays put at
        /// the right end, so the thumb target that never changes never moves.</summary>
        void RenderActions()
        {
            if (_harvestLbl == null || _app == null || _app.Farm == null) return;
            var farm = _app.Farm;
            var s = GS.Local;

            int ready = farm.ReadyPlots().Count;
            int water = farm.WaterablePlots().Count;
            int empty = farm.EmptyPlots().Count;

            bool showH = QuickActions.HarvestUnlocked(s) && ready > 0;
            bool showW = QuickActions.WaterUnlocked(s) && water > 0;
            bool showP = QuickActions.PlantUnlocked(s) && empty > 0;

            SetVerb(_harvestBtn, _harvestLbl, _harvestInk, "Thu hoạch", ready);
            SetVerb(_waterBtn,   _waterLbl,   _waterInk,   "Tưới",      water);
            // The count is empty BEDS: the seed sheet this opens can buy seeds, so "no seeds in
            // the bag" is no longer a reason to hide the verb.
            SetVerb(_plantBtn,   _plantLbl,   _plantInk,   "Gieo",      empty);

            string layout = (showH ? "H" : "") + (showW ? "W" : "") + (showP ? "P" : "");
            if (layout != _barLayout) LayoutBar(showH, showW, showP, layout);

            int cur = farm.CurrentIsland;
            int max = Mathf.Min(IslandSys.Max, farm.IslandCount) - 1;
            string nm = IslandSys.NameOf(cur);
            if (_islandLbl.text != nm) _islandLbl.text = nm;
            _islePrev.interactable = cur > 0;
            _isleNext.interactable = cur < max;
        }

        void LayoutBar(bool h, bool w, bool p, string layout)
        {
            var shown = new List<Button>();
            if (h) shown.Add(_harvestBtn);
            if (w) shown.Add(_waterBtn);
            if (p) shown.Add(_plantBtn);

            float total = shown.Count * (VerbW + BarGap) + PagerW;
            _bar.sizeDelta = new Vector2(total, 72);
            float x = -total / 2f;
            foreach (var b in new[] { _harvestBtn, _waterBtn, _plantBtn })
            {
                bool on = shown.Contains(b);
                if (!on) { b.gameObject.SetActive(false); continue; }
                var rt = b.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(x + VerbW / 2f, 0);
                x += VerbW + BarGap;
                // a verb that just stepped in pops, so the change reads as news
                if (!b.gameObject.activeSelf) { b.gameObject.SetActive(true); Tween.PopIn(rt, 0.26f, 0.6f); }
            }
            if (_pagerRt != null) _pagerRt.anchoredPosition = new Vector2(x + PagerW / 2f, 0);
            _barLayout = layout;
        }

        /// <summary>Badges on the rail. This is what lets five buttons cover eight destinations
        /// without anything going unnoticed — a destination that needs attention says so where it
        /// lives, instead of earning a second button somewhere else to say it.</summary>
        void RenderDots(int chests)
        {
            if (_railDots == null) return;

            // seeds: nothing to plant is worth a nudge, but only once the farm has somewhere to
            // put them — otherwise a new player is nagged about a problem they do not have
            int seeds = 0;
            foreach (var kv in GS.Local.seeds) seeds += kv.Value;
            bool needSeeds = seeds == 0 && _app != null && _app.Farm != null && _app.Farm.EmptyPlots().Count > 0;
            SetDot(0, needSeeds ? -1 : 0);

            // warehouse: full enough that the next harvest would be wasted
            SetDot(1, 0);

            // missions: contracts ready to claim
            int claimable = 0;
            foreach (var m in GS.Local.contracts) if (!m.Empty && m.p >= m.need) claimable++;
            if (GS.Local.ActiveMission(out var mp) is Task at && at != null && mp.p >= at.need) claimable++;
            SetDot(2, claimable);

            // shop: nothing periodic yet, so nothing to claim
            SetDot(3, 0);

            // the tray: chests are the only thing inside it that expires on the player's patience
            SetDot(4, chests);
        }

        void SetDot(int slot, int n)
        {
            if (slot >= _railDots.Length) return;
            var dot = _railDots[slot];
            bool on = n != 0;
            if (dot.gameObject.activeSelf != on) dot.gameObject.SetActive(on);
            if (!on) return;
            var t = dot.GetComponentInChildren<Text>();
            // -1 means "look here" with no number attached
            t.text = n > 0 ? (n > 9 ? "9+" : n.ToString()) : "!";
        }

    }
}
