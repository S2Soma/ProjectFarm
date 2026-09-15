"""The synthesised UI and farm sound effects: no samples, so no licence to track.

Since 2026-09-15 (second round) the sounds that answer a touch — tap, tab, toggle, menu_open/close,
panel_open/close, harvest_0/1, plant_0/1 — come from Tools/make_press_sfx.py instead: recorded mouth pops
(CC0), because the owner still heard these struck sine notes as piercing. That script imports the building
blocks, loudness and master() from here. The ladder below still lists them, at their new rungs.

    uv run --with numpy --with scipy --with soundfile python Tools/gen_sfx.py
    uv run --with numpy --with scipy --with soundfile --with matplotlib python Tools/gen_sfx.py --preview sheet.png
    SFX_OUT=/tmp/try uv run ...          # write somewhere else, to audition before replacing

Why (owner, 2026-09-15): "the sound effects are a bit harsh on the ears — find something chill and soft".
The Kenney set it replaces put most of its energy where the ear is most sensitive: panel_open/close had
a spectral centroid of 12 kHz, menu_open/close and chest_open 5.7-6.7 kHz, toggle and claim were the
loudest things in the game. Played for hours next to a quiet farm, that reads as clicks and beeps.

The sound world: one small wooden-and-glass band. Felt-mallet marimba, kalimba, a warm glockenspiel
bell, a harp for the big moments, soft water bubbles, leaf and soil noise. Nothing is a square or saw
wave, no attack is shorter than 3 ms, every clip goes through a gentle low-pass (<= 6.5 kHz, most
lower) and no partial above 7 kHz is ever generated.

KEY: A-flat major pentatonic (Ab Bb C Eb F). The background music (Audio/Music/terrace_in_the_clouds)
is in A-flat major at 144 BPM and ends on a Db major-seventh chord, so every tonal effect sits inside the
chord of whatever the music is playing instead of rubbing against it. Sfx.cs keeps pitch jitter on
tonal clips to +-1.2 % (20 cents) for the same reason.

MASTERING: mono, 44.1 kHz, Ogg Vorbis (q 0.7). 3 ms raised-cosine fade in, raised-cosine tail. Each
clip is normalised to its row of the loudness ladder below, then capped at a -6 dBFS peak. The level
is the max K-weighted loudness over a 100 ms window ("L100", an approximate LUFS for sounds too short
for integrated loudness), averaged between the full signal and a phone-speaker high-pass at 700 Hz so
a sound built low is not lost on the device. The ladder lives in the files, so Sfx.Table uses
near-uniform volumes: frequent touches are the quietest and shortest, the three session goals (level,
island, legendary) the loudest.

    level clip                          level clip
    -30   tap *                         -23   harvest_0/1 *
    -29   tab *                         -22   error (soft, never a buzzer)
    -27   toggle, menu_close,           -20   coins
          panel_close *                 -19   claim
    -26   menu_open, panel_open *       -18   mutation
    -24   plank_0..2; plant_0/1 *       -17   chest_open
    -22   water                         -15   level_up, legendary (peak-capped near -17;
                                              Sfx.Table gives it a little more volume)
                                        -14   island_unlock
    * written by Tools/make_press_sfx.py

The printout (and --preview) lists each clip's length, decoded peak, level, full-range and phone L100,
and its energy above 6 and 8 kHz relative to the whole clip.

The start-screen cinematic's cine_whoosh / cine_chime are Tools/gen_cine_audio.py's (same key).
Random parts use a seed per clip name, so re-running changes nothing and tuning one clip does not
reshuffle the others.
"""
import os
import sys
import zlib

import numpy as np
import soundfile as sf
from scipy import signal

OUT = os.environ.get("SFX_OUT", "Assets/Resources/Audio")
SR = 44100
PEAK_CAP = 10 ** (-6.2 / 20)       # a little under -6 dBFS: Vorbis overshoots a hair on decode
MAX_PARTIAL_HZ = 7000.0

