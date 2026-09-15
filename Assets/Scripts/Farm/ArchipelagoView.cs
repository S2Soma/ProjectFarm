using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>The world: every island the player can see, and the one seam between them and
    /// the rest of the game.
    ///
    /// Today it holds exactly one <see cref="IslandView"/> and forwards to it, so nothing has
    /// changed on screen. That is deliberate — this step carries no feature precisely so that any
    /// regression is unambiguous. What it buys is that every caller now goes through an object
    /// that CAN hold more than one island, so adding the second one is a change here rather than
    /// a change everywhere.
    ///
    /// Plot indices in the public API are still bare ints meaning "a plot on the current island".
    /// They become <see cref="PlotRef"/> when there is more than one island to be ambiguous
    /// about; until then a wider signature would just be ceremony.</summary>
    public class ArchipelagoView : MonoBehaviour
    {
        readonly List<IslandView> _islands = new List<IslandView>();

        /// <summary>Island the player is looking at. Every index-based call below is relative
        /// to it.</summary>
        public int CurrentIsland { get; private set; }

        RectTransform _root;

        /// <summary>The node the camera moves and scales. Islands are laid out inside it in
        /// world units; nothing else in the game ever touches its transform.</summary>
        RectTransform _archipelago;

        public MapCamera Camera { get; private set; }

        public Action<int> onPlotTapped;
        public Action<int> onIslandTapped;
        /// <summary>The island the verbs, the pager and plot indices refer to has changed.</summary>
        public Action<int> onIslandChanged;
        /// <summary>A tap on open ground at farm zoom (not a plot, not another island).</summary>
        public Action onGroundTapped;
        public Func<Vector3> storeAnchorWorld;

        /// <summary>Islands sit in a single horizontal row: the game is landscape, so panning is
        /// one axis, the pager is linear, and there is never a need for a minimap. A 2D field
        /// would buy nothing and cost both.
        ///
        /// The step is a little over the island's own 1144 width. The first attempt used 1560,
        /// which put ~400 px of empty sea between every pair — at mid zoom that reads as two
        /// islands with a void between them rather than an archipelago.</summary>
        /// <summary>1670: the diamond islands reach 684 either side of their centre, and a bridge
        /// needs a stretch of open sky to read as a bridge rather than a seam.</summary>
        const float IslandStep = 1670f;

        /// <summary>Islands are nudged off the straight line. A perfectly level row of identical
        /// spacing is the thing that reads as machine-placed; a small stagger is all it takes to
        /// look arranged. Deterministic, so an island never moves between sessions.</summary>
        public static Vector2 IslandOffset(int index)
        {
            if (index == 0) return Vector2.zero;               // home island stays on the axis
            var rng = new System.Random(4127 + index * 17);
            return new Vector2((float)(rng.NextDouble() * 90.0 - 45.0),
                               (float)(rng.NextDouble() * 76.0 - 38.0));
        }

        FarmContext _ctx = FarmContext.Own;

        /// <summary>Whose farm is on screen and what the player may do to it. Set once when
        /// entering a friend's farm; every island inherits it.</summary>
        public FarmContext Ctx
        {
            get { return _ctx; }
            set
            {
                _ctx = value;
                foreach (var isl in _islands) isl.Ctx = value;
            }
        }

        IslandView Current => _islands[Mathf.Clamp(CurrentIsland, 0, _islands.Count - 1)];

        public int IslandCount => _islands.Count;

        void SetCurrent(int index)
        {
            if (index == CurrentIsland) return;
            CurrentIsland = index;
            onIslandChanged?.Invoke(index);
        }

        /// <summary>Look at an island. Index-based plot calls follow it.</summary>
        public void GoToIsland(int index, bool animate = true)
        {
            if (index < 0 || index >= _islands.Count) return;
            SetCurrent(index);
            if (Camera == null) return;
            var target = IslandOrigin(index);
            if (animate) Camera.FlyTo(target, Camera.ZBase);
            else Camera.Snap(target, Camera.ZBase);
        }

        /// <summary>Pull back to the widest zoom and centre on the whole row.
        ///
        /// Deliberately does NOT change <c>CurrentIsland</c>. The bottom bar's three verbs act on
        /// a named island, and a map view that silently re-pointed them at whichever island drifted
        /// nearest the centre would make "Thu hoạch · 7" mean something different from one frame
        /// to the next.</summary>
        public void ShowAll()
        {
            if (Camera == null || Camera.Levels.Count == 0) return;
            float z = Camera.ZBase * Camera.Levels[0];
            Camera.FlyTo(Camera.ContentBounds.center, z, 0.40f);
        }

        /// <summary>The current island's field root — what <c>FitFarm</c> scales.</summary>
        public RectTransform Field => _islands.Count > 0 ? Current.Field : null;

        public void Build(RectTransform parent)
        {
            _root = parent;

            // Sibling 0, full screen, invisible: it catches drags that did not start on a plot.
            // It must be BEHIND the islands or it would eat every plot tap.
            var plane = UIKit.Node("panPlane", parent);
            plane.Stretch();
            var planeImg = plane.gameObject.AddComponent<UnityEngine.UI.Image>();
            planeImg.color = new Color(0, 0, 0, 0);
            plane.SetAsFirstSibling();

            _archipelago = UIKit.Node("archipelago", parent);
            _archipelago.Anchor(UIKit.Center, Vector2.zero, Vector2.zero);

            Camera = gameObject.AddComponent<MapCamera>();
            plane.gameObject.AddComponent<PanPlane>().map = Camera;
            Camera.PageTarget = PageTarget;
            Camera.onTap = OnMapTap;

            // Cloud bridges get their own node, moved IN FRONT of the islands once they exist
            // (see Sync). Behind them, each island's cliff and the next island's rim swallowed
            // the bridge — it read as a cloud sinking into the sea, not a path between lawns.
            _bridges = UIKit.Node("bridges", _archipelago);
            _bridges.Anchor(UIKit.Center, Vector2.zero, Vector2.zero);

            // The pet walks above every island and bridge, on a canvas of its own: it animates
            // every frame, and on the world canvas that would rebuild every island with it.
            PetLayer = UIKit.Node("pets", _archipelago);
            PetLayer.Anchor(UIKit.Center, Vector2.zero, Vector2.zero);
            PetLayer.gameObject.AddComponent<Canvas>();

            Sync();

            Camera.Configure(_archipelago, parent);
            Refit();
        }

        /// <summary>Give every island in the state a view. Islands are only ever appended, so
        /// this is the one call that has to run after an unlock — the view follows the data
        /// rather than the other way round.</summary>
        public void Sync()
        {
            var owned = _ctx.owner.islands;
            while (_islands.Count < owned.Count) AddIsland(_islands.Count);
            if (_bridges != null) _bridges.SetAsLastSibling();
            if (PetLayer != null) PetLayer.SetAsLastSibling();
            SyncBridges(animateNew: false);
            Refit();
        }

        // ============================================================
        // swiping between islands
        // ============================================================
        /// <summary>Where a released swipe should settle.
        ///
        /// Projects the release velocity a short way forward, so a flick goes to the next island
        /// even when the finger lifted before crossing the halfway point, then allows at most ONE
        /// island per swipe — a hard fling must not skip a farm the player never saw.</summary>
        Vector2? PageTarget(Vector2 start, Vector2 cam, Vector2 velocity)
        {
            if (_islands.Count < 2) return null;
            // A flick goes by its direction, a slow drag by how far it went: about a seventh of the
            // screen is enough (7% of the way to the next island at farm zoom). Rounding to the
            // nearest island needed a drag past halfway — a long, hard swipe — and anything shorter
            // sprang back. A nudge under that still settles back where it was.
            float moved = cam.x - start.x;
            int dir = 0;
            if (Mathf.Abs(velocity.x) > 700f) dir = velocity.x > 0f ? 1 : -1;
            else if (Mathf.Abs(moved) > IslandStep * 0.07f) dir = moved > 0f ? 1 : -1;
            int nearest = Mathf.Clamp(Mathf.RoundToInt(start.x / IslandStep), 0, _islands.Count - 1);
            // the camera may not have started exactly on the current island (a page still settling)
            if (Mathf.Abs(start.x - IslandOrigin(CurrentIsland).x) < IslandStep * 0.5f) nearest = CurrentIsland;
            nearest = Mathf.Clamp(nearest + dir, 0, _islands.Count - 1);
            SetCurrent(nearest);
            return IslandOrigin(nearest);
        }

        /// <summary>Taps that miss every plot. Zoomed out, tapping an island flies into it; at farm
        /// zoom, tapping the visible edge of a neighbour moves there, and tapping open ground on
        /// the current island is reported (it closes the seed sheet).</summary>
        void OnMapTap(Vector2 content)
        {
            int idx = Mathf.Clamp(Mathf.RoundToInt(content.x / IslandStep), 0, _islands.Count - 1);
            var local = content - IslandOrigin(idx);
            bool onIsland = Mathf.Abs(local.x) < 560f && local.y > -420f && local.y < 360f;

            if (Camera.Ratio < FarmLodRatio)
            {
                if (onIsland) GoToIsland(idx);
                return;
            }
            if (onIsland && idx != CurrentIsland) { GoToIsland(idx); return; }
            onGroundTapped?.Invoke();
        }

        // ============================================================
        // cloud bridges
        // ============================================================
        RectTransform _bridges;
        readonly List<RectTransform> _bridgeNodes = new List<RectTransform>();

        /// <summary>One bridge per unlocked island, from the island before it.</summary>
        public void SyncBridges(bool animateNew)
        {
            if (_bridges == null) return;
            var owned = _ctx.owner.islands;
            for (int i = 1; i < _islands.Count; i++)
            {
                bool want = i < owned.Count && owned[i].unlocked;
                while (_bridgeNodes.Count <= i) _bridgeNodes.Add(null);
                while (_bridgeViews.Count <= i) _bridgeViews.Add(null);
                if (want && _bridgeNodes[i] == null)
                {
                    var view = BuildBridge(i);
                    _bridgeViews[i] = view;
                    _bridgeNodes[i] = view.root;
                    view.SetLight(BridgeLight(), _lantern);
                    if (animateNew) StartCoroutine(FormBridge(view));
                }
                if (!want && _bridgeNodes[i] != null)
                {
                    Destroy(_bridgeNodes[i].gameObject);
                    _bridgeNodes[i] = null;
                    _bridgeViews[i] = null;
                }
            }
        }

        readonly List<Bridge> _bridgeViews = new List<Bridge>();

        /// <summary>The wood takes the same light as every fence on the islands: the weather's land
        /// tint times the time of day.</summary>
        Color BridgeLight() { var c = LandTint * _ambientLand; c.a = 1f; return c; }

        void RelightBridges()
        {
            var c = BridgeLight();
            foreach (var b in _bridgeViews) b?.SetLight(c, _lantern);
        }

        /// <summary>Half the deck's width, as drawn (vertical offset from the centreline).</summary>
        public const float BridgeDeckHalf = 22f;
        /// <summary>How far past the landing point the end posts stand, and half their drawn width.</summary>
        public const float BridgePostOffset = 6f, BridgePostHalfWidth = 10f;
        const float RailHeight = 30f, Sag = 18f;

        /// <summary>A rope bridge: planks slung on two ropes, a rope handrail either side, a post at
        /// each corner planted on the lawn just outside the gate, and two lanterns hanging off the
        /// near handrail. It is made of what the farm is made of — the posts are the fence's own
        /// post art, the lanterns the gate's own lamps — and it starts on the grass behind its
        /// posts, so there is no edge where the bridge meets the island.</summary>
        class Bridge
        {
            public RectTransform root, lights;
            public BridgeRibbon planks, edge, backRail, frontRail;
            public readonly List<Image> posts = new List<Image>();
            public readonly List<bool> postMirror = new List<bool>();
            public readonly List<Image> lanterns = new List<Image>();
            public readonly List<Image> glows = new List<Image>();
            public readonly List<float> flickerPhase = new List<float>();
            public int plankCount;
            public float[] reveal;
            public float lantern;
            public Color wood = Color.white;

            public IEnumerable<BridgeRibbon> Ribbons { get { yield return planks; yield return edge; yield return backRail; yield return frontRail; } }

            public void SetLight(Color c, float k)
            {
                wood = c;
                planks.SetColors(c, c * new Color(0.93f, 0.93f, 0.93f, 1f));
                edge.SetColors(c, c);
                backRail.SetColors(c * new Color(0.9f, 0.9f, 0.9f, 1f), c * new Color(0.9f, 0.9f, 0.9f, 1f));
                frontRail.SetColors(c, c);
                foreach (var p in posts) p.color = c;
                foreach (var l in lanterns) l.color = c;
                lantern = k;
                ApplyGlow(Time.unscaledTime);
            }

            /// <summary>Lantern light, with a slow uneven flicker. The glows live on their own nested
            /// canvas, so this touches nothing else in the world.</summary>
            public void ApplyGlow(float t)
            {
                for (int i = 0; i < glows.Count; i++)
                {
                    float a = lantern * (reveal != null ? reveal[i] : 1f);
                    bool on = a > 0.01f;
                    if (glows[i].enabled != on) glows[i].enabled = on;
                    if (!on) continue;
                    float f = Flicker(t, flickerPhase[i]);
                    glows[i].color = new Color(1f, 0.74f, 0.36f, 0.80f * a * f);
                }
                if (lights != null && lights.gameObject.activeSelf != (lantern > 0.01f)) lights.gameObject.SetActive(lantern > 0.01f);
            }
        }

        /// <summary>0.9-1.0: two slow sines out of step, so a lantern breathes rather than blinks.</summary>
        public static float Flicker(float t, float phase)
        {
            return 0.92f + 0.05f * Mathf.Sin(t * 5.1f + phase) + 0.03f * Mathf.Sin(t * 13.7f + phase * 2.3f);
        }

        static float Smooth01(float t) { return t * t * (3f - 2f * t); }

        Bridge BuildBridge(int to)
        {
            var br = new Bridge();
            br.root = UIKit.Node("bridge" + to, _bridges);
            br.root.Anchor(UIKit.Center, Vector2.zero, Vector2.zero);

            // Landing points on the lawn outside each gate. The gates are level with the field's
            // centre, 26 below the island's origin.
            float land = IslandView.BridgeLandX;
            Vector2 a = IslandOrigin(to - 1) + new Vector2(land, -26f);
            Vector2 b = IslandOrigin(to) + new Vector2(-land, -26f);
            float span = b.x - a.x;
            int n = Mathf.Max(24, Mathf.CeilToInt(span / 12f)) + 1;

            // the rope sags between the posts; the ends stay level with the gates
            Vector2 At(float t)
            {
                float y = Mathf.Lerp(a.y, b.y, Smooth01(t)) - Sag * Mathf.Sin(Mathf.PI * t);
                return new Vector2(Mathf.Lerp(a.x, b.x, t), y);
            }
            var centre = new Vector2[n];
            for (int i = 0; i < n; i++) centre[i] = At(i / (float)(n - 1));

            float arc = 0f;
            for (int i = 1; i < n; i++) arc += Vector2.Distance(centre[i - 1], centre[i]);

            float[] Const(float v) { var arr = new float[n]; for (int i = 0; i < n; i++) arr[i] = v; return arr; }
            BridgeRibbon Ribbon(string name, BridgeRibbon.Row row, float top, float bottom, float pitch)
            {
                var rt = UIKit.Node(name, br.root);
                rt.Anchor(UIKit.Center, Vector2.zero, Vector2.zero);
                var r = rt.gameObject.AddComponent<BridgeRibbon>();
                r.raycastTarget = false;
                r.row = row;
                // a whole number of planks (or strings) end to end, so neither end is a cut board
                int count = Mathf.Max(1, Mathf.RoundToInt(arc / pitch));
                r.unitsPerTile = BridgeRibbon.DefaultUnitsPerTile * arc / (count * pitch);
                r.SetPath(centre, Const(top), Const(bottom));
                if (row == BridgeRibbon.Row.Planks) br.plankCount = count;
                return r;
            }

            var postSprite = Art.Load("Art/gen/fence_post");
            Image Post(Vector2 at, bool mirror)
            {
                var im = UIKit.Img(br.root, postSprite, Color.white, "post");
                var rt = im.rectTransform;
                rt.anchorMin = rt.anchorMax = UIKit.Center;
                rt.pivot = new Vector2(22.5f / 44f, 7f / 167f);
                rt.sizeDelta = new Vector2(44f, 167f) * 0.46f;
                rt.anchoredPosition = at;
                if (mirror) rt.localScale = new Vector3(-1f, 1f, 1f);
                br.posts.Add(im);
                br.postMirror.Add(mirror);
                return im;
            }

            float h = BridgeDeckHalf;
            float px = BridgePostOffset;
            // back row first: the far posts, then the far handrail tied to them
            Post(a + new Vector2(px, h + 2f), false);
            Post(b + new Vector2(-px, h + 2f), true);
            br.backRail = Ribbon("railBack", BridgeRibbon.Row.Rail, h + RailHeight, h, 32f);
            br.edge = Ribbon("planksEdge", BridgeRibbon.Row.Edge, -h + 1f, -h - 6f, 16f);
            br.planks = Ribbon("planks", BridgeRibbon.Row.Planks, h, -h, 16f);
            // the near posts stand in front of the deck's first boards
            Post(a + new Vector2(px, -h - 2f), false);
            Post(b + new Vector2(-px, -h - 2f), true);
            br.frontRail = Ribbon("railFront", BridgeRibbon.Row.Rail, -h + RailHeight, -h, 32f);

            // two lanterns hanging off the near handrail, a third of the way from each end
            var lanternSprite = Art.Load("Art/bridge/bridge_lantern");
            br.lights = UIKit.Node("lights", br.root);
            br.lights.Anchor(UIKit.Center, Vector2.zero, Vector2.zero);
            br.lights.gameObject.AddComponent<Canvas>();
            var rng = new System.Random(733 + to * 11);
            foreach (float t in new[] { 0.34f, 0.66f })
            {
                var hook = At(t) + new Vector2(0f, -h + RailHeight - 1f);
                var im = UIKit.Img(br.root, lanternSprite, Color.white, "lantern");
                var rt = im.rectTransform;
                rt.anchorMin = rt.anchorMax = UIKit.Center;
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(36f, 74f) * 0.56f;
                rt.anchoredPosition = hook;
                br.lanterns.Add(im);

                var glow = UIKit.Img(br.lights, Theme.Glow(), Color.clear, "glow");
                // the flame sits 51 of the lamp's 74 px down from its hook
                glow.rectTransform.Anchor(UIKit.Center, hook - new Vector2(0f, 51f * 0.56f), new Vector2(96f, 96f));
                glow.enabled = false;
                br.glows.Add(glow);
                br.flickerPhase.Add((float)(rng.NextDouble() * 6.28));
            }
            br.lights.SetAsLastSibling();
            br.reveal = new float[br.glows.Count];
            for (int i = 0; i < br.reveal.Length; i++) br.reveal[i] = 1f;
            return br;
        }

        void FlickerAll()
        {
            if (_lantern <= 0.01f) return;
            float t = Time.unscaledTime;
            foreach (var b in _bridgeViews) b?.ApplyGlow(t);
            foreach (var isl in _islands) isl.FlickerLanterns(t);
        }

        /// <summary>The unlock animation, <see cref="BridgeBuildSeconds"/> long: the near posts go
        /// in, the planks are laid across the gap one at a time with the ropes paying out just
        /// behind them, the far posts go in, and the lanterns are hung.</summary>
        System.Collections.IEnumerator FormBridge(Bridge br)
        {
            foreach (var r in br.Ribbons) r.SetFill(0f);
            for (int i = 0; i < br.posts.Count; i++) br.posts[i].rectTransform.localScale = Vector3.zero;
            foreach (var l in br.lanterns) l.rectTransform.localScale = Vector3.zero;
            for (int i = 0; i < br.reveal.Length; i++) br.reveal[i] = 0f;
            br.ApplyGlow(Time.unscaledTime);

            // posts: 0 far-left, 1 far-right, 2 near-left, 3 near-right
            PopPost(br, 0, 0.22f);
            PopPost(br, 2, 0.22f);

            float t0 = Time.unscaledTime;
            int lastBoards = 0;
            while (true)
            {
                float e = Time.unscaledTime - t0;
                if (e >= 0.95f) break;
                float k = Mathf.Clamp01((e - 0.10f) / 0.85f);
                k = k < 0.5f ? 2f * k * k : 1f - Mathf.Pow(-2f * k + 2f, 2f) / 2f;
                // whole planks only: each one lands, rather than the deck sliding out
                float laid = Mathf.Floor(k * br.plankCount) / br.plankCount;
                // a knock for every third plank as it lands
                int boards = Mathf.FloorToInt(k * br.plankCount);
                if (boards != lastBoards && boards % 3 == 0 && boards > 0) Sfx.Play(SfxId.Plank);
                lastBoards = boards;
                br.planks.SetFill(laid);
                br.edge.SetFill(laid);
                float rope = Mathf.Clamp01(k - 0.04f);
                br.backRail.SetFill(rope);
                br.frontRail.SetFill(rope);
                yield return null;
            }
            foreach (var r in br.Ribbons) r.SetFill(1f);

            PopPost(br, 1, 0.26f);
            PopPost(br, 3, 0.26f);
            for (int i = 0; i < br.lanterns.Count; i++)
            {
                yield return new WaitForSecondsRealtime(0.1f);
                PopHanging(br.lanterns[i].rectTransform);
                StartCoroutine(RevealGlow(br, i, 0.3f));
            }
        }

        /// <summary>Pop a post in. PopIn would reset its scale to (1,1,1), and a mirrored post
        /// has to keep its -1 — which cannot be read back while it is hidden at scale 0.</summary>
        void PopPost(Bridge br, int i, float time)
        {
            StartCoroutine(PopRoutine(br.posts[i].rectTransform, time, br.postMirror[i] ? -1f : 1f));
        }

        System.Collections.IEnumerator PopRoutine(RectTransform rt, float time, float sx)
        {
            for (float e = 0f; e < time; e += Time.unscaledDeltaTime)
            {
                if (rt == null) yield break;
                float k = Tween.EaseBack(Mathf.Clamp01(e / time));
                float s = Mathf.LerpUnclamped(0.2f, 1f, k);
                rt.localScale = new Vector3(sx * s, s, 1f);
                yield return null;
            }
            if (rt != null) rt.localScale = new Vector3(sx, 1f, 1f);
        }

        /// <summary>A lantern is hung: it drops in from its hook and swings to rest.</summary>
        void PopHanging(RectTransform rt) { StartCoroutine(HangRoutine(rt)); }

        System.Collections.IEnumerator HangRoutine(RectTransform rt)
        {
            const float time = 0.6f;
            for (float e = 0f; e < time; e += Time.unscaledDeltaTime)
            {
                if (rt == null) yield break;
                float k = Mathf.Clamp01(e / time);
                rt.localScale = Vector3.one * Mathf.Min(1f, k * 4f);
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(k * 14f) * 14f * (1f - k));
                yield return null;
            }
            if (rt != null) { rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity; }
        }

        System.Collections.IEnumerator RevealGlow(Bridge br, int i, float time)
        {
            for (float e = 0f; e < time; e += Time.unscaledDeltaTime)
            {
                br.reveal[i] = e / time;
                br.ApplyGlow(Time.unscaledTime);
                yield return null;
            }
            br.reveal[i] = 1f;
            br.ApplyGlow(Time.unscaledTime);
        }

        /// <summary>Seconds the unlock animation takes to reach the far end of a bridge.</summary>
        public const float BridgeBuildSeconds = 1.25f;

        // ============================================================
        // weather look
        // ============================================================
        /// <summary>What an hour of weather does to the land itself: a colour multiplier on the
        /// island and how much snow lies on it. The sky, rain and light live in WeatherFx; this is
        /// the part that has to move WITH the camera, so it belongs to the world.</summary>
        public static void LandLook(Weather w, out Color tint, out float snow)
        {
            snow = 0f;
            switch (w)
            {
                case Weather.Rain:    tint = new Color(0.84f, 0.91f, 0.94f); break;   // wet, darker
                case Weather.Storm:   tint = new Color(0.66f, 0.72f, 0.84f); break;   // under cloud
                case Weather.Snow:    tint = new Color(0.90f, 0.95f, 1.00f); snow = 1f; break;
                case Weather.Drought: tint = new Color(1.06f, 0.92f, 0.66f); break;   // scorched
                case Weather.Wind:    tint = new Color(0.97f, 1.00f, 0.93f); break;
                default:              tint = Color.white; break;
            }
        }

        /// <summary>Blend the land from one weather's look to another's.</summary>
        public void SetWeatherLook(Weather from, Weather to, float t)
        {
            LandLook(from, out var ca, out var sa);
            LandLook(to, out var cb, out var sb);
            var tint = Color.Lerp(ca, cb, t);
            float snow = Mathf.Lerp(sa, sb, t);
            LandTint = tint;
            foreach (var isl in _islands) isl.ApplyWeatherLook(tint, snow);
            RelightBridges();
        }

        /// <summary>The weather's tint on the land as last applied — the readability floor counts it.</summary>
        public Color LandTint { get; private set; } = Color.white;

        Color _ambientLand = Color.white, _ambientCrop = Color.white;
        float _lantern;

        /// <summary>The time of day's light, already floored for readability (see SkyView).</summary>
        public void SetAmbient(Color land, Color crop)
        {
            _ambientLand = land; _ambientCrop = crop;
            foreach (var isl in _islands) isl.SetAmbient(land, crop);
            RelightBridges();
        }

        public void SetLanternLight(float k)
        {
            _lantern = k;
            foreach (var isl in _islands) isl.SetLanternLight(k);
            RelightBridges();
        }

        /// <summary>Recompute the fit for the current canvas. Called on every resize, which is
        /// what used to be GameApp.FitFarm.</summary>
        public void Refit()
        {
            if (Camera == null) return;
            Camera.ContentBounds = BoundsFor(_islands.Count);
            Camera.Recompute(_islands.Count);
            ApplyLod();
        }

        /// <summary>Bring the world in line with how far out the camera is.
        ///
        /// The rule the whole zoom design rests on: BELOW the farm level a plot is not a tap
        /// target, so the plot raycaster is switched off entirely. A mis-tap then becomes
        /// impossible rather than merely unlikely, which is a much stronger guarantee than
        /// widening hit areas could ever give.
        ///
        /// The other half is text. Field countdowns are the only text in the world, they are
        /// drawn with a dynamic OS font, and a dynamic font atlas re-rasterizes on demand — a
        /// rebuild during a pinch dirties every canvas referencing that atlas. Hiding them below
        /// the farm level removes the failure mode exactly where many canvases are on screen at
        /// once, and they would be unreadable at that size anyway.</summary>
        public void ApplyLod()
        {
            if (Camera == null) return;
            bool farm = Camera.Ratio >= FarmLodRatio;
            for (int i = 0; i < _islands.Count; i++) _islands[i].SetLod(farm);
        }

        /// <summary>Zoom ratio at or above which plots are directly manipulable.</summary>
        public const float FarmLodRatio = 0.80f;

        void LateUpdate()
        {
            FlickerAll();
            // The camera animates, so LOD has to follow it rather than only changing on input.
            if (Camera == null) return;
            bool farm = Camera.Ratio >= FarmLodRatio;
            if (farm == _lodFarm) return;
            _lodFarm = farm;
            ApplyLod();
        }

        bool _lodFarm = true;

        /// <summary>Extent of the whole row, in archipelago units.
        ///
        /// Measured on the GRID, not on the island sprite — the same choice the old fit formula
        /// made, and for the same reason: the island's soil cliff is meant to bleed off the
        /// bottom of the screen like a backdrop, so including it would let the player drag the
        /// farm around inside an otherwise empty view.
        ///
        /// The consequence is the property that matters at this step: at base zoom a single
        /// island is SMALLER than the viewport on both axes, so it centres and cannot be panned
        /// at all — exactly as it behaved before there was a camera. Panning starts existing
        /// only once the player zooms in, or once there is a second island to pan to.</summary>
        static Rect BoundsFor(int count)
        {
            // Grid half-extents are 477 x 239 (4 touching cells of 168x84 at PlotScale 1.42);
            // the vertical slack is the tallest crop plus its ready badge, which must not be
            // clipped at the back row.
            //
            // Centred on y = 0, NOT on the grid's own centre. The field is deliberately anchored
            // 26 px below the middle of the screen, and since the camera centres whatever it is
            // given, bounds around the grid centre would silently cancel that offset and lift the
            // whole farm — which is exactly what happened the first time.
            // GridW covers the island's grass tips (±684), not just the beds, so the overview and a
            // pan to the end of the row show whole islands.
            const float GridW = 1400f, GridH = 640f;
            float span = GridW + IslandStep * Mathf.Max(0, count - 1);
            // the stagger pushes islands up to 45 px sideways and 38 px vertically
            const float Stagger = 48f;
            return new Rect(-GridW * 0.5f - Stagger, -GridH * 0.5f - Stagger,
                            span + Stagger * 2f, GridH + Stagger * 2f);
        }

        /// <summary>Where island <paramref name="index"/> sits in the row.</summary>
        public static Vector2 IslandOrigin(int index)
        {
            return new Vector2(index * IslandStep, 0f) + IslandOffset(index);
        }

        IslandView AddIsland(int index)
        {
            // Its own GameObject, not another component on this one: several IslandViews sharing
            // a host would make GetComponent<IslandView>() ambiguous and SetActive un-targetable,
            // and pooling at the multi-island step needs to toggle exactly one island at a time.
            var go = new GameObject("island" + index);
            go.transform.SetParent(transform, false);

            var view = go.AddComponent<IslandView>();
            view.islandIndex = index;
            view.Ctx = _ctx;
            view.map = Camera;
            // A plot tap names its island. Plot indices mean "on the current island" everywhere
            // downstream, so tapping a plot on a neighbour's visible edge must make that island
            // current FIRST — otherwise the popup opened for the same index on the wrong farm.
            view.onPlotTapped = i => { if (index != CurrentIsland) GoToIsland(index); onPlotTapped?.Invoke(i); };
            // A tap on a locked island also MOVES to it. Paying tribute to an island you are not
            // looking at is the one case where the panel and the map can disagree about what the
            // player means, so the camera goes where the finger did.
            int islandIdx = view.islandIndex;
            view.onIslandTapped = i => { GoToIsland(islandIdx); onIslandTapped?.Invoke(i); };
            view.storeAnchorWorld = () => storeAnchorWorld != null ? storeAnchorWorld() : Vector3.zero;
            view.Build(_archipelago);
            view.Field.anchoredPosition += IslandOrigin(index);
            // an island added at dusk starts in dusk's light, not noon's
            view.SetAmbient(_ambientLand, _ambientCrop);
            view.SetLanternLight(_lantern);
            _islands.Add(view);
            return view;
        }

        // ---- forwarding: one island today, the current island tomorrow ----
        public void RenderAll()                 { foreach (var isl in _islands) isl.RenderAll(); }
        public void RefreshStale()              { foreach (var isl in _islands) isl.RefreshStale(); }
        public void RenderPlot(int i)           { Current.RenderPlot(i); }
        public List<int> TickingPlots()         { return Current.TickingPlots(); }
        public List<int> ReadyPlots()           { return Current.ReadyPlots(); }
        public List<int> WaterablePlots()       { return Current.WaterablePlots(); }
        public List<int> EmptyPlots()           { return Current.EmptyPlots(); }
        public List<Plot> Plots                 { get { return Current.Plots; } }
        public Vector2 WorldOfPlot(int i)       { return Current.WorldOfPlot(i); }
        public RectTransform PlotRoot(int i)    { return _islands.Count > 0 ? Current.PlotRoot(i) : null; }

        /// <summary>A plot's position in archipelago (camera) units, for focusing on it.</summary>
        public Vector2 ContentOfPlot(int i)     { return Current.Field.anchoredPosition + Current.SlotPos(i); }

        public void SetSelectedPlot(int i)
        {
            for (int k = 0; k < _islands.Count; k++) _islands[k].SetSelected(k == CurrentIsland ? i : -1);
        }

        // ---- the pet works on any island, not only the one being looked at ----
        /// <summary>Where the pet walks: above islands and bridges, in archipelago space.</summary>
        public RectTransform PetLayer { get; private set; }

        /// <summary>A plot's centre in <see cref="PetLayer"/> space.</summary>
        public Vector2 PetPosOfPlot(int island, int i)
        {
            if (island < 0 || island >= _islands.Count || PetLayer == null) return Vector2.zero;
            var root = _islands[island].PlotRoot(i);
            return root != null ? (Vector2)PetLayer.InverseTransformPoint(root.position) : Vector2.zero;
        }

        /// <summary>A plot the rain just watered: redraw it and let the drops fall.</summary>
        public void ShowRainWater(int island, int i)
        {
            if (island < 0 || island >= _islands.Count) return;
            _islands[island].RenderPlot(i);
            _islands[island].Droplets(i);
        }

        public bool WaterOn(int island, int i)
        {
            return island >= 0 && island < _islands.Count && _islands[island].Water(i);
        }

        public bool HarvestOn(int island, int i, out IslandView.HarvestResult res)
        {
            res = default;
            return island >= 0 && island < _islands.Count && _islands[island].Harvest(i, out res);
        }

        public bool Plant(int i, string seedId) { return Current.Plant(i, seedId); }
        public bool Water(int i)                { return Current.Water(i); }
        public bool ForceWater(int i)           { return Current.ForceWater(i); }
        public bool InstantGrow(int i)          { return Current.InstantGrow(i); }
        public bool Hasten(int i, float frac)   { return Current.Hasten(i, frac); }
        public bool Harvest(int i, out IslandView.HarvestResult res, int sweepIndex = -1) { return Current.Harvest(i, out res, sweepIndex); }
        public void PreviewHarvestFx(int i) { Current.PreviewHarvestFx(i); }
        public void PreviewCosmeticFx(int i, string id) { Current.PreviewCosmeticFx(i, id); }
    }
}
