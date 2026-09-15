using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>Thú cưng: the pet book and the hatchery.
    ///
    /// Thú cưng — the pet chosen on the left, with what it does in numbers (plots a patrol, when
    /// the next patrol is, its favourite snack); every pet on the right, the unhatched ones as
    /// dark silhouettes so the book shows what is left to find.
    ///
    /// Ấp trứng — one egg or ten, the odds in full, and how far the guarantee is. Hatching plays
    /// the reveal in the panel itself: the egg shakes, cracks in the rarity's colour, and the pet
    /// hops out.</summary>
    public class PetPanel : PanelBase
    {
        public PetPanel(GameApp app) : base(app) { }
        public override string Title => "Thú cưng";
        public override Vector2 Size => new Vector2(1040, 600);
        public override Color Accent => Theme.Purple;

        static int _tab;
        string _sel;
        RectTransform _page, _reveal;
        System.Action<int> _setTab;
        Text _timer;

        public static void OpenOnTab(int tab) { _tab = Mathf.Clamp(tab, 0, 1); }

        public override void Build()
        {
            var tabs = UIKit.Node("tabs", Body);
            tabs.Anchor(UIKit.TopLeft, new Vector2(0, -4), new Vector2(380, 46));
            _setTab = UIKit.Tabs(tabs, new[] { "Thú cưng", "Ấp trứng" }, i => { _tab = i; Refresh(); }, 178, 46, 10);

            var wallet = UIKit.Node("wallet", Body);
            wallet.Anchor(UIKit.TopRight, new Vector2(0, -4), new Vector2(210, 46));
            UIKit.Round(wallet, Theme.Cream2, 23, "bg").rectTransform.Stretch();
            var ic = UIKit.Img(wallet, CoinIcon, Color.white, "ic");
            ic.preserveAspect = true;
            ic.rectTransform.Anchor(UIKit.Left, new Vector2(28, 0), new Vector2(32, 32));
            _wallet = UIKit.Label(wallet, "", 22, Theme.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
            _wallet.rectTransform.Stretch(52, 0, 18, 0);

            _page = UIKit.Node("page", Body);
            _page.Stretch(0, 58, 0, 0);
            Refresh();
        }

        Text _wallet;

        public override void Refresh()
        {
            _setTab?.Invoke(_tab);
            _wallet.text = Fmt.Short(GS.Local.coin);
            if (_reveal != null) return;                 // the reveal owns the page until dismissed
            ClearList(_page);
            _timer = null;
            if (_tab == 0) BuildBook(); else BuildHatchery();
        }

        /// <summary>Called by the game every second while the panel is open.</summary>
        public void TickTimer()
        {
            if (_timer == null || app.Pets == null) return;
            int sec = app.Pets.SecondsToPatrol;
            _timer.text = app.Pets.Busy ? "Đang đi tuần" : "Đi tuần sau " + (sec / 60) + ":" + (sec % 60).ToString("00");
        }

        // ============================================================
        // the book
        // ============================================================
        void BuildBook()
        {
            var s = GS.Local;
            if (string.IsNullOrEmpty(_sel) || !s.pets.ContainsKey(_sel)) _sel = s.petActive;

            if (s.pets.Count == 0)
            {
                var none = UIKit.Node("none", _page);
                none.Stretch();
                Well(none);
                var egg = UIKit.Img(none, Art.Item("item_egg"), Color.white, "egg");
                egg.preserveAspect = true;
                egg.rectTransform.Anchor(UIKit.Center, new Vector2(0, 70), new Vector2(170, 170));
                var t = UIKit.Label(none, "Bạn chưa có thú cưng nào", 26, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.rectTransform.Anchor(UIKit.Center, new Vector2(0, -44), new Vector2(700, 40));
                var sub = UIKit.Label(none, "Ấp một quả trứng để có bạn đồng hành tự tưới nước và thu hoạch giúp bạn. Quả đầu tiên miễn phí!", 18, Theme.InkSoft, TextAnchor.UpperCenter);
                sub.horizontalOverflow = HorizontalWrapMode.Wrap;
                sub.rectTransform.Anchor(UIKit.Center, new Vector2(0, -96), new Vector2(620, 60));
                var go = UIKit.Btn(none, "Ấp trứng", Theme.Blue, Theme.BlueDeep, 22, 22, () => { _tab = 1; Refresh(); });
                go.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 26), new Vector2(240, 58));
                return;
            }

            // ---- left: the selected pet ----
            var left = UIKit.Node("left", _page);
            left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(0, 1);
            left.pivot = new Vector2(0, 0.5f);
            left.offsetMin = Vector2.zero; left.offsetMax = new Vector2(430, 0);
            Well(left);

            var d = PetSys.Def(_sel);
            int lv = PetSys.LevelOf(s, d.id);
            var rc = Theme.Hex(PetSys.RarityHex[d.rarity]);

            var glow = UIKit.Img(left, Theme.Glow(), rc.Alpha(0.35f), "glow");
            glow.rectTransform.Anchor(UIKit.TopLeft, new Vector2(-10, 10), new Vector2(250, 250));
            var portrait = UIKit.Img(left, Art.Load("Art/pets/" + d.id + "/portrait"), Color.white, "portrait");
            portrait.preserveAspect = true;
            portrait.rectTransform.Anchor(UIKit.TopLeft, new Vector2(20, -18), new Vector2(190, 190));

            // fixed columns: the text column starts at 222 and is 196 wide
            const float tx = 222f, tw = 196f;
            var name = UIKit.Label(left, d.name, 30, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            name.rectTransform.Anchor(UIKit.TopLeft, new Vector2(tx, -22), new Vector2(tw, 44));
            var pill = UIKit.Node("rarity", left);
            pill.Anchor(UIKit.TopLeft, new Vector2(tx, -70), new Vector2(128, 30));
            UIKit.Round(pill, rc, 15, "bg").rectTransform.Stretch();
            UIKit.LabelOutlined(pill, PetSys.RarityName[d.rarity], 16, Color.white, TextAnchor.MiddleCenter).rectTransform.Stretch();

            Stars(left, new Vector2(tx, -112), lv);
            var jobs = UIKit.Label(left, "Mỗi lượt làm " + PetSys.JobsPerPatrol(d, lv) + " ô", 18, Theme.InkSoft, TextAnchor.MiddleLeft);
            jobs.rectTransform.Anchor(UIKit.TopLeft, new Vector2(tx, -150), new Vector2(tw, 28));
            var fav = GameData.Get(d.favourite);
            var favLine = UIKit.Label(left, "Mê nhất: " + (fav != null ? fav.name : d.favourite), 18, Theme.InkSoft, TextAnchor.MiddleLeft);
            favLine.rectTransform.Anchor(UIKit.TopLeft, new Vector2(tx, -178), new Vector2(tw, 28));

            var blurb = UIKit.Label(left, d.blurb, 18, Theme.Ink, TextAnchor.UpperLeft);
            blurb.horizontalOverflow = HorizontalWrapMode.Wrap;
            blurb.rectTransform.Anchor(UIKit.TopLeft, new Vector2(22, -222), new Vector2(386, 84));

            bool active = s.petActive == d.id;
            if (active)
            {
                _timer = UIKit.Label(left, "", 19, Theme.GreenDeep, TextAnchor.MiddleCenter, FontStyle.Bold);
                _timer.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 86), new Vector2(386, 30));
                TickTimer();
                var tag = UIKit.Btn(left, "Đang đi theo bạn", Theme.Cream3, Theme.Hex("#B9A98C"), 20, 20, null);
                tag.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 20), new Vector2(300, 56));
                tag.interactable = false;
            }
            else
            {
                var follow = UIKit.Btn(left, "Cho đi theo", Theme.Green, Theme.GreenDark, 22, 22, () =>
                {
                    GS.Local.petActive = d.id;
                    GS.Save();
                    Sfx.Play(SfxId.Claim);
                    Refresh();
                });
                follow.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 20), new Vector2(300, 56));
            }

            // ---- right: every pet ----
            var right = UIKit.Node("right", _page);
            right.anchorMin = new Vector2(0, 0); right.anchorMax = new Vector2(1, 1);
            right.offsetMin = new Vector2(446, 0); right.offsetMax = Vector2.zero;
            Well(right);
            var grid = UIKit.Node("grid", right);
            grid.Stretch(14, 14, 14, 14);
            // three by two inside a well about 580 x 440: 164 + 10 + 164 + 10 + 164 = 512 across,
            // 196 + 10 + 196 = 402 down, with the 14 px inset on each side
            const float cw = 164f, ch = 196f, gap = 10f;
            for (int i = 0; i < PetSys.All.Length; i++)
            {
                var p = PetSys.All[i];
                int plv = PetSys.LevelOf(s, p.id);
                bool owned = plv > 0;
                var cell = UIKit.Node("pet_" + p.id, grid);
                cell.Anchor(UIKit.TopLeft, new Vector2((i % 3) * (cw + gap), -(i / 3) * (ch + gap)), new Vector2(cw, ch));
                var pc = Theme.Hex(PetSys.RarityHex[p.rarity]);
                var frame = UIKit.Img(cell, null, (p.id == _sel ? Theme.GreenDeep : pc).Alpha(owned ? 1f : 0.45f), "frame");
                frame.rectTransform.Stretch(-3, -3, -3, -3);
                Chrome.Shape(frame, 21f);
                var face = UIKit.Img(cell, null, owned ? Theme.Cream : Theme.Cream3, "face");
                face.rectTransform.Stretch();
                Chrome.Shape(face, 18f);
                face.raycastTarget = true;

                var im = UIKit.Img(cell, Art.Load("Art/pets/" + p.id + "/portrait"), owned ? Color.white : new Color(0.16f, 0.13f, 0.2f, 0.55f), "im");
                im.preserveAspect = true;
                im.rectTransform.Anchor(UIKit.Top, new Vector2(0, -8), new Vector2(124, 124));

                var nm = UIKit.Label(cell, owned ? p.name : "???", 19, owned ? Theme.Ink : Theme.InkSoft, TextAnchor.MiddleCenter, FontStyle.Bold);
                nm.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 30), new Vector2(cw - 10, 28));
                var sub = UIKit.Label(cell, owned ? "Cấp " + plv : PetSys.RarityName[p.rarity], 16, owned ? Theme.InkSoft : pc, TextAnchor.MiddleCenter);
                sub.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 7), new Vector2(cw - 10, 24));

                if (s.petActive == p.id)
                {
                    var badge = UIKit.Node("active", cell);
                    badge.Anchor(UIKit.TopLeft, new Vector2(6, -6), new Vector2(34, 34));
                    UIKit.Round(badge, Theme.Green, 17, "bg").rectTransform.Stretch();
                    var paw = UIKit.Img(badge, Theme.Skin.Check, Color.white, "ic");
                    paw.preserveAspect = true;
                    paw.rectTransform.Stretch(7, 7, 7, 7);
                }

                if (owned)
                {
                    var b = cell.gameObject.AddComponent<Button>();
                    b.targetGraphic = face;
                    string id = p.id;
                    b.onClick.AddListener(() => { _sel = id; Sfx.Play(SfxId.Tab); Refresh(); });
                    cell.gameObject.AddComponent<PressFx>();
                }
            }
            Tween.Stagger(grid, 0.03f);
        }

        static void Stars(RectTransform parent, Vector2 at, int level)
        {
            for (int i = 0; i < PetSys.MaxLevel; i++)
            {
                var st = UIKit.Img(parent, Theme.Skin.StarGold, i < level ? Color.white : new Color(0.35f, 0.3f, 0.25f, 0.28f), "star");
                st.preserveAspect = true;
                st.rectTransform.Anchor(UIKit.TopLeft, at + new Vector2(i * 34f, 0), new Vector2(30, 30));
            }
        }

        // ============================================================
        // the hatchery
        // ============================================================
        void BuildHatchery()
        {
            var s = GS.Local;
            var well = UIKit.Node("well", _page);
            well.Stretch();
            Well(well);

            // left: the egg
            var glow = UIKit.Img(well, Theme.Glow(), Theme.Hex("#FFE9A8").Alpha(0.8f), "glow");
            glow.rectTransform.Anchor(UIKit.Left, new Vector2(40, 30), new Vector2(340, 340));
            var egg = UIKit.Img(well, Art.Item("item_egg"), Color.white, "egg");
            egg.preserveAspect = true;
            egg.rectTransform.Anchor(UIKit.Left, new Vector2(95, 40), new Vector2(230, 230));
            _egg = egg.rectTransform;

            // right: the odds
            const float x = 430f;
            var head = UIKit.Label(well, "Tỉ lệ nở", 24, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            head.rectTransform.Anchor(UIKit.TopLeft, new Vector2(x, -20), new Vector2(300, 36));
            for (int r = PetSys.RarityOdds.Length - 1; r >= 0; r--)
            {
                int row = PetSys.RarityOdds.Length - 1 - r;
                float y = -66f - row * 50f;
                var rc = Theme.Hex(PetSys.RarityHex[r]);
                var pill = UIKit.Node("r" + r, well);
                pill.Anchor(UIKit.TopLeft, new Vector2(x, y), new Vector2(130, 34));
                UIKit.Round(pill, rc, 17, "bg").rectTransform.Stretch();
                UIKit.LabelOutlined(pill, PetSys.RarityName[r], 16, Color.white, TextAnchor.MiddleCenter).rectTransform.Stretch();
                var pct = UIKit.Label(well, (PetSys.RarityOdds[r] * 100f).ToString("0") + "%", 20, Theme.Ink, TextAnchor.MiddleRight, FontStyle.Bold);
                pct.rectTransform.Anchor(UIKit.TopLeft, new Vector2(x + 136, y - 1), new Vector2(64, 34));
                // the pets of that rarity, as small faces
                int k = 0;
                foreach (var p in PetSys.All.Where(pp => pp.rarity == r))
                {
                    var f = UIKit.Img(well, Art.Load("Art/pets/" + p.id + "/idle"), s.pets.ContainsKey(p.id) ? Color.white : new Color(0.16f, 0.13f, 0.2f, 0.55f), "f");
                    f.preserveAspect = true;
                    f.rectTransform.Anchor(UIKit.TopLeft, new Vector2(x + 218 + k * 50, y + 6), new Vector2(46, 46));
                    k++;
                }
            }
            int left = PetSys.Pity - s.petPity;
            var pity = UIKit.Label(well, "Chắc chắn nở Sử thi trở lên trong " + left + " trứng nữa", 17, Theme.InkSoft, TextAnchor.MiddleLeft);
            pity.rectTransform.Anchor(UIKit.TopLeft, new Vector2(x, -272), new Vector2(520, 28));
            var dup = UIKit.Label(well, "Nở trùng thú cưng đã có: thú cưng đó lên 1 cấp (tối đa " + PetSys.MaxLevel + ")", 17, Theme.InkSoft, TextAnchor.MiddleLeft);
            dup.rectTransform.Anchor(UIKit.TopLeft, new Vector2(x, -300), new Vector2(560, 28));

            long p1 = PetSys.EggPrice(s, 1), p10 = PetSys.EggPrice(s, 10);
            if (s.petFreeEggs > 0)
            {
                // the stock sits on the egg itself; a line of text above the buttons ran into them
                var badge = UIKit.Node("eggs", well);
                badge.Anchor(UIKit.Left, new Vector2(250, -96), new Vector2(92, 40));
                UIKit.Round(badge, Theme.Purple, 20, "bg").rectTransform.Stretch();
                UIKit.LabelOutlined(badge, "×" + Fmt.N(s.petFreeEggs), 20, Color.white, TextAnchor.MiddleCenter).rectTransform.Stretch();
            }
            var one = UIKit.Btn(well, p1 == 0 ? "Ấp 1 · Miễn phí" : "Ấp 1 · " + Fmt.Short(p1), Theme.Blue, Theme.BlueDeep, 21, 22, () => Hatch(1));
            one.GetComponent<RectTransform>().Anchor(UIKit.BottomLeft, new Vector2(x, 24), new Vector2(250, 60));
            var ten = UIKit.Btn(well, p10 == 0 ? "Ấp 10 · Miễn phí" : "Ấp 10 · " + Fmt.Short(p10), Theme.Amber, Theme.AmberDeep, 21, 22, () => Hatch(10));
            ten.GetComponent<RectTransform>().Anchor(UIKit.BottomLeft, new Vector2(x + 266, 24), new Vector2(250, 60));
            if (s.coin < p1) UIKit.Restyle(one, Theme.Cream3, Theme.InkSoft);
            if (s.coin < p10) UIKit.Restyle(ten, Theme.Cream3, Theme.InkSoft);
        }

        RectTransform _egg;

        void Hatch(int count)
        {
            var s = GS.Local;
            if (s.coin < PetSys.EggPrice(s, count)) { app.Toast("Không đủ xu ấp trứng"); return; }
            var got = PetSys.Hatch(s, count, () => Random.value);
            if (got == null) return;
            GS.Save();
            app.OnPetsChanged();
            Tween.Kill(_revealCo);
            _revealCo = Tween.I.StartCoroutine(Reveal(got));
        }

        Coroutine _revealCo;

        IEnumerator Reveal(List<(PetDef pet, bool isNew, int level)> got)
        {
            // the egg shakes harder and harder
            if (_egg != null)
            {
                Sfx.Play(SfxId.ChestOpen);
                for (float t = 0f; t < 0.9f && _egg != null; t += Time.unscaledDeltaTime)
                {
                    float a = Mathf.Lerp(3f, 16f, t / 0.9f);
                    _egg.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 42f) * a);
                    yield return null;
                }
            }
            if (_page == null) yield break;

            int best = got.Max(g => g.pet.rarity);
            ClearList(_page);
            _reveal = UIKit.Node("reveal", _page);
            _reveal.Stretch();
            Well(_reveal);
            var rc = Theme.Hex(PetSys.RarityHex[best]);
            Sfx.Play(best >= 3 ? SfxId.Legendary : best >= 2 ? SfxId.Mutation : SfxId.Claim);

            var flash = UIKit.Img(_reveal, Theme.Glow(), rc.Alpha(0.9f), "flash");
            flash.rectTransform.Anchor(UIKit.Center, new Vector2(0, 30), new Vector2(200, 200));
            Tween.I.StartCoroutine(Grow(flash.rectTransform, 200f, got.Count == 1 ? 620f : 900f, 0.5f));

            if (got.Count == 1)
            {
                var g = got[0];
                var gc = Theme.Hex(PetSys.RarityHex[g.pet.rarity]);
                var rays = UIKit.Img(_reveal, Art.Load("Art/fx/mut_rays"), gc.Alpha(0.55f), "rays");
                rays.rectTransform.Anchor(UIKit.Center, new Vector2(0, 60), new Vector2(420, 420));
                Tween.I.StartCoroutine(Spin(rays.rectTransform));
                var im = UIKit.Img(_reveal, Art.Load("Art/pets/" + g.pet.id + "/happy"), Color.white, "pet");
                im.preserveAspect = true;
                im.rectTransform.Anchor(UIKit.Center, new Vector2(0, 66), new Vector2(210, 210));
                Tween.PopIn(im.rectTransform, 0.45f, 0.2f);
                var nm = UIKit.Label(_reveal, g.pet.name, 34, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                nm.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 124), new Vector2(600, 48));
                var line = UIKit.Label(_reveal, PetSys.RarityName[g.pet.rarity] + " · " + (g.isNew ? "Thú cưng mới!" : "Lên cấp " + g.level), 21, gc, TextAnchor.MiddleCenter, FontStyle.Bold);
                line.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 86), new Vector2(600, 32));
            }
            else
            {
                const float cw = 150f, ch = 158f, gap = 10f;
                float x0 = -(5 * cw + 4 * gap) / 2f + cw / 2f;
                var cells = UIKit.Node("cells", _reveal);
                cells.Stretch();
                for (int i = 0; i < got.Count; i++)
                {
                    var g = got[i];
                    var gc = Theme.Hex(PetSys.RarityHex[g.pet.rarity]);
                    var cell = UIKit.Node("g" + i, cells);
                    cell.Anchor(UIKit.Center, new Vector2(x0 + (i % 5) * (cw + gap), 122 - (i / 5) * (ch + gap)), new Vector2(cw, ch));
                    var fr = UIKit.Img(cell, null, gc, "frame");
                    fr.rectTransform.Stretch(-3, -3, -3, -3);
                    Chrome.Shape(fr, 20f);
                    var fc = UIKit.Img(cell, null, Theme.Cream, "face");
                    fc.rectTransform.Stretch();
                    Chrome.Shape(fc, 17f);
                    var im = UIKit.Img(cell, Art.Load("Art/pets/" + g.pet.id + "/idle"), Color.white, "pet");
                    im.preserveAspect = true;
                    im.rectTransform.Anchor(UIKit.Top, new Vector2(0, -6), new Vector2(96, 96));
                    UIKit.Label(cell, g.pet.name, 18, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold)
                         .rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 26), new Vector2(cw - 8, 26));
                    UIKit.Label(cell, g.isNew ? "Mới!" : "Lên cấp " + g.level, 16, g.isNew ? Theme.GreenDeep : gc, TextAnchor.MiddleCenter, FontStyle.Bold)
                         .rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 5), new Vector2(cw - 8, 22));
                }
                Tween.Stagger(cells, 0.07f);
            }

            var ok = UIKit.Btn(_reveal, "Tuyệt!", Theme.Green, Theme.GreenDark, 24, 22, () =>
            {
                if (_reveal != null) Object.Destroy(_reveal.gameObject);
                _reveal = null;
                Refresh();
            });
            ok.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 14), new Vector2(220, 56));
            _wallet.text = Fmt.Short(GS.Local.coin);
        }

        static IEnumerator Grow(RectTransform rt, float from, float to, float time)
        {
            var im = rt.GetComponent<Image>();
            Color c = im.color;
            for (float t = 0f; t < time && rt != null; t += Time.unscaledDeltaTime)
            {
                float k = t / time;
                rt.sizeDelta = Vector2.one * Mathf.Lerp(from, to, 1f - (1f - k) * (1f - k));
                im.color = c.Alpha(c.a * (1f - k * 0.7f));
                yield return null;
            }
        }

        static IEnumerator Spin(RectTransform rt)
        {
            while (rt != null)
            {
                rt.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 22f);
                yield return null;
            }
        }

        /// <summary>For the screenshot pass: open straight on a reveal.</summary>
        public void RevealForAudit(List<(PetDef pet, bool isNew, int level)> got)
        {
            _revealCo = Tween.I.StartCoroutine(Reveal(got));
        }
    }
}