# A-flat major pentatonic, equal temperament, A4 = 440
_SEMI = {"C": 0, "Db": 1, "D": 2, "Eb": 3, "E": 4, "F": 5, "Gb": 6, "G": 7, "Ab": 8, "A": 9, "Bb": 10, "B": 11}


def hz(note):
    name, octave = note[:-1], int(note[-1])
    return 440.0 * 2 ** ((_SEMI[name] - 9) / 12 + (octave - 4))


PENTA = ["Ab", "Bb", "C", "Eb", "F"]


# ============================================================
# building blocks
# ============================================================
def n_(seconds):
    return int(round(seconds * SR))


def time(seconds):
    return np.arange(n_(seconds)) / SR


def canvas(seconds):
    return np.zeros(n_(seconds))


def place(out, x, at, gain=1.0):
    i = n_(at)
    k = max(0, min(len(x), len(out) - i))
    out[i:i + k] += gain * x[:k]
    return out


def rise(n, seconds):
    """Raised-cosine 0 -> 1 over `seconds`, then 1. The only kind of attack in this file."""
    e = np.ones(n)
    k = min(n, max(n_(seconds), 1))
    e[:k] = 0.5 - 0.5 * np.cos(np.pi * np.arange(k) / k)
    return e


def fall(n, seconds):
    """1, then raised-cosine 1 -> 0 over the last `seconds`."""
    return rise(n, seconds)[::-1]


def partials(f, seconds, parts, attack=0.004, phase=0.0, tremolo=(0.0, 0.0)):
    """Sum of decaying sines. parts = (ratio, amplitude, decay seconds). Anything above
    MAX_PARTIAL_HZ is left out rather than filtered afterwards."""
    t = time(seconds)
    y = np.zeros_like(t)
    for ratio, amp, decay in parts:
        if f * ratio >= MAX_PARTIAL_HZ:
            continue
        y += amp * np.sin(2 * np.pi * f * ratio * t + phase * ratio) * np.exp(-t / decay)
    depth, rate = tremolo
    if depth:
        y *= 1 - depth * (0.5 - 0.5 * np.cos(2 * np.pi * rate * t))
    return y * rise(len(t), attack)


def lowpass(x, fc, order=4):
    return signal.sosfilt(signal.butter(order, fc, "low", fs=SR, output="sos"), x)


def highpass(x, fc, order=2):
    return signal.sosfilt(signal.butter(order, fc, "high", fs=SR, output="sos"), x)


def bandpass(x, lo, hi, order=4):
    return signal.sosfilt(signal.butter(order, [lo, hi], "band", fs=SR, output="sos"), x)


# ---- instruments ----
def marimba(f, seconds=0.5, decay=0.22, bright=1.0, mallet=0.05, rng=None):
    """Felt mallet on a wooden bar: a round fundamental, the bar's 4th partial dying almost at once,
    and a breath of low-passed noise for the felt touching the wood."""
    y = partials(f, seconds, [(1.0, 1.0, decay), (3.93, 0.10 * bright, decay * 0.15),
                              (2.0, 0.03 * bright, decay * 0.4)], attack=0.003)
    if mallet and rng is not None:
        k = n_(0.014)
        touch = lowpass(rng.standard_normal(k), 1500) * np.hanning(k)
        y[:k] += mallet * touch / (np.max(np.abs(touch)) + 1e-9)
    return y


def kalimba(f, seconds=0.5, decay=0.3):
    """A thumb-plucked tine: nearly a pure tone, a faint octave and the tine's high inharmonic
    ring that is gone within a few tens of ms."""
    return partials(f, seconds, [(1.0, 1.0, decay), (2.0, 0.06, decay * 0.45), (5.9, 0.05, decay * 0.06)],
                    attack=0.004)


def bell(f, seconds=1.0, decay=0.6, tremolo=(0.0, 0.0)):
    """A soft glockenspiel struck with a rubber mallet: warm, the upper partials short, and a
    slightly detuned twin of the fundamental for a slow shimmer."""
    return partials(f, seconds, [(1.0, 1.0, decay), (1.0016, 0.30, decay * 0.9), (2.0, 0.20, decay * 0.5),
                                 (3.0, 0.06, decay * 0.28), (4.07, 0.025, decay * 0.15)],
                    attack=0.005, tremolo=tremolo)


