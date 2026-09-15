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
            // A true radius at any size (see Chrome.Shape) — the old quantised 1x plates were
            // softened by every 1.5x screen and snapped 20 and 22 to the same 18.
            var im = Img(parent, null, color, name);
            Chrome.Shape(im, radius);
            return im;
        }

        /// <summary>A rounded card with a soft drop shadow behind it.</summary>
        public static Image Card(Transform parent, Color color, int radius = 24, string name = "card",
                                 float shadow = 0.30f, float drop = 8f)
        {
            var holder = Node(name, parent);
            return SurfaceLook.Add(holder, Looks.Paper, radius).Fill;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
                                 TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Node("label", parent);
            var t = rt.gameObject.AddComponent<Text>();
            // The weight lives in the font file, never in fontStyle — see Theme.FontFor.
            t.font = Theme.FontFor(style, size);
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        /// <summary>White lettering on glass or artwork, with an outline in the surface's own dark
        /// tone (<paramref name="line"/>, default the HUD glass ink).
        ///
        /// It was a 60% black drop shadow. On a heavy rounded face a black shadow reads as dirt
        /// under the letters; an outline in the colour of what the text sits on reads as the
        /// letters being cut out of it. Outline costs five copies of the glyph mesh against
        /// Shadow's two, which the HUD's few dozen short labels can afford.</summary>
        public static Text LabelOutlined(Transform parent, string text, int size, Color color,
                                         TextAnchor anchor = TextAnchor.MiddleCenter, Color? line = null)
        {
            var t = Label(parent, text, size, color, anchor, FontStyle.Bold);
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = line ?? Theme.GlassInk.Alpha(0.92f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        // ---------------- buttons ----------------
        /// <summary>M6 button: gradient face, white rim, dark edge and a 6 px lip the face sinks
        /// into while held. <paramref name="lip"/> and <paramref name="radius"/> are kept for the
        /// call sites and ignored — the tone decides the dark colour, and the radius follows the
        /// button rule (18 once the face is 60 tall, a pill below that) so no two buttons on a
        /// screen disagree about their corners.</summary>
        public static Button Btn(Transform parent, string text, Color face, Color lip, int size = 26,
                                 int radius = 18, Action onClick = null)
        {
            var root = Node("btn", parent);
            var look = Looks.Button(Theme.Skin.ToneOf(face));
            var surf = SurfaceLook.Add(root, look, SurfaceLook.Auto, pressable: true);
            surf.RaycastBody();

            // Capped at 26: the old 28 px labels ran into the lip on 58 px buttons.
            var label = Label(surf.Face, text, Mathf.Min(size, 26), look.ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.rectTransform.Stretch(14, 0, 14, 2);
            var o = label.gameObject.AddComponent<Outline>();
            o.effectColor = look.inkLine;
            o.effectDistance = new Vector2(1.5f, -1.5f);

            var b = root.gameObject.AddComponent<SkinButton>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = surf.Fill;
            b.Bind(surf, look, Looks.BtnOff, label);
            b.onClick.AddListener(() => Sfx.Play(SfxId.Tap));
            if (onClick != null) b.onClick.AddListener(() => onClick());
            root.gameObject.AddComponent<PressFx>();
            return b;
        }

        /// <summary>Every button face now carries white lettering; only the cream grey does not.</summary>
        public static Color InkFor(Color face)
        {
            return Theme.Skin.ToneOf(face) == Theme.Tone.Grey ? Theme.Ink : Color.white;
        }

        /// <summary>Swaps a button to another tone — used when a button turns available or not.
        /// The grey tone IS the unavailable look, so a call site that restyles to Cream3 with
        /// soft ink gets the same face as a disabled button; <paramref name="labelColor"/> is
        /// ignored because every face is lettered white with an outline in its own dark.</summary>
        public static void Restyle(Button b, Color tone, Color? labelColor = null)
        {
            if (b == null) return;
            if (b is SkinButton sb) { sb.SetNormal(Looks.Button(Theme.Skin.ToneOf(tone))); return; }
            var lab = BtnLabel(b);
            if (lab != null) lab.color = labelColor ?? InkFor(tone);
        }

        public static Text BtnLabel(Button b)
        {
            return b.GetComponentInChildren<Text>();
        }

        /// <summary>Circular icon button (HUD rails, close buttons).</summary>
        /// <param name="iconTint">Defaults to white. The grey face is nearly white in its top
        /// half, so a white glyph on it loses its upper half entirely — a 2x2 "more" icon read as
        /// a domino. Anything drawn on grey passes dark ink here.</param>
        public static Button IconBtn(Transform parent, Sprite icon, Color face, float iconScale = 0.62f,
                                     Action onClick = null, string caption = null, Color? iconTint = null)
        {
            // The same M6 material as the text buttons, as a disc: gradient face, white rim, dark
            // edge and a lip the face sinks into. It was the Kenney round skin, the last surface in
            // the game that did not belong to the material system.
            var root = Node("iconbtn", parent);
            var tone = Theme.Skin.ToneOf(face);
            var look = tone == Theme.Tone.Grey ? Looks.BtnCream : Looks.Button(tone);
            look.lip = 4f;
            var surf = SurfaceLook.Add(root, look, SurfaceLook.Pill, pressable: true);
            surf.RaycastBody();
            var circle = surf.Fill;

            Image iconImg = null;
            if (icon != null)
            {
                var ic = Img(surf.Face, icon, iconTint ?? Color.white, "icon");
                iconImg = ic;
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
                plate.Anchor(Bottom, new Vector2(0, -20), new Vector2(112, 28));
                SurfaceLook.Add(plate, Looks.Glass);
                var cap = Label(plate, caption, 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                cap.rectTransform.Stretch(4, 0, 4, 0);
            }

            var b = root.gameObject.AddComponent<SkinButton>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = circle;
            // disabled keeps the face's own colour and fades the glyph: an arrow at the end of
            // the island row should look unavailable, not like a different button
            var off = look;
            off.top = Color.Lerp(look.top, Looks.BtnOff.top, 0.6f);
            off.bottom = Color.Lerp(look.bottom, Looks.BtnOff.bottom, 0.6f);
            b.Bind(surf, look, off, null);
            b.icon = iconImg;
            b.onClick.AddListener(() => Sfx.Play(SfxId.Tap));
            if (onClick != null) b.onClick.AddListener(() => onClick());
            root.gameObject.AddComponent<PressFx>();
            return b;
        }

        /// <summary>A one-line text field in a recessed well: dark ink, grey placeholder. The caller
        /// places the returned field's RectTransform. <paramref name="onSubmit"/> runs on Enter /
        /// the mobile keyboard's Done, not when the field merely loses focus.</summary>
        public static InputField TextField(Transform parent, string placeholder, InputField.ContentType type,
                                           int size = 22, Action onSubmit = null)
        {
            var box = Node("field", parent);
            SurfaceLook.Add(box, Looks.Well, 18f);
            var hit = box.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);

            var text = Label(box, "", size, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            text.supportRichText = false;
            text.rectTransform.Stretch(20, 0, 20, 2);
            var ph = Label(box, placeholder, size - 2, Theme.InkSoft.Alpha(0.55f), TextAnchor.MiddleLeft);
            ph.rectTransform.Stretch(20, 0, 20, 2);

            var f = box.gameObject.AddComponent<InputField>();
            f.targetGraphic = hit;
            f.textComponent = text;
            f.placeholder = ph;
            f.characterLimit = 120;
            f.lineType = InputField.LineType.SingleLine;
            f.contentType = type;
            f.caretColor = Theme.Ink;
            f.customCaretColor = true;
            f.caretWidth = 2;
            f.selectionColor = Theme.Green.Alpha(0.35f);
            f.shouldHideMobileInput = false;
            if (onSubmit != null)
                f.onEndEdit.AddListener(_ =>
                {
#if ENABLE_INPUT_SYSTEM
                    var kb = UnityEngine.InputSystem.Keyboard.current;
                    if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) { onSubmit(); return; }
#endif
                    if (f.touchScreenKeyboard != null && f.touchScreenKeyboard.status == TouchScreenKeyboard.Status.Done) onSubmit();
                });
            return f;
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
        static bool Near(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.03f && Mathf.Abs(a.g - b.g) < 0.03f && Mathf.Abs(a.b - b.b) < 0.03f;
        }

        /// <summary>A recessed pill track with a gradient pill fill. Callers drive it by setting
        /// <c>fillAmount</c> on the returned image, as before; <see cref="BarDriver"/> turns that
        /// into a LENGTH so both ends stay round (Filled images ignore 9-slicing).</summary>
        public static Image Bar(Transform parent, Color track, Color fill, int radius = 10)
        {
            var root = Node("bar", parent);
            // TrackGlass (white 22%) was a pale stripe that washed the fill out on the HUD; a bar
            // on glass is a dark groove now. Cream panels keep the brown track they pass in.
            bool onGlass = track.r > 0.9f && track.g > 0.9f && track.a < 0.5f;
            Color tTop = onGlass ? new Color(0f, 0.05f, 0.05f, 0.42f) : track;
            Color tBot = onGlass ? new Color(0f, 0.05f, 0.05f, 0.30f) : track.Alpha(track.a * 0.75f);
            SurfaceLook.Add(root, new Look
            {
                top = tTop, bottom = tBot,
                edge = new Color(0f, 0f, 0f, onGlass ? 0.30f : 0.22f), edgeW = 0f, inTop = 1.5f,
            });

            // Green gets the designer's own ramp; any other colour keeps its hue and darkens
            // toward the bottom, with the top 40% held flat and bright — the gloss.
            bool green = Near(fill, Theme.Green);
            var f = Img(root, null, green ? Theme.Hex("#79E08D") : fill, "fill");
            f.type = Image.Type.Sliced;
            f.gameObject.AddComponent<VGradient>().Set(Color.white, new Color(0.70f, 0.72f, 0.70f, 1f), 0f, 0.6f);
            f.fillAmount = 0.5f;
            f.gameObject.AddComponent<BarDriver>();
            return f;
        }

        /// <summary>Pill-shaped HUD chip: icon + value, with an optional trailing "+" button.</summary>
        public static Text Chip(Transform parent, Sprite icon, string value, Color face, float width = 180f,
                                Action onPlus = null)
        {
            var root = Node("chip", parent);
            root.sizeDelta = new Vector2(width, 52);
            // M2. The old "stroke" was a SOLID white 22% plate laid over the whole chip, not a
            // ring — it is why the coin chip was grey (#84929C) next to the teal season bar.
            SurfaceLook.Add(root, Looks.Glass);

            // The icon overhangs the left cap by 6 px and is concentric with it, so it reads as a
            // token pinned to the pill rather than a glyph printed inside it.
            if (icon != null)
            {
                var holder = Node("ic", root);
                holder.Anchor(Left, new Vector2(-6, 0), new Vector2(56, 56));
                var ic = Img(holder, icon, Color.white, "icon");
                ic.preserveAspect = true;
                ic.rectTransform.Stretch(2, 2, 2, 2);
            }

            var t = LabelOutlined(root, value, 24, Color.white, TextAnchor.MiddleRight);
            t.rectTransform.Stretch(icon != null ? 56 : 18, 0, onPlus != null ? 54 : 18, 0);

            if (onPlus != null)
            {
                // 44 inside a 52 pill, 4 px from the end: concentric with the right cap.
                var plus = Node("plus", root);
                plus.Anchor(Right, new Vector2(-4, 0), new Vector2(44, 44));
                var pb = PlusBtn(plus, onPlus);
                pb.GetComponent<RectTransform>().Stretch();
            }
            return t;
        }

        /// <summary>The small round "+" that tops up a currency: a lipless M6 green disc.</summary>
        public static Button PlusBtn(Transform parent, Action onClick)
        {
            var root = Node("plusBtn", parent);
            var look = Looks.BtnGreen;
            look.lip = 3f;
            look.lipColor = Theme.Hex("#1C7439");
            look.shadow = Color.clear;
            var surf = SurfaceLook.Add(root, look, SurfaceLook.Pill, pressable: true);
            surf.RaycastBody();
            var lab = LabelOutlined(surf.Face, "+", 30, Color.white, TextAnchor.MiddleCenter, look.inkLine);
            lab.rectTransform.Stretch(0, 0, 0, 3);
            var b = root.gameObject.AddComponent<SkinButton>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = surf.Fill;
            b.Bind(surf, look, Looks.BtnOff, null);
            if (onClick != null) b.onClick.AddListener(() => onClick());
            root.gameObject.AddComponent<PressFx>();
            return b;
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
            // A segmented control: one recessed M5 track, the selected segment a raised green
            // pill inside it. Separate cream lozenges with gaps read as three unrelated buttons.
            const float pad = 4f;
            var root = Node("tabs", parent);
            root.sizeDelta = new Vector2(names.Length * w + (names.Length - 1) * gap, h);
            SurfaceLook.Add(root, Looks.Well);
            var faces = new List<GameObject>();
            var labels = new List<Text>();
            float seg = (root.sizeDelta.x - pad * 2f) / names.Length;

            var pillLook = Looks.BtnGreen;
            pillLook.lip = 3f;
            pillLook.shadow = new Color(0f, 0f, 0f, 0.18f);
            pillLook.blur = 4f;
            pillLook.drop = new Vector2(0f, -1f);

            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var cell = Node("tab", root);
                // Anchor(Left) makes x the LEFT edge. The old "+ w / 2" pushed every tab row half
                // a tab to the right of where its caller centred it.
                cell.Anchor(Left, new Vector2(pad + i * seg, 0), new Vector2(seg, h - pad * 2f));

                var faceNode = Node("face", cell);
                faceNode.Stretch();
                SurfaceLook.Add(faceNode, pillLook, SurfaceLook.Pill);

                var hit = cell.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                var lab = Label(cell, names[i], Mathf.Min(22, (int)h / 2), Theme.InkSoft, TextAnchor.MiddleCenter, FontStyle.Bold);
                lab.rectTransform.Stretch(4, 0, 4, 3);
                var o = lab.gameObject.AddComponent<Outline>();
                o.effectColor = Theme.Hex("#1C7439");
                o.effectDistance = new Vector2(1.5f, -1.5f);

                var b = cell.gameObject.AddComponent<Button>();
                b.targetGraphic = hit;
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { Sfx.Play(SfxId.Tab); onPick(idx); });
                cell.gameObject.AddComponent<PressFx>();
                faces.Add(faceNode.gameObject);
                labels.Add(lab);
            }

            Action<int> setActive = sel =>
            {
                for (int i = 0; i < faces.Count; i++)
                {
                    bool on = i == sel;
                    faces[i].SetActive(on);
                    labels[i].color = on ? Color.white : Theme.Hex("#8A7152");
                    labels[i].GetComponent<Outline>().enabled = on;
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
