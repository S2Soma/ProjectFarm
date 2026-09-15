"""Art for the start-screen → farm cinematic: the dive through the cloud sea.

    uv run --with pillow --with numpy python Tools/gen_cinematic.py

  Art/cine/cine_veil.png     1024x576   the inside of a sunlit cloud. RGB is the painted light, and
                                        ALPHA IS DENSITY, not coverage: Shaders/UICloudVeil reads it
                                        to burn the veil away thin-parts-first (and to close it the
                                        same way), so the hole opens in cloud shapes instead of a
                                        circle. The sprite is always drawn fully opaque.
  Art/cine/cine_mass_a|b|c   768x768    round billow heaps: the clouds that rush past the camera, and
                                        the curtains that close over the screen and part again
  Art/cine/cine_puff.png     256x256    a small soft puff: the card and the logo turn into these
  Art/cine/cine_shafts.png   1024x1024  soft light shafts from a hollow centre, drawn additively

Painted with the same billow painter as the sky (gen_sky.paint_clouds): noon colours baked in —
white lit tops, lavender bellies — and multiplied at runtime, so the same heap is warm gold in the
dive and takes the farm's own hour as it parts. Screen-space art: the masses and the puff get
mipmaps (they start tiny and end huge), the veil and the shafts do not (FarmTextureImporter).
All original; nothing here is traced or sampled.
"""
import math
import os
import random
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_sky as sky  # noqa: E402  (the billow painter, noise and blur)

OUT = os.environ.get("CINE_OUT", "Assets/Resources/Art/cine")
os.makedirs(OUT, exist_ok=True)
sky.OUT = OUT
SS = sky.SS


def save_straight(rgb_premul, alpha, name, size):
    straight = rgb_premul / np.clip(alpha, 1e-4, 1)[..., None]
    img = Image.fromarray(np.dstack([np.clip(straight, 0, 255), np.clip(alpha * 255, 0, 255)]).astype(np.uint8))
    img = img.resize(size, Image.LANCZOS)
    img.save(os.path.join(OUT, name + ".png"))
    print("  ", name, img.size)


# The heaps and puffs are met up close, in full sun: a higher key than the sky's cumulus, whose
# lavender bellies read as grey marshmallows once a heap fills half the screen.
BRIGHT = dict(
    HI=np.array([255, 255, 255], np.float32),
    WARM=np.array([255, 250, 238], np.float32),
    MID=np.array([246, 245, 250], np.float32),
    SH=np.array([226, 226, 242], np.float32),
    DEEP=np.array([204, 206, 230], np.float32),
)


class bright:
    """Paint with the high-key palette, then put the sky's back."""
    def __enter__(self):
        self.saved = {k: getattr(sky, k) for k in BRIGHT}
        for k, v in BRIGHT.items():
            setattr(sky, k, v)

    def __exit__(self, *a):
        for k, v in self.saved.items():
            setattr(sky, k, v)


def soften(rgb, a, r):
    """Out of focus: the heaps pass close to the lens. Blur premultiplied, so edges stay clean."""
    a2 = sky.blur(a, r)
    rgb2 = np.dstack([sky.blur(rgb[..., c], r) for c in range(3)])
    return rgb2, a2


# ------------------------------------------------------------------ heaps
def mass(variant, seed):
    """A heap seen from any side: one big billow in the middle, a ring of lumps round it, smaller
    ones heaped on top. Not flat-bottomed like the sky's cumulus — these are met in mid-air."""
    S = 768 * SS
    rng = random.Random(seed)
    c = S / 2
    billows = [(c + rng.uniform(-0.02, 0.02) * S, c + 0.04 * S, 0.24 * S)]
    ring = {"a": 8, "b": 7, "c": 9}[variant]
    stretch = {"a": (1.0, 0.86), "b": (0.86, 1.0), "c": (1.08, 0.78)}[variant]
    for i in range(ring):
        ang = i / ring * math.tau + rng.uniform(-0.25, 0.25)
        rad = rng.uniform(0.20, 0.27) * S
        r = rng.uniform(0.11, 0.16) * S
        billows.append((c + math.cos(ang) * rad * stretch[0], c + math.sin(ang) * rad * stretch[1], r))
    # a crown of smaller billows on the upper, sunlit half
    for _ in range(rng.randint(4, 6)):
        ang = rng.uniform(math.pi * 1.05, math.pi * 1.95)
        rad = rng.uniform(0.22, 0.33) * S
        billows.append((c + math.cos(ang) * rad * stretch[0], c + math.sin(ang) * rad * stretch[1], rng.uniform(0.07, 0.10) * S))
    # and a few tucked into the lower edge, so the outline is not a clean ring
    for _ in range(rng.randint(2, 4)):
        ang = rng.uniform(0.15 * math.pi, 0.85 * math.pi)
        rad = rng.uniform(0.26, 0.32) * S
        billows.append((c + math.cos(ang) * rad * stretch[0], c + math.sin(ang) * rad * stretch[1], rng.uniform(0.06, 0.09) * S))
    with bright():
        rgb, a = sky.paint_clouds(S, S, billows, seed=seed)
    rgb, a = soften(rgb, a, 2.2 * SS)
    save_straight(rgb, a, "cine_mass_" + variant, (768, 768))


