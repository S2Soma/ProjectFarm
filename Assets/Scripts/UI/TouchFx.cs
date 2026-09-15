using UnityEngine;

namespace LQFarm
{
    /// <summary>Trang trí ▸ Chạm and ▸ Vuốt: a small burst where a finger lands and a trail behind
    /// a drag, on a top layer that never takes a tap.
    ///
    /// Reads the Input System's pointer directly rather than listening to UI events: a drag that
    /// pans the map, a tap on a button and a tap on empty sky should all leave the same mark.</summary>
    public class TouchFx : MonoBehaviour
    {
        public RectTransform layer;

        bool _down;
        Vector2 _last;
        float _pending;

        /// <summary>Distance between trail puffs, in canvas units.</summary>
        const float TrailStep = 16f;

        void Update()
        {
            var s = GS.Local;
            if (s == null || layer == null || FxKit.I == null) return;
            string tap = Cosmetics.Worn(s, CosmeticSlot.Tap);
            string swipe = Cosmetics.Worn(s, CosmeticSlot.Swipe);
            if (tap == null && swipe == null) { _down = false; return; }

#if ENABLE_INPUT_SYSTEM
            var ptr = UnityEngine.InputSystem.Pointer.current;
            if (ptr == null) return;
            Vector2 screen = ptr.position.ReadValue();
            bool pressedNow = ptr.press.wasPressedThisFrame;
            bool held = ptr.press.isPressed;
#else
            Vector2 screen = Input.mousePosition;
            bool pressedNow = Input.GetMouseButtonDown(0);
            bool held = Input.GetMouseButton(0);
#endif
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, null, out var local)) return;

            if (pressedNow)
            {
                _down = true;
                _last = local;
                _pending = 0f;
                if (tap != null) CosmeticFx.Emit(layer, local, tap);
                return;
            }
            if (!held) { _down = false; return; }
            if (!_down || swipe == null) { _last = local; return; }

            // puffs evenly along the path, however fast the finger moved this frame
            Vector2 d = local - _last;
            float len = d.magnitude;
            if (len < 0.5f) return;
            _pending += len;
            var recipes = CosmeticFx.For(swipe);
            while (_pending >= TrailStep && recipes != null)
            {
                float back = _pending - TrailStep;
                var at = local - d.normalized * back;
                foreach (var r in recipes) FxKit.I.Emit(layer, at, r);
                _pending -= TrailStep;
            }
            _last = local;
        }
    }
}