def harp(f, seconds=0.8, decay=0.45):
    """A nylon harp string: harmonic, the brighter partials fading first."""
    return partials(f, seconds, [(1.0, 1.0, decay), (2.0, 0.38, decay * 0.45), (3.0, 0.16, decay * 0.3),
                                 (4.0, 0.06, decay * 0.2), (5.0, 0.025, decay * 0.12)], attack=0.003)


def pad(notes, seconds, attack, release, amp=1.0):
    """Sustained sines with a detuned twin (a slow chorus) and a quiet octave."""
    t = time(seconds)
    y = np.zeros_like(t)
    for f in notes:
        y += np.sin(2 * np.pi * f * t) + 0.8 * np.sin(2 * np.pi * f * 1.0035 * t + 1.1) \
            + 0.10 * np.sin(2 * np.pi * 2 * f * t)
    return amp * y / len(notes) * rise(len(t), attack) * fall(len(t), release)


def bubble(f0, f1, seconds, glide, decay, attack=0.003):
    """A sine that slides f0 -> f1 (exponentially, over `glide`) and dies: a water bubble or a
    soft cork pop when rising, a sigh when falling."""
    t = time(seconds)
    k = np.clip(t / glide, 0, 1)
    freq = f0 * (f1 / f0) ** k
    ph = 2 * np.pi * np.cumsum(freq) / SR
    return np.sin(ph) * np.exp(-t / decay) * rise(len(t), attack)


def grains(rng, seconds, count, lo, hi, length=(0.008, 0.02), spread=1.0, decay=1.0):
    """Band-limited noise grains scattered in time: leaves brushing, soil crumbling."""
    out = canvas(seconds)
    for _ in range(count):
        g = n_(rng.uniform(*length))
        at = rng.uniform(0, seconds * spread)
        w = bandpass(rng.standard_normal(g + 400), lo, hi)[400:] * np.hanning(g)
        amp = rng.uniform(0.4, 1.0) * np.exp(-at / decay)
        place(out, w / (np.max(np.abs(w)) + 1e-9), at, amp)
    return out


def swish(rng, seconds, f_from, f_to, peak_at, decay, q=1.1):
    """Air past a sheet of paper: noise through a band-pass whose centre moves."""
    t = time(seconds)
    x = rng.standard_normal(len(t))
    fc = f_from * (f_to / f_from) ** np.clip(t / seconds, 0, 1)
    y = np.zeros_like(x)
    low = band = 0.0
    damp = 1.0 / q
    for i in range(len(x)):           # Chamberlin state-variable filter, per-sample cutoff
        g = 2.0 * np.sin(np.pi * fc[i] / SR)
        high = x[i] - low - damp * band
        band += g * high
        low += g * band
        y[i] = band
    env = np.where(t < peak_at, (0.5 - 0.5 * np.cos(np.pi * t / peak_at)), np.exp(-(t - peak_at) / decay))
    y = lowpass(y * env, min(3200, f_from * 2.2, f_to * 2.2))
    return y / (np.max(np.abs(y)) + 1e-9)


def _comb(x, d, g):
    y = x.copy()
    for k in range(d, len(y), d):
        y[k:k + d] += g * y[k - d:k][:len(y[k:k + d])]
    return y


def _allpass(x, d, g):
    y = -g * x
    y[d:] += x[:-d]
    for k in range(d, len(y), d):
        seg = y[k:k + d]
        seg += g * y[k - d:k][:len(seg)]
    return y


def space(x, mix=0.15, decay=0.6, tone=3500):
    """A small, soft room (Schroeder: four combs, two all-passes), low-passed so the reflections
    are warmer than the sound itself. It is what rounds the plucks off."""
    wet = np.zeros_like(x)
    for ms in (29.7, 37.1, 41.1, 43.7):
        d = n_(ms / 1000)
        g = 10 ** (-3 * d / (decay * SR))
        wet += _comb(x, d, g)
    for ms, g in ((5.0, 0.7), (1.7, 0.7)):
        wet = _allpass(wet, n_(ms / 1000), g)
    wet = lowpass(wet / 4, tone)
    return x * (1 - mix) + wet * mix * 2.2


