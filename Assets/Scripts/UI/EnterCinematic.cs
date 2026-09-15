using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>From the start screen into the farm: a short flight through the cloud sea, not a
    /// loading screen.
    ///
    ///     0.00–0.45  the card blooms into a cloud that drifts off, the logo lifts out of frame, the
    ///                sun flares; the camera pulls back a hair, then starts to push into the island
    ///     0.26–1.10  clouds fly out past the island (behind it, and at the frame's edges in front),
    ///                the sky's own clouds sweep outward; from 0.58 a veil of cloud closes from the
    ///                edges, and heaps condense in it, until the frame is pure warm light
    ///     1.10       COVERED. Only now is the farm built (GameApp.BuildGame): the second or two a
    ///                phone spends there is a still frame of cloud, and the farm is given three frames
    ///                to lay itself out before anything is shown
    ///     1.20–2.75  the light takes the farm's hour and weather, the veil burns open from the middle,
    ///                the heaps sweep out past the camera, the camera comes down onto the home island,
    ///                and from 1.90 the HUD arrives from its edges
    ///
    /// Everything is a pure function of one clock (<see cref="Pose"/>), so a tap fast-forwards it
    /// coherently (the rest plays in 0.35 s), and the screenshot pass can stop it on any beat.
    /// It lives on its own nested canvas above everything (order 31), allocates nothing per frame,
    /// and removes itself — its layer, its material and its textures — when done.
    ///
    /// Tuning: the beat constants below; the dissolve's look in Shaders/UICloudVeil; the art in
    /// Tools/gen_cinematic.py.</summary>
    public class EnterCinematic : MonoBehaviour, IPointerDownHandler
    {
        // ============================================================
        // beats, in seconds of the cinematic's own clock
        // ============================================================
        const float CardFrom = 0.06f;
        const float RushFrom = 0.26f;
        /// <summary>The veil starts closing from the screen edges.</summary>
        const float CloseFrom = 0.58f;
        /// <summary>The heaps condense inside the veil.</summary>
        const float WallFrom = 0.90f;
        /// <summary>Fully covered: the farm is built behind this frame.</summary>
        public const float CoverAt = 1.10f;
        /// <summary>A breath of pure light before it parts (the least the cover is held).</summary>
        public const float Hold = 0.10f;
        public const float RevealLength = 1.55f;
        public const float Total = CoverAt + Hold + RevealLength;

        /// <summary>Reveal-relative: the veil's burn, the heaps' exit, the camera, the HUD.</summary>
        const float VeilTime = 1.0f, WallOut = 1.05f, CamFrom = 0.05f, CamTime = 1.48f, HudFrom = 0.70f, HudTime = 0.58f;
        /// <summary>The camera starts this far out from the farm zoom (a ratio of ZBase).</summary>
        const float CamStartRatio = 0.56f;
        /// <summary>The sky layer starts this much higher and settles as the camera comes down.</summary>
        const float SkyLift = 46f;

        const float FastForwardSeconds = 0.35f;
        const int SettleFrames = 3;

        // ---- the dive's light, and the farm's hour it hands over to ----
        // Warmth is ADDED (the glow, the shafts, the veil's edge light), never multiplied in: gold
        // multiplied over the lavender shadows turned the whole wall beige.
        static readonly Color DiveLight = new Color(1.00f, 0.99f, 0.97f);
        static readonly Color DiveShade = new Color(0.98f, 0.97f, 1.00f);
        static readonly Color DiveRim = new Color(1.00f, 0.90f, 0.66f, 0.34f);
        static readonly Color DiveGlow = new Color(1.00f, 0.82f, 0.52f);
        const float DiveLift = 0.30f, FarmLift = 0.06f;
        static readonly Color Haze = new Color(0.84f, 0.91f, 1.00f);

        public static EnterCinematic Current { get; private set; }

        /// <summary>The screenshot pass holds the clock and sets <see cref="AuditTime"/> itself.</summary>
        public static bool AuditScrub;
        public float AuditTime;
        public float Clock => _v;
        /// <summary>The farm is built and laid out; the reveal may run.</summary>
        public bool Revealing => _built && Time.frameCount >= _builtFrame + SettleFrames;

        GameApp _app;
        RectTransform _layer;
        StartScreen.LeaveParts _s;
        Action _build;
        Vector2 _size;

        float _v, _speed = 1f;
        int _coverFrame = -1, _builtFrame = -1;
        bool _built, _done, _whoosh, _chime;

        // ---- the start screen as it was ----
        CanvasGroup _cardGroup, _logoGroup;
        Vector2 _cardBase, _logoBase, _island, _nearBase;
        float _logoScale = 1f;
        Vector2 _cardCentre, _cardHalf, _logoCentre, _logoHalf;
        Color _haloColor, _raysColor;
        Vector3 _sunScale;

        // ---- the cinematic's own layer ----
        Image _shafts, _veil, _glow, _flare;
        Material _veilMat;
        readonly List<Texture> _textures = new List<Texture>(8);

        struct Rush { public RectTransform rt; public Image im; public float t0, life, z0, ang, rad, size, spin, mirror; public bool front; }
        struct Heap { public RectTransform rt; public Image im; public VGradient g; public Vector2 home; public float size, spin, delay; }
        struct Puff { public RectTransform rt; public Image im; public Vector2 at, drift; public float t0, life, size; }
        Rush[] _rush;
        Heap[] _heaps;
        Puff[] _puffs;
        const float SpriteSize = 512f;

        // ---- the farm, once built ----
        List<Hud.EntrancePart> _hud;
        Vector2[] _hudBase;
        CanvasGroup[] _hudGroups;
        bool[] _hudAdded;
        bool _hudDone;
        RectTransform _bg;
        Vector2 _bgPivot;
        Color _farmLight = Color.white, _farmShade = Color.white, _farmRim = DiveRim, _farmGlow = DiveGlow;
        float _tintAt = -1f;

        // ============================================================
        // start
        // ============================================================
        public static EnterCinematic Play(GameApp app, RectTransform layer, StartScreen start, Action build)
        {
            var c = layer.gameObject.AddComponent<EnterCinematic>();
            c._app = app;
            c._layer = layer;
            c._build = build;
            c._s = start.BeginLeave();
            c._startedAt = Time.realtimeSinceStartup;
            c.Build();
            Current = c;
            c.Pose(0f);
            return c;
        }

        Sprite Load(string name)
        {
            var sp = Resources.Load<Sprite>("Art/cine/" + name);
            if (sp != null && !_textures.Contains(sp.texture)) _textures.Add(sp.texture);
            return sp;
        }

        void Build()
        {
            _size = _layer.rect.size;
            if (_size.x < 1f || _size.y < 1f) _size = new Vector2(GameApp.RefW, GameApp.RefH);
            float W = _size.x, H = _size.y;

            // a tap anywhere hurries the film along; nothing under it can be touched meanwhile
            var catcher = UIKit.Img(_layer, null, new Color(0f, 0f, 0f, 0f), "tap");
            catcher.rectTransform.Stretch();
            catcher.raycastTarget = true;

            // the sun's flare: a soft glow added over it (scaling the painted halo up showed its hard rim)
            _flare = UIKit.Img(_layer, Theme.Glow(), new Color(1f, 0.9f, 0.7f, 0f), "flare");
            _flare.material = MutationTint.AdditiveMaterial;
            _flare.canvasRenderer.cullTransparentMesh = true;

            // the card and the logo, turning into cloud
            _s.fx.spin = null;                               // the sun's rays are ours now
            _cardGroup = GroupOn(_s.card);
            _logoGroup = GroupOn(_s.logo);
            _cardBase = _s.card.anchoredPosition;
            _logoBase = _s.logo.anchoredPosition;
            _logoScale = _s.logo.localScale.x;               // below 1 when the start screen shrank it to clear the card
            RectIn(_s.card, out _cardCentre, out _cardHalf);
            RectIn(_s.logo, out _logoCentre, out _logoHalf);
            _haloColor = _s.halo.color;
            _raysColor = _s.rays.color;
            _sunScale = _s.sun.rectTransform.localScale;
            _nearBase = _s.near.anchoredPosition;
            // the island's centre, from the middle of the screen (its art is anchored at 0.29, 0.38)
            var art = _s.islandArt;
            _island = new Vector2((art.anchorMin.x - 0.5f) * W, (art.anchorMin.y - 0.5f) * H) + art.anchoredPosition;

            var puff = Load("cine_puff");
            // twelve over the card on a jittered 4x3 grid, blooming from its middle outward; four
            // along the logo's ribbon, later, as it lifts away
            _puffs = new Puff[16];
            for (int i = 0; i < _puffs.Length; i++)
            {
                bool logo = i >= 12;
                Vector2 at, half = logo ? _logoHalf : _cardHalf;
                float t0;
                if (!logo)
                {
                    float gx = (i % 4 + 0.5f) / 4f * 2f - 1f, gy = (i / 4 + 0.5f) / 3f * 2f - 1f;
                    gx += (Frac(i * 0.618034f + 0.11f) - 0.5f) * 0.3f;
                    gy += (Frac(i * 0.381966f + 0.47f) - 0.5f) * 0.3f;
                    at = _cardCentre + new Vector2(gx * half.x * 0.86f, gy * half.y * 0.86f);
                    t0 = CardFrom + 0.06f * Mathf.Sqrt(gx * gx * 0.5f + gy * gy * 0.5f) + Frac(i * 0.29f) * 0.02f;
                }
                else
                {
                    float gx = ((i - 12) + 0.5f) / 4f * 2f - 1f;
                    at = _logoCentre + new Vector2(gx * half.x * 0.62f, -half.y * 0.55f);
                    t0 = 0.16f + (i - 12) * 0.03f;
                }
                var away = at - (logo ? _logoCentre : _cardCentre);
                away = away.sqrMagnitude > 1f ? away.normalized : Vector2.up;
                var im = UIKit.Img(_layer, puff, Color.clear, "puff");
                im.rectTransform.sizeDelta = Vector2.one * 256f;
                im.canvasRenderer.cullTransparentMesh = true;
                im.enabled = false;
                if (i % 3 == 1) im.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                _puffs[i] = new Puff
                {
                    rt = im.rectTransform, im = im, at = at,
                    // carried off to the right and up, the way the camera turns away from the card
                    drift = away * Mathf.Lerp(50f, 90f, Frac(i * 0.7f)) + new Vector2(logo ? 0f : 240f, 110f),
                    t0 = t0,
                    life = logo ? 0.55f : 0.62f,
                    size = (logo ? 0.55f : 1.25f) * _cardHalf.x * Mathf.Lerp(0.85f, 1.15f, Frac(i * 0.53f + 0.2f)),
                };
            }

            // The clouds rushing past. Six fly out BEHIND the island, in the start screen's own depth
            // stack (a cloud fading in over the farm is a ghost laid on it, not a cloud in front of
            // it); five later ones pass in front, out at the frame's edges where the island is not.
            var heaps = new[] { Load("cine_mass_a"), Load("cine_mass_b"), Load("cine_mass_c") };
            var behind = UIKit.Node("rush_behind", _s.layer);
            behind.Stretch();
            behind.SetSiblingIndex(_s.island.GetSiblingIndex());
            // edge-passers, in the order they pass: up-left, right, down-left, up-right, down-right (radians)
            var frontAngles = new[] { 2.55f, -0.15f, 3.80f, 0.95f, 5.35f };
            _rush = new Rush[6 + frontAngles.Length];
            for (int i = 0; i < _rush.Length; i++)
            {
                bool front = i >= 6;
                var im = UIKit.Img(front ? _layer : behind, heaps[i % 3], Color.clear, front ? "rush_front" : "rush");
                im.rectTransform.sizeDelta = Vector2.one * SpriteSize;
                im.canvasRenderer.cullTransparentMesh = true;
                im.enabled = false;
                float g = Frac(i * 0.618034f + 0.21f);
                _rush[i] = front
                    ? new Rush
                    {
                        rt = im.rectTransform, im = im, front = true,
                        t0 = 0.40f + (i - 6) * 0.085f, life = 0.44f, z0 = 1.5f,
                        ang = frontAngles[i - 6], rad = Mathf.Lerp(1.05f, 1.25f, g),
                        size = Mathf.Lerp(0.95f, 1.15f, Frac(i * 0.4142f + 0.3f)),
                        spin = Mathf.Lerp(4f, 9f, g) * (i % 2 == 0 ? 1f : -1f), mirror = i % 2 == 0 ? 1f : -1f,
                    }
                    : new Rush
                    {
                        rt = im.rectTransform, im = im,
                        t0 = RushFrom + i * 0.07f, life = 0.60f, z0 = 3.0f,
                        ang = i * 2.39996f + 0.55f, rad = Mathf.Lerp(0.55f, 0.95f, g),
                        size = Mathf.Lerp(0.70f, 1.0f, Frac(i * 0.4142f + 0.3f)),
                        spin = Mathf.Lerp(5f, 14f, g) * (i % 2 == 0 ? 1f : -1f), mirror = i % 4 < 2 ? 1f : -1f,
                    };
            }

            // the veil: the density burn (Shaders/UICloudVeil); a plain fade if the shader is missing
            var shader = Resources.Load<Shader>("Shaders/UICloudVeil");
            _veil = UIKit.Img(_layer, shader != null ? Load("cine_veil") : null, (shader != null ? Color.white : DiveLight).Alpha(0f), "veil");
            _veil.rectTransform.Stretch(-8, -8, -8, -8);
            _veil.canvasRenderer.cullTransparentMesh = true;
            if (shader != null)
            {
                _veilMat = new Material(shader) { name = "UICloudVeil (cinematic)" };
                _veilMat.SetFloat("_Aspect", (W + 16f) / (H + 16f));
                _veil.material = _veilMat;
            }

            // light through the cloud, added over the veil (on a bright sky it would not show)
            _shafts = UIKit.Img(_layer, Load("cine_shafts"), DiveGlow.Alpha(0f), "shafts");
            _shafts.material = MutationTint.AdditiveMaterial;
            _shafts.rectTransform.sizeDelta = Vector2.one * H * 1.5f;
            _shafts.canvasRenderer.cullTransparentMesh = true;

            // the heaps that close in front of the veil and part again, as curtains
            // (home in half-screens from the middle, size in screen heights, sprite)
            var homes = new (float x, float y, float s, int m)[]
            {
                (-0.74f, 0.62f, 1.00f, 0), (0.76f, 0.66f, 0.95f, 1), (-0.78f, -0.64f, 1.08f, 2),
                (0.74f, -0.70f, 1.12f, 0), (0.05f, -1.02f, 0.95f, 2), (-0.08f, 1.04f, 0.86f, 1),
            };
            _heaps = new Heap[homes.Length];
            for (int i = 0; i < homes.Length; i++)
            {
                var im = UIKit.Img(_layer, heaps[homes[i].m], Color.clear, "heap");
                im.rectTransform.sizeDelta = Vector2.one * SpriteSize;
                im.canvasRenderer.cullTransparentMesh = true;
                im.enabled = false;
                if (i % 2 == 1) im.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                _heaps[i] = new Heap
                {
                    rt = im.rectTransform, im = im, g = im.gameObject.AddComponent<VGradient>(),
                    home = new Vector2(homes[i].x * W * 0.5f, homes[i].y * H * 0.5f),
                    size = homes[i].s * H / SpriteSize,
                    spin = (i % 2 == 0 ? 1f : -1f) * (4f + i),
                    delay = i * 0.022f,
                };
            }

            // the light inside the cloud, added
            _glow = UIKit.Img(_layer, Theme.Glow(), DiveGlow.Alpha(0f), "glow");
            _glow.material = MutationTint.AdditiveMaterial;
            _glow.rectTransform.sizeDelta = new Vector2(H * 2.4f, H * 1.8f);
            _glow.canvasRenderer.cullTransparentMesh = true;

            Grade(0f);
        }

        static CanvasGroup GroupOn(RectTransform rt)
        {
            var g = rt.GetComponent<CanvasGroup>();
            return g != null ? g : rt.gameObject.AddComponent<CanvasGroup>();
        }

        /// <summary>A rect's centre and half size in this layer's units (the card sits in the safe area).</summary>
        void RectIn(RectTransform rt, out Vector2 centre, out Vector2 half)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            Vector2 a = _layer.InverseTransformPoint(c[0]), b = _layer.InverseTransformPoint(c[2]);
            centre = (a + b) * 0.5f;
            half = (b - a) * 0.5f;
        }

        // ============================================================
        // clock
        // ============================================================
        void Update()
        {
            if (_done) return;
            try
            {
                Step();
                // a watchdog: whatever happens, the player is never left looking at a cloud
                if (!AuditScrub && Time.realtimeSinceStartup - _startedAt > 30f) Skip();
            }
            catch (Exception e)
            {
                // a film must never stand between the player and the farm: end it on the spot
                Debug.LogException(e);
                try { if (!_built) DoBuild(); } catch (Exception e2) { Debug.LogException(e2); }
                Finish();
            }
        }

        float _startedAt;

        void Step()
        {
            // a frame that took a second (the build) must not jump the film forward
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            float want = AuditScrub ? Mathf.Max(_v, AuditTime) : _v + dt * _speed;

            if (!_built)
            {
                if (want >= CoverAt)
                {
                    want = CoverAt;
                    // this frame draws the full cover; the next one builds behind it
                    if (_coverFrame < 0) _coverFrame = Time.frameCount;
                    else if (Time.frameCount > _coverFrame) DoBuild();
                }
            }
            else if (!Revealing) want = Mathf.Min(want, CoverAt + Hold);

            _v = want;
            Cues(_v);
            Pose(_v);
            if (_v >= Total) Finish();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_done || _v < 0.12f) return;
            _speed = Mathf.Max(_speed, (Total - _v) / FastForwardSeconds);
        }

        void Cues(float v)
        {
            if (!_whoosh && v >= 0.24f) { _whoosh = true; Sfx.Play(SfxId.Whoosh); }
            if (!_chime && v >= CoverAt + Hold + 0.04f) { _chime = true; Sfx.Play(SfxId.Chime); }
        }

        void DoBuild()
        {
            _built = true;
            _builtFrame = Time.frameCount;
            foreach (var r in _rush) if (r.front && r.im != null) r.im.enabled = false;
            _flare.enabled = false;
            // hidden behind the veil: the start screen goes before the farm comes, so the two never draw together
            _s.screen.Dispose();
            _build?.Invoke();

            var farm = _app != null ? _app.Farm : null;
            if (farm != null && farm.Camera != null)
                farm.Camera.Snap(ArchipelagoView.IslandOrigin(0), farm.Camera.ZBase * CamStartRatio);

            var hud = _app != null ? _app.Hud : null;
            if (hud != null)
            {
                _hud = hud.EntranceParts();
                _hudBase = new Vector2[_hud.Count];
                _hudGroups = new CanvasGroup[_hud.Count];
                _hudAdded = new bool[_hud.Count];
                for (int i = 0; i < _hud.Count; i++)
                {
                    var rt = _hud[i].rt;
                    _hudBase[i] = rt.anchoredPosition;
                    var g = rt.GetComponent<CanvasGroup>();
                    _hudAdded[i] = g == null;
                    _hudGroups[i] = g != null ? g : rt.gameObject.AddComponent<CanvasGroup>();
                }
            }

            _bg = _app != null ? _app.BackgroundLayer : null;
            if (_bg != null)
            {
                _bgPivot = _bg.pivot;
                // a stretched rect keeps its place when the pivot moves: this only picks where it scales from
                _bg.pivot = new Vector2(0.5f, 0f);
            }

        }

        bool _farmLit;
        float _farmNight;

        /// <summary>The hour and the weather the farm opens on: the parting cloud takes the colours the
        /// sky's own clouds have now (the same sum SkyView paints them with). Taken once the farm has
        /// run a few frames, so the weather has settled.</summary>
        void TakeFarmLight()
        {
            _farmLit = true;
            var p = DayCycle.Sample(DayCycle.Hour);
            _farmNight = p.night;
            var weather = _app != null ? _app.WeatherView : null;
            Color cloud = Color.white;
            float desat = 0f;
            if (weather != null) { cloud = weather.Blended.cloud; desat = weather.Blended.desat; }
            float ww = Mathf.Lerp(1f, 0.5f, p.night);
            desat *= 1f - 0.3f * p.night;
            var mul = Color.Lerp(Color.white, cloud, ww);
            _farmLight = Grey(p.skyCloudLit * mul, desat);
            _farmShade = Grey(Color.Lerp(p.skyCloudLit, p.skyCloudShade, 0.3f) * mul, desat);
            var light = p.night > 0.5f ? p.moonGlow : p.sunGlow;
            _farmRim = new Color(light.r, light.g, light.b, Mathf.Lerp(0.20f, 0.10f, p.night) * (1f - desat));
            _farmGlow = Grey(light, desat);
            _tintAt = -1f;
        }

        static Color Grey(Color c, float amount)
        {
            float l = DayCycle.Luma(c);
            var o = Color.Lerp(c, new Color(l, l, l), amount);
            o.a = 1f;
            return o;
        }

        // ============================================================
        // the shot, as a function of time
        // ============================================================
        void Pose(float v)
        {
            float W = _size.x, H = _size.y;
            float tau = v - CoverAt - Hold;

            // ---- the push into the island (drives the rush's vanishing point too) ----
            float du = Clamp01((v - 0.08f) / (CoverAt - 0.08f));
            float d = Mathf.Pow(du, 2.2f);
            float pull = v < 0.22f ? Mathf.Sin(v / 0.22f * Mathf.PI) * 0.012f : 0f;
            float zoom = 1f / (1f - 0.66f * d) - pull;
            float pan = EaseInOutCubic(Clamp01((v - 0.05f) / 0.95f));
            Vector2 focus = _island * (1f - pan) * zoom;

            if (!_built) PoseStart(v, zoom, pan, d);

            // ---- puffs where the card and the logo were ----
            for (int i = 0; i < _puffs.Length; i++)
            {
                ref var p = ref _puffs[i];
                float u = (v - p.t0) / p.life;
                bool on = u > 0f && u < 1f;
                if (p.im.enabled != on) p.im.enabled = on;
                if (!on) continue;
                // bloom (a quarter of its life), then drift off with the dive and thin out
                const float Bloom = 0.20f;
                float grow = EaseOutBack(Clamp01(u / Bloom), 1.2f);
                float go = Clamp01((u - Bloom) / (1f - Bloom));
                p.rt.anchoredPosition = p.at + p.drift * EaseInQuad(go) + (focus - _island) * 0.25f * go;
                float sc = p.size / 256f * (Mathf.LerpUnclamped(0.3f, 1f, grow) + 0.6f * EaseInQuad(go));
                p.rt.localScale = new Vector3(sc, sc, 1f);
                float a = Smooth(0f, 0.6f, u / Bloom) * (1f - Smooth(0.05f, 0.75f, go));
                SetColor(p.im, new Color(1f, 0.99f, 0.96f, 0.97f * a));
            }

            // ---- clouds rushing out of the vanishing point (the ones behind the island went with
            // the start screen when the farm was built) ----
            for (int i = 0; i < _rush.Length && !_built; i++)
            {
                ref var r = ref _rush[i];
                float u = (v - r.t0) / r.life;
                bool on = u > 0f && u < 1f;
                if (r.im.enabled != on) r.im.enabled = on;
                if (!on) continue;
                float z = Mathf.Lerp(r.z0, 0.5f, u);
                float k = 1f / z;
                var dir = new Vector2(Mathf.Cos(r.ang), Mathf.Sin(r.ang) * 0.8f);
                r.rt.anchoredPosition = focus + dir * (r.rad * H * k);
                float sc = r.size * H / SpriteSize * k;
                r.rt.localScale = new Vector3(sc * r.mirror, sc, 1f);
                r.rt.localRotation = Quaternion.Euler(0f, 0f, r.spin * u);
                // behind: out of the haze, gone before it is huge; in front: in quickly, out past the edge
                float a = r.front ? Smooth(0.66f, 0.82f, k) : Smooth(0.33f, 0.62f, k) * (1f - Smooth(1.55f, 1.95f, k));
                var c = Color.Lerp(Haze, Color.white, Smooth(0.33f, 0.9f, k));
                c.a = a;
                SetColor(r.im, c);
            }

            // ---- light shafts: through the rush, then through the parting ----
            {
                float a; Vector2 at; float sc;
                if (tau < 0f)
                {
                    a = 0.55f * Smooth(0.55f, 1.0f, v) * (1f - Smooth(0.9f, 1.1f, v));
                    at = focus;
                    sc = 1.1f + 1.3f * d;
                }
                else
                {
                    // sunlight through the parting; at night only a trace of it
                    a = 0.55f * Smooth(0f, 0.18f, tau) * (1f - Smooth(0.25f, 0.8f, tau)) * (1f - 0.75f * _farmNight);
                    at = Vector2.zero;
                    sc = 1.5f + 0.9f * tau;
                }
                SetColor(_shafts, Color.Lerp(DiveGlow, _farmGlow, TintK(tau)).Alpha(a));
                _shafts.rectTransform.anchoredPosition = at;
                _shafts.rectTransform.localScale = new Vector3(sc, sc, 1f);
                _shafts.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -7f * v);
            }

            // ---- the veil ----
            float cover;
            if (v < CoverAt) cover = EaseInOutSine(Clamp01((v - CloseFrom) / (CoverAt - CloseFrom)));
            else if (tau <= 0f) cover = 1f;
            else cover = 1f - EaseOutCubic(Clamp01((tau - 0.02f) / VeilTime));
            SetColor(_veil, _veil.color.Alpha(cover));

            // ---- the heaps: forming ahead, then sweeping out past the camera ----
            for (int i = 0; i < _heaps.Length; i++)
            {
                ref var h = ref _heaps[i];
                float z, a;
                if (v < CoverAt)
                {
                    float u = Clamp01((v - WallFrom - h.delay) / (CoverAt - WallFrom - h.delay));
                    // they condense where the veil is already thick, so they are never see-through over the farm
                    z = Mathf.Lerp(1.3f, 1f, EaseOutQuad(u));
                    a = Smooth(0f, 0.7f, u);
                }
                else if (tau <= 0f) { z = 1f; a = 1f; }
                else
                {
                    float e = EaseInOutCubic(Clamp01((tau - h.delay) / WallOut));
                    z = Mathf.Lerp(1f, 0.40f, e);
                    a = 1f - Smooth(0.72f, 1f, e);
                }
                bool on = a > 0.004f;
                if (h.im.enabled != on) h.im.enabled = on;
                if (!on) continue;
                float k = 1f / z;
                h.rt.anchoredPosition = h.home * k;
                float sx = h.rt.localScale.x < 0f ? -1f : 1f;
                h.rt.localScale = new Vector3(h.size * k * sx, h.size * k, 1f);
                h.rt.localRotation = Quaternion.Euler(0f, 0f, h.spin * (k - 1f));
                SetColor(h.im, new Color(1f, 1f, 1f, a));
            }

            // ---- the light in the middle ----
            {
                float a;
                if (tau < 0f) a = 0.62f * cover;
                else a = (0.62f + 0.20f * Mathf.Sin(Mathf.PI * Clamp01(tau / 0.3f))) * (1f - Smooth(0.10f, 0.85f, tau));
                SetColor(_glow, Color.Lerp(DiveGlow, _farmGlow, TintK(tau)).Alpha(a));
            }

            if (_built && !_farmLit && Revealing) TakeFarmLight();
            if (_built) PoseFarm(tau);
            Grade(tau);
        }

        /// <summary>The start screen, until the veil hides it.</summary>
        void PoseStart(float v, float zoom, float pan, float d)
        {
            float H = _size.y;
            // the card dips as it is let go, lifts, and is gone under its own puffs by 0.21
            float dip = v < 0.14f ? -Mathf.Sin(v / 0.14f * Mathf.PI) * 5f : 0f;
            float cu = Clamp01((v - CardFrom) / 0.30f);
            _s.card.anchoredPosition = _cardBase + new Vector2(0f, dip + 26f * EaseInQuad(cu));
            float cs = Mathf.Lerp(1f, 0.95f, EaseInQuad(cu));
            _s.card.localScale = new Vector3(cs, cs, 1f);
            _cardGroup.alpha = 1f - Smooth(CardFrom + 0.07f, CardFrom + 0.15f, v);

            // the logo lifts away up out of frame as the camera tilts down into the island
            float lu = Clamp01((v - 0.02f) / 0.46f);
            _s.logo.anchoredPosition = _logoBase + new Vector2(0f, 190f * EaseInCubic(lu));
            float ls = _logoScale * (1f + 0.07f * EaseInQuad(lu));
            _s.logo.localScale = new Vector3(ls, ls, 1f);
            _logoGroup.alpha = 1f - Smooth(0.45f, 1f, lu);
            if (_s.version != null) SetColor(_s.version, _s.version.color.Alpha(1f - Clamp01(v / 0.2f)));

            // the camera: each plane scales and pans by its depth. The sky's own cumulus sweep outward
            // with the push — the clearest "flying past" there is, and never over the island
            Depth(_s.sky, zoom, 0.5f, pan * 0.35f, 0f);
            Depth(_s.far, zoom, 0.42f, pan * 0.7f, 40f);
            Depth(_s.island, zoom, 1f, pan, 10000f);
            Depth(_s.near, zoom, 1.55f, pan, 40f);
            _s.near.anchoredPosition += _nearBase + new Vector2(0f, -0.3f * H * d);

            // the sun flares as the dive begins
            float f = EaseOutCubic(Clamp01(v / 0.55f));
            _s.halo.color = _haloColor.Alpha(_haloColor.a * (1f - f));
            Vector2 sunAt = _layer.InverseTransformPoint(_s.sun.rectTransform.position);
            _flare.rectTransform.anchoredPosition = sunAt;
            float fs = H * 0.55f * (1f + 1.5f * f);
            _flare.rectTransform.sizeDelta = new Vector2(fs, fs);
            SetColor(_flare, _flare.color.Alpha(0.6f * f * (1f - Smooth(0.5f, 0.95f, v))));
            _s.rays.rectTransform.localScale = Vector3.one * (1f + 0.9f * EaseInOutCubic(Clamp01(v / 0.9f)));
            _s.rays.color = _raysColor.Alpha(Mathf.Min(1f, _raysColor.a + 0.35f * f));
            _s.rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 4f - 70f * v * v);
            _s.sun.rectTransform.localScale = _sunScale * (1f + 0.22f * f);
        }

        /// <summary>A plane scaled about the middle and panned toward the island — never so far that
        /// its own edge (plus the overscan its art has) comes into view.</summary>
        void Depth(RectTransform plane, float zoom, float weight, float pan, float margin)
        {
            float sc = 1f + (zoom - 1f) * weight;
            plane.localScale = new Vector3(sc, sc, 1f);
            var p = -_island * pan * sc;
            float mx = _size.x * 0.5f * (sc - 1f) + margin, my = _size.y * 0.5f * (sc - 1f) + margin;
            plane.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -mx, mx), Mathf.Clamp(p.y, -my, my));
        }

        /// <summary>The farm under the parting: the camera coming down onto the home island, the sky
        /// settling, the HUD arriving.</summary>
        void PoseFarm(float tau)
        {
            // gentle start while the hole is still small, strongest as the farm opens up, a long settle
            float cu = Clamp01((tau - CamFrom) / CamTime);
            float e = 1f - Mathf.Pow(1f - Mathf.Pow(cu, 1.6f), 2.6f);
            var farm = _app != null ? _app.Farm : null;
            if (farm != null && farm.Camera != null)
            {
                float zb = farm.Camera.ZBase;
                float ratio = CamStartRatio * Mathf.Pow(1f / CamStartRatio, e);
                farm.Camera.Snap(ArchipelagoView.IslandOrigin(0), zb * ratio);
            }
            if (_bg != null)
            {
                float k = 1f - e;
                _bg.anchoredPosition = new Vector2(0f, SkyLift * k);
                _bg.localScale = new Vector3(1f + 0.06f * k, 1f + 0.06f * k, 1f);
            }

            if (_hud == null || _hudDone) return;
            bool all = true;
            for (int i = 0; i < _hud.Count; i++)
            {
                var part = _hud[i];
                if (part.rt == null) continue;
                float u = Clamp01((tau - HudFrom - part.delay) / HudTime);
                if (u < 1f) all = false;
                if (part.pop)
                {
                    float s = Mathf.LerpUnclamped(0.25f, 1f, EaseOutBack(u, 2.2f));
                    part.rt.localScale = new Vector3(s, s, 1f);
                    // HUD rects pivot on their anchor corner: keep the CENTRE still while it grows
                    var toCentre = Vector2.Scale(part.rt.sizeDelta, new Vector2(0.5f, 0.5f) - part.rt.pivot);
                    part.rt.anchoredPosition = _hudBase[i] + toCentre * (1f - s);
                    _hudGroups[i].alpha = Smooth(0f, 0.25f, u);
                }
                else
                {
                    part.rt.anchoredPosition = _hudBase[i] + part.from * (1f - EaseOutBack(u, 0.9f));
                    _hudGroups[i].alpha = Smooth(0f, 0.3f, u);
                }
            }
            _hudDone = all;
        }

        /// <summary>From the dive's warm gold to the farm's own hour: it starts in the held breath of
        /// light, so on a night farm the cloud has already dimmed to indigo when the hole opens — white
        /// cloud round a black hole read as a burn in paper.</summary>
        float TintK(float tau) { return _farmLit ? Smooth(-Hold, 0.4f, tau) : 0f; }

        void Grade(float tau)
        {
            float k = TintK(tau);
            if (Mathf.Abs(k - _tintAt) < 0.004f) return;
            _tintAt = k;
            var light = Color.Lerp(DiveLight, _farmLight, k);
            var shade = Color.Lerp(DiveShade, _farmShade, k);
            if (_veilMat != null)
            {
                _veilMat.SetColor("_Light", light);
                _veilMat.SetColor("_Shade", shade);
                _veilMat.SetColor("_Rim", Color.Lerp(DiveRim, _farmRim, k));
                _veilMat.SetFloat("_Lift", Mathf.Lerp(DiveLift, FarmLift, k));
            }
            light.a = shade.a = 1f;
            for (int i = 0; i < _heaps.Length; i++) _heaps[i].g.Set(light, shade);
        }

        // ============================================================
        // end
        // ============================================================
        /// <summary>Straight to the end state (the Editor's tools need the farm now).</summary>
        public void Skip()
        {
            if (_done) return;
            if (!_built) DoBuild();
            Finish();
        }

        void Finish()
        {
            if (_done) return;
            _done = true;
            try
            {
                var farm = _app != null ? _app.Farm : null;
                if (farm != null) farm.GoToIsland(0, false);
                if (_hud != null)
                    for (int i = 0; i < _hud.Count; i++)
                    {
                        var rt = _hud[i].rt;
                        if (rt == null) continue;
                        rt.anchoredPosition = _hudBase[i];
                        rt.localScale = Vector3.one;
                        if (_hudGroups[i] == null) continue;
                        if (_hudAdded[i]) Destroy(_hudGroups[i]);
                        else _hudGroups[i].alpha = 1f;
                    }
                if (_bg != null)
                {
                    _bg.localScale = Vector3.one;
                    _bg.anchoredPosition = Vector2.zero;
                    _bg.pivot = _bgPivot;
                }
            }
            finally
            {
                // whatever failed above, the veil comes off and the game gets its screen back
                if (Current == this) Current = null;
                if (_veilMat != null) Destroy(_veilMat);
                Tween.Run(UnloadLater(_textures.ToArray()));
                if (_app != null) _app.OnCinematicEnded(this);
                if (_layer != null) Destroy(_layer.gameObject);
            }
        }

        /// <summary>The cinematic's art is used once per launch: give the memory back once the
        /// images that drew it are gone.</summary>
        static IEnumerator UnloadLater(Texture[] textures)
        {
            yield return null;
            yield return null;
            foreach (var t in textures) if (t != null) Resources.UnloadAsset(t);
        }

        void OnDestroy()
        {
            // torn down from outside (a restart mid-flight): leave nothing half-done behind
            if (Current == this) Current = null;
            if (_veilMat != null) Destroy(_veilMat);
        }

        // ============================================================
        // helpers
        // ============================================================
        static void SetColor(Graphic g, Color c)
        {
            var o = g.color;
            if (Mathf.Abs(o.r - c.r) + Mathf.Abs(o.g - c.g) + Mathf.Abs(o.b - c.b) + Mathf.Abs(o.a - c.a) < 0.002f) return;
            g.color = c;
        }

        static float Frac(float x) { return x - Mathf.Floor(x); }
        static float Clamp01(float x) { return x < 0f ? 0f : x > 1f ? 1f : x; }
        static float Smooth(float e0, float e1, float x) { float t = Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }
        static float EaseInQuad(float t) { return t * t; }
        static float EaseInCubic(float t) { return t * t * t; }
        static float EaseOutQuad(float t) { return 1f - (1f - t) * (1f - t); }
        static float EaseOutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
        static float EaseInOutCubic(float t) { return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f; }
        static float EaseInOutSine(float t) { return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f; }
        static float EaseOutBack(float t, float s) { float u = t - 1f; return 1f + (s + 1f) * u * u * u + s * u * u; }
    }
}
