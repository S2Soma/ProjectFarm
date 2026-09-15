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
        /// <summary>The warm halo behind the unlock art — hidden with the art, or it is left as a
        /// blurred orange smudge over "Không có nội dung mới".</summary>
        Image _unlockGlow;
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
            // Dark ink, no outline: this bar sits on a cream card, where white-on-pale-track was
            // the least readable number on the screen.
            _xpNum = UIKit.Label(barBox, "", 17, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
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
            _unlockGlow = UIKit.Img(art, Theme.Glow(), Theme.Amber.Alpha(0.35f), "glow");
            _unlockGlow.rectTransform.Stretch(-14, -14, -14, -14);
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
            // Discs, not rounded boxes: the level you have is pressed into the card (a well), the
            // one you are buying stands out of it (an amber button face). Two cream-and-orange
            // rectangles either side of a bar read as two more buttons.
            bool next = Theme.Skin.ToneOf(face) == Theme.Tone.Amber;
            var box = UIKit.Node("lv", parent);
            box.Anchor(UIKit.Center, new Vector2(x, 2), new Vector2(86, 86));
            Look look;
            if (next) { look = Looks.BtnAmber; look.lip = 4f; }
            else look = Looks.Well;
            var surf = SurfaceLook.Add(box, look, SurfaceLook.Pill);
            Color line = next ? look.inkLine : Color.clear;
            Color fg = next ? Color.white : Theme.InkSoft;
            var cap = UIKit.Label(surf.Face, "CẤP", 13, fg.Alpha(next ? 0.92f : 0.8f), TextAnchor.MiddleCenter, FontStyle.Bold);
            cap.rectTransform.Anchor(UIKit.Top, new Vector2(0, -12), new Vector2(80, 18));
            var t = next
                ? UIKit.LabelOutlined(surf.Face, "1", 34, fg, TextAnchor.MiddleCenter, line)
                : UIKit.Label(surf.Face, "1", 34, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Anchor(UIKit.Center, new Vector2(0, -9), new Vector2(80, 44));
            return t;
        }

        public override void Refresh()
        {
            var a = GameData.Level(GS.Local.lv);
            var b = GameData.Level(GS.Local.lv + 1);

            _lvA.text = GS.Local.lv.ToString();
            _lvB.text = (GS.Local.lv + 1).ToString();
            _xpFill.fillAmount = Mathf.Clamp01(GS.Local.xp / (float)a.xpNeed);
            _xpNum.text = Fmt.N(Math.Min(GS.Local.xp, a.xpNeed)) + " / " + Fmt.N(a.xpNeed);

            var fromVals = new[] { a.growCut, a.mutate, a.priceUp, a.energyUp };
            var toVals = new[] { b.growCut, b.mutate, b.priceUp, b.energyUp };
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].from.text = Fmt.Pct(fromVals[i]);
                _rows[i].to.text = Fmt.Pct(toVals[i]);
            }

            var next = GameData.Seeds.FirstOrDefault(s => s.lv == GS.Local.lv + 1);
            string quick = QuickActions.AnnouncedAt(GS.Local.lv + 1);
            if (quick != null)
            {
                // a new verb on the HUD outranks a new seed: it changes how every later
                // session is played
                _unlockArt.enabled = true; if (_unlockGlow != null) _unlockGlow.enabled = true;
                _unlockArt.sprite = Theme.Skin.StarGold;
                _unlockName.text = quick;
            }
            else if (next != null)
            {
                _unlockArt.enabled = true; if (_unlockGlow != null) _unlockGlow.enabled = true;
                _unlockArt.sprite = Art.Icon(next.art, 0);
                _unlockName.text = next.name;
            }
            // Levels no longer HAND OUT plots (LevelInfo.plots is dead data since plots are
            // bought), so "+1 ô đất trồng" was a promise the level-up would not keep. What a
            // level really unlocks is the right to buy the next plot, or an island's gate.
            else if (GS.Local.PlotLevelNow(0) == GS.Local.lv + 1
                     && IslandSys.OpenCount(GS.Local.islands[0]) < IslandSys.PlotsPerIsland)
            {
                _unlockArt.enabled = true; if (_unlockGlow != null) _unlockGlow.enabled = true;
                _unlockArt.sprite = Art.TileEmpty;
                _unlockName.text = "Mở bán ô đất · " + Fmt.N(GS.Local.PlotPriceNow(0));
            }
            else if (IslandSys.NextLocked(GS.Local) is int isle && isle > 0
                     && IslandSys.Def(isle).lv == GS.Local.lv + 1)
            {
                _unlockArt.enabled = true; if (_unlockGlow != null) _unlockGlow.enabled = true;
                _unlockArt.sprite = Theme.Skin.Farmhouse;
                _unlockName.text = "Đủ cấp mở " + IslandSys.NameOf(isle);
            }
            else
            {
                _unlockArt.enabled = false; if (_unlockGlow != null) _unlockGlow.enabled = false;
                _unlockName.text = "Không có nội dung mới";
            }

            _cost.text = Fmt.N(a.cost);
            bool xpOk = GS.Local.xp >= a.xpNeed, coinOk = GS.Local.coin >= a.cost;
            bool can = coinOk && xpOk;
            // Say what is missing. "Chưa đủ điều kiện" under a full XP bar read as a bug: the
            // player could see the XP was there and could not see that the coins were not.
            _cost.color = coinOk ? Theme.Ink : Theme.Red;
            _go.interactable = true;
            UIKit.BtnLabel(_go).text = can ? "Nâng cấp ngay"
                : !xpOk ? "Thiếu " + Fmt.N(a.xpNeed - GS.Local.xp) + " XP"
                : "Thiếu " + Fmt.N(a.cost - GS.Local.coin) + " xu";
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
            // Open on a chest the player can actually open. Defaulting to the tier currently
            // being FILLED showed "Rương thần kỳ ×0 · Chưa có rương" to a player holding two
            // Rương quý — the panel's one action greyed out with the answer one tap away.
            _sel = GS.Local.ChestTier();
            for (int t = GS.Local.chests.Length - 1; t >= 0; t--)
                if (GS.Local.chests[t] > 0) { _sel = t; break; }

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
            // dark ink on the cream card; white-on-pale-track was barely legible
            _energyText = UIKit.Label(barBox, "", 18, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
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

                // the frame and a faint wash repeat the tier colour, so the row reads as four
                // tiers even before the chests themselves are looked at
                var face = UIKit.Round(cell, Color.Lerp(Theme.Cream, tints[i], 0.18f), 22, "face");
                face.rectTransform.Stretch();
                face.raycastTarget = true;
                var frame = UIKit.Img(cell, null, tints[i], "frame");
                frame.rectTransform.Stretch(-3, -3, -3, -3);
                Chrome.Shape(frame, 25f);
                frame.transform.SetAsFirstSibling();

                UIKit.Img(cell, Theme.Glow(), tints[i].Alpha(0.62f), "glow")
                     .rectTransform.Anchor(UIKit.Center, new Vector2(0, 10), new Vector2(168, 168));

                // one drawing per tier (Tools/gen_items.py): iron-banded wood, gold-banded wood,
                // purple with a crystal lock, gold with a ruby lock
                var art = UIKit.Img(cell, Art.Item("chest_" + i), Color.white, "art");
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
            // Drawn under the chest row, whose selected ring and 1.04 scale overhang its bottom.
            info.SetSiblingIndex(row.GetSiblingIndex());

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
            int tier = 3;                                    // every kind can drop now; none is locked
            _energyFill.fillAmount = Mathf.Clamp01(GS.Local.energy / (float)GS.Local.EnergyGoal);
            var odds = PlayerState.ChestOdds(GS.Local.lv);
            _energyText.text = Fmt.N(GS.Local.energy) + " / " + Fmt.N(GS.Local.EnergyGoal) + "  ·  huyền thoại " + Fmt.Pct(odds[3]);

            for (int i = 0; i < _cells.Count; i++)
            {
                bool sel = i == _sel;
                bool locked = i > tier;
                var face = _cells[i].Find("face").GetComponent<Image>();
                var frame = _cells[i].Find("frame").GetComponent<Image>();
                face.color = sel ? Color.white : Theme.Cream.Alpha(0.85f);
                // 3 px ring, 5 px when selected, always concentric with the 22 px face. The old
                // -6 px ring poked past the cell into its neighbour's gap.
                float ring = sel ? 5f : 3f;
                frame.rectTransform.Stretch(-ring, -ring, -ring, -ring);
                Chrome.Shape(frame, 22f + ring);
                var art = _cells[i].Find("art").GetComponent<Image>();
                art.color = locked ? new Color(0.55f, 0.55f, 0.58f, 0.85f) : Color.white;
                _cells[i].localScale = Vector3.one * (sel ? 1.04f : 0.98f);
                _counts[i].text = "x" + GS.Local.chests[i];
            }

            var c = GameData.Chests[_sel];
            _name.text = c.name;
            _desc.text = c.desc;
            int n = GS.Local.chests[_sel];
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

        /// <summary>0 = Đơn hàng (contracts), 1 = Chương truyện, 2 = Hằng ngày.</summary>
        static int _tab;
        static int _chapter = -1;

        /// <summary>For the screenshot pass: which tab the next MissionsPanel opens on.</summary>
        public static void OpenOnTab(int tab) { _tab = Mathf.Clamp(tab, 0, 2); }
        RectTransform _list, _chapterBar, _box;
        Text _chNum, _chName, _chDone, _streak;
        Action<int> _setTab;
        readonly List<RectTransform> _tabDots = new List<RectTransform>();

        static int ActiveChapter()
        {
            for (int i = 0; i < GameData.Chapters.Length; i++)
                if (GameData.Chapters[i].tasks.Any(t => !GS.Local.Progress(t, false).claimed)) return i;
            // fall through
            return GameData.Chapters.Length - 1;
        }

        public override void Build()
        {
            if (_chapter < 0) _chapter = ActiveChapter();

            var tabs = UIKit.Node("tabs", Body);
            // 176, not 140: "Chương truyện" at 22 px bold is ~160 px and labels overflow silently,
            // so it ran across both neighbouring tabs.
            tabs.Anchor(UIKit.TopLeft, new Vector2(0, -4), new Vector2(544, 46));
            _setTab = UIKit.Tabs(tabs, new[] { "Đơn hàng", "Chương truyện", "Hằng ngày" },
                                 i => { _tab = i; Refresh(); }, 176, 46, 8);
            tabs.GetChild(0).GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            // a red count on each tab that has a reward waiting, so a finished mission is findable
            // without opening every tab (owner, 15/9)
            foreach (Transform cell in tabs.GetChild(0))
                if (cell.name == "tab") _tabDots.Add(Hud.Dot((RectTransform)cell));
            // above the tab's top-right corner: at the Menu's 26 px / +6 the dot sat on "Chương truyện"
            foreach (var d in _tabDots) { d.sizeDelta = new Vector2(22, 22); d.anchoredPosition = new Vector2(8, 17); }

            // Streak lives here and NOT on the HUD. Contracts bring a third countdown into a
            // game that already has weather (1 h) and tags (12 h), and three live clocks is one
            // too many — the player starts ignoring all of them, and the first casualty is the
            // weather readout, which is the strongest thing in the whole redesign.
            _streak = UIKit.Label(Body, "", 17, Theme.AmberDeep, TextAnchor.MiddleRight, FontStyle.Bold);
            _streak.rectTransform.Anchor(UIKit.TopRight, new Vector2(-16, -18), new Vector2(240, 26));

            // Chapter stepper: its own full-width row under the tabs. It used to squeeze into the
            // 300 px left of the tabs, overlapping the "Hằng ngày" segment by 14 px, with a name
            // ("Chuyên gia cây trồng") wider than the gap between its arrows.
            _chapterBar = UIKit.Node("chapter", Body);
            _chapterBar.anchorMin = new Vector2(0, 1); _chapterBar.anchorMax = new Vector2(1, 1);
            _chapterBar.pivot = new Vector2(0.5f, 1f);
            _chapterBar.offsetMin = new Vector2(0, -114); _chapterBar.offsetMax = new Vector2(0, -60);
            SurfaceLook.Add(_chapterBar, Looks.Row, 18f);

            var prev = UIKit.IconBtn(_chapterBar, Theme.Skin.ArrowLeft, Theme.Blue, 0.5f,
                                     () => { _chapter = Mathf.Max(0, _chapter - 1); Refresh(); });
            prev.GetComponent<RectTransform>().Anchor(UIKit.Left, new Vector2(8, 0), new Vector2(42, 42));

            var next = UIKit.IconBtn(_chapterBar, Theme.Skin.ArrowRight, Theme.Blue, 0.5f,
                                     () => { _chapter = Mathf.Min(GameData.Chapters.Length - 1, _chapter + 1); Refresh(); });
            next.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-8, 0), new Vector2(42, 42));

            _chNum = UIKit.Label(_chapterBar, "", 17, Theme.BlueDeep, TextAnchor.MiddleLeft, FontStyle.Bold);
            _chNum.rectTransform.Anchor(UIKit.Left, new Vector2(64, 0), new Vector2(120, 30));
            _chName = UIKit.Label(_chapterBar, "", 22, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            _chName.rectTransform.Anchor(UIKit.Left, new Vector2(184, 0), new Vector2(360, 32));
            _chDone = UIKit.Label(_chapterBar, "", 17, Theme.InkSoft, TextAnchor.MiddleRight);
            _chDone.rectTransform.Anchor(UIKit.Right, new Vector2(-64, 0), new Vector2(220, 30));

            var box = UIKit.Node("box", Body);
            box.anchorMin = new Vector2(0, 0); box.anchorMax = new Vector2(1, 1);
            box.offsetMin = new Vector2(0, 0); box.offsetMax = new Vector2(0, -58);
            _box = box;
            Well(box);
            _list = UIKit.ScrollList(box, 10f, new RectOffset(12, 12, 12, 12));
            ((RectTransform)_list.parent).Stretch(4, 4, 4, 4);

            Refresh();
        }

        public override void Refresh()
        {
            _setTab?.Invoke(_tab);
            if (_tabDots.Count == 3)
            {
                Hud.SetDot(_tabDots[0], GS.Local.ClaimableContracts());
                Hud.SetDot(_tabDots[1], GS.Local.ClaimableStory());
                Hud.SetDot(_tabDots[2], GS.Local.ClaimableDaily());
            }
            _chapterBar.gameObject.SetActive(_tab == 1);
            _box.offsetMax = new Vector2(0, _tab == 1 ? -122 : -58);

            int st = GS.Local.streak;
            _streak.text = st > 0 ? "Chuỗi " + st : "";

            if (_tab == 0) { RefreshContracts(); return; }
            bool _daily = _tab == 2;

            Task[] tasks;
            if (_daily) tasks = GameData.Daily;
            else
            {
                _chapter = Mathf.Clamp(_chapter, 0, GameData.Chapters.Length - 1);
                var ch = GameData.Chapters[_chapter];
                _chNum.text = "Chương " + (_chapter + 1);
                _chName.text = ch.name;
                int claimed = ch.tasks.Count(t => GS.Local.Progress(t, false).claimed);
                _chDone.text = claimed >= ch.tasks.Length ? "Đã hoàn thành" : "Xong " + claimed + "/" + ch.tasks.Length + " nhiệm vụ";
                tasks = ch.tasks;
            }

            ClearList(_list);

            // Claimable first, then in progress, then already claimed: finishing one moves what is
            // left to do up to the top instead of leaving it wherever the table put it.
            var order = new List<int>();
            for (int k = 0; k < tasks.Length; k++) order.Add(k);
            int Rank(int k)
            {
                var r = GS.Local.Progress(tasks[k], _daily);
                return r.claimed ? 2 : r.p >= tasks[k].need ? 0 : 1;
            }
            order.Sort((x, y) => Rank(x) != Rank(y) ? Rank(x).CompareTo(Rank(y)) : x.CompareTo(y));

            foreach (int ti in order)
            {
                var t = tasks[ti];
                var task = t;
                var pr = GS.Local.Progress(t, _daily);
                bool done = pr.p >= t.need;

                var row = Row(_list, 74f);

                // Chapter missions are pre-graded: 1 bronze, 2 silver, 3 gold, 4 diamond, so
                // every level ENDS on a diamond. The grade badge is the same one the contract
                // board uses, because a player should not have to learn two vocabularies.
                float xOff = 20f;
                if (!_daily)
                {
                    // no caption: a 74 px row has no room under the medal, and bronze / silver /
                    // gold / diamond medals need none
                    GradeMedal(row, MissionSys.ChapterGrade(ti), new Vector2(50, 0), 54, false);
                    xOff = 96f;
                }

                var title = UIKit.Label(row, t.t, 21, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                title.rectTransform.Anchor(UIKit.TopLeft, new Vector2(xOff, -12), new Vector2(360, 26));
                title.rectTransform.pivot = new Vector2(0, 1);

                // progress bar
                var barBox = UIKit.Node("p", row);
                // The reward chips start 404 px from the row's right edge (~420 from its left), so
                // bar + count must end before that: a 300 px bar pushed "10/10" under the coin chip.
                barBox.Anchor(UIKit.BottomLeft, new Vector2(xOff, 14), new Vector2(250, 16));
                barBox.pivot = new Vector2(0, 0);
                var fill = UIKit.Bar(barBox, Theme.TrackDark, done ? Theme.Green : Theme.Blue, 8);
                fill.transform.parent.GetComponent<RectTransform>().Stretch();
                fill.fillAmount = Mathf.Clamp01(pr.p / (float)t.need);

                var pt = UIKit.Label(row, pr.p + "/" + t.need, 16, Theme.InkSoft, TextAnchor.MiddleLeft);
                pt.rectTransform.Anchor(UIKit.BottomLeft, new Vector2(xOff + 260, 12), new Vector2(60, 20));
                pt.rectTransform.pivot = new Vector2(0, 0);

                // Rewards are quoted in UNIT — the margin of the best crop the player can grow —
                // so the tables keep their meaning as the economy grows instead of needing a
                // rebalance every few levels.
                int rXp = MissionSys.TaskXp(GS.Local, t, _daily);
                int rCoin = MissionSys.TaskCoin(GS.Local, t, _daily);

                var xpChip = RewardChip(row, XpIcon, Fmt.N(rXp), Theme.Blue);
                xpChip.Anchor(UIKit.Right, new Vector2(-292, 16), new Vector2(112, 32));
                var coinChip = RewardChip(row, CoinIcon, Fmt.N(rCoin), Theme.AmberDeep);
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

            ScrollTop(_list);
            Tween.Stagger(_list, 0.04f);
        }

        /// <summary>The contract board: one row per slot, with its grade worn on the outside.
        ///
        /// The grade badge is the point of this screen. A player has to be able to look at the
        /// board and decide whether the gold one is worth reorganising the farm for, which is
        /// only a decision if the grade is visible before they commit.</summary>
        void RefreshContracts()
        {
            GS.Local.SyncContracts();
            ClearList(_list);

            long now = GS.Now;
            var list = GS.Local.contracts;

            // Ready to claim, then in progress, then the slots waiting to refill. The board used to
            // keep every slot in place so it never moved under a habit; what players actually saw
            // was a claimed order leaving an empty "Đơn mới sau 8 phút" wedged between live ones.
            var order = new List<int>();
            for (int k = 0; k < list.Count; k++) order.Add(k);
            int Rank(int k) { var mm = list[k]; return mm.Empty ? 2 : mm.Done ? 0 : 1; }
            order.Sort((x, y) => Rank(x) != Rank(y) ? Rank(x).CompareTo(Rank(y)) : x.CompareTo(y));

            foreach (int i in order)
            {
                int slot = i;
                var m = list[i];
                // 96, not 82. Three stacked lines (title, expiry, progress) plus Vietnamese
                // diacritics need ~1.45x the font size per line box, and at 82 the bar was
                // landing on top of the expiry text. UIKit.Label overflows silently rather than
                // wrapping, so a rect that is too short does not clip — it collides.
                var row = Row(_list, 96f);

                if (m.Empty)
                {
                    // A recessed well, not a missing row: a board whose rows move as they fill is
                    // a board you cannot build any habit around.
                    var wait = UIKit.Label(row, now < m.refillAt
                            ? "Đơn mới sau " + Fmt.Time((int)((m.refillAt - now) / 1000L))
                            : "Đang chuẩn bị đơn mới…",
                        18, Theme.InkSoft, TextAnchor.MiddleCenter);
                    wait.rectTransform.Stretch();
                    continue;
                }

                var gd = MissionSys.Def(m.grade);

                // Three columns, and the boundaries are stated once here rather than rediscovered
                // per widget. Everything overflows silently in this UI, so a rect that is too
                // narrow does not clip — it lands on its neighbour, which is how the badge ended
                // up written across the expiry text on the first pass.
                //
                //   badge   20 .. 126
                //   text   148 .. 420   (title / bar+count / expiry, stacked)
                //   chips  436 .. 566
                //   button 656 .. 796
                //
                // UIKit.Anchor sets pivot = anchor, so a UIKit.Left anchor positions the LEFT
                // EDGE, not the centre — reading it as a centre is what put the badge on top of
                // the progress bar, and UIKit.Right likewise positions the right edge.
                const float TextX = 148f, TextW = 272f;

                GradeMedal(row, m.grade, new Vector2(73, 12), 58, true);

                var title = UIKit.Label(row, MissionSys.Describe(m), 20, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                title.rectTransform.Anchor(UIKit.TopLeft, new Vector2(TextX, -8), new Vector2(TextW, 28));
                title.rectTransform.pivot = new Vector2(0, 1);

                var barBox = UIKit.Node("p", row);
                barBox.Anchor(UIKit.TopLeft, new Vector2(TextX, -44), new Vector2(180, 14));
                barBox.pivot = new Vector2(0, 1);
                var fill = UIKit.Bar(barBox, Theme.TrackDark, m.Done ? Theme.Green : Theme.Hex(gd.hex), 7);
                fill.transform.parent.GetComponent<RectTransform>().Stretch();
                fill.fillAmount = Mathf.Clamp01(m.p / (float)Mathf.Max(1, m.need));

                var pt = UIKit.Label(row, m.p + "/" + m.need, 15, Theme.InkSoft, TextAnchor.MiddleLeft);
                pt.rectTransform.Anchor(UIKit.TopLeft, new Vector2(TextX + 190, -40), new Vector2(80, 22));
                pt.rectTransform.pivot = new Vector2(0, 1);

                // Expiry gets its own line, small, and only turns urgent near the end. It is
                // information on a row, not a third live clock competing with weather and tags.
                long left = m.expiresAt - now;
                bool urgent = left < 10L * 60_000L;
                var exp = UIKit.Label(row, "Còn " + Fmt.Time((int)(left / 1000L)), 14,
                                      urgent ? Theme.Red : Theme.InkSoft, TextAnchor.MiddleLeft,
                                      urgent ? FontStyle.Bold : FontStyle.Normal);
                exp.rectTransform.Anchor(UIKit.TopLeft, new Vector2(TextX, -66), new Vector2(200, 20));
                exp.rectTransform.pivot = new Vector2(0, 1);

                var xpChip = RewardChip(row, XpIcon, Fmt.N(MissionSys.XpReward(GS.Local, m)), Theme.Blue);
                xpChip.Anchor(UIKit.Right, new Vector2(-250, 20), new Vector2(130, 30));
                var coinChip = RewardChip(row, CoinIcon, Fmt.N(MissionSys.CoinReward(GS.Local, m)), Theme.AmberDeep);
                coinChip.Anchor(UIKit.Right, new Vector2(-250, -20), new Vector2(130, 30));

                if (m.Done)
                {
                    var b = UIKit.Btn(row, "Nhận", Theme.Green, Theme.GreenDark, 22, 22,
                                      () => { if (GS.Local.ClaimContract(slot)) { app.AfterClaim(); Refresh(); } });
                    b.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-20, 0), new Vector2(140, 50));
                }
                else
                {
                    var b = UIKit.Btn(row, "Đi đến", Theme.Blue, Theme.BlueDeep, 22, 22,
                                      () => { app.CloseAll(); app.Toast(MissionSys.Describe(m)); });
                    b.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-20, 0), new Vector2(140, 50));
                }
            }

            ScrollTop(_list);
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
        bool? _shownSeedTab;          // a tab switch starts the grid at its top; selling in place keeps the scroll
        RectTransform _grid;
        Button _sell;
        public Button SellButton => _sell;
        Text _empty;
        Image _emptyIcon;
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

            // An empty state with a picture: a line of pale text alone in a large well read as a
            // loading failure. The well is light now, so the ink is dark.
            _emptyIcon = UIKit.Img(box, Theme.Skin.NavStore, Theme.Hex("#8A7152").Alpha(0.35f), "emptyIcon");
            _emptyIcon.preserveAspect = true;
            _emptyIcon.rectTransform.Anchor(UIKit.Center, new Vector2(0, 34), new Vector2(96, 96));
            _empty = UIKit.Label(box, "", 20, Theme.InkSoft, TextAnchor.MiddleCenter, FontStyle.Bold);
            _empty.rectTransform.Anchor(UIKit.Center, new Vector2(0, -42), new Vector2(600, 32));

            _sell = UIKit.Btn(Body, "Bán sỉ", Theme.Amber, Theme.AmberDeep, 25, 26, () => app.SellAll());
            _sell.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 2), new Vector2(360, 62));

            Refresh();
        }

        public override void Refresh()
        {
            _setTab?.Invoke(_seedTab ? 1 : 0);
            bool newTab = _shownSeedTab != _seedTab;
            _shownSeedTab = _seedTab;
            ClearList(_grid);

            if (_seedTab)
            {
                var owned = GS.Local.seeds.Where(kv => kv.Value > 0).ToList();
                _empty.text = owned.Count == 0 ? "Túi hạt giống trống" : "";
                _emptyIcon.sprite = Theme.Skin.NavSeeds;
                _emptyIcon.enabled = owned.Count == 0;
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
                var list = GS.Local.StoreList();
                _empty.text = list.Count == 0 ? "Kho trống. Hãy thu hoạch nông sản!" : "";
                _emptyIcon.sprite = Theme.Skin.NavStore;
                _emptyIcon.enabled = list.Count == 0;
                foreach (var it in list)
                {
                    var s = GameData.Get(it.crop);
                    if (s == null) continue;
                    string cap = s.name + (it.v > 0 ? " " + Art.Elem(it.v).shortName : "");
                    var cell = ItemCell(_grid, Art.Icon(s.art, it.v), Color.white, s.r,
                                        it.n.ToString(), cap);
                    if (it.v > 0)
                    {
                        MutationTint.Apply(cell.Find("art/im")?.GetComponent<Image>(), s.art, it.v);
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

            if (newTab) ScrollTop(_grid);
            Tween.Stagger(_grid, 0.02f);
        }
    }
}
