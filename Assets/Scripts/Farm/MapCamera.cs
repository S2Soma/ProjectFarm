using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LQFarm
{
    /// <summary>Pan and zoom over the archipelago.
    ///
    /// Not a <c>ScrollRect</c>: that gives one-finger drag but no pinch, and its content-size /
    /// normalizedPosition model fights a scaled child. This moves and scales one node instead —
    /// <c>anchoredPosition = -camera</c>, <c>localScale = Z</c> — and reads its input straight
    /// from the EventSystem, so it needs no Input API and works the same on a phone and in the
    /// Editor.
    ///
    /// Zoom is ONLY the content node's localScale. Touching <c>CanvasScaler.scaleFactor</c>
    /// instead would re-rasterize every glyph in the game on every pinch frame.</summary>
    public class MapCamera : MonoBehaviour
    {
        RectTransform _content, _viewport;

        /// <summary>The scale at which one island fills the play area — what the old FitFarm
        /// computed. Everything else is a ratio of it, so the whole thing stays responsive.</summary>
        public float ZBase { get; private set; } = 1f;

        /// <summary>Zoom levels as RATIOS of <see cref="ZBase"/>, smallest first.
        ///
        /// Ratios rather than absolute scales, because the player's zoom has to survive a canvas
        /// that changes size — a rotation, a resized Game view, a different device. Storing the
        /// absolute scale looked equivalent and was not: after a resize the old value was clamped
        /// into the newly computed range and the camera stuck at maximum zoom, with no input from
        /// anyone. A ratio means "the player is at the farm level", which is what actually
        /// survives.
        ///
        /// Pinch is continuous during the gesture and snaps to the nearest level on release: a
        /// procedurally built UI has ~40 hand-tuned overlay rects, and a continuum of LOD
        /// thresholds is not something that can be tested.</summary>
        public readonly List<float> Levels = new List<float>();

        float _ratio = 1f;

        public float Z => ZBase * _ratio;
        public float Ratio => _ratio;
        public Vector2 Camera { get; private set; }

        /// <summary>Content extent in content-local units, before scaling.</summary>
        public Rect ContentBounds { get; set; } = new Rect(-600, -500, 1200, 1000);

        /// <summary>At farm zoom a released swipe settles on an island instead of wherever the
        /// finger stopped. Given the camera and release velocity (content units/s), returns the
        /// camera target, or null to stay put. Set by the archipelago, which knows the islands.
        ///
        /// Without it the map panned freely between islands but the game never learned which one
        /// the player was now looking at — the pager, the three verbs and the plot popup all
        /// kept acting on the island the swipe started from.</summary>
        public System.Func<Vector2, Vector2, Vector2?> PageTarget;

        /// <summary>A tap on the map that did not land on a plot, in content units.</summary>
        public System.Action<Vector2> onTap;

        /// <summary>Screen pixels covered at the bottom by a sheet (the seed picker). The camera
        /// treats the space above it as the viewport, so "centre on this plot" means centre in
        /// the part of the screen the player can still see.</summary>
        public float InsetBottomPx { get; private set; }

        public void SetInsetBottom(float px, Vector2? focus = null)
        {
            InsetBottomPx = Mathf.Max(0f, px);
            var want = focus.HasValue ? focus.Value - new Vector2(0f, InsetBottomPx * 0.5f / Mathf.Max(0.01f, Z)) : Camera;
            AnimateTo(ClampCamera(want, Z, 0f), Z, 0.28f);
        }

        const float PageTime = 0.32f;

        /// <summary>How far past the edge a drag may pull before springing back.</summary>
        const float Overscroll = 120f;
        const float SnapTime = 0.18f;
        const float Friction = 6f;

        // ---- gesture state ----
        readonly Dictionary<int, Vector2> _pointers = new Dictionary<int, Vector2>();
        bool _panning;
        Vector2 _velocity;
        float _pinchStartDist, _pinchStartZ;
        Vector2 _pinchStartMid, _pinchStartCam;

        // ---- animation ----
        bool _animating;
        float _animT, _animDur;
        Vector2 _camFrom, _camTo;
        float _zFrom, _zTo;

        public void Configure(RectTransform content, RectTransform viewport)
        {
            _content = content;
            _viewport = viewport;
            Recompute();
            Apply();
        }

        /// <summary>Recalculate ZBase for the current canvas, and rebuild the level list.
        ///
        /// The margins are the old FitFarm numbers and mean the same thing: room taken by the HUD
        /// rails and the action bar. The formula is fitted to the GRID, not to the island — the
        /// island is meant to bleed under the HUD bars like a backdrop.</summary>
        public void Recompute(int islandCount = 1)
        {
            // The level list depends only on how many islands exist, so it is built even when
            // the canvas has not been laid out yet — leaving it empty during the first frames is
            // what made the camera unclampable.
            // ZBase first: the level list is expressed as RATIOS of it, and the widest level is
            // now computed from the content, so it cannot be built before ZBase is known.
            Vector2 canvas = _viewport != null ? _viewport.rect.size : Vector2.zero;
            if (_viewport != null)
            {
                if (canvas.x > 0f && canvas.y > 0f)
                {
                    // Touching beds at PlotScale 1.42: 954 wide; crop tops at +278 and the
                    // field rim at -246.
                    const float FieldW = 964f, FieldH = 524f;

                    // The margins are the HUD furniture the field must not sit under, measured
                    // from the layout rather than guessed. Horizontally that is now ONE rail
                    // (104 wide, 58 in from the edge) doubled because the field is centred:
                    // 2 x 162 would be 324, but the rail only covers the middle third of its
                    // side, so half of it is the honest figure. Deleting the left rail took this
                    // from 210 to 160, and that 50 px is what the sea gutter is made of.
                    //
                    // Vertically: player card / season bar above (110) and the action bar below
                    // (72 + 14 margin + a little air).
                    float room = Mathf.Min((canvas.x - 160f) / FieldW, (canvas.y - 200f) / FieldH);
                    ZBase = Mathf.Clamp(room, 0.55f, 1.5f);
                }
            }

            Levels.Clear();
            // With a single island there is nothing to zoom OUT to, and allowing it would only
            // let the player end up staring at a small island for no reason. Zooming IN stays
            // available — it is the accessibility lever.
            if (islandCount > 1)
            {
                // Z0 has to FIT THE ARCHIPELAGO, and the old fixed 0.267 did not. It was chosen
                // against the plan's 3x2 grid (2.900 wide); the islands actually ship as a single
                // row 7.575 wide, so "see everything" showed four of six and the two on the ends
                // were unreachable by zooming out. Measuring the content is also the only version
                // that stays correct when a seventh island is added.
                float z0 = Mathf.Min(0.267f, FitRatio(canvas));
                Levels.Add(z0);

                // With six islands z0 lands near 0.14, and 0.14 -> 0.59 is a 4x step: one pinch
                // teleports from "the whole world" to "one farm" with nothing legible in between.
                // A geometric middle rung keeps every step around 2x, which is roughly what a
                // single pinch gesture covers.
                if (0.590f / z0 > 2.6f) Levels.Add(Mathf.Sqrt(z0 * 0.590f));

                Levels.Add(0.590f);   // Z1 — one island plus its neighbours' edges
            }
            Levels.Add(1.000f);       // Z2 — the farm, direct manipulation
            Levels.Add(1.350f);       // zoomed in

            _ratio = Mathf.Clamp(_ratio, Levels[0], Levels[Levels.Count - 1]);
            MoveCamera(Camera, rubber: false);
        }

        // ============================================================
        // input — driven by the pan plane and forwarded by TapOrDrag
        // ============================================================
        public void PointerDown(PointerEventData e)
        {
            _pointers[e.pointerId] = e.position;
            _animating = false;
            if (_pointers.Count == 2) BeginPinch();
        }

        public void PointerUp(PointerEventData e)
        {
            _pointers.Remove(e.pointerId);
            if (_pointers.Count < 2) _pinchStartDist = 0f;
            if (_pointers.Count == 0 && _panning) { _panning = false; SnapToNearestLevel(); }
        }

        public void BeginDrag(PointerEventData e)
        {
            _panning = true;
            _velocity = Vector2.zero;
            _pointers[e.pointerId] = e.position;
        }

        public void Drag(PointerEventData e)
        {
            _pointers[e.pointerId] = e.position;

            if (_pointers.Count >= 2) { UpdatePinch(); return; }

            // delta is in screen pixels; the camera lives in canvas units at the current zoom
            Vector2 d = e.delta / CanvasScale() / Mathf.Max(0.01f, Z);
            MoveCamera(Camera - d, rubber: true);
            _velocity = Vector2.Lerp(_velocity, -d / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.5f);
        }

        public void EndDrag(PointerEventData e)
        {
            _pointers.Remove(e.pointerId);
            if (_pointers.Count > 0) return;
            _panning = false;
            SnapToNearestLevel();
        }

        /// <summary>Mouse wheel / trackpad. Device gets pinch; this is what makes the same code
        /// testable in the Editor.</summary>
        public void Scroll(PointerEventData e)
        {
            if (Levels.Count < 2) return;
            float target = Z * (1f + Mathf.Clamp(e.scrollDelta.y, -3f, 3f) * 0.08f);
            ZoomAround(ScreenToContent(e.position), target);
        }

        public void Tap(Vector2 screen)
        {
            onTap?.Invoke(ScreenToContent(screen));
        }

        void BeginPinch()
        {
            _velocity = Vector2.zero;
            var pts = new List<Vector2>(_pointers.Values);
            _pinchStartDist = Vector2.Distance(pts[0], pts[1]);
            _pinchStartZ = Z;
            _pinchStartMid = ScreenToContent((pts[0] + pts[1]) * 0.5f);
            _pinchStartCam = Camera;
        }

        void UpdatePinch()
        {
            if (_pinchStartDist <= 1f) { BeginPinch(); return; }
            var pts = new List<Vector2>(_pointers.Values);
            float dist = Vector2.Distance(pts[0], pts[1]);
            float want = _pinchStartZ * (dist / _pinchStartDist);
            Camera = _pinchStartCam;
            ZoomAround(_pinchStartMid, want);
        }

        /// <summary>Zoom while keeping <paramref name="anchor"/> (in content units) under the
        /// same screen point — the difference between a pinch that feels attached to the world
        /// and one that slides out from under the finger.</summary>
        void ZoomAround(Vector2 anchor, float targetZ)
        {
            if (Levels.Count == 0) return;
            float want = targetZ / Mathf.Max(0.01f, ZBase);
            // a little past the ends, so a pinch has somewhere to rubber-band against
            float r = Mathf.Clamp(want, Levels[0] * 0.9f, Levels[Levels.Count - 1] * 1.1f);
            float before = Z;
            Vector2 offset = anchor - Camera;
            _ratio = r;
            Camera = anchor - offset * (before / Mathf.Max(0.01f, Z));
            MoveCamera(Camera, rubber: true);
        }

        void SnapToNearestLevel()
        {
            if (Levels.Count == 0) return;
            float best = Levels[0];
            foreach (float l in Levels)
                if (Mathf.Abs(l - _ratio) < Mathf.Abs(best - _ratio)) best = l;
            float z = ZBase * best;
            Vector2 cam = Camera;
            float time = SnapTime;
            if (best >= 0.95f && PageTarget != null)
            {
                var t = PageTarget(Camera, _velocity);
                if (t.HasValue) { cam = t.Value - new Vector2(0f, InsetBottomPx * 0.5f / Mathf.Max(0.01f, z)); time = PageTime; }
            }
            _velocity = Vector2.zero;
            AnimateTo(ClampCamera(cam, z, 0f), z, time);
        }

        /// <summary>The zoom ratio at which the whole content rect fits on screen, with a little
        /// air so the outermost islands are not flush against the bezel.</summary>
        float FitRatio(Vector2 canvas)
        {
            if (canvas.x <= 0f || canvas.y <= 0f) return 0.267f;
            var b = ContentBounds;
            if (b.width <= 0f || b.height <= 0f) return 0.267f;
            float fit = Mathf.Min((canvas.x - 200f) / b.width, (canvas.y - 220f) / b.height);
            return Mathf.Max(0.06f, fit / Mathf.Max(0.01f, ZBase));
        }

        /// <summary>Jump without easing — for entering a farm, not for moving inside one.</summary>
        public void Snap(Vector2 target, float z)
        {
            _ratio = z / Mathf.Max(0.01f, ZBase);
            _animating = false;
            MoveCamera(target, rubber: false);
        }

        /// <summary>Ease the camera to an island at a chosen zoom.</summary>
        public void FlyTo(Vector2 target, float z, float seconds = 0.35f)
        {
            AnimateTo(ClampCamera(target, z, 0f), z, seconds);
        }

        void AnimateTo(Vector2 cam, float z, float seconds)
        {
            _camFrom = Camera; _camTo = cam;
            _zFrom = Z; _zTo = z;
            _animT = 0f; _animDur = Mathf.Max(0.01f, seconds);
            _animating = true;
        }

        // ============================================================
        // movement
        // ============================================================
        void MoveCamera(Vector2 want, bool rubber)
        {
            Camera = ClampCamera(want, Z, rubber ? Overscroll : 0f);
            Apply();
        }

        /// <summary>Screen pixels the free area's centre sits right of the screen centre, as a
        /// camera offset: positive moves content LEFT, away from the right-hand rail.</summary>
        public const float CentreBiasPx = 80f;

        Vector2 ClampCamera(Vector2 want, float z, float slack)
        {
            if (_viewport == null) return want;
            z = Mathf.Max(0.01f, z);
            // the visible viewport is the screen minus the bottom sheet, and its centre sits
            // half the sheet's height above the screen centre
            Vector2 view = _viewport.rect.size - new Vector2(0f, InsetBottomPx);
            Vector2 half = view * 0.5f / z;
            float up = InsetBottomPx * 0.5f / z;
            var b = ContentBounds;

            // An axis whose content is narrower than the viewport centres instead of panning —
            // otherwise a small island would slide around inside an empty screen.
            // Centred content is centred in the space the HUD leaves free, not on the screen: the
            // rail covers ~170 px on the right and nothing covers the left, so a true centre put
            // the last island of the overview under the buttons.
            float x = (b.width <= half.x * 2f)
                ? b.center.x + CentreBiasPx / Mathf.Max(0.01f, z)
                : Mathf.Clamp(want.x, b.xMin + half.x - slack, b.xMax - half.x + slack);
            float y = (b.height <= half.y * 2f)
                ? b.center.y - up
                : Mathf.Clamp(want.y, b.yMin + half.y - up - slack, b.yMax - half.y - up + slack);
            return new Vector2(x, y);
        }

        void Apply()
        {
            if (_content == null) return;
            _content.localScale = Vector3.one * Z;
            _content.anchoredPosition = -Camera * Z;
        }

        float CanvasScale()
        {
            var c = _viewport != null ? _viewport.GetComponentInParent<Canvas>() : null;
            return c != null ? Mathf.Max(0.01f, c.scaleFactor) : 1f;
        }

        Vector2 ScreenToContent(Vector2 screen)
        {
            if (_viewport == null) return Camera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, screen, null, out Vector2 local);
            return Camera + local / Mathf.Max(0.01f, Z);
        }

        void Update()
        {
            if (_animating)
            {
                _animT += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_animT / _animDur);
                float e = 1f - (1f - t) * (1f - t);          // ease-out quad
                _ratio = Mathf.Lerp(_zFrom, _zTo, e) / Mathf.Max(0.01f, ZBase);
                Camera = Vector2.Lerp(_camFrom, _camTo, e);
                Apply();
                if (t >= 1f) _animating = false;
                return;
            }

            if (_panning || _pointers.Count > 0) return;

            // inertia, then spring back inside the bounds
            if (_velocity.sqrMagnitude > 1f)
            {
                Camera += _velocity * Time.unscaledDeltaTime;
                _velocity = Vector2.Lerp(_velocity, Vector2.zero, Friction * Time.unscaledDeltaTime);
                MoveCamera(Camera, rubber: true);
            }

            Vector2 inside = ClampCamera(Camera, Z, 0f);
            if ((inside - Camera).sqrMagnitude > 0.01f)
            {
                Camera = Vector2.Lerp(Camera, inside, 12f * Time.unscaledDeltaTime);
                Apply();
            }
        }
    }
}
