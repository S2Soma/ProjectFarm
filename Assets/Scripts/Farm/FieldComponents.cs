using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LQFarm
{
    // Behaviours shared by everything drawn on a field. They were at the bottom of the view
    // class, which was fine while there was one view; with islands being pooled and rebuilt they
    // are used from several places and belong on their own.

    /// <summary>A tap that is a tap, and a drag that is a drag.
    ///
    /// This replaces <c>Button</c> on every plot, and it is not a refinement — it is a fix for a
    /// defect that arrives the moment the map can be panned. <c>Button</c> raises its click from
    /// <c>IPointerClickHandler</c>, which fires whenever press and release land on the same
    /// GameObject, REGARDLESS of how far the finger travelled in between. Left alone, dragging
    /// the world across a plot would plant a crop on it. Not subtly, and not rarely.
    ///
    /// So: <see cref="onTap"/> fires only when the finger stayed inside <see cref="TapSlop"/>,
    /// lifted within <see cref="TapSeconds"/>, and never entered a drag. Everything else is
    /// forwarded verbatim to the camera, so a drag that happens to start on a plot pans the world
    /// instead of being swallowed.
    ///
    /// <c>DiamondHit</c> is untouched — the raycast shape was never the problem.</summary>
    public class TapOrDrag : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        /// <summary>Reference pixels of travel still counted as a tap. Roughly a millimetre of
        /// finger roll on a phone — small enough not to swallow deliberate drags, large enough
        /// that a firm press is not rejected.</summary>
        public const float TapSlop = 12f;
        public const float TapSeconds = 0.40f;

        public System.Action onTap;
        public MapCamera map;

        Vector2 _downAt;
        float _downTime;
        bool _dragging, _armed;

        /// <summary>Counters and a clock, so the tap/drag decision can be tested without a live
        /// EventSystem. The rule this component exists to enforce is exactly the kind that gets
        /// quietly broken by a later edit, so it is worth being able to assert.</summary>
        public static int TapCount, DragCount;
        public static System.Func<float> Clock = () => Time.unscaledTime;
        public static void ResetCounters() { TapCount = 0; DragCount = 0; }

        public void OnPointerDown(PointerEventData e)
        {
            _downAt = e.position;
            _downTime = Clock();
            _dragging = false;
            _armed = true;
            if (map != null) map.PointerDown(e);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            _dragging = true;
            _armed = false;
            if (map != null) map.BeginDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (map != null) map.Drag(e);
        }

        public void OnEndDrag(PointerEventData e)
        {
            _dragging = false;
            if (map != null) map.EndDrag(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (map != null) map.PointerUp(e);

            bool near = (e.position - _downAt).magnitude <= TapSlop * CanvasScale();
            bool quick = Clock() - _downTime <= TapSeconds;
            if (_armed && !_dragging && near && quick)
            {
                TapCount++;
                onTap?.Invoke();
            }
            else if (_dragging || !near)
            {
                DragCount++;
            }
            _armed = false;
        }

        public void OnScroll(PointerEventData e)
        {
            if (map != null) map.Scroll(e);
        }

        float CanvasScale()
        {
            var c = GetComponentInParent<Canvas>();
            return c != null ? Mathf.Max(0.01f, c.scaleFactor) : 1f;
        }
    }

    /// <summary>The full-screen surface that catches drags which did not start on anything.</summary>
    public class PanPlane : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public MapCamera map;

        // A tap on open ground or sea: same thresholds as a plot tap, so the two agree about
        // what counts as a tap.
        Vector2 _downAt; float _downTime; bool _dragged;

        public void OnPointerDown(PointerEventData e)
        {
            _downAt = e.position; _downTime = TapOrDrag.Clock(); _dragged = false;
            if (map != null) map.PointerDown(e);
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (map != null) map.PointerUp(e);
            if (map == null || _dragged) return;
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
            bool still = (e.position - _downAt).magnitude / scale < TapOrDrag.TapSlop;
            bool quick = TapOrDrag.Clock() - _downTime < TapOrDrag.TapSeconds;
            if (still && quick) map.Tap(e.position);
        }
        public void OnBeginDrag(PointerEventData e)   { _dragged = true; if (map != null) map.BeginDrag(e); }
        public void OnDrag(PointerEventData e)        { if (map != null) map.Drag(e); }
        public void OnEndDrag(PointerEventData e)     { if (map != null) map.EndDrag(e); }
        public void OnScroll(PointerEventData e)      { if (map != null) map.Scroll(e); }
    }

    /// <summary>Restricts a plot's raycast to its own diamond (plus its soil skirt), so the
    /// overlapping rectangles stop stealing each other's taps. Nearer plots already sit later
    /// in the hierarchy, so the topmost hit is the nearest one — which is what the eye picks.</summary>
    public class DiamondHit : MonoBehaviour, ICanvasRaycastFilter
    {
        public float halfW = 90f, halfH = 45f, skirt = 25f;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera cam)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, cam, out var p))
                return false;
            // the band directly under the bed is this plot's own skirt; nothing else claims it
            float y = (p.y < 0f && p.y > -skirt) ? 0f : (p.y < 0f ? p.y + skirt : p.y);
            return Mathf.Abs(p.x) / halfW + Mathf.Abs(y) / halfH <= 1f;
        }
    }

    /// <summary>Squashes the whole plot on touch, so a tap feels like it landed.</summary>
    public class PlotPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform target;

        Coroutine _co;

        public void OnPointerDown(PointerEventData e) { Go(0.93f, 0.07f); }
        public void OnPointerUp(PointerEventData e)   { Go(1f, 0.14f); }

        void Go(float to, float time)
        {
            if (target == null || !gameObject.activeInHierarchy) return;
            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(Run(to, time));
        }

        System.Collections.IEnumerator Run(float to, float time)
        {
            Vector3 from = target.localScale;
            Vector3 dst = Vector3.one * to;
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                if (target == null) yield break;
                target.localScale = Vector3.Lerp(from, dst, t / time);
                yield return null;
            }
            if (target != null) target.localScale = dst;
            _co = null;
        }
    }
}
