using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>The permanent on-screen furniture.
    ///
    ///     top left      player card, the active mission, and under it the two bulk verbs
    ///     top           the weather disc (tap for the season popup)
    ///     top right     energy and coins
    ///     bottom left   the island pager
    ///     bottom right  the menu button; every destination slides up out of it
    ///
    /// The middle of the screen and both side edges are left to the farm.</summary>
    public class Hud
    {
        public RectTransform Root;
        RectTransform _cluster;
        GameApp _app;

        Text _coin, _energy, _mission, _level, _xpText, _name;
        Image _avatarRing, _badge;
        string _lastFrame = "?", _lastBadge = "?";
        Image _xpFill, _energyFill, _chestIcon;
        Text _chestCount;
        RectTransform _missionChip, _energyChip, _missionDot;

        long _lastCoin = -1;
        string _lastMission = "";

        /// <summary>Where harvested produce flies to: the Kho button when the menu is open, the
        /// menu button that hides it otherwise.</summary>
        public Vector3 WarehouseWorld
        {
            get
            {
                if (_menuOpen && _warehouseSlot != null) return _warehouseSlot.position;
                return _menuBtn != null ? _menuBtn.position : Vector3.zero;
            }
        }

        public void Build(RectTransform parent, GameApp app)
        {
            _app = app;
            Root = UIKit.Node("hud", parent);
            Root.Stretch();

            BuildPlayerCard(app);
            BuildWeather(app);
            BuildCurrencies(app);
            BuildPager(app);
            BuildMenu(app);
            BuildPetButton(app);
        }

        // ------------------------------------------------------------
        // weather: one disc, one ring
        // ------------------------------------------------------------
        RectTransform _weather, _greenBadge;
        Image _wIcon, _wRing;
        Text _greenText;
        string _lastWName = "";

        /// <summary>The weather is a single icon with its hour running out round the rim.
        ///
        /// The bar it replaces carried a clock, a name, two percentages, a forecast mark, a tag
        /// count, six tag pips and a second clock: eight readouts in the one band a player's eye
        /// crosses on every glance, about a system that changes once an hour. The ring answers
        /// the only question a glance needs — "how long until it changes?" — and everything else
        /// is one tap away in the season popup, which already showed all of it.
        ///
        /// Anchored TopLeft against the player card, never Top: the notch inset applies to one
        /// side only, so a Top-anchored element drifts off centre on a notched phone.</summary>
        void BuildWeather(GameApp app)
        {
            const float D = 74f;
            _weather = UIKit.Node("weather", Root);
            _weather.Anchor(UIKit.TopLeft, new Vector2(376, -14), new Vector2(D, D));
            var fill = SurfaceLook.Add(_weather, Looks.Glass, SurfaceLook.Pill).Fill;
            fill.raycastTarget = true;

            // track, then the remaining hour as a clockwise arc from twelve o'clock
            var track = UIKit.Img(_weather, Theme.Ring(0.17f), new Color(0f, 0.05f, 0.05f, 0.45f), "track");
            track.rectTransform.Stretch(5, 5, 5, 5);
            _wRing = UIKit.Img(_weather, Theme.Ring(0.17f), Color.white, "ring");
            _wRing.rectTransform.Stretch(5, 5, 5, 5);
            _wRing.type = Image.Type.Filled;
            _wRing.fillMethod = Image.FillMethod.Radial360;
            _wRing.fillOrigin = (int)Image.Origin360.Top;
            _wRing.fillClockwise = true;
            _wRing.gameObject.AddComponent<WeatherRing>();

            _wIcon = UIKit.Img(_weather, Art.WeatherIcon(Weather.Sunny), Color.white, "icon");
            _wIcon.preserveAspect = true;
            _wIcon.rectTransform.Anchor(UIKit.Center, Vector2.zero, new Vector2(38, 38));

            // Greenhouse charges are an item the player owns, not weather information, so they
            // keep a corner count here — it only exists while charges are left.
            _greenBadge = UIKit.Node("green", _weather);
            _greenBadge.Anchor(UIKit.BottomRight, new Vector2(10, -6), new Vector2(46, 22));
            SurfaceLook.Add(_greenBadge, Badge(Looks.BtnGreen));
            _greenText = UIKit.LabelOutlined(_greenBadge, "", 14, Color.white, TextAnchor.MiddleCenter, Theme.Hex("#1C7439"));
            _greenText.rectTransform.Stretch();
            _greenBadge.gameObject.SetActive(false);

            var b = _weather.gameObject.AddComponent<Button>();
            b.targetGraphic = fill;
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { CloseMenu(); app.Open(new SeasonPanel(app)); });
            _weather.gameObject.AddComponent<PressFx>();
        }

        void RenderWeather()
        {
            if (_weather == null) return;
            var s = GS.Local;
            var w = WeatherSys.Now(s);
            var d = WeatherSys.Def(w);
            var icon = Art.WeatherIconNow(w, out var tint);
            string key = d.name + (icon != null ? icon.name : "");
            if (key != _lastWName)
            {
                _wIcon.sprite = icon;
                _wIcon.color = tint;
                _wRing.color = tint;
                _lastWName = key;
            }

            bool has = s.greenhouse > 0;
            if (_greenBadge.gameObject.activeSelf != has) _greenBadge.gameObject.SetActive(has);
            if (has) _greenText.text = "×" + s.greenhouse;
        }


        // ------------------------------------------------------------
        // top-left cluster: who you are, and what you are working on
        // ------------------------------------------------------------
        void BuildPlayerCard(GameApp app)
        {
            var cluster = UIKit.Node("playerCluster", Root);
            cluster.Anchor(UIKit.TopLeft, new Vector2(16, -14), new Vector2(344, 176));
            _cluster = cluster;

            // --- identity card ---
            var card = UIKit.Node("card", cluster);
            card.Anchor(UIKit.TopLeft, Vector2.zero, new Vector2(344, 74));
            card.pivot = new Vector2(0, 1);
            var cardFill = SurfaceLook.Add(card, Looks.Glass).Fill;
            // The XP bar lives here, so this is where a player reaches when it fills: the card
            // opens the upgrade panel.
            cardFill.raycastTarget = true;
            var cardBtn = card.gameObject.AddComponent<Button>();
            cardBtn.targetGraphic = cardFill;
            cardBtn.transition = Selectable.Transition.None;
            cardBtn.onClick.AddListener(() => { CloseMenu(); Sfx.Play(SfxId.Tap); app.Open(new UpgradePanel(app)); });
            card.gameObject.AddComponent<PressFx>();

            // The avatar is a medallion pinned on the pill's left cap: concentric with it, its
            // ring 6 px proud of the pill's edge. Floating 31 px in from the end, it read as a
            // photo placed on a bar rather than part of it.
            var av = UIKit.Node("avatar", card);
            av.Anchor(UIKit.Left, new Vector2(5, 0), new Vector2(64, 64));
            UIKit.Img(av, Theme.Circle(), Theme.Cream, "disc").rectTransform.Stretch(3, 3, 3, 3);
            var pic = UIKit.Img(av, Theme.Skin.Farmer, Color.white, "pic");
            pic.preserveAspect = true;
            pic.rectTransform.Stretch(6, 6, 6, 6);
            var ring = UIKit.Img(av, Theme.Skin.AvatarRing, Color.white, "ring");
            ring.preserveAspect = true;
            ring.rectTransform.Stretch(-6, -6, -6, -6);
            _avatarRing = ring;

            var lvBadge = UIKit.Node("lv", av);
            lvBadge.Anchor(UIKit.BottomRight, new Vector2(6, -2), new Vector2(34, 26));
            SurfaceLook.Add(lvBadge, Badge(Looks.BtnAmber));
            _level = UIKit.LabelOutlined(lvBadge, "1", 19, Color.white);
            _level.rectTransform.Stretch();

            // a worn badge sits between the portrait and the name
            _badge = UIKit.Img(card, null, Color.white, "badge");
            _badge.preserveAspect = true;
            _badge.rectTransform.Anchor(UIKit.Left, new Vector2(90, 15), new Vector2(26, 26));
            _badge.enabled = false;

            var name = UIKit.LabelOutlined(card, "Nông Trại Của Bạn", 19, Color.white, TextAnchor.MiddleLeft);
            _name = name;
            // The ring is drawn 6 px proud of the 64 px avatar, so its right edge is at 75, and
            // the level badge overhangs it to 85. Starting the name any closer puts the "N"
            // underneath one of them.
            name.rectTransform.Anchor(UIKit.Left, new Vector2(92, 15), new Vector2(234, 28));
            name.rectTransform.pivot = new Vector2(0, 0.5f);

            var bar = UIKit.Node("xp", card);
            bar.Anchor(UIKit.Left, new Vector2(92, -13), new Vector2(228, 22));
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

            var sbg = SurfaceLook.Add(strip, Looks.Glass).Fill;
            sbg.raycastTarget = true;

            var ic = UIKit.Img(strip, Theme.Skin.Alert, Theme.Amber, "ic");
            ic.preserveAspect = true;
            ic.rectTransform.Anchor(UIKit.Left, new Vector2(16, 0), new Vector2(24, 24));

            _mission = UIKit.LabelOutlined(strip, "", 17, Color.white, TextAnchor.MiddleLeft);
            _mission.rectTransform.Stretch(46, 0, 20, 0);

            var b = strip.gameObject.AddComponent<Button>();
            b.targetGraphic = sbg;
            b.onClick.AddListener(() => app.Open(new MissionsPanel(app)));
            strip.gameObject.AddComponent<PressFx>();

            // how many rewards wait on the board (any tab): a finished mission used to look exactly
            // like one in progress until the player happened to open the board (owner, 15/9)
            _missionDot = Dot(strip);
            _missionDot.Anchor(UIKit.Right, new Vector2(-8, 0), new Vector2(26, 26));
            _mission.rectTransform.Stretch(46, 0, 40, 0);

            BuildActionRow(app, cluster);
        }

        // ------------------------------------------------------------
        // top-right cluster: every resource in one aligned block
        // ------------------------------------------------------------
        void BuildCurrencies(GameApp app)
        {
            var cluster = UIKit.Node("wallet", Root);
            cluster.Anchor(UIKit.TopRight, new Vector2(-16, -14), new Vector2(392, 52));
            cluster.pivot = new Vector2(1, 1);
            _wallet = cluster;

            // --- row 1: magic energy, then coins ---
            var enChip = UIKit.Node("energyChip", cluster);
            _energyChip = enChip;
            // 170 + 16 + 206: the coin chip takes the width, because a seven-digit balance is the
            // number here that grows and the energy readout never passes "1.200/1.200".
            enChip.Anchor(UIKit.TopLeft, Vector2.zero, new Vector2(170, 52));
            enChip.pivot = new Vector2(0, 1);
            var enBg = SurfaceLook.Add(enChip, Looks.Glass).Fill;
            enBg.raycastTarget = true;

            // Same overhanging token as the coin, so the two chips read as one pair.
            var chestHolder = UIKit.Node("chest", enChip);
            chestHolder.Anchor(UIKit.Left, new Vector2(-2, 0), new Vector2(50, 50));
            // The painted flask from the item set, the same one the shop sells energy under — the
            // flat white glyph was the only untextured icon left on the HUD.
            _chestIcon = UIKit.Img(chestHolder, Art.Item("item_energy"), Color.white, "ic");
            _chestIcon.preserveAspect = true;
            _chestIcon.rectTransform.Stretch();

            _chestCount = UIKit.LabelOutlined(chestHolder, "", 15, Color.white, TextAnchor.LowerRight);
            _chestCount.rectTransform.Anchor(UIKit.BottomRight, new Vector2(6, -4), new Vector2(36, 18));

            var enBar = UIKit.Node("bar", enChip);
            enBar.Anchor(UIKit.Right, new Vector2(-20, -11), new Vector2(96, 12));
            // TrackGlass, not TrackDark: the brown track is built for cream panels and vanished
            // on the glass chip, so a low energy reading looked like a stray blue pixel.
            _energyFill = UIKit.Bar(enBar, Theme.TrackGlass, Theme.Purple, 6);
            _energyFill.transform.parent.GetComponent<RectTransform>().Stretch();

            _energy = UIKit.LabelOutlined(enChip, "0/300", 16, Color.white, TextAnchor.MiddleRight);
            _energy.rectTransform.Anchor(UIKit.Right, new Vector2(-18, 9), new Vector2(100, 24));

            var enBtn = enChip.gameObject.AddComponent<Button>();
            enBtn.targetGraphic = enBg;
            enBtn.onClick.AddListener(() => app.Open(new ChestPanel(app)));
            enChip.gameObject.AddComponent<PressFx>();

            var coinChip = UIKit.Node("coinChip", cluster);
            coinChip.Anchor(UIKit.TopRight, Vector2.zero, new Vector2(206, 52));
            coinChip.pivot = new Vector2(1, 1);
            _coin = UIKit.Chip(coinChip, Theme.Skin.Coin, "0", Theme.Glass, 206,
                               () => app.Open(new ShopPanel(app)));
            coinChip.GetChild(0).GetComponent<RectTransform>().Stretch();
            // 206 - 56 - 54 leaves 96 px of digits, and "1.286.400" at 24 px Black is ~118.
            // Best-fit shrinks the digits instead of running them under the coin; best-fit only
            // works with Wrap/Truncate, which is harmless for a single number.
            _coin.horizontalOverflow = HorizontalWrapMode.Wrap;
            _coin.verticalOverflow = VerticalWrapMode.Truncate;
            _coin.resizeTextForBestFit = true;
            _coin.resizeTextMinSize = 15;
            _coin.resizeTextMaxSize = 24;

        }

        // ------------------------------------------------------------
        // the two bulk verbs, docked under the mission strip
        // ------------------------------------------------------------
        const float PillW = 170f, PillH = 46f, PillGap = 4f;
        RectTransform _actionRow;
        SkinButton _harvestBtn, _waterBtn;
        RectTransform _harvestCount, _waterCount;

        static Look Tint(Look l, float toward, Color target)
        {
            l.top = Color.Lerp(l.top, target, toward).Alpha(l.top.a);
            l.bottom = Color.Lerp(l.bottom, target, toward).Alpha(l.bottom.a);
            return l;
        }

        static Look ActionLook(string top, string bottom, string edge)
        {
            var l = Looks.Glass;
            l.top = Theme.Hex(top).Alpha(0.96f);
            l.bottom = Theme.Hex(bottom).Alpha(0.97f);
            l.edge = Theme.Hex(edge).Alpha(0.7f);
            l.inkLine = Theme.Hex(edge);
            return l;
        }

        /// <summary>Thu hoạch and Tưới nước, as a pair of pills in the mission strip's own
        /// material — the same shape and height family, each in its own colour so the verbs never
        /// read as more missions.
        ///
        /// They live with the mission because the mission is almost always one of them ("Thu
        /// hoạch cà rốt (0/6)"), and top left is where the eye already goes for "what should I do".
        /// There is no Gieo: an empty bed is planted by tapping it, which opens the seed sheet.
        ///
        /// A verb shows only while it has plots to act on (2026-09-15: the owner asked for the
        /// empty, grey pair to go), and pops in when a crop ripens or a window opens.</summary>
        void BuildActionRow(GameApp app, RectTransform cluster)
        {
            _actionRow = UIKit.Node("actions", cluster);
            _actionRow.Anchor(UIKit.TopLeft, new Vector2(0, -126), new Vector2(PillW * 2 + PillGap, PillH));
            _actionRow.pivot = new Vector2(0, 1);

            var harvest = ActionLook("#45B35E", "#1F6E36", "#0D3D1D");
            var water = ActionLook("#3D96DC", "#1C5896", "#0A2C4C");
            var off = ActionLook("#66716F", "#3E4847", "#1C2322");
            off.top.a = 0.9f; off.bottom.a = 0.92f; off.rim.a = 0.12f;
            off.ink = new Color(1f, 1f, 1f, 0.55f);

            _harvestBtn = ActionPill(_actionRow, 0f, Art.Item("icon_harvest"), "Thu hoạch", harvest, off,
                                     () => { CloseMenu(); app.HarvestAll(); }, out _harvestCount);
            _waterBtn = ActionPill(_actionRow, PillW + PillGap, Art.Item("item_can"), "Tưới nước", water, off,
                                   () => { CloseMenu(); app.WaterAll(); }, out _waterCount);
        }

        SkinButton ActionPill(RectTransform row, float x, Sprite icon, string word, Look on, Look off,
                              Action act, out RectTransform count)
        {
            var root = UIKit.Node(word, row);
            root.Anchor(UIKit.Left, new Vector2(x, 0), new Vector2(PillW, PillH));
            var surf = SurfaceLook.Add(root, on);
            surf.RaycastBody();

            // Anchor(Left) is the LEFT edge: the icon spans 8..44, the word starts at 48.
            var ic = UIKit.Img(root, icon, Color.white, "icon");
            ic.preserveAspect = true;
            ic.rectTransform.Anchor(UIKit.Left, new Vector2(8, 1), new Vector2(36, 36));

            var lab = UIKit.LabelOutlined(root, word, 18, Color.white, TextAnchor.MiddleLeft, on.inkLine);
            lab.rectTransform.Stretch(48, 0, 10, 1);

            // how many plots the verb would act on: a red count pinned to the corner, the same
            // badge the menu uses, so "something is waiting" has one look on the whole HUD
            count = Dot(root);

            var b = root.gameObject.AddComponent<SkinButton>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = surf.Fill;
            b.Bind(surf, on, off, lab, Tint(on, 0.14f, Color.white), Tint(on, 0.18f, Color.black));
            b.onClick.AddListener(() => act());
            root.gameObject.AddComponent<PressFx>();
            return b;
        }

        void RenderActions()
        {
            if (_harvestBtn == null || _app == null || _app.Farm == null) return;
            var farm = _app.Farm;
            var s = GS.Local;

            bool hasH = QuickActions.HarvestUnlocked(s);
            bool hasW = QuickActions.WaterUnlocked(s);
            int ready = hasH ? farm.ReadyPlots().Count : 0;
            int water = hasW ? farm.WaterablePlots().Count : 0;

            // Shown only while there is something to do: a grey "Thu hoạch" and "Tưới nước" sat on
            // the screen for the whole of every wait, two big buttons saying "nothing".
            bool showH = hasH && ready > 0, showW = hasW && water > 0;
            SetVerb(_harvestBtn, _harvestCount, showH, ready);
            SetVerb(_waterBtn, _waterCount, showW, water);

            // a lone Tưới slides left so the row has no hole in it
            var wrt = (RectTransform)_waterBtn.transform;
            float wx = showH ? PillW + PillGap : 0f;
            if (!Mathf.Approximately(wrt.anchoredPosition.x, wx)) wrt.anchoredPosition = new Vector2(wx, 0);

            // docked straight under the strip, or under the card when there is no active mission
            float y = _missionChip.gameObject.activeSelf ? -126f : -80f;
            if (!Mathf.Approximately(_actionRow.anchoredPosition.y, y)) _actionRow.anchoredPosition = new Vector2(0, y);

            int cur = farm.CurrentIsland;
            int max = Mathf.Min(IslandSys.Max, farm.IslandCount) - 1;
            string nm = IslandSys.NameOf(cur);
            if (_islandLbl.text != nm) _islandLbl.text = nm;
            _islePrev.interactable = cur > 0;
            _isleNext.interactable = cur < max;
        }

        static void SetVerb(SkinButton b, RectTransform count, bool unlocked, int n)
        {
            if (b.gameObject.activeSelf != unlocked)
            {
                b.gameObject.SetActive(unlocked);
                if (unlocked) Tween.PopIn(b.transform, 0.26f, 0.6f);
            }
            if (!unlocked) return;
            bool on = n > 0;
            if (b.interactable != on)
            {
                b.interactable = on;
                // the palette goes grey on its own; the painted icon has to be told
                var ic = b.transform.Find("icon")?.GetComponent<Image>();
                if (ic != null) ic.color = on ? Color.white : new Color(1f, 1f, 1f, 0.42f);
            }
            SetDot(count, n);
        }

        // ------------------------------------------------------------
        // bottom left: the island pager
        // ------------------------------------------------------------
        const float PagerW = 268f;
        RectTransform _pager;
        Text _islandLbl;
        Button _islePrev, _isleNext;

        void BuildPager(GameApp app)
        {
            var pager = UIKit.Node("pager", Root);
            pager.Anchor(UIKit.BottomLeft, new Vector2(16, 16), new Vector2(PagerW, 72));
            _pager = pager;
            SurfaceLook.Add(pager, Looks.Glass);

            // 56 inside a 72 pill, 8 px from each end: concentric with the caps.
            _islePrev = UIKit.IconBtn(pager, Theme.Skin.ArrowLeft, Theme.Cream3, 0.44f,
                                      () => { CloseMenu(); app.StepIsland(-1); });
            _islePrev.GetComponent<RectTransform>().Anchor(UIKit.Left, new Vector2(8, 0), new Vector2(56, 56));

            _isleNext = UIKit.IconBtn(pager, Theme.Skin.ArrowRight, Theme.Cream3, 0.44f,
                                      () => { CloseMenu(); app.StepIsland(1); });
            _isleNext.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-8, 0), new Vector2(56, 56));

            // The name is a button too: it opens the whole archipelago, which is what a player
            // reaching for the island control at level 20 actually wants.
            var mid = UIKit.Node("name", pager);
            // 268 - 2x(56 + 8) = 140: room for eight Vietnamese characters at 20 px.
            mid.Anchor(UIKit.Center, Vector2.zero, new Vector2(PagerW - 130, 60));
            var midBg = UIKit.Round(mid, new Color(1, 1, 1, 0.001f), 20, "hit");
            midBg.rectTransform.Stretch();
            midBg.raycastTarget = true;
            _islandLbl = UIKit.LabelOutlined(mid, "Vườn Nhà", 20, Color.white);
            _islandLbl.rectTransform.Stretch();
            var mb = mid.gameObject.AddComponent<Button>();
            mb.targetGraphic = midBg;
            mb.onClick.AddListener(() => { CloseMenu(); app.ShowArchipelago(); });
            mid.gameObject.AddComponent<PressFx>();
        }

        /// <summary>Hidden while the seed sheet covers the bottom of the screen.</summary>
        public void SetActionBarVisible(bool on)
        {
            if (_pager != null && _pager.gameObject.activeSelf != on) _pager.gameObject.SetActive(on);
        }

        // ------------------------------------------------------------
        // bottom right: the menu
        // ------------------------------------------------------------
        /// <summary>A small pill badge (level, greenhouse count) in a button tone: the button's
        /// palette without its lip or drop shadow, which at 22 px would be most of the badge.</summary>
        static Look Badge(Look btn)
        {
            btn.lip = 0f;
            btn.shadow = Color.clear;
            btn.rimW = 1.5f;
            btn.edgeW = 1.5f;
            return btn;
        }

        /// <summary>A count pinned to a button's corner.</summary>
        public static RectTransform Dot(RectTransform slot)
        {
            var dot = UIKit.Node("dot", slot);
            dot.Anchor(UIKit.TopRight, new Vector2(6, 6), new Vector2(26, 26));
            UIKit.Round(dot, Theme.Red, 13, "bg").rectTransform.Stretch();
            var t = UIKit.LabelOutlined(dot, "", 15, Color.white, TextAnchor.MiddleCenter, Theme.RedDeep);
            t.rectTransform.Stretch();
            dot.gameObject.SetActive(false);
            return dot;
        }

        public static void SetDot(RectTransform dot, int n)
        {
            if (dot == null) return;
            bool on = n != 0;
            if (dot.gameObject.activeSelf != on) dot.gameObject.SetActive(on);
            if (!on) return;
            var t = dot.GetComponentInChildren<Text>();
            // -1 means "look here" with no number attached
            string want = n > 0 ? (n > 9 ? "9+" : n.ToString()) : "!";
            if (t.text != want) t.text = want;
        }

        const float MenuD = 80f, MenuMargin = 16f;
        const float CellD = 70f, ColW = 96f, RowH = 102f, PanelPad = 14f;

        RectTransform _menuBtn, _menuPanel, _menuScrim, _warehouseSlot, _menuDot;
        Image _menuIcon;
        CanvasGroup _menuGroup;
        readonly List<RectTransform> _menuSlots = new List<RectTransform>();
        readonly List<int> _menuRows = new List<int>();

        /// <summary>The one setting the game has: sound on or off, at the top of the menu. A
        /// settings screen for a single switch would be a panel that exists to hold a button.</summary>
        void BuildSoundToggle(RectTransform panel, float w, float headerH)
        {
            var row = UIKit.Node("sound", panel);
            row.Anchor(UIKit.TopLeft, new Vector2(PanelPad, -PanelPad + 2f), new Vector2(w - PanelPad * 2f, headerH - 8f));

            // Tài khoản: sign-in, cloud sync, linking an email — once-in-a-while, so a pill in the
            // header rather than a cell in the grid of daily destinations
            var acc = UIKit.Node("account", row);
            acc.Anchor(UIKit.Left, new Vector2(-2, 0), new Vector2(112, 34));
            var accLook = Looks.BtnBlue;
            accLook.lip = 3f;
            var accSurf = SurfaceLook.Add(acc, accLook, SurfaceLook.Pill, pressable: true);
            accSurf.RaycastBody();
            var accIcon = UIKit.Img(accSurf.Face, Art.Load("Art/gen/account"), Color.white, "ic");
            accIcon.preserveAspect = true;
            accIcon.rectTransform.Anchor(UIKit.Left, new Vector2(10, 1), new Vector2(17, 17));
            var accLabel = UIKit.LabelOutlined(accSurf.Face, "Tài khoản", 15, Color.white, TextAnchor.MiddleCenter, accLook.inkLine);
            accLabel.rectTransform.Anchor(UIKit.Left, new Vector2(29, 1), new Vector2(76, 24));
            var accBtn = acc.gameObject.AddComponent<Button>();
            accBtn.targetGraphic = accSurf.Fill;
            accBtn.transition = Selectable.Transition.None;
            accBtn.onClick.AddListener(() => { Sfx.Play(SfxId.Tap); CloseMenu(); _app.Open(new AccountPanel(_app)); });
            acc.gameObject.AddComponent<PressFx>();
            _accountDot = Dot(acc);
            _accountBtn = acc;

            // Âm thanh and Nhạc nền: one round chip each. Tap toggles; off is a faded face and a struck
            // glyph. Two switches did not fit beside the account pill.
            _musicChip = ToggleChip(row, 0f, "music");
            _soundChip = ToggleChip(row, -40f, "sound");
            _soundChip.onClick.AddListener(() =>
            {
                Sfx.Enabled = !Sfx.Enabled;
                if (Sfx.Enabled) Sfx.Play(SfxId.Toggle);
                RenderSoundToggle();
            });
            _musicChip.onClick.AddListener(() =>
            {
                Music.Enabled = !Music.Enabled;
                Sfx.Play(SfxId.Toggle);
                RenderSoundToggle();
            });
            RenderSoundToggle();
        }

        Button _soundChip, _musicChip;

        static Button ToggleChip(RectTransform row, float right, string name)
        {
            var node = UIKit.Node(name, row);
            node.Anchor(UIKit.Right, new Vector2(right, 0), new Vector2(36, 36));
            var face = UIKit.Img(node, null, Color.white, "face");
            face.rectTransform.Stretch();
            Chrome.Shape(face, 18f);
            face.raycastTarget = true;
            var icon = UIKit.Img(node, null, Color.white, "icon");
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.Anchor(UIKit.Center, Vector2.zero, new Vector2(22, 22));
            var b = node.gameObject.AddComponent<Button>();
            b.targetGraphic = face;
            b.transition = Selectable.Transition.None;
            node.gameObject.AddComponent<PressFx>();
            return b;
        }

        static void PaintChip(Button chip, bool on, string art)
        {
            if (chip == null) return;
            var face = chip.transform.Find("face").GetComponent<Image>();
            var icon = chip.transform.Find("icon").GetComponent<Image>();
            face.color = on ? Theme.Hex("#3FAF5E") : new Color(0f, 0.05f, 0.05f, 0.45f);
            icon.sprite = Art.Load("Art/gen/" + art + (on ? "_on" : "_off"));
            icon.color = on ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }

        void RenderSoundToggle()
        {
            PaintChip(_soundChip, Sfx.Enabled, "sound");
            PaintChip(_musicChip, Music.Enabled, "music");
        }
        RectTransform _accountDot, _accountBtn;
        public RectTransform AccountButton => _accountBtn;
        readonly Dictionary<string, RectTransform> _menuDots = new Dictionary<string, RectTransform>();
        bool _menuOpen;
        Coroutine _menuAnim;

        /// <summary>Every destination behind one button in the bottom-right corner.
        ///
        /// The rail it replaces was a permanent column of five discs down the right edge of the
        /// farm, and one of them opened a second tray of four more. Both levels are one grid now,
        /// two columns by four rows, sliding up out of the button. The column nearest the edge
        /// holds what is visited every session (seeds, the warehouse, the shop, missions); the
        /// inner one what is visited now and then. Captions are back: in a menu that is opened
        /// on purpose, a label costs nothing and saves the guess.
        ///
        /// Anything that needs attention badges its own cell AND the menu button, so closing the
        /// menu never hides that something is waiting.</summary>
        void BuildMenu(GameApp app)
        {
            // Catches a tap anywhere else, so tapping the farm closes the menu instead of planting
            // a seed under it. Invisible: the menu should not dim the farm it sits on.
            _menuScrim = UIKit.Node("menuScrim", Root);
            _menuScrim.Stretch();
            var scrim = UIKit.Img(_menuScrim, null, new Color(0, 0, 0, 0f), "bg");
            scrim.rectTransform.Stretch();
            scrim.raycastTarget = true;
            var sb = _menuScrim.gameObject.AddComponent<Button>();
            sb.targetGraphic = scrim;
            sb.transition = Selectable.Transition.None;
            sb.onClick.AddListener(CloseMenu);
            _menuScrim.gameObject.SetActive(false);

            // (column, row from the top, ...) — column 1 is the one against the screen edge
            var items = new (int col, int row, string key, Sprite art, string cap, Color tone, Action act)[]
            {
                (1, 0, "quest",   Theme.Skin.NavQuest,   "Nhiệm vụ", Theme.Blue,  () => app.Open(new MissionsPanel(app))),
                (1, 1, "shop",    Theme.Skin.NavShop,    "Cửa hàng", Theme.Red,   () => app.Open(new ShopPanel(app))),
                (1, 2, "store",   Theme.Skin.NavStore,   "Kho",      Theme.Amber, () => app.Open(new WarehousePanel(app))),
                (1, 3, "seeds",   Theme.Skin.NavSeeds,   "Hạt giống", Theme.Green, () => app.Open(new SeedShopPanel(app))),
                (0, 0, "upgrade", Theme.Skin.NavUpgrade, "Nâng cấp", Theme.Blue,  () => app.Open(new UpgradePanel(app))),
                (0, 1, "album",   Theme.Skin.NavAlbum,   "Sưu tập",  Theme.Red,   () => app.Open(new CollectionPanel(app))),
                (0, 2, "friends", Theme.Skin.NavFriends, "Bạn bè",   Theme.Green, () => app.Open(new FriendsPanel(app))),
                (0, 3, "chest",   Art.Item("chest_1"),   "Rương",    Theme.Amber, () => app.Open(new ChestPanel(app))),
            };

            const float HeaderH = 50f, FooterH = 54f;
            float w = PanelPad * 2f + ColW * 2f;
            float h = PanelPad + HeaderH + RowH * 4f + 4f + FooterH;
            _menuPanel = UIKit.Node("menu", Root);
            _menuPanel.Anchor(UIKit.BottomRight, new Vector2(-MenuMargin + 4f, MenuMargin + MenuD + 14f), new Vector2(w, h));
            // glass a touch lighter than the HUD chips: it is laid over the farm, not beside it
            var glass = Looks.Glass;
            glass.top.a = 0.88f; glass.bottom.a = 0.92f;
            glass.blur = 18f; glass.drop = new Vector2(0f, -6f);
            SurfaceLook.Add(_menuPanel, glass, 28f).Fill.raycastTarget = true;
            _menuGroup = _menuPanel.gameObject.AddComponent<CanvasGroup>();
            BuildSoundToggle(_menuPanel, w, HeaderH);

            foreach (var it in items)
            {
                var slot = UIKit.Node("m_" + it.key, _menuPanel);
                slot.Anchor(UIKit.TopLeft, new Vector2(PanelPad + it.col * ColW, -PanelPad - HeaderH - it.row * RowH), new Vector2(ColW, RowH));
                _menuRows.Add(it.row);
                slot.gameObject.AddComponent<CanvasGroup>();

                var disc = UIKit.Node("disc", slot);
                disc.Anchor(UIKit.Top, new Vector2(0, 0), new Vector2(CellD, CellD));
                var act = it.act;
                bool grey = Theme.Skin.ToneOf(it.tone) == Theme.Tone.Grey;
                var btn = UIKit.IconBtn(disc, it.art, it.tone, it.key == "chest" ? 0.66f : 0.52f,
                                        () => { CloseMenu(); act(); }, null, grey ? Theme.Ink : (Color?)null);
                btn.GetComponent<RectTransform>().Stretch();

                // 24 px of line box for a 16 px font: Vietnamese stacks two diacritics.
                var cap = UIKit.LabelOutlined(slot, it.cap, 16, Color.white, TextAnchor.UpperCenter);
                cap.rectTransform.Anchor(UIKit.Top, new Vector2(0, -CellD - 3f), new Vector2(ColW, 24));

                _menuDots[it.key] = Dot(disc);
                _menuSlots.Add(slot);
                if (it.key == "store") _warehouseSlot = disc;
            }
            // The rules and the gift-code field. A footer rather than more cells: places a player
            // looks once, not destinations visited every session.
            float half = (w - PanelPad * 2f - 8f) / 2f;
            var guide = UIKit.Btn(_menuPanel, "Hướng dẫn", Theme.Blue, Theme.BlueDeep, 15, 18,
                                  () => { CloseMenu(); app.Open(new GuidePanel(app)); });
            guide.GetComponent<RectTransform>().Anchor(UIKit.BottomLeft, new Vector2(PanelPad, PanelPad), new Vector2(half, FooterH - 10f));
            _guideBtn = (RectTransform)guide.transform;
            var code = UIKit.Btn(_menuPanel, "Nhập code", Theme.Amber, Theme.AmberDeep, 15, 18,
                                 () => { CloseMenu(); app.Open(new CodePanel(app)); });
            code.GetComponent<RectTransform>().Anchor(UIKit.BottomRight, new Vector2(-PanelPad, PanelPad), new Vector2(half, FooterH - 10f));

            _menuPanel.gameObject.SetActive(false);

            var mbHolder = UIKit.Node("menuBtn", Root);
            mbHolder.Anchor(UIKit.BottomRight, new Vector2(-MenuMargin, MenuMargin), new Vector2(MenuD, MenuD));
            var mb = UIKit.IconBtn(mbHolder, Theme.Skin.More, Theme.Cream3, 0.5f, ToggleMenu, null, Theme.Ink);
            mb.GetComponent<RectTransform>().Stretch();
            _menuIcon = mb.transform.Find("face/icon")?.GetComponent<Image>();
            _menuBtn = mbHolder;
            _menuDot = Dot(mbHolder);
        }

        RectTransform _wallet;

        /// <summary>One cluster of the HUD as the arrival cinematic brings it in: slid in from its own
        /// edge (<see cref="from"/> is the offset it starts at, in HUD units), or popped in place.</summary>
        public struct EntrancePart
        {
            public RectTransform rt;
            public Vector2 from;
            public float delay;
            public bool pop;
        }

        /// <summary>The clusters in the order they arrive (see <see cref="EnterCinematic"/>): the
        /// player card from the left, the wallet from the right, the pager and the corner buttons up
        /// from the bottom, and the weather disc popping last between them. Each starts a little past
        /// its own edge — far enough to be off screen, not so far that it streaks.</summary>
        public List<EntrancePart> EntranceParts()
        {
            var list = new List<EntrancePart>(6);
            void Add(RectTransform rt, Vector2 from, float delay, bool pop = false)
            {
                if (rt != null) list.Add(new EntrancePart { rt = rt, from = from, delay = delay, pop = pop });
            }
            Add(_cluster, new Vector2(-(16f + 344f + 24f), 0f), 0.00f);
            Add(_wallet, new Vector2(16f + 392f + 24f, 0f), 0.06f);
            Add(_pager, new Vector2(0f, -(16f + 72f + 24f)), 0.12f);
            Add(_petBtn, new Vector2(0f, -(16f + MenuD + 30f)), 0.18f);
            Add(_menuBtn, new Vector2(0f, -(16f + MenuD + 30f)), 0.23f);
            Add(_weather, Vector2.zero, 0.26f, true);
            return list;
        }

        // ---- what the tutorial points at ----
        public bool MenuOpen => _menuOpen;
        public RectTransform MenuButton => _menuBtn;
        public RectTransform MissionStrip => _missionChip;
        public RectTransform HarvestPill => _harvestBtn != null ? (RectTransform)_harvestBtn.transform : null;
        public RectTransform WaterPill => _waterBtn != null ? (RectTransform)_waterBtn.transform : null;
        public RectTransform WeatherDisc => _weather;
        public RectTransform EnergyChip => _energyChip;
        public RectTransform PagerNext => _isleNext != null ? (RectTransform)_isleNext.transform : null;
        public RectTransform GuideButton => _guideBtn;
        public RectTransform MenuPanel => _menuPanel;
        RectTransform _guideBtn;
        /// <summary>A destination's disc in the menu grid, by key ("store", "upgrade"…).</summary>
        public RectTransform MenuCell(string key)
        {
            foreach (var slot in _menuSlots)
                if (slot.name == "m_" + key) return slot.GetChild(0) as RectTransform;
            return null;
        }

        void ToggleMenu() { SetMenu(!_menuOpen); }
        public void CloseMenu() { SetMenu(false); }
        /// <summary>Opened by the screenshot pass.</summary>
        public void OpenMenuForAudit() { SetMenu(true, true); }
        /// <summary>The same as tapping the button: animated. For the screenshot pass.</summary>
        public void OpenMenuAnimated() { SetMenu(true); }
        /// <summary>The same as tapping the weather disc. For the screenshot pass.</summary>
        public void TapWeatherForAudit() { _weather.GetComponent<Button>().onClick.Invoke(); }

        /// <summary>Hidden (and closed) while the seed sheet covers the bottom of the screen.</summary>
        public void SetRailVisible(bool on)
        {
            if (!on) SetMenu(false, true);
            if (_menuBtn != null && _menuBtn.gameObject.activeSelf != on) _menuBtn.gameObject.SetActive(on);
            _railVisible = on;
            RenderPet();
        }

        // ------------------------------------------------------------
        // bottom right, beside the menu: the pet
        // ------------------------------------------------------------
        RectTransform _petBtn, _petDot;
        Image _petFace;
        Text _petTimer;
        bool _railVisible = true;

        /// <summary>The pet's own button, next to the menu: its face, and a small clock to its next
        /// patrol. A pet is the one thing on the farm that acts on its own, so when it will act is
        /// worth a glance; opening a menu to find out is not. Absent until pets unlock.</summary>
        void BuildPetButton(GameApp app)
        {
            _petBtn = UIKit.Node("petBtn", Root);
            _petBtn.Anchor(UIKit.BottomRight, new Vector2(-MenuMargin - MenuD - 16f, MenuMargin), new Vector2(MenuD, MenuD));
            var b = UIKit.IconBtn(_petBtn, null, Theme.Purple, 0.5f, () => { CloseMenu(); app.Open(new PetPanel(app)); });
            b.GetComponent<RectTransform>().Stretch();
            _petFace = UIKit.Img(_petBtn, null, Color.white, "face");
            _petFace.preserveAspect = true;
            _petFace.raycastTarget = false;
            _petFace.rectTransform.Anchor(UIKit.Center, new Vector2(0, 6), new Vector2(MenuD * 0.8f, MenuD * 0.8f));

            var chip = UIKit.Node("timer", _petBtn);
            chip.Anchor(UIKit.Bottom, new Vector2(0, -10), new Vector2(74, 26));
            var look = Looks.Glass;
            SurfaceLook.Add(chip, look, SurfaceLook.Pill);
            _petTimer = UIKit.LabelOutlined(chip, "", 15, Color.white, TextAnchor.MiddleCenter);
            _petTimer.rectTransform.Stretch();
            _petDot = Dot(_petBtn);
            _petBtn.gameObject.SetActive(false);
        }

        public RectTransform PetButton => _petBtn;

        void RenderPet()
        {
            if (_petBtn == null) return;
            var s = GS.Local;
            bool on = PetSys.Unlocked(s) && _railVisible;
            if (_petBtn.gameObject.activeSelf != on) _petBtn.gameObject.SetActive(on);
            if (!on) return;
            var pet = PetSys.Active(s);
            var face = pet != null ? Art.Load("Art/pets/" + pet.id + "/idle") : Art.Item("item_egg");
            if (_petFace.sprite != face) _petFace.sprite = face;
            // eggs waiting to hatch: the gift-code stock, or the free first egg
            SetDot(_petDot, s.petFreeEggs > 0 ? Mathf.Min(99, s.petFreeEggs) : s.petEggs == 0 ? 1 : 0);
            string t = "";
            if (pet != null && _app != null && _app.Pets != null)
            {
                int sec = _app.Pets.SecondsToPatrol;
                t = _app.Pets.Busy && sec == 0 ? "..." : (sec / 60) + ":" + (sec % 60).ToString("00");
            }
            else t = "Ấp";
            if (_petTimer.text != t) _petTimer.text = t;
        }

        void SetMenu(bool open, bool instant = false)
        {
            if (_menuPanel == null || (open == _menuOpen && !instant)) return;
            _menuOpen = open;
            _menuScrim.gameObject.SetActive(open);
            if (_menuIcon != null)
            {
                // The pack's cross is a heavy glyph; at the grid icon's size and full ink it read
                // as a black blot. Smaller and in soft ink it reads as "close".
                _menuIcon.sprite = open ? Theme.Skin.IconCross : Theme.Skin.More;
                _menuIcon.color = open ? Theme.InkSoft : Theme.Ink;
                var fit = _menuIcon.GetComponent<AspectFill>();
                if (fit != null) { fit.scale = open ? 0.36f : 0.5f; fit.Apply(); }
            }
            if (open)
            {
                _menuScrim.SetAsLastSibling();
                _menuPanel.SetAsLastSibling();
                _menuBtn.SetAsLastSibling();
            }
            if (!instant) Sfx.Play(open ? SfxId.MenuOpen : SfxId.MenuClose);
            if (_menuAnim != null) Tween.Kill(_menuAnim);
            _menuAnim = Tween.Run(MenuRoutine(open, instant));
        }

        /// <summary>Open: the panel rises 48 px out of the button while it fades in, and the rows
        /// follow it up from the bottom row, 25 ms apart. Close: everything drops back in 0.14 s.
        /// Short on purpose — this is opened dozens of times a session.</summary>
        IEnumerator MenuRoutine(bool open, bool instant)
        {
            var baseY = MenuMargin + MenuD + 14f;
            const float rise = 48f;
            if (open) _menuPanel.gameObject.SetActive(true);

            float time = instant ? 0f : (open ? 0.22f : 0.14f);
            float from = _menuGroup.alpha;
            float fromY = _menuPanel.anchoredPosition.y;
            float toY = open ? baseY : baseY - rise;
            if (open && !instant && from <= 0.01f) fromY = baseY - rise;

            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / time);
                float e = open ? Tween.EaseOut(k) : k * k;
                _menuGroup.alpha = Mathf.Lerp(from, open ? 1f : 0f, e);
                _menuPanel.anchoredPosition = new Vector2(_menuPanel.anchoredPosition.x, Mathf.Lerp(fromY, toY, e));
                for (int i = 0; i < _menuSlots.Count; i++)
                {
                    var slot = _menuSlots[i];
                    int rowFromBottom = 3 - _menuRows[i];
                    float delay = open ? rowFromBottom * 0.025f : 0f;
                    float sk = open ? Tween.EaseOut(Mathf.Clamp01((t - delay) / (time - 0.075f))) : 1f - e;
                    slot.GetComponent<CanvasGroup>().alpha = sk;
                    var cell = slot.GetChild(0) as RectTransform;
                    cell.anchoredPosition = new Vector2(0, -(1f - sk) * 18f);
                }
                yield return null;
            }

            _menuGroup.alpha = open ? 1f : 0f;
            _menuPanel.anchoredPosition = new Vector2(_menuPanel.anchoredPosition.x, toY);
            foreach (var slot in _menuSlots)
            {
                slot.GetComponent<CanvasGroup>().alpha = open ? 1f : 0f;
                ((RectTransform)slot.GetChild(0)).anchoredPosition = Vector2.zero;
            }
            if (!open) _menuPanel.gameObject.SetActive(false);
            _menuAnim = null;
        }

        /// <summary>Badges in the menu, and their sum on the menu button.</summary>
        void RenderDots(int chests)
        {
            if (_menuDot == null) return;

            // seeds: nothing to plant is worth a nudge, but only once the farm has somewhere to
            // put them — otherwise a new player is nagged about a problem they do not have
            int seeds = 0;
            foreach (var kv in GS.Local.seeds) seeds += kv.Value;
            bool needSeeds = seeds == 0 && _app != null && _app.Farm != null && _app.Farm.EmptyPlots().Count > 0;

            int claimable = GS.Local.ClaimableMissions;
            SetDot(_missionDot, claimable);

            // enough XP and coins to level: the one upgrade nobody should sit on unaware of
            var s = GS.Local;
            bool canLevel = s.xp >= s.XpNeed && s.coin >= GameData.Level(s.lv).cost;

            SetDot(_menuDots["seeds"], needSeeds ? -1 : 0);
            SetDot(_menuDots["quest"], claimable);
            SetDot(_menuDots["chest"], chests);
            SetDot(_menuDots["upgrade"], canLevel ? -1 : 0);
            // the account only asks for attention when saving to it is actually failing
            SetDot(_accountDot, Supa.SignedIn && (CloudSync.State == SyncState.Error || CloudSync.State == SyncState.Conflict) ? -1 : 0);

            int total = claimable + chests;
            SetDot(_menuDot, total > 0 ? total : (needSeeds || canLevel ? -1 : 0));
        }

        /// <summary>The worn frame replaces the portrait ring (drawn a little larger: the ornaments
        /// sit outside it), and a worn badge pushes the name along by its width.</summary>
        void RenderCosmetics()
        {
            var s = GS.Local;
            string frame = Cosmetics.Worn(s, CosmeticSlot.Frame);
            if (frame != _lastFrame)
            {
                _lastFrame = frame;
                var c = frame != null ? Cosmetics.Get(frame) : null;
                _avatarRing.sprite = c != null ? Art.Item(c.art) : Theme.Skin.AvatarRing;
                float pad = c != null ? -12f : -6f;
                _avatarRing.rectTransform.Stretch(pad, pad, pad, pad);
            }
            string badge = Cosmetics.Worn(s, CosmeticSlot.Badge);
            if (badge != _lastBadge)
            {
                _lastBadge = badge;
                var c = badge != null ? Cosmetics.Get(badge) : null;
                _badge.enabled = c != null;
                if (c != null) _badge.sprite = Art.Item(c.art);
                _name.rectTransform.anchoredPosition = new Vector2(c != null ? 122f : 92f, 15f);
                _name.rectTransform.sizeDelta = new Vector2(c != null ? 204f : 234f, 28f);
            }
        }

        // ------------------------------------------------------------
        // refresh
        // ------------------------------------------------------------
        public void Render(bool snap = false)
        {
            _level.text = GS.Local.lv.ToString();
            RenderCosmetics();

            // counting up digit by digit only reads below ten million; past that the chip says "22 tỷ"
            if (snap || _lastCoin < 0 || GS.Local.coin >= 10_000_000L) _coin.text = Fmt.Short(GS.Local.coin);
            else if (_lastCoin != GS.Local.coin) Tween.Count(_coin, GS.Local.coin);
            _lastCoin = GS.Local.coin;

            int need = GS.Local.XpNeed;
            _xpFill.fillAmount = Mathf.Clamp01(GS.Local.xp / (float)need);
            // a full bar says what to do with it, instead of a number that stopped moving
            string xpLine = GS.Local.xp >= need ? "Đủ XP · chạm để nâng cấp" : Fmt.N(GS.Local.xp) + " / " + Fmt.N(need);
            if (_xpText.text != xpLine) _xpText.text = xpLine;

            int goal = GS.Local.EnergyGoal;
            _energyFill.fillAmount = Mathf.Clamp01(GS.Local.energy / (float)goal);
            _energy.text = Fmt.N(GS.Local.energy) + "/" + Fmt.N(goal);

            int owned = 0;
            for (int i = 0; i < GS.Local.chests.Length; i++) owned += GS.Local.chests[i];
            _chestCount.text = owned > 0 ? "×" + owned : "";
            RenderPet();
            _chestIcon.color = owned > 0 ? Color.white : new Color(1, 1, 1, 0.72f);

            // --- the mission strip is conditional ---
            // A permanent "Đã hoàn thành mọi nhiệm vụ" is a 344 px banner whose only job is to
            // say there is nothing to say. When there is no active mission the strip goes away
            // and the verbs move up under the player card.
            var t = GS.Local.ActiveMission(out var pr);
            bool show = t != null;
            if (_missionChip.gameObject.activeSelf != show) _missionChip.gameObject.SetActive(show);
            if (show)
            {
                string mt = t.t + "  (" + pr.p + "/" + t.need + ")";
                if (mt != _lastMission) { _mission.text = mt; _lastMission = mt; }
            }

            RenderWeather();
            RenderActions();
            RenderDots(owned);
        }
    }
}
