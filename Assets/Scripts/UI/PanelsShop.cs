using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    // ================================================================
    // CỬA HÀNG HẠT GIỐNG
    // ================================================================
    public class SeedShopPanel : PanelBase
    {
        public SeedShopPanel(GameApp app) : base(app) { }
        public override string Title => "Cửa hàng hạt giống";
        public override Vector2 Size => new Vector2(1020, 570);
        public override Color Accent => Theme.GreenDeep;

        static string _sel;
        static int _qty = 1;

        RectTransform _grid, _detail;
        readonly Dictionary<string, RectTransform> _cells = new Dictionary<string, RectTransform>();
        Image _art;
        Text _name, _rarity, _price, _total, _qtyText;
        readonly Dictionary<string, Text> _stat = new Dictionary<string, Text>();
        Button _buy;

        public override void Build()
        {
            if (string.IsNullOrEmpty(_sel))
                _sel = GameData.Seeds.LastOrDefault(s => s.lv <= GS.lv)?.id ?? GameData.Seeds[0].id;

            // ---- catalogue ----
            var left = UIKit.Node("left", Body);
            left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0, 1);
            left.pivot = new Vector2(0, 0.5f);
            left.offsetMin = new Vector2(0, 0); left.offsetMax = new Vector2(572, 0);
            left.sizeDelta = new Vector2(572, 0);
            Well(left);

            _grid = UIKit.ScrollGrid(left, new Vector2(104, 116), new Vector2(12, 12), new RectOffset(14, 14, 14, 14));
            ((RectTransform)_grid.parent).Stretch(4, 4, 4, 4);

            foreach (var s in GameData.Seeds)
            {
                var seed = s;
                bool locked = s.lv > GS.lv;
                var cell = ItemCell(_grid, Art.Icon(s.art, 0), Color.white, s.r, null,
                                    locked ? "Cấp " + s.lv : Fmt.N(s.price), locked,
                                    () => { _sel = seed.id; _qty = 1; Refresh(); });

                if (!locked)
                {
                    var coin = UIKit.Img(cell, CoinIcon, Color.white, "coin");
                    coin.preserveAspect = true;
                    coin.rectTransform.Anchor(UIKit.Bottom, new Vector2(-34, 14), new Vector2(18, 18));
                }
                else
                {
                    var lockIc = UIKit.Img(cell, Theme.Skin.Lock, Color.white, "lock");
                    lockIc.preserveAspect = true;
                    lockIc.rectTransform.Anchor(UIKit.Center, new Vector2(0, 6), new Vector2(34, 34));
                }

                if (!string.IsNullOrEmpty(s.badge))
                {
                    var tagCol = s.badge == "exp" ? Theme.Blue : s.badge == "helm" ? Theme.Red : Theme.Green;
                    var tagTxt = s.badge == "exp" ? "EXP" : s.badge == "helm" ? "HOT" : "MỚI";
                    var tag = UIKit.Node("badge", cell);
                    tag.Anchor(UIKit.TopLeft, new Vector2(4, -4), new Vector2(46, 22));
                    UIKit.Round(tag, tagCol, 11, "bg").rectTransform.Stretch();
                    UIKit.Label(tag, tagTxt, 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold)
                         .rectTransform.Stretch();
                }

                _cells[s.id] = cell;
            }

            // ---- detail ----
            _detail = UIKit.Node("detail", Body);
            _detail.anchorMin = new Vector2(1, 0); _detail.anchorMax = new Vector2(1, 1);
            _detail.pivot = new Vector2(1, 0.5f);
            _detail.offsetMin = new Vector2(-386, 0); _detail.offsetMax = new Vector2(0, 0);
            UIKit.Round(_detail, Theme.Cream2, 22, "bg").rectTransform.Stretch();

            var artBox = UIKit.Node("art", _detail);
            artBox.Anchor(UIKit.Top, new Vector2(0, -8), new Vector2(112, 112));
            UIKit.Img(artBox, Theme.Glow(), Theme.Green.Alpha(0.28f), "glow").rectTransform.Stretch(-16, -16, -16, -16);
            _art = UIKit.Img(artBox, null, Color.white, "im");
            _art.preserveAspect = true;
            _art.rectTransform.Stretch(8, 8, 8, 8);

            _name = UIKit.Label(_detail, "", 26, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            _name.rectTransform.Anchor(UIKit.Top, new Vector2(0, -124), new Vector2(340, 30));

            _rarity = UIKit.Label(_detail, "", 16, Theme.InkSoft, TextAnchor.MiddleCenter);
            _rarity.rectTransform.Anchor(UIKit.Top, new Vector2(0, -152), new Vector2(340, 20));

            string[] keys = { "time", "xp", "en", "sell", "colors" };
            string[] labels = { "Thời gian chín", "Kinh nghiệm", "Năng lượng", "Giá bán", "Số loại" };
            for (int i = 0; i < keys.Length; i++)
            {
                var row = UIKit.Node("s", _detail);
                row.Anchor(UIKit.Top, new Vector2(0, -180 - i * 30), new Vector2(340, 26));
                UIKit.Round(row, Theme.Cream, 14, "bg").rectTransform.Stretch();
                UIKit.Label(row, labels[i], 18, Theme.InkSoft, TextAnchor.MiddleLeft)
                     .rectTransform.Stretch(14, 0, 120, 0);
                var v = UIKit.Label(row, "", 19, Theme.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
                v.rectTransform.Stretch(180, 0, 14, 0);
                _stat[keys[i]] = v;
            }

            // quantity stepper
            var qty = UIKit.Node("qty", _detail);
            qty.Anchor(UIKit.Bottom, new Vector2(0, 80), new Vector2(340, 44));
            UIKit.Round(qty, Theme.Cream, 24, "bg").rectTransform.Stretch();

            var minus = UIKit.IconBtn(qty, null, Theme.Red, 0.5f, () => { _qty = Mathf.Max(1, _qty - 1); Refresh(); });
            minus.GetComponent<RectTransform>().Anchor(UIKit.Left, new Vector2(26, 0), new Vector2(38, 38));
            UIKit.LabelOutlined(minus.transform, "-", 30, Color.white).rectTransform.Stretch(0, 0, 0, 4);

            var plus = UIKit.IconBtn(qty, null, Theme.Green, 0.5f, () => { _qty = Mathf.Min(99, _qty + 1); Refresh(); });
            plus.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-26, 0), new Vector2(38, 38));
            UIKit.LabelOutlined(plus.transform, "+", 28, Color.white).rectTransform.Stretch(0, 0, 0, 4);

            _qtyText = UIKit.Label(qty, "1", 24, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            _qtyText.rectTransform.Stretch(60, 0, 60, 0);

            _buy = UIKit.Btn(_detail, "Mua", Theme.Green, Theme.GreenDark, 25, 26, () => app.BuySeed(_sel, _qty));
            _buy.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 16), new Vector2(340, 54));
            _total = UIKit.BtnLabel(_buy);

            _price = UIKit.Label(_detail, "", 1, Color.clear);  // kept for layout symmetry

            Refresh();
        }

        public override void Refresh()
        {
            foreach (var kv in _cells)
            {
                var frame = kv.Value.Find("frame").GetComponent<Image>();
                var seed = GameData.Get(kv.Key);
                bool sel = kv.Key == _sel;
                bool locked = seed.lv > GS.lv;
                frame.color = sel ? Theme.GreenDeep : Theme.Rarity[seed.r].Alpha(locked ? 0.35f : 0.9f);
                float g = sel ? -6 : -3;
                frame.rectTransform.Stretch(g, g, g, g);
                kv.Value.localScale = Vector3.one * (sel ? 1.05f : 1f);
            }

            var s = GameData.Get(_sel);
            if (s == null) return;
            bool lockedSel = s.lv > GS.lv;

            _art.sprite = Art.Icon(s.art, 0);
            _name.text = s.name;
            string[] rn = { "Thường", "Hiếm", "Sử thi", "Huyền thoại" };
            _rarity.text = rn[s.r] + "  ·  mở ở cấp " + s.lv;
            _rarity.color = Theme.Rarity[s.r];

            _stat["time"].text = Fmt.Time(Mathf.RoundToInt(GS.GrowTime(s)));
            _stat["xp"].text = "+" + s.xp;
            _stat["en"].text = "+" + s.en;
            _stat["sell"].text = Fmt.N(GS.SellPrice(s.id, 0));
            _stat["colors"].text = s.colors + " loại";

            _qtyText.text = _qty.ToString();
            long cost = (long)s.price * _qty;

            _buy.interactable = !lockedSel;
            _total.text = lockedSel ? "Cần cấp " + s.lv : "Mua · " + Fmt.N(cost);
            UIKit.Restyle(_buy, lockedSel ? Theme.Cream3 : Theme.Green, lockedSel ? Theme.InkSoft : (Color?)null);
        }
    }

    // ================================================================
    // BẠN BÈ
    // ================================================================
    public class FriendsPanel : PanelBase
    {
        public FriendsPanel(GameApp app) : base(app) { }
        public override string Title => "Bạn bè";
        public override Vector2 Size => new Vector2(880, 540);
        public override Color Accent => Theme.Teal;

        static int _tab;
        RectTransform _list;
        Text _steal;
        Action<int> _setTab;

        public override void Build()
        {
            var tabs = UIKit.Node("tabs", Body);
            tabs.Anchor(UIKit.TopLeft, new Vector2(0, -4), new Vector2(460, 46));
            tabs.pivot = new Vector2(0, 1);
            _setTab = UIKit.Tabs(tabs, new[] { "Bạn bè", "Gợi ý", "Hồ sơ" }, i => { _tab = i; Refresh(); }, 146, 46, 10);

            var counter = UIKit.Node("counter", Body);
            counter.Anchor(UIKit.TopRight, new Vector2(0, -4), new Vector2(216, 46));
            UIKit.Round(counter, Theme.Cream2, 23, "bg").rectTransform.Stretch();
            var ic = UIKit.Img(counter, Theme.Skin.NavFriends, Theme.Teal, "ic");
            ic.preserveAspect = true;
            ic.rectTransform.Anchor(UIKit.Left, new Vector2(26, 0), new Vector2(28, 28));
            UIKit.Label(counter, "Lượt thăm", 16, Theme.InkSoft, TextAnchor.MiddleLeft)
                 .rectTransform.Stretch(48, 0, 70, 0);
            _steal = UIKit.Label(counter, "", 20, Theme.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            _steal.rectTransform.Stretch(120, 0, 16, 0);

            var box = UIKit.Node("box", Body);
            box.anchorMin = new Vector2(0, 0); box.anchorMax = new Vector2(1, 1);
            box.offsetMin = new Vector2(0, 0); box.offsetMax = new Vector2(0, -58);
            Well(box);
            _list = UIKit.ScrollList(box, 10f, new RectOffset(12, 12, 12, 12));
            ((RectTransform)_list.parent).Stretch(4, 4, 4, 4);

            Refresh();
        }

        public override void Refresh()
        {
            _setTab?.Invoke(_tab);
            _steal.text = (20 - GS.stealLeft) + " / 20";
            foreach (Transform c in _list) UnityEngine.Object.Destroy(c.gameObject);

            if (_tab == 2) { BuildProfile(); return; }

            bool suggest = _tab == 1;
            var pool = suggest ? GameData.Friends.Skip(3) : GameData.Friends.Take(3);

            foreach (var f in pool)
            {
                var friend = f;
                bool used = GS.visited.Contains(f.id);
                var row = Row(_list, 86f);

                var av = UIKit.Node("av", row);
                av.Anchor(UIKit.Left, new Vector2(58, 0), new Vector2(64, 64));
                UIKit.Img(av, Theme.Circle(), f.color.Alpha(0.22f), "disc").rectTransform.Stretch();
                var pic = UIKit.Img(av, Art.Crop(f.av), Color.white, "pic");
                pic.preserveAspect = true;
                pic.rectTransform.Stretch(10, 10, 10, 10);
                UIKit.Img(av, Theme.Ring(0.11f), f.color, "ring").rectTransform.Stretch();

                var lvBadge = UIKit.Node("lv", av);
                lvBadge.Anchor(UIKit.BottomRight, new Vector2(4, -2), new Vector2(30, 24));
                UIKit.Round(lvBadge, f.color, 12, "bg").rectTransform.Stretch();
                UIKit.Label(lvBadge, f.lv.ToString(), 16, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold)
                     .rectTransform.Stretch();

                var name = UIKit.Label(row, f.name, 22, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                name.rectTransform.Anchor(UIKit.TopLeft, new Vector2(104, -18), new Vector2(360, 28));
                name.rectTransform.pivot = new Vector2(0, 1);

                var tag = UIKit.Node("tag", row);
                tag.Anchor(UIKit.BottomLeft, new Vector2(104, 16), new Vector2(180, 26));
                tag.pivot = new Vector2(0, 0);
                var tagOn = suggest ? false : !used;
                UIKit.Round(tag, tagOn ? Theme.Green.Alpha(0.18f) : Theme.Cream3, 13, "bg").rectTransform.Stretch();
                UIKit.Label(tag, suggest ? "Chưa kết bạn" : used ? "Đã thăm hôm nay" : "Có thể thăm nom",
                            16, tagOn ? Theme.GreenDeep : Theme.InkSoft, TextAnchor.MiddleCenter)
                     .rectTransform.Stretch();

                var b = UIKit.Btn(row, suggest ? "Kết bạn" : "Thăm nom",
                                  suggest ? Theme.Blue : Theme.Teal,
                                  suggest ? Theme.BlueDeep : Theme.Hex("#268C80"), 22, 22,
                                  () => app.VisitFriend(friend, suggest));
                b.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-20, 0), new Vector2(170, 54));
            }

            Tween.Stagger(_list, 0.05f);
        }

        void BuildProfile()
        {
            void Stat(string title, string value, Sprite icon, Color col)
            {
                var row = Row(_list, 78f);
                var box = UIKit.Node("ic", row);
                box.Anchor(UIKit.Left, new Vector2(54, 0), new Vector2(56, 56));
                UIKit.Img(box, Theme.Circle(), col.Alpha(0.18f), "bg").rectTransform.Stretch();
                var im = UIKit.Img(box, icon, col, "im");
                im.preserveAspect = true;
                im.rectTransform.Stretch(12, 12, 12, 12);

                var t = UIKit.Label(row, title, 21, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                t.rectTransform.Anchor(UIKit.TopLeft, new Vector2(96, -14), new Vector2(500, 26));
                t.rectTransform.pivot = new Vector2(0, 1);

                var v = UIKit.Label(row, value, 18, Theme.InkSoft, TextAnchor.MiddleLeft);
                v.rectTransform.Anchor(UIKit.BottomLeft, new Vector2(96, 14), new Vector2(640, 24));
                v.rectTransform.pivot = new Vector2(0, 0);
            }

            Stat("Trang trại của bạn",
                 "Cấp " + GS.lv + " · " + GS.MaxPlots + " ô đất · " + GS.CollectedCount + " bộ sưu tập",
                 Theme.Skin.Farmhouse, Theme.Green);
            Stat("Tài sản",
                 Fmt.N(GS.coin) + " xu · " + Fmt.N(GS.energy) + " năng lượng kỳ diệu",
                 CoinIcon, Theme.Amber);
            Stat("Thống kê",
                 "Thu hoạch " + GS.stats.harvest + " · Gieo " + GS.stats.plant +
                 " · Tưới " + GS.stats.water + " · Đột biến " + GS.stats.mutate,
                 Theme.Skin.StarGold, Theme.Blue);
            Stat("Giao thương",
                 "Đã bán " + GS.stats.sell + " nông sản · mở " + GS.stats.chest + " rương",
                 Theme.Skin.NavStore, Theme.Purple);

            Tween.Stagger(_list, 0.05f);
        }
    }

    // ================================================================
    // CỬA HÀNG
    // ================================================================
    public class ShopPanel : PanelBase
    {
        public ShopPanel(GameApp app) : base(app) { }
        public override string Title => "Cửa hàng";
        public override Vector2 Size => new Vector2(1040, 580);
        public override Color Accent => Theme.AmberDeep;

        static int _tab;
        RectTransform _grid;
        Action<int> _setTab;

        public override void Build()
        {
            var tabs = UIKit.Node("tabs", Body);
            tabs.Anchor(UIKit.TopLeft, new Vector2(0, -4), new Vector2(380, 46));
            tabs.pivot = new Vector2(0, 1);
            _setTab = UIKit.Tabs(tabs, new[] { "Xu nông trại", "Vật phẩm" }, i => { _tab = i; Refresh(); }, 178, 46, 10);

            var wallet = UIKit.Node("wallet", Body);
            wallet.Anchor(UIKit.TopRight, new Vector2(0, -4), new Vector2(210, 46));
            UIKit.Round(wallet, Theme.Cream2, 23, "bg").rectTransform.Stretch();
            var ic = UIKit.Img(wallet, CoinIcon, Color.white, "ic");
            ic.preserveAspect = true;
            ic.rectTransform.Anchor(UIKit.Left, new Vector2(28, 0), new Vector2(32, 32));
            var money = UIKit.Label(wallet, Fmt.N(GS.coin), 22, Theme.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            money.rectTransform.Stretch(52, 0, 18, 0);
            _wallet = money;

            var box = UIKit.Node("box", Body);
            box.anchorMin = new Vector2(0, 0); box.anchorMax = new Vector2(1, 1);
            box.offsetMin = new Vector2(0, 0); box.offsetMax = new Vector2(0, -58);
            Well(box);
            _grid = UIKit.ScrollGrid(box, new Vector2(228, 218), new Vector2(16, 16), new RectOffset(16, 16, 16, 16));
            ((RectTransform)_grid.parent).Stretch(4, 4, 4, 4);

            Refresh();
        }

        Text _wallet;

        public override void Refresh()
        {
            _setTab?.Invoke(_tab);
            if (_wallet != null) _wallet.text = Fmt.N(GS.coin);
            foreach (Transform c in _grid) UnityEngine.Object.Destroy(c.gameObject);

            var items = _tab == 0 ? GameData.ShopCoin : GameData.ShopGoods;
            foreach (var it in items)
            {
                var item = it;
                bool sold = GS.shopBought.Contains(it.id);

                var card = UIKit.Node("card", _grid);
                var face = UIKit.Round(card, sold ? Theme.Cream3 : Theme.Cream, 20, "face");
                face.rectTransform.Stretch();
                face.raycastTarget = true;

                var timer = UIKit.Node("t", card);
                timer.Anchor(UIKit.TopRight, new Vector2(-8, -8), new Vector2(86, 24));
                UIKit.Round(timer, Theme.Ink.Alpha(0.14f), 12, "bg").rectTransform.Stretch();
                UIKit.Label(timer, it.time, 14, Theme.InkSoft, TextAnchor.MiddleCenter).rectTransform.Stretch();

                var artBox = UIKit.Node("art", card);
                artBox.Anchor(UIKit.Top, new Vector2(0, -20), new Vector2(96, 96));
                UIKit.Img(artBox, Theme.Glow(), Theme.Amber.Alpha(0.22f), "glow")
                     .rectTransform.Stretch(-14, -14, -14, -14);
                var im = UIKit.Img(artBox, Art.Crop(it.art), sold ? new Color(1, 1, 1, 0.5f) : Color.white, "im");
                im.preserveAspect = true;
                im.rectTransform.Stretch();

                var nm = UIKit.Label(card, it.name, 19, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                nm.rectTransform.Anchor(UIKit.Top, new Vector2(0, -122), new Vector2(210, 24));

                var sub = UIKit.Label(card, it.sub, 15, Theme.InkSoft, TextAnchor.MiddleCenter);
                sub.rectTransform.Anchor(UIKit.Top, new Vector2(0, -146), new Vector2(210, 20));

                var priceBox = UIKit.Node("p", card);
                priceBox.Anchor(UIKit.Bottom, new Vector2(0, 12), new Vector2(190, 36));
                UIKit.Round(priceBox, sold ? Theme.Cream3 : Theme.Amber.Alpha(0.2f), 19, "bg")
                     .rectTransform.Stretch();
                if (!sold)
                {
                    var ci = UIKit.Img(priceBox, CoinIcon, Color.white, "ic");
                    ci.preserveAspect = true;
                    ci.rectTransform.Anchor(UIKit.Left, new Vector2(24, 0), new Vector2(26, 26));
                }
                UIKit.Label(priceBox, sold ? "Đã mua" : Fmt.N(it.price), 20,
                            sold ? Theme.InkSoft : Theme.AmberDeep,
                            sold ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight, FontStyle.Bold)
                     .rectTransform.Stretch(sold ? 0 : 42, 0, sold ? 0 : 16, 0);

                var b = card.gameObject.AddComponent<Button>();
                b.targetGraphic = face;
                b.onClick.AddListener(() => app.BuyShopItem(item));
                card.gameObject.AddComponent<PressFx>();
            }

            Tween.Stagger(_grid, 0.03f);
        }
    }

    // ================================================================
    // BỘ SƯU TẬP
    // ================================================================
    public class CollectionPanel : PanelBase
    {
        public CollectionPanel(GameApp app) : base(app) { }
        public override string Title => "Bộ sưu tập";
        public override Vector2 Size => new Vector2(1000, 580);
        public override Color Accent => Theme.Red;

        RectTransform _list;
        Text _count;

        public override void Build()
        {
            // milestone header
            var head = UIKit.Node("head", Body);
            head.anchorMin = new Vector2(0, 1); head.anchorMax = new Vector2(1, 1);
            head.pivot = new Vector2(0.5f, 1);
            head.offsetMin = new Vector2(0, -78); head.offsetMax = new Vector2(0, 0);
            UIKit.Round(head, Theme.Cream2, 20, "bg").rectTransform.Stretch();

            var star = UIKit.Img(head, Theme.Skin.Crown, Theme.Amber, "ic");
            star.preserveAspect = true;
            star.rectTransform.Anchor(UIKit.Left, new Vector2(46, 0), new Vector2(56, 56));

            _count = UIKit.Label(head, "", 24, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            _count.rectTransform.Anchor(UIKit.Left, new Vector2(86, 12), new Vector2(260, 28));
            _count.rectTransform.pivot = new Vector2(0, 0.5f);

            var track = UIKit.Node("track", head);
            track.Anchor(UIKit.Left, new Vector2(86, -16), new Vector2(520, 28));
            track.pivot = new Vector2(0, 0.5f);
            var trackBar = UIKit.Bar(track, new Color(0.30f, 0.24f, 0.18f, 0.75f), Theme.Amber, 14);
            trackBar.transform.parent.GetComponent<RectTransform>().Stretch();
            _milestoneFill = trackBar;

            for (int i = 0; i < GameData.CollectMilestones.Length; i++)
            {
                int m = GameData.CollectMilestones[i];
                float x = m / (float)GameData.CollectMilestones.Last() * 520f;
                var node = UIKit.Node("m", track);
                node.Anchor(UIKit.Left, new Vector2(Mathf.Min(x, 512f), 0), new Vector2(34, 34));
                UIKit.Img(node, Theme.Circle(), Theme.Cream, "bg").rectTransform.Stretch();
                var ring = UIKit.Img(node, Theme.Ring(0.16f), Theme.AmberDeep, "ring");
                ring.rectTransform.Stretch();
                UIKit.Label(node, m.ToString(), 14, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold)
                     .rectTransform.Stretch();
                _nodes.Add((m, ring));
            }

            var claim = UIKit.Btn(head, "Nhận thưởng", Theme.Amber, Theme.AmberDeep, 22, 24,
                                  () => app.ClaimAllMilestones());
            claim.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-16, 0), new Vector2(196, 54));

            var box = UIKit.Node("box", Body);
            box.anchorMin = new Vector2(0, 0); box.anchorMax = new Vector2(1, 1);
            box.offsetMin = new Vector2(0, 0); box.offsetMax = new Vector2(0, -88);
            Well(box);
            _list = UIKit.ScrollList(box, 12f, new RectOffset(14, 14, 14, 14));
            ((RectTransform)_list.parent).Stretch(4, 4, 4, 4);

            Refresh();
        }

        Image _milestoneFill;
        readonly List<(int need, Image ring)> _nodes = new List<(int, Image)>();

        public override void Refresh()
        {
            int total = GS.CollectedCount;
            _count.text = "Đã sưu tầm " + total + "/" + GameData.CollectTotal;
            _milestoneFill.fillAmount = Mathf.Clamp01(total / (float)GameData.CollectMilestones.Last());
            foreach (var n in _nodes) n.ring.color = total >= n.need ? Theme.Green : Theme.Cream3;

            foreach (Transform c in _list) UnityEngine.Object.Destroy(c.gameObject);

            foreach (var set in GameData.Collections)
            {
                var theSet = set;
                int have = set.items.Count(it => GS.collected.Contains(it.Key));
                bool full = have >= set.items.Length;
                bool claimed = GS.claimedSets.Contains(set.id);

                var block = UIKit.Node("set", _list);
                var le = block.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 172; le.minHeight = 172;
                UIKit.Round(block, Theme.Cream, 18, "bg").rectTransform.Stretch();

                var title = UIKit.Label(block, set.name, 22, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                title.rectTransform.Anchor(UIKit.TopLeft, new Vector2(20, -14), new Vector2(340, 28));
                title.rectTransform.pivot = new Vector2(0, 1);

                var badge = UIKit.Node("n", block);
                badge.Anchor(UIKit.TopLeft, new Vector2(368, -14), new Vector2(72, 26));
                badge.pivot = new Vector2(0, 1);
                UIKit.Round(badge, full ? Theme.Green.Alpha(0.2f) : Theme.Cream3, 13, "bg").rectTransform.Stretch();
                UIKit.Label(badge, have + "/" + set.items.Length, 16,
                            full ? Theme.GreenDeep : Theme.InkSoft, TextAnchor.MiddleCenter, FontStyle.Bold)
                     .rectTransform.Stretch();

                for (int i = 0; i < set.items.Length; i++)
                {
                    var it = set.items[i];
                    var seed = GameData.Get(it.crop);
                    if (seed == null) continue;
                    bool got = GS.collected.Contains(it.Key);
                    var cell = ItemCell(block, Art.Icon(seed.art, it.v), Art.VariantTint(seed.art, it.v),
                                        seed.r, null, got ? it.name : "Chưa có", !got);
                    cell.Anchor(UIKit.BottomLeft, new Vector2(20 + i * 108, 12), new Vector2(98, 98));
                }

                // reward chips + claim
                var rw = UIKit.Node("rw", block);
                rw.Anchor(UIKit.BottomRight, new Vector2(-206, 16), new Vector2(126, 34));
                RewardChip(rw, CoinIcon, Fmt.N(set.coin), Theme.AmberDeep).Stretch();
                var rw2 = UIKit.Node("rw2", block);
                rw2.Anchor(UIKit.BottomRight, new Vector2(-206, 58), new Vector2(126, 34));
                RewardChip(rw2, XpIcon, Fmt.N(set.xp), Theme.Blue).Stretch();

                if (claimed)
                {
                    var tag = UIKit.Node("done", block);
                    tag.Anchor(UIKit.Right, new Vector2(-20, -6), new Vector2(160, 50));
                    UIKit.Round(tag, Theme.Cream3, 25, "bg").rectTransform.Stretch();
                    UIKit.Label(tag, "Đã nhận", 20, Theme.InkSoft, TextAnchor.MiddleCenter, FontStyle.Bold)
                         .rectTransform.Stretch();
                }
                else
                {
                    var b = UIKit.Btn(block, full ? "Nhận thưởng" : "Còn thiếu " + (set.items.Length - have),
                                      full ? Theme.Green : Theme.Cream3,
                                      full ? Theme.GreenDark : Theme.Hex("#B9A98C"), 20, 24,
                                      () => app.ClaimSet(theSet));
                    b.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-20, -6), new Vector2(176, 54));
                }
            }

            Tween.Stagger(_list, 0.05f);
        }
    }
}