# ============================================================
# loudness
# ============================================================
def _k_weight(x):
    """ITU-R BS.1770 K-weighting (shelf + high-pass), recomputed for 44.1 kHz."""
    w0 = 2 * np.pi * 1500 / SR
    a_ = 10 ** (4.0 / 40)
    al = np.sin(w0) / (2 / np.sqrt(2))
    c = np.cos(w0)
    b = [a_ * ((a_ + 1) + (a_ - 1) * c + 2 * np.sqrt(a_) * al), -2 * a_ * ((a_ - 1) + (a_ + 1) * c),
         a_ * ((a_ + 1) + (a_ - 1) * c - 2 * np.sqrt(a_) * al)]
    a = [(a_ + 1) - (a_ - 1) * c + 2 * np.sqrt(a_) * al, 2 * ((a_ - 1) - (a_ + 1) * c),
         (a_ + 1) - (a_ - 1) * c - 2 * np.sqrt(a_) * al]
    x = signal.lfilter(b, a, x)
    w0 = 2 * np.pi * 38 / SR
    al = np.sin(w0) / (2 * 0.5)
    c = np.cos(w0)
    return signal.lfilter([(1 + c) / 2, -(1 + c), (1 + c) / 2], [1 + al, -2 * c, 1 - al], x)


def l100(x):
    """Max K-weighted loudness over a 100 ms window (approximate LUFS)."""
    y = _k_weight(np.asarray(x, dtype=np.float64))
    w = n_(0.1)
    if len(y) < w:
        y = np.pad(y, (0, w - len(y)))
    power = np.convolve(y * y, np.ones(w) / w, "valid")
    return -0.691 + 10 * np.log10(np.max(power) + 1e-12)


def phone(x):
    """What a phone's small speaker leaves: very little under ~700 Hz."""
    return highpass(np.asarray(x, dtype=np.float64), 700, order=2)


def loudness(x):
    """The level the ladder is set in: L100 averaged between full range (headphones) and a phone
    speaker, so a sound built low (a soil thump, a wooden knock) is not lost on the device while
    a bright one is not louder in headphones."""
    return 0.5 * (l100(x) + l100(phone(x)))


def master(y, target, lp=6500, fade_out=0.03):
    y = highpass(y, 45)
    y = lowpass(y, lp)
    y = y * rise(len(y), 0.003) * fall(len(y), fade_out)
    y *= 10 ** ((target - loudness(y)) / 20)
    peak = np.max(np.abs(y))
    if peak > PEAK_CAP:
        y *= PEAK_CAP / peak
    return y


# ============================================================
# the clips
# ============================================================
N = hz


def clip_water(rng):
    # a watering can: droplets (rising bubbles that come to rest on the scale) over a soft shower
    y = canvas(0.44)
    notes = [N(n) for n in ("C6", "Eb6", "F6", "Ab6", "Bb5")]
    times = np.sort(rng.beta(1.3, 2.4, 15) * 0.34)
    for at in times:
        f = notes[rng.integers(len(notes))]
        dur = rng.uniform(0.03, 0.05)
        amp = rng.uniform(0.35, 1.0) * (1 - 0.45 * at / 0.34)
        place(y, bubble(f * 0.82, f, dur + 0.03, glide=dur * 0.6, decay=dur * 0.45, attack=0.003), at, amp)
    shower = bandpass(rng.standard_normal(len(y)), 700, 3200)
    t = np.arange(len(y)) / SR
    shower *= np.where(t < 0.05, 0.5 - 0.5 * np.cos(np.pi * t / 0.05), np.exp(-(t - 0.05) / 0.12))
    y += 0.10 * shower / np.max(np.abs(shower))
    return master(space(y, 0.12, 0.3), -22, lp=5500, fade_out=0.08)


