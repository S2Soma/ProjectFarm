"""The sounds that answer a touch — tap, tab, toggle, menu_open/close, panel_open/close, and the pop in
harvest_0/1 and plant_0/1 — built from recorded mouth pops instead of synthesised tones.

    uv run --with numpy --with scipy --with soundfile python Tools/make_press_sfx.py --fetch
    uv run --with numpy --with scipy --with soundfile --with matplotlib python Tools/make_press_sfx.py --preview sheet.png
    --src DIR                            where the original pops are (default ~/.cache/matu-farm/sfx_src/pops)
    SFX_OUT=/tmp/try uv run ...          # write somewhere else, to audition before replacing

Why (owner, 2026-09-15, second round): "âm thanh khi nhấn hơi chói tai không hay, hãy tìm âm thanh khác" — the
press sound is still piercing, find a different one. The first rework (Tools/gen_sfx.py) had already taken
nearly everything above 2 kHz out of these clips (0.0 % of the energy above 3 kHz, centroids 600-830 Hz), so
brightness was not what was left. What was left, measured (scratch analysis, numbers in the table below):

  * every press sound was a decaying SINE: tap a 622 Hz note over 311 Hz, tab 830 Hz (+415, 1660), toggle,
    menu and panel marimba/kalimba notes at Eb5-Ab5. 88-98 % of each clip's power sat within 40 cents of
    one line and that line rang for 80-250 ms. A pure tone at 600-850 Hz with a 2-3 ms rise is a beep,
    however soft its spectrum; on a phone speaker, which plays little under ~700 Hz, only the ring is left.
  * one tap stacked two or three of those in the same frame: the button's Tap, GameApp.Open's PanelOpen,
    the menu's MenuClose — three struck Eb5/Ab5 notes, each with its own +-1.2 % pitch jitter, so they beat
    against each other. Sfx.cs now lets one press sound through per touch.
  * harvest and plant carried the same ringing note (marimba C5/Eb5, kalimba F5/Ab5: 72-97 % in one line,
    170-250 ms) and fire on every plot tap, so their tone is replaced too; their soil and leaf layers stay.

So the family changes character rather than filter settings. A mouth pop ("bop") is a resonance that GLIDES
up by more than an octave in ~35 ms (about 400 -> 1000 Hz in these recordings) and opens with a 5-10 ms swell
of air pressure instead of a strike. It is over before it can be heard as a held pitch: 10-26 % of the power
in one line, rings <= 35 ms.

    clip          before: line%  ring ms  rise ms  peak      after: line%  ring ms  rise ms  peak
    tap                   88      81      1.8    -11.9            26       7      7.1    -15.7
    tab                   97     100      3.2    -14.2            20       9      9.1    -14.9
    toggle                91     132       59    -13.6            11      35       10    -13.4
    menu_open             98     186       48    -13.7            16      23       12    -12.5
    menu_close            95     167       47    -13.4            12      29       54    -13.6
    panel_open            72     250       14    -11.4            14      16       14    -12.1
    panel_close           88     181       60    -12.6            15      13       60    -12.4
    harvest_0             97     247       16     -9.0            20       7       18     -8.0
    plant_0               72     170        4     -8.7            20       0        4     -8.6
  (line% = power within +-40 cents of the strongest line; ring = time that line stays within a semitone
   above -25 dB; rise = 10-90 % of the 1 ms envelope, so a pair of pops whose second is louder shows a long
   one. Before and after alike: < 0.1 % of the energy above 3 kHz.)

SOURCE — third-party audio that ships (see Assets/Art_CREDITS.txt):
    "Pop sounds" by cogitollc, OpenGameArt.org, posted 2022-07-14, CC0 1.0 (public domain)
    https://opengameart.org/content/pop-sounds — pop4, pop6, pop7, pop9, pop10 (.ogg), recorded by the
    author on an iPhone SE (Voice Memos, Ocenaudio). The rest of that pack is unused; pop2/3/5/7/8/9 decode
    above full scale, pop4 and pop10 do not, and none of them is flat-topped.
The originals are NOT in the repo. --fetch downloads them into --src; the sha1s pin the exact files these
sounds were tuned on, so a changed upload stops the script instead of quietly producing different sounds.

PROCESSING (reproducible from the originals): mono, DC removed, 90 Hz high-pass (breath rumble), trimmed to
the pop (36 dB under its peak, minus 1.5 ms, 4 ms tail fade), resampled to pitch like tape — each pop's
energy-weighted glide frequency is measured and moved onto a note of A-flat major pentatonic, the key of the
music — then layered, 2.8 kHz 2nd-order low-pass, and gen_sfx.master(): 45 Hz high-pass, 4.2 kHz 4th-order
low-pass, 3 ms raised-cosine fade in, raised-cosine tail, level set on the loudness ladder, peak capped.
The panel sounds add a breath of air (gen_sfx.swish, 280-1000 Hz, low-passed at 1.8 kHz, quiet); harvest
keeps gen_sfx's leaf grains and gets a small soil "tuk", plant keeps its soil thump and crumble.

LADDER (same measure as gen_sfx.py, whose docstring has the rest of the ladder):
    -30  tap                        -27  toggle, menu_close, panel_close      -23  harvest_0/1
    -29  tab                        -26  menu_open, panel_open                -24  plant_0/1
Three dB under the first rework's rungs for the press family and two for harvest/plant: the most frequent
sounds in the game, and a short pop reaches a given loudness with a higher peak than a ringing note
(tap now peaks at -16 dBFS).
"""
import hashlib
import os
import sys
import urllib.request
from fractions import Fraction

