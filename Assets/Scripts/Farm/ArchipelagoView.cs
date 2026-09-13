using System;
using System.Collections.Generic;
using UnityEngine;

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
        const float IslandStep = 1300f;

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
        Vector2? PageTarget(Vector2 cam, Vector2 velocity)
        {
            if (_islands.Count < 2) return null;
            float projected = cam.x + Mathf.Clamp(velocity.x * 0.22f, -IslandStep * 0.6f, IslandStep * 0.6f);
            int nearest = Mathf.RoundToInt(projected / IslandStep);
            nearest = Mathf.Clamp(nearest, CurrentIsland - 1, CurrentIsland + 1);
            nearest = Mathf.Clamp(nearest, 0, _islands.Count - 1);
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

        /// <summary>One bridge per unlocked island, from the island before it.
        ///
        /// Built from the painted sky clouds rather than a new sprite, so the bridge is made of
        /// the same stuff as the weather overhead. Puffs sit on a shallow sagging arc between
        /// the two grass rims, each with a cool shadow puff under it so the path reads as having
        /// volume at every zoom.</summary>
        public void SyncBridges(bool animateNew)
        {
            if (_bridges == null) return;
            var owned = _ctx.owner.islands;
            for (int i = 1; i < _islands.Count; i++)
            {
                bool want = i < owned.Count && owned[i].unlocked;
                while (_bridgeNodes.Count <= i) _bridgeNodes.Add(null);
                if (want && _bridgeNodes[i] == null) _bridgeNodes[i] = BuildBridge(i, animateNew);
                if (!want && _bridgeNodes[i] != null) { Destroy(_bridgeNodes[i].gameObject); _bridgeNodes[i] = null; }
            }
        }

        RectTransform BuildBridge(int to, bool animate)
        {
            var node = UIKit.Node("bridge" + to, _bridges);
            node.Anchor(UIKit.Center, Vector2.zero, Vector2.zero);

            // Each end lands ON the lawn, just past the field's side corner (the field's centre
            // is 26 below the origin, its side corners 477 out), so the bridge visibly leaves one
            // island's grass and arrives on the next — drawn in front of both, never under them.
            Vector2 a = IslandOrigin(to - 1) + new Vector2(505f, -96f);
            Vector2 b = IslandOrigin(to) + new Vector2(-505f, -96f);
            float len = Vector2.Distance(a, b);
            int puffs = Mathf.Max(5, Mathf.CeilToInt(len / 58f)) + 1;
            var rng = new System.Random(733 + to * 11);
            int order = 0;

            // two rows: a heavy base with a cool underside, and a lighter walkway on top that
            // arches up a little in the middle — the arch is what says "bridge"
            for (int row = 0; row < 2; row++)
            {
                bool top = row == 1;
                for (int k = 0; k < puffs; k++)
                {
                    float t = k / (float)(puffs - 1);
                    Vector2 p = Vector2.Lerp(a, b, t);
                    p.y += Mathf.Sin(t * Mathf.PI) * (top ? 30f : 20f) + (top ? 22f : 0f);
                    p.y += (float)(rng.NextDouble() * 8.0 - 4.0);
                    // slim: a path, not a cloud bank sitting on the island's edge
                    float w = top ? 84f + (float)rng.NextDouble() * 24f : 120f + (float)rng.NextDouble() * 36f;
                    var sp = Art.Load("Art/bg/cloud" + (1 + rng.Next(6)));
                    if (sp == null) continue;
                    float h = w * sp.rect.height / sp.rect.width;

                    if (!top)
                    {
                        var shade = UIKit.Img(node, sp, new Color(0.60f, 0.72f, 0.90f, 0.9f), "shade");
                        shade.preserveAspect = true;
                        shade.rectTransform.Anchor(UIKit.Center, p + new Vector2(0f, -18f), new Vector2(w * 1.04f, h));
                        if (animate) Pop(shade.transform, order);
                    }
                    var puff = UIKit.Img(node, sp, top ? Color.white : new Color(0.93f, 0.96f, 1f), "puff");
                    puff.preserveAspect = true;
                    puff.rectTransform.Anchor(UIKit.Center, p, new Vector2(w, h));
                    if (animate) Pop(puff.transform, order);
                    order++;
                }
            }
            return node;
        }

        void Pop(Transform t, int order)
        {
            t.localScale = Vector3.zero;
            StartCoroutine(PopLater(t, 0.045f * order));
        }

        System.Collections.IEnumerator PopLater(Transform t, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            Tween.PopIn(t, 0.34f, 0.2f);
        }

        /// <summary>Seconds the unlock animation takes to reach the far end of a bridge.</summary>
        public const float BridgeBuildSeconds = 0.9f;

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
            foreach (var isl in _islands) isl.ApplyWeatherLook(tint, snow);
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
            const float GridW = 964f, GridH = 640f;
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
            _islands.Add(view);
            return view;
        }

        // ---- forwarding: one island today, the current island tomorrow ----
        public void RenderAll()                 { foreach (var isl in _islands) isl.RenderAll(); }
        public void RenderPlot(int i)           { Current.RenderPlot(i); }
        public List<int> TickingPlots()         { return Current.TickingPlots(); }
        public List<int> ReadyPlots()           { return Current.ReadyPlots(); }
        public List<int> WaterablePlots()       { return Current.WaterablePlots(); }
        public List<int> EmptyPlots()           { return Current.EmptyPlots(); }
        public List<Plot> Plots                 { get { return Current.Plots; } }
        public Vector2 WorldOfPlot(int i)       { return Current.WorldOfPlot(i); }

        /// <summary>A plot's position in archipelago (camera) units, for focusing on it.</summary>
        public Vector2 ContentOfPlot(int i)     { return Current.Field.anchoredPosition + IslandView.CellPos(i); }

        public void SetSelectedPlot(int i)
        {
            for (int k = 0; k < _islands.Count; k++) _islands[k].SetSelected(k == CurrentIsland ? i : -1);
        }

        public bool Plant(int i, string seedId) { return Current.Plant(i, seedId); }
        public bool Water(int i)                { return Current.Water(i); }
        public bool ForceWater(int i)           { return Current.ForceWater(i); }
        public bool InstantGrow(int i)          { return Current.InstantGrow(i); }
        public bool Harvest(int i, out IslandView.HarvestResult res) { return Current.Harvest(i, out res); }
    }
}
