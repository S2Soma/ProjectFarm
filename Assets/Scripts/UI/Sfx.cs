using System.Collections.Generic;
using UnityEngine;

namespace LQFarm
{
    public enum SfxId
    {
        Tap, MenuOpen, MenuClose, PanelOpen, PanelClose, Tab,
        Harvest, Plant, Water, Coins, Error, Claim,
        LevelUp, IslandUnlock, Mutation, Legendary, ChestOpen, Plank, Toggle,
        Whoosh, Chime,
    }

    /// <summary>Sound effects, in Resources/Audio. All synthesised for this game: Tools/gen_sfx.py
    /// (a soft wooden-and-glass set — marimba, kalimba, warm bells, water, leaves — in A-flat major
    /// pentatonic, the key of the background music) and Tools/gen_cine_audio.py (whoosh, chime).
    ///
    /// Short, quiet and rate-limited. A farm game is played in long sessions, often next to
    /// something else, so the rule is: every sound is a confirmation of something the player just
    /// did, none is ambient, and the loud ones — the jingles — are kept for the three moments a
    /// session builds toward (a level, an island, a legendary).
    ///
    /// The loudness ladder (taps quietest … jingles loudest) is baked into the files by gen_sfx.py,
    /// so the volumes below are nearly uniform and only nudge. Change a sound's level there first.
    ///
    /// Six AudioSources round-robin, so a sweep's plucks overlap instead of cutting each other off,
    /// and each gets a small pitch jitter so a run of the same clip does not machine-gun — kept to
    /// ±1.2 % on tonal clips so they stay in key with the music; noisy ones (water, soil, wood)
    /// wander more. The big jingles duck the <see cref="Music"/> while they ring.
    /// On/off is a device preference (PlayerPrefs), not part of the save: it belongs to the phone
    /// in the player's hand, not to the farm.</summary>
    public static class Sfx
    {
        const string PrefKey = "lq.sfx";
        const int Voices = 6;

        const float Tonal = 0.012f;   // ±20 cents: still in key
        const float Noisy = 0.04f;

        /// <summary>clips (one picked at random), volume, minimum gap between plays (s), pitch jitter
        /// (± fraction), and how far to duck the music while the clip plays (0 = not at all).</summary>
        static readonly Dictionary<SfxId, (string[] clips, float volume, float gap, float jitter, float duck)> Table =
            new Dictionary<SfxId, (string[], float, float, float, float)>
        {
            { SfxId.Tap,          (new[] { "tap" }, 0.90f, 0.05f, Tonal, 0f) },
            { SfxId.MenuOpen,     (new[] { "menu_open" }, 0.90f, 0.1f, Tonal, 0f) },
            { SfxId.MenuClose,    (new[] { "menu_close" }, 0.90f, 0.1f, Tonal, 0f) },
            { SfxId.PanelOpen,    (new[] { "panel_open" }, 0.90f, 0.1f, Tonal, 0f) },
            { SfxId.PanelClose,   (new[] { "panel_close" }, 0.90f, 0.1f, Tonal, 0f) },
            { SfxId.Tab,          (new[] { "tab" }, 0.90f, 0.05f, Tonal, 0f) },
            // harvest_0/1 are two notes of the scale (C, Eb): a sweep picks between them, a little tune
            { SfxId.Harvest,      (new[] { "harvest_0", "harvest_1" }, 0.90f, 0.035f, Tonal, 0f) },
            { SfxId.Plant,        (new[] { "plant_0", "plant_1" }, 0.90f, 0.05f, Noisy, 0f) },
            { SfxId.Water,        (new[] { "water" }, 0.90f, 0.06f, Noisy, 0f) },
            { SfxId.Coins,        (new[] { "coins" }, 0.90f, 0.15f, Tonal, 0f) },
            { SfxId.Error,        (new[] { "error" }, 0.85f, 0.25f, Tonal, 0f) },
            { SfxId.Claim,        (new[] { "claim" }, 0.90f, 0.2f, Tonal, 0f) },
            { SfxId.LevelUp,      (new[] { "level_up" }, 0.90f, 0.5f, 0f, 0.5f) },
            { SfxId.IslandUnlock, (new[] { "island_unlock" }, 0.90f, 0.5f, 0f, 0.55f) },
            { SfxId.Mutation,     (new[] { "mutation" }, 0.90f, 0.1f, Tonal, 0f) },
            // legendary's peak caps it ~2 dB under its rung of the ladder; the volume gives that back
            { SfxId.Legendary,    (new[] { "legendary" }, 1.00f, 0.5f, 0f, 0.55f) },
            { SfxId.ChestOpen,    (new[] { "chest_open" }, 0.90f, 0.2f, Tonal, 0f) },
            // a run of knocks while a bridge is laid: under the other effects
            { SfxId.Plank,        (new[] { "plank_0", "plank_1", "plank_2" }, 0.80f, 0.06f, Noisy, 0f) },
            { SfxId.Toggle,       (new[] { "toggle" }, 0.90f, 0.1f, Tonal, 0f) },
            // the flight from the start screen into the farm (EnterCinematic); Tools/gen_cine_audio.py
            { SfxId.Whoosh,       (new[] { "cine_whoosh" }, 0.60f, 1.0f, 0.03f, 0.3f) },
            { SfxId.Chime,        (new[] { "cine_chime" }, 0.60f, 1.0f, 0f, 0.2f) },
        };

