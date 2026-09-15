using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>The tribute board for one island, opened by tapping the locked island itself.
    ///
    /// The panel exists for the ACT of paying — the numbers already live on the map, where they
    /// belong. So it is deliberately thin: what is owed, what is on hand, one button per crop,
    /// and the claim. Everything explanatory ("why this crop", "what the perk does") is one line,
    /// because a player who panned to a locked island and tapped it has already decided.</summary>
    public class IslandPanel : PanelBase
    {
        readonly int _index;

        public IslandPanel(GameApp app, int index) : base(app) { _index = index; }

        public override string Title => IslandSys.Def(_index).name;
        // 580: at 500 the claim note ("Cần cấp 16…") was printed across the third tribute bar and
        // the claim button sat on the third row's lower edge.
        public override Vector2 Size => new Vector2(760, 580);

        readonly List<RectTransform> _rows = new List<RectTransform>();
        Button _claim;
        Text _claimNote;

        public override void Build()
        {
            var body = Body;
            var s = GS.Local;
            var def = IslandSys.Def(_index);

            var hero = UIKit.Round(body, Theme.Cream, 18, "hero");
            hero.rectTransform.Anchor(UIKit.TopLeft, new Vector2(8, -8), new Vector2(728, 92));
            hero.rectTransform.pivot = new Vector2(0, 1);

            var blurb = UIKit.Label(hero.rectTransform, def.blurb, 18, Theme.InkSoft, TextAnchor.MiddleLeft);
            blurb.rectTransform.Anchor(UIKit.TopLeft, new Vector2(20, -12), new Vector2(690, 26));
            blurb.rectTransform.pivot = new Vector2(0, 1);

            // The perk is the reason to want the island at all, so it is the largest thing here.
            var perk = UIKit.Label(hero.rectTransform, def.perk ?? "", 22, Theme.GreenDeep,
                                   TextAnchor.MiddleLeft, FontStyle.Bold);
            perk.rectTransform.Anchor(UIKit.TopLeft, new Vector2(20, -42), new Vector2(690, 30));
            perk.rectTransform.pivot = new Vector2(0, 1);

            var gate = UIKit.Label(hero.rectTransform,
                                   "Cấp " + def.lv + "  ·  " + Fmt.N(def.coin) + " xu  ·  16 ô đất, "
                                   + def.freePlots + " ô tặng kèm",
                                   16, Theme.InkSoft, TextAnchor.MiddleLeft);
            gate.rectTransform.Anchor(UIKit.TopLeft, new Vector2(20, -68), new Vector2(690, 22));
            gate.rectTransform.pivot = new Vector2(0, 1);

            var hdr = UIKit.Label(body, "Cống nạp: nông sản nộp vào sẽ bị tiêu mất", 17, Theme.Ink,
                                  TextAnchor.MiddleLeft, FontStyle.Bold);
            hdr.rectTransform.Anchor(UIKit.TopLeft, new Vector2(12, -108), new Vector2(500, 24));
            hdr.rectTransform.pivot = new Vector2(0, 1);

            for (int i = 0; i < def.tribute.Length; i++) BuildRow(body, def.tribute[i], i);

            _claim = UIKit.Btn(body, "Mở đảo", Theme.Green, Theme.GreenDark, 24, 24, Claim);
            _claim.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 14), new Vector2(300, 60));

            _claimNote = UIKit.Label(body, "", 15, Theme.InkSoft, TextAnchor.MiddleCenter);
            _claimNote.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 84), new Vector2(640, 24));

            Refresh();
        }

        void BuildRow(RectTransform body, Tribute t, int i)
        {
            var row = UIKit.Node("row" + i, body);
            row.Anchor(UIKit.TopLeft, new Vector2(12, -136 - i * 66), new Vector2(724, 58));
            row.pivot = new Vector2(0, 1);
            UIKit.Round(row, Theme.Cream2, 14, "bg").rectTransform.Stretch();

            var seed = GameData.Get(t.crop);
            var ic = UIKit.Img(row, Art.Icon(seed != null ? seed.art : null, 0), Color.white, "ic");
            ic.preserveAspect = true;
            ic.rectTransform.Anchor(UIKit.Left, new Vector2(16, 0), new Vector2(42, 42));

            //   icon    16 ..  58
            //   name     68 .. 300      count 310 .. 470   (one line)
            //   bar      68 .. 470      (the line below)
            //   stock   486 .. 576
            //   button  590 .. 710
            var nm = UIKit.Label(row, seed != null ? seed.name : t.crop, 19, Theme.Ink,
                                 TextAnchor.MiddleLeft, FontStyle.Bold);
            nm.rectTransform.Anchor(UIKit.Left, new Vector2(68, 12), new Vector2(232, 24));

            // The count sits BESIDE the bar, not on it: lettering centred on a fill is unreadable
            // on exactly the part of the bar that shows progress.
            var count = UIKit.Label(row, "", 16, Theme.InkSoft, TextAnchor.MiddleRight, FontStyle.Bold);
            count.rectTransform.Anchor(UIKit.Left, new Vector2(310, 12), new Vector2(160, 24));

            var track = UIKit.Node("bar", row);
            track.Anchor(UIKit.Left, new Vector2(68, -14), new Vector2(402, 14));
            var fill = UIKit.Bar(track, Theme.TrackDark, Theme.Green, 7);
            fill.transform.parent.GetComponent<RectTransform>().Stretch();

            var stock = UIKit.Label(row, "", 16, Theme.InkSoft, TextAnchor.MiddleRight);
            stock.rectTransform.Anchor(UIKit.Left, new Vector2(486, 0), new Vector2(90, 22));

            string crop = t.crop;
            var pay = UIKit.Btn(row, "Nộp", Theme.Amber, Theme.AmberDeep, 19, 18, () => Pay(crop));
            pay.GetComponent<RectTransform>().Anchor(UIKit.Right, new Vector2(-14, 0), new Vector2(120, 44));

            _rows.Add(row);
            row.gameObject.AddComponent<RowRefs>().Set(fill, count, stock, pay, t);
        }

        /// <summary>Row widgets, hung on the row itself so Refresh does not need a parallel list
        /// that can fall out of step with the hierarchy.</summary>
        class RowRefs : MonoBehaviour
        {
            public Image fill; public Text count, stock; public Button pay; public Tribute t;
            public void Set(Image f, Text c, Text s, Button b, Tribute tr)
            { fill = f; count = c; stock = s; pay = b; t = tr; }
        }

        void Pay(string crop)
        {
            int n = IslandSys.Pay(GS.Local, _index, crop, IslandSys.Payable(GS.Local, _index, crop));
            if (n <= 0) { app.Toast("Kho chưa có nông sản này"); return; }
            var seed = GameData.Get(crop);
            app.Toast("Đã nộp " + Fmt.N(n) + " " + (seed != null ? seed.name : crop));
            app.AfterTribute();
            Refresh();
        }

        void Claim()
        {
            if (!IslandSys.Claim(GS.Local, _index))
            {
                var s = GS.Local;
                if (s.lv < IslandSys.Def(_index).lv) app.Toast("Chưa đủ cấp");
                else if (!IslandSys.TributeDone(s, _index)) app.Toast("Chưa nộp đủ cống nạp");
                else app.Toast("Không đủ xu nông trại");
                return;
            }
            app.OnIslandUnlocked(_index);
            app.CloseAll();
        }

        public override void Refresh()
        {
            var s = GS.Local;
            var def = IslandSys.Def(_index);

            foreach (var row in _rows)
            {
                var r = row.GetComponent<RowRefs>();
                int paid = IslandSys.Paid(s, _index, r.t.crop);
                int have = s.StockOf(r.t.crop);
                bool done = paid >= r.t.need;

                r.fill.fillAmount = Mathf.Clamp01(paid / (float)r.t.need);
                r.count.text = Fmt.N(paid) + " / " + Fmt.N(r.t.need);
                r.stock.text = done ? "Đủ" : "Kho: " + Fmt.N(have);

                bool can = !done && have > 0;
                r.pay.interactable = can;
                UIKit.Restyle(r.pay, can ? Theme.Amber : Theme.Cream3, can ? (Color?)null : Theme.InkSoft);
                UIKit.BtnLabel(r.pay).text = done ? "Xong" : "Nộp";
            }

            bool lvOk = s.lv >= def.lv;
            bool tributeOk = IslandSys.TributeDone(s, _index);
            bool coinOk = s.coin >= def.coin;
            bool inOrder = _index <= 1 || (_index - 1 < s.islands.Count && s.islands[_index - 1].unlocked);
            bool ready = lvOk && tributeOk && coinOk && inOrder;

            _claim.interactable = ready;
            UIKit.Restyle(_claim, ready ? Theme.Green : Theme.Cream3, ready ? (Color?)null : Theme.InkSoft);

            // One reason at a time, in the order the player can act on them.
            if (!inOrder) _claimNote.text = "Cần mở " + IslandSys.NameOf(_index - 1) + " trước";
            else if (!lvOk) _claimNote.text = "Cần cấp " + def.lv + ", bạn đang cấp " + s.lv;
            else if (!tributeOk) _claimNote.text = "Còn thiếu cống nạp";
            else if (!coinOk) _claimNote.text = "Cần " + Fmt.N(def.coin - s.coin) + " xu nữa";
            else _claimNote.text = def.perk + ", áp dụng cho mọi đảo";
        }
    }
}
