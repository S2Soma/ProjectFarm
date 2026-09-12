using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>Widget factory. Every screen in the game is assembled from these.</summary>
    public static class UIKit
    {
        // ---------------- layout helpers ----------------
        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(100, 100);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static RectTransform Anchor(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Size(this RectTransform rt, float w, float h)
        {
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static readonly Vector2 TopLeft = new Vector2(0, 1);
        public static readonly Vector2 TopRight = new Vector2(1, 1);
        public static readonly Vector2 Top = new Vector2(0.5f, 1);
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 Bottom = new Vector2(0.5f, 0);
        public static readonly Vector2 BottomLeft = new Vector2(0, 0);
        public static readonly Vector2 BottomRight = new Vector2(1, 0);
        public static readonly Vector2 Left = new Vector2(0, 0.5f);
        public static readonly Vector2 Right = new Vector2(1, 0.5f);

        // ---------------- primitives ----------------
        public static Image Img(Transform parent, Sprite sprite, Color color, string name = "img")
        {
            var rt = Node(name, parent);
            var im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite;
            im.color = color;
            im.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero) im.type = Image.Type.Sliced;
            return im;
        }

        /// <summary>A solid rounded rectangle.</summary>
        public static Image Round(Transform parent, Color color, int radius = 20, string name = "round")
        {
            var im = Img(parent, Theme.Round(radius), color, name);
            im.type = Image.Type.Sliced;
            return im;
        }

        /// <summary>A rounded card with a soft drop shadow behind it.</summary>
        public static Image Card(Transform parent, Color color, int radius = 24, string name = "card",
                                 float shadow = 0.30f, float drop = 8f)
        {
            var holder = Node(name, parent);
            var sh = Img(holder, Theme.Shadow(radius, 18), new Color(0, 0, 0, shadow), "shadow");
            sh.type = Image.Type.Sliced;
            sh.rectTransform.Stretch(-14, -14 + drop, -14, -14 - drop);
            var im = Img(holder, Theme.Skin.Panel, Color.white, "bg");
            im.type = Image.Type.Sliced;
            im.rectTransform.Stretch();
            return im;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
                                 TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Node("label", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Theme.Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        /// <summary>Label lifted off artwork by a drop shadow. Deliberately a Shadow and not
        /// an Outline: Outline emits the glyph mesh five times, Shadow twice, and these
        /// labels are the most numerous thing on screen.</summary>
        public static Text LabelOutlined(Transform parent, string text, int size, Color color,
                                         TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var t = Label(parent, text, size, color, anchor, FontStyle.Bold);
            var o = t.gameObject.AddComponent<Shadow>();
            o.effectColor = new Color(0f, 0f, 0f, 0.6f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        // ---------------- buttons ----------------
        /// <summary>Chunky rounded button with a darker lip and a press bounce.</summary>
        public static Button Btn(Transform parent, string text, Color face, Color lip, int size = 26,
                                 int radius = 18, Action onClick = null)
        {
            var root = Node("btn", parent);

            var im = Img(root, Theme.Skin.Button(Theme.Skin.ToneOf(face)), Color.white, "face");
            im.type = Image.Type.Sliced;
            im.raycastTarget = true;
            im.rectTransform.Stretch();

            // the sprite carries a depth lip along the bottom, so the label sits slightly high
            var label = Label(im.transform, text, size, InkFor(face), TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.Stretch(10, 0, 10, 8);
            var sh = label.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, label.color == Color.white ? 0.45f : 0.18f);
            sh.effectDistance = new Vector2(1f, -1f);

            var b = root.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f);
            colors.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            colors.fadeDuration = 0.06f;
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            root.gameObject.AddComponent<PressFx>();
            return b;
        }

        /// <summary>White lettering vanishes on the amber and grey buttons; those get dark ink.</summary>
        public static Color InkFor(Color face)
        {
            var tone = Theme.Skin.ToneOf(face);
            return (tone == Theme.Tone.Amber || tone == Theme.Tone.Grey) ? Theme.Ink : Color.white;
        }

        /// <summary>Swaps a button to another tone — used when a button turns available or not.</summary>
        public static void Restyle(Button b, Color tone, Color? labelColor = null)
        {
            if (b == null) return;
            var face = b.transform.Find("face")?.GetComponent<Image>();
            if (face != null)
            {
                face.sprite = Theme.Skin.Button(Theme.Skin.ToneOf(tone));
                face.color = Color.white;
            }
            var lab = BtnLabel(b);
            if (lab != null) lab.color = labelColor ?? InkFor(tone);
        }

        public static Text BtnLabel(Button b)
        {
            return b.GetComponentInChildren<Text>();
        }

        /// <summary>Circular icon button (HUD rails, close buttons).</summary>
        public static Button IconBtn(Transform parent, Sprite icon, Color face, float iconScale = 0.62f,
                                     Action onClick = null, string caption = null)
        {
            var root = Node("iconbtn", parent);
            var sh = Img(root, Theme.Circle(), new Color(0, 0, 0, 0.25f), "shadow");
            sh.rectTransform.Stretch(-2, 0, -2, -6);
            var circle = Img(root, Theme.Skin.Round(Theme.Skin.ToneOf(face)), Color.white, "face");
            circle.rectTransform.Stretch();
            circle.preserveAspect = true;
            circle.raycastTarget = true;

            if (icon != null)
            {
                var ic = Img(circle.transform, icon, Color.white, "icon");
                ic.preserveAspect = true;
                ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = Center;
                ic.rectTransform.pivot = Center;
                ic.rectTransform.anchoredPosition = Vector2.zero;
                var fit = ic.gameObject.AddComponent<AspectFill>();
                fit.scale = iconScale;
            }

            if (!string.IsNullOrEmpty(caption))
            {
                // white lettering straight on the sky was unreadable; give it a dark plate
                var plate = Node("cap", root);
                plate.Anchor(Bottom, new Vector2(0, -20), new Vector2(112, 26));
                var plateBg = Round(plate, new Color(0.07f, 0.15f, 0.12f, 0.66f), 12, "bg");
                plateBg.rectTransform.Stretch();
                var cap = Label(plate, caption, 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                cap.rectTransform.Stretch(4, 0, 4, 0);
            }

            var b = root.gameObject.AddComponent<Button>();
            b.targetGraphic = circle;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            root.gameObject.AddComponent<PressFx>();
            return b;
        }

        /// <summary>Invisible full-rect button, for tapping artwork.</summary>
        public static Button HitArea(Transform parent, Action onClick)
        {
            var rt = Node("hit", parent);
            rt.Stretch();
            var im = rt.gameObject.AddComponent<Image>();
            im.color = new Color(0, 0, 0, 0);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            var c = b.colors; c.fadeDuration = 0f; b.colors = c;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return b;
        }

        // ---------------- compound widgets ----------------
        /// <summary>A rounded progress bar. Returns the fill image; drive it with SetFill.</summary>
        public static Image Bar(Transform parent, Color track, Color fill, int radius = 10)
        {
            var bg = Img(parent, Theme.Skin.BarTrack, track, "bar");
            bg.type = Image.Type.Sliced;
            var f = Img(bg.transform, Theme.Skin.BarFor(fill), Color.white, "fill");
            f.type = Image.Type.Sliced;
            f.rectTransform.anchorMin = new Vector2(0, 0);
            f.rectTransform.anchorMax = new Vector2(1, 1);
            f.rectTransform.offsetMin = new Vector2(2, 2);
            f.rectTransform.offsetMax = new Vector2(-2, -2);
            f.type = Image.Type.Filled;
            f.fillMethod = Image.FillMethod.Horizontal;
            f.fillOrigin = 0;
            f.fillAmount = 0.5f;
            return f;
        }

        /// <summary>Pill-shaped HUD chip: icon + value, with an optional trailing "+" button.</summary>
        public static Text Chip(Transform parent, Sprite icon, string value, Color face, float width = 180f,
                                Action onPlus = null)
        {
            var root = Node("chip", parent);
            root.sizeDelta = new Vector2(width, 52);
            var bg = Round(root, face, 26, "bg");
            bg.rectTransform.Stretch();
            var stroke = Img(root, Theme.Round(26), new Color(1, 1, 1, 0.22f), "stroke");
            stroke.type = Image.Type.Sliced;
            stroke.rectTransform.Stretch(2, 2, 2, 2);

            if (icon != null)
            {
                var holder = Node("ic", root);
                holder.Anchor(Left, new Vector2(26, 0), new Vector2(44, 44));
                var ic = Img(holder, icon, Color.white, "icon");
                ic.preserveAspect = true;
                ic.rectTransform.Stretch(2, 2, 2, 2);
            }

            var t = LabelOutlined(root, value, 24, Color.white, TextAnchor.MiddleRight);
            t.rectTransform.Stretch(52, 0, onPlus != null ? 46 : 16, 0);

            if (onPlus != null)
            {
                var plus = Node("plus", root);
                plus.Anchor(Right, new Vector2(-4, 0), new Vector2(40, 40));
                var pb = IconBtn(plus, null, Theme.Green, 0.6f, onPlus);
                pb.GetComponent<RectTransform>().Stretch();
                LabelOutlined(pb.transform, "+", 30, Color.white).rectTransform.Stretch(0, 0, 0, 4);
            }
            return t;
        }

        /// <summary>A vertical scroll view. Returns the content transform (already has a VerticalLayoutGroup).</summary>
        public static RectTransform ScrollList(Transform parent, float spacing = 10f, RectOffset pad = null)
        {
            var view = Node("scroll", parent);
            // an invisible raycast target so a drag that starts on empty space still scrolls
            var catcher = view.gameObject.AddComponent<Image>();
            catcher.color = new Color(0, 0, 0, 0);
            var sr = view.gameObject.AddComponent<ScrollRect>();
            var mask = view.gameObject.AddComponent<RectMask2D>();
            mask.padding = new Vector4(0, 0, 0, 0);

            var content = Node("content", view);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 0);

            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = spacing;
            vl.padding = pad ?? new RectOffset(0, 0, 0, 0);
            vl.childControlWidth = true;
            // Must be true, or LayoutElement.preferredHeight is ignored and every row
            // falls back to Node()'s default 100px — which silently broke the row heights
            // set all over the panels (mission rows, friend rows, collection blocks).
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.content = content;
            sr.viewport = view;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.elasticity = 0.08f;
            sr.scrollSensitivity = 28f;
            sr.inertia = true;
            sr.decelerationRate = 0.12f;
            return content;
        }

        /// <summary>A scrollable grid. Returns the content transform (GridLayoutGroup attached).</summary>
        public static RectTransform ScrollGrid(Transform parent, Vector2 cell, Vector2 spacing, RectOffset pad = null)
        {
            var content = ScrollList(parent, 0f, pad);
            UnityEngine.Object.DestroyImmediate(content.GetComponent<VerticalLayoutGroup>());
            var g = content.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = spacing;
            g.padding = pad ?? new RectOffset(0, 0, 0, 0);
            g.childAlignment = TextAnchor.UpperCenter;
            g.constraint = GridLayoutGroup.Constraint.Flexible;
            return content;
        }

        /// <summary>Row of pill tabs. Calls back with the index tapped; returns a setter for the active tab.</summary>
        public static Action<int> Tabs(Transform parent, string[] names, Action<int> onPick,
                                       float w = 150f, float h = 46f, float gap = 10f)
        {
            var root = Node("tabs", parent);
            root.sizeDelta = new Vector2(names.Length * w + (names.Length - 1) * gap, h);
            var faces = new List<Image>();
            var labels = new List<Text>();

            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var cell = Node("tab", root);
                cell.anchorMin = cell.anchorMax = cell.pivot = Left;
                cell.anchoredPosition = new Vector2(i * (w + gap) + w / 2f, 0);
                cell.sizeDelta = new Vector2(w, h);

                var face = Round(cell, Theme.Cream3, 22, "face");
                face.rectTransform.Stretch();
                face.raycastTarget = true;
                var lab = Label(cell, names[i], 22, Theme.InkSoft, TextAnchor.MiddleCenter, FontStyle.Bold);
                lab.rectTransform.Stretch();

                var b = cell.gameObject.AddComponent<Button>();
                b.targetGraphic = face;
                b.onClick.AddListener(() => onPick(idx));
                cell.gameObject.AddComponent<PressFx>();
                faces.Add(face);
                labels.Add(lab);
            }

            Action<int> setActive = sel =>
            {
                for (int i = 0; i < faces.Count; i++)
                {
                    bool on = i == sel;
                    faces[i].color = on ? Theme.GreenDeep : Theme.Cream3;
                    labels[i].color = on ? Color.white : Theme.InkSoft;
                }
            };
            setActive(0);
            return setActive;
        }
    }

    /// <summary>Scales its Image to fit the parent, preserving aspect.</summary>
    public class AspectFill : MonoBehaviour
    {
        public float scale = 1f;
        Image _img;
        RectTransform _rt, _parent;

        void Start() { Apply(); }
        void OnEnable() { Apply(); }

        public void Apply()
        {
            _img = _img ? _img : GetComponent<Image>();
            _rt = _rt ? _rt : (RectTransform)transform;
            _parent = _parent ? _parent : (RectTransform)transform.parent;
            if (_img == null || _img.sprite == null || _parent == null) return;

            var box = _parent.rect.size * scale;
            var s = _img.sprite.rect.size;
            if (s.x <= 0 || s.y <= 0 || box.x <= 0 || box.y <= 0) return;
            float k = Mathf.Min(box.x / s.x, box.y / s.y);
            _rt.sizeDelta = s * k;
        }
    }

    /// <summary>Squash-and-bounce feedback on press.</summary>
    public class PressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        Vector3 _base = Vector3.one;
        Coroutine _co;

        void Awake() { _base = transform.localScale; }

        public void OnPointerDown(PointerEventData e) { Go(_base * 0.94f, 0.06f); }
        public void OnPointerUp(PointerEventData e)   { Go(_base, 0.12f, true); }

        void Go(Vector3 target, float time, bool bounce = false)
        {
            if (!gameObject.activeInHierarchy) return;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Run(target, time, bounce));
        }

        IEnumerator Run(Vector3 target, float time, bool bounce)
        {
            Vector3 from = transform.localScale;
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                float k = t / time;
                if (bounce) k = 1f + Mathf.Sin(k * Mathf.PI) * 0.16f * (1f - k) - (1f - k) * 0f;
                transform.localScale = Vector3.LerpUnclamped(from, target, bounce ? Mathf.Clamp01(t / time) * k : k);
                yield return null;
            }
            transform.localScale = target;
            _co = null;
        }
    }
}