import numpy as np
import soundfile as sf
from scipy import signal

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_sfx as G  # noqa: E402  (building blocks, loudness, master, write, preview)

SR = G.SR
URL = "https://opengameart.org/sites/default/files/{}"
# the pops used, and the sha1 of each file as downloaded on 2026-09-15
SOURCES = {
    "pop4.ogg": "1b662c108e496119aaf1f8f012de7d1a204e05c0",
    "pop6.ogg": "a542f6902104f75e42cfea215b9a7afbb6b01ef0",
    "pop7.ogg": "0b26fcfebe5bb6038c99697d1f4c3a569d4d8209",
    "pop9.ogg": "83c1341426272fc8a9ed57139001ccce8e4b4b27",
    "pop10.ogg": "56b0afb09ebe4def87cfa24e5d740c376e08d1c3",
}


# ============================================================
# source handling
# ============================================================
def sha1(path):
    with open(path, "rb") as f:
        return hashlib.sha1(f.read()).hexdigest()


def fetch(src):
    os.makedirs(src, exist_ok=True)
    for name in SOURCES:
        path = os.path.join(src, name)
        if not os.path.exists(path):
            print("   fetch", URL.format(name))
            req = urllib.request.Request(URL.format(name), headers={"User-Agent": "MATU-Farm-build"})
            with urllib.request.urlopen(req, timeout=60) as r, open(path, "wb") as f:
                f.write(r.read())


def check(src):
    bad = False
    for name, want in SOURCES.items():
        path = os.path.join(src, name)
        if not os.path.exists(path):
            print(f"   missing {path} (run with --fetch)")
            bad = True
        elif want and sha1(path) != want:
            print(f"   {name}: sha1 {sha1(path)} differs from the file these sounds were tuned on ({want})")
            bad = True
    if bad:
        sys.exit(1)


_cache = {}


def pop(src, name):
    """One pop: mono, DC-free, rumble removed, trimmed to the sound, peak 1."""
    if name not in _cache:
        y, sr = sf.read(os.path.join(src, name), always_2d=True)
        y = y.mean(1)
        if sr != SR:
            y = signal.resample_poly(y, SR, sr)
        y = y - np.mean(y)
        y = signal.sosfiltfilt(signal.butter(2, 90, "high", fs=SR, output="sos"), y)
        pk = np.max(np.abs(y))
        start = max(0, np.argmax(np.abs(y) > pk * 10 ** (-36 / 20)) - G.n_(0.0015))
        w = G.n_(0.005)
        env = np.sqrt(np.convolve(y * y, np.ones(w) / w, "same"))
        after = np.where(env > pk * 10 ** (-48 / 20))[0]
        end = min(len(y), after[-1] + G.n_(0.005))
        y = y[start:end] * G.fall(end - start, 0.004)
        _cache[name] = y / np.max(np.abs(y))
    return _cache[name]


