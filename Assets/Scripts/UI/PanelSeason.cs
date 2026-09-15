using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>"Mùa vụ &amp; Cây bonus" — the full weather and crop-tag picture.
    ///
    /// The HUD bar shows the SITUATION; this shows the NUMBERS. Weather and tags multiply
    /// together, but the product is only meaningful once a crop is named, so it appears here, per
    /// crop, and nowhere that would have to generalise it across twenty-eight of them.</summary>
    public class SeasonPanel : PanelBase
    {
        public SeasonPanel(GameApp app) : base(app) { }

        public override string Title => "Mùa vụ & Cây bonus";
        /// <summary>Taller while a bought forecast is running: the twelve-hour row is a whole
        /// extra band, and squeezing it into the existing height would cost the weather cells the
        /// size that makes them readable.</summary>
        public override Vector2 Size => new Vector2(920, ShopSys.ForecastActive(GS.Local) ? 600 : 520);

        public override void Build()
        {
            var body = Body;
            var s = GS.Local;
            long now = GS.Now;
            var w = WeatherSys.Now(s);
            var d = WeatherSys.Def(w);

            // ---------------- left column: weather ----------------
            var left = UIKit.Node("weather", body);
            left.Anchor(UIKit.TopLeft, new Vector2(8, -8), new Vector2(420, 470));
            left.pivot = new Vector2(0, 1);

            var hero = UIKit.Round(left, Theme.Cream, 18, "hero");
            hero.rectTransform.Anchor(UIKit.TopLeft, Vector2.zero, new Vector2(420, 124));
            hero.rectTransform.pivot = new Vector2(0, 1);

            var icon = UIKit.Img(hero.rectTransform, Art.WeatherIconNow(w, out var nowTint), nowTint, "icon");
            icon.preserveAspect = true;
            icon.rectTransform.Anchor(UIKit.Left, new Vector2(62, 0), new Vector2(76, 76));

            var nm = UIKit.Label(hero.rectTransform, d.name, 28, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            nm.rectTransform.Anchor(UIKit.Left, new Vector2(230, 22), new Vector2(240, 34));

            var clock = UIKit.Label(hero.rectTransform, "", 30, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            clock.rectTransform.Anchor(UIKit.Left, new Vector2(230, -18), new Vector2(240, 36));
            clock.text = "Còn " + Fmt.Time((int)(WeatherSys.MsLeft(now) / 1000L));

            var note = UIKit.Label(left, d.note, 15, Theme.InkSoft, TextAnchor.MiddleLeft);
            note.rectTransform.Anchor(UIKit.TopLeft, new Vector2(8, -132), new Vector2(410, 24));
            note.rectTransform.pivot = new Vector2(0, 1);

            // Four stat rows. A term equal to 1.0 is drawn in InkSoft so the eye skips what is
            // not doing anything — the set of terms that matter changes every hour, and that is
            // the thing worth seeing at a glance.
            StatRow(left, 0, "Thời gian chín", d.grow, false);
            StatRow(left, 1, "Giá bán", d.sell, true);
            StatRow(left, 2, "Kinh nghiệm", d.xp, true);
            StatRow(left, 3, "Đột biến", d.mutate, true);

            // trước → nay → kế. Three cells is honest: rotation is random, and only "next" is
            // ever knowable. It also teaches the sixty-minute cadence without a word.
            long h = WeatherSys.SlotIndex(now);
            Strip(left, 0, "Trước", WeatherSys.At(s, h - 1), 0.45f);
            Strip(left, 1, "Nay", w, 1f);
            bool reveal = WeatherSys.Revealed(s, now);
            Strip(left, 2, reveal ? "Kế" : "Kế · ?", reveal ? WeatherSys.Next(s) : (Weather)255, reveal ? 1f : 0.35f);

            // With a forecast running, the three-cell strip becomes a twelve-hour row. It is a
            // separate row rather than a longer strip because the three cells answer "what is
            // happening" and this one answers "when should I plant" — different questions, and
            // the first should not get smaller to make room for the second.
            if (ShopSys.ForecastActive(s)) Forecast(left, s, h);

            // ---------------- right column: tagged crops ----------------
            var right = UIKit.Node("tags", body);
            right.Anchor(UIKit.TopLeft, new Vector2(444, -8), new Vector2(432, 400));
            right.pivot = new Vector2(0, 1);

            var hdr = UIKit.Label(right, "Làm mới sau " + Fmt.Time((int)(TagSys.MsLeft(now) / 1000L)),
                                  20, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            hdr.rectTransform.Anchor(UIKit.TopLeft, Vector2.zero, new Vector2(432, 26));
            hdr.rectTransform.pivot = new Vector2(0, 1);

            var sub = UIKit.Label(right, "Đổi lúc 06:00 và 18:00", 15, Theme.InkSoft, TextAnchor.MiddleLeft);
            sub.rectTransform.Anchor(UIKit.TopLeft, new Vector2(0, -26), new Vector2(432, 22));
            sub.rectTransform.pivot = new Vector2(0, 1);

            var set = TagSys.Now(s);
            for (int i = 0; i < set.Count; i++) TagCell(right, i, set[i], w);
        }

        void StatRow(RectTransform parent, int index, string label, float mul, bool higherIsBetter)
        {
            float x = (index % 2) * 208f;
            float y = -168f - (index / 2) * 40f;

            var row = UIKit.Round(parent, Theme.Cream, 14, "stat");
            row.rectTransform.Anchor(UIKit.TopLeft, new Vector2(x, y), new Vector2(200, 32));
            row.rectTransform.pivot = new Vector2(0, 1);

            var l = UIKit.Label(row.rectTransform, label, 16, Theme.InkSoft, TextAnchor.MiddleLeft);
            l.rectTransform.Stretch(12, 0, 70, 0);

            bool idle = Mathf.Abs(mul - 1f) < 0.001f;
            int pct = Mathf.RoundToInt((mul - 1f) * 100f);
            var v = UIKit.Label(row.rectTransform, idle ? "·" : (pct > 0 ? "+" : "") + pct + "%",
                                18, idle ? Theme.InkSoft : (((pct > 0) == higherIsBetter) ? Theme.GreenDeep : Theme.Red),
                                TextAnchor.MiddleRight, idle ? FontStyle.Normal : FontStyle.Bold);
            v.rectTransform.Stretch(120, 0, 12, 0);
        }

        /// <summary>The bought forecast: one small cell per hour, out to twelve.</summary>
        void Forecast(RectTransform parent, PlayerState s, long hour)
        {
            int hours = Mathf.Clamp(ShopSys.ForecastSlots(s), 1, 12);

            var hdr = UIKit.Label(parent, "Dự báo " + hours + " lượt thời tiết tới (mỗi lượt 15 phút)", 15, Theme.InkSoft,
                                  TextAnchor.MiddleLeft);
            hdr.rectTransform.Anchor(UIKit.TopLeft, new Vector2(8, -352), new Vector2(410, 20));
            hdr.rectTransform.pivot = new Vector2(0, 1);

            const float cw = 33f;
            for (int i = 0; i < hours; i++)
            {
                var fw = WeatherSys.At(s, hour + 1 + i);
                var cell = UIKit.Node("f" + i, parent);
                cell.Anchor(UIKit.TopLeft, new Vector2(10 + i * cw, -374), new Vector2(cw - 3f, 42));
                cell.pivot = new Vector2(0, 1);
                UIKit.Round(cell, new Color(0, 0, 0, 0.06f), 8, "bg").rectTransform.Stretch();

                var ic = UIKit.Img(cell, Art.WeatherIcon(fw), Theme.Hex(WeatherSys.Def(fw).hex), "ic");
                ic.preserveAspect = true;
                ic.rectTransform.Anchor(UIKit.Top, new Vector2(0, -3), new Vector2(22, 22));

                // Clock time the window starts, so the row maps onto the player's evening.
                var start = System.DateTimeOffset.FromUnixTimeMilliseconds(WeatherSys.SlotStart(hour + 1 + i)).ToLocalTime();
                var lb = UIKit.Label(cell, start.ToString("H:mm"), 10, Theme.InkSoft, TextAnchor.MiddleCenter);
                lb.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 8), new Vector2(cw, 14));
            }
        }

        void Strip(RectTransform parent, int index, string caption, Weather w, float alpha)
        {
            var cell = UIKit.Node("strip" + index, parent);
            cell.Anchor(UIKit.TopLeft, new Vector2(index * 96f + 62f, -256f), new Vector2(88, 88));
            cell.pivot = new Vector2(0, 1);

            bool known = (int)w < WeatherSys.All.Length;
            var hex = known ? WeatherSys.Def(w).hex : "#8A8478";

            var disc = UIKit.Round(cell, new Color(0, 0, 0, 0.06f), 16, "bg");
            disc.rectTransform.Stretch();

            if (known)
            {
                var ic = UIKit.Img(cell, Art.WeatherIcon(w), Theme.Hex(hex), "ic");
                ic.preserveAspect = true;
                ic.rectTransform.Anchor(UIKit.Center, new Vector2(0, 10), new Vector2(40, 40));
                var c = ic.color; c.a = alpha; ic.color = c;
            }
            else
            {
                // Not a star: a star is a reward glyph everywhere else in this game, and using it
                // for "unknown" would read as a promise rather than a blank.
                var q = UIKit.Label(cell, "?", 34, new Color(0.54f, 0.52f, 0.47f, 0.75f),
                                    TextAnchor.MiddleCenter, FontStyle.Bold);
                q.rectTransform.Anchor(UIKit.Center, new Vector2(0, 10), new Vector2(40, 44));
            }

            var cap = UIKit.Label(cell, known ? WeatherSys.Def(w).name : caption, 14,
                                  Theme.InkSoft, TextAnchor.MiddleCenter);
            cap.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 14), new Vector2(88, 20));
        }

        static string Num(float v) { return v.ToString("0.00", Fmt.Vi); }

        void TagCell(RectTransform parent, int index, TagSys.Tagged t, Weather nowW)
        {
            var seed = GameData.Get(t.cropId);
            if (seed == null) return;
            var def = TagSys.Def(t.tag);

            float x = (index % 3) * 142f;
            float y = -62f - (index / 3) * 158f;

            bool season = t.tag == CropTag.Season;
            bool live = season && t.weather == nowW;

            // The season tag's border lighting up when its weather arrives IS the reason weather
            // and tags share a frame. Flattening the pair into one number would make the
            // interaction invisible, which is the interaction's whole value.
            //
            // The border is the cell's own plate with the cream face inset 3 px inside it. It was
            // a CIRCLE ring sprite stretched over a 132x150 card — an ellipse cutting through the
            // name and the chip.
            Color border = live ? Theme.Amber : Theme.Hex(def.hex);
            if (!live && !season) border.a = 0.55f;
            var cell = UIKit.Round(parent, border, 19, "tag" + index);
            cell.rectTransform.Anchor(UIKit.TopLeft, new Vector2(x, y), new Vector2(132, 150));
            cell.rectTransform.pivot = new Vector2(0, 1);
            var inner = UIKit.Round(cell.rectTransform, Theme.Cream, 16, "face");
            inner.rectTransform.Stretch(3, 3, 3, 3);

            var art = UIKit.Img(cell.rectTransform, Art.Icon(seed.art, 0), Color.white, "art");
            art.preserveAspect = true;
            art.rectTransform.Anchor(UIKit.Top, new Vector2(0, -10), new Vector2(58, 58));

            var nm = UIKit.Label(cell.rectTransform, seed.name, 15, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            nm.rectTransform.Anchor(UIKit.Top, new Vector2(0, -72), new Vector2(128, 22));

            var chip = UIKit.Round(cell.rectTransform, Theme.Hex(def.hex), 10, "chip");
            chip.rectTransform.Anchor(UIKit.Top, new Vector2(0, -96), new Vector2(104, 22));
            var ct = UIKit.Label(chip.rectTransform, def.name, 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            ct.rectTransform.Stretch();

            // This is where the combined rate lives — per crop, where it is actually true.
            //
            // Each tag reports on ITS OWN axis. A first version printed the sell multiplier for
            // every tag, so an XP tag read "bán ×1,00" — arithmetically true and completely
            // useless, since the one thing the player wants to know is what the tag does.
            string line;
            if (season)
            {
                // "×2,2 · đang hiệu lực" is ~140 px at 13 px bold in a 128 px box, and labels
                // overflow silently — it ran over both cell borders.
                line = live ? "×2,2 lúc này" : "×2,2 khi " + WeatherSys.Def(t.weather).name;
            }
            else if (t.tag == CropTag.Xp)
            {
                line = "kinh nghiệm ×" + Num(WeatherSys.Def(nowW).xp * def.xp);
            }
            else if (t.tag == CropTag.Mutation)
            {
                line = "đột biến ×" + Num(WeatherSys.Def(nowW).mutate * def.mutate);
            }
            else
            {
                line = "bán ×" + Num(WeatherSys.Def(nowW).sell * def.sell);
            }
            var ln = UIKit.Label(cell.rectTransform, line, 13,
                                 live ? Theme.AmberDeep : Theme.InkSoft, TextAnchor.MiddleCenter,
                                 live ? FontStyle.Bold : FontStyle.Normal);
            ln.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 12), new Vector2(128, 20));
        }
    }
}
