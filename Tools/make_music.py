"""Background music: the owner's Terrace_in_the_Clouds.mp3 -> a seamless, quieter Ogg loop.

    uv run --with numpy --with scipy --with soundfile --with pyloudnorm python Tools/make_music.py

  in   Terrace_in_the_Clouds.mp3 (repo root, supplied by the owner; left untouched)
  out  Assets/Resources/Audio/Music/terrace_in_the_clouds.ogg   (played by UI/Music.cs)

What the source is (measured 2026-09-15): MP3 192 kbps CBR, 44.1 kHz stereo, 158.93 s, 3.8 MB.
Integrated loudness about -12.2 LUFS, decoded sample peak +0.46 dBFS (69 samples over full scale:
inter-sample overs of a master limited to 0 dBFS, not audible clipping). A-flat major, a steady
144.00 BPM grid from the first note (a half-time, 72 BPM feel): a plucked 11 s intro, a full section,
a breakdown at 66-93 s, a second full section, and a real ending - the last chord (Db maj9) lands on
beat 360 (150.06 s) and rings out to silence, leaving ~6 s of reverb tail at -40 dB and below.
Played as it is, every loop would stop for several seconds.

The loop: the file is cut at exactly 364 beats (364 x 18375 samples = 151.667 s), one bar after the
last chord, where the chord has decayed to about -27 dB; the intro's first note falls on the next
beat of the same grid. The rest of the ring-out (7.3 s, never above -20 dBFS peak) is not thrown
away but mixed over the head of the file, so when the player wraps, the last chord keeps ringing
under the intro's first bar, as it would if the band played the song again. The last chord and the
intro's first are both a Db major-seventh colour (Db Ab C Eb / Db F Ab C), so the overlap is
consonant. No crossfade dips the intro, and nothing is time-stretched. The far end of the ring-out
gets a 2 s raised-cosine fade so the noise floor ends at zero. On the wrap the level dips to about
-38 dBFS for a few tenths of a second before the intro's downbeat: a breath, not a gap.

Level: -19 LUFS integrated (gain -6.8 dB), so the music sits under the effects (Tools/gen_sfx.py) at
Music.Volume; the peak lands near -6 dBFS, far under the -1 dBFS ceiling. Stereo, 44.1 kHz, Vorbis q 0.5.

Provenance (from the file itself): no title/artist/album/comment tags; one ID3 GEOB frame holding a
C2PA manifest signed by Google ("Google C2PA Core Generator Library") whose actions read
"Created by Google Generative AI" (digitalSourceType trainedAlgorithmicMedia) and "Applied
imperceptible SynthID watermark", time-stamped 2026-09-15 07:03 UTC. Generated with a Google AI music
tool; the licence terms of that tool for commercial use are the owner's to confirm before release.
"""
import os

import numpy as np
import pyloudnorm as pyln
import soundfile as sf
from scipy import signal

SRC = "Terrace_in_the_Clouds.mp3"
OUT = os.environ.get("MUSIC_OUT", "Assets/Resources/Audio/Music/terrace_in_the_clouds.ogg")
SR = 44100
BEAT = 18375                 # samples per beat at exactly 144 BPM
LOOP_BEATS = 364             # the last chord is on beat 360; the loop restarts one bar later
TARGET_LUFS = -19.0
CEILING_DBFS = -1.0
TAIL_FADE = 2.0


def db(v):
    return 20 * np.log10(max(v, 1e-12))


def true_peak(x):
    return np.max(np.abs(signal.resample_poly(x, 4, 1, axis=0)))


def main():
    x, sr = sf.read(SRC, always_2d=True)
    assert sr == SR, sr
    n_loop = LOOP_BEATS * BEAT
    assert len(x) > n_loop, (len(x), n_loop)

    y = x[:n_loop].copy()
    tail = x[n_loop:].copy()
    k = min(len(tail), int(TAIL_FADE * SR))
    tail[-k:] *= (0.5 + 0.5 * np.cos(np.pi * np.arange(k) / k))[:, None]
    assert len(tail) < len(y)
    y[:len(tail)] += tail

    meter = pyln.Meter(SR)
    gain = TARGET_LUFS - meter.integrated_loudness(y)
    y *= 10 ** (gain / 20)
    tp = true_peak(y)
    if db(tp) > CEILING_DBFS:
        y *= 10 ** (CEILING_DBFS / 20) / tp

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    # in blocks: libsndfile's Vorbis writer crashes when handed minutes of audio in one call
    with sf.SoundFile(OUT, "w", SR, 2, format="OGG", subtype="VORBIS", compression_level=0.5) as f:
        for i in range(0, len(y), SR):
            f.write(y[i:i + SR].astype(np.float32))

    back, _ = sf.read(OUT, always_2d=True)
    seam = np.concatenate([back[-SR:], back[:SR]])       # what the player hears at the wrap
    step = np.abs(np.diff(seam, axis=0)).max(axis=1)
    print(f"   {OUT}")
    print(f"   {len(back) / SR:.3f} s ({len(back)} samples, loop {n_loop}), gain {gain:+.1f} dB")
    print(f"   {meter.integrated_loudness(back):.1f} LUFS, sample peak {db(np.abs(back).max()):.1f} dBFS, "
          f"true peak ~{db(true_peak(back)):.1f} dBFS, {os.path.getsize(OUT) / 1e6:.2f} MB")
    print(f"   seam: largest sample step {step[SR - 2:SR + 2].max():.4f} "
          f"(elsewhere around it {np.percentile(step, 99.9):.4f})")


if __name__ == "__main__":
    main()
