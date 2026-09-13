using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Scripting;

namespace LQFarm
{
    /// <summary>Reading and writing the save file.
    ///
    /// Uses Newtonsoft rather than <c>JsonUtility</c> for one disqualifying reason: JsonUtility
    /// silently DROPS any JSON key it has no field for. <c>FromJson</c> ignores unknown keys and
    /// <c>ToJson</c> writes only declared ones, so the first time an older build opened a save
    /// written by a newer build — or by a server — it would delete everything it did not
    /// recognise. Every DTO below therefore carries <see cref="JsonExtensionData"/>, which
    /// round-trips unknown keys untouched.
    ///
    /// Three secondary reasons: JsonUtility has no dictionaries (the old parallel seedK/seedV
    /// lists were the ugliest code in the project, and their reader silently truncated on a
    /// length mismatch instead of failing), it cannot tell null from "", and it writes enums as
    /// integers — so reordering an enum would silently reinterpret every existing save.
    ///
    /// Compatibility rules, enforced by review:
    ///   - every DTO keeps an extension-data bag
    ///   - collections are keyed by id, never positional (islands carry their own id)
    ///   - timestamps are int64 unix ms UTC — never DateTime, never float
    ///   - enums serialise as strings
    ///   - fields are never removed, only ignored; renames get [JsonProperty("oldName")]
    ///   - a reader older than minV refuses the file and renames it rather than overwriting</summary>
    public static class SaveIO
    {
        public const int Version = 2;

        /// <summary>Oldest reader that may open a Version-2 file. Raise this only for a change
        /// that genuinely cannot be read by older code; everything else is additive.</summary>
        public const int MinVersion = 2;

        static string Path => System.IO.Path.Combine(Application.persistentDataPath, "lqfarm.save.json");

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            // Explicit: a missing number must stay 0, never be guessed from a field initialiser.
            DefaultValueHandling = DefaultValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = { new StringEnumConverter() },
            Formatting = Formatting.None,
        };

        // ================================================================
        // write
        // ================================================================
        /// <summary>Serialise without touching the filesystem. Exists so the round-trip can be
        /// tested against a string instead of the player's real save.</summary>
        public static string ToJson(PlayerState s)
        {
            return JsonConvert.SerializeObject(ToDto(s), Settings);
        }

