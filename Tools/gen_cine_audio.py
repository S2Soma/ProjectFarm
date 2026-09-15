"""Two sounds for the start-screen → farm cinematic, synthesised from nothing (no samples).

    uv run --with numpy --with soundfile python Tools/gen_cine_audio.py

  Audio/cine_whoosh.ogg  1.4 s  air rushing past: noise through a band-pass whose centre climbs as
                                the clouds close in and falls away as they part, a slow swell and a
                                soft tail. Quiet on purpose — it sits under the claim blip. Low-passed
                                at ~4 kHz (2026-09-15: the unfiltered hiss was the brightest sound left).
  Audio/cine_chime.ogg   1.9 s  the reveal: four soft bell tones rising (Ab5 C6 Eb6 Ab6 — A-flat, the
                                key of the background music and of Tools/gen_sfx.py), inharmonic
                                partials for a glassy edge (none above 7 kHz), a long gentle decay.

Mono, 44.1 kHz, peaks well under full scale; Sfx.Table sets the playing volume on top.
"""
import os

import numpy as np
import soundfile as sf

OUT = "Assets/Resources/Audio"
SR = 44100
rng = np.random.default_rng(20260915)


def svf_bandpass(x, fc, q):
    """Chamberlin state-variable filter with a per-sample cutoff (Hz)."""
    y = np.zeros_like(x)
    low = band = 0.0
    damp = 1.0 / q
    for i in range(len(x)):
        f = 2.0 * np.sin(np.pi * min(fc[i], SR / 6) / SR)
        high = x[i] - low - damp * band
        band += f * high
        low += f * band
        y[i] = band
    return y


def smooth(x, fc, poles=4):
    """Cascaded one-pole low-passes: a gentle roll-off with no ringing."""
    a = np.exp(-2 * np.pi * fc / SR)
    for _ in range(poles):
        y = np.zeros_like(x)
        acc = 0.0
        for i in range(len(x)):
            acc = a * acc + (1 - a) * x[i]
            y[i] = acc
        x = y
    return x


def whoosh():
    n = int(1.4 * SR)
    t = np.arange(n) / SR
    noise = rng.standard_normal(n).astype(np.float64)
    # pink-ish: a gentle one-pole low-pass under the white noise, so it is air, not hiss
    pink = np.zeros(n)
    acc = 0.0
    for i in range(n):
        acc = 0.985 * acc + 0.015 * noise[i]
        pink[i] = acc
    src = 0.35 * noise + 4.0 * pink

    peak_t = 0.72
    # the band climbs toward the peak, then sinks as the cloud parts
    rise = np.clip(t / peak_t, 0, 1)
    fall = np.clip((t - peak_t) / (1.4 - peak_t), 0, 1)
    fc = 260 + 1500 * rise ** 1.6 * (1 - fall) + 520 * fall * (1 - fall)
    fc = np.maximum(fc, 180)
    body = svf_bandpass(src, fc, 0.9)
    air = svf_bandpass(src, fc * 2.6, 1.6) * 0.35

    # swell in (eased), soft exponential tail
    env = np.where(t < peak_t, (t / peak_t) ** 2.4, np.exp(-(t - peak_t) / 0.22))
    # a slow flutter, the way a gust is never steady
    env *= 1 + 0.12 * np.sin(2 * np.pi * 5.3 * t + 0.6) * np.clip(t / 0.4, 0, 1)
    y = smooth((body + air) * env, 4200)
    fade = np.clip((1.4 - t) / 0.08, 0, 1)
    y *= fade
    return y / np.max(np.abs(y)) * 0.42


def chime():
    n = int(1.9 * SR)
    t = np.arange(n) / SR
    y = np.zeros(n)
    notes = [830.61, 1046.50, 1244.51, 1661.22]
    for k, f0 in enumerate(notes):
        start = 0.0 + k * 0.085
        tt = t - start
        on = tt >= 0
        tt = np.where(on, tt, 0)
        attack = np.clip(tt / 0.006, 0, 1)
        tone = np.zeros(n)
        # a struck glass bar: the fundamental, a quiet octave, and two inharmonic partials that die fast
        for ratio, amp, decay in ((1.0, 1.0, 0.95), (2.0, 0.22, 0.55), (2.76, 0.16, 0.28), (5.40, 0.07, 0.12)):
            if f0 * ratio >= 7000:
                continue
            tone += amp * np.sin(2 * np.pi * f0 * ratio * tt + k) * np.exp(-tt / decay)
        level = [0.75, 0.85, 0.95, 1.0][k]
        y += np.where(on, tone * attack * level, 0)
    # a touch of shimmer, and the ends softened
    y *= 1 + 0.06 * np.sin(2 * np.pi * 6.0 * t)
    y *= np.clip((1.9 - t) / 0.25, 0, 1)
    return y / np.max(np.abs(y)) * 0.38


if __name__ == "__main__":
    OUT = os.environ.get("CINE_AUDIO_OUT", OUT)
    os.makedirs(OUT, exist_ok=True)
    for name, data in (("cine_whoosh", whoosh()), ("cine_chime", chime())):
        path = os.path.join(OUT, name + ".ogg")
        sf.write(path, data.astype(np.float32), SR, format="OGG", subtype="VORBIS")
        print("  ", path, f"{len(data) / SR:.2f}s")