def glide_hz(x):
    """Energy-weighted instantaneous frequency of a pop: the pitch it is heard at."""
    z = signal.sosfiltfilt(signal.butter(4, [150, 3000], "band", fs=SR, output="sos"), x)
    a = signal.hilbert(z)
    env = np.abs(a)[:-1]
    inst = np.diff(np.unwrap(np.angle(a))) / (2 * np.pi) * SR
    keep = env > env.max() * 0.1
    w = env[keep] ** 2
    return float((inst[keep] * w).sum() / w.sum())


def repitch(x, semitones):
    """Resample: pitch and length move together, as with a tape — the pop keeps its own shape."""
    ratio = Fraction(2 ** (-semitones / 12)).limit_denominator(400)
    return signal.resample_poly(x, ratio.numerator, ratio.denominator)


def tuned(src, name, note):
    """The pop moved onto a note of the scale (its glide centred there)."""
    x = pop(src, name)
    target = G.hz(note)
    semis = 12 * np.log2(target / glide_hz(x))
    return repitch(x, semis)


def air(rng, seconds, f_from, f_to, peak_at, decay):
    """A breath of air under a panel: gen_sfx's moving band-pass noise, kept low."""
    return G.lowpass(G.swish(rng, seconds, f_from, f_to, peak_at=peak_at, decay=decay), 1800, order=2)


def thump(f0, f1, seconds, glide, decay):
    """gen_sfx.bubble with a tail fade: cut off bare, its last few per cent of amplitude click."""
    x = G.bubble(f0, f1, seconds, glide=glide, decay=decay, attack=0.004)
    return x * G.fall(len(x), seconds * 0.3)


def finish(y, target, fade_out):
    y = signal.sosfilt(signal.butter(2, 2800, "low", fs=SR, output="sos"), y)
    return G.master(y, target, lp=4200, fade_out=fade_out)


# ============================================================
# the clips
# ============================================================
def clip_tap(src, rng):
    # one soft "bop", glide centred on C5
    x = tuned(src, "pop10.ogg", "C5")
    y = G.canvas(len(x) / SR + 0.012)
    G.place(y, x, 0.002)
    return finish(y, -30, fade_out=0.02)


def clip_tab(src, rng):
    # a smaller, lighter pop a step above the tap
    x = tuned(src, "pop4.ogg", "Eb5")
    y = G.canvas(len(x) / SR + 0.012)
    G.place(y, x, 0.002)
    return finish(y, -29, fade_out=0.02)


def clip_toggle(src, rng):
    # "bup-bup" on one note: a latch, neither up nor down (the same clip turns things on and off)
    y = G.canvas(0.15)
    G.place(y, tuned(src, "pop9.ogg", "Bb4"), 0.002, 1.0)
    G.place(y, tuned(src, "pop10.ogg", "Bb4"), 0.056, 0.55)
    return finish(y, -27, fade_out=0.03)


def clip_menu_open(src, rng):
    # two pops climbing: the menu pops up
    y = G.canvas(0.17)
    G.place(y, tuned(src, "pop6.ogg", "Bb4"), 0.002, 1.0)
    G.place(y, tuned(src, "pop4.ogg", "Eb5"), 0.05, 0.6)
    return finish(y, -26, fade_out=0.035)


def clip_menu_close(src, rng):
    # the same two pops falling
    y = G.canvas(0.17)
    G.place(y, tuned(src, "pop4.ogg", "Eb5"), 0.002, 0.6)
    G.place(y, tuned(src, "pop6.ogg", "Bb4"), 0.046, 1.0)
    return finish(y, -27, fade_out=0.035)


