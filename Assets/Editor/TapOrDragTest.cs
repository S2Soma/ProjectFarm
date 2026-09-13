using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LQFarm.EditorTools
{
    /// <summary>Guards the one rule that panning a map breaks.
    ///
    /// Unity's <c>Button</c> fires its click whenever press and release land on the same object,
    /// no matter how far the finger travelled between them — so with a pannable map, dragging
    /// across a plot plants on it. <see cref="TapOrDrag"/> exists solely to stop that, and this
    /// asserts it still does, including the two ways a platform can fail to send drag events at
    /// all (distance alone must be enough) and the press-then-think-better-of-it case.</summary>
    public static class TapOrDragTest
    {
        [MenuItem("Tools/LQ Farm/Kiểm tra chạm vs kéo")]
        public static void Run()
        {
            var fails = new List<string>();
            float now = 0f;
            var saveClock = TapOrDrag.Clock;
            TapOrDrag.Clock = () => now;

            try
            {
                // 1. A real tap: a few pixels of finger roll, lifted quickly.
                Check(fails, Simulate(ref now, from: new Vector2(400, 300), to: new Vector2(403, 302),
                                      withDragEvents: false, seconds: 0.09f) == 1,
                      "chạm thật (đi 3px, 0,09s) phải tính là chạm");

                // 2. THE regression: a 30 px drag that begins on the plot.
                Check(fails, Simulate(ref now, from: new Vector2(400, 300), to: new Vector2(430, 300),
                                      withDragEvents: true, seconds: 0.20f) == 0,
                      "kéo 30px bắt đầu trên ô đất KHÔNG được tính là chạm");

                // 3. Same travel, but the platform never sent drag callbacks. Distance alone has
                //    to reject it — relying on OnBeginDrag would make this a silent plant.
                Check(fails, Simulate(ref now, from: new Vector2(400, 300), to: new Vector2(430, 300),
                                      withDragEvents: false, seconds: 0.20f) == 0,
                      "đi 30px mà không có sự kiện drag vẫn KHÔNG được tính là chạm");

                // 4. Press, hold, think better of it, lift without moving.
                Check(fails, Simulate(ref now, from: new Vector2(400, 300), to: new Vector2(401, 300),
                                      withDragEvents: false, seconds: 0.80f) == 0,
                      "giữ 0,8s rồi thả KHÔNG được tính là chạm");

                // 5. Right at the slop boundary, still a tap.
                Check(fails, Simulate(ref now, from: new Vector2(400, 300), to: new Vector2(400 + TapOrDrag.TapSlop - 1, 300),
                                      withDragEvents: false, seconds: 0.10f) == 1,
                      $"đi {TapOrDrag.TapSlop - 1}px (sát ngưỡng) vẫn phải là chạm");

                // 6. One pixel past it, no longer a tap.
                Check(fails, Simulate(ref now, from: new Vector2(400, 300), to: new Vector2(400 + TapOrDrag.TapSlop + 2, 300),
                                      withDragEvents: false, seconds: 0.10f) == 0,
                      $"đi {TapOrDrag.TapSlop + 2}px (quá ngưỡng) KHÔNG được là chạm");
            }
            finally { TapOrDrag.Clock = saveClock; }

            if (fails.Count == 0) Debug.Log("Chạm vs kéo OK — 6/6 kiểm tra đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Chạm vs kéo: " + f);
                Debug.LogError($"Chạm vs kéo: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok) fails.Add(what); }

        /// <summary>Drive one press-move-release through the component and report how many taps
        /// it produced. No EventSystem needed: the handlers are called directly, which is also
        /// how a platform would call them.</summary>
        static int Simulate(ref float now, Vector2 from, Vector2 to, bool withDragEvents, float seconds)
        {
            var go = new GameObject("tapTest", typeof(RectTransform));
            var t = go.AddComponent<TapOrDrag>();
            int taps = 0;
            t.onTap = () => taps++;

            try
            {
                var e = new PointerEventData(null) { position = from, pointerId = 0 };
                t.OnPointerDown(e);

                if (withDragEvents)
                {
                    e.position = Vector2.Lerp(from, to, 0.5f);
                    t.OnBeginDrag(e);
                    e.position = to;
                    t.OnDrag(e);
                    t.OnEndDrag(e);
                }

                now += seconds;
                e.position = to;
                t.OnPointerUp(e);
            }
            finally { Object.DestroyImmediate(go); }

            return taps;
        }
    }
}
