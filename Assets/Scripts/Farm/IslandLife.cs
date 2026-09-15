using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>What makes an island feel alive: its water, its light, its plants in the wind and the
    /// creatures that live on it. One per <see cref="IslandView"/>, chosen by the island's painting
    /// (<see cref="IslandDef.style"/>), never by its place in the archipelago:
    ///
    ///   0 Vườn Nhà   meadow flowers and grass swaying, two butterflies, gulls passing over
    ///   6 Đảo Nước   the river flowing, the waterfall with mist and spray, reeds, a fish jumping, a dragonfly
    ///   7 Khổng Lồ   giant mushrooms, flowers and leaves swaying, vines off the cliff, fat bees, falling petals
    ///   1 Đảo Gió    the painted windmill's sails turning, a flag flying, tall grass and dandelion seeds, gulls
    ///   2 Đảo Băng   frost glints on the snowfield and on the (painted) ice crystals, cold mist off the cliff
    ///   3 Đảo Hoả    lava cracks breathing, vents smoking, embers rising
    ///   4 Đảo Lôi    the painted crystals charging, static arcing between two lightning rods
    ///   5 Đảo Vàng   glints travelling over the gold, sparkles on the ground and the treasure
    ///
    /// Two kinds of motion, with one rule each:
    ///
    ///  * SHADER motion (everything above that is not a creature) is built once as <see cref="LifeQuads"/>
    ///    or Images on the world canvas and animated by _Time inside Resources/Shaders/UI*.shader. The mesh
    ///    never changes, so the world canvas never re-batches for it — the reason the lanterns got a
    ///    canvas of their own.
    ///  * CPU motion (butterflies, bees, gulls, the fish, the dragonfly) lives on two small nested canvases
    ///    per island, one under the plots and one over the front fence. Only those rebuild while something
    ///    moves, and <see cref="Step"/> switches them off when the island is off screen, the camera is pulled
    ///    back past <see cref="NearRatio"/>, or the island is still locked. Every creature is pooled at build.
    ///
    /// Nothing here reads or changes the farm: presentation only, and the Farm/ layer rule holds (it never
    /// touches the local player's state — the hour and the weather arrive through <see cref="SetEnvironment"/>).</summary>
    public class IslandLife
    {
        /// <summary>Zoom ratio at or above which creatures are simulated (the fireflies' threshold).</summary>
        public const float NearRatio = 0.59f;

        // ============================================================
        // environment (SkyView → ArchipelagoView, once a second or while the weather blends)
        // ============================================================
        public static float Night { get; private set; }
        public static Weather Sky { get; private set; } = Weather.Sunny;

        public static void SetEnvironment(float night, Weather weather, float wind)
        {
            Night = night;
            Sky = weather;
            Shader.SetGlobalFloat("_MiTNight", night);
            Shader.SetGlobalFloat("_MiTWind", wind);
        }

        /// <summary>Butterflies, bees and dragonflies: daylight and fair weather.</summary>
        public static bool FairDay => Night < 0.35f && (Sky == Weather.Sunny || Sky == Weather.Wind || Sky == Weather.Drought);
        /// <summary>Gulls: daylight, not rain or storm.</summary>
        static bool BirdWeather => Night < 0.45f && Sky != Weather.Rain && Sky != Weather.Storm;

        // ============================================================
        // materials and atlases
        // ============================================================
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        /// <summary>One shared material per look, configured the first time it is asked for.</summary>
        static Material Mat(string shader, string key, System.Action<Material> setup = null)
        {
            if (Mats.TryGetValue(key, out var m) && m != null) return m;
            var sh = Resources.Load<Shader>("Shaders/" + shader);
            m = sh != null ? new Material(sh) { name = key + " (life)" } : null;
            if (m != null) setup?.Invoke(m);
            Mats[key] = m;
            return m;
        }

        static void Blend(Material m, bool additive)
        {
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        }

        static Texture Tex(string path) { var s = Art.Load(path); return s != null ? s.texture : null; }
        static Texture NoiseTex => Tex("Art/life/tile_noise");

        /// <summary>The MiT/UI Sway atlases (Tools/gen_life.py): every one is 4 x 2 cells.</summary>
        const int AtlasCols = 4, AtlasRows = 2;
        const string Meadow = "Art/life/life_meadow", GiantAtlas = "Art/life/life_giant", WindAtlas = "Art/life/life_wind";

        // cells
        const int M_Grass = 0, M_TallGrass = 1, M_Daisy = 2, M_Pink = 3, M_Buttercup = 4, M_Fern = 5, M_Reeds = 6, M_Cattail = 7;
        const int G_Mushroom = 0, G_MushroomPair = 1, G_Flower = 2, G_Bells = 3, G_Leaf = 4, G_Clover = 5, G_Fern = 6, G_Vine = 7;
        const int W_Blades = 0, W_PinA = 1, W_PinB = 2, W_Pennant = 3, W_Banner = 4, W_Wheat = 5, W_Dandelion = 6;

        static Material SwayMat => Mat("UISway", "sway", m => { m.SetVector("_Cells", new Vector4(AtlasCols, AtlasRows, 0, 0)); m.SetFloat("_Speed", 1.5f); });
        /// <summary>MiT/UI Sway on a whole texture (one cell): the painted windmill's sails.</summary>
        static Material SailsMat => Mat("UISway", "sway_single", m => { m.SetVector("_Cells", new Vector4(1, 1, 0, 0)); m.SetFloat("_Speed", 1.5f); });

        static Rect CellUV(int index)
        {
            int cx = index % AtlasCols, cy = index / AtlasCols;
            float w = 1f / AtlasCols, h = 1f / AtlasRows;
            // a hair inside the cell, so mip bleeding never reaches the neighbour
            return new Rect(cx * w + 0.002f, 1f - (cy + 1) * h + 0.002f, w - 0.004f, h - 0.004f);
        }

        static Vector4 Data(float weight, float phase, int kind, int cell, float amp)
        {
            return new Vector4(weight, phase, kind + 4 * cell, amp);
        }

        // ============================================================
        // per island
        // ============================================================
        readonly IslandView _view;
        readonly int _style;
        RectTransform _ground, _behind, _back, _front;
        bool _canvasOn = true;
        readonly System.Random _rng;

        readonly List<Graphic> _landLit = new List<Graphic>();
        readonly List<Graphic> _propLit = new List<Graphic>();
        readonly List<Graphic> _glowing = new List<Graphic>();

        public IslandLife(IslandView view, int style)
        {
            _view = view;
            _style = style;
            _rng = new System.Random(5501 + style * 97);
        }

        Vector2 Yard(float u, float v) { return _view.YardPoint(u, v); }

        // ============================================================
        // build: ground (after scenery), plants and prop parts (during scenery)
        // ============================================================
        public void Build(RectTransform ground, RectTransform behind, RectTransform back, RectTransform front)
        {
            _ground = ground;
            _behind = behind;
            _back = back;
            _front = front;

            switch (_style)
            {
                case 2: BuildIslandGlow("glow_ice", m =>
                        {
                            m.SetVector("_Pulse", new Vector4(0.9f, 0.35f, 3f, 0));
                            m.SetVector("_Glint", new Vector4(0.55f, 0.05f, 0.025f, 0.9f));
                            m.SetVector("_Twinkle", new Vector4(1.6f, 96f, 1.5f, 0.28f));
                            m.SetColor("_GlintColor", new Color(0.86f, 0.95f, 1f));
                            m.SetFloat("_NightBoost", 1.25f);
                        });
                        BuildColdMist();
                        BuildAurora();
                        break;
                case 3: BuildIslandGlow("glow_fire", m =>
                        {
                            m.SetVector("_Pulse", new Vector4(1.3f, 0.6f, 2.5f, 0));
                            m.SetVector("_Glint", Vector4.zero);
                            m.SetVector("_Twinkle", Vector4.zero);
                            m.SetFloat("_NightBoost", 1.6f);
                        });
                        BuildCliffEmbers();
                        break;
                case 4: BuildIslandGlow("glow_storm", m =>
                        {
                            m.SetVector("_Pulse", new Vector4(2.1f, 0.7f, 4f, 0));
                            m.SetVector("_Glint", new Vector4(0.8f, 0.11f, 0.03f, 0.7f));
                            m.SetVector("_Twinkle", new Vector4(0.9f, 70f, 2.6f, 0.3f));
                            m.SetColor("_GlintColor", new Color(0.7f, 1f, 1f));
                            m.SetFloat("_NightBoost", 1.5f);
                        });
                        break;
                case 5: BuildIslandGlow("glow_gold", m =>
                        {
                            m.SetVector("_Pulse", new Vector4(0.8f, 0.25f, 3f, 0));
                            m.SetVector("_Glint", new Vector4(1.1f, 0.07f, 0.03f, 0.8f));
                            m.SetVector("_Twinkle", new Vector4(1.5f, 80f, 1.8f, 0.2f));
                            m.SetColor("_GlintColor", new Color(1f, 0.9f, 0.55f));
                            m.SetFloat("_NightBoost", 1.3f);
                        });
                        break;
                case 6: BuildRiver(); break;
                case 7: BuildVines(); break;
                case 1: BuildSeeds(); break;
            }
            BuildCreatures();
        }

        /// <summary>The island's own light mask (island_N_glow.png) over its painting, additive.</summary>
        void BuildIslandGlow(string key, System.Action<Material> setup)
        {
            var sp = Art.Load("Art/islands/island_" + _style + "_glow");
            var mat = Mat("UIGlow", key, m => { m.SetTexture("_Noise", NoiseTex); setup(m); });
            if (sp == null || mat == null) return;
            var im = UIKit.Img(_ground, sp, Color.white, "glow");
            im.material = mat;
            im.rectTransform.Anchor(UIKit.Center, new Vector2(0, IslandView.IslandSpriteCentreY), IslandView.IslandSpriteRect);
            _glowing.Add(im);
        }

        /// <summary>Moving parts that belong to a prop, placed right after it in the depth order.</summary>
        public void AttachToProp(string piece, Image prop, RectTransform layer)
        {
            if (!piece.StartsWith("prop_")) return;
            string art = piece.Substring(5);
            var rt = prop.rectTransform;
            var size = rt.sizeDelta;
            var foot = rt.anchoredPosition;                // pivot is the bottom-centre
            bool mirror = rt.localScale.x < 0f;
            var sp = prop.sprite;
            // in front of the field, anything rising off a prop would pass over the crops: keep it low
            bool inFront = foot.y < 0f;
            // a sprite pixel (from its top-left) → field space
            Vector2 Px(float px, float py)
            {
                float sx = size.x / sp.rect.width, sy = size.y / sp.rect.height;
                float x = (px - sp.rect.width * 0.5f) * sx;
                if (mirror) x = -x;
                return foot + new Vector2(x, (sp.rect.height - py) * sy);
            }

            switch (art)
            {
                case "mill":
                {
                    // the painted windmill's sails, lifted off its body by slice_decor.py (split_sails), turn on their hub
                    if (!DecorCatalog.All.TryGetValue("mill", out var e) || e.spinSize <= 0f) break;
                    var sails = LifeQuads.Create(layer, "sails", SailsMat, Tex("Art/decor/mill_sails"));
                    var hub = foot + new Vector2((e.spinAt.x - 0.5f) * size.x * (mirror ? -1f : 1f), (e.spinAt.y - e.pivotY) * size.y);
                    float s = e.spinSize * size.x * 0.5f;
                    var d = Data(0, (float)_rng.NextDouble() * 6f, 2, 0, 0.07f);
                    sails.Add(hub + new Vector2(-s, -s), hub + new Vector2(-s, s), hub + new Vector2(s, s), hub + new Vector2(s, -s),
                              new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), d);
                    _propLit.Add(sails);
                    break;
                }
                case "flagpole":
                {
                    var cloth = LifeQuads.Create(layer, "flag", SwayMat, Tex(WindAtlas));
                    var top = Px(32, 44);
                    // the cloth flies downwind (+x) whichever way the island is mirrored
                    float w = size.x * 2.9f, h = w;
                    var bl = top + new Vector2(0, -h * 0.5f);
                    var uv = CellUV(W_Pennant);
                    cloth.Add(bl, bl + new Vector2(0, h), bl + new Vector2(w, h), bl + new Vector2(w, 0),
                              new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin),
                              Data(0, 0.4f, 1, W_Pennant, 0.06f), Data(0, 0.4f, 1, W_Pennant, 0.06f),
                              Data(1, 0.4f, 1, W_Pennant, 0.06f), Data(1, 0.4f, 1, W_Pennant, 0.06f));
                    _propLit.Add(cloth);
                    break;
                }
                case "vent":
                {
                    AddPropGlow(layer, "Art/life/prop_vent_glow", rt, "glow_vent", m =>
                    {
                        m.SetVector("_Pulse", new Vector4(1.7f, 0.55f, 1.5f, 0));
                        m.SetVector("_Glint", Vector4.zero);
                        m.SetVector("_Twinkle", Vector4.zero);
                        m.SetFloat("_NightBoost", 1.5f);
                    });
                    var mouth = Px(100, 58);
                    float sw = size.x * 1.35f, sh = size.x * 2.6f;
                    if (!inFront)
                    {
                        var smoke = LifeQuads.Create(layer, "smoke", SmokeMat, NoiseTex);
                        AddUpright(smoke, mouth + new Vector2(0, -6f), new Vector2(sw, sh), (float)_rng.NextDouble());
                        _landLit.Add(smoke);
                    }
                    var embers = LifeQuads.Create(layer, "embers", EmberMat, Tex("Art/fx/p_dot"));
                    AddUpright(embers, mouth + new Vector2(0, 4f), new Vector2(sw * 0.9f, inFront ? size.x * 0.7f : sh * 0.85f), (float)_rng.NextDouble());
                    _glowing.Add(embers);
                    break;
                }
                case "rod":
                    _rods.Add(Px(55, 34));
                    if (_rods.Count == 2)
                    {
                        var a = _rods[0]; var b = _rods[1];
                        if (a.x > b.x) { var tmp = a; a = b; b = tmp; }
                        var arc = LifeQuads.Create(layer, "arc", ArcMat, NoiseTex);
                        var d = b - a;
                        var n = new Vector2(-d.y, d.x).normalized * Mathf.Max(40f, d.magnitude * 0.42f);
                        var a0 = a - d * 0.12f; var b0 = b + d * 0.12f;
                        arc.Add(a0 - n, a0 + n, b0 + n, b0 - n, new Vector2(-0.12f, 0), new Vector2(-0.12f, 1), new Vector2(1.12f, 1), new Vector2(1.12f, 0),
                                new Vector4(0, 0.37f, 0, 0));
                        _glowing.Add(arc);
                    }
                    break;
                case "crystals":
                {
                    // the painted cluster (Art/decor/crystals_slate|ice): its light mask is cut with it by slice_decor.py
                    bool ice = sp.name.EndsWith("_ice");
                    AddPropGlow(layer, "Art/decor/" + sp.name + "_glow", rt, ice ? "glow_icecrystal" : "glow_crystal", m =>
                    {
                        if (ice)
                        {
                            m.SetVector("_Pulse", new Vector4(1f, 0.4f, 1.5f, 0));
                            m.SetVector("_Glint", new Vector4(1.4f, 0.22f, 0.06f, 1.4f));
                            m.SetVector("_Twinkle", new Vector4(1.6f, 6f, 2.2f, 0.45f));
                            m.SetColor("_GlintColor", new Color(0.9f, 0.98f, 1f));
                            m.SetFloat("_NightBoost", 1.3f);
                        }
                        else
                        {
                            m.SetVector("_Pulse", new Vector4(2.4f, 0.75f, 1.2f, 0));
                            m.SetVector("_Glint", new Vector4(0.8f, 0.35f, 0.08f, 1.2f));
                            m.SetVector("_Twinkle", new Vector4(1.2f, 7f, 3f, 0.5f));
                            m.SetColor("_GlintColor", new Color(0.75f, 1f, 1f));
                            m.SetFloat("_NightBoost", 1.6f);
                        }
                    }, ice ? 0.35f : 0.5f);
                    if (!ice && sp.name.EndsWith("_slate"))
                    {
                        var sparks = LifeQuads.Create(layer, "static", StaticMat, Tex("Art/life/p_star"));
                        AddUpright(sparks, foot + new Vector2(0, size.y * 0.1f), new Vector2(size.x * 1.1f, size.y * (inFront ? 1.0f : 1.3f)), (float)_rng.NextDouble());
                        _glowing.Add(sparks);
                    }
                    break;
                }
                case "chest":
                case "coins":
                    AddPropGlow(layer, "Art/life/prop_" + art + "_glow", rt, "glow_treasure", m =>
                    {
                        m.SetVector("_Pulse", new Vector4(1.1f, 0.3f, 1.5f, 0));
                        m.SetVector("_Glint", new Vector4(1.5f, 0.18f, 0.07f, 1.3f));
                        m.SetVector("_Twinkle", new Vector4(2.2f, 8f, 2.4f, 0.4f));
                        m.SetColor("_GlintColor", new Color(1f, 0.93f, 0.6f));
                        m.SetFloat("_NightBoost", 1.3f);
                    }, 0.22f);
                    break;
            }
        }

        readonly List<Vector2> _rods = new List<Vector2>();

        void AddPropGlow(RectTransform layer, string path, RectTransform prop, string key, System.Action<Material> setup, float steady = 1f)
        {
            var sp = Art.Load(path);
            var mat = Mat("UIGlow", key, m => { m.SetTexture("_Noise", NoiseTex); setup(m); });
            if (sp == null || mat == null) return;
            var im = UIKit.Img(layer, sp, new Color(1, 1, 1, steady), "glow");
            im.material = mat;
            var rt = im.rectTransform;
            rt.anchorMin = rt.anchorMax = UIKit.Center;
            rt.pivot = prop.pivot;
            rt.sizeDelta = prop.sizeDelta;
            rt.anchoredPosition = prop.anchoredPosition;
            rt.localScale = prop.localScale;
            _glowing.Add(im);
        }

        static Material SmokeMat => Mat("UIPlume", "plume_smoke", m =>
        {
            m.SetColor("_ColA", new Color(0.36f, 0.30f, 0.30f, 0.55f));
            m.SetColor("_ColB", new Color(0.72f, 0.68f, 0.68f, 0.28f));
            m.SetVector("_Spread", new Vector4(0.16f, 0.46f, 0, 0));
            m.SetFloat("_Rise", 0.16f);
            m.SetFloat("_Puff", 1.5f);
            m.SetVector("_Fade", new Vector4(0.10f, 0.55f, 0, 0));
        });

        static Material EmberMat => Mat("UIDrift", "drift_embers", m =>
        {
            m.SetVector("_Grid", new Vector4(5f, 9f, 0, 0));
            m.SetVector("_Vel", new Vector4(0.05f, 0.9f, 0, 0));
            m.SetVector("_Shape", new Vector4(0.13f, 0.6f, 0.42f, 0f));
            m.SetVector("_Wobble", new Vector4(0.08f, 1.7f, 0, 0));
            m.SetVector("_Life", new Vector4(3.2f, 0.4f, 0.12f, 0.55f));
            m.SetVector("_Edge", new Vector4(0.5f, 0, 0, 0));
            m.SetColor("_Color", new Color(1f, 0.62f, 0.22f, 1f));
            Blend(m, true);
        });

        static Material StaticMat => Mat("UIDrift", "drift_static", m =>
        {
            m.SetVector("_Grid", new Vector4(5f, 5f, 0, 0));
            m.SetVector("_Vel", new Vector4(0f, 0.12f, 0, 0));
            m.SetVector("_Shape", new Vector4(0.12f, 0.5f, 0.35f, 0.6f));
            m.SetVector("_Wobble", new Vector4(0.06f, 7f, 0, 0));
            m.SetVector("_Life", new Vector4(6f, 1f, 0.2f, 0.4f));
            m.SetVector("_Edge", new Vector4(0.5f, 0, 0, 0));
            m.SetColor("_Color", new Color(0.62f, 0.96f, 1f, 1f));
            Blend(m, true);
        });

        static Material ArcMat => Mat("UIArc", "arc", m =>
        {
            m.SetColor("_Color", new Color(0.62f, 0.95f, 1f, 1f));
            m.SetVector("_Arc", new Vector4(0.14f, 0.24f, 0.62f, 0.045f));
        });

        /// <summary>One atlas cell centred on <paramref name="centre"/> (spinning parts).</summary>
        static void AddCell(LifeQuads q, Vector2 centre, Vector2 size, int cell, Vector4 data)
        {
            var uv = CellUV(cell);
            var h = size * 0.5f;
            q.Add(centre + new Vector2(-h.x, -h.y), centre + new Vector2(-h.x, h.y), centre + new Vector2(h.x, h.y), centre + new Vector2(h.x, -h.y),
                  new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin), data);
        }

        /// <summary>An upright quad standing on <paramref name="foot"/> with 0..1 texcoords and a seed.</summary>
        static void AddUpright(LifeQuads q, Vector2 foot, Vector2 size, float seed)
        {
            float hw = size.x * 0.5f;
            q.Add(foot + new Vector2(-hw, 0), foot + new Vector2(-hw, size.y), foot + new Vector2(hw, size.y), foot + new Vector2(hw, 0),
                  new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector4(0, seed, 0, 0));
        }

        /// <summary>A centred rectangle with 0..1 texcoords and a seed.</summary>
        static void AddRect(LifeQuads q, Vector2 centre, Vector2 size, float seed)
        {
            var h = size * 0.5f;
            q.Add(centre + new Vector2(-h.x, -h.y), centre + new Vector2(-h.x, h.y), centre + new Vector2(h.x, h.y), centre + new Vector2(h.x, -h.y),
                  new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector4(0, seed, 0, 0));
        }

        // ============================================================
        // plants
        // ============================================================
        struct Plant { public float u, v; public int cell; public float h; public string atlas; }

        /// <summary>Every swaying plant of this island, in grid cells. Pure data, so IslandTest can hold it
        /// to the yard rules: inside the fence, off the beds, tall ones only behind the field.</summary>
        public static List<(float u, float v, float height, string atlas, int cell)> PlantsFor(int islandIndex)
        {
            var list = new List<(float, float, float, string, int)>();
            var def = IslandSys.Def(islandIndex);
            var rng = new System.Random(733 + def.style * 53);
            bool river = def.layout == IslandLayout.River;

            void Scatter(int count, string atlas, int[] cells, float hMin, float hMax, bool frontOk = true, bool backOk = true)
            {
                int tries = 0;
                while (count > 0 && tries++ < 400)
                {
                    float t = (float)rng.NextDouble() * 4.8f - 2.4f;
                    float d = 2.18f + (float)rng.NextDouble() * 0.34f;
                    int edge = rng.Next(4);
                    float u = edge == 0 ? -d : edge == 3 ? d : t;
                    float v = edge == 1 ? -d : edge == 2 ? d : t;
                    bool front = u > 2f || v > 2f;
                    if (front ? !frontOk : !backOk) continue;
                    // tall plants keep off the top corner, which a 16:9 screen cuts at farm zoom
                    if (!frontOk && u < -1.7f && v < -1.7f) continue;
                    if (!YardFree(islandIndex, u, v, river)) continue;
                    bool clash = false;
                    foreach (var p in list) if (Mathf.Abs(p.Item1 - u) + Mathf.Abs(p.Item2 - v) < 0.42f) { clash = true; break; }
                    if (clash) continue;
                    int cell = cells[rng.Next(cells.Length)];
                    list.Add((u, v, hMin + (float)rng.NextDouble() * (hMax - hMin), atlas, cell));
                    count--;
                }
            }

            switch (def.style)
            {
                case 0:
                    Scatter(26, Meadow, new[] { M_Grass, M_Grass, M_Daisy, M_Pink, M_Buttercup, M_Fern, M_TallGrass }, 52f, 72f);
                    break;
                case 6:
                    Scatter(16, Meadow, new[] { M_Grass, M_Daisy, M_Pink, M_Fern, M_TallGrass }, 50f, 68f);
                    // reeds round the spring and by the river mouth
                    foreach (var (u, v, cell, h) in new[] { (-2.52f, 0.72f, M_Cattail, 62f), (-2.55f, -0.70f, M_Reeds, 58f), (-2.2f, 0.78f, M_Reeds, 52f),
                                                            (-2.25f, -0.80f, M_Cattail, 58f), (2.22f, 0.66f, M_Cattail, 56f), (2.5f, -0.68f, M_Reeds, 54f),
                                                            (2.52f, 0.60f, M_Reeds, 50f), (2.2f, -0.66f, M_Cattail, 52f) })
                        list.Add((u, v, h, Meadow, cell));
                    break;
                case 7:
                    // giant flowers behind the field (the painted toadstool and stump are props now), lower ones in front
                    Scatter(3, GiantAtlas, new[] { G_Flower, G_Bells, G_Flower }, 175f, 210f, frontOk: false);
                    Scatter(1, GiantAtlas, new[] { G_Leaf }, 135f, 150f, frontOk: false);
                    Scatter(5, GiantAtlas, new[] { G_Clover, G_Leaf, G_Fern }, 66f, 78f, backOk: false);
                    Scatter(14, Meadow, new[] { M_Grass, M_Fern, M_TallGrass }, 56f, 72f);
                    break;
                case 1:
                    Scatter(18, WindAtlas, new[] { W_Wheat, W_Wheat, W_Dandelion }, 58f, 74f);
                    Scatter(8, Meadow, new[] { M_TallGrass, M_Grass }, 52f, 66f);
                    break;
                case 4:
                    Scatter(12, Meadow, new[] { M_Grass, M_TallGrass, M_Fern }, 50f, 64f);
                    break;
                case 5:
                    Scatter(14, WindAtlas, new[] { W_Wheat }, 54f, 68f);
                    break;
            }
            return list;
        }

        /// <summary>A yard spot no plant may take: gates, the river and its beds, existing props.</summary>
        static bool YardFree(int islandIndex, float u, float v, bool river)
        {
            float F = IslandView.FenceCells;
            // the bridges come in at the left and right corners
            if (Mathf.Abs(u + F) + Mathf.Abs(v - F) < 1.1f || Mathf.Abs(u - F) + Mathf.Abs(v + F) < 1.1f) return false;
            if (river)
            {
                if (Mathf.Abs(v) < 0.95f) return false;                                  // the water and its banks
                if (Mathf.Abs(v) > 1.8f && Mathf.Abs(u) < 2.2f) return false;            // the beds reach the fence
            }
            int style = IslandSys.Def(islandIndex).style;
            foreach (var p in IslandView.Places)
                if (p.style == style && Mathf.Abs(p.u - u) + Mathf.Abs(p.v - v) < 0.25f + p.w / 190f) return false;
            if (islandIndex == 0)
                foreach (var d in IslandView.Decor)
                    if (Mathf.Abs(d.u - u) + Mathf.Abs(d.v - v) < 0.6f) return false;
            return true;
        }

        /// <summary>Swaying plants in two meshes: behind the beds, and in front of them under the front fence.</summary>
        public void BuildPlants(RectTransform decoBack, RectTransform decoFront)
        {
            var plants = PlantsFor(_view.islandIndex);
            if (plants.Count == 0) return;
            // one mesh per atlas per side (and flowers apart, which snow buries), all sharing the sway material
            var meshes = new Dictionary<string, LifeQuads>();
            LifeQuads MeshFor(string atlas, bool front, bool flowers)
            {
                string key = atlas + (front ? "|f" : "|b") + (flowers ? "|flowers" : "");
                if (meshes.TryGetValue(key, out var q)) return q;
                q = LifeQuads.Create(front ? decoFront : decoBack, (front ? "plantsFront" : "plantsBack") + (flowers ? "Flowers" : ""), SwayMat, Tex(atlas));
                if (front) q.transform.SetAsFirstSibling();
                meshes[key] = q;
                (flowers ? _flowers : _propLit).Add(q);
                return q;
            }

            // back to front, so nearer plants cover farther ones
            plants.Sort((a, b) => (a.u + a.v).CompareTo(b.u + b.v));
            foreach (var p in plants)
            {
                bool front = p.u + p.v >= 0f;
                var q = MeshFor(p.atlas, front, IsFlower(p.atlas, p.cell));
                var foot = Yard(p.u, p.v);
                bool big = p.atlas != Meadow;
                float cellAspect = big && p.atlas == GiantAtlas ? 256f / 320f : p.atlas == WindAtlas ? 1f : 128f / 160f;
                var size = new Vector2(p.height * cellAspect, p.height);
                float phase = (foot.x * 0.011f + foot.y * 0.017f) + (float)_rng.NextDouble() * 0.8f;
                // big plants lean less and slower-looking: a share of their cell's width
                float amp = p.atlas == GiantAtlas ? 0.035f : 0.075f;
                var uv = CellUV(p.cell);
                q.AddStanding(foot + new Vector2(0, -3f), size, uv,
                              Data(0, phase, 0, p.cell, amp), Data(1, phase, 0, p.cell, amp), _rng.Next(2) == 0);
            }
        }

        /// <summary>Blooms that a snowfall buries (they fade out with the snow cover).</summary>
        static bool IsFlower(string atlas, int cell)
        {
            if (atlas == Meadow) return cell == M_Daisy || cell == M_Pink || cell == M_Buttercup;
            if (atlas == GiantAtlas) return false;          // the giants stand through it, frosted
            return cell == W_Dandelion;
        }

        readonly List<Graphic> _flowers = new List<Graphic>();

        // ============================================================
        // Khổng Lồ: vines off the cliff
        // ============================================================
        void BuildVines()
        {
            var q = LifeQuads.Create(_ground, "vines", SwayMat, Tex(GiantAtlas));
            _propLit.Add(q);
            var uv = CellUV(G_Vine);
            float R = IslandView.RimCells;
            foreach (var (u, v, len) in new[] { (R, -1.9f, 118f), (R, -0.7f, 150f), (R, 0.9f, 104f), (R, 2.0f, 132f),
                                                (-1.6f, R, 126f), (-0.3f, R, 96f), (1.1f, R, 142f) })
            {
                var top = _view.YardPoint(u, v) + new Vector2(0, 4f);
                float w = len * 0.42f;
                float phase = (float)_rng.NextDouble() * 6f;
                // anchored at the top: weight 0 at the top vertices, 1 at the bottom
                var dTop = Data(0, phase, 0, G_Vine, 0.10f);
                var dBot = Data(1, phase, 0, G_Vine, 0.10f);
                q.Add(top + new Vector2(-w * 0.5f, -len), top + new Vector2(-w * 0.5f, 0), top + new Vector2(w * 0.5f, 0), top + new Vector2(w * 0.5f, -len),
                      new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin),
                      dBot, dTop, dTop, dBot);
            }

            // petals drifting down from the giant flowers, behind the field
            var petals = LifeQuads.Create(_ground, "petals", PetalMat, Tex("Art/items/particle_petal"));
            AddRect(petals, _view.YardPoint(-2.3f, -1.0f) + new Vector2(0, 90f), new Vector2(260f, 220f), 0.23f);
            AddRect(petals, _view.YardPoint(-1.0f, -2.3f) + new Vector2(0, 90f), new Vector2(260f, 220f), 0.71f);
            _propLit.Add(petals);
        }

        static Material PetalMat => Mat("UIDrift", "drift_petals", m =>
        {
            m.SetVector("_Grid", new Vector4(4f, 4f, 0, 0));
            m.SetVector("_Vel", new Vector4(0.12f, -0.22f, 0, 0));
            m.SetVector("_Shape", new Vector4(0.16f, 0.4f, 0.28f, 0.25f));
            m.SetVector("_Wobble", new Vector4(0.09f, 1.1f, 0, 0));
            m.SetVector("_Life", new Vector4(0.6f, 0f, 0.22f, 0.35f));
            m.SetVector("_Edge", new Vector4(0.55f, 0, 0, 0));
            Blend(m, false);
        });

        // ============================================================
        // Đảo Gió: dandelion seeds on the wind, above the back yard and past the tips
        // ============================================================
        void BuildSeeds()
        {
            var q = LifeQuads.Create(_ground, "seeds", Mat("UIDrift", "drift_seeds", m =>
            {
                m.SetVector("_Grid", new Vector4(7f, 3f, 0, 0));
                m.SetVector("_Vel", new Vector4(0.55f, 0.08f, 0, 0));
                m.SetVector("_Shape", new Vector4(0.2f, 0.4f, 0.3f, 0.12f));
                m.SetVector("_Wobble", new Vector4(0.1f, 1.4f, 0, 0));
                m.SetVector("_Life", new Vector4(0.7f, 0f, 0.18f, 0.25f));
                m.SetVector("_Edge", new Vector4(0.4f, 0, 0, 0));
                Blend(m, false);
            }), Tex("Art/life/p_seed"));
            // the strip over the back fence (above the crops' tops) and one over each side tip
            AddRect(q, new Vector2(0f, 330f), new Vector2(900f, 140f), 0.19f);
            AddRect(q, new Vector2(-640f, 60f), new Vector2(260f, 200f), 0.47f);
            AddRect(q, new Vector2(640f, 60f), new Vector2(260f, 200f), 0.83f);
            _propLit.Add(q);
        }

        // ============================================================
        // Đảo Băng: the aurora behind the island at night
        // ============================================================
        void BuildAurora()
        {
            if (_behind == null) return;
            var q = LifeQuads.Create(_behind, "aurora", Mat("UIAurora", "aurora"), NoiseTex);
            // from behind the back corner up into the sky; the painting hides the lower edge
            AddRect(q, new Vector2(0f, 470f), new Vector2(1700f, 560f), 0.5f);
            _glowing.Add(q);
        }

        // ============================================================
        // Đảo Băng: cold mist off the cliff
        // ============================================================
        void BuildColdMist()
        {
            var mist = LifeQuads.Create(_ground, "coldMist", Mat("UIPlume", "plume_cold", m =>
            {
                m.SetColor("_ColA", new Color(0.88f, 0.95f, 1f, 0.55f));
                m.SetColor("_ColB", new Color(0.92f, 0.97f, 1f, 0.30f));
                m.SetVector("_Spread", new Vector4(0.40f, 0.50f, 0, 0));
                m.SetFloat("_Rise", 0.05f);
                m.SetFloat("_Puff", 1.3f);
                m.SetVector("_Fade", new Vector4(0.25f, 0.35f, 0, 0));
            }), NoiseTex);
            // horizontal plumes along the two front cliff faces, drifting the way the wind streaks go (+x)
            foreach (var (c, w) in new[] { (new Vector2(-330f, -250f), 420f), (new Vector2(330f, -250f), 420f), (new Vector2(0f, -400f), 360f) })
            {
                float h = 110f;
                var bl = c + new Vector2(-w * 0.5f, -h * 0.5f);
                mist.Add(bl, bl + new Vector2(0, h), bl + new Vector2(w, h), bl + new Vector2(w, 0),
                         new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector4(0, c.x * 0.001f + 0.5f, 0, 0));
            }
            _landLit.Add(mist);
        }

        // ============================================================
        // Đảo Hoả: embers off the cliff's lava veins
        // ============================================================
        void BuildCliffEmbers()
        {
            var q = LifeQuads.Create(_ground, "cliffEmbers", Mat("UIDrift", "drift_cliff_embers", m =>
            {
                m.SetVector("_Grid", new Vector4(16f, 6f, 0, 0));
                m.SetVector("_Vel", new Vector4(0.1f, 0.55f, 0, 0));
                m.SetVector("_Shape", new Vector4(0.1f, 0.6f, 0.3f, 0f));
                m.SetVector("_Wobble", new Vector4(0.1f, 1.3f, 0, 0));
                m.SetVector("_Life", new Vector4(2.6f, 0.5f, 0.15f, 0.5f));
                m.SetVector("_Edge", new Vector4(0.45f, 0, 0, 0));
                m.SetColor("_Color", new Color(1f, 0.55f, 0.18f, 1f));
                Blend(m, true);
            }), Tex("Art/fx/p_dot"));
            AddRect(q, new Vector2(-330f, -300f), new Vector2(560f, 240f), 0.13f);
            AddRect(q, new Vector2(330f, -300f), new Vector2(560f, 240f), 0.57f);
            _glowing.Add(q);
        }

        // ============================================================
        // Đảo Nước: the river, the waterfall, the mist
        // ============================================================
        // Must match Tools/gen_islands.py (build: the river) and UIWater.shader.
        public const float SpringU = -2.25f, RiverBand = 0.44f;
        public static float Meander(float u) { return 0.05f * Mathf.Sin(u * 1.7f + 0.6f) + 0.02f * Mathf.Sin(u * 4.3f); }

        Material _water;
        const float FallLength = 290f;

        void BuildRiver()
        {
            var noise = NoiseTex;
            var sh = Resources.Load<Shader>("Shaders/UIWater");
            _water = sh != null ? new Material(sh) { name = "UIWater (Đảo Nước)" } : null;
            if (_water == null || noise == null) return;
            _water.SetFloat("_RimU", IslandView.RimCells);

            // one parallelogram in grid space whose texcoords are the grid cells themselves
            var river = LifeQuads.Create(_ground, "river", _water, noise);
            float u0 = SpringU - 0.68f, u1 = IslandView.RimCells + 0.04f, v0 = -0.72f, v1 = 0.72f;
            river.Add(IslandView.GridPoint(u0, v0), IslandView.GridPoint(u0, v1), IslandView.GridPoint(u1, v1), IslandView.GridPoint(u1, v0),
                      new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), Vector4.zero);
            _landLit.Add(river);

            // Leaves riding the current. The quad is laid along the river (texcoord x downstream), so the
            // drift shader's lattice flows with the water and every leaf lies flat on it, squashed by the
            // same isometric shear as the ground.
            var leaves = LifeQuads.Create(_ground, "leaves", Mat("UIDrift", "drift_river_leaves", mm =>
            {
                mm.SetVector("_Grid", new Vector4(8f, 1f, 0, 0));
                mm.SetVector("_Vel", new Vector4(0.36f, 0f, 0, 0));
                mm.SetVector("_Shape", new Vector4(0.2f, 0.3f, 0.3f, 0.05f));
                mm.SetVector("_Wobble", new Vector4(0.05f, 0.7f, 0, 0));
                mm.SetVector("_Life", new Vector4(0.2f, 0f, 0.08f, 0.1f));
                mm.SetVector("_Edge", new Vector4(0.08f, 0, 0, 0));
                Blend(mm, false);
            }), Tex("Art/items/particle_leaf"));
            float lu0 = SpringU + 0.5f, lu1 = IslandView.RimCells - 0.05f;
            // a band either side of the meander's mean line, narrower than the water
            leaves.Add(IslandView.GridPoint(lu0, -0.26f), IslandView.GridPoint(lu0, 0.26f), IslandView.GridPoint(lu1, 0.26f), IslandView.GridPoint(lu1, -0.26f),
                       new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector4(0, 0.29f, 0, 0));
            _landLit.Add(leaves);

            // the waterfall hangs straight down from the lip, where the river leaves the rim
            float m = Meander(IslandView.RimCells);
            float half = RiverBand - 0.03f;
            var lipL = IslandView.GridPoint(IslandView.RimCells - 0.02f, m + half);   // larger v is further left
            var lipR = IslandView.GridPoint(IslandView.RimCells - 0.02f, m - half);
            var padA = (lipR - lipL) * 0.14f;
            var drop = new Vector2(0f, -FallLength);
            var lift = new Vector2(0f, 7f);
            var fall = LifeQuads.Create(_ground, "waterfall", Mat("UIFall", "fall"), noise);
            fall.Add(lipL - padA + drop, lipL - padA + lift, lipR + padA + lift, lipR + padA + drop,
                     new Vector2(-0.14f, 1f), new Vector2(-0.14f, 0f), new Vector2(1.14f, 0f), new Vector2(1.14f, 1f), Vector4.zero);
            _landLit.Add(fall);

            var foot = (lipL + lipR) * 0.5f + new Vector2(0f, -FallLength * 0.72f);
            var mist = LifeQuads.Create(_ground, "mist", Mat("UIPlume", "plume_mist", mm =>
            {
                mm.SetColor("_ColA", new Color(0.93f, 0.97f, 1f, 0.85f));
                mm.SetColor("_ColB", new Color(0.97f, 0.99f, 1f, 0.55f));
                mm.SetVector("_Spread", new Vector4(0.30f, 0.50f, 0, 0));
                mm.SetFloat("_Rise", 0.10f);
                mm.SetFloat("_Puff", 1.9f);
                mm.SetVector("_Fade", new Vector4(0.25f, 0.55f, 0, 0));
            }), noise);
            AddRect(mist, foot + new Vector2(0f, -40f), new Vector2(260f, 190f), 0.37f);
            _landLit.Add(mist);

            var spray = LifeQuads.Create(_ground, "spray", Mat("UIDrift", "drift_spray", mm =>
            {
                mm.SetVector("_Grid", new Vector4(9f, 7f, 0, 0));
                mm.SetVector("_Vel", new Vector4(0.25f, -0.9f, 0, 0));
                mm.SetVector("_Shape", new Vector4(0.16f, 0.5f, 0.55f, 0f));
                mm.SetVector("_Wobble", new Vector4(0.04f, 2.1f, 0, 0));
                mm.SetVector("_Life", new Vector4(2.2f, 0.3f, 0.25f, 0.45f));
                mm.SetVector("_Edge", new Vector4(0.45f, 0, 0, 0));
                Blend(mm, false);
            }), Tex("Art/fx/p_dot"));
            AddRect(spray, foot + new Vector2(0f, 20f), new Vector2(230f, 170f), 0.61f);
            _landLit.Add(spray);
        }

        // ============================================================
        // creatures
        // ============================================================
        enum Kind { Butterfly, Bee, Dragonfly }

        class Flyer
        {
            public Kind kind;
            public Image im, shadow;
            public Vector2 pos, vel, target;
            public float alt, phase, rest, a, speed;
            public bool front;
            public List<Vector2> spots;
            public int at;
            public Vector2 size;
        }

        class Gull { public Image im; public Vector2 offset; public float phase; }

        readonly List<Flyer> _flyers = new List<Flyer>();
        readonly List<Gull> _gulls = new List<Gull>();
        float _flockNext = -1f, _flockT0, _flockDur;
        Vector2 _flockFrom, _flockTo;
        bool _flockOn;

        Image _fish;
        float _fishNext = -1f, _fishT0 = -10f;
        Vector2 _fishA, _fishB;
        float _fishU, _fishV;
        int _fishSplash;

        Color _tint = Color.white;

        void BuildCreatures()
        {
            if (_back == null) return;
            switch (_style)
            {
                case 0: AddFlyers(Kind.Butterfly, 2); AddGulls(3); break;
                case 6: AddFlyers(Kind.Butterfly, 1); AddFlyers(Kind.Dragonfly, 1); AddGulls(2); AddFish(); break;
                case 7: AddFlyers(Kind.Bee, 2); AddFlyers(Kind.Butterfly, 1); break;
                case 1: AddGulls(3); AddFlyers(Kind.Butterfly, 1); break;
                case 5: AddFlyers(Kind.Butterfly, 1); break;
            }
            if (_back != null) _back.gameObject.SetActive(false);
            if (_front != null) _front.gameObject.SetActive(false);
            _canvasOn = false;
        }

        /// <summary>Waypoints in the yard: back and front halves separately, so a creature stays on one
        /// canvas (behind the beds, or in front of them) and never crosses the field.</summary>
        /// <summary>Waypoints IN ORDER along the yard ring — the back half (left gate → top corner → right
        /// gate) or the front half (left gate → bottom corner → right gate). A creature only ever flies to a
        /// neighbouring waypoint, so it follows the ring and never cuts across the field and its crops.</summary>
        List<Vector2> YardSpots(bool front)
        {
            var list = new List<Vector2>();
            bool river = IslandSys.Def(_view.islandIndex).layout == IslandLayout.River;
            float F = IslandView.FenceCells;
            const float D = 2.34f;
            void Try(float u, float v)
            {
                if (river && Mathf.Abs(v) > 1.8f && Mathf.Abs(u) < 2.2f) return;         // Đảo Nước's beds reach the fence
                if (Mathf.Abs(u + F) + Mathf.Abs(v - F) < 1.0f || Mathf.Abs(u - F) + Mathf.Abs(v + F) < 1.0f) return;   // the gates
                list.Add(Yard(u, v));
            }
            if (!front)
            {
                for (float t = 2.2f; t >= -D; t -= 0.5f) Try(-D, t);                  // up the back-left edge
                for (float t = -D + 0.5f; t <= 2.21f; t += 0.5f) Try(t, -D);          // down the back-right edge
            }
            else
            {
                for (float t = -2.2f; t <= D; t += 0.5f) Try(t, D);                   // along the front-left edge
                for (float t = D - 0.5f; t >= -2.21f; t -= 0.5f) Try(D, t);           // back up the front-right edge
            }
            return list;
        }

        void AddFlyers(Kind kind, int count)
        {
            string art = kind == Kind.Bee ? "bee" : kind == Kind.Dragonfly ? "dragonfly" : null;
            for (int i = 0; i < count; i++)
            {
                bool front = kind != Kind.Dragonfly && (i % 2 == 1);
                var layer = front ? _front : _back;
                string sprite = art ?? ((_style + i) % 2 == 0 ? "butterfly_a" : "butterfly_b");
                var sp = Art.Load("Art/life/" + sprite);
                if (sp == null) continue;
                var shadow = UIKit.Img(layer, Theme.Glow(), new Color(0.1f, 0.12f, 0.2f, 0f), "shadow");
                shadow.raycastTarget = false;
                var im = UIKit.Img(layer, sp, new Color(1, 1, 1, 0), kind.ToString());
                im.raycastTarget = false;
                var f = new Flyer
                {
                    kind = kind, im = im, shadow = shadow, front = front,
                    spots = YardSpots(front),
                    phase = (float)_rng.NextDouble() * 6f,
                    speed = kind == Kind.Bee ? 34f : kind == Kind.Dragonfly ? 90f : 52f,
                    size = kind == Kind.Bee ? new Vector2(46f, 46f) : kind == Kind.Dragonfly ? new Vector2(44f, 29f) : new Vector2(36f, 30f),
                };
                // the dragonfly keeps to the spring: its banks and the pool itself
                if (kind == Kind.Dragonfly) f.spots = new List<Vector2> { IslandView.GridPoint(-2.25f, 0.95f), IslandView.GridPoint(-2.5f, 0.55f),
                                                                           IslandView.GridPoint(-2.35f, 0.05f), IslandView.GridPoint(-2.55f, -0.45f),
                                                                           IslandView.GridPoint(-2.3f, -0.9f) };
                if (f.spots.Count == 0) continue;
                f.at = _rng.Next(f.spots.Count);
                f.pos = f.spots[f.at];
                f.target = f.pos;
                im.rectTransform.sizeDelta = f.size;
                shadow.rectTransform.sizeDelta = new Vector2(f.size.x * 0.8f, f.size.x * 0.3f);
                im.gameObject.SetActive(false);
                shadow.gameObject.SetActive(false);
                _flyers.Add(f);
            }
        }

        void AddGulls(int count)
        {
            var sp = Art.Load("Art/life/bird");
            if (sp == null) return;
            for (int i = 0; i < count; i++)
            {
                var im = UIKit.Img(_back, sp, Color.white, "gull");
                im.raycastTarget = false;
                im.rectTransform.sizeDelta = new Vector2(52f, 26f) * (1f - i * 0.12f);
                im.gameObject.SetActive(false);
                _gulls.Add(new Gull { im = im, offset = new Vector2(-i * 58f - (float)_rng.NextDouble() * 20f, (i % 2 == 0 ? 1 : -1) * i * 22f), phase = i * 1.3f });
            }
        }

        void AddFish()
        {
            var sp = Art.Load("Art/life/fish");
            if (sp == null) return;
            _fish = UIKit.Img(_back, sp, Color.white, "fish");
            _fish.raycastTarget = false;
            _fish.rectTransform.sizeDelta = new Vector2(40f, 20f);
            _fish.gameObject.SetActive(false);
        }

        // ============================================================
        // light and weather
        // ============================================================
        public void Recolour(Color land, Color prop, float snow)
        {
            foreach (var g in _landLit) if (g != null) g.color = land;
            // under snow the plants take a frosted cast rather than their summer green
            var frost = Color.Lerp(prop, prop * new Color(0.86f, 0.92f, 1.05f) + new Color(0.10f, 0.10f, 0.12f, 0f), snow * 0.8f);
            frost.a = 1f;
            foreach (var g in _propLit) if (g != null) g.color = frost;
            var buried = frost; buried.a = Mathf.Clamp01(1f - snow * 1.4f);
            foreach (var g in _flowers)
            {
                if (g == null) continue;
                g.color = buried;
                bool on = buried.a > 0.01f;
                if (g.gameObject.activeSelf != on) g.gameObject.SetActive(on);
            }
            // _glowing are light sources: never dimmed by the hour (UIGlow boosts them at night instead)
            _tint = prop;
        }

        // ============================================================
        // per frame
        // ============================================================
        public void Step(float t, float dt, bool active)
        {
            if (_canvasOn != active)
            {
                _canvasOn = active;
                if (_back != null) _back.gameObject.SetActive(active);
                if (_front != null) _front.gameObject.SetActive(active);
            }
            if (!active) return;

            bool fair = FairDay;
            for (int i = 0; i < _flyers.Count; i++) StepFlyer(_flyers[i], t, dt, fair);
            StepGulls(t);
            StepFish(t);
        }

        void StepFlyer(Flyer f, float t, float dt, bool fair)
        {
            f.a = Mathf.MoveTowards(f.a, fair ? 1f : 0f, dt * 0.8f);
            bool show = f.a > 0.001f;
            if (f.im.gameObject.activeSelf != show) { f.im.gameObject.SetActive(show); f.shadow.gameObject.SetActive(show); }
            if (!show) return;

            if (f.rest > 0f)
            {
                f.rest -= dt;
                f.vel = Vector2.Lerp(f.vel, Vector2.zero, dt * 4f);
            }
            else
            {
                var to = f.target - f.pos;
                float dist = to.magnitude;
                if (dist < 14f)
                {
                    // a neighbour along the ring, one or two steps either way
                    int step = (_rng.Next(2) == 0 ? -1 : 1) * (1 + _rng.Next(2));
                    int next = f.at + step;
                    if (next < 0 || next >= f.spots.Count) next = f.at - step;
                    f.at = Mathf.Clamp(next, 0, f.spots.Count - 1);
                    f.target = f.spots[f.at] + new Vector2((float)_rng.NextDouble() * 30f - 15f, (float)_rng.NextDouble() * 14f - 7f);
                    if (_rng.NextDouble() < (f.kind == Kind.Butterfly ? 0.4 : 0.55))
                        f.rest = f.kind == Kind.Butterfly ? 1.6f + (float)_rng.NextDouble() * 2.4f : 0.6f + (float)_rng.NextDouble() * 1.2f;
                }
                var desired = dist > 0.01f ? to / dist * f.speed : Vector2.zero;
                float wob = f.kind == Kind.Butterfly ? 28f : f.kind == Kind.Bee ? 12f : 4f;
                var side = new Vector2(-desired.y, desired.x).normalized * Mathf.Sin(t * 2.3f + f.phase) * wob;
                // a dragonfly darts: long hovers, quick dashes
                float dash = f.kind == Kind.Dragonfly ? (Mathf.Repeat(t * 0.6f + f.phase, 1f) < 0.3f ? 2.2f : 0.15f) : 1f;
                f.vel = Vector2.Lerp(f.vel, desired * dash + side, dt * (f.kind == Kind.Dragonfly ? 6f : 2.2f));
            }
            f.pos += f.vel * dt;

            float targetAlt = f.rest > 0f
                ? (f.kind == Kind.Butterfly ? 16f : f.kind == Kind.Bee ? 34f : 20f)
                : (f.kind == Kind.Butterfly ? 34f + 12f * Mathf.Sin(t * 1.7f + f.phase) : f.kind == Kind.Bee ? 44f + 6f * Mathf.Sin(t * 2.6f + f.phase) : 26f);
            f.alt = Mathf.Lerp(f.alt, targetAlt, dt * 3f);

            var rt = f.im.rectTransform;
            float bob = f.kind == Kind.Bee ? Mathf.Sin(t * 9f + f.phase) * 2f : 0f;
            rt.anchoredPosition = f.pos + new Vector2(0f, f.alt + bob);
            float face = f.vel.x < -2f ? -1f : f.vel.x > 2f ? 1f : Mathf.Sign(rt.localScale.x == 0 ? 1f : rt.localScale.x);
            switch (f.kind)
            {
                case Kind.Butterfly:
                {
                    float flap = f.rest > 0f ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 2.2f + f.phase)) : 0.22f + 0.78f * Mathf.Abs(Mathf.Sin(t * 15f + f.phase));
                    rt.localScale = new Vector3(flap, 1f, 1f);
                    rt.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp(-f.vel.x * 0.25f, -16f, 16f));
                    break;
                }
                case Kind.Bee:
                    // the art faces left
                    rt.localScale = new Vector3(-face, 1f, 1f);
                    rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 3f + f.phase) * 4f);
                    break;
                default:
                    rt.localScale = new Vector3(face, 1f, 1f);
                    rt.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp(f.vel.y * 0.08f, -10f, 10f) * face);
                    break;
            }
            var c = _tint; c.a = f.a;
            f.im.color = c;
            f.shadow.rectTransform.anchoredPosition = f.pos;
            float sh = Mathf.Clamp01(1f - f.alt / 90f);
            f.shadow.color = new Color(0.1f, 0.12f, 0.2f, 0.20f * sh * f.a);
        }

        void StepGulls(float t)
        {
            if (_gulls.Count == 0) return;
            if (_flockNext < 0f) _flockNext = t + 4f + (float)_rng.NextDouble() * 10f;
            if (!_flockOn)
            {
                if (t < _flockNext || !BirdWeather) return;
                _flockOn = true;
                _flockT0 = t;
                _flockDur = 13f + (float)_rng.NextDouble() * 5f;
                bool ltr = _rng.Next(2) == 0;
                float y = 170f + (float)_rng.NextDouble() * 150f;
                _flockFrom = new Vector2(ltr ? -1050f : 1050f, y);
                _flockTo = new Vector2(ltr ? 1050f : -1050f, y + (float)_rng.NextDouble() * 120f - 40f);
                foreach (var g in _gulls) g.im.gameObject.SetActive(true);
            }
            float k = (t - _flockT0) / _flockDur;
            if (k >= 1f)
            {
                _flockOn = false;
                _flockNext = t + (_style == 1 ? 12f : 24f) + (float)_rng.NextDouble() * 20f;
                foreach (var g in _gulls) g.im.gameObject.SetActive(false);
                return;
            }
            float dir = Mathf.Sign(_flockTo.x - _flockFrom.x);
            var head = Vector2.Lerp(_flockFrom, _flockTo, k);
            float fade = Mathf.Clamp01(k * 8f) * Mathf.Clamp01((1f - k) * 8f);
            foreach (var g in _gulls)
            {
                var rt = g.im.rectTransform;
                rt.anchoredPosition = head + new Vector2(g.offset.x * dir, g.offset.y + Mathf.Sin(t * 1.1f + g.phase) * 6f);
                // a few beats, then a glide
                float cycle = Mathf.Repeat(t * 0.45f + g.phase * 0.2f, 1f);
                float flap = cycle < 0.45f ? Mathf.Sin(t * 9f + g.phase) : 0.35f;
                rt.localScale = new Vector3(dir, Mathf.Lerp(-0.35f, 1f, flap * 0.5f + 0.5f), 1f);
                var c = _tint; c.a = fade;
                g.im.color = c;
            }
        }

        void StepFish(float t)
        {
            if (_fish == null || _water == null) return;
            float lt = Time.timeSinceLevelLoad;
            if (_fishNext < 0f) _fishNext = t + 3f;
            float k = (t - _fishT0) / 0.9f;
            if (k > 1f)
            {
                if (_fish.gameObject.activeSelf)
                {
                    _fish.gameObject.SetActive(false);
                    _water.SetVector("_Splash1", new Vector4(_fishU + 0.42f, _fishV, lt, 1f));
                }
                bool calm = Sky != Weather.Storm && Sky != Weather.Snow;
                if (t < _fishNext || !calm) return;
                _fishNext = t + (Night > 0.5f ? 9f : 5f) + (float)_rng.NextDouble() * 7f;
                _fishT0 = t;
                _fishU = -1.8f + (float)_rng.NextDouble() * 4.0f;
                _fishV = Meander(_fishU) + ((float)_rng.NextDouble() - 0.5f) * 0.3f;
                _fishA = IslandView.GridPoint(_fishU, _fishV);
                _fishB = IslandView.GridPoint(_fishU + 0.42f, _fishV);
                _water.SetVector("_Splash0", new Vector4(_fishU, _fishV, lt, 1f));
                _fish.gameObject.SetActive(true);
                k = 0f;
            }
            if (!_fish.gameObject.activeSelf) return;
            const float Height = 44f;
            var p = Vector2.Lerp(_fishA, _fishB, k) + new Vector2(0f, 4f * k * (1f - k) * Height);
            var dp = (_fishB - _fishA) + new Vector2(0f, 4f * (1f - 2f * k) * Height);
            var rt = _fish.rectTransform;
            rt.anchoredPosition = p;
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dp.y, dp.x) * Mathf.Rad2Deg);
            var c = _tint; c.a = Mathf.Clamp01(Mathf.Min(k, 1f - k) * 12f);
            _fish.color = c;
        }
    }
}
