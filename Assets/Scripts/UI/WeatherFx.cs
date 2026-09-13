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
    ///   * light — sky, sea, hills, clouds and sun re-tinted per state (background layer);
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
        public struct Scene
        {
            public Image sky, sun, sunGlow, farHills, nearHills, water, vignette;
            public List<Image> clouds, swell;
            public RectTransform petals;
        }

        struct Look
        {
            public Color sky, sea, hills, cloud, sun, air;   // air = full-screen wash over the world
            public float sunAlpha, cloudSpeed, sway;
        }

        static Look LookOf(Weather w)
        {
            switch (w)
            {
                case Weather.Rain: return new Look {
                    sky = new Color(0.66f, 0.76f, 0.84f), sea = new Color(0.72f, 0.82f, 0.88f),
                    hills = new Color(0.70f, 0.78f, 0.86f), cloud = new Color(0.80f, 0.84f, 0.90f),
                    sun = Color.white, sunAlpha = 0.0f, air = new Color(0.20f, 0.30f, 0.42f, 0.12f),
                    cloudSpeed = 1.3f, sway = 1.2f };
                case Weather.Storm: return new Look {
                    sky = new Color(0.42f, 0.46f, 0.60f), sea = new Color(0.50f, 0.58f, 0.70f),
                    hills = new Color(0.46f, 0.52f, 0.64f), cloud = new Color(0.56f, 0.58f, 0.68f),
                    sun = Color.white, sunAlpha = 0.0f, air = new Color(0.10f, 0.12f, 0.24f, 0.26f),
                    cloudSpeed = 2.4f, sway = 2.6f };
                case Weather.Snow: return new Look {
                    sky = new Color(0.86f, 0.92f, 1.00f), sea = new Color(0.84f, 0.92f, 1.00f),
                    hills = new Color(0.92f, 0.96f, 1.00f), cloud = Color.white,
                    sun = new Color(0.95f, 0.97f, 1f), sunAlpha = 0.35f, air = new Color(0.85f, 0.92f, 1f, 0.10f),
                    cloudSpeed = 0.6f, sway = 0.6f };
                case Weather.Drought: return new Look {
                    sky = new Color(1.00f, 0.88f, 0.70f), sea = new Color(0.98f, 0.90f, 0.76f),
                    hills = new Color(1.00f, 0.86f, 0.70f), cloud = new Color(1f, 0.94f, 0.84f),
                    sun = new Color(1f, 0.78f, 0.50f), sunAlpha = 1.0f, air = new Color(1f, 0.62f, 0.22f, 0.10f),
                    cloudSpeed = 0.35f, sway = 0.4f };
                case Weather.Wind: return new Look {
                    sky = new Color(0.94f, 0.98f, 1.00f), sea = new Color(0.94f, 0.98f, 1.00f),
                    hills = Color.white, cloud = Color.white,
                    sun = Color.white, sunAlpha = 0.85f, air = new Color(1f, 1f, 1f, 0f),
                    cloudSpeed = 3.2f, sway = 2.2f };
                default: return new Look {
                    sky = Color.white, sea = Color.white, hills = Color.white, cloud = Color.white,
                    sun = Color.white, sunAlpha = 1.0f, air = new Color(1, 1, 1, 0),
                    cloudSpeed = 1.0f, sway = 1.0f };
            }
        }

        public const float BlendSeconds = 2.2f;

        Scene _scene;
        ArchipelagoView _world;
        RectTransform _air;
        Image _wash, _flash;

        // the art's own colours, captured once, so a tint is always relative to the painting
        Color _sky0, _sun0, _glow0, _far0, _near0, _water0;
        readonly List<Color> _cloud0 = new List<Color>();
        readonly List<Drifter> _drifters = new List<Drifter>();
        readonly List<float> _drift0 = new List<float>();

        Weather _from = Weather.Sunny, _to = Weather.Sunny;
        float _t = 1f;
        bool _started;

        // ---- particles ----
        enum Kind { Rain, Snow, Streak, Mote, Leaf }
        class P { public RectTransform rt; public Image im; public Vector2 pos, vel; public float life, phase; public Kind kind; }
        readonly List<P> _pool = new List<P>();
        const int PoolSize = 90;
        float _nextFlash = 6f;

        public void Init(Scene scene, RectTransform airLayer, ArchipelagoView world)
        {
            _scene = scene;
            _world = world;
            _air = airLayer;

            _sky0 = scene.sky.color; _sun0 = scene.sun.color; _glow0 = scene.sunGlow.color;
            _far0 = scene.farHills.color; _near0 = scene.nearHills.color; _water0 = scene.water.color;
            foreach (var c in scene.clouds)
            {
                _cloud0.Add(c.color);
                var d = c.GetComponent<Drifter>();
                _drifters.Add(d);
                _drift0.Add(d != null ? d.speed : 0f);
            }

            _wash = UIKit.Img(_air, null, new Color(1, 1, 1, 0), "wash");
            _wash.rectTransform.Stretch(-40, -40, -40, -40);
            _wash.raycastTarget = false;

            for (int i = 0; i < PoolSize; i++)
            {
                var im = UIKit.Img(_air, Theme.Circle(), Color.white, "p");
                im.raycastTarget = false;
                im.rectTransform.anchorMin = im.rectTransform.anchorMax = new Vector2(0, 0);
                im.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                im.enabled = false;
                _pool.Add(new P { rt = im.rectTransform, im = im });
            }

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

        void ApplyBlend()
        {
            float e = _t * _t * (3f - 2f * _t);
            var a = LookOf(_from); var b = LookOf(_to);

            _scene.sky.color = _sky0 * Color.Lerp(a.sky, b.sky, e);
            _scene.water.color = _water0 * Color.Lerp(a.sea, b.sea, e);
            var hills = Color.Lerp(a.hills, b.hills, e);
            _scene.farHills.color = _far0 * hills;
            _scene.nearHills.color = _near0 * hills;

            float sunA = Mathf.Lerp(a.sunAlpha, b.sunAlpha, e);
            var sunTint = Color.Lerp(a.sun, b.sun, e);
            var sc = _sun0 * sunTint; sc.a = _sun0.a * sunA; _scene.sun.color = sc;
            var gc = _glow0 * sunTint; gc.a = _glow0.a * sunA; _scene.sunGlow.color = gc;
            // a bigger, hotter sun in drought
            float sunScale = Mathf.Lerp(_from == Weather.Drought ? 1.35f : 1f, _to == Weather.Drought ? 1.35f : 1f, e);
            _scene.sun.rectTransform.localScale = Vector3.one * sunScale;
            _scene.sunGlow.rectTransform.localScale = Vector3.one * sunScale;

            var cloud = Color.Lerp(a.cloud, b.cloud, e);
            float speed = Mathf.Lerp(a.cloudSpeed, b.cloudSpeed, e);
            for (int i = 0; i < _scene.clouds.Count; i++)
            {
                var c0 = _cloud0[i];
                var c = c0 * cloud; c.a = c0.a * (_to == Weather.Storm || _to == Weather.Rain ? Mathf.Lerp(1f, 1.15f, e) : 1f);
                _scene.clouds[i].color = c;
                if (_drifters[i] != null) _drifters[i].speed = _drift0[i] * speed;
            }

            _wash.color = Color.Lerp(a.air, b.air, e);
            FieldAnimator.SwayScale = Mathf.Lerp(a.sway, b.sway, e);

            // petals belong to fair weather; hide them in rain, storm and snow
            if (_scene.petals != null)
            {
                bool fair = _to == Weather.Sunny || _to == Weather.Wind || _to == Weather.Drought;
                if (_scene.petals.gameObject.activeSelf != fair) _scene.petals.gameObject.SetActive(fair);
            }

            if (_world != null) _world.SetWeatherLook(_from, _to, e);
        }

        // ============================================================
        // particles
        // ============================================================
        void Respawn()
        {
            foreach (var p in _pool) { p.im.enabled = false; p.life = -1f; }
            int n = CountFor(_to);
            for (int i = 0; i < n && i < _pool.Count; i++) Spawn(_pool[i], _to, randomY: true);
        }

        static int CountFor(Weather w)
        {
            switch (w)
            {
                case Weather.Rain: return 60;
                case Weather.Storm: return 90;
                case Weather.Snow: return 70;
                case Weather.Wind: return 34;
                case Weather.Drought: return 22;
                default: return 0;
            }
        }

        void Spawn(P p, Weather w, bool randomY)
        {
            var size = _air.rect.size;
            float W = Mathf.Max(1f, size.x), H = Mathf.Max(1f, size.y);
            p.phase = Random.value * 6.28f;
            p.life = 1f;
            p.im.enabled = true;
            p.rt.localRotation = Quaternion.identity;

            switch (w)
            {
                case Weather.Rain:
                case Weather.Storm:
                {
                    bool storm = w == Weather.Storm;
                    p.kind = Kind.Rain;
                    p.im.sprite = null;
                    p.im.color = storm ? new Color(0.82f, 0.88f, 1f, 0.55f) : new Color(0.86f, 0.93f, 1f, 0.45f);
                    float len = storm ? Random.Range(26f, 40f) : Random.Range(18f, 28f);
                    p.rt.sizeDelta = new Vector2(2.2f, len);
                    float fall = storm ? Random.Range(1300f, 1700f) : Random.Range(900f, 1200f);
                    float slant = storm ? -420f : -120f;
                    p.vel = new Vector2(slant, -fall);
                    p.rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(p.vel.x, -p.vel.y) * Mathf.Rad2Deg);
                    p.pos = new Vector2(Random.Range(0f, W + 300f), randomY ? Random.Range(0f, H) : H + 40f);
                    break;
                }
                case Weather.Snow:
                {
                    p.kind = Kind.Snow;
                    p.im.sprite = Theme.Circle();
                    float s = Random.Range(4f, 10f);
                    p.rt.sizeDelta = new Vector2(s, s);
                    p.im.color = new Color(1f, 1f, 1f, Random.Range(0.6f, 0.95f));
                    p.vel = new Vector2(Random.Range(-20f, 20f), -Random.Range(40f, 90f) * (s / 7f));
                    p.pos = new Vector2(Random.Range(0f, W), randomY ? Random.Range(0f, H) : H + 20f);
                    break;
                }
                case Weather.Wind when Random.value < 0.55f:
                {
                    // leaves tumbling across: the one wind cue that reads in a still screenshot
                    p.kind = Kind.Leaf;
                    p.im.sprite = Theme.Circle();
                    p.im.type = Image.Type.Simple;
                    float s = Random.Range(9f, 15f);
                    p.rt.sizeDelta = new Vector2(s, s * 0.55f);
                    var greens = new[] { new Color(0.47f, 0.72f, 0.30f), new Color(0.62f, 0.78f, 0.32f), new Color(0.85f, 0.70f, 0.30f) };
                    var g = greens[Random.Range(0, greens.Length)]; g.a = 0.95f;
                    p.im.color = g;
                    p.vel = new Vector2(Random.Range(380f, 620f), Random.Range(-60f, 30f));
                    p.pos = new Vector2(randomY ? Random.Range(-100f, W) : -60f, Random.Range(H * 0.15f, H * 0.9f));
                    break;
                }
                case Weather.Wind:
                {
                    p.kind = Kind.Streak;
                    p.im.sprite = Theme.Round(6);
                    p.im.type = Image.Type.Sliced;
                    p.rt.sizeDelta = new Vector2(Random.Range(70f, 160f), 3f);
                    p.im.color = new Color(1f, 1f, 1f, 0f);
                    p.vel = new Vector2(Random.Range(700f, 1000f), Random.Range(-30f, 10f));
                    p.pos = new Vector2(randomY ? Random.Range(-200f, W) : -200f, Random.Range(H * 0.10f, H * 0.85f));
                    break;
                }
                case Weather.Drought:
                {
                    p.kind = Kind.Mote;
                    p.im.sprite = Theme.Glow();
                    float s = Random.Range(10f, 22f);
                    p.rt.sizeDelta = new Vector2(s, s);
                    p.im.color = new Color(1f, 0.86f, 0.55f, 0f);
                    p.vel = new Vector2(Random.Range(-10f, 10f), Random.Range(18f, 40f));
                    p.pos = new Vector2(Random.Range(0f, W), randomY ? Random.Range(0f, H * 0.7f) : Random.Range(0f, H * 0.3f));
                    break;
                }
                default:
                    p.im.enabled = false; p.life = -1f;
                    break;
            }
            p.rt.anchoredPosition = p.pos;
        }

        void StepParticles(float dt)
        {
            var size = _air.rect.size;
            float W = size.x, H = size.y;
            // particles fade in with the blend, so a new state's rain does not start at full force
            float strength = Mathf.Clamp01(_t * 1.4f);

            foreach (var p in _pool)
            {
                if (p.life < 0f) continue;
                p.pos += p.vel * dt;

                switch (p.kind)
                {
                    case Kind.Rain:
                        if (p.pos.y < -40f || p.pos.x < -60f) Spawn(p, _to, randomY: false);
                        break;
                    case Kind.Snow:
                        p.pos.x += Mathf.Sin(Time.unscaledTime * 1.3f + p.phase) * 22f * dt;
                        if (p.pos.y < -20f) Spawn(p, _to, randomY: false);
                        break;
                    case Kind.Streak:
                    {
                        float u = Mathf.Clamp01((p.pos.x + 200f) / (W + 400f));
                        var c = p.im.color; c.a = Mathf.Sin(u * Mathf.PI) * 0.35f * strength; p.im.color = c;
                        if (p.pos.x > W + 200f) Spawn(p, _to, randomY: false);
                        break;
                    }
                    case Kind.Leaf:
                    {
                        p.pos.y += Mathf.Sin(Time.unscaledTime * 3.1f + p.phase) * 70f * dt;
                        p.rt.localRotation = Quaternion.Euler(0, 0, (Time.unscaledTime * 260f + p.phase * 57f) % 360f);
                        var c = p.im.color; c.a = 0.95f * strength; p.im.color = c;
                        if (p.pos.x > W + 60f) Spawn(p, _to, randomY: false);
                        break;
                    }
                    case Kind.Mote:
                    {
                        p.life -= dt * 0.12f;
                        var c = p.im.color; c.a = Mathf.Sin(Mathf.Clamp01(p.life) * Mathf.PI) * 0.45f * strength; p.im.color = c;
                        p.pos.x += Mathf.Sin(Time.unscaledTime * 0.9f + p.phase) * 12f * dt;
                        if (p.life <= 0f || p.pos.y > H) Spawn(p, _to, randomY: false);
                        break;
                    }
                }

                if (p.kind == Kind.Rain || p.kind == Kind.Snow)
                {
                    var c = p.im.color;
                    float target = p.kind == Kind.Snow ? 0.85f : (_to == Weather.Storm ? 0.55f : 0.45f);
                    c.a = target * strength;
                    p.im.color = c;
                }
                p.rt.anchoredPosition = p.pos;
            }
        }

        void StepLightning(float dt)
        {
            if (_flash == null) return;
            var c = _flash.color;
            c.a = Mathf.Max(0f, c.a - dt * 2.8f);

            if (_to == Weather.Storm && _t >= 1f)
            {
                _nextFlash -= dt;
                if (_nextFlash <= 0f)
                {
                    c.a = Random.Range(0.35f, 0.6f);
                    _nextFlash = Random.Range(4.5f, 11f);
                }
            }
            _flash.color = c;
        }
    }
}