def clip_coins(rng):
    # two small warm bells and a quieter third: coins into a cloth purse, not a cash register
    y = canvas(0.46)
    for at, note, amp in ((0.0, "C6", 0.8), (0.06, "Eb6", 1.0), (0.125, "Ab6", 0.45)):
        place(y, partials(N(note), 0.4, [(1, 1, 0.14), (2.0, 0.16, 0.06), (2.76, 0.05, 0.03)], attack=0.004),
              at, amp)
    return master(space(y, 0.14, 0.4), -20, lp=6000, fade_out=0.1)


def clip_error(rng):
    # "not yet": two low, muted wooden notes stepping down, the second sagging a little
    y = canvas(0.3)
    place(y, marimba(N("Eb5"), 0.14, decay=0.06, bright=0.35, mallet=0.03, rng=rng), 0)
    place(y, partials(N("Eb4"), 0.14, [(1, 1, 0.05), (2, 0.2, 0.03)]), 0, 0.35)
    t = time(0.2)
    sag = N("C5") * (1 - 0.03 * np.clip(t / 0.12, 0, 1))
    tone = np.sin(2 * np.pi * np.cumsum(sag) / SR) * np.exp(-t / 0.075)
    tone += 0.35 * np.sin(np.pi * np.cumsum(sag) / SR) * np.exp(-t / 0.06)        # the octave below
    tone += 0.05 * np.sin(2 * np.pi * np.cumsum(sag * 3.93) / SR) * np.exp(-t / 0.012)
    place(y, tone * rise(len(t), 0.004), 0.095)
    return master(y, -22, lp=2800, fade_out=0.06)


def clip_claim(rng):
    y = canvas(0.56)
    for at, note, amp in ((0.0, "C5", 0.75), (0.065, "Eb5", 0.85), (0.13, "Ab5", 1.0)):
        place(y, marimba(N(note), 0.4, decay=0.15, bright=0.8, mallet=0.03, rng=rng), at, amp)
    place(y, bell(N("C6"), 0.4, decay=0.18), 0.13, 0.22)
    return master(space(y, 0.16, 0.5), -19, lp=5500, fade_out=0.12)


def clip_mutation(rng):
    # something rare in the soil: a quick run up, then a shimmering dyad and a breath of air
    y = canvas(0.78)
    for i, note in enumerate(("Eb5", "F5", "Ab5")):
        place(y, kalimba(N(note), 0.3, decay=0.1), i * 0.045, 0.45 + 0.08 * i)
    for note, amp in (("Ab5", 0.7), ("C6", 0.55)):
        place(y, bell(N(note), 0.62, decay=0.3, tremolo=(0.25, 7.0)), 0.14, amp)
    air = bandpass(rng.standard_normal(n_(0.6)), 1800, 4500)
    t = time(0.6)
    air *= np.sin(np.pi * np.clip(t / 0.6, 0, 1)) ** 2
    place(y, air / np.max(np.abs(air)), 0.1, 0.05)
    return master(space(y, 0.22, 0.7), -18, lp=6000, fade_out=0.18)


def clip_chest_open(rng):
    # a wooden lid lifts (a round knock), and a little sparkle climbs out
    y = canvas(0.82)
    place(y, marimba(N("Ab4"), 0.2, decay=0.07, bright=0.4, mallet=0.06, rng=rng), 0)
    place(y, partials(N("Ab3"), 0.2, [(1, 1, 0.06)]), 0, 0.5)
    place(y, bubble(170, 90, 0.12, glide=0.05, decay=0.04, attack=0.004), 0, 0.5)
    place(y, grains(rng, 0.05, 4, 200, 900, length=(0.01, 0.02), decay=0.03), 0, 0.2)
    for i, note in enumerate(("Eb5", "Ab5", "C6", "Eb6")):
        place(y, kalimba(N(note), 0.5, decay=0.22), 0.12 + i * 0.05, 0.42 + 0.06 * i)
    place(y, bell(N("Ab5"), 0.5, decay=0.3), 0.27, 0.2)
    return master(space(y, 0.2, 0.6), -17, lp=5500, fade_out=0.2)


