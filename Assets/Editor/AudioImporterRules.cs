using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    /// <summary>Import rules for the game's audio, so nobody has to set them by hand (and nobody's
    /// Inspector click quietly undoes them — like FarmTextureImporter, this runs on every import).
    ///
    ///   Resources/Audio/Music/  the background loop (Tools/make_music.py, UI/Music.cs): Streaming,
    ///                           Vorbis q 0.5, not preloaded. Minutes of stereo stay on disk and are
    ///                           decoded as they play, instead of ~50 MB of PCM in memory.
    ///   Resources/Audio/        the effects (Tools/gen_sfx.py, gen_cine_audio.py): Decompress On
    ///                           Load, Vorbis q 0.7, preloaded. Short and frequent, so they play with
    ///                           no decode cost and no first-play hitch; Vorbis rather than ADPCM
    ///                           because ADPCM's hiss shows on soft, sine-like tails.
    ///
    /// Normalize is always OFF. The effects carry a deliberate loudness ladder (taps quiet, jingles
    /// loud) and the music is mastered to -19 LUFS; the importer's peak normalisation would flatten
    /// both. Normalize only acts while Force To Mono mixes down, so Force To Mono stays off too — the
    /// generators already write the effects in mono, and a stereo effect is reported instead.
    /// Platform overrides are cleared so these settings are the ones that ship.</summary>
    public class AudioImporterRules : AssetPostprocessor
    {
        const string Root = "Assets/Resources/Audio/";
        const string MusicDir = Root + "Music/";

        public override uint GetVersion() { return 1; }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var im = (AudioImporter)assetImporter;
            bool music = assetPath.StartsWith(MusicDir);

            var s = im.defaultSampleSettings;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            if (music)
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.quality = 0.5f;
                s.preloadAudioData = false;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.quality = 0.7f;
                s.preloadAudioData = true;
            }
            im.defaultSampleSettings = s;
            im.forceToMono = false;
            im.loadInBackground = false;             // streaming does not stall; effects are tiny
            im.ambisonic = false;
            foreach (var platform in new[] { "Android", "iOS", "Standalone" })
                if (im.ContainsSampleSettingsOverride(platform)) im.ClearSampleSettingOverride(platform);

            // not exposed as a property; it is the "normalize" field of the importer
            var so = new SerializedObject(im);
            var normalize = so.FindProperty("normalize");
            if (normalize != null && normalize.boolValue)
            {
                normalize.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        void OnPostprocessAudio(AudioClip clip)
        {
            if (!assetPath.StartsWith(Root) || assetPath.StartsWith(MusicDir)) return;
            if (clip.channels > 1)
                Debug.LogWarning($"[Audio] {assetPath} is {clip.channels}-channel; effects are meant to be mono " +
                                 "(Tools/gen_sfx.py writes mono). It plays, at double the memory.");
        }
    }
}
