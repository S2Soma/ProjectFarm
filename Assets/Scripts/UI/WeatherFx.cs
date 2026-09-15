using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>What the weather looks like on the map, not just on the HUD.
    ///
    /// Before this the only sign of a storm was a small purple glyph in the season bar: the sky
    /// stayed noon blue, the sun kept shining and the islands looked identical in all six
    /// states. Weather is the system that changes the value of every planting, so the world has
    /// to SHOW it — a player glancing at the farm should know it is raining before reading
    /// anything.
    ///
    /// Three layers, each with one job:
    ///   * light — the multipliers for sky, sea, clouds and sun; SkyView composes them with the
    ///             time of day (this class no longer paints the background itself);
    ///   * land  — island tint and snow cover, which must move with the camera, so it lives in
    ///             ArchipelagoView and is only driven from here;
    ///   * air   — rain, snow, wind streaks, heat motes and lightning in their own layer above
    ///             the world and below the HUD, so weather falls across the islands but never
    ///             across a button.
    ///
    /// A change of hour cross-fades over <see cref="BlendSeconds"/>. Particles are a fixed pool
    /// of plain Images moved in ONE Update — the same rule FieldAnimator follows for plots.</summary>
    public class WeatherFx : MonoBehaviour
    {
        public struct Look
        {
            // air = the weather's tint at the RIM of the view (Art/fx/edge_wash). It was a flat wash over
            // the whole screen, and a snowy or rainy hour looked like a fogged-up phone screen.
            public Color sky, sea, hills, cloud, sun, air;
            public float sunAlpha, cloudSpeed, sway;
            /// <summary>Extra desaturation of the background, how much of the star field shows, and
            /// the least the lanterns are lit even by day (an overcast storm lights them).</summary>
            public float desat, stars, lanternMin;
        }

        static Look LookOf(Weather w)
        {
            switch (w)
            {
                case Weather.Rain: return new Look {
                    sky = new Color(0.66f, 0.76f, 0.84f), sea = new Color(0.72f, 0.82f, 0.88f),
                    hills = new Color(0.70f, 0.78f, 0.86f), cloud = new Color(0.80f, 0.84f, 0.90f),
                    sun = Color.white, sunAlpha = 0.0f, air = new Color(0.20f, 0.30f, 0.42f, 0.35f),
                    cloudSpeed = 1.3f, sway = 1.2f, desat = 0.45f, stars = 0f, lanternMin = 0.35f };
                case Weather.Storm: return new Look {
                    sky = new Color(0.42f, 0.46f, 0.60f), sea = new Color(0.50f, 0.58f, 0.70f),
                    hills = new Color(0.46f, 0.52f, 0.64f), cloud = new Color(0.56f, 0.58f, 0.68f),
                    sun = Color.white, sunAlpha = 0.0f, air = new Color(0.10f, 0.12f, 0.24f, 0.55f),
                    cloudSpeed = 2.4f, sway = 2.6f, desat = 0.60f, stars = 0f, lanternMin = 0.50f };
                case Weather.Snow: return new Look {
                    sky = new Color(0.86f, 0.92f, 1.00f), sea = new Color(0.84f, 0.92f, 1.00f),
                    hills = new Color(0.92f, 0.96f, 1.00f), cloud = Color.white,
                    sun = new Color(0.95f, 0.97f, 1f), sunAlpha = 0.35f, air = new Color(0.90f, 0.95f, 1f, 0.45f),
                    cloudSpeed = 0.6f, sway = 0.6f, desat = 0.20f, stars = 0.30f };
                case Weather.Drought: return new Look {
                    sky = new Color(1.00f, 0.88f, 0.70f), sea = new Color(0.98f, 0.90f, 0.76f),
                    hills = new Color(1.00f, 0.86f, 0.70f), cloud = new Color(1f, 0.94f, 0.84f),
                    sun = new Color(1f, 0.78f, 0.50f), sunAlpha = 1.0f, air = new Color(1f, 0.62f, 0.22f, 0.30f),
                    cloudSpeed = 0.35f, sway = 0.4f, stars = 0.8f };
                case Weather.Wind: return new Look {
                    sky = new Color(0.94f, 0.98f, 1.00f), sea = new Color(0.94f, 0.98f, 1.00f),
                    hills = Color.white, cloud = Color.white,
                    sun = Color.white, sunAlpha = 0.85f, air = new Color(1f, 1f, 1f, 0f),
                    cloudSpeed = 3.2f, sway = 2.2f, stars = 1f };
                default: return new Look {
                    sky = Color.white, sea = Color.white, hills = Color.white, cloud = Color.white,
                    sun = Color.white, sunAlpha = 1.0f, air = new Color(1, 1, 1, 0),
                    cloudSpeed = 1.0f, sway = 1.0f, stars = 1f };
            }
        }

        public const float BlendSeconds = 2.2f;

        ArchipelagoView _world;
        RectTransform _air;
        Image _wash, _flash;

        /// <summary>The weather's multipliers as blended right now, for SkyView to compose with the
        /// time of day. <see cref="SunScale"/> is the drought's bigger, hotter sun.</summary>
        public Look Blended { get; private set; } = LookOf(Weather.Sunny);
        public float SunScale { get; private set; } = 1f;
        /// <summary>True while a change of weather is cross-fading, so the sky repaints every frame.</summary>
        public bool Blending => _t < 1f;
        /// <summary>The state being blended toward — for the night-specific touches.</summary>
        public Weather Target => _to;
        /// <summary>The air tint as it falls on the middle of the screen, where the farm is — which is
        /// what the readability floor has to count. The edge wash is clear there.</summary>
        public Color Wash => _wash != null ? _wash.color.Alpha(0f) : new Color(1, 1, 1, 0);
        /// <summary>The air layer, over the world and under the HUD (fireflies live here).</summary>
        public RectTransform Air => _air;

        Weather _from = Weather.Sunny, _to = Weather.Sunny;
        float _t = 1f;
        bool _started;

        // ---- particles ----
        // Each particle has a DEPTH, 0 far to 1 near, and everything about it follows: size, speed,
        // opacity, sway. One flat sheet of identical drops read as scratches on the glass; three
        // depths read as weather falling through air between you and the islands.
        enum Kind { Rain, Snow, Flake, Streak, Mote, Leaf, Petal, Splash, Pollen, Shimmer }
        class P
        {
            public RectTransform rt; public Image im;
            public Vector2 pos, vel; public float life, maxLife, phase, depth, size, rot, spin, baseA;
            public Kind kind;
        }
        readonly List<P> _pool = new List<P>();
        const int PoolSize = 200;
        float _nextFlash = 6f;
        Image _bolt, _rays;
        float _gust;

        public void Init(RectTransform airLayer, ArchipelagoView world)
        {
            _world = world;
            _air = airLayer;

            _wash = UIKit.Img(_air, Art.Load("Art/fx/edge_wash"), new Color(1, 1, 1, 0), "wash");
            _wash.rectTransform.Stretch(-40, -40, -40, -40);
            _wash.raycastTarget = false;

            // sun shafts from the top corner on a clear or parched day
            _rays = UIKit.Img(_air, Art.Load("Art/fx/mut_rays"), new Color(1f, 0.95f, 0.8f, 0f), "rays");
            _rays.raycastTarget = false;
            _rays.material = MutationTint.AdditiveMaterial;
            _rays.rectTransform.anchorMin = _rays.rectTransform.anchorMax = new Vector2(0f, 1f);
            _rays.rectTransform.sizeDelta = new Vector2(1500f, 1500f);
            _rays.rectTransform.anchoredPosition = new Vector2(80f, -40f);

            for (int i = 0; i < PoolSize; i++)
            {
                var im = UIKit.Img(_air, Theme.Circle(), Color.white, "p");
                im.raycastTarget = false;
                im.rectTransform.anchorMin = im.rectTransform.anchorMax = new Vector2(0, 0);
                im.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                im.enabled = false;
                _pool.Add(new P { rt = im.rectTransform, im = im, life = -1f });
            }

            _bolt = UIKit.Img(_air, null, new Color(0.8f, 0.88f, 1f, 0f), "bolt");
            _bolt.raycastTarget = false;
            _bolt.material = MutationTint.AdditiveMaterial;
            _bolt.rectTransform.anchorMin = _bolt.rectTransform.anchorMax = new Vector2(0f, 1f);
            _bolt.rectTransform.pivot = new Vector2(0.5f, 1f);
            _bolt.enabled = false;

            _flash = UIKit.Img(_air, null, new Color(1, 1, 1, 0), "flash");
            _flash.rectTransform.Stretch(-40, -40, -40, -40);
            _flash.raycastTarget = false;
        }

        /// <summary>Set the weather. The first call applies instantly; later changes blend.</summary>
        public void SetWeather(Weather w)
        {
            // a preview holds until released — otherwise the game's once-a-second weather check
            // blends it straight back to the real hour, mid screenshot
            if (_held) return;
            if (!_started)
            {
                _started = true;
                _from = _to = w; _t = 1f;
                Respawn();
                ApplyBlend();
                return;
            }
            if (w == _to) return;
            _from = CurrentBlendTarget();
            _to = w;
            _t = 0f;
            Respawn();
        }

        /// <summary>End a preview and blend back to the real hour.</summary>
        public void ReleaseWeather(Weather real)
        {
            _held = false;
            SetWeather(real);
        }

        Weather CurrentBlendTarget() { return _t >= 0.5f ? _to : _from; }

        bool _held;

        /// <summary>For the screenshot pass and the dev menu: jump straight to a state and hold
        /// it until <see cref="ReleaseWeather"/>.</summary>
        public void ForceWeather(Weather w)
        {
            _held = true;
            _started = true;
            _from = _to = w; _t = 1f;
            Respawn();
            ApplyBlend();
        }

        void Update()
        {
            if (!_started || _air == null) return;
            float dt = Time.unscaledDeltaTime;

            if (_t < 1f)
            {
                _t = Mathf.Min(1f, _t + dt / BlendSeconds);
                ApplyBlend();
            }

            StepParticles(dt);
            StepLightning(dt);
        }

        static Look Mix(Look a, Look b, float e)
        {
            return new Look
            {
                sky = Color.Lerp(a.sky, b.sky, e), sea = Color.Lerp(a.sea, b.sea, e),
                hills = Color.Lerp(a.hills, b.hills, e), cloud = Color.Lerp(a.cloud, b.cloud, e),
                sun = Color.Lerp(a.sun, b.sun, e), air = Color.Lerp(a.air, b.air, e),
                sunAlpha = Mathf.Lerp(a.sunAlpha, b.sunAlpha, e), cloudSpeed = Mathf.Lerp(a.cloudSpeed, b.cloudSpeed, e),
                sway = Mathf.Lerp(a.sway, b.sway, e), desat = Mathf.Lerp(a.desat, b.desat, e),
                stars = Mathf.Lerp(a.stars, b.stars, e), lanternMin = Mathf.Lerp(a.lanternMin, b.lanternMin, e),
            };
        }

        void ApplyBlend()
        {
            float e = _t * _t * (3f - 2f * _t);
            var look = Mix(LookOf(_from), LookOf(_to), e);
            Blended = look;
            SunScale = Mathf.Lerp(_from == Weather.Drought ? 1.35f : 1f, _to == Weather.Drought ? 1.35f : 1f, e);

            _wash.color = look.air;
            FieldAnimator.SwayScale = look.sway;

            if (_world != null) _world.SetWeatherLook(_from, _to, e);
        }

        // ============================================================
        // particles
        // ============================================================
        void Respawn()
        {
            foreach (var p in _pool) { p.im.enabled = false; p.life = -1f; }
            int n = 0;
            foreach (var (kind, count) in Recipe(_to))
                for (int k = 0; k < count && n < _pool.Count; k++, n++)
                    Spawn(_pool[n], kind, randomY: true);
        }

        /// <summary>What falls, blows or floats in each weather, and how many of each.</summary>
        static (Kind kind, int count)[] Recipe(Weather w)
        {
            switch (w)
            {
                case Weather.Rain:    return new[] { (Kind.Rain, 80), (Kind.Splash, 14) };
                case Weather.Storm:   return new[] { (Kind.Rain, 130), (Kind.Splash, 20), (Kind.Leaf, 8) };
                case Weather.Snow:    return new[] { (Kind.Snow, 70), (Kind.Flake, 16) };
                case Weather.Wind:    return new[] { (Kind.Streak, 26), (Kind.Leaf, 22), (Kind.Petal, 12) };
                case Weather.Drought: return new[] { (Kind.Mote, 30), (Kind.Shimmer, 6) };
                case Weather.Sunny:   return new[] { (Kind.Pollen, 16) };
                default:              return new (Kind, int)[0];
            }
        }

        /// <summary>Far particles are common, near ones rare: the eye needs many small ones to read
        /// distance and only a few big ones to feel close.</summary>
        static float PickDepth()
        {
            float r = Random.value;
            return r < 0.55f ? Random.Range(0.05f, 0.4f) : r < 0.87f ? Random.Range(0.4f, 0.72f) : Random.Range(0.72f, 1f);
        }

        void Spawn(P p, Kind kind, bool randomY)
        {
            var size = _air.rect.size;
            float W = Mathf.Max(1f, size.x), H = Mathf.Max(1f, size.y);
            p.kind = kind;
            p.phase = Random.value * 6.28f;
            p.depth = PickDepth();
            p.rot = 0f; p.spin = 0f;
            p.life = p.maxLife = 1f;
            p.im.enabled = true;
            p.im.type = Image.Type.Simple;
            p.im.material = null;
            p.im.preserveAspect = false;
            p.rt.localRotation = Quaternion.identity;
            float d = p.depth;
            bool storm = _to == Weather.Storm;

            switch (kind)
            {
                case Kind.Rain:
                {
                    p.im.sprite = Art.Load("Art/fx/p_rain");
                    float len = Mathf.Lerp(16f, 54f, d) * (storm ? 1.25f : 1f);
                    p.rt.sizeDelta = new Vector2(Mathf.Lerp(3f, 7f, d), len);
                    float fall = Mathf.Lerp(700f, 1500f, d) * (storm ? 1.3f : 1f);
                    p.vel = new Vector2((storm ? -0.30f : -0.10f) * fall, -fall);
                    p.rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(p.vel.x, -p.vel.y) * Mathf.Rad2Deg);
                    p.baseA = Mathf.Lerp(0.30f, 0.78f, d);
                    p.im.color = new Color(0.86f, 0.93f, 1f, 0f);
                    p.pos = new Vector2(Random.Range(0f, W + 300f), randomY ? Random.Range(0f, H) : H + 60f);
                    break;
                }
                case Kind.Splash:
                {
                    p.im.sprite = Art.Load("Art/fx/p_ring");
                    p.size = Mathf.Lerp(14f, 34f, d);
                    p.maxLife = p.life = Random.Range(0.25f, 0.45f);
                    p.life = randomY ? Random.Range(0f, p.maxLife) : p.maxLife;
                    p.vel = Vector2.zero;
                    p.baseA = Mathf.Lerp(0.25f, 0.6f, d);
                    p.im.color = new Color(0.9f, 0.96f, 1f, 0f);
                    // the ground is the lower part of the view; nearer splashes lower on screen
                    p.pos = new Vector2(Random.Range(0f, W), Mathf.Lerp(H * 0.62f, H * 0.06f, d) + Random.Range(-H * 0.08f, H * 0.08f));
                    break;
                }
                case Kind.Snow:
                {
                    p.im.sprite = Theme.Circle();
                    float sz = Mathf.Lerp(3f, 9f, d);
                    p.rt.sizeDelta = new Vector2(sz, sz);
                    p.vel = new Vector2(Random.Range(-14f, 14f), -Mathf.Lerp(28f, 95f, d));
                    p.baseA = Mathf.Lerp(0.35f, 0.95f, d);
                    p.im.color = new Color(1f, 1f, 1f, 0f);
                    p.pos = new Vector2(Random.Range(0f, W), randomY ? Random.Range(0f, H) : H + 20f);
                    break;
                }
                case Kind.Flake:
                {
                    // the few flakes right in front of you: big, soft, turning
                    p.im.sprite = Art.Load("Art/fx/p_flake");
                    p.depth = d = Random.Range(0.8f, 1f);
                    float sz = Random.Range(20f, 36f);
                    p.rt.sizeDelta = new Vector2(sz, sz);
                    p.vel = new Vector2(Random.Range(-20f, 20f), -Random.Range(90f, 140f));
                    p.spin = Random.Range(-50f, 50f);
                    p.baseA = Random.Range(0.55f, 0.85f);
                    p.im.color = new Color(1f, 1f, 1f, 0f);
                    p.pos = new Vector2(Random.Range(0f, W), randomY ? Random.Range(0f, H) : H + 40f);
                    break;
                }
                case Kind.Streak:
                {
                    p.im.sprite = Theme.Round(6);
                    p.im.type = Image.Type.Sliced;
                    p.rt.sizeDelta = new Vector2(Mathf.Lerp(60f, 220f, d), Mathf.Lerp(2f, 4.5f, d));
                    p.vel = new Vector2(Mathf.Lerp(500f, 1150f, d), Random.Range(-30f, 10f));
                    p.baseA = Mathf.Lerp(0.12f, 0.38f, d);
                    p.im.color = new Color(1f, 1f, 1f, 0f);
                    p.pos = new Vector2(randomY ? Random.Range(-200f, W) : -240f, Random.Range(H * 0.08f, H * 0.9f));
                    break;
                }
                case Kind.Leaf:
                case Kind.Petal:
                {
                    p.im.sprite = Art.Load(kind == Kind.Leaf ? "Art/items/particle_leaf" : "Art/items/particle_petal");
                    p.im.preserveAspect = true;
                    float sz = Mathf.Lerp(12f, 30f, d);
                    p.rt.sizeDelta = new Vector2(sz, sz);
                    p.vel = new Vector2(Mathf.Lerp(260f, 720f, d) * (storm ? 1.3f : 1f), Random.Range(-60f, 30f));
                    p.spin = Random.Range(180f, 420f) * (Random.value < 0.5f ? -1f : 1f);
                    p.baseA = Mathf.Lerp(0.55f, 1f, d);
                    p.im.color = new Color(1f, 1f, 1f, 0f);
                    p.pos = new Vector2(randomY ? Random.Range(-100f, W) : -60f, Random.Range(H * 0.12f, H * 0.92f));
                    break;
                }
                case Kind.Mote:
                case Kind.Pollen:
                {
                    p.im.sprite = Art.Load("Art/fx/p_dot");
                    p.im.material = MutationTint.AdditiveMaterial;
                    bool pollen = kind == Kind.Pollen;
                    float sz = pollen ? Mathf.Lerp(4f, 12f, d) : Mathf.Lerp(6f, 24f, d);
                    p.rt.sizeDelta = new Vector2(sz, sz);
                    p.vel = pollen ? new Vector2(Random.Range(-12f, 18f), Random.Range(-6f, 10f))
                                   : new Vector2(Random.Range(-10f, 10f), Mathf.Lerp(12f, 42f, d));
                    p.maxLife = p.life = pollen ? Random.Range(5f, 9f) : Random.Range(4f, 8f);
                    if (randomY) p.life = Random.Range(0.2f, 1f) * p.maxLife;
                    p.baseA = pollen ? Mathf.Lerp(0.18f, 0.55f, d) : Mathf.Lerp(0.12f, 0.45f, d);
                    p.im.color = pollen ? new Color(1f, 0.97f, 0.75f, 0f) : new Color(1f, 0.82f, 0.5f, 0f);
                    p.pos = new Vector2(Random.Range(0f, W), pollen ? Random.Range(H * 0.1f, H * 0.95f)
                                                               : (randomY ? Random.Range(0f, H * 0.7f) : Random.Range(0f, H * 0.3f)));
                    break;
                }
                case Kind.Shimmer:
                {
                    // heat haze: a wide, nearly invisible warm band rising and wavering
                    p.im.sprite = Art.Load("Art/fx/p_dot");
                    p.im.material = MutationTint.AdditiveMaterial;
                    p.rt.sizeDelta = new Vector2(Random.Range(W * 0.4f, W * 0.8f), Random.Range(40f, 90f));
                    p.vel = new Vector2(Random.Range(-6f, 6f), Random.Range(10f, 22f));
                    p.maxLife = p.life = Random.Range(6f, 10f);
                    if (randomY) p.life = Random.Range(0.2f, 1f) * p.maxLife;
                    p.baseA = Random.Range(0.05f, 0.10f);
                    p.im.color = new Color(1f, 0.75f, 0.45f, 0f);
                    p.pos = new Vector2(Random.Range(W * 0.2f, W * 0.8f), Random.Range(0f, H * 0.5f));
                    break;
                }
            }
            p.rt.anchoredPosition = p.pos;
        }

        void StepParticles(float dt)
        {
            var size = _air.rect.size;
            float W = size.x, H = size.y;
            float now = Time.unscaledTime;
            // particles fade in with the blend, so a new state's rain does not start at full force
            float strength = Mathf.Clamp01(_t * 1.4f);
            // wind comes in gusts
            _gust = 0.65f + 0.6f * Mathf.Pow(Mathf.Sin(now * 0.55f) * 0.5f + 0.5f, 2f);
            float night = DayCycle.Sample(DayCycle.Hour).night;

            foreach (var p in _pool)
            {
                if (p.life < 0f) continue;
                float gust = (p.kind == Kind.Streak || p.kind == Kind.Leaf || p.kind == Kind.Petal) ? _gust : 1f;
                p.pos += p.vel * gust * dt;
                float a = p.baseA;

                switch (p.kind)
                {
                    case Kind.Rain:
                        if (p.pos.y < -60f || p.pos.x < -80f) Spawn(p, Kind.Rain, randomY: false);
                        break;
                    case Kind.Splash:
                    {
                        p.life -= dt;
                        float k = 1f - Mathf.Clamp01(p.life / p.maxLife);
                        float sz = p.size * (0.3f + 0.9f * k);
                        p.rt.sizeDelta = new Vector2(sz, sz * 0.38f);
                        a *= 1f - k;
                        if (p.life <= 0f) Spawn(p, Kind.Splash, randomY: false);
                        break;
                    }
                    case Kind.Snow:
                        p.pos.x += Mathf.Sin(now * 1.2f + p.phase) * Mathf.Lerp(10f, 30f, p.depth) * dt;
                        if (p.pos.y < -20f) Spawn(p, Kind.Snow, randomY: false);
                        break;
                    case Kind.Flake:
                        p.pos.x += Mathf.Sin(now * 0.9f + p.phase) * 40f * dt;
                        p.rot += p.spin * dt;
                        p.rt.localRotation = Quaternion.Euler(0, 0, p.rot);
                        if (p.pos.y < -40f) Spawn(p, Kind.Flake, randomY: false);
                        break;
                    case Kind.Streak:
                    {
                        float u = Mathf.Clamp01((p.pos.x + 240f) / (W + 480f));
                        a *= Mathf.Sin(u * Mathf.PI) * gust;
                        if (p.pos.x > W + 240f) Spawn(p, Kind.Streak, randomY: false);
                        break;
                    }
                    case Kind.Leaf:
                    case Kind.Petal:
                        p.pos.y += Mathf.Sin(now * 3.1f + p.phase) * Mathf.Lerp(30f, 90f, p.depth) * dt;
                        p.rot += p.spin * dt;
                        p.rt.localRotation = Quaternion.Euler(0, 0, p.rot);
                        if (p.pos.x > W + 60f) Spawn(p, p.kind, randomY: false);
                        break;
                    case Kind.Mote:
                    case Kind.Pollen:
                    case Kind.Shimmer:
                    {
                        p.life -= dt;
                        float k = Mathf.Clamp01(p.life / p.maxLife);
                        a *= Mathf.Sin(k * Mathf.PI);
                        if (p.kind == Kind.Pollen)
                        {
                            a *= 0.7f + 0.3f * Mathf.Sin(now * 3f + p.phase);
                            a *= 1f - night * 0.85f;                       // fireflies take over at night
                        }
                        p.pos.x += Mathf.Sin(now * (p.kind == Kind.Shimmer ? 0.6f : 0.9f) + p.phase) * (p.kind == Kind.Shimmer ? 20f : 12f) * dt;
                        if (p.life <= 0f || p.pos.y > H + 40f) Spawn(p, p.kind, randomY: false);
                        break;
                    }
                }

                var c = p.im.color; c.a = a * strength; p.im.color = c;
                p.rt.anchoredPosition = p.pos;
            }

            // sun shafts: clear or parched daytime only, fading with the blend
            if (_rays != null)
            {
                bool sunny = _to == Weather.Sunny || _to == Weather.Drought;
                float target = sunny ? (1f - night) * (_to == Weather.Drought ? 0.16f : 0.11f) * strength : 0f;
                var rc = _rays.color; rc.a = Mathf.MoveTowards(rc.a, target, dt * 0.2f); _rays.color = rc;
                _rays.enabled = rc.a > 0.002f;
                if (_rays.enabled) _rays.rectTransform.localRotation = Quaternion.Euler(0, 0, -now * 1.5f);
            }
        }

        void StepLightning(float dt)
        {
            if (_flash == null) return;
            var c = _flash.color;
            c.a = Mathf.Max(0f, c.a - dt * 2.8f);
            var bc = _bolt.color;
            bc.a = Mathf.Max(0f, bc.a - dt * 3.2f);

            if (_to == Weather.Storm && _t >= 1f)
            {
                _nextFlash -= dt;
                if (_nextFlash <= 0f)
                {
                    c.a = Random.Range(0.30f, 0.55f);
                    // a bolt somewhere across the sky, most of the times it flashes
                    if (Random.value < 0.8f)
                    {
                        var size = _air.rect.size;
                        _bolt.sprite = Art.Load("Art/fx/p_bolt_" + Random.Range(0, 3));
                        float h = size.y * Random.Range(0.55f, 0.85f);
                        _bolt.rectTransform.sizeDelta = new Vector2(h * 0.4f, h);
                        _bolt.rectTransform.anchoredPosition = new Vector2(Random.Range(size.x * 0.12f, size.x * 0.88f), 20f);
                        _bolt.rectTransform.localScale = new Vector3(Random.value < 0.5f ? -1f : 1f, 1f, 1f);
                        bc.a = 1f;
                    }
                    _nextFlash = Random.Range(4.5f, 11f);
                }
            }
            _flash.color = c;
            _bolt.color = bc;
            _bolt.enabled = bc.a > 0.01f;
        }
    }
}