def _plank(rng, note):
    # a rope-bridge board settling: hollow wood, knocked softly
    y = canvas(0.14)
    f = N(note)
    place(y, partials(f, 0.14, [(1.0, 1.0, 0.04), (2.52, 0.3, 0.018), (4.1, 0.06, 0.009)], attack=0.003), 0)
    place(y, partials(f / 2, 0.14, [(1.0, 1.0, 0.035)], attack=0.004), 0, 0.45)
    k = n_(0.006)
    click = bandpass(rng.standard_normal(k + 400), 500, 2000)[400:] * np.hanning(k)
    place(y, click / np.max(np.abs(click)), 0, 0.12)
    return master(y, -24, lp=3200, fade_out=0.04)


def clip_plank_0(rng):
    return _plank(rng, "Ab4")


def clip_plank_1(rng):
    return _plank(rng, "Bb4")


def clip_plank_2(rng):
    return _plank(rng, "C5")


def clip_level_up(rng):
    # a harp climbs the scale into a warm bell chord over a soft pad
    y = canvas(1.4)
    for i, note in enumerate(("Ab4", "C5", "Eb5", "F5", "Ab5")):
        place(y, harp(N(note), 0.7, decay=0.3), i * 0.07, 0.42 + 0.05 * i)
    for note, amp in (("Ab5", 0.62), ("C6", 0.5), ("Eb6", 0.4)):
        place(y, bell(N(note), 1.04, decay=0.5), 0.36, amp)
    place(y, pad([N("Ab3"), N("Eb4"), N("C5")], 1.1, attack=0.14, release=0.7, amp=0.22), 0.3)
    place(y, bell(N("Ab6"), 0.5, decay=0.25), 0.54, 0.12)
    return master(space(y, 0.22, 1.0), -15, lp=6000, fade_out=0.3)


def clip_island_unlock(rng):
    # a new island rising out of the clouds: a pad swells, bells rise through it, a high shimmer
    y = canvas(1.5)
    place(y, pad([N("Ab3"), N("C4"), N("Eb4"), N("Ab4")], 1.5, attack=0.35, release=0.75, amp=0.3), 0)
    for i, note in enumerate(("Eb5", "Ab5", "C6", "Eb6")):
        place(y, bell(N(note), 1.3 - i * 0.14, decay=0.62), 0.1 + i * 0.14, 0.55 + 0.03 * i)
    place(y, bell(N("Ab6"), 0.8, decay=0.35, tremolo=(0.2, 6.0)), 0.68, 0.16)
    return master(space(y, 0.3, 1.3), -14, lp=6000, fade_out=0.35)


def clip_legendary(rng):
    # a harp glissando over two octaves of the scale, a bell chord blooms, a few far sparkles
    y = canvas(1.5)
    scale = [n + str(o) for o in (4, 5, 6) for n in PENTA][:11]      # Ab4 .. Ab6
    for i, note in enumerate(scale):
        place(y, harp(N(note), 0.7, decay=0.32), i * 0.036, 0.3 + 0.022 * i)
    for i, (note, amp) in enumerate((("Ab5", 0.5), ("C6", 0.42), ("Eb6", 0.36), ("Ab6", 0.18))):
        place(y, bell(N(note), 1.08, decay=0.7), 0.42 + 0.018 * i, amp)
    place(y, pad([N("Ab3"), N("Eb4"), N("C5")], 1.2, attack=0.22, release=0.7, amp=0.24), 0.3)
    for _ in range(7):
        note = ("F6", "Ab6", "Bb6", "C7")[rng.integers(4)]
        place(y, bell(N(note), 0.3, decay=0.1), rng.uniform(0.55, 1.15), rng.uniform(0.05, 0.11))
    return master(space(y, 0.3, 1.2), -15, lp=6000, fade_out=0.35)


# tap, tab, toggle, menu_open/close, panel_open/close, harvest_0/1 and plant_0/1 are Tools/make_press_sfx.py's
# (recorded mouth pops, 2026-09-15): do not add them back here, this script would overwrite them.
CLIPS = [
    "water", "coins", "error", "claim",
    "mutation", "chest_open", "plank_0", "plank_1", "plank_2", "level_up", "island_unlock", "legendary",
]


