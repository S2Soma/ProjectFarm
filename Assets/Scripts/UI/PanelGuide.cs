using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>Menu ▸ Hướng dẫn: the rules, one page per system, and a button to replay the
    /// walkthrough.
    ///
    /// Every number on these pages is read from the table that the game itself uses — weather
    /// from <see cref="WeatherSys.All"/>, bonuses from <see cref="TagSys.Defs"/>, mutation tiers
    /// from <see cref="Art.Elements"/>, islands from <see cref="IslandSys.Defs"/> — so a balance
    /// change can never leave the guide quoting the old figure.
    ///
    /// Pages are pictures first. Each one opens on the thing itself (the thirsty bed, the six
    /// weathers, the four glows) and keeps the prose to a few short lines under it.</summary>
    public class GuidePanel : PanelBase
    {
        public GuidePanel(GameApp app, int page = 0) : base(app) { _page = page; }
        public override string Title => "Hướng dẫn chơi";
        public override Vector2 Size => new Vector2(1060, 620);

        int _page;
        RectTransform _content;
        readonly List<(GameObject face, Text label, Image icon)> _tabs = new List<(GameObject, Text, Image)>();

        public const int PageCount = 8;

        /// <summary>Name, icon, and the tint for icons that are white glyphs (null for painted
        /// ones): a white glyph on the cream card is invisible until its row is selected.</summary>
        static readonly (string name, Func<Sprite> icon, Color? glyph)[] Pages =
        {
            ("Cơ bản",       () => Theme.Skin.NavSeeds, Theme.GreenDeep),
            ("Tưới nước",    () => Art.Item("item_can"), null),
            ("Thời tiết",    () => Art.WeatherIcon(Weather.Rain), Theme.Hex(WeatherSys.Def(Weather.Rain).hex)),
            ("Cây bonus",    () => Theme.Skin.StarGold, null),
            ("Đột biến",     () => Art.Icon("carrot", 3), null),
            ("Nhiệm vụ",     () => Art.Item("medal_gold"), null),
            ("Rương",        () => Art.Item("chest_1"), null),
            ("Đảo & ô đất",  () => Theme.Skin.Farmhouse, Theme.BlueDeep),
        };

        const float ListW = 232f, RowH = 50f, RowGap = 5f;

        public override void Build()
        {
            // --- page list ---
            var list = UIKit.Node("pages", Body);
            list.anchorMin = new Vector2(0, 0); list.anchorMax = new Vector2(0, 1);
            list.pivot = new Vector2(0, 1);
            list.offsetMin = new Vector2(0, 0); list.offsetMax = new Vector2(ListW, 0);

            for (int i = 0; i < Pages.Length; i++)
            {
                int idx = i;
                var row = UIKit.Node("page" + i, list);
                row.Anchor(UIKit.TopLeft, new Vector2(0, -i * (RowH + RowGap)), new Vector2(ListW, RowH));

                var face = UIKit.Node("face", row);
                face.Stretch();
                var look = Looks.BtnGreen;
                look.lip = 3f; look.shadow = new Color(0f, 0f, 0f, 0.18f); look.blur = 4f; look.drop = new Vector2(0f, -1f);
                SurfaceLook.Add(face, look, 18f);

                var hit = row.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                var ic = UIKit.Img(row, Pages[i].icon(), Color.white, "icon");
                ic.preserveAspect = true;
                ic.rectTransform.Anchor(UIKit.Left, new Vector2(12, 1), new Vector2(34, 34));
                var lab = UIKit.Label(row, Pages[i].name, 19, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                lab.rectTransform.Stretch(56, 0, 8, 2);
                lab.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);

                var b = row.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { Sfx.Play(SfxId.Tab); ShowPage(idx); });
                row.gameObject.AddComponent<PressFx>();
                _tabs.Add((face.gameObject, lab, ic));
            }

            var replay = UIKit.Btn(list, "Chơi lại hướng dẫn", Theme.Blue, Theme.BlueDeep, 18, 20, () =>
            {
                app.CloseAll();
                app.Tutorial?.Restart();
            });
            replay.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 0), new Vector2(ListW, 52));

            // --- page ---
            var well = UIKit.Node("well", Body);
            well.Stretch(ListW + 18f, 0, 0, 0);
            SurfaceLook.Add(well, Looks.Well, 18f);
            _content = UIKit.Node("content", well);
            _content.Stretch(24, 18, 24, 16);

            ShowPage(_page);
        }

        /// <summary>For the screenshot pass.</summary>
        public void ShowPage(int page)
        {
            _page = Mathf.Clamp(page, 0, Pages.Length - 1);
            for (int i = 0; i < _tabs.Count; i++)
            {
                bool on = i == _page;
                _tabs[i].face.SetActive(on);
                _tabs[i].label.color = on ? Color.white : Theme.Hex("#6F5A40");
                if (Pages[i].glyph.HasValue) _tabs[i].icon.color = on ? Color.white : Pages[i].glyph.Value;
                var o = _tabs[i].label.GetComponent<Outline>();
                o.enabled = on;
                o.effectColor = Theme.Hex("#1C7439");
            }

            ClearList(_content);
            switch (_page)
            {
                case 0: Basics(); break;
                case 1: Watering(); break;
                case 2: WeatherPage(); break;
                case 3: Bonus(); break;
                case 4: Mutation(); break;
                case 5: MissionsPage(); break;
                case 6: Chests(); break;
                case 7: Islands(); break;
            }
        }

        // ============================================================
        // building blocks
        // ============================================================
        float _y;

        Text Heading(string text)
        {
            _y = 0f;
            var t = UIKit.Label(_content, text, 26, Theme.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            t.rectTransform.anchorMin = new Vector2(0, 1); t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.rectTransform.offsetMin = new Vector2(0, -40); t.rectTransform.offsetMax = new Vector2(0, 0);
            _y = 44f;
            return t;
        }

        float Width => _content.rect.width > 10f ? _content.rect.width : 730f;

        Text Para(string text, int size = 18, Color? color = null, float gapAfter = 10f)
        {
            var t = UIKit.Label(_content, text, size, color ?? Theme.InkSoft, TextAnchor.UpperLeft);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.lineSpacing = 1.08f;
            var settings = t.GetGenerationSettings(new Vector2(Width, 0f));
            float h = t.cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) / t.pixelsPerUnit + 4f;
            t.rectTransform.anchorMin = new Vector2(0, 1); t.rectTransform.anchorMax = new Vector2(1, 1);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.rectTransform.offsetMin = new Vector2(0, -_y - h); t.rectTransform.offsetMax = new Vector2(0, -_y);
            _y += h + gapAfter;
            return t;
        }

        RectTransform Block(float h)
        {
            var node = UIKit.Node("block", _content);
            node.anchorMin = new Vector2(0, 1); node.anchorMax = new Vector2(1, 1);
            node.pivot = new Vector2(0.5f, 1);
            node.offsetMin = new Vector2(0, -_y - h); node.offsetMax = new Vector2(0, -_y);
            _y += h + 12f;
            return node;
        }

        /// <summary>A cream card with a picture and a caption under it.</summary>
        static RectTransform Tile(RectTransform parent, float x, float w, float h, Sprite art, float artH, string caption, string sub = null)
        {
            var cell = UIKit.Node("tile", parent);
            cell.Anchor(UIKit.TopLeft, new Vector2(x, 0), new Vector2(w, h));
            SurfaceLook.Add(cell, Looks.Row, 16f);
            if (art != null)
            {
                var im = UIKit.Img(cell, art, Color.white, "art");
                im.preserveAspect = true;
                im.rectTransform.Anchor(UIKit.Top, new Vector2(0, -8), new Vector2(w - 16, artH));
            }
            var cap = UIKit.Label(cell, caption, 18, Theme.Ink, TextAnchor.UpperCenter, FontStyle.Bold);
            cap.rectTransform.Anchor(UIKit.Top, new Vector2(0, -artH - 12), new Vector2(w - 10, 28));
            if (sub != null)
            {
                var s = UIKit.Label(cell, sub, 15, Theme.InkSoft, TextAnchor.UpperCenter);
                s.horizontalOverflow = HorizontalWrapMode.Wrap;
                s.rectTransform.Anchor(UIKit.Top, new Vector2(0, -artH - 40), new Vector2(w - 16, h - artH - 44));
            }
            return cell;
        }

        /// <summary>The thirsty bed as the farm draws it: the blue rim over the bed art and the water drop standing at the
        /// front-left of where the crop would be (IslandView.ThirstFoot). Only the picture: the animation is the farm's.</summary>
        static void Thirsty(RectTransform tile, float w, float artH)
        {
            var rim = UIKit.Img(tile, Art.Load("Art/beds/bed_glow_thirst"), Color.white, "rim");
            rim.preserveAspect = true;
            rim.raycastTarget = false;
            rim.rectTransform.Anchor(UIKit.Top, new Vector2(0, -8), new Vector2(w - 16, artH));
            // the bed art is 780 x 640 with its diamond 760 px wide: plot units → this picture's pixels
            float disp = Mathf.Min(w - 16, artH * 780f / 640f) / 780f;       // UI px per bed-art px
            float dh = 640f * disp;
            float scale = disp * 760f / IslandView.TW;                         // UI px per plot unit
            float cy = -8f - (artH - dh) * 0.5f - dh * (1f - 0.475f);          // the diamond's centre line
            var foot = new Vector2(IslandView.ThirstFoot.x * scale, cy + IslandView.ThirstFoot.y * scale);
            var drop = UIKit.Img(tile, Art.Load("Art/beds/thirst_drop"), Color.white, "drop");
            drop.preserveAspect = true;
            drop.raycastTarget = false;
            float d = Mathf.Max(26f, IslandView.ThirstCellUnits * scale);
            drop.rectTransform.anchorMin = drop.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            drop.rectTransform.pivot = new Vector2(0.5f, 0.05f);
            drop.rectTransform.sizeDelta = new Vector2(d, d);
            drop.rectTransform.anchoredPosition = foot;
        }

        static void Arrow(RectTransform parent, float x, float y)
        {
            var a = UIKit.Img(parent, Theme.Skin.ArrowRight, Theme.Hex("#B39873"), "arrow");
            a.preserveAspect = true;
            a.rectTransform.Anchor(UIKit.TopLeft, new Vector2(x, y), new Vector2(22, 22));
        }

        static void Pill(RectTransform parent, Vector2 pos, Vector2 size, string text, Color col, Vector2 anchor)
        {
            var chip = UIKit.Node("pill", parent);
            chip.Anchor(anchor, pos, size);
            var bg = UIKit.Img(chip, null, col, "bg");
            bg.rectTransform.Stretch();
            Chrome.Shape(bg, size.y / 2f);
            UIKit.Label(chip, text, 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Stretch(6, 0, 6, 1);
        }

        static string M(float v) { return "×" + Fmt.Mul(v); }

        // ============================================================
        // pages
        // ============================================================
        void Basics()
        {
            Heading("Vòng lặp nông trại");
            var row = Block(208f);
            float w = (Width - 3 * 30f) / 4f;
            var steps = new (Sprite art, string cap, string sub)[]
            {
                (Art.Bed("empty", 0),   "Gieo",       "Chạm ô đất trống, chọn hạt giống"),
                (Art.Bed("thirsty", 1), "Tưới",       "Ô có giọt nước là cây khát"),
                (Art.Icon("carrot", 0), "Thu hoạch",  "Viền vàng lấp lánh là đã chín"),
                (Theme.Skin.Coin,       "Bán",        "Menu ▸ Kho ▸ Bán sỉ để lấy xu"),
            };
            for (int i = 0; i < steps.Length; i++)
            {
                float x = i * (w + 30f);
                var tile = Tile(row, x, w, 208f, steps[i].art, 104f, steps[i].cap, steps[i].sub);
                if (i == 1) Thirsty(tile, w, 104f);
                if (i < steps.Length - 1) Arrow(row, x + w + 4f, -92f);
            }
            Para("• Xu dùng để mua hạt giống, mở thêm ô đất và nâng cấp trang trại.\n"
               + "• Thu hoạch cho kinh nghiệm (XP). Đầy thanh XP dưới tên bạn thì vào Menu ▸ Nâng cấp.\n"
               + "• Cây vẫn lớn khi bạn tắt game, quay lại là thu hoạch được ngay.\n"
               + "• Cấp " + QuickActions.HarvestLevel + " mở nút Thu hoạch nhanh, cấp " + QuickActions.WaterLevel + " mở nút Tưới nhanh.");
        }

        void Watering()
        {
            Heading("Tưới đúng cữ");
            var row = Block(190f);
            float w = (Width - 2 * 30f) / 3f;
            Thirsty(Tile(row, 0, w, 190f, Art.Bed("thirsty", 1), 110f, "Đang khát", "Có giọt nước: chạm để tưới"), w, 110f);
            Arrow(row, w + 4f, -80f);
            Tile(row, w + 30f, w, 190f, Art.Bed("watered", 1), 110f, "Đã tưới", "Đất sẫm màu, cây chín sớm hơn");
            Arrow(row, 2 * w + 34f, -80f);
            // three crops from the table itself, short to long, so a rebalance rewrites the page
            Seed shortC = GameData.Get("carrot"), midC = GameData.Get("pumpkin"), longC = GameData.Get("avocado");
            var last = Tile(row, 2 * (w + 30f), w, 190f, Art.Bed("ready", 1), 110f, "Chín sớm",
                            midC.name + ": mỗi lần sớm " + Fmt.Time(midC.waterCut));
            Pill(last, new Vector2(-10, -10), new Vector2(84, 28), "−" + Fmt.Time(midC.waterCut), Theme.GreenDeep, UIKit.TopRight);

            string Drinks(Seed sd) { return sd.name + " " + sd.waters + " lần"; }
            string Cut(Seed sd) { return sd.name + " " + Fmt.Time(sd.waterCut); }
            var drought = WeatherSys.Def(Weather.Drought);
            Para("• Mỗi loại cây khát một số lần riêng: " + Drinks(shortC) + ", " + Drinks(midC) + ", " + Drinks(longC)
               + ". Ô cứ khát như vậy cho tới cữ kế tiếp.\n"
               + "• Mỗi lần tưới, cây chín sớm một khoảng thời gian cố định: " + Cut(shortC) + ", " + Cut(midC) + ", " + Cut(longC)
               + ". Thẻ hạt giống và ô đất đang lớn đều ghi rõ.\n"
               + "• Không tưới cây vẫn lớn, chỉ chín chậm hơn. Trời " + drought.name + " mỗi lần tưới được "
               + (Mathf.Approximately(drought.waterCut, 2f) ? "gấp đôi" : "gấp " + Fmt.Mul(drought.waterCut)) + ".\n"
               + "• Cữ tới mà chưa tưới thì cữ trước mất: vắng mặt không bị phạt, nhưng cũng không được cộng dồn.\n"
               + "• Trời " + WeatherSys.Def(Weather.Rain).name + " hoặc " + WeatherSys.Def(Weather.Storm).name + ": tới cữ là trời tự tưới, không cần chạm, kể cả lúc tắt game.");
        }

        void WeatherPage()
        {
            Heading("Thời tiết đổi mỗi 15 phút");
            Para("Hiệu ứng được chốt lúc gieo: cây gieo khi trời mưa giữ hiệu ứng mưa đến lúc thu hoạch, dù trời đã tạnh.", 17);
            var grid = Block(248f);
            const float gap = 12f;
            float w = (Width - 2 * gap) / 3f, h = (248f - gap) / 2f;
            for (int i = 0; i < WeatherSys.All.Length; i++)
            {
                var d = WeatherSys.All[i];
                var cell = UIKit.Node("w", grid);
                cell.Anchor(UIKit.TopLeft, new Vector2((i % 3) * (w + gap), -(i / 3) * (h + gap)), new Vector2(w, h));
                SurfaceLook.Add(cell, Looks.Row, 16f);
                var ic = UIKit.Img(cell, Art.WeatherIcon(d.id), Theme.Hex(d.hex), "ic");
                ic.preserveAspect = true;
                ic.rectTransform.Anchor(UIKit.TopLeft, new Vector2(12, -12), new Vector2(40, 40));
                UIKit.Label(cell, d.name, 19, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold)
                     .rectTransform.Anchor(UIKit.TopLeft, new Vector2(60, -12), new Vector2(w - 70, 28));
                UIKit.Label(cell, "Chín " + M(d.grow) + " · Giá " + M(d.sell), 15, Theme.InkSoft, TextAnchor.MiddleLeft)
                     .rectTransform.Anchor(UIKit.TopLeft, new Vector2(60, -38), new Vector2(w - 70, 22));
                var note = UIKit.Label(cell, d.note, 14, Color.Lerp(Theme.Hex(d.hex), Theme.Ink, 0.55f), TextAnchor.UpperLeft);
                note.horizontalOverflow = HorizontalWrapMode.Wrap;
                note.rectTransform.Anchor(UIKit.TopLeft, new Vector2(12, -64), new Vector2(w - 24, h - 68));
            }
            Para("Chạm đĩa thời tiết ở góc trên để xem còn bao lâu nữa trời đổi.", 17);
        }

        void Bonus()
        {
            Heading("Cây bonus");
            Para("Mỗi 12 giờ (6:00 và 18:00) có " + TagSys.TaggedCount + " loại cây được gắn bonus. Xem trong đĩa thời tiết, hoặc trên thẻ hạt giống lúc gieo.", 17);
            for (int i = 1; i < TagSys.Defs.Length; i++)
            {
                var d = TagSys.Defs[i];
                var row = Block(58f);
                SurfaceLook.Add(row, Looks.Row, 16f);
                Pill(row, new Vector2(14, 0), new Vector2(150, 34), "★ " + d.name, Theme.Hex(d.hex), UIKit.Left);
                string extra = d.id == CropTag.Mutation ? ", chỉ cây Sử thi, Huyền thoại"
                             : d.id == CropTag.Season ? " (sai thời tiết: bình thường)" : "";
                var lab = UIKit.Label(row, d.blurb + extra, 18, Theme.Ink, TextAnchor.MiddleLeft);
                lab.rectTransform.Stretch(182, 0, 12, 0);
                _y -= 4f;
            }
        }

        void Mutation()
        {
            Heading("Đột biến: cây phát sáng");
            Para("Mỗi lần gieo có cơ hội đột biến, lên cấp càng dễ. Cây đột biến chín lâu hơn một chút nhưng bán giá gấp nhiều lần, và được ghi vào Sổ sưu tập.", 17);
            var row = Block(206f);
            const float gap = 14f;
            float w = (Width - 3 * gap) / 4f;
            for (int v = 1; v < Art.Elements.Length; v++)
            {
                var el = Art.Elements[v];
                float x = (v - 1) * (w + gap);
                var cell = Tile(row, x, w, 206f, Art.Icon("carrot", v), 96f, el.name, null);
                // the tier's colour, the way the plot shows it: tinted crop over its glow
                MutationTint.Apply(cell.Find("art").GetComponent<Image>(), "carrot", v);
                var glow = UIKit.Img(cell, Theme.Glow(), el.glow.Alpha(0.85f), "glow");
                glow.rectTransform.Anchor(UIKit.Top, new Vector2(0, 8), new Vector2(150, 130));
                glow.rectTransform.SetSiblingIndex(cell.Find("art").GetSiblingIndex());
                UIKit.Label(cell, el.Grade, 15, Color.Lerp(el.glow, Theme.Ink, 0.5f), TextAnchor.MiddleCenter, FontStyle.Bold)
                     .rectTransform.Anchor(UIKit.Top, new Vector2(0, -136), new Vector2(w - 10, 22));
                UIKit.Label(cell, "Giá " + M(el.sell), 17, Theme.GreenDeep, TextAnchor.MiddleCenter, FontStyle.Bold)
                     .rectTransform.Anchor(UIKit.Top, new Vector2(0, -160), new Vector2(w - 10, 24));
                UIKit.Label(cell, el.yieldAdd > 0 ? "Thêm " + el.yieldAdd + " quả" : "Số quả như thường", 14, Theme.InkSoft, TextAnchor.MiddleCenter)
                     .rectTransform.Anchor(UIKit.Top, new Vector2(0, -182), new Vector2(w - 10, 20));
            }
            Para("Ô đất phát sáng ngay từ lúc gieo, nên bạn biết trước cây nào sắp đột biến.", 17);
        }

        void MissionsPage()
        {
            Heading("Nhiệm vụ");
            var items = new (Sprite art, string title, string text)[]
            {
                (Art.Item("medal_gold"), "Chương truyện", "Chuỗi mục tiêu cố định, hiện ngay dưới tên bạn. Xong mục này mở mục kế tiếp, thưởng lớn dần."),
                (Art.Item("medal_diamond"), "Đơn hàng", "Đơn ngắn hạn, có hạng Đồng · Bạc · Vàng · Kim Cương. Hoàn thành liên tiếp để giữ chuỗi, để đơn hết hạn là mất chuỗi."),
                (Art.Item("medal_silver"), "Hằng ngày", "Việc nhỏ làm mới mỗi ngày: thu hoạch, tưới, gieo, bán. Nhận thưởng đều đặn."),
            };
            foreach (var it in items)
            {
                var row = Block(104f);
                SurfaceLook.Add(row, Looks.Row, 16f);
                var im = UIKit.Img(row, it.art, Color.white, "art");
                im.preserveAspect = true;
                im.rectTransform.Anchor(UIKit.Left, new Vector2(16, 0), new Vector2(70, 70));
                UIKit.Label(row, it.title, 20, Theme.Ink, TextAnchor.UpperLeft, FontStyle.Bold)
                     .rectTransform.Anchor(UIKit.TopLeft, new Vector2(104, -12), new Vector2(Width - 120, 28));
                var t = UIKit.Label(row, it.text, 16, Theme.InkSoft, TextAnchor.UpperLeft);
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.rectTransform.Anchor(UIKit.TopLeft, new Vector2(104, -42), new Vector2(Width - 124, 58));
            }
            Para("Mở ở Menu ▸ Nhiệm vụ. Nút có số đỏ là đang có thưởng chờ nhận.", 17);
        }

        void Chests()
        {
            Heading("Năng lượng & rương");
            var row = Block(176f);
            const float gap = 12f;
            float w = (Width - 4 * gap) / 5f;
            Tile(row, 0, w, 176f, Art.Item("item_energy"), 90f, "Năng lượng", "Tích khi thu hoạch");
            string[] tiers = { "Thường", "Quý", "Thần kỳ", "Huyền thoại" };
            for (int t = 0; t < 4; t++)
                Tile(row, (t + 1) * (w + gap), w, 176f, Art.Item("chest_" + t), 90f, tiers[t], null);
            Para("• Mỗi lần thu hoạch cho năng lượng kỳ diệu (thanh tím ở góc trên bên phải).\n"
               + "• Đầy thanh là được một rương. Rương càng quý càng nhiều xu và hạt giống hiếm.\n"
               + "• Chạm thanh năng lượng để mở rương. Nâng cấp trang trại giúp tích năng lượng nhanh hơn.");
        }

        void Islands()
        {
            Heading("Mở rộng quần đảo");
            var row = Block(178f);
            const float gap = 12f;
            int n = Mathf.Min(IslandSys.Max - 1, 7);
            float w = (Width - (n - 1) * gap) / n;
            for (int i = 1; i <= n && i < IslandSys.Defs.Length; i++)
            {
                var d = IslandSys.Def(i);
                var cell = Tile(row, (i - 1) * (w + gap), w, 178f, Art.Load("Art/islands/island_" + d.style), Mathf.Min(84f, w - 8f), d.name, "Cấp " + d.lv);
                // seven tiles to a row: the perk is said without "toàn trang trại", or it ran to three lines
                var perk = UIKit.Label(cell, (d.perk ?? "").Replace(" toàn trang trại", ""), 13, Theme.GreenDeep, TextAnchor.UpperCenter, FontStyle.Bold);
                perk.horizontalOverflow = HorizontalWrapMode.Wrap;
                perk.rectTransform.Anchor(UIKit.Top, new Vector2(0, -142), new Vector2(w - 12, 34));
            }
            Para("• Chạm ô có ổ khoá để mua thêm ô đất, cần đủ cấp và đủ xu. Ô sắp mở ghi cấp cần có ngay dưới ổ khoá; giá xem khi chạm.\n"
               + "• Đảo Nước chỉ trồng hai bên bờ sông. Khổng Lồ có 4 ô đất lớn, chỉ trồng cây lớn (táo, cam, chuối, dừa).\n"
               + "• Đảo mới cần đủ cấp và nộp đủ nông sản cống nạp (nộp dần được). Mỗi đảo tặng sẵn ô đất "
               + "và một đặc quyền cho cả trang trại.\n"
               + "• Nút ◀ ▶ ở góc dưới bên trái để đi giữa các đảo; chạm tên đảo để xem toàn cảnh.");
        }
    }
}
