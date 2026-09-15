using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>The dimmed screen with a lit hole in it.
    ///
    /// One mesh, not four dark rectangles around the target: those meet in hard corners and
    /// read as a box drawn round the button. The hole here is a rounded rectangle with a soft
    /// edge — an inner ring of vertices at alpha 0 on the hole's rim, a second ring the feather
    /// width outside it at full dim — and the dark reaches the screen edge on rays from the hole's
    /// centre out to a circle far past the screen.
    ///
    /// Taps inside the hole fall through to whatever is under it (see
    /// <see cref="IsRaycastLocationValid"/>); taps outside are swallowed while
    /// <see cref="block"/> is on, so a step that asks for one tap cannot be derailed by another.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class Spotlight : MaskableGraphic, ICanvasRaycastFilter
    {
        public bool hasHole;
        /// <summary>Hole in this graphic's local space.</summary>
        public Rect hole;
        public float radius = 24f;
        public float feather = 22f;
        public bool block = true;
        /// <summary>A soft light ring round the hole's rim. At night the farm under the dim is
        /// nearly as dark as the dim itself, and a hole with nothing round it disappears.</summary>
        public float glow = 0.55f;
        const float GlowOut = 12f, GlowIn = 7f;

        const int CornerSteps = 8;
        const float EdgeStep = 22f;
        const float Far = 20000f;

        public void SetHole(Rect r, float rad)
        {
            hasHole = true;
            hole = r;
            radius = Mathf.Min(rad, Mathf.Min(r.width, r.height) * 0.5f);
            SetVerticesDirty();
        }

        public void ClearHole()
        {
            if (!hasHole) return;
            hasHole = false;
            SetVerticesDirty();
        }

        /// <summary>The outline of a rounded rectangle grown by <paramref name="grow"/>, sampled so
        /// that point k means the same place on the curve for every <paramref name="grow"/> — the
        /// inner and outer rings then pair up vertex for vertex.</summary>
        static void Outline(List<Vector2> pts, Rect r, float rad, float grow)
        {
            pts.Clear();
            float R = rad + grow;
            // corner centres, clockwise from top right; arcs start at 0°, 270°, 180°, 90° going clockwise
            var centres = new[]
            {
                new Vector2(r.xMax - rad, r.yMax - rad), new Vector2(r.xMax - rad, r.yMin + rad),
                new Vector2(r.xMin + rad, r.yMin + rad), new Vector2(r.xMin + rad, r.yMax - rad),
            };
            float[] start = { 90f, 0f, 270f, 180f };
            for (int c = 0; c < 4; c++)
            {
                for (int k = 0; k <= CornerSteps; k++)
                {
                    float a = (start[c] - 90f * k / CornerSteps) * Mathf.Deg2Rad;
                    pts.Add(centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R);
                }
                // the straight edge to the next corner, subdivided so the rays out to the far
                // circle never span a wide angle (a chord across a wide angle would cut back
                // over the screen)
                Vector2 from = centres[c] + Dir(start[c] - 90f) * R;
                Vector2 to = centres[(c + 1) % 4] + Dir(start[(c + 1) % 4]) * R;
                float len = Vector2.Distance(centres[c], centres[(c + 1) % 4]);
                int n = Mathf.Max(1, Mathf.CeilToInt(len / EdgeStep));
                for (int k = 1; k < n; k++) pts.Add(Vector2.Lerp(from, to, k / (float)n));
            }
        }

        static Vector2 Dir(float deg) { float a = deg * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }

        static readonly List<Vector2> _in = new List<Vector2>(), _mid = new List<Vector2>(),
                                      _gOut = new List<Vector2>(), _gIn = new List<Vector2>();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Color32 full = color;
            Color32 clear = color; clear.a = 0;

            if (!hasHole)
            {
                var r = rectTransform.rect;
                vh.AddVert(new Vector3(r.xMin, r.yMin), full, Vector2.zero);
                vh.AddVert(new Vector3(r.xMin, r.yMax), full, Vector2.zero);
                vh.AddVert(new Vector3(r.xMax, r.yMax), full, Vector2.zero);
                vh.AddVert(new Vector3(r.xMax, r.yMin), full, Vector2.zero);
                vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
                return;
            }

            float rad = Mathf.Min(radius, Mathf.Min(hole.width, hole.height) * 0.5f);
            Outline(_in, hole, rad, 0f);
            Outline(_mid, hole, rad, feather);
            Vector2 centre = hole.center;
            int n = _in.Count;
            for (int i = 0; i < n; i++)
            {
                vh.AddVert(_in[i], clear, Vector2.zero);
                vh.AddVert(_mid[i], full, Vector2.zero);
                Vector2 d = (_mid[i] - centre).normalized;
                vh.AddVert(centre + d * Far, full, Vector2.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int a0 = i * 3, b0 = j * 3;
                // feather band
                vh.AddTriangle(a0, a0 + 1, b0 + 1); vh.AddTriangle(b0 + 1, b0, a0);
                // out to the far circle
                vh.AddTriangle(a0 + 1, a0 + 2, b0 + 2); vh.AddTriangle(b0 + 2, b0 + 1, a0 + 1);
            }

            if (glow <= 0f) return;
            // the light ring, drawn last so it sits over the dark: bright on the rim, fading out
            // into the dim and, more briefly, in over the target
            Outline(_gOut, hole, rad, GlowOut);
            Color32 lit = new Color(1f, 0.97f, 0.86f, glow * color.a / 0.72f);
            Color32 none = lit; none.a = 0;
            int baseIdx = vh.currentVertCount;
            // _gIn is sampled on a smaller rectangle; pair it with the rim by shrinking the hole
            var shrunk = new Rect(hole.x + GlowIn, hole.y + GlowIn, Mathf.Max(1f, hole.width - GlowIn * 2f), Mathf.Max(1f, hole.height - GlowIn * 2f));
            Outline(_gIn, shrunk, Mathf.Max(0f, rad - GlowIn), 0f);
            if (_gIn.Count != n || _gOut.Count != n) return;     // a hole too small to pair rings on
            for (int i = 0; i < n; i++)
            {
                vh.AddVert(_gIn[i], none, Vector2.zero);
                vh.AddVert(_in[i], lit, Vector2.zero);
                vh.AddVert(_gOut[i], none, Vector2.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int a0 = baseIdx + i * 3, b0 = baseIdx + j * 3;
                vh.AddTriangle(a0, a0 + 1, b0 + 1); vh.AddTriangle(b0 + 1, b0, a0);
                vh.AddTriangle(a0 + 1, a0 + 2, b0 + 2); vh.AddTriangle(b0 + 2, b0 + 1, a0 + 1);
            }
        }

        public bool IsRaycastLocationValid(Vector2 sp, Camera cam)
        {
            if (!block) return false;
            if (!hasHole) return true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, cam, out var lp);
            return !hole.Contains(lp);
        }
    }

    /// <summary>What the coach shows for one moment of the tutorial or one tip.</summary>
    public class CoachSpec
    {
        public string key;                 // same key = same card; the card is only rebuilt when it changes
        public string title, body;
        /// <summary>Screen-pixel rect of the thing being pointed at, or null for none. Polled every
        /// frame, because what it points at moves: the camera pans, panels pop in.</summary>
        public Func<Rect?> target;
        public float holeRadius = 22f;
        public float holePad = 10f;
        /// <summary>Dim the screen and swallow taps outside the hole.</summary>
        public bool dim;
        /// <summary>With <see cref="dim"/>: the same dark, but every tap goes through. For steps where
        /// the player keeps playing while the card talks — plant the rest, wait for a crop, harvest
        /// the others — which used to show no dim at all and read as a different kind of guide.</summary>
        public bool passThrough;
        public bool hand = true;
        public string primary;
        public Action onPrimary;
        public string secondary;
        public Action onSecondary;
        /// <summary>Card placement when there is no target: centre of the screen, or high and out of
        /// the way (for "wait for the crop" steps, where the farm itself is what to look at).</summary>
        public bool centred;
        /// <summary>Screen rect the card must not cover (the seed sheet, the open menu).</summary>
        public Func<Rect?> avoid;
    }

    /// <summary>The tutorial's on-screen furniture: <see cref="Spotlight"/>, a pointing hand and a
    /// paper card with the farmer's portrait. It knows nothing about steps — <see cref="Tutorial"/>
    /// hands it a <see cref="CoachSpec"/> and it follows the target around.</summary>
    public class CoachView
    {
        RectTransform _layer, _cardHolder, _hand, _ripple;
        Spotlight _dim;
        Image _rippleImg;
        CanvasGroup _dimGroup, _cardGroup, _handGroup;
        CoachSpec _spec;
        string _builtKey;
        Rect _holeNow;
        bool _holeValid;
        float _t;
        float _handSide = 1f;

        public bool Visible => _spec != null;
        Text _bodyText;

        /// <summary>Change the current card's second line in place — for a countdown, which must
        /// not rebuild (and re-fade) the whole card every second.</summary>
        public void SetBody(string body)
        {
            if (_bodyText != null && _bodyText.text != body) _bodyText.text = body;
        }
        public CoachSpec Spec => _spec;

        /// <summary>The sprite's fingertip, as a pivot. Printed by Tools/make_pointer.py (Fluent Emoji
        /// 3D hand on a 320 px canvas with room for its shadow).</summary>
        static readonly Vector2 FingerTip = new Vector2(0.403f, 0.863f);
        /// <summary>The sprite's box. The hand fills about 80% of it, the rest is shadow margin.</summary>
        const float HandH = 128f;
        const float CardW = 520f, CardWCompact = 460f;

        public void Build(RectTransform layer)
        {
            _layer = layer;

            var dimRt = UIKit.Node("dim", layer);
            dimRt.Stretch();
            _dim = dimRt.gameObject.AddComponent<Spotlight>();
            _dim.color = new Color(0.02f, 0.05f, 0.07f, 0.72f);
            _dimGroup = dimRt.gameObject.AddComponent<CanvasGroup>();
            _dimGroup.alpha = 0f;
            dimRt.gameObject.SetActive(false);

            _ripple = UIKit.Node("ripple", layer);
            _ripple.sizeDelta = new Vector2(60, 60);
            _rippleImg = UIKit.Img(_ripple, Theme.Ring(0.12f), Color.white, "ring");
            _rippleImg.rectTransform.Stretch();
            _rippleImg.raycastTarget = false;
            _ripple.gameObject.SetActive(false);

            _hand = UIKit.Node("hand", layer);
            _hand.sizeDelta = new Vector2(HandH, HandH);
            _hand.pivot = FingerTip;
            var hi = UIKit.Img(_hand, Art.Item("pointer_hand"), Color.white, "img");
            hi.rectTransform.Stretch();
            hi.raycastTarget = false;
            _handGroup = _hand.gameObject.AddComponent<CanvasGroup>();
            _handGroup.blocksRaycasts = false;
            _hand.gameObject.SetActive(false);

            _cardHolder = UIKit.Node("card", layer);
            _cardGroup = _cardHolder.gameObject.AddComponent<CanvasGroup>();
            _cardHolder.gameObject.SetActive(false);
        }

        public void Show(CoachSpec spec)
        {
            bool fresh = _spec == null;
            _spec = spec;
            if (spec.key != _builtKey) BuildCard(spec, fresh);

            bool dim = spec.dim;
            if (_dim.gameObject.activeSelf != dim)
            {
                _dim.gameObject.SetActive(dim);
                if (dim) { _dimGroup.alpha = 0f; _holeValid = false; }
            }
            _dim.block = dim && !spec.passThrough;
        }

        public void Hide()
        {
            if (_spec == null) return;
            _spec = null;
            _builtKey = null;
            _dim.gameObject.SetActive(false);
            _hand.gameObject.SetActive(false);
            _ripple.gameObject.SetActive(false);
            foreach (Transform c in _cardHolder) UnityEngine.Object.Destroy(c.gameObject);
            _cardHolder.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------
        // card
        // ------------------------------------------------------------
        Vector2 _cardSize;

        void BuildCard(CoachSpec spec, bool fresh)
        {
            _builtKey = spec.key;
            _bodyText = null;
            foreach (Transform c in _cardHolder) UnityEngine.Object.Destroy(c.gameObject);
            _cardHolder.gameObject.SetActive(true);

            if (spec.target == null && !spec.centred && spec.primary == null) { BuildBanner(spec, fresh); return; }

            const float PadL = 104f, PadR = 26f, PadT = 20f;
            float cardW = spec.target == null && !spec.centred ? CardWCompact : CardW;
            float textW = cardW - PadL - PadR;

            var card = UIKit.Node("paper", _cardHolder);
            card.Stretch();
            var face = SurfaceLook.Add(card, Looks.Paper, 26f).Fill;
            face.raycastTarget = true;              // a tap on the card never falls through to the farm

            var title = UIKit.Label(card, spec.title, 23, Theme.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            float titleH = Mathf.Max(34f, PreferredHeight(title, textW) + 4f);

            Text body = null;
            float bodyH = 0f;
            if (!string.IsNullOrEmpty(spec.body))
            {
                body = UIKit.Label(card, spec.body, 18, Theme.InkSoft, TextAnchor.UpperLeft);
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                body.lineSpacing = 1.05f;
                _bodyText = body;
                bodyH = PreferredHeight(body, textW) + 6f;
            }

            bool buttons = spec.primary != null || spec.secondary != null;
            float h = PadT + titleH + bodyH + (buttons ? 64f : 12f) + 8f;
            h = Mathf.Max(h, 118f);
            _cardSize = new Vector2(cardW, h);
            _cardHolder.anchorMin = _cardHolder.anchorMax = new Vector2(0.5f, 0.5f);
            _cardHolder.pivot = new Vector2(0.5f, 0.5f);
            _cardHolder.sizeDelta = _cardSize;

            title.rectTransform.Anchor(UIKit.TopLeft, new Vector2(PadL, -PadT), new Vector2(textW, titleH));
            if (body != null)
                body.rectTransform.Anchor(UIKit.TopLeft, new Vector2(PadL, -PadT - titleH), new Vector2(textW, bodyH));

            // the farmer, on a disc in the card's corner
            var av = UIKit.Node("avatar", card);
            av.Anchor(UIKit.TopLeft, new Vector2(16, -16), new Vector2(76, 76));
            UIKit.Img(av, Theme.Circle(), Theme.Hex("#E7F3DA"), "disc").rectTransform.Stretch();
            var pic = UIKit.Img(av, Theme.Skin.Farmer, Color.white, "pic");
            pic.preserveAspect = true;
            pic.rectTransform.Stretch(3, 3, 3, 3);
            var avRing = UIKit.Img(av, Theme.Ring(0.12f), Theme.Hex("#C9A46A"), "ring");
            avRing.rectTransform.Stretch(-3, -3, -3, -3);

            if (spec.primary != null)
            {
                var b = UIKit.Btn(card, spec.primary, Theme.Green, Theme.GreenDark, 20, 20, () => spec.onPrimary?.Invoke());
                b.GetComponent<RectTransform>().Anchor(UIKit.BottomRight, new Vector2(-PadR, 16), new Vector2(Mathf.Max(150f, 26f + spec.primary.Length * 11f), 50));
            }
            if (spec.secondary != null)
            {
                var link = UIKit.Node("skip", card);
                link.Anchor(UIKit.BottomLeft, new Vector2(PadL - 8f, 20), new Vector2(170, 42));
                var hit = UIKit.Img(link, null, new Color(1, 1, 1, 0.001f), "hit");
                hit.rectTransform.Stretch();
                hit.raycastTarget = true;
                var lab = UIKit.Label(link, spec.secondary, 17, Theme.InkSoft, TextAnchor.MiddleLeft);
                lab.rectTransform.Stretch(8, 0, 0, 0);
                var lb = link.gameObject.AddComponent<Button>();
                lb.targetGraphic = hit;
                lb.transition = Selectable.Transition.None;
                lb.onClick.AddListener(() => spec.onSecondary?.Invoke());
                link.gameObject.AddComponent<PressFx>();
            }

            // every new card fades in; only the first one after nothing also pops
            _cardGroup.alpha = 0f;
            Tween.Fade(_cardGroup, 1f, 0.2f);
            if (fresh) Tween.PopIn(_cardHolder, 0.24f, 0.9f);
        }

        /// <summary>The waiting steps' card: a slim strip under the HUD's top row, small portrait,
        /// two short lines and a "Bỏ qua" at the end. While a crop grows, the farm is the thing to
        /// look at — a full card there hid the back row of plots, the ones about to turn thirsty.</summary>
        void BuildBanner(CoachSpec spec, bool fresh)
        {
            const float W = 640f, PadL = 80f, PadR = 118f, PadV = 12f;
            float textW = W - PadL - PadR;

            var card = UIKit.Node("paper", _cardHolder);
            card.Stretch();
            SurfaceLook.Add(card, Looks.Paper, 24f).Fill.raycastTarget = true;

            var title = UIKit.Label(card, spec.title, 19, Theme.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            float titleH = PreferredHeight(title, textW) + 2f;
            Text body = null;
            float bodyH = 0f;
            if (!string.IsNullOrEmpty(spec.body))
            {
                body = UIKit.Label(card, spec.body, 16, Theme.InkSoft, TextAnchor.UpperLeft);
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                bodyH = PreferredHeight(body, textW) + 2f;
                _bodyText = body;
            }
            float h = Mathf.Max(76f, PadV * 2f + titleH + bodyH);
            _cardSize = new Vector2(W, h);
            _cardHolder.anchorMin = _cardHolder.anchorMax = new Vector2(0.5f, 0.5f);
            _cardHolder.pivot = new Vector2(0.5f, 0.5f);
            _cardHolder.sizeDelta = _cardSize;

            float top = (h - titleH - bodyH) * 0.5f;
            title.rectTransform.Anchor(UIKit.TopLeft, new Vector2(PadL, -top), new Vector2(textW, titleH));
            if (body != null) body.rectTransform.Anchor(UIKit.TopLeft, new Vector2(PadL, -top - titleH), new Vector2(textW, bodyH));

            var av = UIKit.Node("avatar", card);
            av.Anchor(UIKit.Left, new Vector2(14, 0), new Vector2(54, 54));
            UIKit.Img(av, Theme.Circle(), Theme.Hex("#E7F3DA"), "disc").rectTransform.Stretch();
            var pic = UIKit.Img(av, Theme.Skin.Farmer, Color.white, "pic");
            pic.preserveAspect = true;
            pic.rectTransform.Stretch(2, 2, 2, 2);
            UIKit.Img(av, Theme.Ring(0.12f), Theme.Hex("#C9A46A"), "ring").rectTransform.Stretch(-2, -2, -2, -2);

            if (spec.secondary != null)
            {
                var link = UIKit.Node("skip", card);
                link.Anchor(UIKit.Right, new Vector2(-12, 0), new Vector2(96, 44));
                var hit = UIKit.Img(link, null, new Color(1, 1, 1, 0.001f), "hit");
                hit.rectTransform.Stretch();
                hit.raycastTarget = true;
                UIKit.Label(link, "Bỏ qua", 16, Theme.InkSoft, TextAnchor.MiddleCenter).rectTransform.Stretch();
                var lb = link.gameObject.AddComponent<Button>();
                lb.targetGraphic = hit;
                lb.transition = Selectable.Transition.None;
                lb.onClick.AddListener(() => spec.onSecondary?.Invoke());
                link.gameObject.AddComponent<PressFx>();
            }

            _cardGroup.alpha = 0f;
            Tween.Fade(_cardGroup, 1f, 0.2f);
            if (fresh) Tween.PopIn(_cardHolder, 0.24f, 0.9f);
        }

        static float PreferredHeight(Text t, float width)
        {
            var settings = t.GetGenerationSettings(new Vector2(width, 0f));
            return t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, settings) / t.pixelsPerUnit;
        }

        // ------------------------------------------------------------
        // per frame
        // ------------------------------------------------------------
        public void Tick(float dt)
        {
            if (_spec == null || _layer == null) return;
            _t += dt;

            Rect layerRect = _layer.rect;
            Rect? target = _spec.target != null ? _spec.target() : null;
            Rect? local = null;
            if (target.HasValue)
            {
                var r = target.Value;
                Vector2 a = ToLocal(new Vector2(r.xMin, r.yMin));
                Vector2 b = ToLocal(new Vector2(r.xMax, r.yMax));
                var lr = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                lr.xMin -= _spec.holePad; lr.xMax += _spec.holePad;
                lr.yMin -= _spec.holePad; lr.yMax += _spec.holePad;
                local = lr;
            }

            // --- the hole eases toward the target: an iris closing onto it, then a slow breath ---
            if (_dim.gameObject.activeSelf)
            {
                if (_dimGroup.alpha < 1f) _dimGroup.alpha = Mathf.Min(1f, _dimGroup.alpha + dt / 0.22f);
                if (local.HasValue)
                {
                    var want = local.Value;
                    if (!_holeValid)
                    {
                        // start wide around the target so the dark closes in
                        _holeNow = new Rect(want.center - want.size * 2.2f, want.size * 4.4f);
                        _holeValid = true;
                    }
                    float k = 1f - Mathf.Exp(-dt * 12f);
                    _holeNow = new Rect(Vector2.Lerp(_holeNow.position, want.position, k), Vector2.Lerp(_holeNow.size, want.size, k));
                    float breath = 3f * Mathf.Sin(_t * 3.2f);
                    var shown = new Rect(_holeNow.x - breath, _holeNow.y - breath, _holeNow.width + breath * 2f, _holeNow.height + breath * 2f);
                    _dim.SetHole(shown, _spec.holeRadius + breath);
                }
                else { _dim.ClearHole(); _holeValid = false; }
            }

            // --- hand ---
            bool showHand = _spec.hand && local.HasValue;
            if (_hand.gameObject.activeSelf != showHand) _hand.gameObject.SetActive(showHand);
            if (showHand) PlaceHand(local.Value, layerRect);
            else if (_ripple.gameObject.activeSelf) _ripple.gameObject.SetActive(false);

            PlaceCard(local, layerRect);
        }

        Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, null, out var lp);
            return lp;
        }

        enum HandMode { Up, Down, Side }
        HandMode _handMode;
        Rect _handRect;

        /// <summary>The fingertip rests on the target and the hand hangs toward the middle of the
        /// screen: below the target when there is room, above it for something on the bottom edge
        /// (the menu button, a seed card, Bán sỉ), beside it only when neither fits. Every 1.3 s it
        /// presses: the tip reaches in, dips, and a ring spreads from where it touched.</summary>
        void PlaceHand(Rect hole, Rect layer)
        {
            Rect safe = SafeLocal(layer);
            float roomBelow = hole.yMin - safe.yMin, roomAbove = safe.yMax - hole.yMax;
            _handMode = roomBelow >= HandH * 0.95f ? HandMode.Up
                      : roomAbove >= HandH * 0.95f ? HandMode.Down
                      : HandMode.Side;
            // body toward the screen's middle: target on the right, hand comes from the left
            _handSide = hole.center.x > layer.center.x ? -1f : 1f;

            float tilt = _handMode == HandMode.Side ? 62f : 24f;
            float rot = _handMode == HandMode.Down ? 180f - tilt : tilt;
            // unmirrored art has its thumb on the left, so a hand coming from the right is drawn
            // as is; one coming from the left is mirrored and every angle flips with it
            if (_handSide < 0) rot = -rot;
            _hand.localScale = new Vector3(_handSide > 0 ? 1f : -1f, 1f, 1f);
            _hand.localRotation = Quaternion.Euler(0, 0, rot);

            Vector2 tip = hole.center;
            float nudge = Mathf.Min(Mathf.Min(hole.width, hole.height) * 0.1f, 8f);
            if (_handMode == HandMode.Up) tip.y -= nudge;
            else if (_handMode == HandMode.Down) tip.y += nudge;
            else tip.x += _handSide * nudge;

            Vector2 bodyDir = -(Vector2)(_hand.localRotation * Vector2.up);
            Vector2 bodyCentre = tip + bodyDir * HandH * 0.5f;
            _handRect = new Rect(bodyCentre - Vector2.one * HandH * 0.5f, Vector2.one * HandH);

            // one tap per cycle: reach in, press, let the ring spread, drift back and rest
            const float Period = 1.3f;
            float ph = (_t % Period) / Period;
            float reach = ph < 0.28f ? Tween.EaseOut(ph / 0.28f)
                        : ph < 0.46f ? 1f
                        : 1f - Tween.EaseOut((ph - 0.46f) / 0.54f);
            float press = ph >= 0.28f && ph < 0.46f ? Mathf.Sin((ph - 0.28f) / 0.18f * Mathf.PI) : 0f;
            Vector2 axis = (Vector2)(_hand.localRotation * Vector2.up);
            Vector2 hover = -axis * 16f * (1f - reach);
            _hand.anchoredPosition = LayerToAnchored(tip + hover);
            float sc = 1f - 0.1f * press;
            _hand.localScale = new Vector3(_hand.localScale.x * sc, sc, 1f);
            _handGroup.alpha = 0.55f + 0.45f * reach;

            bool rip = ph >= 0.32f && ph < 0.9f;
            if (_ripple.gameObject.activeSelf != rip) _ripple.gameObject.SetActive(rip);
            if (rip)
            {
                float q = (ph - 0.32f) / 0.58f;
                _ripple.anchoredPosition = LayerToAnchored(tip);
                _ripple.sizeDelta = Vector2.one * Mathf.Lerp(24f, 104f, Tween.EaseOut(q));
                _rippleImg.color = new Color(1f, 1f, 1f, 0.9f * (1f - q) * (1f - q));
            }
        }

        /// <summary>Beside the target on whichever side has room, never over the target, the hand
        /// or whatever the step says to keep clear; centred or high when there is nothing to point
        /// at. Always inside the safe area.</summary>
        void PlaceCard(Rect? hole, Rect layer)
        {
            if (!_cardHolder.gameObject.activeSelf) return;
            Rect safe = SafeLocal(layer);
            Vector2 half = _cardSize * 0.5f;
            Rect? avoid = null;
            if (_spec.avoid != null && _spec.avoid() is Rect ar)
            {
                Vector2 a = ToLocal(ar.min), b = ToLocal(ar.max);
                avoid = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }

            Vector2 Clamp(Vector2 p)
            {
                p.x = Mathf.Clamp(p.x, safe.xMin + 12f + half.x, safe.xMax - 12f - half.x);
                p.y = Mathf.Clamp(p.y, safe.yMin + 12f + half.y, safe.yMax - 12f - half.y);
                return p;
            }

            Vector2 c;
            if (hole.HasValue)
            {
                var h = hole.Value;
                bool hand = _hand.gameObject.activeSelf;
                var blocked = new List<Rect> { Grow(h, 10f) };
                if (hand) blocked.Add(Grow(_handRect, 4f));
                if (avoid.HasValue) blocked.Add(Grow(avoid.Value, 8f));

                // a union of what is below / above the target, so "below" clears the hand too
                float lowest = h.yMin, highest = h.yMax;
                if (hand) { lowest = Mathf.Min(lowest, _handRect.yMin); highest = Mathf.Max(highest, _handRect.yMax); }
                var cands = new List<Vector2>();
                bool targetHigh = h.center.y > layer.center.y;
                var below = new Vector2(h.center.x, lowest - 18f - half.y);
                var above = new Vector2(h.center.x, highest + 18f + half.y);
                if (targetHigh) { cands.Add(below); cands.Add(above); } else { cands.Add(above); cands.Add(below); }
                if (avoid.HasValue)
                {
                    var av = avoid.Value;
                    cands.Add(new Vector2(h.center.x, av.yMax + 18f + half.y));
                    cands.Add(new Vector2(av.xMin - 18f - half.x, h.center.y));
                    cands.Add(new Vector2(av.xMax + 18f + half.x, h.center.y));
                }
                float leftOf = Mathf.Min(h.xMin, hand ? _handRect.xMin : h.xMin);
                float rightOf = Mathf.Max(h.xMax, hand ? _handRect.xMax : h.xMax);
                cands.Add(new Vector2(leftOf - 18f - half.x, h.center.y));
                cands.Add(new Vector2(rightOf + 18f + half.x, h.center.y));

                c = Clamp(cands[0]);
                foreach (var cand in cands)
                {
                    var p = Clamp(cand);
                    var r = new Rect(p - half, _cardSize);
                    bool hit = false;
                    foreach (var bl in blocked) if (bl.Overlaps(r)) { hit = true; break; }
                    if (!hit) { c = p; break; }
                }
            }
            else if (_spec.centred) c = layer.center;
            else
            {
                // high, under the HUD's top row (weather disc, wallet) and clear of the toasts and
                // harvest receipts that rise from the bottom
                const float TopRow = 92f;
                c = Clamp(new Vector2(safe.center.x, safe.yMax - TopRow - half.y));
            }

            var want = LayerToAnchored(c);
            // glide rather than jump when the target moves
            var cur = _cardHolder.anchoredPosition;
            _cardHolder.anchoredPosition = (cur - want).sqrMagnitude > 4f && _cardGroup.alpha > 0.9f
                ? Vector2.Lerp(cur, want, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f))
                : want;
        }

        static Rect Grow(Rect r, float by) { return new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f); }

        Vector2 LayerToAnchored(Vector2 layerLocal)
        {
            // children use centre anchors; the layer's local origin is its pivot (centre)
            return layerLocal - _layer.rect.center;
        }

        Rect SafeLocal(Rect layer)
        {
            var sa = SafeAreaFitter.SafeArea;
            Vector2 a = ToLocal(sa.min), b = ToLocal(sa.max);
            var r = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            if (r.width < 100f || r.height < 100f) return layer;
            return r;
        }

        // ------------------------------------------------------------
        // targets
        // ------------------------------------------------------------
        /// <summary>A RectTransform's screen rect, or null while it is hidden. Every layer is a
        /// screen-space-overlay canvas, where world space IS screen pixels.</summary>
        public static Rect? ScreenRect(RectTransform rt, float scale = 1f)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return null;
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            var r = Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
            if (Mathf.Abs(scale - 1f) > 0.001f)
                r = new Rect(r.center - r.size * scale * 0.5f, r.size * scale);
            return r;
        }
    }
}