        public static void Save(PlayerState s)
        {
            try
            {
                s.clock.savedAtUtc = GS.Now;
                s.clock.savedAtMono = Time.realtimeSinceStartup;
                string json = ToJson(s);
                // Write beside the target and move into place: a process killed mid-write leaves
                // the previous save intact rather than a truncated file that loads as a new game.
                string tmp = Path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(Path)) File.Delete(Path);
                File.Move(tmp, Path);
            }
            catch (Exception e) { Debug.LogWarning("Không lưu được tiến trình: " + e.Message); }
        }

        // ================================================================
        // read
        // ================================================================
        public enum LoadResult { NewGame, Loaded, RefusedTooNew, Corrupt }

        public static LoadResult Load(PlayerState s)
        {
            string raw = null;
            try { if (File.Exists(Path)) raw = File.ReadAllText(Path); }
            catch (Exception e) { Debug.LogWarning("Không đọc được file lưu: " + e.Message); }

            var result = FromJson(s, raw, out string keepReason);
            if (keepReason != null) Quarantine(raw, keepReason);
            return result;
        }

        /// <summary>Deserialise without touching the filesystem.
        ///
        /// <paramref name="quarantineTag"/> comes back non-null when the caller should move the
        /// file aside rather than let the next save overwrite it: the player's progress is in
        /// there and this reader is not the one that should destroy it.</summary>
        public static LoadResult FromJson(PlayerState s, string raw, out string quarantineTag)
        {
            quarantineTag = null;

            if (string.IsNullOrEmpty(raw)) { s.NewGame(); return LoadResult.NewGame; }

            // Read the version BEFORE trusting the body's shape.
            //
            // Deserialising straight into SaveDto looks equivalent and is not: a field whose type
            // changed between schemas throws, and the version check then never runs. That is not
            // hypothetical — v1 wrote `chests` as an int array where v2 writes an object, so every
            // real v1 file was being reported as corrupt rather than as an old version. A JObject
            // parse only fails on JSON that is genuinely malformed.
            JObject doc = null;
            try { doc = JObject.Parse(raw); }
            catch (Exception e) { Debug.LogWarning("File lưu hỏng: " + e.Message); }

            if (doc == null)
            {
                quarantineTag = "hong";
                s.NewGame();
                return LoadResult.Corrupt;
            }

            // "version" is v1's spelling of the same field. Absent entirely means a schema older
            // than any version key, which is also v1 territory.
            int v = (int?)doc["v"] ?? (int?)doc["version"] ?? 0;
            int minV = (int?)doc["minV"] ?? 0;

            // Written by a newer build that declares this reader too old. Never overwrite it —
            // the real progress is in there and a future build can still read it.
            if (Version < minV)
            {
                Debug.LogWarning($"File lưu cần phiên bản {minV}, bản này là {Version}. Đã giữ lại bản cũ.");
                quarantineTag = "v" + v;
                s.NewGame();
                return LoadResult.RefusedTooNew;
            }

            // v1 was the JsonUtility schema. The owner chose a clean reset for the redesign, so
            // there is deliberately no v1 reader: the file is kept, not migrated.
            if (v < Version)
            {
                Debug.Log($"Bỏ qua file lưu v{v} (redesign đã reset). Đã giữ lại bản cũ.");
                quarantineTag = "v" + v;
                s.NewGame();
                return LoadResult.NewGame;
            }

            SaveDto d = null;
            try { d = doc.ToObject<SaveDto>(JsonSerializer.Create(Settings)); }
            catch (Exception e) { Debug.LogWarning("File lưu đúng phiên bản nhưng sai cấu trúc: " + e.Message); }

            if (d == null)
            {
                quarantineTag = "hong";
                s.NewGame();
                return LoadResult.Corrupt;
            }

            FromDto(s, d);
            return LoadResult.Loaded;
        }

        /// <summary>Move a save aside instead of destroying it. Never throws.</summary>
        static void Quarantine(string raw, string tag)
        {
            try
            {
                string bak = Path + "." + tag + ".bak";
                File.WriteAllText(bak, raw);
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch (Exception) { /* the save is already unusable; losing the backup changes nothing */ }
        }

        public static void Delete()
        {
            try { if (File.Exists(Path)) File.Delete(Path); } catch (Exception) { }
        }

        // ================================================================
        // mapping
        // ================================================================
        /// <summary>Fill the DTO the state was loaded from, rather than a fresh one.
        ///
        /// This is what actually makes unknown-key preservation work, and it is easy to get
        /// wrong: <see cref="JsonExtensionData"/> only round-trips while the DTO INSTANCE that
        /// read those keys is still alive. Mapping into a domain object and then serialising a
        /// brand-new DTO would throw the bags away and silently reproduce the exact JsonUtility
        /// defect this serialiser was chosen to avoid.
        ///
        /// The cost is that every known field must be assigned here unconditionally — anything
        /// missed keeps a stale value from the previous load instead of being recomputed.</summary>
        static SaveDto ToDto(PlayerState s)
        {
            var d = s.loadedEcho ?? (s.loadedEcho = new SaveDto());

            d.v = Version;
            d.minV = MinVersion;
            d.worldSeed = s.worldSeed.ToString("x16", CultureInfo.InvariantCulture);
            d.clock = s.clock;
            d.stats = s.stats;

            var p = d.player ?? (d.player = new PlayerDto());
            p.ownerId = s.ownerId; p.displayName = s.displayName;
            p.level = s.lv; p.xp = s.xp; p.coin = s.coin; p.energy = s.energy;

            var inv = d.inventory ?? (d.inventory = new InventoryDto());
            inv.seeds = s.seeds; inv.produce = s.store;

            var ch = d.chests ?? (d.chests = new ChestDto());
            ch.owned = s.chests;

            var pr = d.progress ?? (d.progress = new ProgressDto());
            pr.chapter = s.missions;
            pr.daily = s.daily;
            pr.dailyBucket = s.day;
            pr.buffMutateUntil = s.buffMutateUntil;
            pr.forecastUntil = s.forecastUntil;
            pr.greenhouse = s.greenhouse;
            pr.contracts = s.contracts;
            pr.streak = s.streak;
            pr.sinceDiamond = s.sinceDiamond;
            pr.sinceLegendary = s.sinceLegendary;
            pr.contractsToday = s.contractsToday;
            pr.collected = new List<string>(s.collected);
            pr.claimedSets = new List<string>(s.claimedSets);
            pr.claimedMilestones = new List<int>(s.claimedMs);

            var so = d.social ?? (d.social = new SocialDto());
            so.visited = new List<string>(s.visited);
            so.shopBought = new List<string>(s.shopBought);
            so.stealBudgetLeft = s.stealLeft;

            var meta = d.meta ?? (d.meta = new MetaDto());
            meta.build = Application.version;
            meta.platform = Application.platform.ToString();

            // Islands are rebuilt wholesale: an island removed from the state must disappear
            // from the file, which reusing entries in place would not achieve. Their own
            // extension bags are carried over by index where one already existed.
            var old = d.islands;
            d.islands = new List<IslandDto>(s.islands.Count);
            for (int i = 0; i < s.islands.Count; i++)
            {
                var isl = s.islands[i];
                var dto = (old != null && i < old.Count) ? old[i] : new IslandDto();
                dto.id = isl.id; dto.unlocked = isl.unlocked; dto.unlockedAt = isl.unlockedAt;
                dto.plots = isl.plots;
                dto.tribute = isl.tribute != null && isl.tribute.Count > 0 ? isl.tribute : null;
                d.islands.Add(dto);
            }
            return d;
        }

        static void FromDto(PlayerState s, SaveDto d)
        {
            // Keep the document itself: its extension-data bags are the only copy of any
            // key a newer build wrote, and ToDto writes back through this same instance.
            s.loadedEcho = d;

            s.worldSeed = ParseSeed(d.worldSeed);

            if (d.clock != null)
            {
                s.clock.lastSeenUtc = d.clock.lastSeenUtc;
                s.clock.savedAtUtc = d.clock.savedAtUtc;
                s.clock.savedAtMono = d.clock.savedAtMono;
                s.clock.resetOffsetMinutes = d.clock.resetOffsetMinutes;
                s.clock.clockSuspect = d.clock.clockSuspect;
            }

            var p = d.player ?? new PlayerDto();
            s.ownerId = p.ownerId ?? "";
            s.displayName = string.IsNullOrEmpty(p.displayName) ? "Nông dân" : p.displayName;
            s.lv = Mathf.Max(1, p.level);
            s.xp = p.xp; s.coin = p.coin; s.energy = p.energy;

            s.islands.Clear();
            if (d.islands != null)
                foreach (var isl in d.islands)
                {
                    var target = new Island(isl.id, isl.unlocked) { unlockedAt = isl.unlockedAt };
                    if (isl.plots != null) target.plots.AddRange(isl.plots);
                    if (isl.tribute != null && isl.tribute.Count > 0)
                        target.tribute = new Dictionary<string, int>(isl.tribute);
                    s.islands.Add(target);
                }
            if (s.islands.Count == 0) s.islands.Add(new Island(0, true));

            var inv = d.inventory ?? new InventoryDto();
            Replace(s.seeds, inv.seeds);
            Replace(s.store, inv.produce);

            var ch = d.chests ?? new ChestDto();
            s.chests = (ch.owned != null && ch.owned.Length == 4) ? ch.owned : new int[4];

            var pr = d.progress ?? new ProgressDto();
            Replace(s.missions, pr.chapter);
            Replace(s.daily, pr.daily);
            s.day = pr.dailyBucket;
            s.buffMutateUntil = pr.buffMutateUntil;
            s.forecastUntil = pr.forecastUntil;
            s.greenhouse = pr.greenhouse;
            s.contracts.Clear();
            if (pr.contracts != null) s.contracts.AddRange(pr.contracts);
            s.streak = pr.streak;
            s.sinceDiamond = pr.sinceDiamond;
            s.sinceLegendary = pr.sinceLegendary;
            s.contractsToday = pr.contractsToday;
            Replace(s.collected, pr.collected);
            Replace(s.claimedSets, pr.claimedSets);
            Replace(s.claimedMs, pr.claimedMilestones);

            var so = d.social ?? new SocialDto();
            Replace(s.visited, so.visited);
            Replace(s.shopBought, so.shopBought);
            s.stealLeft = so.stealBudgetLeft;

            s.stats = d.stats ?? new Stats();
        }

        static long ParseSeed(string hex)
        {
            if (!string.IsNullOrEmpty(hex) &&
                long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long v) && v != 0)
                return v;
            return 0;   // PlayerState.NewGame/EnsureSeed assigns a fresh one
        }

        static void Replace<TK, TV>(Dictionary<TK, TV> target, Dictionary<TK, TV> src)
        {
            target.Clear();
            if (src == null) return;
            foreach (var kv in src) target[kv.Key] = kv.Value;
        }

        static void Replace<T>(HashSet<T> target, List<T> src)
        {
            target.Clear();
            if (src == null) return;
            foreach (var v in src) target.Add(v);
        }
    }

    // ================================================================
    // DTOs — the on-disk shape.
    //
    // [Preserve] is load-bearing on Android, which builds with IL2CPP and managed stripping on.
    // Nothing in the game ever reads these fields by name, so the linker would happily remove
    // them and the failure would appear only on device, as a save that silently loses data.
    // Newtonsoft itself is covered by the link.xml that ships inside its own package.
    // ================================================================
    [Preserve]
    public class SaveDto
    {
        /// <summary>Both default to 0, NOT to the current version.
        ///
        /// Newtonsoft leaves a field at its initialiser when the JSON key is absent, and the v1
        /// schema spelled this key "version". Initialising these to the current version would
        /// therefore make every v1 file read as a valid v2 file with no islands and no
        /// inventory — the loader would accept it and wipe the player's farm. 0 means
        /// "not a version this reader knows", which routes to quarantine.</summary>
        public int v;
        public int minV;
        public string worldSeed;
        public Clock clock;
        public PlayerDto player;
        public List<IslandDto> islands = new List<IslandDto>();
        public InventoryDto inventory;
        public ChestDto chests;
        public ProgressDto progress;
        public SocialDto social;
        public Stats stats;
        public MetaDto meta;

        [JsonExtensionData] public IDictionary<string, JToken> _x;
    }

    [Preserve]
    public class PlayerDto
    {
        public string ownerId = "";
        public string displayName;
        public int level = 1;
        public long xp;
        public int coin;
        public long energy;
        [JsonExtensionData] public IDictionary<string, JToken> _x;
    }

    [Preserve]
    public class IslandDto
    {
        public int id;
        public bool unlocked;
        public long unlockedAt;
        public List<Plot> plots = new List<Plot>();
        public Dictionary<string, int> tribute;
        [JsonExtensionData] public IDictionary<string, JToken> _x;
    }

    [Preserve]
    public class InventoryDto
    {
        public Dictionary<string, int> seeds = new Dictionary<string, int>();
        public Dictionary<string, int> produce = new Dictionary<string, int>();
        [JsonExtensionData] public IDictionary<string, JToken> _x;
    }

    [Preserve]
    public class ChestDto
    {
        /// <summary>Unopened chests per tier. The magic-energy bar that fills them lives on
        /// <see cref="PlayerDto.energy"/> and is deliberately not mirrored here — two fields
        /// holding one number is how saves start disagreeing with themselves.</summary>
        public int[] owned = new int[4];
        [JsonExtensionData] public IDictionary<string, JToken> _x;
    }

    [Preserve]
    public class ProgressDto
    {
        public Dictionary<string, TaskRec> chapter = new Dictionary<string, TaskRec>();
        public Dictionary<string, TaskRec> daily = new Dictionary<string, TaskRec>();
        public long dailyBucket;
        public long buffMutateUntil;
        public long forecastUntil;
        public int greenhouse;
        public List<MissionRec> contracts = new List<MissionRec>();
        public int streak;
        public int sinceDiamond;
        public int sinceLegendary;
        public int contractsToday;
        public List<string> collected = new List<string>();
        public List<string> claimedSets = new List<string>();
        public List<int> claimedMilestones = new List<int>();
        [JsonExtensionData] public IDictionary<string, JToken> _x;
    }

    [Preserve]
    public class SocialDto
    {
        public List<string> visited = new List<string>();
        public List<string> shopBought = new List<string>();
        public int stealBudgetLeft = 20;
        [JsonExtensionData] public IDictionary<string, JToken> _x;
    }

    [Preserve]
    public class MetaDto
    {
        public string build;
        public string platform;
        [JsonExtensionData] public IDictionary<string, JToken> _x;
    }
}
