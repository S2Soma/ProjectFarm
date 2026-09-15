using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>The pet on the map: its sprites and how it moves. No rules here — see
    /// <see cref="PetDirector"/> for what it decides and <see cref="PetSys"/> for the numbers.</summary>
    public class PetActor
    {
        /// <summary>Its pivot is the bottom-centre: <c>anchoredPosition</c> is where its feet touch the ground.</summary>
        public RectTransform Root { get; private set; }
        public string PetId { get; private set; }

        RectTransform _body;
        Image _sprite, _shadow;
        RectTransform _bubble;
        Image _bubbleIcon;
        Text _zzz;
        Sprite _idle, _sleep, _happy;
        readonly List<Sprite> _walk = new List<Sprite>();

        public enum Pose { Idle, Walk, Sleep, Happy, Jump }
        Pose _pose;
        float _poseT, _facing = 1f, _landT = 1f;
        public Pose CurrentPose => _pose;

        /// <summary>Height of the feet above the ground: a hop over the river. The shadow stays down.</summary>
        public float Lift;
        /// <summary>How much of the walking bounce to play (a rope bridge takes shorter steps).</summary>
        public float StepBounce = 1f;
        /// <summary>An extra vertical nudge of the body (the planks giving underfoot).</summary>
        public float Sway;

        public const float Height = 118f;

        public static PetActor Build(RectTransform layer)
        {
            var a = new PetActor();
            a.Root = UIKit.Node("pet", layer);
            a.Root.Anchor(UIKit.Center, Vector2.zero, new Vector2(Height, Height));
            a.Root.pivot = new Vector2(0.5f, 0f);

            a._shadow = UIKit.Img(a.Root, Theme.Glow(), new Color(0.05f, 0.12f, 0.06f, 0.45f), "shadow");
            a._shadow.rectTransform.Anchor(UIKit.Bottom, new Vector2(0, -8), new Vector2(Height * 0.72f, 26));
            a._shadow.raycastTarget = false;

            a._body = UIKit.Node("body", a.Root);
            a._body.anchorMin = a._body.anchorMax = new Vector2(0.5f, 0f);
            a._body.pivot = new Vector2(0.5f, 0f);
            a._body.sizeDelta = new Vector2(Height, Height);
            a._body.anchoredPosition = Vector2.zero;
            a._sprite = UIKit.Img(a._body, null, Color.white, "sprite");
            a._sprite.preserveAspect = true;
            a._sprite.raycastTarget = false;
            a._sprite.rectTransform.Stretch();

            a._zzz = UIKit.LabelOutlined(a.Root, "z", 26, Color.white, TextAnchor.MiddleCenter);
            a._zzz.rectTransform.Anchor(UIKit.Center, new Vector2(34, 60), new Vector2(60, 40));
            a._zzz.gameObject.SetActive(false);

            a._bubble = UIKit.Node("bubble", a.Root);
            a._bubble.Anchor(UIKit.Bottom, new Vector2(0, Height - 14), new Vector2(64, 60));
            var look = Looks.Paper;
            look.shadow = new Color(0, 0, 0, 0.25f); look.blur = 6f; look.drop = new Vector2(0, -2);
            SurfaceLook.Add(a._bubble, look, 22f);
            a._bubbleIcon = UIKit.Img(a._bubble, null, Color.white, "ic");
            a._bubbleIcon.preserveAspect = true;
            a._bubbleIcon.rectTransform.Stretch(9, 9, 9, 9);
            a._bubble.gameObject.SetActive(false);
            return a;
        }

        public void SetPet(string id)
        {
            if (id == PetId) return;
            PetId = id;
            string dir = "Art/pets/" + id + "/";
            _idle = Art.Load(dir + "idle");
            _sleep = Art.Load(dir + "sleep");
            _happy = Art.Load(dir + "happy");
            _walk.Clear();
            for (int i = 0; i < 8; i++)
            {
                var sp = Art.Load(dir + "walk_" + i);
                if (sp == null) break;
                _walk.Add(sp);
            }
            SetPose(_pose);
        }

        public void SetPose(Pose p)
        {
            _pose = p; _poseT = 0f;
            _zzz.gameObject.SetActive(p == Pose.Sleep);
            _sprite.sprite = p == Pose.Sleep ? _sleep
                           : p == Pose.Happy || p == Pose.Jump ? (_happy != null ? _happy : _idle)
                           : p == Pose.Walk && _walk.Count > 0 ? _walk[0] : _idle;
        }

        /// <summary>Turn to face along <paramref name="dx"/> (screen units); tiny moves keep the old facing.</summary>
        public void Face(float dx) { if (Mathf.Abs(dx) > 1f) _facing = dx < 0f ? -1f : 1f; }

        /// <summary>A little squash as the feet come down after a hop.</summary>
        public void Land() { _landT = 0f; }

        public void ShowBubble(Sprite icon, Color tint)
        {
            _bubbleIcon.sprite = icon;
            _bubbleIcon.color = tint;
            _bubble.gameObject.SetActive(icon != null);
            if (icon != null) Tween.PopIn(_bubble, 0.25f, 0.4f);
        }

        public void HideBubble() { _bubble.gameObject.SetActive(false); }

        /// <summary>Per frame while on screen: walk cycle, the bounce, breathing, the floating z.</summary>
        public void Tick(float dt)
        {
            _poseT += dt;
            _landT += dt;
            float hop = 0f, squash = 1f;
            switch (_pose)
            {
                case Pose.Walk:
                    if (_walk.Count > 0) _sprite.sprite = _walk[(int)(_poseT * 10f) % _walk.Count];
                    hop = Mathf.Abs(Mathf.Sin(_poseT * 9f)) * 9f * StepBounce;
                    squash = 1f + Mathf.Sin(_poseT * 18f) * 0.03f;
                    break;
                case Pose.Idle:
                    squash = 1f + Mathf.Sin(_poseT * 2.6f) * 0.025f;
                    break;
                case Pose.Sleep:
                    squash = 1f + Mathf.Sin(_poseT * 1.6f) * 0.03f;
                    float u = Mathf.Repeat(_poseT * 0.6f, 1f);
                    _zzz.rectTransform.anchoredPosition = new Vector2(30 + u * 16f, 50 + u * 40f);
                    _zzz.color = new Color(1f, 1f, 1f, Mathf.Sin(u * Mathf.PI));
                    _zzz.fontSize = 18 + (int)(u * 12f);
                    break;
                case Pose.Happy:
                    hop = Mathf.Abs(Mathf.Sin(_poseT * 12f)) * 14f * Mathf.Clamp01(1f - _poseT);
                    break;
                case Pose.Jump:
                    squash = 1.07f;                       // stretched out in the air
                    break;
            }
            if (_landT < 0.18f) squash *= 1f - 0.12f * Mathf.Sin(_landT / 0.18f * Mathf.PI);
            _body.anchoredPosition = new Vector2(0, hop + Lift + Sway);
            _body.localScale = new Vector3(_facing * (2f - squash), squash, 1f);
            float air = Mathf.Clamp01((hop + Lift) / 60f);
            _shadow.color = new Color(0.05f, 0.12f, 0.06f, 0.45f * (1f - air * 0.6f));
            _shadow.rectTransform.localScale = Vector3.one * (1f - air * 0.35f);
        }
    }

    /// <summary>The pet's day. It LIVES on one island at a time — the one it last walked to — and stays
    /// there when the player swipes elsewhere; a new session starts it on Vườn Nhà. Between patrols it
    /// wanders its island's yard (now and then over a bridge to the next open island); every three minutes
    /// it patrols (<see cref="PetSys.FindJobs"/>: its own island first, then the nearest ones), then maybe
    /// helps itself to a snack from the warehouse.
    ///
    /// Every move is a walk on <see cref="PetPaths"/>: inside the fence, round the props, through the gate,
    /// along the rope bridge's own curve, across Đảo Nước by its one hop over the river. Nothing teleports.
    /// Work goes through <see cref="GameApp.PetWork"/>, which calls the same view methods a tap does, so
    /// every harvest flies to the warehouse and counts for missions exactly as the player's own.
    ///
    /// Off screen the pet keeps walking (a position per frame) but stops animating and is hidden.</summary>
    public class PetDirector : MonoBehaviour
    {
        GameApp _app;
        PetActor _actor;
        long _nextPatrol;
        bool _patrolling, _wandering, _stopWander, _audit;
        Coroutine _run;
        float _wanderAt;

        // ---- where the pet is ----
        /// <summary>The island it stands on — or, on a bridge, the one it last left.</summary>
        int _island = -1;
        /// <summary>Its feet, in that island's grid cells (PetPaths).</summary>
        Vector2 _cell;
        /// <summary>The bridge it is on (numbered by the island it leads to), or -1.</summary>
        int _bridge = -1;
        float _bridgeS;

        /// <summary>First patrol shortly after the game opens, so a returning player sees the pet
        /// get to work instead of waiting three minutes for proof it does anything.</summary>
        const long FirstPatrolMs = 20_000L;
        /// <summary>Screen units a second on grass, and on a bridge (the planks are for crossing, not
        /// strolling). An island gate to gate is about seven seconds.</summary>
        public const float Speed = 190f, BridgeSpeed = 235f;
        const float HopSeconds = 0.46f, HopHeight = 46f;
        /// <summary>The feet ride this far below the deck's centreline: between the two ropes, on the boards.</summary>
        const float DeckFeet = -3f;

        public static PetDirector Attach(GameApp app)
        {
            var d = app.gameObject.AddComponent<PetDirector>();
            d._app = app;
            d._nextPatrol = GS.Now + FirstPatrolMs;
            return d;
        }

        /// <summary>Seconds until the next patrol, for the pet panel; 0 while one is running.</summary>
        public int SecondsToPatrol => _patrolling || _audit ? 0 : Mathf.Max(0, (int)((_nextPatrol - GS.Now) / 1000L));
        /// <summary>On patrol (wandering does not count).</summary>
        public bool Busy => _patrolling;
        /// <summary>The island the pet is on (or last left, while on a bridge); -1 before it is placed.</summary>
        public int Island => _island;
        public bool OnBridge => _bridge >= 0;
        /// <summary>The island to look at to see the pet: its own, or on a bridge the end it is nearer.</summary>
        public int NearestIsland
        {
            get
            {
                if (_bridge >= 0) return _bridgeS < PetPaths.Bridge(_bridge).Length * 0.5f ? _bridge - 1 : _bridge;
                return Mathf.Max(0, _island);
            }
        }
        public Vector2 Cell => _cell;
        public PetActor Actor => _actor;

        /// <summary>Start a patrol now (the "Đi tuần ngay" button and the screenshot pass).</summary>
        public void PatrolNow() { if (!_patrolling) _nextPatrol = GS.Now; }

        static bool Open(PlayerState s, int i) { return i >= 0 && i < s.islands.Count && s.islands[i].unlocked; }

        void Update()
        {
            var farm = _app != null ? _app.Farm : null;
            var s = GS.Local;
            if (farm == null || farm.PetLayer == null || s == null) return;
            var pet = PetSys.Unlocked(s) ? PetSys.Active(s) : null;

            if (pet == null)
            {
                if (_actor != null) _actor.Root.gameObject.SetActive(false);
                return;
            }
            if (_actor == null) _actor = PetActor.Build(farm.PetLayer);
            PetPaths.WornDecor = Cosmetics.Worn(s, CosmeticSlot.Decor);
            if (_actor.PetId != pet.id) _actor.SetPet(pet.id);
            if (_island < 0 || (!_audit && !Open(s, _island) && _bridge < 0)) PlaceHome();

            // off screen: keep the position, skip the animation and the draw
            bool visible = farm.PetOnScreen(_actor.Root.anchoredPosition, 160f);
            if (_actor.Root.gameObject.activeSelf != visible) _actor.Root.gameObject.SetActive(visible);
            if (visible) _actor.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f));

            if (_audit || _patrolling) return;
            if (GS.Now >= _nextPatrol && !_app.Tutorial.Blocking)
            {
                // a stroll in progress stops at its next safe step (never on a bridge, never mid-hop)
                if (_wandering) { _stopWander = true; return; }
                _nextPatrol = GS.Now + PetSys.PatrolMs;
                _run = StartCoroutine(Patrol(pet));
                return;
            }
            if (!_wandering && Time.unscaledTime >= _wanderAt) _run = StartCoroutine(Wander());
        }

        // ============================================================
        // where the pet is drawn
        // ============================================================
        /// <summary>A new session: somewhere in Vườn Nhà's back yard.</summary>
        void PlaceHome()
        {
            _island = 0;
            _bridge = -1;
            _cell = PetPaths.For(0).YardSpot(() => Random.value, Vector2.zero, 0f, 1e6f);
            Apply();
            _actor.SetPose(PetActor.Pose.Idle);
        }

        void Apply()
        {
            if (_actor == null) return;
            if (_bridge >= 0)
            {
                _actor.Root.anchoredPosition = PetPaths.Bridge(_bridge).At(_bridgeS) + new Vector2(0f, DeckFeet);
                return;
            }
            _actor.Root.anchoredPosition = ArchipelagoView.PetPosOfCell(_island, _cell);
        }

        /// <summary>Face along a stretch of an island, by its direction on screen.</summary>
        void FaceCells(Vector2 a, Vector2 b) { _actor.Face(IslandView.GridPoint(b.x - a.x, b.y - a.y).x); }

        // ============================================================
        // walking
        // ============================================================
        /// <summary>Walk a whole trip. <paramref name="interruptible"/>: a stroll, which stops where it
        /// stands when a patrol comes due — only ever on an island, between steps.</summary>
        IEnumerator Follow(List<PetLeg> legs, bool interruptible)
        {
            if (legs == null) yield break;
            float t0 = Time.unscaledTime;
            for (int li = 0; li < legs.Count; li++)
            {
                var leg = legs[li];
                if (leg.bridge >= 0) { yield return CrossBridge(leg); continue; }
                bool last = li == legs.Count - 1;
                var pts = leg.points;
                _island = leg.island;
                int k = 1;
                float along = 0f;
                while (k < pts.Count)
                {
                    if (pts[k].hop)
                    {
                        yield return Hop(pts[k - 1].cell, pts[k].cell);
                        k++; along = 0f;
                        continue;
                    }
                    if (interruptible && _stopWander) yield break;
                    if (_actor.CurrentPose != PetActor.Pose.Walk) _actor.SetPose(PetActor.Pose.Walk);

                    float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                    // ease in over the first fifth of a second of the trip, out over the last few steps
                    float ease = Mathf.Clamp01(0.35f + (Time.unscaledTime - t0) * 3.2f);
                    if (last && k == pts.Count - 1)
                    {
                        float left = PetPaths.WorldLength(pts[k].cell - pts[k - 1].cell) - along;
                        ease = Mathf.Min(ease, Mathf.Clamp(left / 40f, 0.3f, 1f));
                    }
                    float step = Speed * ease * dt;
                    while (step > 0f && k < pts.Count && !pts[k].hop)
                    {
                        float seg = PetPaths.WorldLength(pts[k].cell - pts[k - 1].cell);
                        if (step < seg - along) { along += step; step = 0f; }
                        else { step -= seg - along; along = 0f; k++; }
                    }
                    if (k < pts.Count)
                    {
                        var a = pts[k - 1].cell; var b = pts[k].cell;
                        float seg = Mathf.Max(1e-4f, PetPaths.WorldLength(b - a));
                        _cell = Vector2.Lerp(a, b, Mathf.Clamp01(along / seg));
                        FaceCells(a, b);
                    }
                    else _cell = pts[pts.Count - 1].cell;
                    Apply();
                    yield return null;
                }
            }
            _actor.StepBounce = 1f;
            _actor.Sway = 0f;
            _actor.SetPose(PetActor.Pose.Idle);
        }

        /// <summary>Across one rope bridge on the deck's own curve, a bit quicker than on grass, with shorter
        /// steps and the planks giving a little underfoot.</summary>
        IEnumerator CrossBridge(PetLeg leg)
        {
            var path = PetPaths.Bridge(leg.bridge);
            _island = leg.forward ? leg.bridge - 1 : leg.bridge;
            _bridge = leg.bridge;
            _bridgeS = leg.forward ? 0f : path.Length;
            _actor.Face(leg.forward ? 10f : -10f);
            _actor.SetPose(PetActor.Pose.Walk);
            _actor.StepBounce = 0.55f;
            float t = 0f;
            while (true)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                t += dt;
                _bridgeS += (leg.forward ? 1f : -1f) * BridgeSpeed * dt;
                bool done = leg.forward ? _bridgeS >= path.Length : _bridgeS <= 0f;
                _bridgeS = Mathf.Clamp(_bridgeS, 0f, path.Length);
                // deepest where the rope sags most
                float mid = Mathf.Sin(Mathf.PI * _bridgeS / Mathf.Max(1f, path.Length));
                _actor.Sway = -Mathf.Abs(Mathf.Sin(t * 9f)) * 2.5f * mid;
                Apply();
                if (done) break;
                yield return null;
            }
            _bridge = -1;
            _island = leg.forward ? leg.bridge : leg.bridge - 1;
            _cell = leg.forward ? PetPaths.LeftLanding : PetPaths.RightLanding;
            _actor.StepBounce = 1f;
            _actor.Sway = 0f;
            Apply();
        }

        /// <summary>Đảo Nước's crossing: a crouch, a jump over the water, a squash on the far bank.</summary>
        IEnumerator Hop(Vector2 from, Vector2 to)
        {
            FaceCells(from, to);
            _actor.SetPose(PetActor.Pose.Idle);
            _actor.Land();                                  // the crouch before the jump
            for (float e = 0f; e < 0.14f; e += Time.unscaledDeltaTime) yield return null;
            _actor.SetPose(PetActor.Pose.Jump);
            for (float e = 0f; e < HopSeconds; e += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
            {
                float k = e / HopSeconds;
                _cell = Vector2.Lerp(from, to, k);
                _actor.Lift = 4f * k * (1f - k) * HopHeight;
                Apply();
                yield return null;
            }
            _cell = to;
            _actor.Lift = 0f;
            _actor.Land();
            Apply();
            _actor.SetPose(PetActor.Pose.Walk);
        }

        // ============================================================
        // the patrol
        // ============================================================
        IEnumerator Patrol(PetDef pet)
        {
            _patrolling = true;
            var s = GS.Local;
            float t0 = Time.unscaledTime;
            int max = PetSys.JobsPerPatrol(pet, PetSys.LevelOf(s, pet.id));
            var jobs = WalkingOrder(PetSys.FindJobs(s, max, _island, PetSys.MaxPatrolHops));
            int done = 0;
            foreach (var job in jobs)
            {
                if (Time.unscaledTime - t0 > PetSys.PatrolSeconds) break;
                // plots change while the pet walks: the player may have got there first
                if (!PetSys.StillWanted(s, job)) continue;
                var near = job.island == _island ? _cell : job.island > _island ? PetPaths.LeftLanding : PetPaths.RightLanding;
                var spot = PetPaths.For(job.island).WorkSpot(job.plot, near);
                if (spot == null) continue;
                var legs = PetPaths.Route(_island, _cell, job.island, spot.Value, i => Open(s, i));
                if (legs == null) continue;
                float eta = PetPaths.Length(legs) / Speed;
                if (done > 0 && Time.unscaledTime - t0 + eta > PetSys.PatrolSeconds) break;
                yield return Follow(legs, false);
                if (!PetSys.StillWanted(s, job)) continue;
                FaceCells(_cell, IslandSys.SlotCell(job.island, job.plot));
                if (_app.PetWork(job))
                {
                    done++;
                    _actor.SetPose(PetActor.Pose.Happy);
                    yield return new WaitForSecondsRealtime(0.55f);
                }
            }

            if (done > 0) s.stats.petJobs += done;
            // a snack, sometimes — any produce in the warehouse, never from an empty one
            if (Random.value < PetSys.SnackChance)
            {
                string key = PetSys.PickSnack(s, pet, Random.value);
                if (key != null && PetSys.Eat(s, key))
                {
                    var seed = GameData.Get(PetSys.Crop(key));
                    int v = PetSys.Variant(key);
                    _actor.SetPose(PetActor.Pose.Happy);
                    _actor.ShowBubble(seed != null ? Art.Icon(seed.art, v) : null, Color.white);
                    _app.PetAte(pet, seed, v);
                    yield return new WaitForSecondsRealtime(2.2f);
                    _actor.HideBubble();
                }
            }
            _actor.SetPose(PetActor.Pose.Idle);
            _wanderAt = Time.unscaledTime + Random.Range(3f, 6f);
            _patrolling = false;
            _run = null;
        }

        /// <summary>The patrol's jobs in the order the pet walks them. FindJobs picked WHICH (nearest islands
        /// first); this picks the route: its own island, then the side whose farthest job is nearer (so the
        /// walk ends on the far side instead of coming back), then the other; on each island thirsty plots
        /// before ripe ones, each time the nearest one next.</summary>
        List<PetJob> WalkingOrder(List<PetJob> jobs)
        {
            int left = 0, right = 0;
            foreach (var j in jobs)
            {
                if (j.island < _island) left = Mathf.Max(left, _island - j.island);
                if (j.island > _island) right = Mathf.Max(right, j.island - _island);
            }
            var islands = new List<int> { _island };
            bool leftFirst = left > 0 && (right == 0 || left <= right);
            for (int pass = 0; pass < 2; pass++)
            {
                bool goLeft = (pass == 0) == leftFirst;
                int far = goLeft ? left : right;
                for (int h = 1; h <= far; h++) islands.Add(goLeft ? _island - h : _island + h);
            }

            var order = new List<PetJob>();
            int prevIsland = _island;
            var at = _cell;
            foreach (int isl in islands)
            {
                if (isl != prevIsland) at = isl > prevIsland ? PetPaths.LeftLanding : PetPaths.RightLanding;
                prevIsland = isl;
                for (int pass = 0; pass < 2; pass++)
                {
                    var here = jobs.FindAll(j => j.island == isl && j.water == (pass == 0));
                    while (here.Count > 0)
                    {
                        int best = 0; float bestD = float.MaxValue;
                        for (int i = 0; i < here.Count; i++)
                        {
                            float d = PetPaths.WorldLength(IslandSys.SlotCell(isl, here[i].plot) - at);
                            if (d < bestD) { bestD = d; best = i; }
                        }
                        order.Add(here[best]);
                        at = IslandSys.SlotCell(isl, here[best].plot);
                        here.RemoveAt(best);
                    }
                }
            }
            return order;
        }

        // ============================================================
        // between patrols
        // ============================================================
        IEnumerator Wander()
        {
            _wandering = true;
            _stopWander = false;
            var s = GS.Local;
            float r = Random.value;
            if (r < 0.22f)
            {
                _actor.SetPose(PetActor.Pose.Sleep);
                float until = Time.unscaledTime + Random.Range(4f, 7f);
                while (Time.unscaledTime < until && !_stopWander) yield return null;
            }
            else
            {
                int to = _island;
                // now and then, a walk over to the next island
                if (r < 0.30f)
                {
                    bool l = Open(s, _island - 1), rt = Open(s, _island + 1);
                    if (l || rt) to = l && rt ? (Random.value < 0.5f ? _island - 1 : _island + 1) : l ? _island - 1 : _island + 1;
                }
                var near = to == _island ? _cell : to > _island ? PetPaths.LeftLanding : PetPaths.RightLanding;
                var spot = PetPaths.For(to).YardSpot(() => Random.value, near, to == _island ? 110f : 160f,
                                                     to == _island ? 520f : 640f);
                var legs = PetPaths.Route(_island, _cell, to, spot, i => Open(s, i));
                if (legs != null) yield return Follow(legs, true);
            }
            _actor.SetPose(PetActor.Pose.Idle);
            _wanderAt = Time.unscaledTime + Random.Range(5f, 9f);
            _wandering = false;
            _stopWander = false;
            _run = null;
        }

        // ============================================================
        // the screenshot passes
        // ============================================================
        void StopAll()
        {
            if (_run != null) StopCoroutine(_run);
            _run = null;
            _patrolling = _wandering = _stopWander = false;
            _audit = true;
            _nextPatrol = long.MaxValue;
            if (_actor == null) return;
            _actor.Lift = 0f; _actor.Sway = 0f; _actor.StepBounce = 1f;
            _actor.Root.localScale = Vector3.one;
        }

        /// <summary>For the screenshot pass: put the active pet at its work spot beside a plot of the current island.</summary>
        public void StageForAudit(PetActor.Pose pose, int plot, Sprite bubble = null)
        {
            if (_actor == null) return;
            StopAll();
            _island = _app.Farm.CurrentIsland;
            _bridge = -1;
            var spot = PetPaths.For(_island).WorkSpot(plot, IslandSys.SlotCell(_island, plot) + new Vector2(0.5f, 0.5f));
            _cell = spot ?? IslandSys.SlotCell(_island, plot);
            FaceCells(_cell, IslandSys.SlotCell(_island, plot));
            Apply();
            _actor.SetPose(pose);
            if (bubble != null) _actor.ShowBubble(bubble, Color.white); else _actor.HideBubble();
        }

        /// <summary>For the screenshot pass: stand the pet on a point of an island.</summary>
        public void StageAt(int island, Vector2 cell, PetActor.Pose pose, float faceDx)
        {
            if (_actor == null) return;
            StopAll();
            _island = island; _bridge = -1; _cell = cell;
            _actor.Face(faceDx);
            Apply();
            _actor.SetPose(pose);
            _actor.HideBubble();
        }

        /// <summary>For the screenshot pass: stand the pet part-way along a bridge, walking.</summary>
        public void StageOnBridge(int bridge, float t01, bool forward)
        {
            if (_actor == null) return;
            StopAll();
            var path = PetPaths.Bridge(bridge);
            _island = forward ? bridge - 1 : bridge;
            _bridge = bridge;
            _bridgeS = Mathf.Clamp01(t01) * path.Length;
            _actor.Face(forward ? 10f : -10f);
            _actor.StepBounce = 0.55f;
            Apply();
            _actor.SetPose(PetActor.Pose.Walk);
            _actor.HideBubble();
        }

        /// <summary>For the screenshot pass: walk from wherever the pet is to a point, for real. Returns false
        /// when there is no way there. <see cref="Walking"/> is true until it arrives.</summary>
        public bool StageTrip(int island, Vector2 cell)
        {
            if (_actor == null) return false;
            StopAll();
            var s = GS.Local;
            var legs = PetPaths.Route(_island, _cell, island, cell, i => Open(s, i));
            if (legs == null) return false;
            Walking = true;
            _run = StartCoroutine(TripRoutine(legs));
            return true;
        }

        public bool Walking { get; private set; }

        IEnumerator TripRoutine(List<PetLeg> legs)
        {
            yield return Follow(legs, false);
            Walking = false;
            _run = null;
        }

        public void EndAudit()
        {
            StopAll();
            Walking = false;
            _audit = false;
            _nextPatrol = GS.Now + PetSys.PatrolMs;
            _wanderAt = Time.unscaledTime + 3f;
            if (_actor != null) { _actor.SetPose(PetActor.Pose.Idle); _actor.HideBubble(); }
        }
    }
}
