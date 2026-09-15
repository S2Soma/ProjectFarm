using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>Small coroutine-driven animation helpers. Everything runs on unscaled time
    /// so the interface stays lively even if the game is paused.</summary>
    public class Tween : MonoBehaviour
    {
        static Tween _inst;
        public static Tween I
        {
            get
            {
                if (_inst == null)
                {
                    var go = new GameObject("~Tween");
                    DontDestroyOnLoad(go);
                    _inst = go.AddComponent<Tween>();
                }
                return _inst;
            }
        }

        public static float EaseOut(float t)  { return 1f - Mathf.Pow(1f - t, 3f); }
        public static float EaseBack(float t) { float s = 1.70158f; t -= 1f; return t * t * ((s + 1f) * t + s) + 1f; }

        public static Coroutine Run(IEnumerator co) { return I.StartCoroutine(co); }

        public static void Kill(Coroutine co) { if (co != null) I.StopCoroutine(co); }

        /// <summary>Pop a transform in from nothing with a slight overshoot.</summary>
        public static void PopIn(Transform t, float time = 0.22f, float from = 0.82f)
        {
            if (t == null) return;
            I.StartCoroutine(PopRoutine(t, time, from));
        }

        static IEnumerator PopRoutine(Transform t, float time, float from)
        {
            Vector3 target = Vector3.one;
            t.localScale = target * from;
            for (float e = 0; e < time; e += Time.unscaledDeltaTime)
            {
                if (t == null) yield break;
                float k = EaseBack(Mathf.Clamp01(e / time));
                t.localScale = Vector3.LerpUnclamped(target * from, target, k);
                yield return null;
            }
            if (t != null) t.localScale = target;
        }

        /// <summary>Fade a CanvasGroup.</summary>
        public static Coroutine Fade(CanvasGroup g, float to, float time = 0.16f, Action done = null)
        {
            if (g == null) return null;
            return I.StartCoroutine(FadeRoutine(g, to, time, done));
        }

        static IEnumerator FadeRoutine(CanvasGroup g, float to, float time, Action done)
        {
            float from = g.alpha;
            for (float e = 0; e < time; e += Time.unscaledDeltaTime)
            {
                if (g == null) yield break;
                g.alpha = Mathf.Lerp(from, to, EaseOut(Mathf.Clamp01(e / time)));
                yield return null;
            }
            if (g != null) g.alpha = to;
            done?.Invoke();
        }

        /// <summary>Roll a number up to its new value.</summary>
        public static void Count(Text label, long to, float time = 0.45f, string suffix = "")
        {
            if (label == null) return;
            I.StartCoroutine(CountRoutine(label, to, time, suffix));
        }

        static IEnumerator CountRoutine(Text label, long to, float time, string suffix)
        {
            // string.Replace("", "") THROWS (ArgumentException: oldValue cannot be zero length),
            // and the HUD calls this with no suffix — so every coin change killed the coroutine
            // on its first line and the balance on screen simply never moved after the first
            // render. Its only symptom was a stray console exception on every coin change.
            string digits = label.text.Replace(".", "").Replace(",", "");
            if (!string.IsNullOrEmpty(suffix)) digits = digits.Replace(suffix, "");
            long.TryParse(digits.Trim(), out long from);
            if (from == to) { label.text = Fmt.N(to) + suffix; yield break; }
            for (float e = 0; e < time; e += Time.unscaledDeltaTime)
            {
                if (label == null) yield break;
                float k = EaseOut(Mathf.Clamp01(e / time));
                label.text = Fmt.N((long)Mathf.Round(Mathf.Lerp(from, to, k))) + suffix;
                yield return null;
            }
            if (label != null) label.text = Fmt.N(to) + suffix;
        }

        /// <summary>Move a transform along an arc to a target, then destroy it.</summary>
        public static void FlyArc(RectTransform node, Vector2 from, Vector2 to, float time, float lift, Action onArrive = null,
                                  float delay = -1f)
        {
            I.StartCoroutine(FlyRoutine(node, from, to, time, lift, onArrive, delay));
        }

        /// <param name="delay">Negative picks a small random delay, so a handful of simultaneous
        /// arcs do not move in lockstep. A sweep passes an explicit one to stagger them in order.</param>
        static IEnumerator FlyRoutine(RectTransform node, Vector2 from, Vector2 to, float time, float lift, Action onArrive, float delay)
        {
            if (delay < 0f) delay = UnityEngine.Random.Range(0f, 0.12f);
            if (delay > 0f && node != null) node.localScale = Vector3.zero;
            yield return new WaitForSecondsRealtime(delay);
            for (float e = 0; e < time; e += Time.unscaledDeltaTime)
            {
                if (node == null) yield break;
                float k = Mathf.Clamp01(e / time);
                Vector2 p = Vector2.Lerp(from, to, EaseOut(k));
                p.y += Mathf.Sin(k * Mathf.PI) * lift;
                node.anchoredPosition = p;
                node.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.55f, k);
                yield return null;
            }
            onArrive?.Invoke();
            if (node != null) Destroy(node.gameObject);
        }

        /// <summary>Screen shake, in reference pixels.</summary>
        public static void Shake(RectTransform target, float amount = 6f, float time = 0.28f)
        {
            if (target == null) return;
            I.StartCoroutine(ShakeRoutine(target, amount, time));
        }

        static IEnumerator ShakeRoutine(RectTransform target, float amount, float time)
        {
            Vector2 home = target.anchoredPosition;
            for (float e = 0; e < time; e += Time.unscaledDeltaTime)
            {
                if (target == null) yield break;
                float k = 1f - e / time;
                target.anchoredPosition = home + new Vector2(
                    UnityEngine.Random.Range(-amount, amount) * k,
                    UnityEngine.Random.Range(-amount, amount) * k);
                yield return null;
            }
            if (target != null) target.anchoredPosition = home;
        }

        /// <summary>Stagger a pop-in across a container's children.</summary>
        public static void Stagger(Transform parent, float step = 0.06f)
        {
            I.StartCoroutine(StaggerRoutine(parent, step));
        }

        static IEnumerator StaggerRoutine(Transform parent, float step)
        {
            if (parent == null) yield break;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                c.localScale = Vector3.zero;
            }
            // The container can be destroyed between two steps (a reward card closed by a quick
            // tap): check it BEFORE the loop condition reads childCount, not after.
            for (int i = 0; parent != null && i < parent.childCount; i++)
            {
                PopIn(parent.GetChild(i), 0.26f, 0.4f);
                yield return new WaitForSecondsRealtime(step);
            }
        }

        /// <summary>Float a piece of text upward and fade it out.</summary>
        public static void FloatText(RectTransform node, float rise = 70f, float time = 1.0f)
        {
            I.StartCoroutine(FloatRoutine(node, rise, time));
        }

        static IEnumerator FloatRoutine(RectTransform node, float rise, float time)
        {
            Vector2 from = node.anchoredPosition;
            var g = node.gameObject.AddComponent<CanvasGroup>();
            for (float e = 0; e < time; e += Time.unscaledDeltaTime)
            {
                if (node == null) yield break;
                float k = Mathf.Clamp01(e / time);
                node.anchoredPosition = from + new Vector2(0, EaseOut(k) * rise);
                g.alpha = 1f - k * k;
                node.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.15f, EaseOut(Mathf.Min(1f, k * 3f)));
                yield return null;
            }
            if (node != null) Destroy(node.gameObject);
        }

        /// <summary>Spin / drift a particle outward and fade it.</summary>
        public static void Spark(RectTransform node, Vector2 drift, float time)
        {
            I.StartCoroutine(SparkRoutine(node, drift, time));
        }

        static IEnumerator SparkRoutine(RectTransform node, Vector2 drift, float time)
        {
            Vector2 from = node.anchoredPosition;
            var img = node.GetComponent<Image>();
            float delay = UnityEngine.Random.Range(0f, 0.16f);
            yield return new WaitForSecondsRealtime(delay);
            for (float e = 0; e < time; e += Time.unscaledDeltaTime)
            {
                if (node == null) yield break;
                float k = Mathf.Clamp01(e / time);
                node.anchoredPosition = from + drift * EaseOut(k);
                node.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.1f, k);
                if (img != null) img.color = new Color(img.color.r, img.color.g, img.color.b, 1f - k);
                yield return null;
            }
            if (node != null) Destroy(node.gameObject);
        }
    }

    /// <summary>Number and duration formatting, Vietnamese style.</summary>
    public static class Fmt
    {
        /// <summary>Vietnamese number style — "." between thousands, "," before decimals — built by
        /// hand rather than taken from the "vi-VN" culture. Culture tables are platform data: on an
        /// IL2CPP phone build a missing or trimmed table throws inside this static initialiser, and
        /// then EVERY number in the game fails to format. This cannot be missing.</summary>
        public static readonly System.Globalization.NumberFormatInfo Vi = new System.Globalization.NumberFormatInfo
        {
            NumberGroupSeparator = ".", NumberDecimalSeparator = ",", NumberGroupSizes = new[] { 3 },
            PercentGroupSeparator = ".", PercentDecimalSeparator = ",",
        };

        /// <summary>A multiplier as the HUD writes it: ×1,35 without trailing zeros.</summary>
        public static string Mul(float v) { return v.ToString("0.##", Vi); }

        public static string N(long n)  { return n.ToString("#,0", Vi); }
        public static string N(int n)   { return n.ToString("#,0", Vi); }

        /// <summary>Big amounts in words for tight spots (the HUD coin chip, a reward cell):
        /// 22.000.000.000 → "22 tỷ", 1.290.000 → "1,29 tr". Below ten million it is the full number.</summary>
        public static string Short(long n)
        {
            long a = n < 0 ? -n : n;
            if (a >= 1_000_000_000L) return (n / 1_000_000_000.0).ToString("0.##", Vi) + " tỷ";
            if (a >= 10_000_000L) return (n / 1_000_000.0).ToString("0.#", Vi) + " tr";
            return N(n);
        }
        /// <summary>Vietnamese decimal comma, trailing zeros dropped: 16%, 25,2%. The old
        /// "0.00" used the DEVICE culture, so one build printed 16.00% here and 16,00% on a
        /// Vietnamese phone, next to "×1,35" elsewhere on the same screen.</summary>
        public static string Pct(float v) { return (v * 100f).ToString("0.#", Vi) + "%"; }

        /// <summary>A duration or countdown: 24g / 2g 30p / 1g 5p / 20p / 3p 05s / 42s. A zero tail is
        /// dropped — crops now run from two minutes to a day, and "24g 0p" or "20p 00s" on a seed card
        /// read like a timer stuck at zero.</summary>
        public static string Time(int sec)
        {
            if (sec < 0) sec = 0;
            if (sec >= 3600)
            {
                int m = (sec % 3600) / 60;
                return (sec / 3600) + "g" + (m > 0 ? " " + m + "p" : "");
            }
            if (sec >= 60)
            {
                int s = sec % 60;
                return (sec / 60) + "p" + (s > 0 ? " " + s.ToString("00") + "s" : "");
            }
            return sec + "s";
        }
    }
}
