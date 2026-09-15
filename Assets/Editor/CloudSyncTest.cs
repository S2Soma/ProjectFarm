using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Checks accounts and cloud save without touching the network: which save wins in
    /// every situation (and that one account's farm is never copied into another), what a save
    /// says about itself, the fingerprint ignoring the clock, how GoTrue / PostgREST answers are
    /// read and worded, and that the build carries no secret key.</summary>
    public static class CloudSyncTest
    {
        const string U = "11111111-1111-1111-1111-111111111111";
        const string Other = "22222222-2222-2222-2222-222222222222";
        const string Dev = "device-a";

        [MenuItem("Tools/LQ Farm/Kiểm tra tài khoản & đồng bộ")]
        public static void Run()
        {
            var fails = new List<string>();
            var saved = GS.Local;
            try
            {
                SummaryReadsTheFile(fails);
                FingerprintIgnoresClock(fails);
                PlanTable(fails);
                NeverCopiesAnotherAccountsFarm(fails);
                SessionParses(fails);
                ErrorsParseAndReadVietnamese(fails);
                EmailShape(fails);
                NoSecretKeyInBuild(fails);
                SchemaLocksRows(fails);
            }
            finally { GS.Local = saved; }

            if (fails.Count == 0) Debug.Log("Tài khoản & đồng bộ OK — mọi bất biến đạt.");
            else
            {
                foreach (var f in fails) Debug.LogError("Đồng bộ: " + f);
                Debug.LogError($"Đồng bộ: {fails.Count} lỗi.");
            }
        }

        static void Check(List<string> fails, bool ok, string what) { if (!ok && fails.Count < 40) fails.Add(what); }

        static PlayerState Fresh()
        {
            var s = new PlayerState();
            s.NewGame();
            s.SyncPlots();
            s.loaded = true;
            GS.Local = s;
            return s;
        }

        static string Json(PlayerState s) { return SaveIO.ToJson(s); }

        // ------------------------------------------------------------
        static void SummaryReadsTheFile(List<string> fails)
        {
            Check(fails, !SaveSummary.Of(null).exists, "file rỗng mà được coi là có");
            Check(fails, !SaveSummary.Of("{hỏng").exists, "file hỏng mà được coi là có");
            Check(fails, !SaveSummary.Of("{\"version\":1,\"level\":9}").exists, "file v1 (game không mở) mà được coi là có");

            var s = Fresh();
            var fresh = SaveSummary.Of(Json(s));
            Check(fails, fresh.exists && !fresh.played && fresh.level == 1, "ván mới: exists/played/level sai");

            s.lv = 7; s.coin = 123456; s.ownerId = U; s.stats.plant = 3;
            var played = SaveSummary.Of(Json(s));
            Check(fails, played.played && played.level == 7 && played.coin == 123456 && played.ownerId == U,
                  $"ván đã chơi đọc sai: played={played.played} lv={played.level} coin={played.coin} owner={played.ownerId}");

            var onlyXp = Fresh(); onlyXp.xp = 5;
            Check(fails, SaveSummary.Of(Json(onlyXp)).played, "có XP mà không tính là đã chơi");
        }

        static void FingerprintIgnoresClock(List<string> fails)
        {
            var s = Fresh();
            s.coin = 999;
            string a = CloudPlan.Fingerprint(Json(s));
            s.clock.savedAtUtc += 60000; s.clock.lastSeenUtc += 60000; s.clock.savedAtMono += 60;
            string b = CloudPlan.Fingerprint(Json(s));
            Check(fails, a == b && a.Length > 0, "đồng hồ tự lưu đổi mà fingerprint cũng đổi — sẽ đẩy lên mây mỗi 45 s dù không chơi");

            var doc = JObject.Parse(Json(s));
            doc["meta"] = new JObject { ["build"] = "9.9", ["platform"] = "Android" };
            Check(fails, CloudPlan.Fingerprint(doc) == a, "đổi build/platform mà fingerprint đổi");

            s.coin = 1000;
            Check(fails, CloudPlan.Fingerprint(Json(s)) != a, "xu đổi mà fingerprint giữ nguyên — tiến trình sẽ không được đẩy");
            Check(fails, CloudPlan.Fingerprint("không phải json") == "", "fingerprint của chuỗi hỏng phải rỗng");
        }

        static SaveSummary L(bool exists, bool played, string owner, string hash = "h-local")
        {
            return new SaveSummary { exists = exists, played = played, ownerId = owner, level = played ? 5 : 1, hash = hash, device = "", savedAt = 1000 };
        }

        static SaveSummary C(bool exists, long savedAt = 5000, string device = "device-b")
        {
            return new SaveSummary { exists = exists, played = exists, ownerId = U, level = 5, savedAt = savedAt, device = device, hash = "h-cloud" };
        }

        static void Expect(List<string> fails, string name, SyncPlan got, SyncPlan want)
        {
            Check(fails, got == want, $"{name}: ra {got}, cần {want}");
        }

        static void PlanTable(List<string> fails)
        {
            // account has nothing yet
            Expect(fails, "tài khoản trống + ván mới chưa gắn", CloudPlan.Decide(L(true, false, ""), C(false), U, Dev, 0, ""), SyncPlan.UploadLocal);
            Expect(fails, "tài khoản trống + ván đã chơi chưa gắn (đẩy save offline lên)", CloudPlan.Decide(L(true, true, ""), C(false), U, Dev, 0, ""), SyncPlan.UploadLocal);
            Expect(fails, "tài khoản trống + ván của chính nó", CloudPlan.Decide(L(true, true, U), C(false), U, Dev, 5000, "x"), SyncPlan.UploadLocal);
            Expect(fails, "tài khoản trống + không có file", CloudPlan.Decide(L(false, false, ""), C(false), U, Dev, 0, ""), SyncPlan.UploadLocal);
            Expect(fails, "tài khoản trống + ván của tài khoản khác", CloudPlan.Decide(L(true, true, Other), C(false), U, Dev, 0, ""), SyncPlan.StartFresh);

            // account has a farm
            Expect(fails, "tài khoản có + máy không có file", CloudPlan.Decide(L(false, false, ""), C(true), U, Dev, 0, ""), SyncPlan.UseCloud);
            Expect(fails, "tài khoản có + ván mới chưa chơi", CloudPlan.Decide(L(true, false, ""), C(true), U, Dev, 0, ""), SyncPlan.UseCloud);
            Expect(fails, "tài khoản có + ván chưa gắn đã chơi (hỏi)", CloudPlan.Decide(L(true, true, ""), C(true), U, Dev, 0, ""), SyncPlan.AskPlayer);
            Expect(fails, "tài khoản có + ván đã chơi của tài khoản khác", CloudPlan.Decide(L(true, true, Other), C(true), U, Dev, 0, ""), SyncPlan.UseCloud);

            // same account on this device
            Expect(fails, "cùng tài khoản, mây vẫn là bản máy này ghi", CloudPlan.Decide(L(true, true, U), C(true, 5000), U, Dev, 5000, "h-old"), SyncPlan.UploadLocal);
            Expect(fails, "cùng tài khoản, máy này ghi nhưng mất phản hồi", CloudPlan.Decide(L(true, true, U), C(true, 7000, Dev), U, Dev, 5000, "h-old"), SyncPlan.UploadLocal);
            Expect(fails, "máy khác ghi, máy này không chơi thêm", CloudPlan.Decide(L(true, true, U, "h-same"), C(true, 9000), U, Dev, 5000, "h-same"), SyncPlan.UseCloud);
            Expect(fails, "máy khác ghi, máy này cũng chơi thêm (hỏi)", CloudPlan.Decide(L(true, true, U, "h-new"), C(true, 9000), U, Dev, 5000, "h-old"), SyncPlan.AskPlayer);
            Expect(fails, "máy khác ghi, máy này chưa từng nhớ hash (hỏi)", CloudPlan.Decide(L(true, true, U, ""), C(true, 9000), U, Dev, 5000, ""), SyncPlan.AskPlayer);
            Expect(fails, "mã máy rỗng không được coi là 'máy này ghi'", CloudPlan.Decide(L(true, true, U, "h-new"), C(true, 9000, ""), U, "", 5000, "h-old"), SyncPlan.AskPlayer);
        }

        /// <summary>Whatever the combination, a farm stamped with another account's id is never
        /// uploaded to this one — that would let one farm be cloned into any number of accounts.</summary>
        static void NeverCopiesAnotherAccountsFarm(List<string> fails)
        {
            int cases = 0;
            foreach (bool played in new[] { false, true })
            foreach (bool cloudExists in new[] { false, true })
            foreach (long known in new[] { 0L, 5000L })
            foreach (string device in new[] { Dev, "device-b", "" })
            foreach (string hash in new[] { "", "h-local", "h-other" })
            {
                var plan = CloudPlan.Decide(L(true, played, Other), C(cloudExists, 5000, device), U, Dev, known, hash);
                cases++;
                Check(fails, plan != SyncPlan.UploadLocal && plan != SyncPlan.AskPlayer,
                      $"ván của tài khoản khác bị đẩy/hỏi đẩy lên tài khoản này (played={played}, cloud={cloudExists}, known={known}, device={device})");
            }
            Check(fails, cases == 72, "số ca thử sai");
        }

        static void SessionParses(List<string> fails)
        {
            const string login = "{\"access_token\":\"eyJ.a.b\",\"token_type\":\"bearer\",\"expires_in\":3600,\"expires_at\":1760000000," +
                                 "\"refresh_token\":\"r3fr35h\",\"user\":{\"id\":\"" + U + "\",\"aud\":\"authenticated\",\"email\":\"a@b.vn\",\"is_anonymous\":false}}";
            var s = SupaSession.FromJson(JObject.Parse(login));
            Check(fails, s != null && s.userId == U && s.email == "a@b.vn" && s.refreshToken == "r3fr35h" && s.expiresAt == 1760000000 && !s.IsGuest,
                  "đọc phiên đăng nhập email sai");
            var back = s == null ? null : SupaSession.FromJson(s.ToJson());
            Check(fails, back != null && back.userId == U && back.accessToken == "eyJ.a.b" && back.refreshToken == "r3fr35h" && back.expiresAt == s.expiresAt,
                  "phiên lưu PlayerPrefs rồi đọc lại bị mất trường");

            const string anon = "{\"access_token\":\"x\",\"expires_in\":3600,\"refresh_token\":\"y\",\"user\":{\"id\":\"" + Other + "\",\"email\":\"\",\"is_anonymous\":true}}";
            var g = SupaSession.FromJson(JObject.Parse(anon));
            Check(fails, g != null && g.IsGuest && g.Label == "Khách" && g.expiresAt > 1700000000, "phiên khách đọc sai");

            // "Confirm email" on: signup answers with the user and no tokens
            const string confirm = "{\"id\":\"" + U + "\",\"aud\":\"authenticated\",\"email\":\"a@b.vn\",\"confirmation_sent_at\":\"2026-09-15T10:00:00Z\"}";
            Check(fails, SupaSession.FromJson(JObject.Parse(confirm)) == null, "đăng ký chờ xác nhận email bị coi là đã đăng nhập");
        }

        static void ErrorsParseAndReadVietnamese(List<string> fails)
        {
            var bad = SupaReply.From(400, "{\"code\":400,\"error_code\":\"invalid_credentials\",\"msg\":\"Invalid login credentials\"}", false);
            Check(fails, bad.Code == "invalid_credentials" && !bad.Ok, "không đọc được error_code của GoTrue");
            Check(fails, Supa.Friendly(bad) == "Sai email hoặc mật khẩu.", "câu báo sai mật khẩu: " + Supa.Friendly(bad));

            var old = SupaReply.From(400, "{\"error\":\"invalid_grant\",\"error_description\":\"Invalid Refresh Token: Refresh Token Not Found\"}", false);
            Check(fails, old.Code == "invalid_grant", "không đọc được lỗi kiểu cũ {error, error_description}");

            var table = SupaReply.From(404, "{\"code\":\"PGRST205\",\"details\":null,\"hint\":null,\"message\":\"Could not find the table 'public.saves' in the schema cache\"}", false);
            Check(fails, table.Code == "PGRST205" && Supa.Friendly(table).Contains("schema.sql"), "bảng chưa tạo phải chỉ tới schema.sql");

            var anon = SupaReply.From(422, "{\"code\":422,\"error_code\":\"anonymous_provider_disabled\",\"msg\":\"Anonymous sign-ins are disabled\"}", false);
            Check(fails, Supa.Friendly(anon).Contains("chơi khách"), "chưa bật khách phải nói rõ");

            var net = SupaReply.From(0, null, true);
            Check(fails, !net.Ok && Supa.Friendly(net).Contains("kết nối"), "mất mạng phải báo không kết nối được");
            Check(fails, SupaReply.From(201, "", false).Ok, "201 phải là thành công");
            Check(fails, Supa.Friendly(SupaReply.From(503, "<html>", false)).Contains("Máy chủ"), "lỗi 5xx phải báo máy chủ lỗi");
            Check(fails, Supa.Friendly(SupaReply.From(429, "", false)).Contains("thử lại"), "429 phải bảo thử lại sau");

            // every code the client names reads as Vietnamese, not the server's English
            string[] codes = { "invalid_credentials", "email_not_confirmed", "confirm_email", "user_already_exists", "email_exists", "weak_password",
                               "email_address_invalid", "validation_failed", "anonymous_provider_disabled", "signup_disabled", "email_provider_disabled",
                               "over_request_rate_limit", "over_email_send_rate_limit", "refresh_token_not_found", "session_not_found", "no_session",
                               "PGRST205", "42501" };
            foreach (var c in codes)
            {
                string m = Supa.Friendly(new SupaReply { Status = 400, Code = c, Message = "English server text" });
                Check(fails, !m.Contains("English") && m.Length > 8, $"mã {c} chưa có câu tiếng Việt");
            }
        }

        static void EmailShape(List<string> fails)
        {
            foreach (var ok in new[] { "a@b.co", "nong.dan+mit@gmail.com", "x@sub.domain.vn" })
                Check(fails, StartScreen.LooksLikeEmail(ok), "email hợp lệ bị từ chối: " + ok);
            foreach (var no in new[] { "", "ab", "a@b", "a@@b.com", "a b@c.com", "@b.com", "a@.com", "a@b.com." })
                Check(fails, !StartScreen.LooksLikeEmail(no), "email sai lọt qua: " + no);
        }

        static void NoSecretKeyInBuild(List<string> fails)
        {
            Check(fails, !new SupaConfig { url = "https://x.supabase.co", anonKey = "sb_secret_0123456789abcdefghijklmn" }.Ok, "secret key mà vẫn được dùng");
            Check(fails, !new SupaConfig { url = "https://x.supabase.co", anonKey = "eyJhbGciOi.eyJyb2xlIjoic2VydmljZV9yb2xlIn0.service_role" }.Ok, "service_role mà vẫn được dùng");
            Check(fails, !new SupaConfig { url = "http://x.supabase.co", anonKey = "sb_publishable_0123456789abcdefghij" }.Ok, "URL không https mà vẫn được dùng");
            Check(fails, new SupaConfig { url = "https://x.supabase.co", anonKey = "sb_publishable_0123456789abcdefghij" }.Ok, "publishable key hợp lệ bị từ chối");

            // the real file that ships
            var ta = Resources.Load<TextAsset>("Config/supabase");
            if (ta == null) return;                     // no project configured: the game plays offline
            var o = JObject.Parse(ta.text);
            string key = (string)o["anonKey"] ?? "";
            Check(fails, !key.StartsWith("sb_secret_"), "Resources/Config/supabase.json chứa SECRET key — ai giải nén APK cũng đọc được");
            if (key.StartsWith("eyJ"))
            {
                // legacy JWT key: its payload says which role it is
                string[] parts = key.Split('.');
                string role = "";
                if (parts.Length == 3)
                {
                    string b64 = parts[1].Replace('-', '+').Replace('_', '/');
                    b64 += new string('=', (4 - b64.Length % 4) % 4);
                    try { role = (string)JObject.Parse(System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(b64)))["role"] ?? ""; }
                    catch (System.Exception) { }
                }
                Check(fails, role == "anon", "khoá JWT trong supabase.json không phải role anon (là '" + role + "')");
            }
        }

        static void SchemaLocksRows(List<string> fails)
        {
            const string path = "Tools/supabase/schema.sql";
            Check(fails, File.Exists(path), "thiếu " + path);
            if (!File.Exists(path)) return;
            string sql = File.ReadAllText(path).ToLowerInvariant();
            Check(fails, sql.Contains("alter table public.saves    enable row level security") || sql.Contains("alter table public.saves enable row level security"), "saves chưa bật RLS");
            Check(fails, sql.Contains("enable row level security") && sql.Contains("public.profiles enable row level security"), "profiles chưa bật RLS");
            Check(fails, sql.Contains("auth.uid()) = user_id"), "policy saves không so auth.uid() với user_id");
            Check(fails, sql.Contains("revoke all on public.saves    from anon") || sql.Contains("revoke all on public.saves from anon"), "anon vẫn có quyền trên saves");
            Check(fails, !sql.Contains("to anon"), "schema cấp quyền cho anon");
            Check(fails, !sql.Contains("for delete"), "schema cho phép xoá từ game");
        }
    }
}
