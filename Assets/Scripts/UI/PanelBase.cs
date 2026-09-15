using System;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>A modal screen. The frame (scrim, card, header, close button) is drawn by
    /// GameApp; a panel only fills in its body and knows how to refresh itself.</summary>
    public abstract class PanelBase
    {
        protected readonly GameApp app;
        public RectTransform Body;      // content area inside the card
        public RectTransform Card;      // the card itself, for panels that need the full frame

        protected PanelBase(GameApp app) { this.app = app; }

        public abstract string Title { get; }
        public virtual string Subtitle => null;
        public virtual Vector2 Size => new Vector2(920, 540);
        public virtual Color Accent => Theme.GreenDeep;

        public abstract void Build();
        public virtual void Refresh() { }

        // ------------------------------------------------------------
        // shared building blocks
        // ------------------------------------------------------------
        /// <summary>M5 — the recessed area lists and grids sit in. It was the Kenney brown inset
        /// with torn-paper edges: a dark, ragged box on a cream card, the heaviest thing on every
        /// panel although it holds nothing but the rows on top of it.</summary>
        protected static RectTransform Well(Transform parent, float top = 0, float bottom = 0)
        {
            var well = UIKit.Node("well", parent);
            well.Stretch(0, top, 0, bottom);
            SurfaceLook.Add(well, Looks.Well, 18f);
            return well;
        }

        /// <summary>Rows are removed from the layout NOW, not at the end of the frame: Destroy is
        /// deferred, so a refresh used to lay the new rows out below the dying ones for a frame and
        /// the list sat part-way down its well.</summary>
        protected static void ClearList(RectTransform list)
        {
            for (int i = list.childCount - 1; i >= 0; i--)
            {
                var c = list.GetChild(i);
                c.SetParent(null, false);
                UnityEngine.Object.Destroy(c.gameObject);
            }
        }

        protected static void ScrollTop(RectTransform list)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            list.anchoredPosition = new Vector2(list.anchoredPosition.x, 0f);
            var sr = list.GetComponentInParent<ScrollRect>();
            if (sr != null) { sr.StopMovement(); sr.verticalNormalizedPosition = 1f; }
        }

        /// <summary>An item cell with a rarity-tinted frame, artwork and a count badge.</summary>
        protected static RectTransform ItemCell(Transform parent, Sprite art, Color tint, int rarity,
                                                string count = null, string caption = null, bool dim = false,
                                                Action onClick = null)
        {
            var cell = UIKit.Node("cell", parent);
            // The rarity ring and the face are concentric (16 + 3 = 19), and the whole cell gets
            // a contact shadow so it sits ON the well instead of being printed into it.
            var frame = UIKit.Img(cell, null, Theme.Rarity[Mathf.Clamp(rarity, 0, 3)].Alpha(dim ? 0.35f : 0.9f), "frame");
            frame.rectTransform.Stretch(-3, -3, -3, -3);
            Chrome.Shape(frame, 19f);
            var face = UIKit.Img(cell, null, dim ? Theme.Cream3 : Theme.Cream, "face");
            face.rectTransform.Stretch();
            Chrome.Shape(face, 16f);
            face.gameObject.AddComponent<VGradient>().Set(Color.white, new Color(0.95f, 0.92f, 0.86f, 1f));
            face.raycastTarget = onClick != null;

            if (art != null)
            {
                var holder = UIKit.Node("art", cell);
                holder.Stretch(8, 8, 8, caption != null ? 22 : 8);
                var im = UIKit.Img(holder, art, dim ? tint * new Color(1, 1, 1, 0.45f) : tint, "im");
                im.preserveAspect = true;
                im.rectTransform.anchorMin = im.rectTransform.anchorMax = UIKit.Center;
                im.rectTransform.pivot = UIKit.Center;
                im.rectTransform.anchoredPosition = Vector2.zero;
                holder.gameObject.AddComponent<AspectFitChild>();
            }

            if (!string.IsNullOrEmpty(count))
            {
                var badge = UIKit.Node("n", cell);
                badge.Anchor(UIKit.BottomRight, new Vector2(-4, 4), new Vector2(44, 24));
                var bl = Looks.Glass;
                bl.shadow = Color.clear; bl.edgeW = 1.5f; bl.rimW = 1.5f;
                SurfaceLook.Add(badge, bl);
                UIKit.LabelOutlined(badge, count, 16, Color.white).rectTransform.Stretch();
            }

            if (!string.IsNullOrEmpty(caption))
            {
                var cap = UIKit.Label(cell, caption, 15, Theme.InkSoft, TextAnchor.MiddleCenter);
                cap.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, 6), new Vector2(0, 20));
                cap.rectTransform.anchorMin = new Vector2(0, 0);
                cap.rectTransform.anchorMax = new Vector2(1, 0);
                cap.rectTransform.offsetMin = new Vector2(4, 4);
                cap.rectTransform.offsetMax = new Vector2(-4, 24);
            }

            if (onClick != null)
            {
                var b = cell.gameObject.AddComponent<Button>();
                b.targetGraphic = face;
                b.onClick.AddListener(() => onClick());
                cell.gameObject.AddComponent<PressFx>();
            }
            return cell;
        }

        /// <summary>Marks a cell as the current selection: the ring thickens to 4 px in the
        /// accent colour and stays concentric (16 + 4 = 20). The old -6 px ring was drawn outside
        /// the grid cell, into the gap, and was cut off by the list's mask on the first row.</summary>
        protected static void MarkSelected(RectTransform cell, bool on, Color accent)
        {
            var frame = cell.Find("frame")?.GetComponent<Image>();
            if (frame == null) return;
            if (on)
            {
                frame.color = accent;
                frame.rectTransform.Stretch(-4, -4, -4, -4);
                Chrome.Shape(frame, 20f);
            }
        }

        /// <summary>A horizontal list row with a cream plate.</summary>
        protected static RectTransform Row(Transform parent, float height)
        {
            var row = UIKit.Node("row", parent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            // Radius 16 inside the well's 18, no border, a 2 px contact shadow. The Kenney cream
            // plate it replaces had 6 px corners and a white 1 px outline: a box in a box.
            SurfaceLook.Add(row, Looks.Row, 16f);
            return row;
        }

        /// <summary>Small reward chip: icon + amount.</summary>
        protected static RectTransform RewardChip(Transform parent, Sprite icon, string text, Color color)
        {
            var chip = UIKit.Node("chip", parent);
            chip.sizeDelta = new Vector2(112, 34);
            var bg = UIKit.Img(chip, null, color.Alpha(0.16f), "bg");
            bg.rectTransform.Stretch();
            Chrome.Shape(bg, 17f);
            if (icon != null)
            {
                var ic = UIKit.Img(chip, icon, Color.white, "ic");
                ic.preserveAspect = true;
                ic.rectTransform.Anchor(UIKit.Left, new Vector2(6, 0), new Vector2(24, 24));
            }
            // 16 px and a 32 px icon column: a five-digit reward ("32.328") ran into the coin at 18
            UIKit.Label(chip, text, 16, color, TextAnchor.MiddleRight, FontStyle.Bold)
                 .rectTransform.Stretch(32, 0, 10, 0);
            return chip;
        }

        /// <summary>A contract grade as a medal (Tools/gen_items.py), optionally captioned. It was a
        /// coloured text pill — in rows that already end in a pill-shaped button, a second pill
        /// on the left read as another thing to press.</summary>
        protected static RectTransform GradeMedal(RectTransform row, Grade g, Vector2 centre, float size, bool caption)
        {
            string[] keys = { null, "bronze", "silver", "gold", "diamond" };
            var node = UIKit.Node("grade", row);
            node.Anchor(UIKit.Left, new Vector2(centre.x - size / 2f, centre.y), new Vector2(size, size));
            string key = keys[Mathf.Clamp((int)g, 0, keys.Length - 1)];
            if (key != null)
            {
                var im = UIKit.Img(node, Art.Item("medal_" + key), Color.white, "medal");
                im.preserveAspect = true;
                im.rectTransform.Stretch();
            }
            if (caption)
            {
                var gd = MissionSys.Def(g);
                var lab = UIKit.Label(row, gd.name, 15, Color.Lerp(Theme.Hex(gd.hex), Theme.Ink, 0.45f),
                                      TextAnchor.MiddleCenter, FontStyle.Bold);
                lab.rectTransform.Anchor(UIKit.Left, new Vector2(centre.x - 55f, centre.y - size / 2f - 12f), new Vector2(110, 22));
            }
            return node;
        }

        protected static Sprite CoinIcon => Theme.Skin.Coin;
        protected static Sprite XpIcon   => Theme.Skin.StarGold;
        protected static Sprite LeafIcon => Theme.Skin.Check;
    }

    /// <summary>Fits the single child image inside this rect, preserving aspect.</summary>
    public class AspectFitChild : MonoBehaviour
    {
        void Start() { Apply(); }
        void OnRectTransformDimensionsChange() { Apply(); }

        public void Apply()
        {
            var rt = (RectTransform)transform;
            if (transform.childCount == 0) return;
            var child = (RectTransform)transform.GetChild(0);
            var img = child.GetComponent<Image>();
            if (img == null || img.sprite == null) return;
            var box = rt.rect.size;
            var s = img.sprite.rect.size;
            if (s.x <= 0 || s.y <= 0 || box.x <= 0 || box.y <= 0) return;
            float k = Mathf.Min(box.x / s.x, box.y / s.y);
            child.sizeDelta = s * k;
        }
    }
}
