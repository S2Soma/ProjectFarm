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

            var shadow = UIKit.Img(_root, Theme.Shadow(26, 26), new Color(0, 0, 0, 0.28f), "shadow");
            shadow.type = Image.Type.Sliced;
            shadow.rectTransform.Stretch(-10, -16, -10, -40);    // (left, top, right, bottom)

            // extends below the screen edge so only the top corners are rounded
            var bg = UIKit.Round(_root, Theme.Cream, 26, "bg");
            bg.rectTransform.Stretch(0, 0, 0, -40);
            bg.raycastTarget = true;

            var grip = UIKit.Round(_root, Theme.Cream3, 3, "grip");
            grip.rectTransform.Anchor(UIKit.Top, new Vector2(0, -8), new Vector2(64, 6));

            _title = UIKit.Label(_root, "Chọn hạt giống", 22, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            _title.rectTransform.Anchor(UIKit.TopLeft, new Vector2(28, -16), new Vector2(460, 32));
            _sub = UIKit.Label(_root, "", 16, Theme.InkSoft, TextAnchor.MiddleLeft);
            _sub.rectTransform.Anchor(UIKit.TopLeft, new Vector2(28, -46), new Vector2(700, 24));

            var close = UIKit.IconBtn(_root, Theme.Skin.IconCross, Theme.Red, 0.5f, () => _app.CloseSeedSheet());
            close.GetComponent<RectTransform>().Anchor(UIKit.TopRight, new Vector2(-18, -14), new Vector2(44, 44));

            // horizontal card strip
            var strip = UIKit.Node("strip", _root);
            strip.anchorMin = new Vector2(0, 0); strip.anchorMax = new Vector2(1, 1);
            strip.offsetMin = new Vector2(16, 10); strip.offsetMax = new Vector2(-16, -76);
            var scroll = strip.gameObject.AddComponent<ScrollRect>();
            strip.gameObject.AddComponent<RectMask2D>();
            var hit = strip.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            _content = UIKit.Node("content", strip);
            _content.anchorMin = new Vector2(0, 0); _content.anchorMax = new Vector2(0, 1);
            _content.pivot = new Vector2(0, 0.5f);
            _content.anchoredPosition = Vector2.zero;
            var row = _content.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 12; row.padding = new RectOffset(6, 6, 4, 4);
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
        public static List<Seed> Order(PlayerState s)
        {
            var owned = new List<Seed>();
            var shop = new List<Seed>();
            foreach (var seed in GameData.Seeds)
            {
                if (seed.lv > s.lv) continue;
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
            foreach (Transform c in _content) UnityEngine.Object.Destroy(c.gameObject);
            var s = GS.Local;
            var w = WeatherSys.Now(s);
            foreach (var seed in Order(s)) BuildCard(seed, s, w);
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

            var frame = UIKit.Img(card, Theme.Round(18), Theme.Rarity[Mathf.Clamp(seed.r, 0, 3)].Alpha(0.9f), "frame");
            frame.type = Image.Type.Sliced;
            frame.rectTransform.Stretch(-3, -3, -3, -3);
            var face = UIKit.Round(card, afford ? Color.white : Theme.Cream3, 18, "face");
            face.rectTransform.Stretch();
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
            pill.Anchor(UIKit.TopRight, new Vector2(-6, -6), new Vector2(have > 0 ? 44 : 66, 24));
            UIKit.Round(pill, have > 0 ? Theme.Ink.Alpha(0.78f) : Theme.Amber, 12, "bg").rectTransform.Stretch();
            if (have > 0)
                UIKit.Label(pill, "×" + have, 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();
            else
            {
                var ci = UIKit.Img(pill, Theme.Skin.Coin, Color.white, "coin");
                ci.preserveAspect = true;
                ci.rectTransform.Anchor(UIKit.Left, new Vector2(4, 0), new Vector2(18, 18));
                UIKit.Label(pill, seed.price == 0 ? "0" : Fmt.N(seed.price), 14, Theme.Ink, TextAnchor.MiddleRight, FontStyle.Bold)
                     .rectTransform.Stretch(22, 0, 6, 0);
            }

            var nm = UIKit.Label(card, seed.name, 17, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            nm.rectTransform.Anchor(UIKit.Top, new Vector2(0, -80), new Vector2(CW - 8, 24));

            int grow = Mathf.RoundToInt(s.GrowTimeIn(seed, w, 0));
            int profit = s.HarvestValue(seed.id, 0) - seed.price;
            var l1 = UIKit.Label(card, "Chín " + Fmt.Time(grow), 14, Theme.InkSoft, TextAnchor.MiddleCenter);
            l1.rectTransform.Anchor(UIKit.Top, new Vector2(0, -104), new Vector2(CW - 8, 20));
            var l2 = UIKit.Label(card, "Lãi +" + Fmt.N(profit) + " xu", 14, Theme.GreenDeep, TextAnchor.MiddleCenter, FontStyle.Bold);
            l2.rectTransform.Anchor(UIKit.Top, new Vector2(0, -124), new Vector2(CW - 8, 20));

            // The crop's tag this half-day, if it has one; otherwise its rarity. One chip — two
            // would not fit a 164 px card without shrinking the text below legibility.
            var tag = TagSys.TagOf(s, seed.id, GS.Now);
            string chipText; Color chipCol;
            if (tag != CropTag.None) { var td = TagSys.Def(tag); chipText = td.name; chipCol = Theme.Hex(td.hex); }
            else
            {
                string[] rn = { "Thường", "Hiếm", "Sử thi", "Huyền thoại" };
                chipText = rn[Mathf.Clamp(seed.r, 0, 3)];
                chipCol = Theme.Rarity[Mathf.Clamp(seed.r, 0, 3)];
            }
            var chip = UIKit.Node("tag", card);
            chip.Anchor(UIKit.Bottom, new Vector2(0, 8), new Vector2(112, 22));
            UIKit.Round(chip, chipCol, 11, "bg").rectTransform.Stretch();
            UIKit.Label(chip, chipText, 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch();

            var b = card.gameObject.AddComponent<Button>();
            b.targetGraphic = face;
            string id = seed.id;
            b.onClick.AddListener(() => _app.PickSeed(id));
            card.gameObject.AddComponent<PressFx>();
        }
    }
}
