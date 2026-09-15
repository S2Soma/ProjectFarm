using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>One kind of particle burst: what the particles look like and how they move.
    /// Sizes and speeds are in the units of the layer they are emitted into.</summary>
    public class FxRecipe
    {
        public string[] sprites;              // Art.Load paths; one picked per particle
        public Color[] colors = { Color.white };
        public int count = 10;
        public Vector2 size = new Vector2(16, 26);
        public Vector2 speed = new Vector2(80, 180);
        public float dir = 90f, spread = 360f;    // degrees: 90 is up
        public float gravity = -260f;            // added to vertical speed per second
        public float drag = 0.8f;                // share of speed lost per second
        public Vector2 life = new Vector2(0.6f, 1.1f);
        public float spin = 240f;                // max degrees per second either way
        public float wobble;                     // sideways sway amplitude
        public float radius;                     // spawn scatter around the point
        public Vector2 offset;                   // spawn centre relative to the point
        public float grow = 1f;                  // size multiplier reached at the end of life
        public bool additive;
        public bool keepAspect = true;
    }

    /// <summary>Pooled UI particles for the cosmetics: planting, watering and harvest bursts on the
    /// field, taps and swipe trails over the screen.
    ///
    /// Each particle is one Image, recycled per layer, moved every frame by this one component
    /// instead of a coroutine each — a swipe trail can hold sixty at once.</summary>
    public class FxKit : MonoBehaviour
    {
        public static FxKit I { get; private set; }

        class Particle
        {
            public Image im;
            public RectTransform rt;
            public Vector2 pos, vel;
            public float age, life, spin, rot, size, grow, wobble, phase;
            public float gravity, drag;
            public Color col;
            public bool active;
        }

        readonly Dictionary<RectTransform, List<Particle>> _pools = new Dictionary<RectTransform, List<Particle>>();
        readonly List<Particle> _live = new List<Particle>();
        const int MaxPerLayer = 140;

        public static FxKit Ensure(GameObject host)
        {
            if (I == null) I = host.AddComponent<FxKit>();
            return I;
        }

        void OnDestroy() { if (I == this) I = null; }

        /// <summary>Burst <paramref name="r"/> at <paramref name="at"/> (layer-local), scaled by
        /// <paramref name="scale"/>. Silently does nothing if the layer is gone.</summary>
        public void Emit(RectTransform layer, Vector2 at, FxRecipe r, float scale = 1f, int countOverride = -1)
        {
            if (layer == null || r == null || r.sprites == null || r.sprites.Length == 0) return;
            int n = countOverride >= 0 ? countOverride : r.count;
            for (int k = 0; k < n; k++)
            {
                var p = Take(layer);
                if (p == null) return;
                var sp = Art.Load(r.sprites[Random.Range(0, r.sprites.Length)]);
                p.im.sprite = sp;
                p.im.material = r.additive ? MutationTint.AdditiveMaterial : null;
                p.im.preserveAspect = r.keepAspect;
                p.col = r.colors[Random.Range(0, r.colors.Length)];
                float a = (r.dir + Random.Range(-r.spread * 0.5f, r.spread * 0.5f)) * Mathf.Deg2Rad;
                float v = Random.Range(r.speed.x, r.speed.y) * scale;
                p.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * v;
                p.pos = at + r.offset * scale + Random.insideUnitCircle * r.radius * scale;
                p.life = Random.Range(r.life.x, r.life.y);
                p.age = 0f;
                p.size = Random.Range(r.size.x, r.size.y) * scale;
                p.grow = r.grow;
                p.spin = Random.Range(-r.spin, r.spin);
                p.rot = Random.Range(0f, 360f) * (r.spin > 0 ? 1f : 0f);
                p.wobble = r.wobble * scale;
                p.phase = Random.value * 6.28f;
                p.gravity = r.gravity * scale;
                p.drag = r.drag;
                p.active = true;
                p.rt.SetAsLastSibling();
                p.im.enabled = true;
                Step(p, 0f);
                _live.Add(p);
            }
        }

        Particle Take(RectTransform layer)
        {
            if (!_pools.TryGetValue(layer, out var pool)) _pools[layer] = pool = new List<Particle>();
            foreach (var p in pool) if (!p.active && p.im != null) return p;
            if (pool.Count >= MaxPerLayer)
            {
                // steal the oldest live one rather than grow without bound
                Particle oldest = null;
                foreach (var p in pool) if (oldest == null || p.age / p.life > oldest.age / oldest.life) oldest = p;
                if (oldest != null) _live.Remove(oldest);
                return oldest;
            }
            var im = UIKit.Img(layer, null, Color.white, "fx");
            im.raycastTarget = false;
            im.rectTransform.anchorMin = im.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            var np = new Particle { im = im, rt = im.rectTransform };
            pool.Add(np);
            return np;
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                if (p.im == null) { _live.RemoveAt(i); continue; }
                p.age += dt;
                if (p.age >= p.life)
                {
                    p.active = false;
                    p.im.enabled = false;
                    _live.RemoveAt(i);
                    continue;
                }
                p.vel.y += p.gravity * dt;
                p.vel *= Mathf.Max(0f, 1f - p.drag * dt);
                p.pos += p.vel * dt;
                p.rot += p.spin * dt;
                Step(p, dt);
            }
        }

        static void Step(Particle p, float dt)
        {
            float t = Mathf.Clamp01(p.age / Mathf.Max(0.01f, p.life));
            float pop = Mathf.Min(1f, t / 0.12f);
            float fade = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            float s = p.size * Mathf.Lerp(1f, p.grow, t) * (0.6f + 0.4f * pop);
            p.rt.sizeDelta = new Vector2(s, s);
            float sway = p.wobble != 0f ? Mathf.Sin(p.age * 5f + p.phase) * p.wobble : 0f;
            p.rt.anchoredPosition = p.pos + new Vector2(sway, 0f);
            p.rt.localRotation = Quaternion.Euler(0, 0, p.rot);
            var c = p.col; c.a *= fade * pop;
            p.im.color = c;
        }
    }

    /// <summary>The recipes behind every effect cosmetic, by id. Sizes are in field units for the
    /// plot effects (IslandView scales them by PlotScale) and canvas units for taps and swipes.</summary>
    public static class CosmeticFx
    {
        static readonly Color[] Party =
        {
            new Color(1f, 0.42f, 0.42f), new Color(1f, 0.83f, 0.36f), new Color(0.42f, 0.82f, 1f),
            new Color(0.73f, 0.55f, 1f), new Color(0.49f, 0.94f, 0.56f),
        };

        const string Star = "Art/fx/mut_spark", Dot = "Art/fx/p_dot", Bubble = "Art/fx/p_bubble", Heart = "Art/fx/p_heart",
                     Confetti = "Art/fx/p_confetti", Diamond = "Art/fx/p_diamond", Ring = "Art/fx/p_ring",
                     Rainbow = "Art/fx/p_rainbow", Petal = "Art/items/particle_petal", Leaf = "Art/items/particle_leaf",
                     Coin = "Art/items/coin", Drop = "Art/gen/droplet";

        static readonly Dictionary<string, FxRecipe[]> Table = new Dictionary<string, FxRecipe[]>
        {
            // ---- planting ----
            { "fx_petals", new[] { new FxRecipe { sprites = new[] { Petal }, count = 9, size = new Vector2(18, 26), speed = new Vector2(80, 150), dir = 90, spread = 150, gravity = -160, life = new Vector2(0.9f, 1.3f), wobble = 8 } } },
            { "pl_stars", new[] { new FxRecipe { sprites = new[] { Star }, colors = new[] { new Color(1f, 0.96f, 0.7f), Color.white }, count = 12, size = new Vector2(18, 34), speed = new Vector2(90, 200), gravity = -40, drag = 2.2f, life = new Vector2(0.5f, 0.9f), spin = 120, offset = new Vector2(0, 20) } } },
            { "pl_sprout", new[] {
                new FxRecipe { sprites = new[] { Leaf }, count = 8, size = new Vector2(20, 28), speed = new Vector2(110, 170), dir = 90, spread = 360, gravity = -120, drag = 1.6f, life = new Vector2(0.7f, 1.0f), spin = 300 },
                new FxRecipe { sprites = new[] { Ring }, colors = new[] { new Color(0.6f, 1f, 0.6f, 0.9f) }, count = 1, size = new Vector2(40, 40), speed = Vector2.zero, gravity = 0, life = new Vector2(0.5f, 0.5f), spin = 0, grow = 4f, additive = true } } },
            { "pl_bubbles", new[] { new FxRecipe { sprites = new[] { Bubble }, count = 8, size = new Vector2(16, 30), speed = new Vector2(40, 90), dir = 90, spread = 80, gravity = 30, drag = 0.5f, life = new Vector2(1.0f, 1.6f), spin = 0, wobble = 10, radius = 26 } } },
            { "pl_firework", new[] {
                new FxRecipe { sprites = new[] { Star }, colors = Party, count = 22, size = new Vector2(22, 40), speed = new Vector2(200, 340), gravity = -220, drag = 1.8f, life = new Vector2(0.6f, 1.0f), spin = 180, offset = new Vector2(0, 60) },
                new FxRecipe { sprites = new[] { Dot }, colors = new[] { new Color(1f, 0.95f, 0.7f) }, count = 1, size = new Vector2(70, 70), speed = Vector2.zero, gravity = 0, life = new Vector2(0.35f, 0.35f), spin = 0, grow = 2.2f, additive = true, offset = new Vector2(0, 60) } } },

            // ---- watering ----
            { "wt_bubbles", new[] { new FxRecipe { sprites = new[] { Bubble }, count = 12, size = new Vector2(14, 30), speed = new Vector2(30, 90), dir = 90, spread = 120, gravity = 40, drag = 0.4f, life = new Vector2(1.1f, 1.7f), spin = 0, wobble = 12, radius = 34, offset = new Vector2(0, 30) } } },
            { "wt_flower", new[] { new FxRecipe { sprites = new[] { Petal }, count = 14, size = new Vector2(16, 24), speed = new Vector2(10, 40), dir = -90, spread = 40, gravity = -70, drag = 0.2f, life = new Vector2(1.1f, 1.5f), wobble = 14, radius = 50, offset = new Vector2(0, 150) } } },
            { "wt_diamond", new[] {
                new FxRecipe { sprites = new[] { Diamond }, colors = new[] { new Color(0.8f, 0.95f, 1f), Color.white }, count = 12, size = new Vector2(14, 26), speed = new Vector2(60, 140), dir = 90, spread = 160, gravity = -300, drag = 0.6f, life = new Vector2(0.7f, 1.1f), spin = 90, offset = new Vector2(0, 90) },
                new FxRecipe { sprites = new[] { Drop }, colors = new[] { new Color(0.55f, 0.85f, 1f) }, count = 6, size = new Vector2(12, 16), speed = new Vector2(20, 60), dir = -90, spread = 50, gravity = -500, life = new Vector2(0.5f, 0.7f), spin = 0, radius = 30, offset = new Vector2(0, 110) } } },
            { "wt_rainbow", new[] {
                new FxRecipe { sprites = new[] { Rainbow }, count = 1, size = new Vector2(190, 190), speed = new Vector2(10, 10), dir = 90, spread = 0, gravity = 0, drag = 0, life = new Vector2(1.4f, 1.4f), spin = 0, grow = 1.15f, offset = new Vector2(0, 95) },
                new FxRecipe { sprites = new[] { Drop }, colors = new[] { new Color(0.55f, 0.85f, 1f) }, count = 9, size = new Vector2(12, 16), speed = new Vector2(20, 60), dir = -90, spread = 40, gravity = -520, life = new Vector2(0.5f, 0.7f), spin = 0, radius = 40, offset = new Vector2(0, 120) } } },

            // ---- harvest ----
            { "fx_leaves", new[] { new FxRecipe { sprites = new[] { Leaf }, count = 9, size = new Vector2(20, 28), speed = new Vector2(100, 190), dir = 90, spread = 150, gravity = -200, life = new Vector2(0.9f, 1.3f), wobble = 6 } } },
            { "hv_confetti", new[] { new FxRecipe { sprites = new[] { Confetti }, colors = Party, count = 26, size = new Vector2(16, 26), speed = new Vector2(160, 280), dir = 90, spread = 110, gravity = -420, drag = 1.0f, life = new Vector2(0.9f, 1.4f), spin = 540, wobble = 6, offset = new Vector2(0, 40), keepAspect = true } } },
            { "hv_stars", new[] { new FxRecipe { sprites = new[] { Star }, colors = new[] { new Color(1f, 0.9f, 0.5f), Color.white }, count = 10, size = new Vector2(20, 36), speed = new Vector2(220, 360), dir = 90, spread = 70, gravity = -80, drag = 2.0f, life = new Vector2(0.6f, 0.9f), spin = 90, offset = new Vector2(0, 40) } } },
            { "hv_coins", new[] { new FxRecipe { sprites = new[] { Coin }, count = 10, size = new Vector2(20, 28), speed = new Vector2(160, 260), dir = 90, spread = 100, gravity = -600, drag = 0.4f, life = new Vector2(0.8f, 1.1f), spin = 0, offset = new Vector2(0, 40) } } },

            // ---- taps (canvas units) ----
            { "tp_ring", new[] {
                new FxRecipe { sprites = new[] { Ring }, colors = new[] { new Color(1f, 1f, 1f, 0.95f) }, count = 1, size = new Vector2(40, 40), speed = Vector2.zero, gravity = 0, life = new Vector2(0.45f, 0.45f), spin = 0, grow = 3.4f, additive = true },
                new FxRecipe { sprites = new[] { Dot }, colors = new[] { new Color(0.85f, 0.95f, 1f) }, count = 7, size = new Vector2(10, 16), speed = new Vector2(80, 140), gravity = 0, drag = 3f, life = new Vector2(0.3f, 0.5f), spin = 0, additive = true } } },
            { "tp_star", new[] { new FxRecipe { sprites = new[] { Star }, colors = new[] { new Color(1f, 0.95f, 0.6f), Color.white }, count = 6, size = new Vector2(28, 48), speed = new Vector2(90, 170), gravity = -60, drag = 2.5f, life = new Vector2(0.4f, 0.6f), spin = 200 } } },
            { "tp_heart", new[] { new FxRecipe { sprites = new[] { Heart }, colors = new[] { new Color(1f, 0.36f, 0.54f), new Color(1f, 0.62f, 0.75f) }, count = 5, size = new Vector2(26, 40), speed = new Vector2(50, 110), dir = 90, spread = 100, gravity = 60, drag = 1.2f, life = new Vector2(0.6f, 0.9f), spin = 30, wobble = 6 } } },
            { "tp_leaf", new[] { new FxRecipe { sprites = new[] { Leaf }, count = 6, size = new Vector2(26, 36), speed = new Vector2(60, 130), gravity = -140, drag = 1.4f, life = new Vector2(0.5f, 0.8f), spin = 360 } } },

            // ---- swipe trails, emitted a few at a time along the finger ----
            { "sw_petals", new[] { new FxRecipe { sprites = new[] { Petal }, count = 1, size = new Vector2(24, 36), speed = new Vector2(10, 40), gravity = -60, drag = 1f, life = new Vector2(0.6f, 0.9f), spin = 200, wobble = 5 } } },
            { "sw_stars", new[] { new FxRecipe { sprites = new[] { Star }, colors = new[] { new Color(1f, 0.95f, 0.65f), Color.white }, count = 1, size = new Vector2(22, 40), speed = new Vector2(5, 30), gravity = -20, drag = 2f, life = new Vector2(0.4f, 0.7f), spin = 120 } } },
            { "sw_rainbow", new[] { new FxRecipe { sprites = new[] { Heart }, colors = Party, count = 1, size = new Vector2(22, 34), speed = new Vector2(0, 30), gravity = 20, drag = 2f, life = new Vector2(0.55f, 0.85f), spin = 60, grow = 0.5f } } },
            { "sw_fairy", new[] {
                new FxRecipe { sprites = new[] { Diamond }, colors = new[] { new Color(1f, 0.95f, 0.8f), new Color(0.85f, 0.95f, 1f) }, count = 1, size = new Vector2(18, 32), speed = new Vector2(10, 50), gravity = -90, drag = 1f, life = new Vector2(0.6f, 1.0f), spin = 90 },
                new FxRecipe { sprites = new[] { Dot }, colors = new[] { new Color(1f, 0.92f, 0.6f, 0.8f) }, count = 1, size = new Vector2(36, 52), speed = Vector2.zero, gravity = 0, life = new Vector2(0.3f, 0.45f), spin = 0, grow = 0.2f, additive = true } } },
        };

        public static FxRecipe[] For(string id) { return id != null && Table.TryGetValue(id, out var r) ? r : null; }

        public static void Emit(RectTransform layer, Vector2 at, string id, float scale = 1f)
        {
            var rs = For(id);
            if (rs == null || FxKit.I == null) return;
            foreach (var r in rs) FxKit.I.Emit(layer, at, r, scale);
        }

        /// <summary>A plot skin's border sprite, or null for the plain bed.</summary>
        public static Sprite PlotSkin(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("pk_")) return null;
            return Art.Load("Art/beds/skin_" + id.Substring(3));
        }
    }
}
