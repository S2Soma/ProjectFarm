using UnityEngine;

namespace LQFarm
{
    /// <summary>Background music: one looping track, Resources/Audio/Music/terrace_in_the_clouds
    /// (made seamless and levelled by Tools/make_music.py), on its own AudioSource — never one of
    /// <see cref="Sfx"/>'s voices, so a burst of effects cannot steal it.
    ///
    /// It boots itself after the scene loads (the scene's camera carries the AudioListener), so it
    /// is already fading in under the start screen, and it lives on a DontDestroyOnLoad host that
    /// GameApp.Restart does not touch — signing out does not restart the song.
    ///
    /// On/off is a device preference like <see cref="Sfx.Enabled"/> (PlayerPrefs
    /// <c>mitfarm.music</c>, default on); switching fades rather than cuts, and a source faded to
    /// silence is paused so nothing is decoded for nobody. <see cref="Duck"/> lowers it for a moment
    /// under a big jingle (Sfx calls it). Nothing here allocates per frame.</summary>
    public sealed class Music : MonoBehaviour
    {
        const string PrefKey = "mitfarm.music";
        const string ClipPath = "Audio/Music/terrace_in_the_clouds";

        /// <summary>Playing volume at full fade. The file is mastered to -19 LUFS; here it sits a few
        /// dB under the effects of Tools/gen_sfx.py.</summary>
        public const float Volume = 0.4f;

        const float BootFade = 2.0f;
        const float ToggleFade = 0.8f;
        const float ResumeFade = 0.6f;
        const float DuckAttack = 0.12f;
        const float DuckRelease = 0.9f;

        static Music _i;
        static int _enabled = -1;

        AudioSource _src;
        /// <summary>0..1 position of the fade; the volume follows its square (an even-sounding fade).</summary>
        float _fade;
        float _fadeTime = BootFade;
        float _duck, _duckTarget, _duckUntil;
        bool _backgrounded;
        bool _running;
        float _lastVolume = -1f;

        /// <summary>Music on/off for this device. Setting it fades the music in or out and saves it.</summary>
        public static bool Enabled
        {
            get
            {
                if (_enabled < 0) _enabled = PlayerPrefs.GetInt(PrefKey, 1);
                return _enabled == 1;
            }
            set
            {
                int v = value ? 1 : 0;
                if (_enabled == v) return;
                _enabled = v;
                PlayerPrefs.SetInt(PrefKey, v);
                PlayerPrefs.Save();
                if (_i != null) _i._fadeTime = ToggleFade;
            }
        }

        /// <summary>Lower the music by <paramref name="amount"/> (0..1 of its volume) for
        /// <paramref name="seconds"/>, then let it come back. Overlapping ducks keep the deeper amount
        /// and the later end.</summary>
        public static void Duck(float amount, float seconds)
        {
            if (_i == null) return;
            float now = Time.unscaledTime;
            amount = Mathf.Clamp01(amount);
            _i._duckTarget = now >= _i._duckUntil ? amount : Mathf.Max(_i._duckTarget, amount);
            _i._duckUntil = Mathf.Max(_i._duckUntil, now + Mathf.Max(0f, seconds));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _i = null;
            _enabled = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (_i != null) return;
            var clip = Resources.Load<AudioClip>(ClipPath);
            if (clip == null) return;                 // a build without music: stay silent
            var go = new GameObject("~Music");
            DontDestroyOnLoad(go);
            _i = go.AddComponent<Music>();
            _i.Begin(clip);
        }

        void Begin(AudioClip clip)
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.clip = clip;
            _src.loop = true;
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            _src.priority = 0;                        // the one sound that is never virtualised
            _src.dopplerLevel = 0f;
            _src.bypassReverbZones = true;
            _src.volume = 0f;
            _fade = 0f;
            _fadeTime = BootFade;
            if (Enabled) Run(true);
        }

        void Run(bool on)
        {
            if (on == _running || _src == null) return;
            _running = on;
            if (!on) _src.Pause();
            else if (_src.time > 0f) _src.UnPause();
            else _src.Play();
        }

        void Update()
        {
            if (_src == null) return;
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;

            bool want = Enabled && !_backgrounded;
            if (want) Run(true);
            _fade = Mathf.MoveTowards(_fade, want ? 1f : 0f, dt / Mathf.Max(0.01f, _fadeTime));

            if (now >= _duckUntil) _duckTarget = 0f;
            float duckTime = _duckTarget > _duck ? DuckAttack : DuckRelease;
            _duck = Mathf.MoveTowards(_duck, _duckTarget, dt / duckTime);

            float v = Volume * _fade * _fade * (1f - _duck);
            if (v != _lastVolume)
            {
                _src.volume = v;
                _lastVolume = v;
            }
            if (!want && _fade <= 0f) Run(false);
        }

        void OnApplicationPause(bool paused)
        {
            if (_src == null) return;
            if (paused)
            {
                // no frames will run until we are back: stop now rather than fade
                _backgrounded = true;
                Run(false);
                _fade = 0f;
                _lastVolume = -1f;
            }
            else if (_backgrounded)
            {
                // (some platforms also send a pause(false) at launch: that one must not cut the boot fade)
                _backgrounded = false;
                _fadeTime = ResumeFade;
            }
        }
    }
}
