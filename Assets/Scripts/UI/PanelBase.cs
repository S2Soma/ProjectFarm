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
        /// <summary>A soft inset well — the sunken area lists and grids sit in.</summary>
        protected static RectTransform Well(Transform parent, float top = 0, float bottom = 0)
        {
            var well = UIKit.Img(parent, Theme.Skin.Inset, Color.white, "well");
            well.type = Image.Type.Sliced;
            well.rectTransform.Stretch(0, top, 0, bottom);
            return well.rectTransform;
        }

        /// <summary>An item cell with a rarity-tinted frame, artwork and a count badge.</summary>
        protected static RectTransform ItemCell(Transform parent, Sprite art, Color tint, int rarity,
                                                string count = null, string caption = null, bool dim = false,
                                                Action onClick = null)
        {
            var cell = UIKit.Node("cell", parent);
            var face = UIKit.Round(cell, dim ? Theme.Cream3 : Theme.Cream, 16, "face");
            face.rectTransform.Stretch();
            face.raycastTarget = onClick != null;

            var frame = UIKit.Img(cell, Theme.Round(16), Theme.Rarity[Mathf.Clamp(rarity, 0, 3)].Alpha(dim ? 0.35f : 0.9f), "frame");
            frame.type = Image.Type.Sliced;
            frame.rectTransform.Stretch(-3, -3, -3, -3);
            frame.transform.SetAsFirstSibling();

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
                UIKit.Round(badge, Theme.Ink.Alpha(0.78f), 12, "bg").rectTransform.Stretch();
                UIKit.Label(badge, count, 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold)
                     .rectTransform.Stretch();
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

        /// <summary>Marks a cell as the current selection.</summary>
        protected static void MarkSelected(RectTransform cell, bool on, Color accent)
        {
            var frame = cell.Find("frame")?.GetComponent<Image>();
            if (frame == null) return;
            if (on)
            {
                frame.color = accent;
                frame.rectTransform.Stretch(-6, -6, -6, -6);
            }
        }

        /// <summary>A horizontal list row with a cream plate.</summary>
        protected static RectTransform Row(Transform parent, float height)
        {
            var row = UIKit.Node("row", parent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            var bg = UIKit.Img(row, Theme.Skin.PanelLight, Color.white, "bg");
            bg.type = Image.Type.Sliced;
            bg.rectTransform.Stretch();
            return row;
        }

        /// <summary>Small reward chip: icon + amount.</summary>
        protected static RectTransform RewardChip(Transform parent, Sprite icon, string text, Color color)
        {
            var chip = UIKit.Node("chip", parent);
            chip.sizeDelta = new Vector2(112, 34);
            UIKit.Round(chip, color.Alpha(0.16f), 17, "bg").rectTransform.Stretch();
            if (icon != null)
            {
                var ic = UIKit.Img(chip, icon, Color.white, "ic");
                ic.preserveAspect = true;
                ic.rectTransform.Anchor(UIKit.Left, new Vector2(20, 0), new Vector2(26, 26));
            }
            UIKit.Label(chip, text, 18, color, TextAnchor.MiddleRight, FontStyle.Bold)
                 .rectTransform.Stretch(36, 0, 10, 0);
            return chip;
        }

        protected static Sprite CoinIcon => Theme.Skin.Coin;
        protected static Sprite XpIcon   => Theme.Skin.StarGold;
        protected static Sprite LeafIcon => Art.Ui("ic_leaf");
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
