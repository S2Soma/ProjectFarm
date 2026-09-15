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
        public RectTransform Root { get; private set; }
        public int Island = -1;
        public string PetId { get; private set; }

        RectTransform _body;
        Image _sprite, _shadow;
        RectTransform _bubble;
        Image _bubbleIcon;
        Text _zzz;
        Sprite _idle, _sleep, _happy;
        readonly List<Sprite> _walk = new List<Sprite>();

        public enum Pose { Idle, Walk, Sleep, Happy }
        Pose _pose;
        float _poseT, _facing = 1f;

        public const float Height = 118f;

        public static PetActor Build(RectTransform layer)
        {
            var a = new PetActor();
            a.Root = UIKit.Node("pet", layer);
            a.Root.Anchor(UIKit.Center, Vector2.zero, new Vector2(Height, Height));

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
            SetPose(Pose.Idle);
        }

        public void SetPose(Pose p)
        {
            _pose = p; _poseT = 0f;
            _zzz.gameObject.SetActive(p == Pose.Sleep);
            _sprite.sprite = p == Pose.Sleep ? _sleep : p == Pose.Happy ? _happy : p == Pose.Walk && _walk.Count > 0 ? _walk[0] : _idle;
        }

        public void Face(float dx) { if (Mathf.Abs(dx) > 1f) _facing = dx < 0f ? -1f : 1f; }

        public void ShowBubble(Sprite icon, Color tint)
        {
            _bubbleIcon.sprite = icon;
            _bubbleIcon.color = tint;
            _bubble.gameObject.SetActive(icon != null);
            if (icon != null) Tween.PopIn(_bubble, 0.25f, 0.4f);
        }

        public void HideBubble() { _bubble.gameObject.SetActive(false); }

        /// <summary>Per frame: walk cycle, the hop, breathing, the floating z.</summary>
        public void Tick(float dt, float lod)
        {
            _poseT += dt;
            float hop = 0f, squash = 1f;
            switch (_pose)
            {
                case Pose.Walk:
                    if (_walk.Count > 0) _sprite.sprite = _walk[(int)(_poseT * 10f) % _walk.Count];
                    hop = Mathf.Abs(Mathf.Sin(_poseT * 9f)) * 9f;
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
            }
            _body.anchoredPosition = new Vector2(0, hop);
            _body.localScale = new Vector3(_facing * (2f - squash), squash, 1f);
            _shadow.color = new Color(0.05f, 0.12f, 0.06f, 0.45f * (1f - hop / 30f));
        }
    }

    /// <summary>The pet's day: wander the island being looked at, and every three minutes walk the
    /// farm doing the watering and harvesting that is waiting (<see cref="PetSys.FindJobs"/>), then
    /// maybe help itself to a snack from the warehouse.
    ///
    /// Moving between islands is a poof, not a walk over the bridges: the islands are 1670 apart
    /// and a walk would take the whole patrol. Work goes through <see cref="GameApp.PetWork"/>,
    /// which calls the same view methods a tap does, so every harvest flies to the warehouse and
    /// counts for missions exactly as the player's own.</summary>
    public class PetDirector : MonoBehaviour
    {
        GameApp _app;
        PetActor _actor;
        long _nextPatrol;
        bool _busy;
        Coroutine _run;
        float _wanderAt;

        /// <summary>First patrol shortly after the game opens, so a returning player sees the pet
        /// get to work instead of waiting three minutes for proof it does anything.</summary>
        const long FirstPatrolMs = 20_000L;
        const float Speed = 300f;

        public static PetDirector Attach(GameApp app)
        {
            var d = app.gameObject.AddComponent<PetDirector>();
            d._app = app;
            d._nextPatrol = GS.Now + FirstPatrolMs;
            return d;
        }

        /// <summary>Seconds until the next patrol, for the pet panel; 0 while one is running.</summary>
        public int SecondsToPatrol => _busy ? 0 : Mathf.Max(0, (int)((_nextPatrol - GS.Now) / 1000L));
        public bool Busy => _busy;

        /// <summary>Start a patrol now (the "Đi tuần ngay" button and the screenshot pass).</summary>
        public void PatrolNow() { if (!_busy) _nextPatrol = GS.Now; }

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
            if (!_actor.Root.gameObject.activeSelf) _actor.Root.gameObject.SetActive(true);
            if (_actor.PetId != pet.id)
            {
                _actor.SetPet(pet.id);
                if (_actor.Island < 0) Place(farm.CurrentIsland);
            }
            _actor.Root.SetAsLastSibling();
            _actor.Tick(Time.unscaledDeltaTime, 1f);

            if (_busy) return;
            if (GS.Now >= _nextPatrol && !_app.Tutorial.Blocking)
            {
                _nextPatrol = GS.Now + PetSys.PatrolMs;
                _run = StartCoroutine(Patrol(pet));
                return;
            }
            if (Time.unscaledTime >= _wanderAt) { _wanderAt = Time.unscaledTime + Random.Range(5f, 9f); _run = StartCoroutine(Wander()); }
        }

        /// <summary>Stand on a free patch of the island's front yard.</summary>
        void Place(int island)
        {
            _actor.Island = island;
            _actor.Root.anchoredPosition = YardSpot(island);
        }

        Vector2 YardSpot(int island)
        {
            // between the front plots and the fence: the plot grid's front corner, pushed forward
            var farm = _app.Farm;
            var a = farm.PetPosOfPlot(island, 15);
            var b = farm.PetPosOfPlot(island, 12);
            var mid = (a + b) * 0.5f;
            return mid + new Vector2(Random.Range(-160f, 160f), Random.Range(-150f, -90f));
        }

        IEnumerator Wander()
        {
            _busy = true;
            int island = _app.Farm.CurrentIsland;
            if (_actor.Island != island) yield return Poof(island, YardSpot(island));
            else if (Random.value < 0.25f)
            {
                _actor.SetPose(PetActor.Pose.Sleep);
                yield return new WaitForSecondsRealtime(Random.Range(4f, 7f));
            }
            else yield return WalkTo(YardSpot(island));
            _actor.SetPose(PetActor.Pose.Idle);
            _busy = false;
        }

        IEnumerator Patrol(PetDef pet)
        {
            _busy = true;
            var s = GS.Local;
            int max = PetSys.JobsPerPatrol(pet, PetSys.LevelOf(s, pet.id));
            var jobs = PetSys.FindJobs(s, max, _app.Farm.CurrentIsland);
            int done = 0;
            foreach (var job in jobs)
            {
                // plots change while the pet walks: the player may have got there first
                var target = _app.Farm.PetPosOfPlot(job.island, job.plot) + new Vector2(-70f, -34f);
                if (_actor.Island != job.island) yield return Poof(job.island, target);
                else yield return WalkTo(target);
                _actor.Face(1f);
                if (_app.PetWork(job))
                {
                    done++;
                    _actor.SetPose(PetActor.Pose.Happy);
                    yield return new WaitForSecondsRealtime(0.55f);
                }
            }

            if (done > 0) s.stats.petJobs += done;
            // a snack, sometimes — and never on an empty warehouse
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
            _busy = false;
        }

        IEnumerator WalkTo(Vector2 to)
        {
            var from = _actor.Root.anchoredPosition;
            float dist = Vector2.Distance(from, to);
            if (dist < 4f) yield break;
            _actor.Face(to.x - from.x);
            _actor.SetPose(PetActor.Pose.Walk);
            float t = 0f, dur = dist / Speed;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                _actor.Root.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / dur));
                yield return null;
            }
            _actor.Root.anchoredPosition = to;
            _actor.SetPose(PetActor.Pose.Idle);
        }

        IEnumerator Poof(int island, Vector2 at)
        {
            var root = _actor.Root;
            for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
            {
                root.localScale = Vector3.one * Mathf.Lerp(1f, 0f, t / 0.22f);
                yield return null;
            }
            _actor.Island = island;
            root.anchoredPosition = at;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.3f;
                root.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.18f) * k;
                yield return null;
            }
            root.localScale = Vector3.one;
        }

        /// <summary>For the screenshot pass: put the active pet on a plot of the current island.</summary>
        public void StageForAudit(PetActor.Pose pose, int plot, Sprite bubble = null)
        {
            if (_actor == null) return;
            if (_run != null) StopCoroutine(_run);
            _busy = true;
            _nextPatrol = long.MaxValue;
            int island = _app.Farm.CurrentIsland;
            _actor.Island = island;
            _actor.Root.anchoredPosition = _app.Farm.PetPosOfPlot(island, plot) + new Vector2(-70f, -34f);
            _actor.Root.localScale = Vector3.one;
            _actor.SetPose(pose);
            if (bubble != null) _actor.ShowBubble(bubble, Color.white); else _actor.HideBubble();
        }

        public void EndAudit() { _busy = false; _nextPatrol = GS.Now + PetSys.PatrolMs; }
    }
}
