using System;
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

        Text _coin, _energy, _collect, _mission, _level, _xpText;
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
            BuildCurrencies(app);
            BuildMissionChip(app);
            BuildRails(app);
            BuildActionBar(app);
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
            var bg = UIKit.Round(card, new Color(0.09f, 0.19f, 0.15f, 0.60f), 26, "bg");
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
            name.rectTransform.Anchor(UIKit.Left, new Vector2(96, 15), new Vector2(228, 22));
            name.rectTransform.pivot = new Vector2(0, 0.5f);

            var bar = UIKit.Node("xp", card);
            bar.Anchor(UIKit.Left, new Vector2(96, -12), new Vector2(228, 22));
            bar.pivot = new Vector2(0, 0.5f);
            _xpFill = UIKit.Bar(bar, Theme.TrackDark, Theme.Green, 11);
            _xpFill.transform.parent.GetComponent<RectTransform>().Stretch();
            _xpText = UIKit.LabelOutlined(bar, "0/0", 15, Color.white);
            _xpText.rectTransform.Stretch();

            // --- the mission you are on, docked to the card so they read as one block ---
            var strip = UIKit.Node("missionStrip", cluster);
            strip.Anchor(UIKit.TopLeft, new Vector2(0, -80), new Vector2(344, 40));
            strip.pivot = new Vector2(0, 1);
            _missionChip = strip;

            var sbg = UIKit.Round(strip, new Color(0.09f, 0.19f, 0.15f, 0.60f), 20, "bg");
            sbg.rectTransform.Stretch();
            sbg.raycastTarget = true;

            var ic = UIKit.Img(strip, Art.Ui("ic_alert"), Theme.Amber, "ic");
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
            cluster.Anchor(UIKit.TopRight, new Vector2(-16, -14), new Vector2(420, 108));
            cluster.pivot = new Vector2(1, 1);

            // --- row 1: magic energy, then coins ---
            var enChip = UIKit.Node("energyChip", cluster);
            enChip.Anchor(UIKit.TopLeft, Vector2.zero, new Vector2(206, 52));
            enChip.pivot = new Vector2(0, 1);
            var enBg = UIKit.Round(enChip, new Color(0.09f, 0.19f, 0.15f, 0.62f), 26, "bg");
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
            enBar.Anchor(UIKit.Right, new Vector2(-14, -11), new Vector2(126, 12));
            _energyFill = UIKit.Bar(enBar, Theme.TrackDark, Theme.Purple, 6);
            _energyFill.transform.parent.GetComponent<RectTransform>().Stretch();

            _energy = UIKit.LabelOutlined(enChip, "0/300", 16, Color.white, TextAnchor.MiddleRight);
            _energy.rectTransform.Anchor(UIKit.Right, new Vector2(-14, 10), new Vector2(126, 20));

            var enBtn = enChip.gameObject.AddComponent<Button>();
            enBtn.targetGraphic = enBg;
            enBtn.onClick.AddListener(() => app.Open(new ChestPanel(app)));
            enChip.gameObject.AddComponent<PressFx>();

            var coinChip = UIKit.Node("coinChip", cluster);
            coinChip.Anchor(UIKit.TopRight, Vector2.zero, new Vector2(206, 52));
            coinChip.pivot = new Vector2(1, 1);
            _coin = UIKit.Chip(coinChip, Theme.Skin.Coin, "0", new Color(0.09f, 0.19f, 0.15f, 0.62f), 206,
                               () => app.Open(new ShopPanel(app)));
            coinChip.GetChild(0).GetComponent<RectTransform>().Stretch();

            // --- row 2: collection, right-aligned with the coin chip above it ---
            var colChip = UIKit.Node("collectChip", cluster);
            colChip.Anchor(UIKit.TopRight, new Vector2(0, -60), new Vector2(206, 44));
            colChip.pivot = new Vector2(1, 1);
            var colBg = UIKit.Round(colChip, new Color(0.09f, 0.19f, 0.15f, 0.62f), 22, "bg");
            colBg.rectTransform.Stretch();
            colBg.raycastTarget = true;
            var colIc = UIKit.Img(colChip, Theme.Skin.NavAlbum, Color.white, "ic");
            colIc.preserveAspect = true;
            colIc.rectTransform.Anchor(UIKit.Left, new Vector2(26, 0), new Vector2(28, 28));
            _collect = UIKit.LabelOutlined(colChip, "0/135", 17, Color.white, TextAnchor.MiddleRight);
            _collect.rectTransform.Stretch(48, 0, 18, 0);
            var colBtn = colChip.gameObject.AddComponent<Button>();
            colBtn.targetGraphic = colBg;
            colBtn.onClick.AddListener(() => app.Open(new CollectionPanel(app)));
            colChip.gameObject.AddComponent<PressFx>();
        }

        /// <summary>Kept so Build() reads in layout order; the strip lives in the player cluster.</summary>
        void BuildMissionChip(GameApp app) { }

        // ------------------------------------------------------------
        // side rails
        // ------------------------------------------------------------
        void BuildRails(GameApp app)
        {
            var left = UIKit.Node("railL", Root);
            left.Anchor(UIKit.Left, new Vector2(62, -6), new Vector2(104, 330));
            RailPlate(left);
            Rail(left, new (Sprite art, string cap, Color col, Action act)[]
            {
                (Theme.Skin.NavShop,  "Cửa hàng", Theme.Amber,  () => app.Open(new ShopPanel(app))),
                (Theme.Skin.NavMagic, "Rương",    Theme.Purple, () => app.Open(new ChestPanel(app))),
                (Theme.Skin.NavQuest, "Nhiệm vụ", Theme.Blue,   () => app.Open(new MissionsPanel(app))),
            });

            var right = UIKit.Node("railR", Root);
            right.Anchor(UIKit.Right, new Vector2(-62, -6), new Vector2(104, 434));
            RailPlate(right);
            var rightBtns = Rail(right, new (Sprite, string, Color, Action)[]
            {
                (Theme.Skin.NavSeeds,   "Hạt giống",  Theme.Green,     () => app.Open(new SeedShopPanel(app))),
                (Theme.Skin.NavStore,   "Kho",        Theme.AmberDeep, () => app.Open(new WarehousePanel(app))),
                (Theme.Skin.NavFriends, "Bạn bè",     Theme.Teal,      () => app.Open(new FriendsPanel(app))),
                (Theme.Skin.NavAlbum,   "Bộ sưu tập", Theme.Red,       () => app.Open(new CollectionPanel(app))),
            });
            _warehouseBtn = rightBtns[1];
        }

        /// <summary>Backing plate so a rail reads as one bar rather than loose floating discs.</summary>
        static void RailPlate(RectTransform rail)
        {
            var bg = UIKit.Round(rail, new Color(0.08f, 0.17f, 0.14f, 0.34f), 26, "plate");
            bg.rectTransform.Stretch(-4, -8, -4, -8);
        }

        RectTransform[] Rail(RectTransform holder, (Sprite art, string cap, Color col, Action act)[] items)
        {
            const float step = 104f;
            float total = items.Length * step;
            var outp = new RectTransform[items.Length];

            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var slot = UIKit.Node("slot", holder);
                slot.Anchor(UIKit.Center, new Vector2(0, total / 2f - step * (i + 0.5f)), new Vector2(78, 78));
                var b = UIKit.IconBtn(slot, it.art, it.col, 0.52f, it.act, it.cap);
                b.GetComponent<RectTransform>().Stretch();
                outp[i] = slot;
            }
            return outp;
        }

        // ------------------------------------------------------------
        // bottom: primary actions
        // ------------------------------------------------------------
        void BuildActionBar(GameApp app)
        {
            var bar = UIKit.Node("actions", Root);
            bar.Anchor(UIKit.Bottom, new Vector2(0, 18), new Vector2(664, 64));

            // a plate so the row reads as one bar instead of three buttons floating
            // on the island's lower edge
            var plate = UIKit.Round(bar, new Color(0.08f, 0.17f, 0.14f, 0.32f), 26, "plate");
            plate.rectTransform.Stretch(-14, -10, -14, -10);

            var harvest = UIKit.Btn(bar, "Thu hoạch tất cả", Theme.Green, Theme.GreenDark, 24, 26,
                                    () => app.HarvestAll());
            harvest.GetComponent<RectTransform>().Anchor(UIKit.Center, new Vector2(-194, 0), new Vector2(276, 64));

            var plant = UIKit.Btn(bar, "Gieo tất cả", Theme.Amber, Theme.AmberDeep, 24, 26,
                                  () => app.PlantAll());
            plant.GetComponent<RectTransform>().Anchor(UIKit.Center, new Vector2(50, 0), new Vector2(196, 64));

            var up = UIKit.Btn(bar, "Nâng cấp", Theme.Blue, Theme.BlueDeep, 24, 26,
                               () => app.Open(new UpgradePanel(app)));
            up.GetComponent<RectTransform>().Anchor(UIKit.Center, new Vector2(238, 0), new Vector2(160, 64));
        }

        // ------------------------------------------------------------
        // refresh
        // ------------------------------------------------------------
        public void Render(bool snap = false)
        {
            _level.text = GS.lv.ToString();

            if (snap || _lastCoin < 0) _coin.text = Fmt.N(GS.coin);
            else if (_lastCoin != GS.coin) Tween.Count(_coin, GS.coin);
            _lastCoin = GS.coin;

            int need = GS.XpNeed;
            _xpFill.fillAmount = Mathf.Clamp01(GS.xp / (float)need);
            _xpText.text = Fmt.N(Math.Min(GS.xp, need)) + " / " + Fmt.N(need);

            int goal = GS.EnergyGoal;
            _energyFill.fillAmount = Mathf.Clamp01(GS.energy / (float)goal);
            _energy.text = Fmt.N(GS.energy) + "/" + Fmt.N(goal);

            int tier = GS.ChestTier();
            int owned = 0;
            for (int i = 0; i < GS.chests.Length; i++) owned += GS.chests[i];
            _chestCount.text = owned > 0 ? "x" + owned : "";
            _chestIcon.color = owned > 0 ? Color.white : new Color(1, 1, 1, 0.72f);

            _collect.text = GS.CollectedCount + "/" + GameData.CollectTotal;

            var t = GS.ActiveMission(out var pr);
            string mt = t != null ? t.t + "  (" + pr.p + "/" + t.need + ")" : "Đã hoàn thành mọi nhiệm vụ";
            if (mt != _lastMission) { _mission.text = mt; _lastMission = mt; }
        }
    }
}