def render(name):
    rng = np.random.default_rng(zlib.crc32(name.encode()))
    return globals()["clip_" + name](rng)


def band_db(y, lo):
    s = np.abs(np.fft.rfft(y * np.hanning(len(y)))) ** 2
    f = np.fft.rfftfreq(len(y), 1 / SR)
    return 10 * np.log10(s[f >= lo].sum() / s.sum() + 1e-15)


def write(name, y):
    path = os.path.join(OUT, name + ".ogg")
    for _ in range(4):
        sf.write(path, y.astype(np.float32), SR, format="OGG", subtype="VORBIS", compression_level=0.3)
        back, _sr = sf.read(path)
        over = np.max(np.abs(back)) / 10 ** (-6 / 20)
        if over <= 1.0:
            return path, back
        y = y / (over * 1.01)
    return path, back


def preview(files, png):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    cols = 2
    rows = (len(files) + cols - 1) // cols
    fig, axes = plt.subplots(rows, cols * 2, figsize=(22, 1.9 * rows),
                             gridspec_kw={"width_ratios": [1, 1.4] * cols})
    for k, (name, y) in enumerate(files):
        r, c = divmod(k, cols)
        aw, asp = axes[r][c * 2], axes[r][c * 2 + 1]
        t = np.arange(len(y)) / SR
        aw.plot(t, y, lw=0.5, color="#2b6")
        aw.axhline(10 ** (-6 / 20), color="r", lw=0.4, ls="--")
        aw.axhline(-10 ** (-6 / 20), color="r", lw=0.4, ls="--")
        span = max(0.3, len(y) / SR)
        aw.set_ylim(-0.55, 0.55)
        aw.set_xlim(0, span)
        pk = 20 * np.log10(np.max(np.abs(y)) + 1e-9)
        aw.set_title(f"{name}  {len(y) / SR * 1000:.0f} ms  peak {pk:.1f} dBFS  level {loudness(y):.1f}  "
                     f">6k {band_db(y, 6000):.0f} dB", fontsize=8, loc="left")
        aw.tick_params(labelsize=6)
        nfft = 256 if len(y) < SR * 0.4 else 1024
        y = np.pad(y, (0, max(0, n_(span) + nfft - len(y))))
        asp.specgram(y, NFFT=nfft, Fs=SR, noverlap=nfft - nfft // 8, cmap="magma", vmin=-120, vmax=-20)
        asp.set_ylim(0, 12000)
        asp.set_xlim(0, span)
        asp.axhline(6000, color="c", lw=0.5, ls="--")
        asp.tick_params(labelsize=6)
    for k in range(len(files), rows * cols):
        r, c = divmod(k, cols)
        axes[r][c * 2].axis("off")
        axes[r][c * 2 + 1].axis("off")
    plt.tight_layout()
    plt.savefig(png, dpi=60)
    print("   preview", png)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    rendered = []
    print(f"   {'clip':14s} {'ms':>5s} {'peak':>6s} {'level':>6s} {'L100':>6s} {'phone':>6s} {'>6k':>5s} {'>8k':>5s}")
    for name in CLIPS:
        path, back = write(name, render(name))
        rendered.append((name, back))
        pk = 20 * np.log10(np.max(np.abs(back)))
        print(f"   {name:14s} {len(back) / SR * 1000:5.0f} {pk:6.1f} {loudness(back):6.1f} {l100(back):6.1f} "
              f"{l100(phone(back)):6.1f} "
              f"{band_db(back, 6000):5.0f} {band_db(back, 8000):5.0f}")
    if "--preview" in sys.argv:
        png = sys.argv[sys.argv.index("--preview") + 1]
        for extra in ("cine_whoosh", "cine_chime"):          # Tools/gen_cine_audio.py's, for comparison
            p = os.path.join(OUT, extra + ".ogg")
            if os.path.exists(p):
                rendered.append((extra, sf.read(p)[0]))
        preview(rendered, png)
