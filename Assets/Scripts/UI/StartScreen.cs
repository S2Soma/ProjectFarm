using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>The first screen: sign in (or play as a guest), then settle which save to play
    /// BEFORE the farm is built. A save that comes from the account is written to the file the
    /// game then opens, so nothing already on screen has to be rebuilt.
    ///
    /// Modes: Home (no account) → Login / SignUp forms → Busy → Choose (both sides hold progress)
    /// → into the farm; or Ready (an account remembered on this device) → into the farm. The
    /// network being down never blocks play: the farm opens on this device's save and
    /// <see cref="CloudSync"/> settles it once it can.</summary>
    public class StartScreen
    {
        /// <summary>Only when a Supabase project is configured; otherwise the game opens directly.</summary>
        public static bool Wanted => Supa.Configured;

        enum Mode { Home, Login, SignUp, Busy, Ready, Choose }

        readonly GameApp _app;
        readonly RectTransform _layer;
        RectTransform _safe, _card, _body, _logo, _scene;
        float _titleW, _logoFitFor = -1f;
        /// <summary>The scenery in four depth planes (sky · far cloud sea · island · near cloud sea),
        /// so the cinematic into the farm can push a camera through them with parallax.</summary>
        RectTransform _depthSky, _depthFar, _depthIsland, _depthNear;
        Image _sun, _halo, _rays;
        Text _version;
        CanvasGroup _group, _bodyGroup;
        StartScreenFx _fx;
        CloudMaterialOwner _cloudMats;
        Mode _mode;
        bool _disposed;
        bool _offlineOffered;
        string _email = "";
        string _message = "";
        bool _messageGood;
        SyncState _readyState;
        string _readyNote = "";
        bool _confirmSwitch;

        const float CardW = 460f, CardH = 560f, Pad = 28f;
        float BodyW => CardW - Pad * 2f;

        public static StartScreen Show(RectTransform layer, GameApp app)
        {
            var s = new StartScreen(app, layer);
            s.Build();
            s.Begin();
            return s;
        }

        /// <summary>Built but not started: the screenshot pass drives it with <see cref="PreviewForAudit"/>.</summary>
        public static StartScreen ShowIdle(RectTransform layer, GameApp app)
        {
            var s = new StartScreen(app, layer);
            s.Build();
            return s;
        }

        StartScreen(GameApp app, RectTransform layer) { _app = app; _layer = layer; }

        // ================================================================
        // lifecycle
        // ================================================================
        void Begin()
        {
            if (Supa.SignedIn)
            {
                SetBusy("Đang kết nối tài khoản…");
                Reconcile(false);
            }
            else Go(Mode.Home);
        }

        /// <summary>Removed at once (Editor tools skipping the screen).</summary>
        public void Dispose()
        {
            _disposed = true;
            if (_layer != null) UnityEngine.Object.Destroy(_layer.gameObject);
        }

        /// <summary>What <see cref="EnterCinematic"/> moves while the screen is left behind.</summary>
        public sealed class LeaveParts
        {
            public RectTransform layer, sky, far, island, islandArt, near, logo, card;
            public Image sun, halo, rays;
            public Text version;
            public StartScreenFx fx;
            public StartScreen screen;
        }

        /// <summary>The player committed: nothing on this screen reacts any more (a sign-in reply
        /// that lands now is ignored), and the cinematic takes the scene over. It calls
        /// <see cref="Dispose"/> once the veil hides it.</summary>
        public LeaveParts BeginLeave()
        {
            _disposed = true;
            if (_group != null) _group.blocksRaycasts = false;
            return new LeaveParts
            {
                layer = _layer, sky = _depthSky, far = _depthFar, island = _depthIsland, islandArt = _scene, near = _depthNear,
                logo = _logo, card = _card, sun = _sun, halo = _halo, rays = _rays, version = _version, fx = _fx, screen = this,
            };
        }

        void Enter()
        {
            if (_disposed) return;
            Sfx.Play(SfxId.Claim);
            _app.EnterFromStart();
        }

        /// <summary>The screenshot pass: the same way in as every button, without signing in.</summary>
        public void EnterForAudit() { Enter(); }

        // ================================================================
        // the scene: sky, sun, drifting clouds, a sea of cloud, the home island with ripe crops
        // ================================================================
        void Build()
        {
            _group = _layer.gameObject.AddComponent<CanvasGroup>();
            _fx = _layer.gameObject.AddComponent<StartScreenFx>();
            _cloudMats = _layer.gameObject.AddComponent<CloudMaterialOwner>();

            _depthSky = Depth("depth_sky");
            _depthFar = Depth("depth_far");
            _depthIsland = Depth("depth_island");
            _depthNear = Depth("depth_near");

            var sky = UIKit.Img(_depthSky, null, Color.white, "sky");
            sky.rectTransform.Stretch();
            sky.raycastTarget = true;                         // nothing under the start screen takes a tap
            sky.gameObject.AddComponent<VGradient>().Set(Theme.Hex("#3E9FE0"), Theme.Hex("#DDF3FD"), 0.18f, 1f);

            var rays = UIKit.Img(_depthSky, Art.Load("Art/sky/sun_rays"), new Color(1f, 0.95f, 0.78f, 0.5f), "rays");
            rays.rectTransform.Anchor(new Vector2(0.1f, 0.93f), Vector2.zero, new Vector2(720, 720));
            rays.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _fx.spin = rays.rectTransform;
            // a soft halo under a small bright disc: the flat cream plate read as a moon
            var halo = UIKit.Img(_depthSky, Art.Load("Art/sky/sun_disc"), new Color(1f, 0.86f, 0.5f, 0.32f), "halo");
            halo.material = MutationTint.AdditiveMaterial;          // light added to the sky, not grey laid over it
            halo.rectTransform.Anchor(new Vector2(0.1f, 0.93f), Vector2.zero, new Vector2(190, 190));
            halo.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var sun = UIKit.Img(_depthSky, Art.Load("Art/sky/sun_disc"), new Color(1f, 1f, 0.94f, 1f), "sun");
            sun.rectTransform.Anchor(new Vector2(0.1f, 0.93f), Vector2.zero, new Vector2(112, 112));
            sun.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _sun = sun; _halo = halo; _rays = rays;

            var cirrus = UIKit.Img(_depthSky, Art.Load("Art/sky/cirrus"), new Color(1f, 1f, 1f, 0.55f), "cirrus");
            cirrus.rectTransform.anchorMin = new Vector2(0f, 0.74f);
            cirrus.rectTransform.anchorMax = new Vector2(1f, 0.74f);
            cirrus.rectTransform.sizeDelta = new Vector2(0f, 120f);
            DayCloud(cirrus, true, 0.0015f, CloudMaterials.BandNoise(5));

            // (sprite, anchor x, anchor y, width, alpha, drift px/s)
            var clouds = new (string art, float x, float y, float w, float a, float speed)[]
            {
                ("cumulus_c", 0.52f, 0.86f, 360f, 0.95f, 7f),
                ("cumulus_b", 0.92f, 0.70f, 250f, 0.90f, 11f),
                ("cumulus_a", 0.05f, 0.62f, 330f, 0.85f, 9f),
                ("cumulus_c", 0.70f, 0.58f, 220f, 0.75f, 14f),
            };
            foreach (var c in clouds)
            {
                var sp = Art.Load("Art/sky/" + c.art);
                if (sp == null) continue;
                var im = UIKit.Img(_depthSky, sp, new Color(1f, 1f, 1f, c.a), "cloud");
                float h = c.w * sp.rect.height / sp.rect.width;
                im.rectTransform.Anchor(new Vector2(c.x, c.y), Vector2.zero, new Vector2(c.w, h));
                im.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                DayCloud(im, false, 0f, 1f / 110f);
                _fx.clouds.Add((im.rectTransform, c.speed));
            }

            Band(_depthFar, "sea_far", 176f, 150f, new Color(0.90f, 0.95f, 1f, 0.95f));
            Band(_depthFar, "sea_mid", 70f, 230f, new Color(0.96f, 0.98f, 1f, 0.97f));

            BuildIsland();

            Band(_depthNear, "sea_near", -150f, 320f, Color.white);

            // controls live inside the safe area; the scenery above runs to the screen edges
            _safe = UIKit.Node("safe", _layer);
            _safe.Stretch();
            _safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildLogo();

            _card = UIKit.Node("card", _safe);
            PlaceCard(false);
            SurfaceLook.Add(_card, Looks.Paper, 30f).Fill.raycastTarget = true;
            _body = UIKit.Node("body", _card);
            _body.Stretch(Pad, Pad - 6f, Pad, Pad);
            _bodyGroup = _body.gameObject.AddComponent<CanvasGroup>();

            var ver = UIKit.LabelOutlined(_safe, "v" + Application.version, 15, Color.white, TextAnchor.LowerLeft, Theme.Hex("#2A6F9E"));
            ver.rectTransform.Anchor(UIKit.BottomLeft, new Vector2(18, 10), new Vector2(200, 24));
            _version = ver;

            Tween.PopIn(_card, 0.34f, 0.9f);
        }

        /// <summary>A full-screen plane: exactly the layer's rect, so what is laid out in it is
        /// where it was, and scaling it about its centre is a camera moving through that depth.</summary>
        RectTransform Depth(string name)
        {
            var rt = UIKit.Node(name, _layer);
            rt.Stretch();
            return rt;
        }

        void Band(RectTransform plane, string art, float bottom, float height, Color tint)
        {
            var im = UIKit.Img(plane, Art.Load("Art/sky/" + art), tint, art);
            im.rectTransform.anchorMin = new Vector2(0f, 0f);
            im.rectTransform.anchorMax = new Vector2(1f, 0f);
            im.rectTransform.pivot = new Vector2(0.5f, 0f);
            im.rectTransform.anchoredPosition = new Vector2(0f, bottom);
            im.rectTransform.sizeDelta = new Vector2(80f, height);
            // the near sea rolls by faster than the far one
            DayCloud(im, true, height > 300f ? 0.0045f : height > 200f ? 0.0025f : 0.0012f, CloudMaterials.BandNoise(height > 300f ? 7 : 10));
        }

        /// <summary>A cloud lit like the farm's sky at noon (the scene is always a clear day): the art's
        /// grey is light, mapped from a cool shade to white, with its soft edge breathing
        /// (<see cref="CloudMaterials"/>). A band scrolls by itself, from the shader's own clock.</summary>
        void DayCloud(Image im, bool band, float drift, float noisePerTexel)
        {
            var mat = CloudMaterials.Cloud(_cloudMats, im.sprite, band,
                new Vector4(band ? 0.5f : 0.45f, band ? 4f : 3f, noisePerTexel, 0.03f), new Vector4(0.08f, 0.96f, 0.05f, 0f));
            if (mat == null) return;
            CloudMaterials.Paint(mat, Color.white, Theme.Hex(band ? "#B4C8E6" : "#C3D4EE"), Theme.Hex("#FFF6DC").Alpha(0.3f));
            mat.SetFloat(CloudMaterials.Drift, drift);
            im.material = mat;
        }

        void BuildIsland()
        {
            _scene = UIKit.Node("island", _depthIsland);
            _scene.Anchor(new Vector2(0.29f, 0.38f), Vector2.zero, new Vector2(640, 409));
            _scene.pivot = new Vector2(0.5f, 0.5f);
            _fx.bob = _scene;

            var island = UIKit.Img(_scene, Art.Load("Art/islands/island_0"), Color.white, "art");
            island.rectTransform.Stretch();

            // (art, x, ground y, width) from the island's centre; back rows first
            var plants = new (string art, float x, float y, float w)[]
            {
                ("crops_gen/apple_3",      150f, 118f, 150f),
                ("crops_gen/corn_3",      -120f, 104f, 118f),
                ("crops_gen/pumpkin_3",     20f,  86f, 132f),
                ("crops_gen/strawberry_3", 196f,  38f, 104f),
                ("crops_gen/tomato_3",     -40f,  24f,  74f),
                ("crops_gen/wheat_3",     -196f,  40f,  96f),
            };
            foreach (var p in plants)
            {
                var sp = Art.Load("Art/" + p.art);
                if (sp == null) continue;
                var im = UIKit.Img(_scene, sp, Color.white, "plant");
                float h = p.w * sp.rect.height / sp.rect.width;
                im.rectTransform.anchorMin = im.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                im.rectTransform.pivot = new Vector2(0.5f, 0.06f);
                im.rectTransform.anchoredPosition = new Vector2(p.x, p.y);
                im.rectTransform.sizeDelta = new Vector2(p.w, h);
                _fx.sway.Add(im.rectTransform);
            }

            // two of the pets, hopping now and then
            var pets = new (string id, float x, float y, float w, bool flip)[]
            {
                ("shushi", 80f, -14f, 118f, true),
                ("yummy", -126f, -34f, 104f, false),
            };
            foreach (var p in pets)
            {
                var sp = Art.Load("Art/pets/" + p.id + "/idle");
                if (sp == null) continue;
                var im = UIKit.Img(_scene, sp, Color.white, "pet_" + p.id);
                im.preserveAspect = true;
                im.rectTransform.anchorMin = im.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                im.rectTransform.pivot = new Vector2(0.5f, 0.08f);
                im.rectTransform.anchoredPosition = new Vector2(p.x, p.y);
                im.rectTransform.sizeDelta = new Vector2(p.w, p.w);
                if (p.flip) im.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                _fx.hops.Add(im.rectTransform);
            }
        }

        void BuildLogo()
        {
            _logo = UIKit.Node("logo", _safe);
            _logo.Anchor(new Vector2(0.29f, 1f), new Vector2(0, -34), new Vector2(640, 124));
            // centred over the island. With pivot = anchor (0.29) the title sat 134 px right of the
            // island, and on a 16:10 screen or a browser window its last letters went under the card.
            _logo.pivot = new Vector2(0.5f, 1f);

            var title = UIKit.Label(_logo, Application.productName, 104, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.Anchor(UIKit.Top, Vector2.zero, new Vector2(640, 118));
            title.gameObject.AddComponent<VGradient>().Set(Theme.Hex("#FFF7C2"), Theme.Hex("#FFB524"), 0.15f, 0.85f);
            var o1 = title.gameObject.AddComponent<Outline>();
            o1.effectColor = Theme.Hex("#8A4A0C"); o1.effectDistance = new Vector2(3f, -3f);
            var o2 = title.gameObject.AddComponent<Outline>();
            o2.effectColor = Theme.Hex("#8A4A0C"); o2.effectDistance = new Vector2(-2.5f, 2.5f);
            var sh = title.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.1f, 0.2f, 0.35f, 0.35f); sh.effectDistance = new Vector2(0f, -8f);
            // (the "Nông trại trên mây" ribbon under the title was removed on the owner's request, 15/9)
            _titleW = title.preferredWidth + 12f;           // + the outlines
            _fx.onResize = FitLogo;
        }

        /// <summary>Keeps the title clear of the card at any aspect: it stays centred over the island
        /// when there is room, slides left when there is not, and shrinks only as a last resort. The
        /// canvas is "Expand", so width is never below 1280 but a 16:10 or 4:3 screen has no more
        /// than that, and the title is ~560 px wide.</summary>
        void FitLogo(float width)
        {
            if (_disposed || _logo == null || _card == null || Mathf.Approximately(width, _logoFitFor)) return;
            if (!_logo.gameObject.activeSelf) return;
            _logoFitFor = width;
            if (_safe != null && _safe.rect.width > 0f) width = _safe.rect.width;   // the logo and card live in the safe area
            const float margin = 24f;
            float right = width - 54f - CardW - margin;      // the card's left edge, less a margin
            float room = right - margin;
            float scale = Mathf.Min(1f, room / Mathf.Max(1f, _titleW));
            float half = _titleW * scale * 0.5f;
            float cx = Mathf.Clamp(0.29f * width, margin + half, right - half);
            _logo.anchorMin = _logo.anchorMax = new Vector2(0f, 1f);
            _logo.anchoredPosition = new Vector2(cx, -34f);
            _logo.localScale = new Vector3(scale, scale, 1f);
        }

        void PlaceCard(bool wide)
        {
            if (wide) _card.Anchor(UIKit.Center, new Vector2(0, -8), new Vector2(880, 540));
            else _card.Anchor(UIKit.Right, new Vector2(-54, -6), new Vector2(CardW, CardH));
            if (_logo != null) _logo.gameObject.SetActive(!wide);
            _logoFitFor = -1f;
            if (_layer != null) FitLogo(_layer.rect.width);
        }

        // ================================================================
        // modes
        // ================================================================
        void Go(Mode m)
        {
            if (_disposed) return;
            bool wide = m == Mode.Choose, wasWide = _mode == Mode.Choose;
            _mode = m;
            if (m != Mode.Ready) _confirmSwitch = false;
            for (int i = _body.childCount - 1; i >= 0; i--)
            {
                var child = _body.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(child);
            }
            if (wide != wasWide) { PlaceCard(wide); Tween.PopIn(_card, 0.22f, 0.94f); }
            switch (m)
            {
                case Mode.Home: BuildHome(); break;
                case Mode.Login: BuildForm(false); break;
                case Mode.SignUp: BuildForm(true); break;
                case Mode.Ready: BuildReady(); break;
                case Mode.Busy: BuildBusy(); break;
                case Mode.Choose: BuildChoose(); break;
            }
            if (!wide) FitCard();
            _bodyGroup.alpha = 0f;
            Tween.Fade(_bodyGroup, 1f, 0.16f);
        }

        /// <summary>The card hugs what the mode put in it: Home is shorter than the forms, and a
        /// fixed height left a third of the card empty.</summary>
        void FitCard()
        {
            float lowest = 0f;
            foreach (RectTransform child in _body)
            {
                // everything in the narrow card is laid out from the top (pivot on the top edge)
                if (child.anchorMin.y < 0.99f) { lowest = Mathf.Max(lowest, 300f); continue; }
                lowest = Mathf.Max(lowest, -child.anchoredPosition.y + child.sizeDelta.y * child.pivot.y);
            }
            float h = Mathf.Clamp(lowest + (Pad - 6f) + Pad, 320f, CardH);
            _card.sizeDelta = new Vector2(CardW, h);
        }

        string _busyText = "";
        void SetBusy(string text) { _busyText = text; Go(Mode.Busy); }

        void Say(string text, bool good = false) { _message = text ?? ""; _messageGood = good; }

        Text Title(string text, float y)
        {
            var t = UIKit.Label(_body, text, 30, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Anchor(UIKit.Top, new Vector2(0, y), new Vector2(BodyW, 46));
            return t;
        }

        Text Para(string text, float y, float h, int size = 17, Color? color = null, float width = -1f)
        {
            var t = UIKit.Label(_body, text, size, color ?? Theme.InkSoft, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.rectTransform.Anchor(UIKit.Top, new Vector2(0, y), new Vector2(width > 0 ? width : BodyW, h));
            return t;
        }

        Button Button(string text, Color tone, float y, float h, Action act, float w = -1f, float x = 0f, int size = 24)
        {
            var b = UIKit.Btn(_body, text, tone, tone, size, 18, act);
            b.GetComponent<RectTransform>().Anchor(UIKit.Top, new Vector2(x, y), new Vector2(w > 0 ? w : BodyW, h));
            return b;
        }

        void MessageLine(float y, float h = 50f)
        {
            if (string.IsNullOrEmpty(_message)) return;
            var t = Para(_message, y, h, 17, _messageGood ? Theme.GreenDeep : Theme.Hex("#B03A31"));
            if (!_messageGood) Tween.Shake(t.rectTransform, 5f, 0.25f);
        }

        // ---------------- home ----------------
        void BuildHome()
        {
            Title("Chào nông dân!", -12);
            Para("Lưu nông trại lên mây, chơi tiếp trên máy khác.", -58, 40);

            Button("Chơi ngay", Theme.Green, -126, 76, PlayGuest, size: 26);
            Para("Không cần email — liên kết sau trong Menu ▸ Tài khoản", -206, 26, 15);
            Button("Đăng nhập", Theme.Blue, -244, 62, () => { Say(""); Go(Mode.Login); });
            Button("Tạo tài khoản bằng email", Theme.Amber, -316, 62, () => { Say(""); Go(Mode.SignUp); }, size: 22);

            MessageLine(-390, 56);
            if (_offlineOffered)
                Button("Chơi không cần mạng", Theme.Cream3, -450, 52, Enter, 300, 0, 20);
        }

        void PlayGuest()
        {
            Say("");
            SetBusy("Đang tạo tài khoản khách…");
            Supa.Run(Supa.SignInGuest(r =>
            {
                if (_disposed) return;
                if (r.Ok) { SetBusy("Đang chuẩn bị nông trại…"); Reconcile(true); return; }
                Say(Supa.Friendly(r));
                _offlineOffered = true;
                Go(Mode.Home);
            }));
        }

        // ---------------- login / sign up ----------------
        InputField _emailField, _passField;

        void BuildForm(bool signUp)
        {
            Title(signUp ? "Tạo tài khoản" : "Đăng nhập", -12);

            var el = UIKit.Label(_body, "Email", 16, Theme.InkSoft, TextAnchor.MiddleLeft, FontStyle.Bold);
            el.rectTransform.Anchor(UIKit.Top, new Vector2(0, -64), new Vector2(BodyW - 8, 26));
            _emailField = UIKit.TextField(_body, "ten@email.com", InputField.ContentType.EmailAddress, 22);
            ((RectTransform)_emailField.transform).Anchor(UIKit.Top, new Vector2(0, -92), new Vector2(BodyW, 60));
            _emailField.text = _email;

            var pl = UIKit.Label(_body, signUp ? "Mật khẩu (ít nhất 6 ký tự)" : "Mật khẩu", 16, Theme.InkSoft, TextAnchor.MiddleLeft, FontStyle.Bold);
            pl.rectTransform.Anchor(UIKit.Top, new Vector2(0, -160), new Vector2(BodyW - 8, 26));
            _passField = UIKit.TextField(_body, "••••••", InputField.ContentType.Password, 22, () => Submit(signUp));
            ((RectTransform)_passField.transform).Anchor(UIKit.Top, new Vector2(0, -188), new Vector2(BodyW, 60));

            Button(signUp ? "Tạo tài khoản" : "Đăng nhập", signUp ? Theme.Amber : Theme.Blue, -272, 68, () => Submit(signUp), size: 26);
            MessageLine(-350, 60);

            // the back / switch row sits under the message when there is one, else close under the button
            float rowY = string.IsNullOrEmpty(_message) ? -358f : -424f;
            float half = (BodyW - 12f) / 2f;
            Button("Quay lại", Theme.Cream3, rowY, 54, () => { _email = _emailField.text; Say(""); Go(Mode.Home); }, half, -(half + 12f) / 2f, 20);
            Button(signUp ? "Đã có tài khoản" : "Tạo tài khoản", Theme.Cream3, rowY, 54,
                   () => { _email = _emailField.text; Say(""); Go(signUp ? Mode.Login : Mode.SignUp); }, half, (half + 12f) / 2f, 20);
        }

        void Submit(bool signUp)
        {
            if (_mode != Mode.Login && _mode != Mode.SignUp) return;
            string email = (_emailField.text ?? "").Trim();
            string pass = _passField.text ?? "";
            _email = email;
            if (!LooksLikeEmail(email)) { Say("Email chưa đúng — ví dụ: ten@email.com"); Go(_mode); return; }
            if (pass.Length < 6) { Say("Mật khẩu cần ít nhất 6 ký tự."); Go(_mode); return; }

            var back = _mode;
            SetBusy(signUp ? "Đang tạo tài khoản…" : "Đang đăng nhập…");
            var call = signUp ? Supa.SignUp(email, pass, Done) : Supa.SignIn(email, pass, Done);
            Supa.Run(call);

            void Done(SupaReply r)
            {
                if (_disposed) return;
                if (r.Ok && Supa.SignedIn) { SetBusy("Đang tải nông trại…"); Reconcile(true); return; }
                if (r.Ok && r.Code == "confirm_email")
                {
                    Say("Đã gửi thư xác nhận tới " + email + ". Mở thư, bấm xác nhận rồi đăng nhập ở đây.", true);
                    Go(Mode.Login);
                    return;
                }
                Say(Supa.Friendly(r));
                if (r.Network) _offlineOffered = true;
                Go(back);
            }
        }

        public static bool LooksLikeEmail(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Contains(" ")) return false;
            int at = s.IndexOf('@');
            return at > 0 && at == s.LastIndexOf('@') && s.IndexOf('.', at) > at + 1 && !s.EndsWith(".");
        }

        // ---------------- busy ----------------
        void BuildBusy()
        {
            var ring = UIKit.Node("spinner", _body);
            ring.Anchor(UIKit.Center, new Vector2(0, 40), new Vector2(96, 96));
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                var dot = UIKit.Round(ring, Theme.Green.Alpha(0.2f + 0.8f * i / 9f), 9, "dot");
                dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                dot.rectTransform.sizeDelta = new Vector2(16, 16);
                dot.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 38f;
            }
            _fx.spinner = ring;
            var t = UIKit.Label(_body, _busyText, 22, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Anchor(UIKit.Center, new Vector2(0, -52), new Vector2(BodyW, 36));
        }

        // ---------------- signed in ----------------
        void BuildReady()
        {
            var sess = Supa.Session;
            Title("Chào mừng trở lại!", -12);

            var who = UIKit.Node("who", _body);
            who.Anchor(UIKit.Top, new Vector2(0, -66), new Vector2(BodyW, 92));
            SurfaceLook.Add(who, Looks.Row, 20f);
            var disc = UIKit.Node("avatar", who);
            disc.Anchor(UIKit.Left, new Vector2(12, 0), new Vector2(70, 70));
            SurfaceLook.Add(disc, Looks.BtnCream, SurfaceLook.Pill);
            var face = UIKit.Img(disc, Art.Load("Art/pets/shushi/portrait"), Color.white, "pet");
            face.preserveAspect = true;
            face.rectTransform.Stretch(6, 6, 6, 8);
            string name = sess == null ? "Chưa đăng nhập" : sess.Label;
            var n = UIKit.Label(who, name, name.Length > 22 ? 18 : 22, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            n.rectTransform.Anchor(UIKit.Left, new Vector2(96, 14), new Vector2(BodyW - 110, 32));
            var kind = UIKit.Label(who, sess == null ? "" : sess.IsGuest ? "Tài khoản khách · liên kết email trong Menu" : "Tài khoản email",
                                   15, Theme.InkSoft, TextAnchor.MiddleLeft);
            kind.rectTransform.Anchor(UIKit.Left, new Vector2(96, -18), new Vector2(BodyW - 110, 24));

            // the farm that will open
            var local = SaveSummary.Of(SaveIO.ReadRaw());
            var farm = UIKit.Node("farm", _body);
            farm.Anchor(UIKit.Top, new Vector2(0, -170), new Vector2(BodyW, 60));
            SurfaceLook.Add(farm, Looks.Well, 18f);
            string lv = local.exists ? "Cấp " + local.level : "Nông trại mới";
            var lvl = UIKit.Label(farm, lv, 22, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            lvl.rectTransform.Anchor(UIKit.Left, new Vector2(20, 0), new Vector2(180, 34));
            if (local.exists)
            {
                var coin = UIKit.Img(farm, Art.Item("coin"), Color.white, "coin");
                coin.preserveAspect = true;
                coin.rectTransform.Anchor(UIKit.Right, new Vector2(-150, 0), new Vector2(32, 32));
                var c = UIKit.Label(farm, Fmt.Short(local.coin), 22, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                c.rectTransform.Anchor(UIKit.Right, new Vector2(-16, 0), new Vector2(128, 34));
            }

            // sync status
            Color tone; string note;
            switch (_readyState)
            {
                case SyncState.Synced: tone = Theme.Green; note = "Đã đồng bộ với tài khoản"; break;
                case SyncState.Offline: tone = Theme.Amber; note = "Chưa kết nối được — chơi trên máy, tự đồng bộ khi có mạng"; break;
                default: tone = Theme.Red; note = _readyNote; break;
            }
            var st = UIKit.Node("status", _body);
            st.Anchor(UIKit.Top, new Vector2(0, -242), new Vector2(BodyW, 50));
            var ic = UIKit.Img(st, Art.Load("Art/gen/cloud"), tone, "cloud");
            ic.preserveAspect = true;
            ic.rectTransform.Anchor(UIKit.Left, new Vector2(4, 0), new Vector2(34, 34));
            var nt = UIKit.Label(st, note, 16, _readyState == SyncState.Synced ? Theme.GreenDeep : Theme.InkSoft, TextAnchor.MiddleLeft);
            nt.horizontalOverflow = HorizontalWrapMode.Wrap;
            nt.rectTransform.Anchor(UIKit.Left, new Vector2(48, 0), new Vector2(BodyW - 52, 50));

            Button("Vào nông trại", Theme.Green, -318, 80, Enter, size: 26);
            string sw = _confirmSwitch
                ? "Chạm lần nữa — tài khoản khách sẽ mất"
                : "Đổi tài khoản";
            Button(sw, _confirmSwitch ? Theme.Red : Theme.Cream3, -420, 56, SwitchAccount, size: _confirmSwitch ? 18 : 20);
        }

        void SwitchAccount()
        {
            // a guest account has no email to sign back in with: make leaving it a deliberate act
            if (Supa.Session != null && Supa.Session.IsGuest && !_confirmSwitch) { _confirmSwitch = true; Go(Mode.Ready); return; }
            SetBusy("Đang đăng xuất…");
            Supa.Run(Supa.SignOut(() =>
            {
                CloudSync.Verified = false;
                Say("");
                Go(Mode.Home);
            }));
        }

        // ---------------- the save question ----------------
        CloudSync.Check _check;

        void BuildChoose()
        {
            SaveChoice.Build(_body, _check, useCloud =>
            {
                if (useCloud) CloudSync.ApplyBeforeLoad(_check, SyncPlan.UseCloud);
                else
                {
                    SaveIO.BackupRaw("tai-khoan-cu", _check.cloudJson);
                    CloudSync.ApplyBeforeLoad(_check, SyncPlan.UploadLocal);
                }
                Enter();
            }, true);
        }

        // ================================================================
        // sync
        // ================================================================
        void Reconcile(bool enterWhenDone)
        {
            Supa.Run(CloudSync.Reconcile(SaveIO.ReadRaw(), c =>
            {
                if (_disposed) return;
                if (!c.Ok)
                {
                    if (!Supa.SignedIn)
                    {
                        Say(Supa.Friendly(c.reply));
                        Go(Mode.Home);
                        return;
                    }
                    _readyState = c.reply.Network ? SyncState.Offline : SyncState.Error;
                    _readyNote = Supa.Friendly(c.reply);
                    Go(Mode.Ready);
                    return;
                }
                if (c.plan == SyncPlan.AskPlayer) { _check = c; Go(Mode.Choose); return; }
                CloudSync.ApplyBeforeLoad(c, c.plan);
                _readyState = SyncState.Synced;
                if (enterWhenDone) Enter();
                else Go(Mode.Ready);
            }));
        }

        // ================================================================
        // screenshot pass
        // ================================================================
        public void PreviewForAudit(string mode)
        {
            switch (mode)
            {
                case "home": _offlineOffered = false; Say(""); Go(Mode.Home); break;
                case "home_error": _offlineOffered = true; Say("Máy chủ chưa bật chơi khách (Anonymous sign-ins)."); Go(Mode.Home); break;
                case "login": _email = "nongdan@mitfarm.vn"; Say(""); Go(Mode.Login); break;
                case "signup": _email = ""; Say("Mật khẩu cần ít nhất 6 ký tự."); Go(Mode.SignUp); break;
                case "busy": SetBusy("Đang tải nông trại…"); break;
                case "ready": _readyState = SyncState.Synced; Go(Mode.Ready); break;
                case "ready_offline": _readyState = SyncState.Offline; Go(Mode.Ready); break;
                case "choose":
                    string raw = SaveIO.ReadRaw();
                    _check = new CloudSync.Check { local = SaveSummary.Of(raw) };
                    _check.cloud = _check.local;
                    _check.cloud.level = Mathf.Max(1, _check.local.level - 3);
                    _check.cloud.coin = _check.local.coin / 3;
                    _check.cloud.savedAt = _check.local.savedAt - 26L * 3600 * 1000;
                    Go(Mode.Choose);
                    break;
            }
        }
    }

    /// <summary>The two farms side by side — used by the start screen and, in game, by
    /// <see cref="SaveChoicePanel"/>.</summary>
    public static class SaveChoice
    {
        public static void Build(RectTransform body, CloudSync.Check c, Action<bool> pick, bool withTitle)
        {
            float w = body.rect.width > 10f ? body.rect.width : 820f;
            float top = 0f;
            if (withTitle)
            {
                var t = UIKit.Label(body, "Chọn nông trại để chơi", 30, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.rectTransform.Anchor(UIKit.Top, new Vector2(0, -12), new Vector2(w, 46));
                top = -58f;
            }
            var sub = UIKit.Label(body, "Máy này và tài khoản đều có tiến trình. Bản không chọn vẫn được cất một bản sao trên máy.",
                                  17, Theme.InkSoft, TextAnchor.MiddleCenter);
            sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            sub.rectTransform.Anchor(UIKit.Top, new Vector2(0, top), new Vector2(w, 50));

            bool localNewer = c.local.savedAt >= c.cloud.savedAt;
            float tileW = Mathf.Min(370f, (w - 30f) / 2f);
            Tile(body, new Vector2(-(tileW / 2f + 14f), top - 64f), tileW, "Trên máy này", "Art/gen/phone", c.local, localNewer, () => pick(false));
            Tile(body, new Vector2(tileW / 2f + 14f, top - 64f), tileW, "Trên tài khoản", "Art/gen/cloud", c.cloud, !localNewer, () => pick(true));
        }

        static void Tile(RectTransform body, Vector2 pos, float w, string heading, string icon, SaveSummary s, bool newer, Action act)
        {
            var tile = UIKit.Node("tile", body);
            tile.Anchor(UIKit.Top, pos, new Vector2(w, 330));
            SurfaceLook.Add(tile, Looks.Row, 22f);

            var ic = UIKit.Img(tile, Art.Load(icon), Theme.BlueDeep, "icon");
            ic.preserveAspect = true;
            ic.rectTransform.Anchor(UIKit.TopLeft, new Vector2(20, -18), new Vector2(34, 34));
            var h = UIKit.Label(tile, heading, 20, Theme.InkSoft, TextAnchor.MiddleLeft, FontStyle.Bold);
            h.rectTransform.Anchor(UIKit.TopLeft, new Vector2(62, -18), new Vector2(w - 170, 34));

            if (newer)
            {
                var badge = UIKit.Node("newer", tile);
                badge.Anchor(UIKit.TopRight, new Vector2(-16, -20), new Vector2(92, 30));
                SurfaceLook.Add(badge, Looks.BtnAmber, SurfaceLook.Pill);
                var bl = UIKit.LabelOutlined(badge, "Mới hơn", 15, Color.white, TextAnchor.MiddleCenter, Looks.BtnAmber.inkLine);
                bl.rectTransform.Stretch(4, 0, 4, 2);
            }

            var lv = UIKit.Label(tile, "Cấp " + s.level, 44, Theme.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            lv.rectTransform.Anchor(UIKit.Top, new Vector2(0, -66), new Vector2(w, 66));

            var row = UIKit.Node("coin", tile);
            row.Anchor(UIKit.Top, new Vector2(0, -136), new Vector2(w, 36));
            var coin = UIKit.Img(row, Art.Item("coin"), Color.white, "coin");
            coin.preserveAspect = true;
            string cs = Fmt.Short(s.coin) + " xu";
            var ct = UIKit.Label(row, cs, 24, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            float cw = ct.preferredWidth;
            coin.rectTransform.Anchor(UIKit.Center, new Vector2(-(cw + 38f) / 2f + 16f, 0), new Vector2(32, 32));
            ct.rectTransform.Anchor(UIKit.Center, new Vector2(19f, 0), new Vector2(cw + 4f, 36));

            var when = UIKit.Label(tile, s.savedAt > 0 ? "Lưu lúc " + WhenText(s.savedAt) : "", 16, Theme.InkSoft, TextAnchor.MiddleCenter);
            when.rectTransform.Anchor(UIKit.Top, new Vector2(0, -180), new Vector2(w, 26));

            var b = UIKit.Btn(tile, "Chơi bản này", newer ? Theme.Green : Theme.Blue, Theme.GreenDark, 22, 18, act);
            b.GetComponent<RectTransform>().Anchor(UIKit.Bottom, new Vector2(0, 20), new Vector2(w - 40, 64));
        }

        public static string WhenText(long ms)
        {
            var t = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime();
            return t.ToString("HH:mm") + " · " + t.ToString("dd/MM/yyyy");
        }
    }

    /// <summary>The start screen's idle motion: clouds drifting, the island breathing, crops
    /// swaying, pets hopping, the sun turning, the spinner.</summary>
    public class StartScreenFx : MonoBehaviour
    {
        public RectTransform spin, bob, spinner;
        /// <summary>Called with the layer width on the first frame and whenever it changes (a browser
        /// window resized, a tablet rotated).</summary>
        public System.Action<float> onResize;
        float _width = -1f;
        public readonly List<(RectTransform rt, float speed)> clouds = new List<(RectTransform, float)>();
        public readonly List<RectTransform> sway = new List<RectTransform>();
        public readonly List<RectTransform> hops = new List<RectTransform>();
        Vector2 _bobBase;
        bool _based;
        readonly List<Vector2> _hopBase = new List<Vector2>();

        void Update()
        {
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            if (spin != null) spin.localRotation = Quaternion.Euler(0, 0, -t * 4f);
            if (spinner != null) spinner.localRotation = Quaternion.Euler(0, 0, -Mathf.Floor(t * 12f) * 36f);

            var host = (RectTransform)transform;
            float width = host.rect.width;
            if (onResize != null && !Mathf.Approximately(width, _width)) { _width = width; onResize(width); }
            foreach (var (rt, speed) in clouds)
            {
                if (rt == null) continue;
                var p = rt.anchoredPosition;
                p.x += speed * dt;
                // anchors are fractions of the width: wrap in the same space
                float absX = rt.anchorMin.x * width + p.x;
                if (absX - rt.rect.width * 0.5f > width) p.x -= width + rt.rect.width;
                rt.anchoredPosition = p;
            }

            if (bob != null)
            {
                if (!_based) { _bobBase = bob.anchoredPosition; _based = true; }
                bob.anchoredPosition = _bobBase + new Vector2(0f, Mathf.Sin(t * 0.9f) * 7f);
            }
            for (int i = 0; i < sway.Count; i++)
                if (sway[i] != null) sway[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.3f + i * 1.7f) * 2.2f);

            while (_hopBase.Count < hops.Count) _hopBase.Add(hops[_hopBase.Count].anchoredPosition);
            for (int i = 0; i < hops.Count; i++)
            {
                if (hops[i] == null) continue;
                // a double hop every few seconds, staggered per pet
                float cycle = (t + i * 1.9f) % 4.2f;
                float y = cycle < 0.7f ? Mathf.Abs(Mathf.Sin(cycle / 0.35f * Mathf.PI)) * 16f : 0f;
                hops[i].anchoredPosition = _hopBase[i] + new Vector2(0f, y);
            }
        }
    }
}
