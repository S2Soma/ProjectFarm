using System;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>Menu ▸ Tài khoản: who is signed in, whether the farm is on the account, linking an
    /// email to a guest account, sync now, sign out.</summary>
    public class AccountPanel : PanelBase
    {
        public AccountPanel(GameApp app) : base(app) { }
        public override string Title => "Tài khoản";
        public override Vector2 Size => new Vector2(720, !Supa.SignedIn ? 380f : Supa.Session.IsGuest ? 480f : 440f);

        InputField _email, _pass;
        string _message = "";
        bool _good, _busy, _confirmOut;

        float W => Body.rect.width > 10f ? Body.rect.width : 676f;

        public override void Build() { Draw(); }

        public override void Refresh() { if (!_busy) Draw(); }

        void Draw()
        {
            string typedEmail = _email != null ? _email.text : "";
            for (int i = Body.childCount - 1; i >= 0; i--)
            {
                var child = Body.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(child);
            }
            _email = _pass = null;

            var sess = Supa.Session;
            bool signed = Supa.SignedIn;

            // ---- who ----
            var who = UIKit.Node("who", Body);
            who.Anchor(UIKit.Top, new Vector2(0, -4), new Vector2(W, 92));
            SurfaceLook.Add(who, Looks.Row, 20f);
            var disc = UIKit.Node("avatar", who);
            disc.Anchor(UIKit.Left, new Vector2(12, 0), new Vector2(70, 70));
            SurfaceLook.Add(disc, Looks.BtnCream, SurfaceLook.Pill);
            string pet = string.IsNullOrEmpty(GS.Local.petActive) ? "mit" : GS.Local.petActive;
            var face = UIKit.Img(disc, Art.Load("Art/pets/" + pet + "/portrait") ?? Art.Load("Art/gen/account"), Color.white, "pet");
            face.preserveAspect = true;
            face.rectTransform.Stretch(6, 6, 6, 8);

            string name = !signed ? "Chưa đăng nhập" : sess.Label;
            var n = UIKit.Label(who, name, name.Length > 26 ? 19 : 23, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            n.rectTransform.Anchor(UIKit.Left, new Vector2(96, 14), new Vector2(W - 330, 32));
            string kind = !signed ? "Tiến trình chỉ nằm trên máy này"
                        : (sess.IsGuest ? "Tài khoản khách" : "Tài khoản email") + " · ID " + sess.userId.Substring(0, Math.Min(8, sess.userId.Length));
            var k = UIKit.Label(who, kind, 15, Theme.InkSoft, TextAnchor.MiddleLeft);
            k.rectTransform.Anchor(UIKit.Left, new Vector2(96, -18), new Vector2(W - 330, 24));

            // sync chip
            Color tone; string chip;
            StatusOf(out tone, out chip);
            var st = UIKit.Node("sync", who);
            st.Anchor(UIKit.Right, new Vector2(-14, 0), new Vector2(212, 40));
            SurfaceLook.Add(st, Looks.Well, SurfaceLook.Pill);
            var ci = UIKit.Img(st, Art.Load("Art/gen/cloud"), tone, "cloud");
            ci.preserveAspect = true;
            ci.rectTransform.Anchor(UIKit.Left, new Vector2(12, 0), new Vector2(26, 26));
            var cl = UIKit.Label(st, chip, 15, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            cl.rectTransform.Anchor(UIKit.Left, new Vector2(44, 0), new Vector2(160, 26));

            float y = -112f;
            if (!signed)
            {
                Para("Đăng nhập hoặc chơi bằng tài khoản khách để lưu nông trại lên mây. Nông trại trên máy này được giữ nguyên.", y, 60);
                var go = UIKit.Btn(Body, "Đăng nhập / Đăng ký", Theme.Blue, Theme.BlueDeep, 24, 18, () => GameApp.Restart(true));
                go.GetComponent<RectTransform>().Anchor(UIKit.Top, new Vector2(0, y - 78), new Vector2(360, 66));
            }
            else if (sess.IsGuest)
            {
                Para("Tài khoản khách không có email để đăng nhập lại. Liên kết email để giữ nông trại khi đổi máy hoặc cài lại game.", y, 52);
                float fy = y - 66f;
                const float PassW = 200f, BtnW = 150f, Gap = 10f;
                float emailW = W - PassW - BtnW - Gap * 2f;
                _email = UIKit.TextField(Body, "ten@email.com", InputField.ContentType.EmailAddress, 20);
                ((RectTransform)_email.transform).Anchor(UIKit.TopLeft, new Vector2(0, fy), new Vector2(emailW, 58));
                _email.text = typedEmail;
                _pass = UIKit.TextField(Body, "Mật khẩu", InputField.ContentType.Password, 20, Link);
                ((RectTransform)_pass.transform).Anchor(UIKit.TopLeft, new Vector2(emailW + Gap, fy), new Vector2(PassW, 58));
                var link = UIKit.Btn(Body, "Liên kết", Theme.Green, Theme.GreenDark, 22, 18, Link);
                link.GetComponent<RectTransform>().Anchor(UIKit.TopRight, new Vector2(0, fy), new Vector2(BtnW, 58));
                link.interactable = !_busy;
            }
            else
            {
                Para("Nông trại tự lưu lên tài khoản mỗi " + (int)CloudSync.Interval + " giây và khi thoát game. Đăng nhập email này trên máy khác để chơi tiếp.", y, 52);
                string when = CloudSync.LastSyncUtc > 0 ? "Lần đồng bộ gần nhất: " + SaveChoice.WhenText(CloudSync.LastSyncUtc) : "";
                Para(when, y - 60, 30, 16);
            }

            // ---- message ----
            if (!string.IsNullOrEmpty(_message))
                Para(_message, !signed ? -268f : sess.IsGuest ? -246f : -206f, 52, 17, _good ? Theme.GreenDeep : Theme.Hex("#B03A31"));

            // ---- actions ----
            if (signed)
            {
                float half = (W - 16f) / 2f;
                var sync = UIKit.Btn(Body, _busy ? "Đang đồng bộ…" : "Đồng bộ ngay", Theme.Blue, Theme.BlueDeep, 22, 18, SyncNow);
                sync.GetComponent<RectTransform>().Anchor(UIKit.BottomLeft, new Vector2(0, 6), new Vector2(half, 62));
                sync.interactable = !_busy;
                string outText = _confirmOut ? (sess.IsGuest ? "Chạm lần nữa — khách sẽ mất" : "Chạm lần nữa để đăng xuất") : "Đăng xuất";
                var so = UIKit.Btn(Body, outText, _confirmOut ? Theme.Red : Theme.Cream3, Theme.RedDeep, _confirmOut ? 19 : 22, 18, SignOut);
                so.GetComponent<RectTransform>().Anchor(UIKit.BottomRight, new Vector2(0, 6), new Vector2(half, 62));
                so.interactable = !_busy;
            }
        }

        static void StatusOf(out Color tone, out string text)
        {
            if (!Supa.SignedIn) { tone = Theme.InkSoft; text = "Chưa bật đồng bộ"; return; }
            switch (CloudSync.State)
            {
                case SyncState.Synced: tone = Theme.Green; text = "Đã đồng bộ"; break;
                case SyncState.Checking: tone = Theme.Blue; text = "Đang đồng bộ…"; break;
                case SyncState.Offline: tone = Theme.Amber; text = "Chưa có mạng"; break;
                case SyncState.Conflict: tone = Theme.Amber; text = "Chờ chọn bản lưu"; break;
                case SyncState.Error: tone = Theme.Red; text = "Lỗi đồng bộ"; break;
                default: tone = Theme.InkSoft; text = "Đang chờ"; break;
            }
        }

        Text Para(string text, float y, float h, int size = 17, Color? color = null)
        {
            var t = UIKit.Label(Body, text, size, color ?? Theme.InkSoft, TextAnchor.MiddleCenter);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.rectTransform.Anchor(UIKit.Top, new Vector2(0, y), new Vector2(W, h));
            return t;
        }

        void Say(string text, bool good) { _message = text ?? ""; _good = good; }

        // ================================================================
        // actions
        // ================================================================
        void Link()
        {
            if (_busy || _email == null) return;
            string email = (_email.text ?? "").Trim(), pass = _pass.text ?? "";
            if (!StartScreen.LooksLikeEmail(email)) { Say("Email chưa đúng — ví dụ: ten@email.com", false); Draw(); return; }
            if (pass.Length < 6) { Say("Mật khẩu cần ít nhất 6 ký tự.", false); Draw(); return; }
            _busy = true;
            Say("Đang liên kết…", true);
            Draw();
            Supa.Run(Supa.LinkEmail(email, pass, r =>
            {
                _busy = false;
                if (r.Ok && r.Code == "confirm_email") Say("Đã gửi thư xác nhận tới " + email + ". Bấm xác nhận trong thư là xong.", true);
                else if (r.Ok) Say("Đã liên kết! Đăng nhập bằng " + email + " trên máy khác để chơi tiếp.", true);
                else Say(Supa.Friendly(r), false);
                if (app.Panel == this) Draw();
            }));
        }

        void SyncNow()
        {
            if (_busy || CloudSync.I == null) return;
            _busy = true;
            Say("", true);
            Draw();
            CloudSync.I.SyncNow(ok =>
            {
                _busy = false;
                if (ok) Say("Đã lưu nông trại lên tài khoản.", true);
                else if (CloudSync.State == SyncState.Offline) Say("Chưa kết nối được máy chủ. Tiến trình vẫn lưu trên máy.", false);
                else if (CloudSync.State != SyncState.Conflict) Say(string.IsNullOrEmpty(CloudSync.LastError) ? "Chưa đồng bộ được, thử lại sau." : CloudSync.LastError, false);
                if (app.Panel == this) Draw();
            });
        }

        void SignOut()
        {
            if (_busy) return;
            if (!_confirmOut) { _confirmOut = true; Draw(); return; }
            _busy = true;
            Say("Đang lưu lên tài khoản rồi đăng xuất…", true);
            Draw();
            Supa.Run(SignOutRoutine());
        }

        System.Collections.IEnumerator SignOutRoutine()
        {
            if (CloudSync.I != null) yield return CloudSync.I.Flush();
            // Keep this device's file: signing back in finds it already on the account, and a
            // different account starts its own farm instead of inheriting this one.
            GS.Save();
            yield return Supa.SignOut(null);
            CloudSync.Verified = false;
            GameApp.Restart(true);
        }
    }

    /// <summary>In game: another device played this account while this one was playing too.</summary>
    public class SaveChoicePanel : PanelBase
    {
        readonly CloudSync.Check _check;
        readonly Action<bool> _pick;
        bool _done;

        public SaveChoicePanel(GameApp app, CloudSync.Check check, Action<bool> pick) : base(app)
        {
            _check = check;
            _pick = pick;
        }

        public override string Title => "Chọn nông trại để chơi";
        public override Vector2 Size => new Vector2(880, 540);

        public override void Build()
        {
            SaveChoice.Build(Body, _check, useCloud =>
            {
                if (_done) return;
                _done = true;
                if (!useCloud) SaveIO.BackupRaw("tai-khoan-cu", _check.cloudJson);
                app.CloseAll();
                _pick(useCloud);
            }, false);
        }
    }
}
