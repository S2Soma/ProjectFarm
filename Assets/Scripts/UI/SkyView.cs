using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>The sky behind the archipelago: a sky archipelago above a sea of clouds, lit by the
    /// time of day and tinted by the weather.
    ///
    /// It replaces a flat painted sea with foam ovals under islands that float — a picture that was
    /// neither a sky world nor a sea world — a blue dome of a hill that read as a planet, and
    /// clouds sliced flat by the horizon. The sky itself is plain quads coloured by vertex colour; the
    /// clouds are greyscale LIGHT lit by <see cref="CloudMaterials"/> (Shaders/UICloud): each maps its
    /// grey from the hour's shade colour to its light colour, so one set of art serves noon, dusk and
    /// night without ever multiplying a warm light into brown shadows.
    ///
    /// Layers, back to front (reference units, horizon 430 up from the bottom):
    ///   sky (3 gradient quads) · stars · sun · moon · cirrus · cumulus · sea base · far islets ·
    ///   far cloud-sea · stratus wisps · horizon glow · sheen · mid cloud-sea · near cloud-sea · vignette
    /// The sun and moon sit before the sea base, so the horizon hides them as they set.
    ///
    /// Nothing moves by RectTransform. The cloud sea scrolls, the cumulus drift, their edges breathe,
    /// the islets bob and the sun's rays turn in the vertex and fragment shaders, driven by material
    /// properties — the sky canvas used to re-batch every frame for twelve tiles and five clouds sliding
    /// across it (see <c>Tools ▸ LQ Farm ▸ Đo chi phí bầu trời</c>).
    ///
    /// It is also the composer: once a second (every frame while the weather is blending) it
    /// samples <see cref="DayCycle"/>, folds in <see cref="WeatherFx.Blended"/>, paints the sky,
    /// and pushes the light down to the islands through the readability floor.</summary>
    public class SkyView : MonoBehaviour
    {
        const float Hz = 430f;
        const float Tile = CloudMaterials.TileWidth;

        RectTransform _root;
        WeatherFx _weather;
        ArchipelagoView _world;
        CloudMaterialOwner _owner;

        VGradient _skyLow, _skyMid, _skyTop;
        RectTransform _starsNode;
        readonly List<Star> _stars = new List<Star>();
        Image _sunHalo, _sunGlow, _sunRays, _sun, _moonHalo, _moonGlow, _moon;
        readonly List<Islet> _islets = new List<Islet>();
        readonly List<Firefly> _flies = new List<Firefly>();
        RectTransform _flyNode;

        class Islet { public Image im, glow, dot; public Material mat; public float x01, y, w, bobPeriod, phase; public Vector2 sent = new Vector2(-1e9f, 0f); public VGradient g; }
        class Firefly { public RectTransform rt; public Image core, glow; public Vector2 pos, vel; public float period, phase; }
        readonly List<Cloud> _clouds = new List<Cloud>();
        VGradient _sea, _horizonBlend;
        readonly List<Band> _bands = new List<Band>();
        Image _horizonGlow, _sheen, _vignette;

        class Star { public Image im; public float x01, y, baseA, period, phase; public bool glint; }
        class Cloud { public Image im; public Material mat; public float w, bottom, lerp, alpha, drift, parallax, start01, x, sent = -1e9f; }
        class Band { public Image im; public Material mat; public float top, h, drift, parallax, lerp, dim, x, sentU = -1f, sentTiles = -1f; }

        float _nextPaint, _lastWidth = -1f;
        float _clock;
        bool _raysSpinInShader;
        SkyPalette _p;
        Color _pushedLand = new Color(-1, 0, 0), _pushedCrop;
        float _pushedLantern = -1f;

        // ============================================================
        // build
        // ============================================================
        public void Build(RectTransform layer)
        {
            _root = layer;
            _owner = layer.gameObject.AddComponent<CloudMaterialOwner>();

            _skyLow = SkyQuad("skyLow", Hz - 2f, Hz + 70f, false);
            _skyMid = SkyQuad("skyMid", Hz + 70f, Hz + 220f, false);
            _skyTop = SkyQuad("skyTop", Hz + 220f, 0f, true);

            BuildStars();

            _sunHalo = Glow("sunHalo", 900f);
            _sunRays = Sprite("sunRays", "Art/sky/sun_rays", 420f);
            _sunRays.color = Color.clear;
            var spin = CloudMaterials.Float(_owner, "sun rays");
            if (spin != null)
            {
                spin.SetFloat(CloudMaterials.Spin, 0.035f);          // two degrees a second, as before
                _sunRays.material = spin;
                _raysSpinInShader = true;
            }
            _sunGlow = Glow("sunGlow", 360f);
            _sun = Sprite("sun", "Art/sky/sun_disc", 96f);
            _moonHalo = Glow("moonHalo", 760f);
            _moonGlow = Glow("moonGlow", 300f);
            _moon = Sprite("moon", "Art/sky/moon_disc", 72f);

            // high, thin cirrus streaks behind everything: the sky has a ceiling far above the heaps
            AddBand("cirrus", Hz + 340f, 160f, 7f, 0.012f, 0.35f, 0f, new Vector4(0.30f, 2.5f, CloudMaterials.BandNoise(5), 0.020f));

            // id, sprite, width, bottom, lerp to horizon, alpha, drift, parallax, start, mirrored
            AddCloud("c", 150f, Hz + 18f, 0.45f, 1.00f, 4.0f, 0.040f, 0.12f, false);
            AddCloud("a", 210f, Hz + 46f, 0.35f, 1.00f, 5.0f, 0.050f, 0.52f, false);
            AddCloud("b", 170f, Hz + 30f, 0.40f, 1.00f, 4.5f, 0.045f, 0.80f, false);
            // The two near clouds were 360 and 280 wide: at 1.5x on a 1080p phone a 540 px cloud
            // parked behind the wallet and swallowed the sun.
            AddCloud("a", 250f, Hz + 118f, 0.10f, 0.96f, 8.0f, 0.090f, 0.30f, true);
            AddCloud("c", 200f, Hz + 176f, 0.05f, 0.92f, 10.0f, 0.110f, 0.95f, true);

            var seaImg = UIKit.Img(_root, null, Color.white, "seaBase");
            seaImg.rectTransform.anchorMin = new Vector2(0, 0);
            seaImg.rectTransform.anchorMax = new Vector2(1, 0);
            seaImg.rectTransform.pivot = new Vector2(0.5f, 0);
            seaImg.rectTransform.offsetMin = new Vector2(-4, -60);
            seaImg.rectTransform.offsetMax = new Vector2(4, Hz + 2);
            _sea = seaImg.gameObject.AddComponent<VGradient>();
            // The sea base meets the sky on a hard line where their colours differ (gold horizon, pale
            // sea). The old far band's billows were dense enough to hide it; the soft new ones leave
            // gaps, so the sea's top melts from the horizon's colour instead.
            var blend = UIKit.Img(_root, null, Color.white, "horizonBlend");
            blend.rectTransform.anchorMin = new Vector2(0, 0);
            blend.rectTransform.anchorMax = new Vector2(1, 0);
            blend.rectTransform.pivot = new Vector2(0.5f, 0);
            blend.rectTransform.offsetMin = new Vector2(-4, Hz - 46f);
            blend.rectTransform.offsetMax = new Vector2(4, Hz + 2f);
            _horizonBlend = blend.gameObject.AddComponent<VGradient>();

            // far islets sit just above the horizon, their cliffs sinking into the far cloud band
            BuildIslets();
            // Aerial perspective: the farther a layer, the more it takes the horizon's colour (0.60 /
            // 0.26 / 0), and the art itself is hazier and softer the farther it is (gen_sky.py band).
            // Billow: (edge breathing, warp in texels, noise tiles across a tile, noise speed) — the far
            // sea barely stirs, the near sea rolls.
            AddBand("sea_far", Hz + 8f, 150f, 3f, 0.03f, 0.60f, 0f, new Vector4(0.35f, 1.5f, CloudMaterials.BandNoise(16), 0.022f));
            // stratus wisps drifting between the far and mid heaps: air you can see through
            AddBand("sea_wisps", Hz - 16f, 140f, 12f, 0.045f, 0.32f, 0f, new Vector4(0.45f, 3f, CloudMaterials.BandNoise(6), 0.03f));

            _horizonGlow = UIKit.Img(_root, Theme.Glow(), Color.clear, "horizonGlow");
            _horizonGlow.rectTransform.anchorMin = _horizonGlow.rectTransform.anchorMax = Vector2.zero;
            _horizonGlow.rectTransform.sizeDelta = new Vector2(1800f, 220f);
            _sheen = UIKit.Img(_root, Theme.Glow(), Color.clear, "sheen");
            _sheen.rectTransform.anchorMin = _sheen.rectTransform.anchorMax = Vector2.zero;
            _sheen.rectTransform.sizeDelta = new Vector2(320f, 280f);

            // and the nearer a layer, the darker: a foreground in its own shadow is what pushes the
            // far heaps back, most of all at night when the colours alone barely separate them
            AddBand("sea_mid", Hz - 96f, 230f, 6f, 0.06f, 0.26f, 0.06f, new Vector4(0.45f, 3f, CloudMaterials.BandNoise(10), 0.026f));
            AddBand("sea_near", 222f, 320f, 10f, 0.11f, 0f, 0.16f, new Vector4(0.55f, 5f, CloudMaterials.BandNoise(7), 0.03f));

            _vignette = UIKit.Img(_root, Theme.Vignette(), Color.white, "vignette");
            _vignette.rectTransform.Stretch(-80, -80, -80, -80);

            _nextPaint = 0f;
        }

        /// <summary>Hooked up after the world and the weather exist.</summary>
        public void Bind(WeatherFx weather, ArchipelagoView world)
        {
            _weather = weather;
            _world = world;
            _nextPaint = 0f;
            if (weather != null && weather.Air != null) BuildFireflies(weather.Air);
        }

        /// <summary>Three little islands far off near the horizon, so the archipelago is part of a
        /// wider sky world rather than the only land in it. They are the islands' own paintings,
        /// small and lost in haze, each bobbing on its own slow period; at night each shows one
        /// lantern. Each islet, its glow and its lamp share one material that moves them.</summary>
        void BuildIslets()
        {
            var specs = new (int island, float x01, float y, float w, bool mirror, float period)[]
            {
                // low enough that the far cloud band swallows their cliffs: standing clear of it
                // they read as square chips pinned to the sky
                (1, 0.20f, Hz - 2f, 76f, false, 7f),
                (2, 0.62f, Hz + 6f, 50f, true, 9f),
                (5, 0.87f, Hz - 8f, 100f, true, 11f),
            };
            var rng = new System.Random(77);
            foreach (var sp in specs)
            {
                var mat = CloudMaterials.Float(_owner, "islet");
                var im = UIKit.Img(_root, Art.Load("Art/islands/island_" + sp.island), Color.white, "islet");
                var rt = im.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(sp.w, sp.w * 0.639f);
                if (sp.mirror) rt.localScale = new Vector3(-1f, 1f, 1f);
                var glow = UIKit.Img(_root, Theme.Glow(), Color.clear, "isletGlow");
                glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = Vector2.zero;
                glow.rectTransform.sizeDelta = new Vector2(18f, 18f);
                var dot = UIKit.Img(_root, Theme.Circle(), Color.clear, "isletLamp");
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = Vector2.zero;
                dot.rectTransform.sizeDelta = new Vector2(3f, 3f);
                if (mat != null) { im.material = mat; glow.material = mat; dot.material = mat; }
                _islets.Add(new Islet
                {
                    im = im, glow = glow, dot = dot, mat = mat, g = im.gameObject.AddComponent<VGradient>(),
                    x01 = sp.x01, y = sp.y, w = sp.w, bobPeriod = sp.period, phase = (float)rng.NextDouble() * 6.28f,
                });
            }
        }

        /// <summary>Fireflies over the farm on a clear night. They live in the air layer, over the
        /// world and under the HUD, and only at farm zoom — from the whole-map view they would be
        /// a scatter of dust.</summary>
        void BuildFireflies(RectTransform air)
        {
            _flyNode = UIKit.Node("fireflies", air);
            _flyNode.Stretch();
            var rng = new System.Random(311);
            for (int i = 0; i < 12; i++)
            {
                var node = UIKit.Node("firefly", _flyNode);
                node.anchorMin = node.anchorMax = Vector2.zero;
                node.sizeDelta = Vector2.one;
                var glow = UIKit.Img(node, Theme.Glow(), Theme.Hex("#C8F25A").Alpha(0f), "glow");
                glow.rectTransform.Anchor(UIKit.Center, Vector2.zero, new Vector2(26f, 26f));
                var core = UIKit.Img(node, Theme.Circle(), Theme.Hex("#EAFF8A").Alpha(0f), "core");
                core.rectTransform.Anchor(UIKit.Center, Vector2.zero, new Vector2(4.5f, 4.5f));
                double ang = rng.NextDouble() * 6.28;
                float sp = 8f + (float)rng.NextDouble() * 6f;
                _flies.Add(new Firefly
                {
                    rt = node, core = core, glow = glow,
                    pos = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()),
                    vel = new Vector2(Mathf.Cos((float)ang), Mathf.Sin((float)ang)) * sp,
                    period = 2.5f + (float)rng.NextDouble() * 2f,
                    phase = (float)rng.NextDouble() * 6.28f,
                });
            }
            _flyNode.gameObject.SetActive(false);
        }

        float _fliesA;

        void StepFireflies(float dt, float t)
        {
            if (_flyNode == null) return;
            bool zoomedIn = _world == null || _world.Camera == null || _world.Camera.Ratio >= 0.59f;
            bool on = _fliesA > 0.01f && zoomedIn;
            if (_flyNode.gameObject.activeSelf != on) _flyNode.gameObject.SetActive(on);
            if (!on) return;
            var size = _flyNode.rect.size;
            foreach (var f in _flies)
            {
                // wander: turn a little each frame, keep inside the middle of the screen
                float turn = Mathf.Sin(t * 0.7f + f.phase * 3f) * 1.4f * dt;
                f.vel = new Vector2(f.vel.x * Mathf.Cos(turn) - f.vel.y * Mathf.Sin(turn), f.vel.x * Mathf.Sin(turn) + f.vel.y * Mathf.Cos(turn));
                f.pos += new Vector2(f.vel.x / Mathf.Max(1f, size.x), f.vel.y / Mathf.Max(1f, size.y)) * dt;
                if (f.pos.x < 0.15f || f.pos.x > 0.85f) f.vel.x = Mathf.Abs(f.vel.x) * (f.pos.x < 0.15f ? 1f : -1f);
                if (f.pos.y < 0.08f || f.pos.y > 0.70f) f.vel.y = Mathf.Abs(f.vel.y) * (f.pos.y < 0.08f ? 1f : -1f);
                f.rt.anchoredPosition = new Vector2(f.pos.x * size.x, f.pos.y * size.y);
                // lit 40% of the time, fading in and out
                float blink = Mathf.Repeat(t / f.period + f.phase, 1f);
                float k = Smooth(0f, 0.12f, blink) * (1f - Smooth(0.28f, 0.42f, blink));
                float a = k * _fliesA;
                f.core.color = f.core.color.Alpha(a);
                f.glow.color = f.glow.color.Alpha(0.6f * a);
            }
        }

        VGradient SkyQuad(string name, float y0, float y1, bool toTop)
        {
            var im = UIKit.Img(_root, null, Color.white, name);
            var rt = im.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, toTop ? 1 : 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(-4, y0);
            rt.offsetMax = new Vector2(4, toTop ? 40f : y1);
            return im.gameObject.AddComponent<VGradient>();
        }

        Image Glow(string name, float size)
        {
            var im = UIKit.Img(_root, Theme.Glow(), Color.clear, name);
            im.rectTransform.anchorMin = im.rectTransform.anchorMax = Vector2.zero;
            im.rectTransform.sizeDelta = new Vector2(size, size);
            return im;
        }

        Image Sprite(string name, string path, float size)
        {
            var im = UIKit.Img(_root, Art.Load(path), Color.white, name);
            im.rectTransform.anchorMin = im.rectTransform.anchorMax = Vector2.zero;
            im.rectTransform.sizeDelta = new Vector2(size, size);
            return im;
        }

        void BuildStars()
        {
            _starsNode = UIKit.Node("stars", _root);
            _starsNode.Stretch();
            // its own canvas: twinkling rewrites 76 colours twenty times a second, and that must not
            // rebuild the rest of the sky with it
            _starsNode.gameObject.AddComponent<Canvas>();

            var rng = new System.Random(20260913);
            var dot = Art.Load("Art/sky/star_dot");
            var glint = Art.Load("Art/sky/star_glint");
            for (int i = 0; i < 76; i++)
            {
                bool isGlint = i >= 70;
                double r = rng.NextDouble();
                float size = isGlint ? 9f + (float)rng.NextDouble() * 5f
                                     : (r < 0.7 ? 2f + (float)rng.NextDouble() : 3f + (float)rng.NextDouble() * 1.5f);
                double c = rng.NextDouble();
                var tint = c < 0.80 ? Color.white : c < 0.92 ? Theme.Hex("#CFE0FF") : Theme.Hex("#FFE9B8");
                // the dot sprite's glow is ~4x its core, so draw it that much larger
                var im = UIKit.Img(_starsNode, isGlint ? glint : dot, tint, "star");
                im.rectTransform.anchorMin = im.rectTransform.anchorMax = Vector2.zero;
                im.rectTransform.sizeDelta = Vector2.one * size * (isGlint ? 2.2f : 4f);
                _stars.Add(new Star
                {
                    im = im, glint = isGlint,
                    x01 = (float)rng.NextDouble(),
                    y = (float)(Hz + 50.0 + (720.0 - Hz - 50.0) * System.Math.Sqrt(rng.NextDouble())),
                    baseA = 0.5f + (float)rng.NextDouble() * 0.5f,
                    period = 1.8f + (float)rng.NextDouble() * 2.7f,
                    phase = (float)rng.NextDouble() * 6.28f,
                });
            }
        }

        void AddCloud(string shape, float w, float bottom, float lerp, float alpha, float drift, float parallax, float start01, bool mirror)
        {
            var sp = Art.Load("Art/sky/cumulus_" + shape);
            var im = UIKit.Img(_root, sp, Color.white, "cumulus");
            var rt = im.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
            float h = sp != null ? w * sp.rect.height / sp.rect.width : w * 0.4f;
            rt.sizeDelta = new Vector2(w, h);
            // parked at x = 0 for good: the shader slides it (_Scroll.y)
            rt.anchoredPosition = new Vector2(0f, bottom);
            if (mirror) rt.localScale = new Vector3(-1, 1, 1);
            // noise ~ every 110 texels (a cumulus is drawn at a third of its texture's size)
            var mat = CloudMaterials.Cloud(_owner, sp, false, new Vector4(0.45f, 3f, 1f / 110f, 0.03f), new Vector4(0.10f, 0.97f, 0.05f, 0f));
            if (mat != null) im.material = mat;
            _clouds.Add(new Cloud
            {
                im = im, mat = mat, w = w, bottom = bottom, lerp = lerp,
                alpha = alpha, drift = drift, parallax = parallax, start01 = start01, x = -1f,
            });
        }

        /// <summary>A strip of the cloud sea: ONE quad across the screen, its texture scrolled through
        /// in the shader (it used to be two 2048-wide tiles slid along every frame).</summary>
        void AddBand(string name, float top, float h, float drift, float parallax, float lerp, float dim, Vector4 billow)
        {
            var sp = Art.Load("Art/sky/" + name);
            var im = UIKit.Img(_root, sp, Color.white, name);
            var rt = im.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(8f, h);                 // 4 units past each edge
            rt.anchoredPosition = new Vector2(0f, top);
            var mat = CloudMaterials.Cloud(_owner, sp, true, billow, new Vector4(0.14f, 1.0f, 0.06f, 0f));
            if (mat != null) im.material = mat;
            _bands.Add(new Band { im = im, mat = mat, top = top, h = h, drift = drift, parallax = parallax, lerp = lerp, dim = dim });
        }

        // ============================================================
        // per frame
        // ============================================================
        void Update()
        {
            if (_root == null) return;
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;
            float W = _root.rect.width;
            float camX = _world != null && _world.Camera != null ? _world.Camera.Camera.x : 0f;
            float speed = _weather != null ? _weather.Blended.cloudSpeed : 1f;
            bool resized = !Mathf.Approximately(W, _lastWidth);

            // the clouds' breathing runs on the weather's clock: churning in a storm, still in a drought
            _clock += (Mathf.Lerp(0.6f, 1.6f, Mathf.InverseLerp(0.35f, 2.4f, speed)) - 1f) * dt;
            CloudMaterials.SetClock(_clock);

            // cumulus drift and wrap, with parallax — as a material offset, not a move
            foreach (var c in _clouds)
            {
                if (c.x < 0f) c.x = c.start01 * W;
                c.x += c.drift * speed * dt;
                float span = W + c.w * 2f;
                float px = Mathf.Repeat(c.x - camX * c.parallax + c.w, span) - c.w;
                if (c.mat == null) { c.im.rectTransform.anchoredPosition = new Vector2(px, c.bottom); continue; }
                if (Mathf.Abs(px - c.sent) > 0.01f)
                {
                    c.mat.SetVector(CloudMaterials.Scroll, new Vector4(0f, px, 0f, 1f));
                    c.sent = px;
                }
            }

            // seamless cloud-sea bands: where the screen's left edge falls in the tile. They drift the
            // same way as the cumulus now (they used to run against them: two winds, no calm)
            foreach (var b in _bands)
            {
                b.x += b.drift * speed * dt;
                if (b.mat == null) continue;
                float u = Mathf.Repeat(camX * b.parallax - b.x - 4f, Tile) / Tile;
                float tiles = (W + 8f) / Tile;
                if (Mathf.Abs(u - b.sentU) > 1e-6f || !Mathf.Approximately(tiles, b.sentTiles))
                {
                    b.mat.SetVector(CloudMaterials.Scroll, new Vector4(u, 0f, 1f, tiles));
                    b.sentU = u; b.sentTiles = tiles;
                }
            }

            // stars: positions follow the width; alpha twinkles at ~20 Hz
            if (resized)
            {
                foreach (var s in _stars) s.im.rectTransform.anchoredPosition = new Vector2(s.x01 * W, s.y);
                PlaceIslets(W);
            }
            _lastWidth = W;

            // islets bob and slide a little against the camera
            foreach (var il in _islets)
            {
                var off = new Vector2(Mathf.Clamp(-camX * 0.035f, -180f, 180f), Mathf.Sin(t * 6.2832f / il.bobPeriod + il.phase) * 3f);
                if (il.mat == null)
                {
                    var pos = new Vector2(il.x01 * W, il.y) + off;
                    il.im.rectTransform.anchoredPosition = pos;
                    il.glow.rectTransform.anchoredPosition = pos + LampOffset(il);
                    il.dot.rectTransform.anchoredPosition = pos + LampOffset(il);
                    continue;
                }
                if ((off - il.sent).sqrMagnitude > 0.0004f)
                {
                    il.mat.SetVector(CloudMaterials.Offset, new Vector4(off.x, off.y, 0f, 0f));
                    il.sent = off;
                }
            }
            if (!_raysSpinInShader && _sunRays.enabled) _sunRays.rectTransform.localRotation = Quaternion.Euler(0, 0, -t * 2f);
            StepFireflies(dt, t);

            bool blending = _weather != null && _weather.Blending;
            if (t >= _nextPaint || blending)
            {
                Paint(W);
                _nextPaint = t + 1f;
            }
            Twinkle(t);
        }

        static Vector2 LampOffset(Islet il) { return new Vector2(il.w * 0.12f, il.w * 0.08f); }

        void PlaceIslets(float W)
        {
            foreach (var il in _islets)
            {
                var pos = new Vector2(il.x01 * W, il.y);
                il.im.rectTransform.anchoredPosition = pos;
                il.glow.rectTransform.anchoredPosition = pos + LampOffset(il);
                il.dot.rectTransform.anchoredPosition = pos + LampOffset(il);
            }
        }

        float _starsA;
        float _nextTwinkle;

        void Twinkle(float t)
        {
            bool on = _starsA > 0.005f;
            if (_starsNode.gameObject.activeSelf != on) _starsNode.gameObject.SetActive(on);
            if (!on || t < _nextTwinkle) return;
            _nextTwinkle = t + 0.05f;
            foreach (var s in _stars)
            {
                float k = 0.65f + 0.35f * Mathf.Sin(t * 6.2832f / s.period + s.phase);
                // fade out toward the horizon, where haze would hide them
                float fade = Smooth(Hz + 50f, Hz + 150f, s.y);
                var c = s.im.color; c.a = s.baseA * k * fade * _starsA; s.im.color = c;
                if (s.glint) s.im.rectTransform.localScale = Vector3.one * (0.85f + 0.25f * k);
            }
        }

        static float Smooth(float e0, float e1, float x)
        {
            float u = Mathf.Clamp01((x - e0) / (e1 - e0));
            return u * u * (3f - 2f * u);
        }

        static Color Desat(Color c, float d)
        {
            float l = DayCycle.Luma(c);
            var g = new Color(l, l, l, c.a);
            return Color.Lerp(c, g, d);
        }

        /// <summary>Recolour everything from the time of day and the weather.</summary>
        void Paint(float W)
        {
            float hour = DayCycle.Hour;
            _p = DayCycle.Sample(hour);
            var w = _weather != null ? _weather.Blended : default(WeatherFx.Look);
            if (_weather == null) { w.sky = w.sea = w.cloud = w.sun = Color.white; w.sunAlpha = 1f; w.stars = 1f; }

            float night = _p.night;
            // weather tints at half strength by night, so a drought does not turn the night brown
            float ww = Mathf.Lerp(1f, 0.5f, night);
            float d = w.desat * (1f - 0.3f * night);
            Color Mul(Color c, Color m) { var o = Desat(c * Color.Lerp(Color.white, m, ww), d); o.a = 1f; return o; }

            var top = Mul(_p.skyTop, w.sky);
            var mid = Mul(_p.skyMid, w.sky);
            var low = Mul(_p.skyLow, w.sky);
            var hor = Mul(_p.horizon, w.sky);
            SetGradient(_skyLow, low, hor);
            SetGradient(_skyMid, mid, low);
            SetGradient(_skyTop, top, mid);
            var seaTop = Mul(_p.seaTop, w.sea);
            SetGradient(_sea, seaTop, Mul(_p.seaBottom, w.sea));
            SetGradient(_horizonBlend, hor, seaTop);

            // The light on the clouds. The grey of the art is light, and the material maps it from the
            // shade colour to the light colour — the old multiply of one tint over baked lavender
            // shadows made every golden-hour and dusk shadow brown. Shadows stay cool: a little of
            // the sky's own blue goes into them, and the rim of thin sunlit edges takes the sun's glow.
            var sunLight = Color.Lerp(_p.sunGlow, _p.moonGlow, Smooth(0.3f, 0.7f, night));
            float rimA = Mathf.Lerp(0.30f, 0.12f, night) * (1f - w.desat) * Mathf.Max(0.35f, w.sunAlpha);
            var rim = Desat(sunLight, d).Alpha(rimA);
            var skyCool = Color.Lerp(mid, top, 0.5f);

            var csLit = Mul(_p.cloudSeaLit, w.cloud);
            var csShade = CoolShade(Mul(_p.cloudSeaShade, w.cloud), skyCool);
            foreach (var b in _bands)
            {
                float k = 1f - b.dim * (0.45f + 0.55f * night);
                var lit = Color.Lerp(csLit, hor, b.lerp) * k;
                var sh = Color.Lerp(csShade, hor, b.lerp * 0.7f) * k * (1f - b.dim * 0.5f);
                lit.a = 1f; sh.a = 1f;
                if (b.mat != null) CloudMaterials.Paint(b.mat, lit, sh, rim);
                else b.im.color = lit;
            }
            var scLit = Mul(_p.skyCloudLit, w.cloud);
            var scShade = CoolShade(Mul(_p.skyCloudShade, w.cloud), skyCool);
            foreach (var c in _clouds)
            {
                var lit = Color.Lerp(scLit, hor, c.lerp);
                var sh = Color.Lerp(Color.Lerp(scLit, scShade, 0.85f), hor, c.lerp * 0.8f);
                lit.a = sh.a = 1f;
                if (c.mat != null) CloudMaterials.Paint(c.mat, lit, sh, rim);
                c.im.color = (c.mat != null ? Color.white : lit).Alpha(c.alpha * _p.skyCloudA);
            }

            // sun and moon along their arcs; their light picks the horizon glow and the sheen
            var sunArc = DayCycle.SunArc(hour);
            var moonArc = DayCycle.MoonArc(hour);
            float sunA = DayCycle.SunAlpha(night) * sunArc.z * w.sunAlpha;
            float moonA = DayCycle.MoonAlpha(night) * moonArc.z * w.sunAlpha;
            var sunPos = new Vector2(sunArc.x * W, Hz + sunArc.y);
            var moonPos = new Vector2(moonArc.x * W, Hz + moonArc.y);
            float sunScale = _p.sunScale * (_weather != null ? _weather.SunScale : 1f);

            var sunCol = _p.sun * w.sun; sunCol.a = sunA;
            Place(_sun, sunPos, sunCol, sunScale);
            // shafts of light: faint at noon, stronger in the low gold light of morning and evening
            float rayA = Mathf.Lerp(0.16f, 0.28f, Mathf.Clamp01((_p.sunGlowA - 0.55f) / 0.2f)) * sunA;
            Place(_sunRays, sunPos, _p.sunGlow.Alpha(rayA), sunScale);
            Place(_sunGlow, sunPos, _p.sunGlow.Alpha(_p.sunGlowA * sunA), sunScale);
            Place(_sunHalo, sunPos, _p.sunGlow.Alpha(0.18f * sunA), sunScale);

            var moonCol = _weather != null && _weather.Target == Weather.Drought ? Theme.Hex("#FFE2B8") : _p.moon;
            Place(_moon, moonPos, moonCol.Alpha(moonA), 1f);
            Place(_moonGlow, moonPos, _p.moonGlow.Alpha(Mathf.Max(_p.moonGlowA, 0.35f * night) * moonA), 1f);
            Place(_moonHalo, moonPos, _p.moonGlow.Alpha(0.10f * moonA), 1f);

            bool sunLeads = sunA >= moonA;
            float lightX = sunLeads ? sunPos.x : moonPos.x;
            float bodyA = Mathf.Max(sunA, moonA);
            MoveTo(_horizonGlow, new Vector2(Mathf.Clamp(lightX, 0.2f * W, 0.8f * W), Hz + 6f));
            SetColor(_horizonGlow, Mul(_p.horizonGlow, w.sky).Alpha(_p.horizonGlowA * (1f - w.desat)));
            MoveTo(_sheen, new Vector2(lightX, Hz - 110f));
            SetColor(_sheen, (sunLeads ? _p.sunGlow : _p.moonGlow).Alpha(_p.sheenA * bodyA));

            SetColor(_vignette, _p.vignette.Alpha(_p.vignetteA / 0.51f));   // the art's own alpha peaks at ~0.51

            _starsA = _p.stars * w.stars;

            // far islets: the land's light, lost in the horizon's haze
            var isletTop = Color.Lerp(_p.ambient, hor, 0.58f); isletTop.a = 1f;
            var isletBot = Color.Lerp(_p.ambient, hor, 0.78f); isletBot.a = 1f;
            float lampA = DayCycle.LanternLight(night);
            foreach (var il in _islets)
            {
                SetGradient(il.g, isletTop, isletBot);
                SetColor(il.im, new Color(1f, 1f, 1f, 0.92f));
                SetColor(il.glow, Theme.Hex("#FFC66B").Alpha(0.8f * lampA));
                SetColor(il.dot, Theme.Hex("#FFE3A0").Alpha(lampA));
            }

            // fireflies: clear, calm nights only
            bool calm = _weather == null || _weather.Target == Weather.Sunny || _weather.Target == Weather.Wind || _weather.Target == Weather.Drought;
            float flyK = Smooth(0.60f, 1.0f, night) * (calm ? 1f : 0f);
            if (_weather != null && _weather.Target == Weather.Wind) flyK *= 0.5f;
            _fliesA = flyK;

            if (_world != null)
                _world.SetEnvironment(night, _weather != null ? _weather.Target : Weather.Sunny, FieldAnimator.SwayScale);
            PushLight(night, w);
        }

        /// <summary>A cloud's shadow is lit by the sky, not by the sun: keep a quarter of the sky's
        /// blue in it and never let it fall below a third of the light, so a warm hour's shadows go
        /// lavender instead of brown.</summary>
        static Color CoolShade(Color shade, Color sky)
        {
            var c = Color.Lerp(shade, sky, 0.28f);
            c.a = 1f;
            return c;
        }

        // Only what changed is written: a VGradient or a colour set dirties the sky canvas, and the
        // palette holds still for most of the day.
        readonly Dictionary<VGradient, (Color top, Color bottom)> _gradients = new Dictionary<VGradient, (Color, Color)>();

        void SetGradient(VGradient g, Color topC, Color bottomC)
        {
            if (g == null) return;
            if (_gradients.TryGetValue(g, out var was) && !Differs(was.top, topC) && !Differs(was.bottom, bottomC)) return;
            g.Set(topC, bottomC);
            _gradients[g] = (topC, bottomC);
        }

        static void SetColor(Graphic g, Color c)
        {
            if (g != null && Differs(g.color, c)) g.color = c;
        }

        static void MoveTo(Graphic g, Vector2 pos)
        {
            var rt = g.rectTransform;
            if ((rt.anchoredPosition - pos).sqrMagnitude > 0.04f) rt.anchoredPosition = pos;
        }

        static bool Differs(Color a, Color b)
        {
            const float Step = 0.5f / 255f;
            return Mathf.Abs(a.r - b.r) > Step || Mathf.Abs(a.g - b.g) > Step || Mathf.Abs(a.b - b.b) > Step || Mathf.Abs(a.a - b.a) > Step;
        }

        static void Place(Image im, Vector2 pos, Color c, float scale)
        {
            bool on = c.a > 0.003f;
            if (im.enabled != on) im.enabled = on;
            if (!on) return;
            MoveTo(im, pos);
            SetColor(im, c);
            if (Mathf.Abs(im.rectTransform.localScale.x - scale) > 0.001f) im.rectTransform.localScale = Vector3.one * scale;
        }

        /// <summary>The day's light on the islands and the lanterns, through the readability floor.
        /// Pushed only when a channel moves by 2/255 or more: SetAmbient rewrites colours across the
        /// whole world canvas, and a dusk would otherwise rebuild it every second.</summary>
        void PushLight(float night, WeatherFx.Look w)
        {
            if (_world == null) return;
            var wash = _weather != null ? _weather.Wash : new Color(1, 1, 1, 0);
            DayCycle.FloorAmbient(_p.ambient, _world.LandTint, wash, out var land, out var crop);
            if (Moved(land, _pushedLand) || Moved(crop, _pushedCrop))
            {
                _world.SetAmbient(land, crop);
                _pushedLand = land; _pushedCrop = crop;
            }
            float k = Mathf.Max(DayCycle.LanternLight(night), w.lanternMin);
            if (Mathf.Abs(k - _pushedLantern) > 0.01f)
            {
                _world.SetLanternLight(k);
                _pushedLantern = k;
            }
        }

        static bool Moved(Color a, Color b)
        {
            const float Step = 2f / 255f;
            return Mathf.Abs(a.r - b.r) >= Step || Mathf.Abs(a.g - b.g) >= Step || Mathf.Abs(a.b - b.b) >= Step;
        }

        /// <summary>Repaint now — after a dev tool or the screenshot pass forces an hour.</summary>
        public void Repaint() { _nextPaint = 0f; }
    }
}
