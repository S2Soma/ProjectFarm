using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace LQFarm
{
    /// <summary>Resources/Config/supabase.json — the project URL and its publishable key.
    ///
    /// Only ever the publishable (anon) key: the file ships inside the build and anyone who unzips
    /// the APK can read it. What stops one player touching another's save is Row Level Security
    /// on the tables (Tools/supabase/schema.sql), not the key.</summary>
    public class SupaConfig
    {
        public string url = "";
        public string anonKey = "";

        public bool Ok => url.StartsWith("https://") && anonKey.Length > 20
                          // a secret key in a client build is a leak, not a config; refuse to use it
                          && !anonKey.StartsWith("sb_secret_") && !anonKey.Contains("service_role");

        public static SupaConfig Load()
        {
            var c = new SupaConfig();
            var ta = Resources.Load<TextAsset>("Config/supabase");
            if (ta == null) return c;
            try
            {
                var o = JObject.Parse(ta.text);
                c.url = ((string)o["url"] ?? "").Trim().TrimEnd('/');
                c.anonKey = ((string)o["anonKey"] ?? "").Trim();
            }
            catch (Exception e) { Debug.LogWarning("supabase.json hỏng: " + e.Message); }
            if (!c.Ok && c.anonKey.StartsWith("sb_secret_"))
                Debug.LogError("supabase.json đang chứa SECRET key — không dùng. Chỉ dán publishable/anon key.");
            return c;
        }
    }

    /// <summary>A signed-in player, as GoTrue hands it back. Kept in PlayerPrefs (the app's
    /// private storage), never in the save file: the save is uploaded, the tokens must not be.</summary>
    public class SupaSession
    {
        public string accessToken = "", refreshToken = "", userId = "", email = "";
        public long expiresAt;          // unix seconds
        public bool anonymous;

        public bool IsGuest => anonymous || string.IsNullOrEmpty(email);
        public string Label => IsGuest ? "Khách" : email;

        public static SupaSession FromJson(JObject o)
        {
            if (o == null || string.IsNullOrEmpty((string)o["access_token"])) return null;
            var u = o["user"] as JObject;
            var s = new SupaSession
            {
                accessToken = (string)o["access_token"] ?? "",
                refreshToken = (string)o["refresh_token"] ?? "",
                userId = (string)u?["id"] ?? "",
                email = (string)u?["email"] ?? "",
                anonymous = (bool?)u?["is_anonymous"] ?? false,
            };
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            s.expiresAt = (long?)o["expires_at"] ?? now + ((long?)o["expires_in"] ?? 3600);
            return string.IsNullOrEmpty(s.userId) ? null : s;
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["access_token"] = accessToken, ["refresh_token"] = refreshToken, ["expires_at"] = expiresAt,
                ["user"] = new JObject { ["id"] = userId, ["email"] = email, ["is_anonymous"] = anonymous },
            };
        }
    }

    /// <summary>One HTTP round trip. <see cref="Network"/> means the server was never reached
    /// (no signal, DNS, timeout) — the game carries on offline. Anything else is the server's
    /// answer and <see cref="Code"/> is its error_code / PostgREST code when it gave one.</summary>
    public struct SupaReply
    {
        public long Status;
        public string Body;
        public bool Network;
        public string Code, Message;

        public bool Ok => !Network && Status >= 200 && Status < 300;

        public JToken Json
        {
            get
            {
                if (string.IsNullOrEmpty(Body)) return null;
                try { return JToken.Parse(Body); } catch (Exception) { return null; }
            }
        }

        public static SupaReply From(long status, string body, bool network)
        {
            var r = new SupaReply { Status = status, Body = body, Network = network };
            if (network || (status >= 200 && status < 300) || string.IsNullOrEmpty(body)) return r;
            try
            {
                var o = JToken.Parse(body) as JObject;
                if (o != null)
                {
                    // GoTrue: {code, error_code, msg}; older GoTrue: {error, error_description};
                    // PostgREST: {code:"PGRST205", message}
                    r.Code = (string)o["error_code"] ?? (o["code"]?.Type == JTokenType.String ? (string)o["code"] : null) ?? (string)o["error"];
                    r.Message = (string)o["msg"] ?? (string)o["message"] ?? (string)o["error_description"] ?? "";
                }
            }
            catch (Exception) { }
            return r;
        }
    }

    /// <summary>Supabase over plain REST: GoTrue for accounts, PostgREST for the two tables.
    /// No SDK — two endpoints' worth of JSON does not justify a dependency that has to survive
    /// IL2CPP stripping.
    ///
    /// Every call is a coroutine run by <see cref="Run"/> on a DontDestroyOnLoad host, so a request
    /// outlives the screen — or the whole game instance — that started it.</summary>
    public static class Supa
    {
        const string PrefSession = "mitfarm.supa.session";
        const int TimeoutSeconds = 15;

        static SupaConfig _config;
        public static SupaConfig Config => _config ?? (_config = SupaConfig.Load());
        public static bool Configured => Config.Ok;

        /// <summary>Runs network coroutines on a host of their own that survives the game being
        /// torn down and rebuilt (sign out, a save pulled from the account).</summary>
        public static Coroutine Run(IEnumerator co) { return Host.I.StartCoroutine(co); }

        class Host : MonoBehaviour
        {
            static Host _i;
            public static Host I
            {
                get
                {
                    if (_i == null)
                    {
                        var go = new GameObject("~Supabase");
                        DontDestroyOnLoad(go);
                        _i = go.AddComponent<Host>();
                    }
                    return _i;
                }
            }
        }

        public static SupaSession Session { get; private set; }
        public static bool SignedIn => Session != null && !string.IsNullOrEmpty(Session.refreshToken);

        /// <summary>Raised whenever the session is set, refreshed into a new user, or cleared.</summary>
        public static event Action SessionChanged;

        // ================================================================
        // session persistence
        // ================================================================
        public static void RestoreSession()
        {
            if (Session != null) return;
            string raw = PlayerPrefs.GetString(PrefSession, "");
            if (string.IsNullOrEmpty(raw)) return;
            try { Session = SupaSession.FromJson(JObject.Parse(raw)); }
            catch (Exception) { Session = null; }
        }

        static void SetSession(SupaSession s)
        {
            bool userChanged = (Session?.userId ?? "") != (s?.userId ?? "");
            Session = s;
            if (s == null) PlayerPrefs.DeleteKey(PrefSession);
            else PlayerPrefs.SetString(PrefSession, s.ToJson().ToString(Newtonsoft.Json.Formatting.None));
            PlayerPrefs.Save();
            if (userChanged || s == null) SessionChanged?.Invoke();
        }

        /// <summary>The screenshot pass shows the account screens for a guest, an email account and
        /// nobody. In memory only: nothing is written to PlayerPrefs and nothing is announced.</summary>
        public static void UseSessionForAudit(SupaSession s) { Session = s; }

        /// <summary>Forget the account on this device without asking the server (the refresh
        /// token is revoked by <see cref="SignOut"/> when the network is there).</summary>
        public static void ForgetSession() { SetSession(null); }

        // ================================================================
        // transport
        // ================================================================
        public static IEnumerator Send(string method, string path, string json, Action<SupaReply> done,
                                       bool withUser = true, string prefer = null)
        {
            if (!Configured) { done?.Invoke(new SupaReply { Network = true, Message = "chưa cấu hình" }); yield break; }

            using (var req = new UnityWebRequest(Config.url + path, method))
            {
                if (json != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = TimeoutSeconds;
                // The publishable key identifies the project; it is not a JWT and never goes in
                // Authorization. The user's access token does.
                req.SetRequestHeader("apikey", Config.anonKey);
                if (withUser && Session != null && !string.IsNullOrEmpty(Session.accessToken))
                    req.SetRequestHeader("Authorization", "Bearer " + Session.accessToken);
                if (prefer != null) req.SetRequestHeader("Prefer", prefer);

                yield return req.SendWebRequest();

                bool network = req.result == UnityWebRequest.Result.ConnectionError
                               || req.result == UnityWebRequest.Result.DataProcessingError
                               || req.responseCode == 0;
                done?.Invoke(SupaReply.From(req.responseCode, req.downloadHandler?.text, network));
            }
        }

        /// <summary>Like <see cref="Send"/> for calls made as the player: refreshes a token that
        /// is about to expire first, and once more on a 401, then retries.</summary>
        public static IEnumerator SendAuthed(string method, string path, string json, Action<SupaReply> done, string prefer = null)
        {
            if (!SignedIn) { done?.Invoke(new SupaReply { Status = 401, Code = "no_session" }); yield break; }
            if (Session.expiresAt - 60 < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                SupaReply rr = default;
                yield return Refresh(r => rr = r);
                if (!rr.Ok) { done?.Invoke(rr); yield break; }
            }

            SupaReply first = default;
            yield return Send(method, path, json, r => first = r, true, prefer);
            if (first.Status != 401) { done?.Invoke(first); yield break; }

            SupaReply again = default;
            yield return Refresh(r => again = r);
            if (!again.Ok) { done?.Invoke(again); yield break; }
            yield return Send(method, path, json, done, true, prefer);
        }

        static bool _refreshing;

        /// <summary>Trade the refresh token for a new pair. Refresh tokens rotate, so the new one
        /// is written to disk before anything else can use the old one. A token the server has
        /// thrown away ends the session; a network failure keeps it for the next try.</summary>
        public static IEnumerator Refresh(Action<SupaReply> done)
        {
            while (_refreshing) yield return null;
            if (!SignedIn) { done?.Invoke(new SupaReply { Status = 401, Code = "no_session" }); yield break; }
            // another caller refreshed while this one waited
            if (Session.expiresAt - 60 > DateTimeOffset.UtcNow.ToUnixTimeSeconds() && _lastRefreshOk > Time.realtimeSinceStartup - 5f)
            { done?.Invoke(new SupaReply { Status = 200 }); yield break; }

            _refreshing = true;
            SupaReply reply = default;
            var body = new JObject { ["refresh_token"] = Session.refreshToken };
            yield return Send("POST", "/auth/v1/token?grant_type=refresh_token", body.ToString(Newtonsoft.Json.Formatting.None), r => reply = r, false);
            _refreshing = false;

            if (reply.Ok)
            {
                var s = SupaSession.FromJson(reply.Json as JObject);
                if (s != null) { SetSession(s); _lastRefreshOk = Time.realtimeSinceStartup; }
                else reply.Status = 500;
            }
            else if (!reply.Network && reply.Status >= 400 && reply.Status < 500)
            {
                // refresh_token_not_found / revoked / user deleted — this session is over
                SetSession(null);
            }
            done?.Invoke(reply);
        }
        static float _lastRefreshOk = -999f;

        // ================================================================
        // accounts
        // ================================================================
        /// <summary>"Chơi ngay": a real account with no email. Its id is stable, so its save syncs
        /// like any other; linking an email later keeps the same id.</summary>
        public static IEnumerator SignInGuest(Action<SupaReply> done)
        {
            SupaReply reply = default;
            yield return Send("POST", "/auth/v1/signup", "{\"data\":{}}", r => reply = r, false);
            if (reply.Ok) AdoptSession(ref reply);
            done?.Invoke(reply);
        }

        public static IEnumerator SignIn(string email, string password, Action<SupaReply> done)
        {
            SupaReply reply = default;
            var body = new JObject { ["email"] = email.Trim(), ["password"] = password };
            yield return Send("POST", "/auth/v1/token?grant_type=password", body.ToString(Newtonsoft.Json.Formatting.None), r => reply = r, false);
            if (reply.Ok) AdoptSession(ref reply);
            done?.Invoke(reply);
        }

        /// <summary>Sign up. When the project has "Confirm email" on, GoTrue answers 200 with a
        /// user and NO session — <c>reply.Code</c> is then "confirm_email" and the player has to
        /// open the mail before <see cref="SignIn"/> works.</summary>
        public static IEnumerator SignUp(string email, string password, Action<SupaReply> done)
        {
            SupaReply reply = default;
            var body = new JObject { ["email"] = email.Trim(), ["password"] = password };
            yield return Send("POST", "/auth/v1/signup", body.ToString(Newtonsoft.Json.Formatting.None), r => reply = r, false);
            if (reply.Ok) AdoptSession(ref reply);
            done?.Invoke(reply);
        }

        static void AdoptSession(ref SupaReply reply)
        {
            var s = SupaSession.FromJson(reply.Json as JObject);
            if (s != null) { SetSession(s); _lastRefreshOk = Time.realtimeSinceStartup; }
            else { reply.Code = "confirm_email"; }
        }

        /// <summary>Give a guest account an email and password. The user id — and so the cloud
        /// save — stays the same.</summary>
        public static IEnumerator LinkEmail(string email, string password, Action<SupaReply> done)
        {
            SupaReply reply = default;
            var body = new JObject { ["email"] = email.Trim(), ["password"] = password };
            yield return SendAuthed("PUT", "/auth/v1/user", body.ToString(Newtonsoft.Json.Formatting.None), r => reply = r);
            // GoTrue refuses a password on an anonymous user that has no email yet: set the email
            // first (applied at once when "Confirm email" is off), then the password.
            if (!reply.Ok && !reply.Network && (reply.Message ?? "").IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var first = new JObject { ["email"] = email.Trim() };
                yield return SendAuthed("PUT", "/auth/v1/user", first.ToString(Newtonsoft.Json.Formatting.None), r => reply = r);
                if (reply.Ok)
                {
                    var second = new JObject { ["password"] = password };
                    yield return SendAuthed("PUT", "/auth/v1/user", second.ToString(Newtonsoft.Json.Formatting.None), r => reply = r);
                }
            }
            if (reply.Ok && Session != null)
            {
                var u = reply.Json as JObject;
                string now = (string)u?["email"] ?? "";
                string pending = (string)u?["new_email"] ?? "";
                if (!string.IsNullOrEmpty(now))
                {
                    Session.email = now;
                    Session.anonymous = (bool?)u?["is_anonymous"] ?? false;
                    SetSession(Session);
                }
                else if (!string.IsNullOrEmpty(pending)) reply.Code = "confirm_email";
            }
            done?.Invoke(reply);
        }

        public static IEnumerator SignOut(Action done)
        {
            if (SignedIn)
            {
                // best effort: revoke the refresh token on the server; the device forgets it anyway
                yield return Send("POST", "/auth/v1/logout?scope=local", null, _ => { }, true);
            }
            SetSession(null);
            done?.Invoke();
        }

        // ================================================================
        // player-facing wording
        // ================================================================
        public static string Friendly(SupaReply r)
        {
            if (r.Network) return "Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.";
            string code = r.Code ?? "";
            string msg = (r.Message ?? "").ToLowerInvariant();
            switch (code)
            {
                case "invalid_credentials":
                case "invalid_grant":
                    return msg.Contains("confirm") ? "Email chưa được xác nhận. Mở thư xác nhận rồi thử lại." : "Sai email hoặc mật khẩu.";
                case "email_not_confirmed": return "Email chưa được xác nhận. Mở thư xác nhận rồi thử lại.";
                case "confirm_email": return "Đã gửi thư xác nhận. Mở email, bấm xác nhận rồi quay lại đăng nhập.";
                case "user_already_exists":
                case "email_exists": return "Email này đã có tài khoản — hãy đăng nhập.";
                case "weak_password": return "Mật khẩu quá yếu (ít nhất 6 ký tự).";
                case "email_address_invalid":
                case "validation_failed": return "Email không hợp lệ.";
                case "anonymous_provider_disabled": return "Máy chủ chưa bật chơi khách (Anonymous sign-ins).";
                case "signup_disabled": return "Máy chủ đang tắt đăng ký tài khoản mới.";
                case "email_provider_disabled": return "Máy chủ chưa bật đăng nhập bằng email.";
                case "over_request_rate_limit":
                case "over_email_send_rate_limit": return "Thao tác quá nhanh, thử lại sau ít phút.";
                case "refresh_token_not_found":
                case "refresh_token_already_used":
                case "session_not_found":
                case "no_session": return "Phiên đăng nhập đã hết. Hãy đăng nhập lại.";
                case "PGRST205":
                case "42P01": return "Máy chủ chưa có bảng lưu (chạy Tools/supabase/schema.sql).";
                case "42501": return "Máy chủ từ chối quyền ghi (kiểm tra RLS trong schema.sql).";
            }
            if (r.Status == 429) return "Thao tác quá nhanh, thử lại sau ít phút.";
            if (r.Status >= 500) return "Máy chủ đang lỗi, thử lại sau.";
            return string.IsNullOrEmpty(r.Message) ? "Lỗi máy chủ (" + r.Status + ")." : r.Message;
        }
    }
}