def clip_panel_open(src, rng):
    # a low, round pop with a breath of air rising under it, and a small pop on top as the card lands
    y = G.canvas(0.24)
    G.place(y, air(rng, 0.16, 300, 1000, peak_at=0.05, decay=0.05), 0.0, 0.14)
    G.place(y, tuned(src, "pop7.ogg", "Ab4"), 0.012, 1.0)
    G.place(y, tuned(src, "pop10.ogg", "C5"), 0.08, 0.45)
    return finish(y, -26, fade_out=0.05)


def clip_panel_close(src, rng):
    # the small pop, then the low one, the air falling away
    y = G.canvas(0.22)
    G.place(y, air(rng, 0.14, 950, 280, peak_at=0.035, decay=0.045), 0.0, 0.12)
    G.place(y, tuned(src, "pop4.ogg", "C5"), 0.002, 0.45)
    G.place(y, tuned(src, "pop7.ogg", "Ab4"), 0.052, 1.0)
    return finish(y, -27, fade_out=0.05)


def _harvest(src, rng, note, name):
    # the crop comes out of the soil: a pop tuned to the note (a sweep picks between two, a little tune),
    # a small low tuk of soil for weight, the leaves brushing
    y = G.canvas(0.16)
    G.place(y, thump(250, 140, 0.07, glide=0.025, decay=0.016), 0.0, 0.35)
    G.place(y, tuned(src, name, note), 0.006, 1.0)
    G.place(y, G.grains(rng, 0.08, 7, 900, 3000, length=(0.006, 0.016), decay=0.05), 0.0, 0.10)
    return finish(y, -23, fade_out=0.04)


def clip_harvest_0(src, rng):
    return _harvest(src, rng, "C5", "pop9.ogg")


def clip_harvest_1(src, rng):
    return _harvest(src, rng, "Eb5", "pop6.ogg")


def _plant(src, rng, note, thump_from):
    # a seed pressed into soft soil: the low pat and crumble of gen_sfx's plant, and a small pop where
    # the kalimba tine used to ring
    y = G.canvas(0.2)
    G.place(y, thump(thump_from, thump_from * 0.47, 0.18, glide=0.05, decay=0.045), 0, 0.9)
    G.place(y, G.grains(rng, 0.09, 8, 450, 2200, length=(0.008, 0.02), decay=0.05), 0.004, 0.30)
    G.place(y, tuned(src, "pop4.ogg", note), 0.024, 0.42)
    return finish(y, -24, fade_out=0.05)


def clip_plant_0(src, rng):
    return _plant(src, rng, "F5", 210)


def clip_plant_1(src, rng):
    return _plant(src, rng, "Ab5", 230)


CLIPS = ["tap", "tab", "toggle", "menu_open", "menu_close", "panel_open", "panel_close",
         "harvest_0", "harvest_1", "plant_0", "plant_1"]


def render(src, name):
    rng = np.random.default_rng(G.zlib.crc32(("press:" + name).encode()))
    return globals()["clip_" + name](src, rng)


if __name__ == "__main__":
    args = sys.argv[1:]
    src = os.path.expanduser(args[args.index("--src") + 1] if "--src" in args else "~/.cache/matu-farm/sfx_src/pops")
    if "--fetch" in args:
        fetch(src)
    check(src)
    if "--sha1" in args:
        for n in SOURCES:
            print(f'    "{n}": "{sha1(os.path.join(src, n))}",')
    G.OUT = os.environ.get("SFX_OUT", "Assets/Resources/Audio")
    os.makedirs(G.OUT, exist_ok=True)
    rendered = []
    print(f"   {'clip':14s} {'ms':>5s} {'peak':>6s} {'level':>6s} {'L100':>6s} {'phone':>6s} {'>3k':>5s} {'>5k':>5s}")
    for name in CLIPS:
        path, back = G.write(name, render(src, name))
        rendered.append((name, back))
        pk = 20 * np.log10(np.max(np.abs(back)))
        print(f"   {name:14s} {len(back) / SR * 1000:5.0f} {pk:6.1f} {G.loudness(back):6.1f} {G.l100(back):6.1f} "
              f"{G.l100(G.phone(back)):6.1f} {G.band_db(back, 3000):5.0f} {G.band_db(back, 5000):5.0f}")
    if "--preview" in args:
        G.preview(rendered, args[args.index("--preview") + 1])