        static AudioSource[] _voices;
        static int _next;
        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        static readonly Dictionary<SfxId, float> _last = new Dictionary<SfxId, float>();
        static int _enabled = -1;

        public static bool Enabled
        {
            get
            {
                if (_enabled < 0) _enabled = PlayerPrefs.GetInt(PrefKey, 1);
                return _enabled == 1;
            }
            set
            {
                _enabled = value ? 1 : 0;
                PlayerPrefs.SetInt(PrefKey, _enabled);
                PlayerPrefs.Save();
            }
        }

        static void Ensure()
        {
            if (_voices != null && _voices[0] != null) return;
            var go = new GameObject("~Sfx");
            Object.DontDestroyOnLoad(go);
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                _voices[i] = s;
            }
        }

        static AudioClip Clip(string name)
        {
            if (!_clips.TryGetValue(name, out var c) || c == null) _clips[name] = c = Resources.Load<AudioClip>("Audio/" + name);
            return c;
        }

        public static void Play(SfxId id, float volumeScale = 1f)
        {
            if (!Enabled || !Application.isPlaying) return;
            if (!Table.TryGetValue(id, out var def)) return;
            float now = Time.unscaledTime;
            if (_last.TryGetValue(id, out float last) && now - last < def.gap) return;
            _last[id] = now;

            var clip = Clip(def.clips[Random.Range(0, def.clips.Length)]);
            if (clip == null) return;
            Ensure();
            var v = _voices[_next];
            _next = (_next + 1) % Voices;
            // jingles keep their pitch; short effects wander a little so repeats do not sound pasted
            v.pitch = def.jitter > 0f ? 1f + Random.Range(-def.jitter, def.jitter) : 1f;
            v.PlayOneShot(clip, def.volume * volumeScale);
            if (def.duck > 0f) Music.Duck(def.duck, clip.length * 0.85f);
        }

        /// <summary>Failures are announced by toasts all over the game; rather than touch every call
        /// site, a toast that starts like a refusal gets the error blip.</summary>
        public static bool LooksLikeRefusal(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            string[] starts = { "Không đủ", "Chưa", "Bạn chưa", "Hết lượt", "Kho trống", "Kho chưa", "Bạn đã mua", "Hôm nay bạn đã", "Mở bán ở cấp", "Mã không", "Mã này đã", "Hãy nhập mã" };
            foreach (var s in starts) if (message.StartsWith(s)) return true;
            return false;
        }
    }
}