def puff():
    S = 256 * SS
    rng = random.Random(29)
    c = S / 2
    billows = [(c, c + 0.05 * S, 0.22 * S)]
    for i in range(6):
        ang = i / 6 * math.tau + rng.uniform(-0.3, 0.3)
        billows.append((c + math.cos(ang) * 0.19 * S, c + math.sin(ang) * 0.15 * S, rng.uniform(0.12, 0.17) * S))
    with bright():
        rgb, a = sky.paint_clouds(S, S, billows, seed=31)
    rgb, a = soften(rgb, a, 3.0 * SS)
    save_straight(rgb, a, "cine_puff", (256, 256))


# ------------------------------------------------------------------ the veil
def veil():
    """A frame packed with billows, lit from the upper left, over a pale body — and a density map
    that is highest in each billow's core. The dissolve burns the valleys between billows first,
    so the hole it opens has the outline of the clouds themselves."""
    W, H = 1024 * SS, 576 * SS
    rng = random.Random(71)
    billows = []
    for count, rmin, rmax in [(12, 150, 230), (34, 76, 132), (58, 34, 64)]:
        for _ in range(count):
            billows.append((rng.uniform(-0.06, 1.06) * W, rng.uniform(-0.08, 1.08) * H, rng.uniform(rmin, rmax) * SS))
    rgb, a = sky.paint_clouds(W, H, billows, seed=73)
    # the body under the billows: the pale mid tone, so nothing is ever transparent
    body = sky.MID[None, None, :] * (0.97 + 0.05 * sky.value_noise(H, W, 120 * SS, 74)[..., None])
    rgb = rgb + body * (1 - a[..., None])
    rgb, _ = soften(rgb, np.ones((H, W), np.float32), 1.2 * SS)

    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    dens = np.zeros((H, W), np.float32)
    rtop = max(b[2] for b in billows)
    for cx, cy, r in billows:
        x0, x1 = int(max(0, cx - r)), int(min(W, cx + r))
        y0, y1 = int(max(0, cy - r)), int(min(H, cy + r))
        if x1 <= x0 or y1 <= y0:
            continue
        d = np.hypot(xs[y0:y1, x0:x1] - cx, ys[y0:y1, x0:x1] - cy) / r
        dome = np.sqrt(np.clip(1 - d * d, 0, 1)) * (0.55 + 0.45 * (r / rtop) ** 0.5)
        dens[y0:y1, x0:x1] = np.maximum(dens[y0:y1, x0:x1], dome)
    # little blur: the burn edge should follow the billows' outlines, not a smudge of them
    dens = sky.blur(dens, 3 * SS)
    n1 = sky.value_noise(H, W, 46 * SS, 75)
    n2 = sky.value_noise(H, W, 15 * SS, 76)
    dens = dens * 0.80 + n1 * 0.14 + n2 * 0.06
    lo, hi = np.percentile(dens, 1), np.percentile(dens, 99.5)
    dens = np.clip((dens - lo) / (hi - lo), 0, 1)
    dens = 0.03 + 0.97 * dens

    # colour and density resized apart: resizing RGBA premultiplies, and un-premultiplying where the
    # density is near zero posterized the colour there into cyan and violet blotches
    col = Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).resize((1024, 576), Image.LANCZOS)
    den = Image.fromarray((dens * 255).astype(np.uint8)).resize((1024, 576), Image.LANCZOS)
    img = Image.merge("RGBA", (*col.split(), den))
    img.save(os.path.join(OUT, "cine_veil.png"))
    print("   cine_veil", img.size)


# ------------------------------------------------------------------ light
def shafts(S=1024):
    """Shafts of light from a hollow centre: few, of uneven width and reach, soft on both edges.
    A regular sunburst reads as a drawn star; uneven shafts read as light through gaps in cloud."""
    ys, xs = np.mgrid[0:S, 0:S].astype(np.float32)
    c = S / 2 - 0.5
    dx, dy = xs - c, ys - c
    r = np.hypot(dx, dy) / (S / 2)
    ang = np.arctan2(dy, dx)
    rng = random.Random(8)
    a = np.zeros((S, S), np.float32)
    n = 11
    for i in range(n):
        a0 = i / n * math.tau + rng.uniform(-0.22, 0.22)
        width = math.radians(rng.uniform(4.0, 11.0))
        reach = rng.uniform(0.6, 1.0)
        power = rng.uniform(0.3, 0.9)
        d = np.abs((ang - a0 + math.pi) % math.tau - math.pi)
        # wider toward the outside, the way a shaft spreads
        w = width * (0.7 + 0.6 * r)
        across = np.exp(-(d / w) ** 2)
        along = sky.smoothstep(0.06, 0.34, r) * (1 - sky.smoothstep(reach * 0.25, reach, r))
        a = a + across * along * power * (1 - a)
    a = sky.blur(a, 6.0)
    img = Image.fromarray(np.dstack([np.full((S, S, 3), 255, np.uint8), (np.clip(a, 0, 1) * 255).astype(np.uint8)]))
    img.save(os.path.join(OUT, "cine_shafts.png"))
    print("   cine_shafts", img.size)


if __name__ == "__main__":
    print("cinematic ->", OUT)
    veil()
    for v, s in (("a", 41), ("b", 43), ("c", 47)):
        mass(v, s)
    puff()
    shafts()
