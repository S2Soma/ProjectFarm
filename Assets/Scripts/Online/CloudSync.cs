using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LQFarm
{
    /// <summary>What a save file says about itself, read from its JSON without loading it.</summary>
    public struct SaveSummary
    {
        public bool exists;
        /// <summary>Anything beyond a fresh farm: a level, XP, a crop planted or harvested.</summary>
        public bool played;
        public string ownerId;
        public int level;
        public long coin;
        /// <summary>For a local file its clock.savedAtUtc; for the cloud row the saved_at column.</summary>
        public long savedAt;
        public string device;
        /// <summary><see cref="CloudPlan.Fingerprint"/> of the file — equal means same progress.</summary>
        public string hash;

        public static SaveSummary Of(string raw)
        {
            var s = new SaveSummary { ownerId = "", device = "", hash = "" };
            if (string.IsNullOrEmpty(raw)) return s;
            JObject doc;
            try { doc = JObject.Parse(raw); } catch (Exception) { return s; }
            int v = (int?)doc["v"] ?? 0;
            if (v < SaveIO.MinVersion) return s;          // the game would not open it either

            s.exists = true;
            var p = doc["player"] as JObject;
            s.ownerId = (string)p?["ownerId"] ?? "";
            s.level = Math.Max(1, (int?)p?["level"] ?? 1);
            s.coin = (long?)p?["coin"] ?? 0;
            long xp = (long?)p?["xp"] ?? 0;
            var st = doc["stats"] as JObject;
            int plant = (int?)st?["plant"] ?? 0, harvest = (int?)st?["harvest"] ?? 0;
            s.played = s.level > 1 || xp > 0 || plant > 0 || harvest > 0;
            s.savedAt = (long?)doc["clock"]?["savedAtUtc"] ?? 0;
            s.hash = CloudPlan.Fingerprint(doc);
            return s;
        }
    }

    public enum SyncPlan
    {
        /// <summary>Keep this device's file and write it to the account.</summary>
        UploadLocal,
        /// <summary>Replace this device's file with the account's.</summary>
        UseCloud,
        /// <summary>Both hold progress the other does not know about: the player picks.</summary>
        AskPlayer,
        /// <summary>The file here belongs to another account and this one has nothing yet: set
        /// the file aside and begin a new farm (never copy one account's farm into another).</summary>
        StartFresh,
    }

    /// <summary>The rules for which save wins. Pure, so "Kiểm tra đồng bộ" can walk every case.</summary>
    public static class CloudPlan
    {
        /// <summary>A hash of the progress in a save, ignoring what changes by itself: the clock
        /// (rewritten every autosave) and the build/platform stamp.</summary>
        public static string Fingerprint(JObject doc)
        {
            if (doc == null) return "";
            var c = (JObject)doc.DeepClone();
            c.Remove("clock");
            c.Remove("meta");
            return Hash128.Compute(c.ToString(Newtonsoft.Json.Formatting.None)).ToString();
        }

        public static string Fingerprint(string json)
        {
            try { return Fingerprint(JObject.Parse(json)); } catch (Exception) { return ""; }
        }

        /// <param name="knownSavedAt">saved_at of the cloud row as of this device's last sync
        /// with this account (0: never synced).</param>
        /// <param name="knownHash">fingerprint of the progress that sync carried.</param>
        public static SyncPlan Decide(SaveSummary local, SaveSummary cloud, string userId, string deviceId,
                                      long knownSavedAt, string knownHash)
        {
            bool ownedByOther = !string.IsNullOrEmpty(local.ownerId) && local.ownerId != userId;

            if (!cloud.exists)
                return local.exists && ownedByOther ? SyncPlan.StartFresh : SyncPlan.UploadLocal;

            // nothing here worth keeping
            if (!local.exists || !local.played) return SyncPlan.UseCloud;

            if (local.ownerId == userId)
            {
                // the account still holds what this device last wrote: this file is that or newer
                if (cloud.savedAt == knownSavedAt || (!string.IsNullOrEmpty(deviceId) && cloud.device == deviceId))
                    return SyncPlan.UploadLocal;
                // another device wrote since; did this one move on too?
                return !string.IsNullOrEmpty(knownHash) && local.hash == knownHash ? SyncPlan.UseCloud : SyncPlan.AskPlayer;
            }

            // a farm played before ever signing in, meeting an account that already has one
            if (!ownedByOther) return SyncPlan.AskPlayer;

            // another account's farm on this device; that account's own copy is in its cloud row
            return SyncPlan.UseCloud;
        }
    }

    public enum SyncState { Off, Checking, Synced, Offline, Error, Conflict }

    /// <summary>Keeps the save on the account: every <see cref="Interval"/> seconds when the
    /// progress changed, and when the app goes to the background. The local file stays the source
    /// the game runs on, so losing the network loses nothing.
    ///
    /// Overwrites are guarded by the row's saved_at: an update only lands when the cloud still
    /// holds what this device last saw (<c>PATCH … &amp;saved_at=eq.known</c>). Anything else — a
    /// second phone on the same account — comes back as zero rows and turns into a choice instead
    /// of a silent loss.</summary>
    public class CloudSync : MonoBehaviour
    {
        public const float Interval = 45f;

        public static CloudSync I { get; private set; }
        public static SyncState State = SyncState.Off;
        public static string LastError = "";
        /// <summary>Unix ms of the last successful push or pull this session.</summary>
        public static long LastSyncUtc;
        /// <summary>This session has compared the file with the account and agreed a plan.</summary>
        public static bool Verified;

        GameApp _app;
        float _next;
        bool _busy;
        string _profileSent;
        float _pausedAt = -1f;
        bool _sessionLostShown;

        // ================================================================
        // per-account memory on this device
        // ================================================================
        public static string DeviceId
        {
            get
            {
                string id = PlayerPrefs.GetString("mitfarm.device", "");
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString("mitfarm.device", id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        static string Key(string uid, string what) => "mitfarm.cloud." + uid + "." + what;

        public static long KnownSavedAt(string uid)
        {
            long.TryParse(PlayerPrefs.GetString(Key(uid, "savedAt"), "0"), out long v);
            return v;
        }

        public static string KnownHash(string uid) => PlayerPrefs.GetString(Key(uid, "hash"), "");

        static void Remember(string uid, long savedAt, string hash)
        {
            PlayerPrefs.SetString(Key(uid, "savedAt"), savedAt.ToString());
            if (hash != null) PlayerPrefs.SetString(Key(uid, "hash"), hash);
            PlayerPrefs.Save();
        }

        // ================================================================
        // reconcile — at the start screen (before the farm loads) or in game once online
        // ================================================================
        public class Check
        {
            public SupaReply reply;
            public SyncPlan plan;
            public SaveSummary local, cloud;
            public string cloudJson;
            public bool Ok => reply.Ok;
        }

        /// <summary>Fetch the account's row and decide. <paramref name="localJson"/> is the file on
        /// disk before the game loads, or the running state's JSON in game.</summary>
        public static IEnumerator Reconcile(string localJson, Action<Check> done)
        {
            var c = new Check { local = SaveSummary.Of(localJson) };
            if (!Supa.SignedIn) { c.reply = new SupaReply { Status = 401, Code = "no_session" }; done(c); yield break; }

            State = SyncState.Checking;
            string uid = Supa.Session.userId;
            yield return Supa.SendAuthed("GET", "/rest/v1/saves?select=data,saved_at,device&user_id=eq." + uid, null, r => c.reply = r);
            if (!c.reply.Ok) { NoteFailure(c.reply); done(c); yield break; }

            var row = (c.reply.Json as JArray)?.Count > 0 ? (JObject)((JArray)c.reply.Json)[0] : null;
            if (row != null && row["data"] is JObject data)
            {
                c.cloudJson = data.ToString(Newtonsoft.Json.Formatting.None);
                c.cloud = SaveSummary.Of(c.cloudJson);
                c.cloud.savedAt = (long?)row["saved_at"] ?? 0;
                c.cloud.device = (string)row["device"] ?? "";
            }
            c.plan = CloudPlan.Decide(c.local, c.cloud, uid, DeviceId, KnownSavedAt(uid), KnownHash(uid));
            done(c);
        }

        /// <summary>Carry out a plan on the file, BEFORE the game loads it.</summary>
        public static void ApplyBeforeLoad(Check c, SyncPlan plan)
        {
            string uid = Supa.Session?.userId ?? "";
            switch (plan)
            {
                case SyncPlan.UseCloud:
                    SaveIO.Backup("truoc-dong-bo");
                    if (SaveIO.WriteRaw(c.cloudJson)) Remember(uid, c.cloud.savedAt, c.cloud.hash);
                    break;
                case SyncPlan.StartFresh:
                    SaveIO.Backup("tai-khoan-khac");
                    SaveIO.Delete();
                    Remember(uid, 0, "");
                    break;
                case SyncPlan.UploadLocal:
                    // the next push overwrites exactly the row just read (or inserts one)
                    Remember(uid, c.cloud.exists ? c.cloud.savedAt : 0, null);
                    break;
            }
            Verified = true;
            LastError = "";
        }

        static void NoteFailure(SupaReply r)
        {
            State = r.Network ? SyncState.Offline : SyncState.Error;
            LastError = r.Network ? "" : Supa.Friendly(r);
            if (!r.Network) Debug.LogWarning("[CloudSync] " + r.Status + " " + r.Code + " " + r.Message);
        }

        // ================================================================
        // running game
        // ================================================================
        public static CloudSync Attach(GameApp app)
        {
            var cs = app.gameObject.AddComponent<CloudSync>();
            cs._app = app;
            I = cs;
            // the file now belongs to the signed-in account
            if (Supa.SignedIn && Verified) GS.Local.ownerId = Supa.Session.userId;
            cs._next = Time.unscaledTime + (Verified ? 3f : 8f);
            Supa.SessionChanged += cs.OnSessionChanged;
            if (!Supa.SignedIn) State = SyncState.Off;
            return cs;
        }

        void OnDestroy()
        {
            Supa.SessionChanged -= OnSessionChanged;
            if (I == this) I = null;
        }

        void OnSessionChanged()
        {
            if (Supa.SignedIn || this == null) return;
            State = SyncState.Off;
            Verified = false;
            if (!_sessionLostShown && _app != null && !_app.LeavingForStart)
            {
                _sessionLostShown = true;
                _app.Toast("Phiên đăng nhập đã hết — vào Menu ▸ Tài khoản để đăng nhập lại");
            }
        }

        void Update()
        {
            if (_busy || !Supa.SignedIn || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Interval;
            Supa.Run(Verified ? Push(false) : ReconcileInGame());
        }

        void OnApplicationPause(bool paused)
        {
            if (!Supa.SignedIn) return;
            if (paused)
            {
                _pausedAt = Time.realtimeSinceStartup;
                // the OS may kill a backgrounded app without another callback: this is the push that counts
                if (Verified && !_busy && GS.Local.loaded) { GS.Save(); Supa.Run(Push(false)); }
            }
            else
            {
                // back after a while: another phone may have played this account meanwhile
                bool away = _pausedAt >= 0f && Time.realtimeSinceStartup - _pausedAt > 60f;
                _pausedAt = -1f;
                if (away && !_busy) Supa.Run(ReconcileInGame());
            }
        }

        void OnApplicationQuit()
        {
            if (Supa.SignedIn && Verified && !_busy && GS.Local.loaded) Supa.Run(Push(false));
        }

        /// <summary>Menu ▸ Tài khoản ▸ Đồng bộ ngay.</summary>
        public void SyncNow(Action<bool> done = null)
        {
            if (_busy || !Supa.SignedIn) { done?.Invoke(false); return; }
            _next = Time.unscaledTime + Interval;
            Supa.Run(SyncNowRoutine(done));
        }

        IEnumerator SyncNowRoutine(Action<bool> done)
        {
            if (!Verified) yield return ReconcileInGame();
            if (Verified) yield return Push(true);
            done?.Invoke(State == SyncState.Synced);
        }

        /// <summary>Push first, and let the caller continue once the account has the latest
        /// progress (or the network said no). Used before signing out.</summary>
        public IEnumerator Flush()
        {
            while (_busy) yield return null;
            if (Supa.SignedIn && Verified && GS.Local.loaded) yield return Push(false);
        }

        IEnumerator ReconcileInGame()
        {
            if (_busy || !GS.Local.loaded) yield break;
            _busy = true;
            Check c = null;
            yield return Reconcile(SaveIO.ToJson(GS.Local), r => c = r);
            _busy = false;
            if (c == null || !c.Ok || this == null) yield break;

            switch (c.plan)
            {
                case SyncPlan.UploadLocal:
                    Remember(Supa.Session.userId, c.cloud.exists ? c.cloud.savedAt : 0, null);
                    Verified = true;
                    GS.Local.ownerId = Supa.Session.userId;
                    yield return Push(true);
                    break;
                case SyncPlan.UseCloud:
                case SyncPlan.StartFresh:
                    _app.Toast(c.plan == SyncPlan.UseCloud ? "Đang tải tiến trình mới từ tài khoản…" : "Nông trại này thuộc tài khoản khác — bắt đầu nông trại mới");
                    yield return new WaitForSecondsRealtime(1.2f);
                    ReplaceAndRestart(c, c.plan);
                    break;
                case SyncPlan.AskPlayer:
                    State = SyncState.Conflict;
                    _app.Open(new SaveChoicePanel(_app, c, useCloud =>
                    {
                        if (useCloud) ReplaceAndRestart(c, SyncPlan.UseCloud);
                        else
                        {
                            Remember(Supa.Session.userId, c.cloud.savedAt, null);
                            Verified = true;
                            GS.Local.ownerId = Supa.Session.userId;
                            Supa.Run(Push(true));
                        }
                    }));
                    break;
            }
        }

        void ReplaceAndRestart(Check c, SyncPlan plan)
        {
            // Stop the running farm writing over the file it is about to be replaced by: the
            // autosave refuses a state that is not "loaded".
            GS.Local.loaded = false;
            ApplyBeforeLoad(c, plan);
            GameApp.Restart(false);
        }

        IEnumerator Push(bool force)
        {
            if (_busy || !Supa.SignedIn || !GS.Local.loaded) yield break;
            _busy = true;
            try
            {
                var s = GS.Local;
                string uid = Supa.Session.userId;
                s.ownerId = uid;
                string json = SaveIO.ToJson(s);
                string hash = CloudPlan.Fingerprint(json);
                long known = KnownSavedAt(uid);
                if (!force && known != 0 && hash == KnownHash(uid))
                {
                    State = SyncState.Synced;
                    yield return PushProfile(s);
                    yield break;
                }

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    long savedAt = Math.Max(GS.Now, known + 1);
                    string body = "{\"user_id\":\"" + uid + "\",\"level\":" + s.lv + ",\"coin\":" + s.coin +
                                  ",\"save_version\":" + SaveIO.Version + ",\"saved_at\":" + savedAt +
                                  ",\"device\":\"" + DeviceId + "\",\"data\":" + json + "}";
                    SupaReply r = default;
                    bool conflict;
                    State = SyncState.Checking;
                    if (known == 0)
                    {
                        yield return Supa.SendAuthed("POST", "/rest/v1/saves", body, x => r = x, "return=minimal");
                        conflict = r.Status == 409;
                    }
                    else
                    {
                        yield return Supa.SendAuthed("PATCH", "/rest/v1/saves?user_id=eq." + uid + "&saved_at=eq." + known + "&select=saved_at",
                                                     body, x => r = x, "return=representation");
                        conflict = r.Ok && (r.Json as JArray)?.Count == 0;
                    }

                    if (r.Ok && !conflict)
                    {
                        Remember(uid, savedAt, hash);
                        State = SyncState.Synced;
                        LastError = "";
                        LastSyncUtc = GS.Now;
                        yield return PushProfile(s);
                        yield break;
                    }
                    if (!conflict) { NoteFailure(r); yield break; }

                    // Somebody else's row, or our own write whose answer never arrived. Look.
                    SupaReply meta = default;
                    yield return Supa.SendAuthed("GET", "/rest/v1/saves?select=saved_at,device&user_id=eq." + uid, null, x => meta = x);
                    if (!meta.Ok) { NoteFailure(meta); yield break; }
                    var row = (meta.Json as JArray)?.Count > 0 ? (JObject)((JArray)meta.Json)[0] : null;
                    long cloudAt = (long?)row?["saved_at"] ?? 0;
                    string cloudDevice = (string)row?["device"] ?? "";
                    if (row == null) { known = 0; continue; }
                    if (cloudDevice == DeviceId) { known = cloudAt; continue; }

                    // a different device wrote: stop pushing and ask
                    Verified = false;
                    _busy = false;
                    yield return ReconcileInGame();
                    yield break;
                }
            }
            finally { _busy = false; }
        }

        IEnumerator PushProfile(PlayerState s)
        {
            string key = s.lv + "|" + s.displayName;
            if (key == _profileSent) yield break;
            var body = new JObject { ["id"] = Supa.Session.userId, ["display_name"] = s.displayName ?? "Nông dân", ["level"] = s.lv };
            SupaReply r = default;
            yield return Supa.SendAuthed("POST", "/rest/v1/profiles?on_conflict=id", body.ToString(Newtonsoft.Json.Formatting.None),
                                         x => r = x, "resolution=merge-duplicates,return=minimal");
            if (r.Ok) _profileSent = key;
        }
    }
}
