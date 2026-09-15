using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>The seed picker: a sheet that slides up from the bottom when an empty bed is
    /// chosen (or when "Gieo" is pressed), with one card per seed.
    ///
    /// It replaces a 420x232 popup that floated above the plot, covered the neighbouring beds and
    /// had to be dismissed and re-opened for every single bed. The sheet stays open while the
    /// player works: after each planting the game moves the selection to the nearest empty bed
    /// and scrolls it into view, so filling a field is "tap a seed, tap a seed, tap a seed". It
    /// slides away by itself when the island has no empty bed left.
    ///
    /// Cards show what the decision needs and nothing else: the picture, how many are in the bag
    /// (or the price, since tapping an unowned seed buys one), the grow time in THIS hour's
    /// weather, the profit per harvest, and the crop's rotating tag. Weather and tags change the
    /// right answer every hour, so they belong on the card rather than one panel away.</summary>
    public class SeedSheet
    {
        /// <summary>Height in canvas units. The camera treats this as covered screen.</summary>
        public const float Height = 268f;

        RectTransform _root, _content;
        Text _title, _sub;
        GameApp _app;
        Coroutine _slide;
        public bool IsOpen { get; private set; }

        public void Build(RectTransform layer, GameApp app)
        {
            _app = app;
            _root = UIKit.Node("seedSheet", layer);
            _root.anchorMin = new Vector2(0, 0);
            _root.anchorMax = new Vector2(1, 0);
            _root.pivot = new Vector2(0.5f, 0);
            _root.offsetMin = new Vector2(0, -Height);
            _root.offsetMax = new Vector2(0, 0);
            _root.sizeDelta = new Vector2(0, Height);

            // M3 paper, extending below the screen edge so only the top corners show. Its shadow
            // falls UPWARD onto the farm: a sheet rising out of the bottom edge is lit from the
            // screen, and a hard 1 px edge with no shadow read as a sticker on the map.
            var bgNode = UIKit.Node("bg", _root);
            bgNode.Stretch(0, 0, 0, -40);
            // paper to the screen's side edges, past a notch; the cards stay inside the safe area
            FullBleed.On(bgNode, left: true, right: true, bottom: false, top: false);
            var paper = Looks.Paper;
            paper.drop = new Vector2(0f, 6f);
            paper.blur = 24f;
            paper.shadow = new Color(0f, 0f, 0f, 0.28f);
            var bg = SurfaceLook.Add(bgNode, paper, 28f).Fill;
            bg.raycastTarget = true;

            var grip = UIKit.Img(_root, null, Theme.Hex("#D9C7A6"), "grip");
            grip.rectTransform.Anchor(UIKit.Top, new Vector2(0, -9), new Vector2(64, 8));
            Chrome.Shape(grip, 4f);

            _title = UIKit.Label(_root, "Chọn hạt giống", 22, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            _title.rectTransform.Anchor(UIKit.TopLeft, new Vector2(28, -16), new Vector2(460, 32));
            _sub = UIKit.Label(_root, "", 16, Theme.InkSoft, TextAnchor.MiddleLeft);
            _sub.rectTransform.Anchor(UIKit.TopLeft, new Vector2(28, -46), new Vector2(700, 24));

            var close = UIKit.IconBtn(_root, Theme.Skin.IconCross, Theme.Red, 0.5f, () => _app.CloseSeedSheet());
            close.GetComponent<RectTransform>().Anchor(UIKit.TopRight, new Vector2(-18, -14), new Vector2(44, 44));

            // horizontal card strip
            var strip = UIKit.Node("strip", _root);
            strip.anchorMin = new Vector2(0, 0); strip.anchorMax = new Vector2(1, 1);
            // 12 px of headroom inside the mask: the count and price badges overhang each card's
            // top corner by 8 and were being sliced off by the strip's RectMask2D.
            strip.offsetMin = new Vector2(16, 6); strip.offsetMax = new Vector2(-16, -70);
            var scroll = strip.gameObject.AddComponent<ScrollRect>();
            strip.gameObject.AddComponent<RectMask2D>();
            var hit = strip.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            _content = UIKit.Node("content", strip);
            _content.anchorMin = new Vector2(0, 0); _content.anchorMax = new Vector2(0, 1);
            _content.pivot = new Vector2(0, 0.5f);
            _content.anchoredPosition = Vector2.zero;
            var row = _content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 14; row.padding = new RectOffset(6, 14, 12, 4);
            row.childControlWidth = false; row.childControlHeight = false;
            row.childForceExpandWidth = false; row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.MiddleLeft;
            var fit = _content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _content; scroll.viewport = strip;
            scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            _root.gameObject.SetActive(false);
        }

        public void Show(string title, string sub)
        {
            _title.text = title;
            _sub.text = sub;
            Rebuild();
            if (IsOpen) return;
            IsOpen = true;
            _root.gameObject.SetActive(true);
            Slide(0f);
        }

        public void SetSubtitle(string sub) { _sub.text = sub; }

        /// <summary>Which plot size the cards are for. Set before <see cref="Show"/> / <see cref="Rebuild"/>.</summary>
        public bool Big;

        /// <summary>The sheet itself, and one seed's card in it — for the tutorial's pointer.</summary>
        public RectTransform Root => _root;
        public RectTransform CardFor(string seedId)
        {
            if (_content == null) return null;
            var t = _content.Find("card_" + seedId);
            return t as RectTransform;
        }

        public void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Slide(-Height - 20f);
        }

        void Slide(float to)
        {
            if (_slide != null) Tween.Kill(_slide);
            _slide = Tween.Run(SlideRoutine(to));
        }

        IEnumerator SlideRoutine(float to)
        {
            float from = _root.anchoredPosition.y;
            if (to >= 0f && !_root.gameObject.activeSelf) from = -Height - 20f;
            const float time = 0.24f;
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                float k = Tween.EaseOut(Mathf.Clamp01(t / time));
                _root.anchoredPosition = new Vector2(0, Mathf.Lerp(from, to, k));
                yield return null;
            }
            _root.anchoredPosition = new Vector2(0, to);
            if (to < 0f) _root.gameObject.SetActive(false);
            _slide = null;
        }

        // ============================================================
        // cards
        // ============================================================
        /// <summary>Seeds the player can plant now: owned ones first (most useful first), then
        /// the rest of the unlocked catalogue, which tapping buys. Locked seeds are not shown — a
        /// card that cannot be tapped is noise in a strip meant to be swiped quickly.</summary>
        public static List<Seed> Order(PlayerState s) { return Order(s, false); }

        /// <summary>The same, for one plot size: trees for a big plot, everything else for a small one.</summary>
        public static List<Seed> Order(PlayerState s, bool big)
        {
            var owned = new List<Seed>();
            var shop = new List<Seed>();
            foreach (var seed in GameData.Seeds)
            {
                if (seed.lv > s.lv || seed.big != big) continue;
                s.seeds.TryGetValue(seed.id, out int n);
                (n > 0 ? owned : shop).Add(seed);
            }
            owned.Sort((a, b) => b.lv.CompareTo(a.lv));
            shop.Sort((a, b) => b.lv.CompareTo(a.lv));
            owned.AddRange(shop);
            return owned;
        }

        public void Rebuild()
        {
            // unparent first: Destroy is deferred, and the layout below would still count the old cards
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var c = _content.GetChild(i);
                c.SetParent(null, false);
                UnityEngine.Object.Destroy(c.gameObject);
            }
            var s = GS.Local;
            var w = WeatherSys.Now(s);
            foreach (var seed in Order(s, Big)) BuildCard(seed, s, w);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        void BuildCard(Seed seed, PlayerState s, Weather w)
        {
            const float CW = 164f, CH = 176f;
            s.seeds.TryGetValue(seed.id, out int have);
            bool afford = have > 0 || s.coin >= seed.price;

            var card = UIKit.Node("card_" + seed.id, _content);
            card.sizeDelta = new Vector2(CW, CH);
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = CW; le.preferredHeight = CH;

            // A raised card with the rarity as its edge colour, instead of a rarity plate drawn
            // 3 px outside a flat white one.
            var cardLook = new Look
            {
                top = afford ? Color.white : Theme.Cream3, bottom = afford ? Theme.Hex("#FBF3E4") : Theme.Hex("#DCCDB1"),
                edge = Theme.Rarity[Mathf.Clamp(seed.r, 0, 3)].Alpha(afford ? 0.95f : 0.5f), edgeW = 3f,
                rim = new Color(1f, 1f, 1f, 0.9f), rimW = 2f, rimFade = 0.2f,
                shadow = new Color(0.25f, 0.16f, 0.05f, 0.18f), blur = 6f, drop = new Vector2(0f, -3f),
            };
            var face = SurfaceLook.Add(card, cardLook, 18f).Fill;
            face.raycastTarget = true;

            //   art       top 6 .. 78
            //   name      84 .. 106
            //   stats    106 .. 146  (two short lines)
            //   tag chip 148 .. 170
            var art = UIKit.Img(card, Art.Icon(seed.art, 0), afford ? Color.white : new Color(1, 1, 1, 0.55f), "art");
            art.preserveAspect = true;
            art.rectTransform.Anchor(UIKit.Top, new Vector2(0, -6), new Vector2(76, 72));

            // bag count, or the price when this tap would buy one
            var pill = UIKit.Node("pill", card);
            // Overhangs the card's corner by 8 px so it never sits on the artwork — at -6 inside,
            // the orange price badge covered the top of the pumpkin.
            pill.Anchor(UIKit.TopRight, new Vector2(8, 8), new Vector2(have > 0 ? 46 : 70, 26));
            var badge = have > 0 ? Looks.Glass : Looks.BtnAmber;
            badge.lip = 0f; badge.shadow = new Color(0f, 0f, 0f, 0.2f); badge.blur = 4f; badge.drop = new Vector2(0, -2);
            badge.edgeW = 1.5f; badge.rimW = 1.5f;
            SurfaceLook.Add(pill, badge);
            if (have > 0)
                UIKit.LabelOutlined(pill, "×" + have, 15, Color.white).rectTransform.Stretch();
            else
            {
                var ci = UIKit.Img(pill, Theme.Skin.Coin, Color.white, "coin");
                ci.preserveAspect = true;
                ci.rectTransform.Anchor(UIKit.Left, new Vector2(5, 0), new Vector2(18, 18));
                UIKit.LabelOutlined(pill, seed.price == 0 ? "0" : Fmt.N(seed.price), 14, Color.white, TextAnchor.MiddleRight, badge.inkLine)
                     .rectTransform.Stretch(22, 0, 9, 0);
            }

            var nm = UIKit.Label(card, seed.name, 17, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            nm.rectTransform.Anchor(UIKit.Top, new Vector2(0, -78), new Vector2(CW - 8, 26));

            int grow = Mathf.RoundToInt(s.GrowTimeIn(seed, w, 0));
            int profit = s.HarvestValue(seed.id, 0) - seed.price;
            // the time in THIS weather, and how often the crop drinks — the two facts that decide whether
            // it is a crop to tend now or one to leave growing
            var l1 = UIKit.Label(card, "Chín " + Fmt.Time(grow) + " · tưới " + seed.waters, 14, Theme.InkSoft, TextAnchor.MiddleCenter);
            l1.rectTransform.Anchor(UIKit.Top, new Vector2(0, -102), new Vector2(CW - 8, 20));
            var l2 = UIKit.Label(card, "Lãi +" + Fmt.N(profit) + " xu", 14, Theme.GreenDeep, TextAnchor.MiddleCenter, FontStyle.Bold);
            l2.rectTransform.Anchor(UIKit.Top, new Vector2(0, -120), new Vector2(CW - 8, 20));

            // The crop's bonus this half-day, if it has one; otherwise its rarity. One line — two
            // would not fit a 164 px card without shrinking the text below legibility.
            //
            // They used to share one pill, so "Hiếm" and "Kinh Nghiệm" were the same blue lozenge
            // and a player could not tell a rarity (permanent, already shown by the card's edge)
            // from a bonus (worth planting for, gone at 18:00). The bonus is now the only pill,
            // starred; rarity is plain text in its colour.
            var tag = TagSys.TagOf(s, seed.id, GS.Now);
            if (tag != CropTag.None)
            {
                var td = TagSys.Def(tag);
                var chip = UIKit.Node("tag", card);
                chip.Anchor(UIKit.Bottom, new Vector2(0, 11), new Vector2(128, 24));
                var ring = UIKit.Img(chip, null, Theme.Hex("#FFE08A"), "ring");
                ring.rectTransform.Stretch(-2, -2, -2, -2);
                Chrome.Shape(ring, 14f);
                var chipBg = UIKit.Img(chip, null, Theme.Hex(td.hex), "bg");
                chipBg.rectTransform.Stretch();
                Chrome.Shape(chipBg, 12f);
                UIKit.Label(chip, "★ " + td.name, 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
            }
            else
            {
                string[] rn = { "Thường", "Hiếm", "Sử thi", "Huyền thoại" };
                int r = Mathf.Clamp(seed.r, 0, 3);
                var rl = UIKit.Label(card, rn[r], 14, Color.Lerp(Theme.Rarity[r], Theme.Ink, 0.35f), TextAnchor.MiddleCenter, FontStyle.Bold);
                rl.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 12), new Vector2(CW - 12, 22));
            }

            var b = card.gameObject.AddComponent<Button>();
            b.targetGraphic = face;
            string id = seed.id;
            b.onClick.AddListener(() => _app.PickSeed(id));
            card.gameObject.AddComponent<PressFx>();
        }
    }
}
