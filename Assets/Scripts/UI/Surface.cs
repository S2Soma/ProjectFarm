using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LQFarm
{
    // ======================================================================================
    // The material system.
    //
    // Every surface in the interface used to be ONE tinted plate: a translucent black-green
    // rounded rect on the HUD, a Kenney 9-slice in the panels. A single flat tint is what made
    // the HUD read as "dark glass stuck on a screenshot" — it has no top, no bottom, no edge,
    // and at 78% alpha it borrowed its hue from whatever was behind it (teal over the sea,
    // olive over grass).
    //
    // A surface is now a small stack of plain white shapes, each tinted in code:
    //
    //     shadow   soft blurred shape, offset downward
    //     lip      the dark body peeking out under a button (buttons, wells)
    //     edge     full-size dark border
    //     rim      inset by the edge: a white highlight that fades out by mid-height
    //     fill     inset by edge + rim: the vertical gradient that is the surface itself
    //
    // The shapes are baked large (Tools/gen_chrome.py, shape_16/48/128) and drawn with
    // Image.pixelsPerUnitMultiplier > 1, so a corner is always minified and sharp. Radii are
    // recomputed whenever the rect changes size, which is what lets one sprite be a true pill
    // (radius = half the height) at every height instead of an ellipse-cornered lozenge.
    // ======================================================================================

    /// <summary>What a surface is made of. Colours are final — no Theme tinting on top.</summary>
    public struct Look
    {
        public Color top, bottom;              // fill gradient
        public Color edge; public float edgeW; // full-size border layer; drawn when edge.a > 0
        public Color rim; public float rimW;   // highlight ring inside the edge
        public float rimFade;                  // share of the height the rim takes to fade out
        public float inTop;                    // extra fill inset at the top only: an inner shadow crescent
        public float lip; public Color lipColor;
        public Color shadow; public float blur; public Vector2 drop;
        public Color ink, inkLine;              // label colour and outline (buttons)
    }

    /// <summary>Shape sprites and the multiplier that keeps their corners true.</summary>
    public static class Chrome
    {
        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        static Sprite S(string n)
        {
            if (!_cache.TryGetValue(n, out var s) || s == null) _cache[n] = s = Art.Load("Art/chrome/" + n);
            return s;
        }

        /// <summary>Radius <paramref name="r"/> in reference pixels. The source is the smallest
        /// baked shape at least twice as large, so a 1.5x-2x screen always samples down.</summary>
        public static void Shape(Image im, float r, bool topOnly = false)
        {
            if (im == null) return;
            r = Mathf.Max(0.5f, r);
            float src;
            string name;
            if (topOnly) { name = "top_48"; src = 48f; }
            else if (r * 2f <= 16f) { name = "shape_16"; src = 16f; }
            else if (r * 2f <= 48f) { name = "shape_48"; src = 48f; }
            else { name = "shape_128"; src = 128f; }
            Assign(im, S(name), src / r);
        }

        /// <summary>A drop shadow whose inner shape has radius <paramref name="r"/> and whose
        /// penumbra is about <paramref name="blur"/> wide. The rect must already be expanded by
        /// the blur on every side.</summary>
        public static void Soft(Image im, float r, float blur)
        {
            if (im == null) return;
            bool wide = blur > r * 0.75f;
            float total = Mathf.Max(1f, r + blur);
            Assign(im, S(wide ? "soft_wide" : "soft_tight"), (wide ? 128f : 96f) / total);
        }

        static void Assign(Image im, Sprite s, float mul)
        {
            if (im.sprite != s) im.sprite = s;
            if (im.type != Image.Type.Sliced) im.type = Image.Type.Sliced;
            if (Mathf.Abs(im.pixelsPerUnitMultiplier - mul) > 0.001f) im.pixelsPerUnitMultiplier = mul;
        }
    }

    /// <summary>Vertical gradient multiplied into a graphic's vertex colours.
    ///
    /// A 9-sliced image only has vertices at its slice lines, so a fade that should end at
    /// mid-height would otherwise be stretched across whichever slice happens to span it. The
    /// mesh is cut along the two gradient lines first, so the ramp is exact on any shape.</summary>
    [DisallowMultipleComponent]
    public class VGradient : BaseMeshEffect
    {
        public Color top = Color.white, bottom = Color.white;
        /// <summary>0 is the bottom of the rect, 1 the top. The bottom colour holds below
        /// <see cref="from"/>, the top colour above <see cref="to"/>.</summary>
        public float from = 0f, to = 1f;

        public void Set(Color t, Color b, float f = 0f, float tt = 1f)
        {
            top = t; bottom = b; from = f; to = tt;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        static readonly List<UIVertex> _tris = new List<UIVertex>(64);
        static readonly List<UIVertex> _cut = new List<UIVertex>(96);
        static readonly List<UIVertex> _hi = new List<UIVertex>(6), _lo = new List<UIVertex>(6);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var rect = graphic.rectTransform.rect;
            float h = Mathf.Max(0.0001f, rect.height);
            float yFrom = rect.yMin + Mathf.Clamp01(from) * h;
            float yTo = rect.yMin + Mathf.Clamp01(to) * h;

            _tris.Clear();
            vh.GetUIVertexStream(_tris);
            if (from > 0.001f) Cut(yFrom);
            if (to < 0.999f && Mathf.Abs(yTo - yFrom) > 0.01f) Cut(yTo);

            float span = yTo - yFrom;
            for (int i = 0; i < _tris.Count; i++)
            {
                var v = _tris[i];
                float t = span > 0.0001f ? Mathf.Clamp01((v.position.y - yFrom) / span) : (v.position.y >= yTo ? 1f : 0f);
                v.color = (Color32)((Color)v.color * Color.Lerp(bottom, top, t));
                _tris[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(_tris);
        }

        /// <summary>Clip every triangle against the line y = <paramref name="y"/> into the part
        /// above and the part below, each fanned back into triangles. Winding is preserved.</summary>
        static void Cut(float y)
        {
            _cut.Clear();
            for (int i = 0; i + 2 < _tris.Count; i += 3)
            {
                _hi.Clear(); _lo.Clear();
                for (int k = 0; k < 3; k++)
                {
                    var cur = _tris[i + k];
                    var nxt = _tris[i + (k + 1) % 3];
                    float dc = cur.position.y - y, dn = nxt.position.y - y;
                    if (dc >= 0f) _hi.Add(cur);
                    if (dc <= 0f) _lo.Add(cur);
                    if ((dc > 0f && dn < 0f) || (dc < 0f && dn > 0f))
                    {
                        var p = Lerp(cur, nxt, dc / (dc - dn));
                        _hi.Add(p); _lo.Add(p);
                    }
                }
                Fan(_hi);
                Fan(_lo);
            }
            _tris.Clear();
            _tris.AddRange(_cut);
        }

        static void Fan(List<UIVertex> poly)
        {
            for (int j = 1; j + 1 < poly.Count; j++)
            {
                _cut.Add(poly[0]); _cut.Add(poly[j]); _cut.Add(poly[j + 1]);
            }
        }

        static UIVertex Lerp(UIVertex a, UIVertex b, float t)
        {
            var v = a;
            v.position = Vector3.Lerp(a.position, b.position, t);
            v.normal = Vector3.Lerp(a.normal, b.normal, t);
            v.tangent = Vector4.Lerp(a.tangent, b.tangent, t);
            v.color = Color32.Lerp(a.color, b.color, t);
            v.uv0 = Vector4.Lerp(a.uv0, b.uv0, t);
            v.uv1 = Vector4.Lerp(a.uv1, b.uv1, t);
            v.uv2 = Vector4.Lerp(a.uv2, b.uv2, t);
            v.uv3 = Vector4.Lerp(a.uv3, b.uv3, t);
            return v;
        }
    }

    /// <summary>Owns a surface's layer stack and keeps its radii right as the rect changes.</summary>
    public class SurfaceLook : UIBehaviour
    {
        /// <summary>Radius modes. A fixed radius is any value ≥ 0.</summary>
        public const float Pill = -1f;
        /// <summary>The button rule: 18 once the face is 60 or taller, a pill below that.</summary>
        public const float Auto = -2f;

        public Look look;
        public float radius = Pill;
        /// <summary>Round only the top corners (a ribbon docked to the top of a card).</summary>
        public bool topOnly;

        Image _sh, _lip, _edge, _rim, _fill;
        VGradient _fillG, _rimG;
        RectTransform _face;
        float _sink;
        Vector2 _lastSize = new Vector2(-1, -1);

        public Image Fill => _fill;
        /// <summary>Where content goes. For a button this is the part that sinks when pressed;
        /// for everything else it is the host itself.</summary>
        public RectTransform Face => _face != null ? _face : (RectTransform)transform;

        /// <summary>Build the stack under <paramref name="host"/>. The layers are inserted as
        /// the host's FIRST children, so anything already parented to it stays on top.</summary>
        public static SurfaceLook Add(RectTransform host, Look look, float radius = Pill,
                                      bool pressable = false, bool topOnly = false)
        {
            var s = host.gameObject.AddComponent<SurfaceLook>();
            s.look = look;
            s.radius = radius;
            s.topOnly = topOnly;
            s.Build(pressable);
            return s;
        }

        void Build(bool pressable)
        {
            var host = (RectTransform)transform;
            int idx = 0;

            if (look.shadow.a > 0f && look.blur > 0f)
                _sh = Layer(host, "sh", look.shadow, idx++);
            if (look.lip > 0f)
                _lip = Layer(host, "lip", look.lipColor, idx++);

            Transform parent = host;
            if (pressable)
            {
                _face = UIKit.Node("face", host);
                _face.SetSiblingIndex(idx++);
                parent = _face;
            }
            int j = pressable ? 0 : idx;

            if (look.edge.a > 0f)
                _edge = Layer(parent, "edge", look.edge, j++);
            if (look.rimW > 0f && look.rim.a > 0f)
            {
                _rim = Layer(parent, "rim", Color.white, j++);
                _rimG = _rim.gameObject.AddComponent<VGradient>();
            }
            _fill = Layer(parent, "fill", Color.white, j++);
            _fillG = _fill.gameObject.AddComponent<VGradient>();

            Recolour();
            Layout(true);
        }

        static Image Layer(Transform parent, string name, Color c, int index)
        {
            var im = UIKit.Img(parent, null, c, name);
            im.type = Image.Type.Sliced;
            im.rectTransform.SetSiblingIndex(index);
            return im;
        }

        /// <summary>Make the parts that are always under the finger catch raycasts: the lip does not
        /// move when the face sinks, and the edge covers the face's full outline.</summary>
        public void RaycastBody()
        {
            if (_lip != null) _lip.raycastTarget = true;
            if (_edge != null) _edge.raycastTarget = true;
            _fill.raycastTarget = true;
        }

        /// <summary>Swap the whole palette — a button turning disabled, a tab selected.</summary>
        public void SetLook(Look l)
        {
            bool geometry = l.edgeW != look.edgeW || l.rimW != look.rimW || l.lip != look.lip || l.inTop != look.inTop;
            look = l;
            Recolour();
            if (geometry) Layout(true);
        }

        void Recolour()
        {
            if (_sh != null) _sh.color = look.shadow;
            if (_lip != null) _lip.color = look.lipColor;
            if (_edge != null) _edge.color = look.edge;
            if (_rimG != null) _rimG.Set(look.rim, look.rim.Alpha(0f), 1f - Mathf.Clamp01(look.rimFade <= 0 ? 0.5f : look.rimFade), 1f);
            if (_fillG != null) _fillG.Set(look.top, look.bottom);
        }

        /// <summary>Sink the face into its lip. Only surfaces built pressable have one.</summary>
        public void SetPressed(bool down)
        {
            if (_face == null) return;
            float sink = down ? Mathf.Max(0f, look.lip - 2f) : 0f;
            if (Mathf.Approximately(sink, _sink)) return;
            _sink = sink;
            Layout(true);
        }

        protected override void OnEnable() { base.OnEnable(); Layout(false); }
        protected override void OnRectTransformDimensionsChange() { Layout(false); }

        public void Layout(bool force)
        {
            // AddComponent runs OnEnable before Build has made the layers.
            if (_fill == null) return;
            var host = (RectTransform)transform;
            var size = host.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            if (!force && size == _lastSize) return;
            _lastSize = size;

            float bodyH = size.y - look.lip;
            float R;
            if (radius >= 0f) R = radius;
            else if (radius == Auto) R = bodyH >= 60f ? 18f : Mathf.Min(size.x, bodyH) * 0.5f;
            else R = Mathf.Min(size.x, bodyH) * 0.5f;
            R = Mathf.Min(R, Mathf.Min(size.x, bodyH) * 0.5f);

            if (_sh != null)
            {
                var rt = _sh.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(-look.blur + look.drop.x, -look.blur + look.drop.y);
                rt.offsetMax = new Vector2(look.blur + look.drop.x, look.blur + look.drop.y);
                Chrome.Soft(_sh, R, look.blur);
            }

            if (_lip != null)
            {
                _lip.rectTransform.Stretch(0, look.lip, 0, 0);
                Chrome.Shape(_lip, R, topOnly);
            }

            // The pressable face is the body; without one, the body is the host minus the lip.
            const float bodyTop = 0f;
            float bodyBot = look.lip;
            if (_face != null)
            {
                _face.Stretch(0, _sink, 0, look.lip - _sink);
                bodyBot = 0f;
            }

            if (_edge != null)
            {
                _edge.rectTransform.Stretch(0, bodyTop, 0, bodyBot);
                Chrome.Shape(_edge, R, topOnly);
            }

            float e = look.edgeW;
            if (_rim != null)
            {
                _rim.rectTransform.Stretch(e, bodyTop + e, e, bodyBot + e);
                Chrome.Shape(_rim, R - e, topOnly);
            }

            float f = e + (_rim != null ? look.rimW : 0f);
            _fill.rectTransform.Stretch(f, bodyTop + f + look.inTop, f, bodyBot + f);
            Chrome.Shape(_fill, R - f, topOnly);
        }
    }

    /// <summary>A Button that drives its SurfaceLook: the face sinks into the lip while held
    /// and the palette swaps to the disabled one when it is not interactable. The stock colour
    /// tint could only multiply the whole button grey, which read as broken rather than off.</summary>
    public class SkinButton : Button
    {
        public SurfaceLook surface;
        public Look normal, off;
        public Text label;
        /// <summary>A glyph to fade while disabled (icon buttons have no label).</summary>
        public Image icon;
        /// <summary>Optional palettes for pointer-over and held. Lipless surfaces (the HUD action
        /// pills) have no face to sink, so "held" has to be a colour.</summary>
        public Look? hover, down;
        int _shown = -1;

        public void Bind(SurfaceLook s, Look on, Look disabled, Text lbl, Look? hoverLook = null, Look? downLook = null)
        {
            surface = s; normal = on; off = disabled; label = lbl; hover = hoverLook; down = downLook;
            _shown = -1;
            DoStateTransition(currentSelectionState, true);
        }

        /// <summary>Change the enabled palette (Restyle). The disabled palette stays.</summary>
        public void SetNormal(Look on)
        {
            normal = on;
            _shown = -1;
            DoStateTransition(currentSelectionState, true);
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (surface == null) return;
            surface.SetPressed(state == SelectionState.Pressed);

            // 0 normal, 1 hover, 2 held, 3 disabled. "Selected" (what a click leaves behind on
            // desktop) reads as normal, or the last button clicked would stay lit.
            int key = 0;
            if (state == SelectionState.Disabled) key = 3;
            else if (state == SelectionState.Pressed && down.HasValue) key = 2;
            else if (state == SelectionState.Highlighted && hover.HasValue) key = 1;
            if (key == _shown) return;
            _shown = key;

            var l = key == 3 ? off : key == 2 ? down.Value : key == 1 ? hover.Value : normal;
            surface.SetLook(l);
            if (icon != null) icon.color = icon.color.Alpha(key == 3 ? 0.4f : 1f);
            if (label != null)
            {
                label.color = l.ink;
                var o = label.GetComponent<Outline>();
                if (o != null)
                {
                    o.enabled = l.inkLine.a > 0f;
                    o.effectColor = l.inkLine;
                }
            }
        }
    }

    /// <summary>Drives a radial progress image from the weather clock every frame, so the ring
    /// round the HUD weather icon moves continuously instead of stepping once per HUD render.</summary>
    [RequireComponent(typeof(Image))]
    public class WeatherRing : MonoBehaviour
    {
        Image _im;
        void Awake() { _im = GetComponent<Image>(); }

        void Update()
        {
            if (_im == null) return;
            float left = WeatherSys.MsLeft(GS.Now) / (float)WeatherSys.SlotMs;
            _im.fillAmount = Mathf.Clamp01(left);
        }
    }

    /// <summary>Makes a progress bar's fill a LENGTH, not a crop.
    ///
    /// Image.Type.Filled ignores 9-slicing, so every bar in the game had square-cut ends that
    /// looked like a glitch next to its round track. The fill is now a sliced pill whose width
    /// follows <c>Image.fillAmount</c> — which callers already set — never narrower than it is
    /// tall (a sliver would pinch into an ellipse), and hidden below 3%.</summary>
    [RequireComponent(typeof(Image))]
    public class BarDriver : MonoBehaviour
    {
        public float pad = 2f;
        Image _im;
        float _lastAmt = -1f, _lastW = -1f;

        void Awake() { _im = GetComponent<Image>(); }

        void LateUpdate()
        {
            if (_im == null) return;
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            float amt = Mathf.Clamp01(_im.fillAmount);
            float w = parent.rect.width;
            if (Mathf.Approximately(amt, _lastAmt) && Mathf.Approximately(w, _lastW)) return;
            _lastAmt = amt; _lastW = w;

            float inner = Mathf.Max(0f, w - pad * 2f);
            float h = Mathf.Max(0f, parent.rect.height - pad * 2f);
            // Zero width rather than disabling or culling: callers keep writing colour and
            // fillAmount to it, and a RectMask2D in a scroll list rewrites the cull flag itself.
            float fw = amt < 0.03f ? 0f : Mathf.Clamp(inner * amt, h, inner);

            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(pad + fw, -pad);
            Chrome.Shape(_im, h * 0.5f);
        }
    }
}
