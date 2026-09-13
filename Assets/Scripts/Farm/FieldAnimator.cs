using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>Drives every moving thing on every field from a single <c>Update</c>.
    ///
    /// The animations themselves are trivial — a sine on a rotation, a sine on a position, a
    /// looping scale. The cost was never the arithmetic, it was the dispatch: a component per
    /// moving part means Unity crosses the managed/native boundary once per part per frame, and
    /// at six islands that is 16 plots x 3 parts x 6 = 288 calls before any work happens. Early
    /// outs inside those components do not help, because the call is the expensive part.
    ///
    /// One registry, one call, and entries whose graphic is switched off are skipped without
    /// Unity ever having to ask them.</summary>
    public class FieldAnimator : MonoBehaviour
    {
        public static FieldAnimator I { get; private set; }

        public enum Motion
        {
            /// <summary>A plant leaning in the breeze — rotation about its base.</summary>
            Sway,
            /// <summary>A ripe badge nodding — vertical translation. "Collect me, no rush."</summary>
            Bob,
            /// <summary>A thirst ring expanding and fading — radial scale. "Hurry, this closes."
            /// Deliberately a different motion CLASS from Bob, so the two separate in peripheral
            /// vision across a whole island rather than merely differing in speed.</summary>
            Pulse,
            /// <summary>The selected plot's outline — a slow brightness swell that never fades
            /// out. "This is where the next seed goes."</summary>
            Breathe,
        }

        struct Entry
        {
            public RectTransform rt;
            public Graphic gfx;          // null means "always animate"
            public Motion motion;
            public float phase;
            public Vector2 home;
            public float alpha;
        }

        readonly List<Entry> _entries = new List<Entry>();

        void Awake() { if (I == null) I = this; }
        void OnDestroy() { if (I == this) I = null; }

        public static FieldAnimator Ensure(GameObject host)
        {
            if (I != null) return I;
            I = host.AddComponent<FieldAnimator>();
            return I;
        }

        /// <summary>Register a node. <paramref name="gate"/> is consulted each frame: when its
        /// graphic is disabled or its object inactive, the entry costs one branch and nothing
        /// else.</summary>
        public void Add(RectTransform rt, Motion motion, float phase, Graphic gate = null)
        {
            if (rt == null) return;
            _entries.Add(new Entry
            {
                rt = rt,
                gfx = gate,
                motion = motion,
                phase = phase,
                home = rt.anchoredPosition,
                alpha = gate != null ? gate.color.a : 1f,
            });
        }

        // Tuning kept identical to the per-object components these replaced, so the change is
        // invisible: sway 2.4 deg at 1.5 rad/s, bob 5 px at 3 rad/s, pulse 1.0 -> 1.18 over 1.2 s.
        const float SwayAmp = 2.4f, SwaySpeed = 1.5f;

        /// <summary>Multiplies crop sway. Set by the weather: wind and storm bend the field,
        /// drought nearly stills it. Presentation only — it changes no value.</summary>
        public static float SwayScale = 1f;
        const float BobAmp = 5f, BobSpeed = 3f;
        const float PulsePeriod = 1.2f, PulseFrom = 1.0f, PulseTo = 1.18f;

        void Update()
        {
            float t = Time.unscaledTime;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.rt == null) { _entries.RemoveAt(i); continue; }

                var go = e.rt.gameObject;
                if (!go.activeInHierarchy) continue;
                if (e.gfx != null && !e.gfx.enabled) continue;

                switch (e.motion)
                {
                    case Motion.Sway:
                        e.rt.localRotation = Quaternion.Euler(0f, 0f,
                            Mathf.Sin(t * SwaySpeed * Mathf.Lerp(1f, 1.8f, Mathf.Clamp01(SwayScale - 1f)) + e.phase) * SwayAmp * SwayScale);
                        break;

                    case Motion.Bob:
                        e.rt.anchoredPosition = e.home + new Vector2(0f, Mathf.Sin(t * BobSpeed + e.phase) * BobAmp);
                        break;

                    case Motion.Pulse:
                    {
                        float u = Mathf.Repeat(t / PulsePeriod + e.phase, 1f);
                        e.rt.localScale = Vector3.one * Mathf.Lerp(PulseFrom, PulseTo, u);
                        if (e.gfx != null)
                        {
                            var c = e.gfx.color;
                            c.a = e.alpha * (1f - u);
                            e.gfx.color = c;
                        }
                        break;
                    }

                    case Motion.Breathe:
                    {
                        float s = Mathf.Sin(t * 3.2f + e.phase) * 0.5f + 0.5f;
                        e.rt.localScale = Vector3.one * (1f + 0.025f * s);
                        if (e.gfx != null)
                        {
                            var c = e.gfx.color;
                            c.a = e.alpha * (0.6f + 0.4f * s);
                            e.gfx.color = c;
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>Put a bobbing node back where it started. The badge slot is shared with the
        /// thirsty droplet, which does not bob — without this it would inherit whatever offset
        /// the sine happened to be at when the plot stopped being ripe.</summary>
        public void Rest(RectTransform rt)
        {
            if (rt == null) return;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].rt == rt)
                {
                    if (_entries[i].motion == Motion.Bob) rt.anchoredPosition = _entries[i].home;
                    else if (_entries[i].motion == Motion.Pulse) rt.localScale = Vector3.one;
                    return;
                }
        }

        public int Count => _entries.Count;
    }
}
