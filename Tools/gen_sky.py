"""The sky: a sea of clouds under the archipelago, cumulus, sun, moon and stars.

    uv run --with pillow --with numpy python Tools/gen_sky.py

Why
---
The old background was a flat sea with foam ovals under islands that float, a blue dome of a
hill that read as a planet, 200x120 painted clouds upscaled 2.2x and sliced flat by the
horizon, and every colour baked into two ramps that could only be multiplied — so a dusk or a
night could only ever go muddy.

Everything here is drawn GREYSCALE (the lighting) with alpha, and coloured at runtime by vertex
colour: the same cloud is white at noon, peach at dawn and indigo at night, from one texture.
No outlines; shapes are unions of circles lit as domes, the same trick the islands and crops use.
Drawn at 2x and reduced with Lanczos. Screen-space art, so no mipmaps (see FarmTextureImporter).
"""
from PIL import Image
import numpy as np
import math, os, random

OUT = "Assets/Resources/Art/sky"
os.makedirs(OUT, exist_ok=True)
SS = 2   # supersample


def blur(m, r, wrap_x=False):
    """Float Gaussian-ish blur (three box passes per axis); optional horizontal wrap for tiles."""
    sigma = max(0.5, r)
    w = max(1, int(round(math.sqrt(12.0 * sigma * sigma / 3.0 + 1.0))))
    out = np.asarray(m, np.float32)
    for axis in (0, 1):
        for _ in range(3):
            pad = [(0, 0), (0, 0)]
            pad[axis] = (w // 2 + 1, w - w // 2)
            mode = "wrap" if (wrap_x and axis == 1) else "edge"
            c = np.cumsum(np.pad(out, pad, mode=mode), axis=axis, dtype=np.float64)
            if axis == 0:
                out = ((c[w:, :] - c[:-w, :]) / w)[: out.shape[0], :].astype(np.float32)
            else:
                out = ((c[:, w:] - c[:, :-w]) / w)[:, : out.shape[1]].astype(np.float32)
    return out


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


LIGHT = np.array([0.25, -0.9, 0.55], np.float32)
LIGHT /= np.linalg.norm(LIGHT)


def lit_circles(W, H, circles, blur_r, base_y=None, wrap=False, fill_down=False, darken_down=0.0, creases=True):
    """Union of circles lit as a soft dome. Returns (lum, alpha) at W x H."""
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    mask = np.zeros((H, W), np.float32)
    depths = []
    for (cx, cy, r) in circles:
        d = np.hypot(xs - cx, ys - cy)
        a = np.clip(r - d + 0.5, 0, 1)
        if fill_down:
            a = np.maximum(a, ((ys >= cy) & (np.abs(xs - cx) <= r)).astype(np.float32))
        mask = np.maximum(mask, a)
        depths.append((r - d, r, cy))
    if base_y is not None:
        # a soft flat base: cumulus are flat-bottomed, but a hard edge read as the cloud cut off
        mask *= smoothstep(0, 26 * SS, base_y - ys)

    h = blur(mask, blur_r, wrap_x=wrap)
    h = np.clip((h - 0.2) / 0.8, 0, 1) ** 0.7
    gy, gx = np.gradient(h)
    k = blur_r * 2.0
    n = np.dstack([-gx * k, -gy * k, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    # image coordinates throughout: y grows downward, so an up-facing normal and the light both
    # have negative y
    diffuse = np.clip(n @ LIGHT, 0, 1)
    lum = 0.82 + 0.18 * diffuse

    # Creases: the upper outline of each lump where it sits IN FRONT of a higher one — a soft
    # shadow line tracing the nearer lump over the farther. That is what makes a cloud read as
    # heaped. (A crease wherever two lumps are "equally inside" drew dark vertical streaks.)
    crease = np.zeros((H, W), np.float32)
    for i, (di, ri, cyi) in enumerate(depths if creases else []):
        behind = np.zeros((H, W), bool)
        for j, (dj, rj, cyj) in enumerate(depths):
            if j != i and cyj < cyi - 0.1 * ri:
                behind |= dj > 0.12 * rj
        line = np.exp(-(di / (0.07 * ri)) ** 2) * behind * (ys < cyi) * (di > -0.10 * ri)
        crease = np.maximum(crease, line)
    # wide and faint: at a few pixels wide these read as scratches drawn on the cloud
    lum -= 0.05 * blur(crease, 6 * SS, wrap_x=wrap)

    # a bright rim on up-facing edges, and a shaded underside
    edge = np.clip(mask - blur(mask, 3 * SS), 0, 1)
    lum += 0.06 * edge * (n[..., 1] < 0)
    rows = np.arange(H, dtype=np.float32)[:, None]
    top = np.argmax(mask > 0.5, axis=0).astype(np.float32)[None, :]
    bottom = (H - np.argmax(mask[::-1] > 0.5, axis=0)).astype(np.float32)[None, :]
    span = np.maximum(bottom - top, 1)
    t_down = np.clip((rows - top) / span, 0, 1)
    lum -= 0.09 * smoothstep(0.80, 1.0, t_down)
    if darken_down:
        lum *= 1 - darken_down * t_down
    return np.clip(lum, 0, 1), np.clip(mask, 0, 1)


def save_la(lum, alpha, name, crop=False, size=None):
    rgb = (np.clip(lum, 0, 1) * 255).astype(np.uint8)
    a = (np.clip(alpha, 0, 1) * 255).astype(np.uint8)
    img = Image.fromarray(np.dstack([rgb, rgb, rgb, a]))
    if crop:
        bb = img.getbbox()
        pad = 6 * SS
        img = img.crop((max(0, bb[0] - pad), max(0, bb[1] - pad), min(img.width, bb[2] + pad), min(img.height, bb[3] + pad)))
    if size is None:
        size = (img.width // SS, img.height // SS)
    img = img.resize(size, Image.LANCZOS)
    img.save(os.path.join(OUT, name + ".png"))
    print("  ", name, img.size)


# ------------------------------------------------------------------ painted clouds
# The first version lit each cloud as one smooth dome with a specular-like falloff: it read as
# moulded plastic next to the painted islands. Clouds are now PAINTED, billow by billow, back to
# front: each billow is a ball with its own soft two-step shading (lit, mid, shadow), a wobbly
# edge, brush mottling, a warm rim where it faces the light and a cool lavender underside. The
# definition comes from a lit billow top laid over the shaded belly of the one behind it — the
# way a cumulus is actually painted — so there are no drawn crease lines.
#
# Colours are baked for noon (white highlights, lavender shadows) and multiplied at runtime by
# the time of day, so a dusk turns highlights peach and shadows violet.
HI = np.array([255, 255, 255], np.float32)
WARM = np.array([255, 249, 236], np.float32)
MID = np.array([234, 240, 250], np.float32)
SH = np.array([200, 208, 234], np.float32)
DEEP = np.array([168, 178, 214], np.float32)
PAINT_LIGHT = np.array([-0.40, -0.72, 0.56], np.float32)
PAINT_LIGHT /= np.linalg.norm(PAINT_LIGHT)


def value_noise(h, w, cell, seed, wrap=False):
    """Smooth noise. With wrap, it repeats exactly every w pixels: the grid has a whole number of
    cells across w, is tiled three times, upscaled, and the middle copy is kept — so the right
    edge of a cloud band continues into its own left edge with no seam."""
    rng = np.random.RandomState(seed)
    gh = int(h / cell) + 3
    if not wrap:
        gw = int(w / cell) + 3
        g = rng.rand(gh, gw).astype(np.float32)
        img = Image.fromarray((g * 255).astype(np.uint8)).resize((int(gw * cell), int(gh * cell)), Image.BICUBIC)
        return (np.asarray(img, np.float32) / 255.0)[:h, :w]
    gc = max(2, int(round(w / cell)))
    g = rng.rand(gh, gc).astype(np.float32)
    g3 = np.concatenate([g, g, g], axis=1)
    img = Image.fromarray((g3 * 255).astype(np.uint8)).resize((3 * w, int(gh * w / gc)), Image.BICUBIC)
    a = np.asarray(img, np.float32) / 255.0
    return a[:h, w:2 * w]


def paint_clouds(W, H, billows, wrap=False, body_below=False, base_y=None, seed=1):
    rgb = np.zeros((H, W, 3), np.float32)
    alpha = np.zeros((H, W), np.float32)
    wobble = value_noise(H, W, 34 * SS, seed, wrap)
    mottle = value_noise(H, W, 14 * SS, seed + 1, wrap)
    patches = value_noise(H, W, 90 * SS, seed + 2, wrap)

    def over(y0, y1, x0, x1, col, a):
        sub_a = alpha[y0:y1, x0:x1]
        rgb[y0:y1, x0:x1] = col * a[..., None] + rgb[y0:y1, x0:x1] * (1 - a[..., None])
        alpha[y0:y1, x0:x1] = a + sub_a * (1 - a)

    items = list(billows)
    if wrap:
        items = items + [(cx - W, cy, r) for cx, cy, r in billows] + [(cx + W, cy, r) for cx, cy, r in billows]
    items.sort(key=lambda b: b[1])

    # a band's solid body under its billows, painted first so the billows sit on it
    if body_below:
        ys = np.arange(H, dtype=np.float32)[:, None]
        xs = np.arange(W, dtype=np.float32)[None, :]
        top = np.full(W, float(H), np.float32)
        for cx, cy, r in items:
            x0, x1 = int(max(0, cx - r)), int(min(W, cx + r))
            if x1 <= x0:
                continue
            xs1 = np.arange(x0, x1, dtype=np.float32)
            # the body follows the billow's round bottom half, not a box under it
            prof = cy + 0.35 * r - 0.35 * r * np.sqrt(np.clip(1 - ((xs1 - cx) / r) ** 2, 0, 1))
            top[x0:x1] = np.minimum(top[x0:x1], prof)
        # soften the steps between neighbouring billows
        k = int(24 * SS)
        top = np.convolve(np.concatenate([top[-k:], top, top[:k]]), np.ones(k) / k, mode="same")[k:-k].astype(np.float32)
        a = smooth_np(top[None, :], top[None, :] + 3 * SS, ys)
        t = np.clip((ys - top[None, :]) / max(1.0, H * 0.7), 0, 1)
        col = SH[None, None, :] * (1 - t[..., None]) + DEEP[None, None, :] * t[..., None]
        col = col * (0.97 + 0.06 * patches[..., None])
        over(0, H, 0, W, col, a)

    for cx, cy, r in items:
        pad = r * 1.25
        x0, x1 = int(max(0, cx - pad)), int(min(W, cx + pad))
        y0, y1 = int(max(0, cy - pad)), int(min(H, cy + pad))
        if x1 <= x0 or y1 <= y0:
            continue
        ys, xs = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        dx, dy = (xs - cx) / r, (ys - cy) / r
        d = np.hypot(dx, dy)
        d_eff = d + (wobble[y0:y1, x0:x1] - 0.5) * 0.07
        a = smooth_np(1.0, 1.0 - 2.6 * SS / r, d_eff)
        if base_y is not None:
            a = a * smooth_np(base_y, base_y - 14 * SS, ys)
        if a.max() <= 0:
            continue
        nz = np.sqrt(np.clip(1 - np.minimum(d, 1) ** 2, 0, 1))
        diff = np.clip(dx * PAINT_LIGHT[0] + dy * PAINT_LIGHT[1] + nz * PAINT_LIGHT[2], 0, 1)
        # two soft painted steps instead of a smooth ramp
        lit = smooth_np(0.18, 0.48, diff)
        top_lit = smooth_np(0.60, 0.88, diff)
        col = SH[None, None, :] * (1 - lit[..., None]) + MID[None, None, :] * lit[..., None]
        col = col * (1 - top_lit[..., None]) + HI[None, None, :] * top_lit[..., None]
        # the belly of every billow cools toward the deep shadow
        belly = smooth_np(0.20, 1.0, dy) * 0.45
        col = col * (1 - belly[..., None]) + DEEP[None, None, :] * belly[..., None]
        # warm rim where the edge faces the light
        rim = smooth_np(0.86, 1.0, d_eff) * smooth_np(0.1, 0.6, -(dx * 0.4 + dy * 0.9))
        col = col * (1 - 0.35 * rim[..., None]) + WARM[None, None, :] * (0.35 * rim)[..., None]
        col = col * (0.985 + 0.03 * mottle[y0:y1, x0:x1][..., None])
        col = col * (0.98 + 0.04 * patches[y0:y1, x0:x1][..., None])
        over(y0, y1, x0, x1, col, a)

    if base_y is not None:
        # a cumulus's flat base is its darkest part
        ys = np.arange(H, dtype=np.float32)[:, None]
        k = smooth_np(base_y - 40 * SS, base_y, ys) * 0.45
        rgb[:] = rgb * (1 - k[..., None]) + DEEP[None, None, :] * (k * alpha)[..., None]
    return rgb, alpha


def smooth_np(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def save_rgba(rgb, alpha, name, crop=False):
    # paint_clouds composites PREMULTIPLIED; writing that straight out left every anti-aliased edge
    # darkened by its own coverage — a thin grey outline round every billow
    straight = rgb / np.clip(alpha, 1e-4, 1)[..., None]
    img = Image.fromarray(np.dstack([np.clip(straight, 0, 255), np.clip(alpha * 255, 0, 255)]).astype(np.uint8))
    if crop:
        bb = img.getbbox()
        p = 6 * SS
        img = img.crop((max(0, bb[0] - p), max(0, bb[1] - p), min(img.width, bb[2] + p), min(img.height, bb[3] + p)))
    img = img.resize((img.width // SS, img.height // SS), Image.LANCZOS)
    img.save(os.path.join(OUT, name + ".png"))
    print("  ", name, img.size)


def cumulus(variant):
    W, H = 1024 * SS, 640 * SS
    rng = random.Random({"a": 3, "b": 5, "c": 9}[variant])
    base = 0.88 * H
    cx = W / 2
    billows = []
    # (count, radius as a share of width, half-spread, height above the base) per tier — fewer
    # and larger billows than the first pass, whose even grid of little balls read as cauliflower
    if variant == "a":        # wide heap
        rows = [(5, 0.110, 0.32, 0.0), (3, 0.150, 0.20, 0.10), (1, 0.175, 0.04, 0.24)]
    elif variant == "b":      # a tower
        rows = [(3, 0.125, 0.17, 0.0), (2, 0.135, 0.09, 0.16), (1, 0.130, 0.03, 0.34)]
    else:                     # low and long
        rows = [(6, 0.090, 0.38, 0.0), (3, 0.115, 0.22, 0.07), (1, 0.12, 0.05, 0.15)]
    for count, rr, half, lift in rows:
        for i in range(count):
            u = (i / max(1, count - 1) - 0.5) * 2 if count > 1 else 0
            r = rr * W * rng.uniform(0.85, 1.12)
            x = cx + u * half * W + rng.uniform(-0.03, 0.03) * W
            y = base - r * 0.60 - lift * W + rng.uniform(-0.015, 0.015) * W
            billows.append((x, y, r))
    rgb, a = paint_clouds(W, H, billows, base_y=base, seed={"a": 11, "b": 12, "c": 13}[variant])
    save_rgba(rgb, a, "cumulus_" + variant, crop=True)


def band(name, H, R, seed, fade_bottom=True, haze=0.0, soften=0.0):
    """A seamless strip of the cloud sea.

    Heaps, not a row. The first version walked along the strip dropping one billow of roughly the
    same size every ~R pixels at roughly the same height: from any distance it read as a scalloped
    border, and three of them stacked read as three borders, not as depth. Now the strip is a run
    of HEAPS of very different widths (0.55 to 1.8 R), each a mound of billows tallest in its
    middle, with a lower, smaller back row showing in the valleys between them.

    ``haze`` pulls the colours toward the pale mid tone and ``soften`` blurs the edges: the far
    band gets both, which is the aerial perspective that makes the near band look near."""
    W = 2048 * SS
    H *= SS
    R *= SS
    rng = random.Random(seed)
    billows = []
    # back row: small billows high in the strip, seen only between the heaps
    x = 0.0
    while x < W:
        r = R * rng.uniform(0.45, 0.75)
        billows.append((x, max(r * 1.02, 0.34 * H + R * rng.uniform(-0.05, 0.12)), r))
        x += r * rng.uniform(1.3, 2.0)
    # heaps
    x = rng.uniform(0, R)
    while x < W:
        s = rng.uniform(0.55, 1.8)
        span = R * s * rng.uniform(1.6, 2.4)
        n = max(2, int(span / (R * 0.85)))
        peak = rng.uniform(0.25, 0.75)
        for i in range(n):
            u = i / (n - 1)
            hump = 1 - abs(u - peak) / max(peak, 1 - peak)
            r = R * s * rng.uniform(0.62, 0.86) * (0.7 + 0.4 * hump)
            cx = x + u * span + rng.uniform(-0.12, 0.12) * R
            cy = 0.62 * H - hump * 0.28 * H * min(1.0, s) + rng.uniform(-0.04, 0.06) * H
            billows.append((cx, max(r * 1.02, cy), r))
        # a couple of small billows in front at the heap's foot
        for _ in range(rng.randint(0, 1)):
            cr = R * rng.uniform(0.38, 0.5)
            billows.append((x + rng.uniform(0.1, 0.9) * span, 0.72 * H + rng.uniform(0, 0.1) * H, cr))
        x += span * rng.uniform(0.7, 1.0)
    rgb, a = paint_clouds(W, H, billows, wrap=True, body_below=True, seed=seed)
    if haze > 0:
        straight = rgb / np.clip(a, 1e-4, 1)[..., None]
        straight = straight * (1 - haze) + MID[None, None, :] * haze
        rgb = straight * a[..., None]
    if soften > 0:
        a2 = blur(a, soften * SS, wrap_x=True)
        rgb = np.dstack([blur(rgb[..., c], soften * SS, wrap_x=True) for c in range(3)])
        a = a2
    if fade_bottom:
        # the band's lower edge dissolves into the one in front, instead of ending on a line
        ys = np.arange(H, dtype=np.float32)[:, None]
        k = (1 - smooth_np(0.66 * H, 0.98 * H, ys))
        a = a * k
        rgb = rgb * k[..., None]
    save_rgba(rgb, a, name)


def wisps(name, H, seed, count, length, thick, alpha_max, y_lo, y_hi):
    """Stratus streaks: long, thin, soft-edged clouds lying between the heaps. Nothing says
    'there is a lot of air between here and there' like a haze layer you can see through."""
    W = 2048 * SS
    H *= SS
    rng = random.Random(seed)
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    a = np.zeros((H, W), np.float32)
    for _ in range(count):
        cx = rng.uniform(0, W)
        cy = rng.uniform(y_lo, y_hi) * H
        L = length * SS * rng.uniform(0.6, 1.4)
        T = thick * SS * rng.uniform(0.6, 1.3)
        for off in (-W, 0, W):
            dx = (xs - cx - off) / L
            dy = (ys - cy) / T
            m = np.exp(-(dx ** 2) * 2.2 - dy ** 2 * 2.0) * rng.uniform(0.55, 1.0)
            a = np.maximum(a, m)
    a = np.clip(a * alpha_max * (0.85 + 0.3 * value_noise(H, W, 60 * SS, seed, wrap=True)), 0, 1)
    lum = 0.93 + 0.07 * (1 - ys / H)
    rgb = (MID[None, None, :] * (1 - lum[..., None]) + HI[None, None, :] * lum[..., None]) * a[..., None]
    save_rgba(rgb, a, name)


# ------------------------------------------------------------------ sun, moon, stars
def disc(name, S, r, base, edge_to, maria=False):
    S *= SS; r *= SS
    ys, xs = np.mgrid[0:S, 0:S].astype(np.float32)
    c = S / 2
    d = np.hypot(xs - c, ys - c)
    a = np.clip((r - d) / (2 * SS) + 0.5, 0, 1)
    lum = base - (base - edge_to) * np.clip(d / r, 0, 1) ** 2
    if maria:
        rng = random.Random(5)
        for _ in range(5):
            mx, my = c + rng.uniform(-0.5, 0.5) * r, c + rng.uniform(-0.5, 0.5) * r
            mr = rng.uniform(0.14, 0.30) * r
            m = np.exp(-(np.hypot(xs - mx, ys - my) / mr) ** 2)
            lum -= (base - 0.84) * m
        rim = np.clip(1 - np.hypot(xs - (c - 0.3 * r), ys - (c - 0.3 * r)) / r, 0, 1)
        lum += 0.04 * rim
    save_la(np.clip(lum, 0, 1), a, name, size=(S // SS, S // SS))


def star(name, S, glint):
    S *= SS
    ys, xs = np.mgrid[0:S, 0:S].astype(np.float32)
    c = S / 2 - 0.5
    d = np.hypot(xs - c, ys - c)
    if glint:
        L, sg = 30 * SS, 1.6 * SS
        a = np.exp(-((ys - c) / sg) ** 2) * np.clip(1 - np.abs(xs - c) / L, 0, 1) ** 1.5
        a = np.maximum(a, np.exp(-((xs - c) / sg) ** 2) * np.clip(1 - np.abs(ys - c) / L, 0, 1) ** 1.5)
        a = np.maximum(a, np.exp(-(d / (3 * SS)) ** 2))
    else:
        a = np.maximum(np.exp(-(d / (3 * SS)) ** 2) * 0.8, np.clip(1.2 * SS - d + 0.5, 0, 1))
    save_la(np.ones_like(a), np.clip(a, 0, 1), name, size=(S // SS, S // SS))


def sun_rays(S=512):
    """Twelve soft wedges fading out from the disc: shafts of light round the sun, rotated slowly
    at runtime. Soft enough that they read as light, not as a drawn star."""
    ys, xs = np.mgrid[0:S, 0:S].astype(np.float32)
    c = S / 2 - 0.5
    dx, dy = xs - c, ys - c
    r = np.hypot(dx, dy) / (S / 2)
    ang = np.arctan2(dy, dx)
    wedge = np.zeros_like(r)
    rng = random.Random(4)
    for i in range(12):
        a0 = i * math.pi / 6 + rng.uniform(-0.08, 0.08)
        width = math.radians(rng.uniform(6, 11))
        d = np.abs((ang - a0 + math.pi) % (2 * math.pi) - math.pi)
        wedge = np.maximum(wedge, np.clip(1 - d / width, 0, 1) ** 1.5 * rng.uniform(0.6, 1.0))
    radial = smooth_np(0.18, 0.30, r) * (1 - smooth_np(0.45, 1.0, r))
    a = np.clip(wedge * radial, 0, 1) * 0.6
    img = Image.fromarray(np.dstack([np.full((S, S, 3), 255, np.uint8), (a * 255).astype(np.uint8)]))
    img.save(os.path.join(OUT, "sun_rays.png"))
    print("   sun_rays", img.size)


if __name__ == "__main__":
    print("sky ->", OUT)
    for v in "abc":
        cumulus(v)
    band("sea_far", 150, 34, 101, haze=0.35, soften=1.6)
    band("sea_mid", 230, 70, 202, haze=0.12, soften=0.6)
    band("sea_near", 320, 130, 303, fade_bottom=False)
    wisps("sea_wisps", 140, 404, 14, 260, 12, 0.55, 0.25, 0.8)
    wisps("cirrus", 160, 505, 10, 380, 7, 0.32, 0.2, 0.85)
    disc("sun_disc", 256, 112, 1.0, 0.94)
    disc("moon_disc", 256, 104, 0.96, 0.90, maria=True)
    star("star_dot", 32, False)
    star("star_glint", 64, True)
    sun_rays()
