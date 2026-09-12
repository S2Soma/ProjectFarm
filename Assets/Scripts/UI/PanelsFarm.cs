using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    // ================================================================
    // NÂNG CẤP TRANG TRẠI
    // ================================================================
    public class UpgradePanel : PanelBase
    {
        public UpgradePanel(GameApp app) : base(app) { }
        public override string Title => "Nâng cấp trang trại";
        public override Vector2 Size => new Vector2(880, 540);
        public override Color Accent => Theme.BlueDeep;

        Text _lvA, _lvB, _xpNum, _cost, _unlockName;
        Image _xpFill, _unlockArt;
        readonly List<(Text from, Text to)> _rows = new List<(Text, Text)>();
        Button _go;

        static readonly string[] EffectNames =
        {
            "Thời gian chín được rút ngắn",
            "Xác suất đột biến tăng lên",
            "Giá nông sản tăng",
            "Tăng cường năng lượng kỳ diệu",
        };

        public override void Build()
        {
            // ---- level header ----
            var head = UIKit.Node("head", Body);
            head.Anchor(UIKit.Top, new Vector2(0, 0), new Vector2(0, 92));
            head.anchorMin = new Vector2(0, 1); head.anchorMax = new Vector2(1, 1);
            head.offsetMin = new Vector2(0, -92); head.offsetMax = new Vector2(0, 0);

            _lvA = LevelBadge(head, -244, Theme.Cream3, Theme.InkSoft);
            _lvB = LevelBadge(head, 244, Theme.Amber, Color.white);

            var barBox = UIKit.Node("bar", head);
            barBox.Anchor(UIKit.Center, new Vector2(0, -2), new Vector2(360, 30));
            _xpFill = UIKit.Bar(barBox, Theme.TrackDark, Theme.Green, 15);
            _xpFill.transform.parent.GetComponent<RectTransform>().Stretch();
            _xpNum = UIKit.LabelOutlined(barBox, "", 18, Color.white);
            _xpNum.rectTransform.Stretch();

            UIKit.Label(head, "Kinh nghiệm", 16, Theme.InkSoft)
                 .rectTransform.Anchor(UIKit.Center, new Vector2(0, -30), new Vector2(300, 20));

            // ---- effects ----
            var list = UIKit.Node("effects", Body);
            list.anchorMin = new Vector2(0, 0); list.anchorMax = new Vector2(1, 1);
            list.offsetMin = new Vector2(0, 92); list.offsetMax = new Vector2(-268, -102);
            Well(list);

            for (int i = 0; i < EffectNames.Length; i++)
            {
                var row = UIKit.Node("r", list);
                row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
                row.pivot = new Vector2(0.5f, 1);
                row.offsetMin = new Vector2(12, 0); row.offsetMax = new Vector2(-12, 0);
                row.anchoredPosition = new Vector2(0, -12 - i * 54);
                row.sizeDelta = new Vector2(row.sizeDelta.x, 46);

                UIKit.Round(row, Theme.Cream, 14, "bg").rectTransform.Stretch();
                UIKit.Label(row, EffectNames[i], 19, Theme.Ink, TextAnchor.MiddleLeft)
                     .rectTransform.Stretch(16, 0, 200, 0);

                var from = UIKit.Label(row, "", 19, Theme.InkSoft, TextAnchor.MiddleRight, FontStyle.Bold);
                from.rectTransform.Anchor(UIKit.Right, new Vector2(-128, 0), new Vector2(84, 30));

                var arrow = UIKit.Img(row, Theme.Skin.ArrowRight, Theme.Amber, "arrow");
                arrow.preserveAspect = true;
                arrow.rectTransform.Anchor(UIKit.Right, new Vector2(-92, 0), new Vector2(22, 22));

                var to = UIKit.Label(row, "", 19, Theme.GreenDeep, TextAnchor.MiddleRight, FontStyle.Bold);
                to.rectTransform.Anchor(UIKit.Right, new Vector2(-14, 0), new Vector2(84, 30));

                _rows.Add((from, to));
            }

            // ---- unlock preview ----
            var side = UIKit.Node("unlock", Body);
            side.anchorMin = new Vector2(1, 0); side.anchorMax = new Vector2(1, 1);
            side.pivot = new Vector2(1, 0.5f);
            side.offsetMin = new Vector2(-252, 92); side.offsetMax = new Vector2(0, -102);
            UIKit.Round(side, Theme.Cream2, 20, "bg").rectTransform.Stretch();

            UIKit.Label(side, "Mở khoá ở cấp tới", 18, Theme.InkSoft)
                 .rectTransform.Anchor(UIKit.Top, new Vector2(0, -16), new Vector2(220, 24));

            var art = UIKit.Node("art", side);
            art.Anchor(UIKit.Center, new Vector2(0, 8), new Vector2(150, 150));
            UIKit.Img(art, Theme.Glow(), Theme.Amber.Alpha(0.35f), "glow").rectTransform.Stretch(-14, -14, -14, -14);
            _unlockArt = UIKit.Img(art, null, Color.white, "im");
            _unlockArt.preserveAspect = true;
            _unlockArt.rectTransform.Stretch(10, 10, 10, 10);

            _unlockName = UIKit.Label(side, "", 20, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            _unlockName.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 22), new Vector2(220, 26));

            // ---- footer ----
            var foot = UIKit.Node("foot", Body);
            foot.anchorMin = new Vector2(0, 0); foot.anchorMax = new Vector2(1, 0);
            foot.pivot = new Vector2(0.5f, 0);
            foot.offsetMin = new Vector2(0, 0); foot.offsetMax = new Vector2(0, 76);

            var costBox = UIKit.Node("cost", foot);
            costBox.Anchor(UIKit.Left, new Vector2(16, 6), new Vector2(240, 52));
            UIKit.Round(costBox, Theme.Cream2, 26, "bg").rectTransform.Stretch();
            var ci = UIKit.Img(costBox, CoinIcon, Color.white, "ic");
            ci.preserveAspect = true;
            ci.rectTransform.Anchor(UIKit.Left, new Vector2(28, 0), new Vector2(34, 34));
            _cost = UIKit.Label(costBox, "", 24, Theme.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            _cost.rectTransform.Stretch(52, 0, 18, 0);

            _go = UIKit.Btn(foot, "Nâng cấp ngay", Theme.Green, Theme.GreenDark, 26, 26, () => app.DoUpgrade());
            _go.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-16, 6), new Vector2(268, 60));

            Refresh();
        }

        Text LevelBadge(Transform parent, float x, Color face, Color ink)
        {
            var box = UIKit.Node("lv", parent);
            box.Anchor(UIKit.Center, new Vector2(x, 0), new Vector2(112, 66));
            UIKit.Round(box, face, 20, "bg").rectTransform.Stretch();
            UIKit.Label(box, "CẤP", 14, ink.Alpha(0.75f))
                 .rectTransform.Anchor(UIKit.Top, new Vector2(0, -8), new Vector2(100, 16));
            var t = UIKit.Label(box, "1", 34, ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Anchor(UIKit.Center, new Vector2(0, -8), new Vector2(100, 40));
            return t;
        }

        public override void Refresh()
        {
            var a = GameData.Level(GS.lv);
            var b = GameData.Level(GS.lv + 1);

            _lvA.text = GS.lv.ToString();
            _lvB.text = (GS.lv + 1).ToString();
            _xpFill.fillAmount = Mathf.Clamp01(GS.xp / (float)a.xpNeed);
            _xpNum.text = Fmt.N(Math.Min(GS.xp, a.xpNeed)) + " / " + Fmt.N(a.xpNeed);

            var fromVals = new[] { a.growCut, a.mutate, a.priceUp, a.energyUp };
            var toVals = new[] { b.growCut, b.mutate, b.priceUp, b.energyUp };
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].from.text = Fmt.Pct(fromVals[i]);
                _rows[i].to.text = Fmt.Pct(toVals[i]);
            }

            var next = GameData.Seeds.FirstOrDefault(s => s.lv == GS.lv + 1);
            if (next != null)
            {
                _unlockArt.enabled = true;
                _unlockArt.sprite = Art.Icon(next.art, 0);
                _unlockName.text = next.name;
            }
            else if (b.plots > a.plots)
            {
                _unlockArt.enabled = true;
                _unlockArt.sprite = Art.TileEmpty;
                _unlockName.text = "+1 ô đất trồng";
            }
            else
            {
                _unlockArt.enabled = false;
                _unlockName.text = "Không có nội dung mới";
            }

            _cost.text = Fmt.N(a.cost);
            bool can = GS.coin >= a.cost && GS.xp >= a.xpNeed;
            _go.interactable = true;
            UIKit.BtnLabel(_go).text = can ? "Nâng cấp ngay" : "Chưa đủ điều kiện";
            UIKit.Restyle(_go, can ? Theme.Green : Theme.Cream3, can ? (Color?)null : Theme.InkSoft);
        }
    }

    // ================================================================
    // RƯƠNG THẦN KỲ
    // ================================================================
    public class ChestPanel : PanelBase
    {
        public ChestPanel(GameApp app) : base(app) { }
        public override string Title => "Năng lượng kỳ diệu";
        public override Vector2 Size => new Vector2(880, 520);
        public override Color Accent => Theme.Purple;

        static int _sel = -1;
        readonly List<RectTransform> _cells = new List<RectTransform>();
        readonly List<Text> _counts = new List<Text>();
        Text _name, _desc, _energyText;
        Image _energyFill;
        Button _open;

        public override void Build()
        {
            if (_sel < 0) _sel = GS.ChestTier();

            // energy progress toward the next chest
            var top = UIKit.Node("energy", Body);
            top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1);
            top.pivot = new Vector2(0.5f, 1);
            top.offsetMin = new Vector2(0, -66); top.offsetMax = new Vector2(0, 0);
            UIKit.Round(top, Theme.Cream2, 20, "bg").rectTransform.Stretch();

            var icon = UIKit.Img(top, Theme.Skin.NavMagic, Theme.Purple, "ic");
            icon.preserveAspect = true;
            icon.rectTransform.Anchor(UIKit.Left, new Vector2(36, 0), new Vector2(38, 38));

            var barBox = UIKit.Node("bar", top);
            barBox.Stretch(70, 18, 22, 18);
            _energyFill = UIKit.Bar(barBox, Theme.TrackDark, Theme.Purple, 15);
            _energyFill.transform.parent.GetComponent<RectTransform>().Stretch();
            _energyText = UIKit.LabelOutlined(barBox, "", 19, Color.white);
            _energyText.rectTransform.Stretch();

            // chest row
            var row = UIKit.Node("chests", Body);
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
            row.pivot = new Vector2(0.5f, 1);
            row.offsetMin = new Vector2(0, -270); row.offsetMax = new Vector2(0, -78);

            var tints = new[] { Theme.Teal, Theme.Blue, Theme.Purple, Theme.Amber };
            for (int i = 0; i < GameData.Chests.Length; i++)
            {
                int idx = i;
                var cell = UIKit.Node("chest", row);
                cell.Anchor(UIKit.Center, new Vector2((i - 1.5f) * 196f, 0), new Vector2(180, 184));

                var face = UIKit.Round(cell, Theme.Cream, 22, "face");
                face.rectTransform.Stretch();
                face.raycastTarget = true;
                var frame = UIKit.Img(cell, Theme.Round(22), tints[i], "frame");
                frame.type = Image.Type.Sliced;
                frame.rectTransform.Stretch(-4, -4, -4, -4);
                frame.transform.SetAsFirstSibling();

                UIKit.Img(cell, Theme.Glow(), tints[i].Alpha(0.30f), "glow")
                     .rectTransform.Anchor(UIKit.Center, new Vector2(0, 10), new Vector2(150, 150));

                var art = UIKit.Img(cell, Theme.Skin.NavMagic, tints[i], "art");
                art.preserveAspect = true;
                art.rectTransform.Anchor(UIKit.Center, new Vector2(0, 14), new Vector2(112, 112));

                UIKit.Label(cell, GameData.Chests[i].name, 16, Theme.InkSoft, TextAnchor.MiddleCenter)
                     .rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 34), new Vector2(170, 20));

                var cnt = UIKit.Label(cell, "x0", 22, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                cnt.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 12), new Vector2(170, 24));

                var b = cell.gameObject.AddComponent<Button>();
                b.targetGraphic = face;
                b.onClick.AddListener(() => { _sel = idx; Refresh(); });
                cell.gameObject.AddComponent<PressFx>();

                _cells.Add(cell);
                _counts.Add(cnt);
            }

            // description + action
            var info = UIKit.Node("info", Body);
            info.anchorMin = new Vector2(0, 0); info.anchorMax = new Vector2(1, 0);
            info.pivot = new Vector2(0.5f, 0);
            info.offsetMin = new Vector2(0, 0); info.offsetMax = new Vector2(0, 140);
            UIKit.Round(info, Theme.Cream2, 20, "bg").rectTransform.Stretch();

            _name = UIKit.Label(info, "", 24, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            _name.rectTransform.Anchor(UIKit.TopLeft, new Vector2(24, -14), new Vector2(400, 30));
            _name.rectTransform.pivot = new Vector2(0, 1);

            _desc = UIKit.Label(info, "", 18, Theme.InkSoft, TextAnchor.UpperLeft);
            _desc.rectTransform.anchorMin = new Vector2(0, 0); _desc.rectTransform.anchorMax = new Vector2(1, 1);
            _desc.rectTransform.offsetMin = new Vector2(24, 16); _desc.rectTransform.offsetMax = new Vector2(-280, -52);
            _desc.horizontalOverflow = HorizontalWrapMode.Wrap;

            _open = UIKit.Btn(info, "Mở tất cả", Theme.Amber, Theme.AmberDeep, 25, 26, () => app.OpenChests(_sel));
            _open.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-22, 0), new Vector2(238, 62));

            Refresh();
        }

        public override void Refresh()
        {
            int tier = GS.ChestTier();
            _energyFill.fillAmount = Mathf.Clamp01(GS.energy / (float)GS.EnergyGoal);
            _energyText.text = Fmt.N(GS.energy) + " / " + Fmt.N(GS.EnergyGoal) + "  ·  " + GameData.Chests[tier].name;

            for (int i = 0; i < _cells.Count; i++)
            {
                bool sel = i == _sel;
                bool locked = i > tier;
                var face = _cells[i].Find("face").GetComponent<Image>();
                var frame = _cells[i].Find("frame").GetComponent<Image>();
                face.color = sel ? Color.white : Theme.Cream.Alpha(0.85f);
                frame.rectTransform.Stretch(sel ? -6 : -4, sel ? -6 : -4, sel ? -6 : -4, sel ? -6 : -4);
                var art = _cells[i].Find("art").GetComponent<Image>();
                art.color = locked ? new Color(0.55f, 0.55f, 0.58f, 0.85f) : Color.white;
                _cells[i].localScale = Vector3.one * (sel ? 1.04f : 0.98f);
                _counts[i].text = "x" + GS.chests[i];
            }

            var c = GameData.Chests[_sel];
            _name.text = c.name;
            _desc.text = c.desc;
            int n = GS.chests[_sel];
            UIKit.BtnLabel(_open).text = n > 0 ? "Mở tất cả x" + n : "Chưa có rương";
            UIKit.Restyle(_open, n > 0 ? Theme.Amber : Theme.Cream3, n > 0 ? (Color?)null : Theme.InkSoft);
        }
    }

    // ================================================================
    // NHIỆM VỤ
    // ================================================================
    public class MissionsPanel : PanelBase
    {
        public MissionsPanel(GameApp app) : base(app) { }
        public override string Title => "Nhiệm vụ";
        public override Vector2 Size => new Vector2(900, 600);
        public override Color Accent => Theme.BlueDeep;

        static bool _daily;
        static int _chapter = -1;
        RectTransform _list, _chapterBar;
        Text _chNum, _chName;
        Action<int> _setTab;

        static int ActiveChapter()
        {
            for (int i = 0; i < GameData.Chapters.Length; i++)
                if (GameData.Chapters[i].tasks.Any(t => !GS.Progress(t, false).claimed)) return i;
            return GameData.Chapters.Length - 1;
        }

        public override void Build()
        {
            if (_chapter < 0) _chapter = ActiveChapter();

            var tabs = UIKit.Node("tabs", Body);
            tabs.Anchor(UIKit.Top, new Vector2(-150, -4), new Vector2(320, 46));
            _setTab = UIKit.Tabs(tabs, new[] { "Chương truyện", "Hằng ngày" },
                                 i => { _daily = i == 1; Refresh(); }, 158, 46, 10);
            tabs.GetChild(0).GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            // chapter stepper
            _chapterBar = UIKit.Node("chapter", Body);
            _chapterBar.Anchor(UIKit.Top, new Vector2(258, -4), new Vector2(300, 46));
            UIKit.Round(_chapterBar, Theme.Cream2, 23, "bg").rectTransform.Stretch();

            var prev = UIKit.IconBtn(_chapterBar, Theme.Skin.ArrowLeft, Theme.Blue, 0.5f,
                                     () => { _chapter = Mathf.Max(0, _chapter - 1); Refresh(); });
            prev.GetComponent<RectTransform>().Anchor(UIKit.Left, new Vector2(24, 0), new Vector2(40, 40));

            var next = UIKit.IconBtn(_chapterBar, Theme.Skin.ArrowRight, Theme.Blue, 0.5f,
                                     () => { _chapter = Mathf.Min(GameData.Chapters.Length - 1, _chapter + 1); Refresh(); });
            next.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-24, 0), new Vector2(40, 40));

            _chNum = UIKit.Label(_chapterBar, "", 14, Theme.InkSoft);
            _chNum.rectTransform.Anchor(UIKit.Center, new Vector2(0, 11), new Vector2(200, 18));
            _chName = UIKit.Label(_chapterBar, "", 20, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            _chName.rectTransform.Anchor(UIKit.Center, new Vector2(0, -8), new Vector2(200, 24));

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
            _setTab?.Invoke(_daily ? 1 : 0);
            _chapterBar.gameObject.SetActive(!_daily);

            Task[] tasks;
            if (_daily) tasks = GameData.Daily;
            else
            {
                _chapter = Mathf.Clamp(_chapter, 0, GameData.Chapters.Length - 1);
                var ch = GameData.Chapters[_chapter];
                _chNum.text = "CHƯƠNG " + (_chapter + 1);
                _chName.text = ch.name;
                tasks = ch.tasks;
            }

            foreach (Transform c in _list) UnityEngine.Object.Destroy(c.gameObject);

            foreach (var t in tasks)
            {
                var task = t;
                var pr = GS.Progress(t, _daily);
                bool done = pr.p >= t.need;

                var row = Row(_list, 74f);

                var title = UIKit.Label(row, t.t, 21, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                title.rectTransform.Anchor(UIKit.TopLeft, new Vector2(20, -12), new Vector2(360, 26));
                title.rectTransform.pivot = new Vector2(0, 1);

                // progress bar
                var barBox = UIKit.Node("p", row);
                barBox.Anchor(UIKit.BottomLeft, new Vector2(20, 14), new Vector2(300, 16));
                barBox.pivot = new Vector2(0, 0);
                var fill = UIKit.Bar(barBox, Theme.TrackDark, done ? Theme.Green : Theme.Blue, 8);
                fill.transform.parent.GetComponent<RectTransform>().Stretch();
                fill.fillAmount = Mathf.Clamp01(pr.p / (float)t.need);

                var pt = UIKit.Label(row, pr.p + "/" + t.need, 16, Theme.InkSoft, TextAnchor.MiddleLeft);
                pt.rectTransform.Anchor(UIKit.BottomLeft, new Vector2(330, 12), new Vector2(90, 20));
                pt.rectTransform.pivot = new Vector2(0, 0);

                var xpChip = RewardChip(row, XpIcon, Fmt.N(t.xp), Theme.Blue);
                xpChip.Anchor(UIKit.Right, new Vector2(-292, 16), new Vector2(112, 32));
                var coinChip = RewardChip(row, CoinIcon, Fmt.N(t.coin), Theme.AmberDeep);
                coinChip.Anchor(UIKit.Right, new Vector2(-292, -18), new Vector2(112, 32));

                if (pr.claimed)
                {
                    var tag = UIKit.Node("done", row);
                    tag.Anchor(UIKit.Right, new Vector2(-20, 0), new Vector2(140, 46));
                    UIKit.Round(tag, Theme.Cream3, 23, "bg").rectTransform.Stretch();
                    UIKit.Label(tag, "Đã nhận", 20, Theme.InkSoft, TextAnchor.MiddleCenter, FontStyle.Bold)
                         .rectTransform.Stretch();
                }
                else if (done)
                {
                    var b = UIKit.Btn(row, "Nhận", Theme.Green, Theme.GreenDark, 22, 22,
                                      () => app.ClaimTask(task, _daily));
                    b.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-20, 0), new Vector2(140, 50));
                }
                else
                {
                    var b = UIKit.Btn(row, "Đi đến", Theme.Blue, Theme.BlueDeep, 22, 22,
                                      () => { app.CloseAll(); app.Toast("Hãy quay lại nông trại và làm việc!"); });
                    b.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-20, 0), new Vector2(140, 50));
                }
            }

            Tween.Stagger(_list, 0.04f);
        }
    }

    // ================================================================
    // KHO
    // ================================================================
    public class WarehousePanel : PanelBase
    {
        public WarehousePanel(GameApp app) : base(app) { }
        public override string Title => "Kho nông trại";
        public override Vector2 Size => new Vector2(900, 600);
        public override Color Accent => Theme.AmberDeep;

        static bool _seedTab;
        RectTransform _grid;
        Button _sell;
        Text _empty;
        Action<int> _setTab;

        public override void Build()
        {
            var tabs = UIKit.Node("tabs", Body);
            tabs.Anchor(UIKit.Top, new Vector2(0, -4), new Vector2(320, 46));
            _setTab = UIKit.Tabs(tabs, new[] { "Nông sản", "Hạt giống" }, i => { _seedTab = i == 1; Refresh(); }, 155, 46, 10);
            tabs.GetChild(0).GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            var box = UIKit.Node("box", Body);
            box.anchorMin = new Vector2(0, 0); box.anchorMax = new Vector2(1, 1);
            box.offsetMin = new Vector2(0, 74); box.offsetMax = new Vector2(0, -58);
            Well(box);
            _grid = UIKit.ScrollGrid(box, new Vector2(104, 104), new Vector2(12, 12), new RectOffset(14, 14, 14, 14));
            ((RectTransform)_grid.parent).Stretch(4, 4, 4, 4);

            // sits directly on the dark inset well, so it needs light ink, not InkSoft
            _empty = UIKit.Label(box, "", 20, new Color(1f, 0.96f, 0.88f, 0.75f));
            _empty.rectTransform.Stretch();

            _sell = UIKit.Btn(Body, "Bán sỉ", Theme.Amber, Theme.AmberDeep, 25, 26, () => app.SellAll());
            _sell.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 2), new Vector2(360, 62));

            Refresh();
        }

        public override void Refresh()
        {
            _setTab?.Invoke(_seedTab ? 1 : 0);
            foreach (Transform c in _grid) UnityEngine.Object.Destroy(c.gameObject);

            if (_seedTab)
            {
                var owned = GS.seeds.Where(kv => kv.Value > 0).ToList();
                _empty.text = owned.Count == 0 ? "Túi hạt giống trống" : "";
                foreach (var kv in owned)
                {
                    var s = GameData.Get(kv.Key);
                    if (s == null) continue;
                    ItemCell(_grid, Art.Icon(s.art, 0), Color.white, s.r, kv.Value.ToString(), s.name);
                }
                UIKit.BtnLabel(_sell).text = "Không thể bán";
                _sell.interactable = false;
                UIKit.Restyle(_sell, Theme.Cream3, Theme.InkSoft);
            }
            else
            {
                var list = GS.StoreList();
                _empty.text = list.Count == 0 ? "Kho trống — hãy thu hoạch nông sản!" : "";
                foreach (var it in list)
                {
                    var s = GameData.Get(it.crop);
                    if (s == null) continue;
                    string cap = s.name + (it.v > 0 ? " " + Art.Elem(it.v).shortName : "");
                    var cell = ItemCell(_grid, Art.Icon(s.art, it.v), Art.VariantTint(s.art, it.v), s.r,
                                        it.n.ToString(), cap);
                    if (it.v > 0)
                    {
                        var el = Art.Elem(it.v);
                        var star = UIKit.Node("mut", cell);
                        star.Anchor(UIKit.TopLeft, new Vector2(6, -6), new Vector2(26, 26));
                        UIKit.Img(star, Theme.Circle(), el.glow, "bg").rectTransform.Stretch();
                        var mark = UIKit.Img(star, Theme.Skin.StarGold, Color.white, "ic");
                        mark.preserveAspect = true;
                        mark.rectTransform.Stretch(4, 4, 4, 4);
                    }
                }

                long total = list.Sum(x => (long)x.price * x.n);
                bool has = list.Count > 0;
                UIKit.BtnLabel(_sell).text = has ? "Bán sỉ · " + Fmt.N(total) : "Bán sỉ";
                _sell.interactable = has;
                UIKit.Restyle(_sell, has ? Theme.Amber : Theme.Cream3, has ? (Color?)null : Theme.InkSoft);
            }

            Tween.Stagger(_grid, 0.02f);
        }
    }
}
