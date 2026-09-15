"""The sky: a sea of clouds under the archipelago, cumulus, cirrus, sun, moon and stars.

    uv run --with pillow --with numpy python Tools/gen_sky.py            # everything
    uv run --with pillow --with numpy python Tools/gen_sky.py sea_near   # one sprite

Everything here is drawn GREYSCALE with alpha and coloured at runtime: the same cloud is white at
noon, gold on top and lavender underneath at golden hour, indigo at night, from one texture.

Clouds (2026-09-15 evening, "mây bị thô, đơ, không mềm mại")
------------------------------------------------------------
The first painted clouds were unions of perfect circles, each billow shaded in two hard steps with
lavender baked into its shadow, and multiplied by the hour's colour at runtime. Seen from the whole
map they read as stacked stone balls, and at golden hour the multiply turned every shadow brown.

Now the grey value of a cloud sprite is LIGHT, not colour: 1 is the sunlit top, 0 the deepest
shadow, and the shader (Resources/Shaders/UICloud.shader) maps it onto a ramp between the hour's
shade colour and its light colour — warm tops, cool lavender shadows, never brown. Alpha is soft
density with a wide feathered edge, which the shader breathes with slowly moving noise.

How a cloud is painted (``soft_domes``):
  * a heap is LOBES with buds on their upper surfaces, three scales deep (``lobes``), every lobe an
    ellipsoidal dome, sampled through a low-frequency warp so no outline is a true circle;
  * the silhouette is a soft union of the domes' signed distances, roughened with noise, feathered,
    with faint wisps trailing off it;
  * the light is a SOFT painter's algorithm: each pixel blends the lit domes nearest the front,
    weighted by depth relative to each dome's own size — no crease lines, no marbles;
  * big forms: the heap's heights blurred to heap scale and lit on their own, so light and shade
    fall across whole heaps and the billows only model detail inside them;
  * self-shadow toward the base, a silver lining on thin sunlit edges, and five soft value steps.

The cloud sea is ROWS of rolling heaps (``band``): each row a continuous mass with heaps of very
different widths and heights along its top, sinking into its own shade toward its base, laid back
to front; flattened ellipses, because the sea is seen at a grazing angle. Every band wraps
horizontally (noise with ``wrap=True``, billows repeated at +-W, wrapped blurs).

Composite premultiplied, un-premultiply when saving, and fill fully transparent texels with nearby
light — straight-alpha writes left a grey fringe round every billow. Screen-space art: no mipmaps
(FarmTextureImporter).

``cloud_noise.png`` is the shader's noise: two tileable octaves in R and G, stored sRGB-encoded so
the sampled (linearised) value is uniform.

The old billow painter (``paint_clouds``) stays below: Tools/gen_cinematic.py still paints the
dive's heaps and veil with it.
"""
from PIL import Image
import numpy as np
import math, os, random, sys

OUT = os.environ.get("SKY_OUT", "Assets/Resources/Art/sky")
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


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def fbm(h, w, cell, seed, octaves=4, wrap=False, gain=0.5):
    tot = np.zeros((h, w), np.float32)
    amp, norm, c = 1.0, 0.0, float(cell)
    for o in range(octaves):
        if c < 2:
            break
        tot += amp * value_noise(h, w, c, seed + 31 * o, wrap)
        norm += amp
        amp *= gain
        c /= 2.0
    return tot / norm



def soft_steps(v, n, soft):
    """Painted value planes: v quantised into n steps with smooth transitions (soft 0..0.5)."""
    x = np.clip(v, 0, 1) * n
    f = np.floor(x)
    fr = x - f
    return np.clip((f + smooth(0.5 - soft, 0.5 + soft, fr)) / n, 0, 1)


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


def save_grey(lum_pm, alpha, name, crop=False, size=None, maxw=None):
    straight = lum_pm / np.clip(alpha, 1e-4, 1)
    g = (np.clip(straight, 0, 1) * 255).astype(np.uint8)
    a = (np.clip(alpha, 0, 1) * 255).astype(np.uint8)
    # dilate colour into transparent texels so bilinear filtering never pulls in black
    img = Image.fromarray(np.dstack([g, g, g, a]))
    if crop:
        bb = img.getbbox()
        p = 8 * SS
        img = img.crop((max(0, bb[0] - p), max(0, bb[1] - p), min(img.width, bb[2] + p), min(img.height, bb[3] + p)))
    if size is None:
        size = (img.width // SS, img.height // SS)
    if maxw is not None and size[0] > maxw:
        size = (maxw, int(round(size[1] * maxw / size[0])))
    # resize premultiplied, then un-premultiply
    arr = np.asarray(img, np.float32) / 255.0
    pm = arr[..., 0] * arr[..., 3]
    A = Image.fromarray(arr[..., 3]).resize(size, Image.LANCZOS)
    P = Image.fromarray(pm).resize(size, Image.LANCZOS)
    A = np.clip(np.asarray(A), 0, 1)
    P = np.clip(np.asarray(P), 0, 1)
    Ls = np.where(A > 1e-3, P / np.maximum(A, 1e-3), 0)
    # fill fully transparent texels with the nearest-ish lum (blurred) to avoid dark fringes
    fill = blur(P, 6) / np.maximum(blur(A, 6), 1e-3)
    Ls = np.where(A > 0.02, Ls, np.clip(fill, 0, 1))
    g = (np.clip(Ls, 0, 1) * 255 + 0.5).astype(np.uint8)
    a = (A * 255 + 0.5).astype(np.uint8)
    out = Image.fromarray(np.dstack([g, g, g, a]))
    out.save(os.path.join(OUT, name + ".png"))
    print("  ", name, out.size)
    return out



def soft_domes(W, H, billows, wrap, light, k_sdf, tau=0.18, wrapk=0.45, warp=None, rim_fade=0.12, rref=None, extra_pad=0.0):
    """Soft painter's algorithm. Each billow is a lit ellipsoidal dome; a pixel shows a blend of the
    domes near the front, each weighted by how close it is to the front RELATIVE TO ITS OWN SIZE —
    so a small bud on a big lobe blends in over a few pixels and a big lobe over many, and there are
    no hard crease lines. Returns (soft-union sdf in px, light 0..1)."""
    L = np.array(light, np.float32); L /= np.linalg.norm(L)
    if warp is not None:
        extra_pad = max(extra_pad, float(max(np.abs(warp[0]).max(), np.abs(warp[1]).max())) + 2.0)
    ys = np.arange(H, dtype=np.float32)[:, None]
    xs = np.arange(W, dtype=np.float32)[None, :]
    items = list(billows)
    if wrap:
        items = items + [(b[0] - W,) + tuple(b[1:]) for b in billows] + [(b[0] + W,) + tuple(b[1:]) for b in billows]
    boxes = []
    for cx, cy, rx, ry, z in items:
        x0, x1 = int(max(0, cx - rx * 1.1 - extra_pad)), int(min(W, cx + rx * 1.1 + extra_pad))
        y0, y1 = int(max(0, cy - ry * 1.1 - extra_pad)), int(min(H, cy + ry * 1.1 + extra_pad))
        if x1 > x0 and y1 > y0:
            boxes.append((cx, cy, rx, ry, z, x0, x1, y0, y1))
    Hmax = np.full((H, W), -1e9, np.float32)
    acc_s = np.full((H, W), -1e9, np.float64)
    cache = []
    for cx, cy, rx, ry, z, x0, x1, y0, y1 in boxes:
        px = xs[:, x0:x1]; py = ys[y0:y1]
        if warp is not None:
            px = px + warp[0][y0:y1, x0:x1]; py = py + warp[1][y0:y1, x0:x1]
        u = (px - cx) / rx; v = (py - cy) / ry
        q2 = u * u + v * v
        q = np.sqrt(q2)
        r = min(rx, ry)
        acc_s[y0:y1, x0:x1] = np.logaddexp(acc_s[y0:y1, x0:x1], (k_sdf * (1.0 - q) * r).astype(np.float64))
        nz = np.sqrt(np.clip(1 - q2, 0, 1))
        h = np.where(q < 1, nz * ry + z, -1e9).astype(np.float32)
        Hmax[y0:y1, x0:x1] = np.maximum(Hmax[y0:y1, x0:x1], h)
        cache.append((u, v, q, nz, h))
    acc_w = np.zeros((H, W), np.float32)
    acc_l = np.zeros((H, W), np.float32)
    if rref is None:
        rref = float(np.median([min(b[2], b[3]) for b in billows]))
    # log-weights, normalised per pixel by the largest one: a front billow fading at its rim hands
    # over to the one under it however deep that one is buried (plain weights underflowed to zero)
    logws = []
    maxlog = np.full((H, W), -1e9, np.float32)
    for (cx, cy, rx, ry, z, x0, x1, y0, y1), (u, v, q, nz, h) in zip(boxes, cache):
        r = min(rx, ry)
        soft_r = max(r, 0.7 * rref)
        fade = min(0.6, max(rim_fade, 0.25 * rref / r * rim_fade))
        rim = smooth(1.0, 1.0 - fade, q)
        lw = -np.clip(Hmax[y0:y1, x0:x1] - h, 0, 1e6) / (tau * soft_r) + np.log(np.maximum(rim, 1e-20))
        lw = np.where(q < 1, lw, -1e9).astype(np.float32)
        maxlog[y0:y1, x0:x1] = np.maximum(maxlog[y0:y1, x0:x1], lw)
        logws.append(lw)
    acc_w = np.zeros((H, W), np.float32)
    acc_l = np.zeros((H, W), np.float32)
    for (cx, cy, rx, ry, z, x0, x1, y0, y1), (u, v, q, nz, h), lw in zip(boxes, cache, logws):
        r = min(rx, ry)
        w = np.exp(np.clip(lw - maxlog[y0:y1, x0:x1], -60, 0)) * (q < 1)
        n0, n1, n2 = u * (ry / rx), v, nz
        nl = np.sqrt(n0 * n0 + n1 * n1 + n2 * n2) + 1e-6
        d = (n0 * L[0] + n1 * L[1] + n2 * L[2]) / nl
        lit = np.clip((d + wrapk) / (1 + wrapk), 0, 1)
        lit = 0.62 + (lit - 0.62) * min(1.0, 0.35 + 0.65 * r / rref)
        acc_w[y0:y1, x0:x1] += w
        acc_l[y0:y1, x0:x1] += w * lit
    light_v = np.where(acc_w > 1e-5, acc_l / np.maximum(acc_w, 1e-5), 0.5)
    sdf = np.maximum(acc_s / k_sdf, -1e4).astype(np.float32)
    return sdf, light_v.astype(np.float32), Hmax


# ------------------------------------------------------------------ cumulus
def lobes(rng, x, y, r, z, depth, out, base, squash=1.0):
    """A lobe, and smaller lobes budding on its upper surface: the cauliflower structure of a
    heaped cloud at three scales, instead of one row of equal balls."""
    out.append((x, y, r * rng.uniform(1.0, 1.18), r * squash, z))
    if depth == 0:
        return
    n = rng.randint(2, 4) if depth > 1 else rng.randint(2, 5)
    for _ in range(n):
        ang = rng.uniform(math.radians(200), math.radians(340))     # upper hemisphere (y down)
        cr = r * rng.uniform(0.34, 0.58)
        d = r * rng.uniform(0.62, 0.92)
        cx = x + math.cos(ang) * d
        cy = y + math.sin(ang) * d * squash
        if cy + cr * 0.4 > base:
            continue
        # the bud sits on the parent's surface, a little in front of it
        cz = z + math.sqrt(max(0.0, 1 - (d / r) ** 2)) * r * 0.6 + cr * 0.35
        lobes(rng, cx, cy, cr, cz, depth - 1, out, base, squash)


def cumulus(variant):
    # the heap is laid out on a 1024-wide unit but painted on a wider sheet: heaps that ran off a
    # 1024 sheet were cut straight down their side, and a mirrored cloud showed the cut
    U = 1024 * SS
    W, H = 1400 * SS, 640 * SS
    rng = random.Random({"a": 3, "b": 5, "c": 9}[variant])
    base = 0.84 * H
    cx = W / 2
    billows = []
    # the heap: (x as share of width, radius as share of width, lift above the base)
    plan = {
        "a": [(-0.26, 0.10, 0.00), (-0.12, 0.14, 0.05), (0.04, 0.17, 0.10), (0.20, 0.12, 0.04), (0.32, 0.08, 0.0), (-0.36, 0.06, 0.0)],
        "b": [(-0.16, 0.10, 0.00), (0.0, 0.14, 0.08), (0.13, 0.10, 0.02), (-0.04, 0.11, 0.24), (0.05, 0.08, 0.36)],
        "c": [(-0.38, 0.07, 0.0), (-0.24, 0.09, 0.0), (-0.08, 0.11, 0.02), (0.08, 0.10, 0.01), (0.24, 0.085, 0.0), (0.37, 0.06, 0.0)],
    }[variant]
    for ux, ur, lift in plan:
        r = ur * U * rng.uniform(0.9, 1.1)
        x = cx + ux * U + rng.uniform(-0.01, 0.01) * U
        y = base - r * 0.55 - lift * U
        lobes(rng, x, y, r, r * 0.3 + lift * U * 0.2, 2, billows, base)
    # a low skirt along the base so the bottom is one flat shelf, not a row of balls
    for i in range(10):
        u = i / 9 - 0.5
        span = max(abs(p[0]) for p in plan) + 0.05
        r = 0.05 * U * rng.uniform(0.8, 1.2)
        billows.append((cx + u * 2 * span * U * 0.95, base - r * 0.35, r * 1.8, r * 0.6, 0.0))

    rmean = float(np.median([b[3] for b in billows]))
    warp = ((fbm(H, W, 90 * SS, 5 + ord(variant), 3) - 0.5) * 0.5 * rmean, (fbm(H, W, 90 * SS, 9 + ord(variant), 3) - 0.5) * 0.5 * rmean)
    sdf, diff, hmax = soft_domes(W, H, billows, False, (-0.5, -0.85, 0.5), k_sdf=1.0 / (0.12 * rmean), tau=0.36, warp=warp, extra_pad=0.3 * rmean, rim_fade=0.2)
    ys = np.arange(H, dtype=np.float32)[:, None]

    n1 = fbm(H, W, 40 * SS, 11 + ord(variant), 4)
    n2 = fbm(H, W, 12 * SS, 23 + ord(variant), 3)
    edge = sdf + (n1 - 0.5) * 0.45 * rmean + (n2 - 0.5) * 0.22 * rmean
    feather = 0.14 * rmean
    alpha = smooth(-feather, feather * 1.6, edge)
    alpha *= smooth(base + 0.03 * H, base - 0.08 * H + (n1 - 0.5) * 0.10 * H, ys)
    wn = fbm(H, W, 50 * SS, 91 + ord(variant), 4)
    wisp = smooth(0.5, 0.78, wn) * smooth(-1.1 * rmean, -0.05 * rmean, edge) * (1 - smooth(-0.05 * rmean, 0.3 * rmean, edge))
    alpha = np.maximum(alpha, wisp * 0.3)
    diff = blur(diff, 0.06 * rmean)

    top_line = np.argmax(alpha > 0.5, axis=0).astype(np.float32)
    # per-column tops jump where one lobe ends and the next begins: smooth them, or every such
    # column becomes a vertical seam in the shading
    top_line = np.where(alpha.max(axis=0) > 0.5, top_line, base)
    k = int(0.8 * rmean) | 1
    pad = np.pad(top_line, (k, k), mode="edge")
    kern = np.exp(-np.linspace(-2.5, 2.5, 2 * k + 1) ** 2); kern /= kern.sum()
    top_line = np.convolve(pad, kern, mode="same")[k:-k].astype(np.float32)[None, :]
    bottom_h = float(base - top_line.min())
    depth = np.clip((ys - top_line) / max(1.0, 0.8 * bottom_h), 0, 1)
    under = smooth(base - 0.25 * bottom_h, base, ys)
    v = 0.15 + 0.85 * diff
    v = v * (1 - 0.30 * depth ** 1.3) * (1 - 0.30 * under)
    thin = 1 - smooth(0.0, 0.45 * rmean, edge)
    v = v + 0.25 * thin * smooth(0.45, 0.85, diff) * (1 - under)
    v = soft_steps(v, 5, 0.36)
    v = v * (0.975 + 0.05 * fbm(H, W, 24 * SS, 77, 3))
    v = np.clip(v, 0, 1)
    return save_grey(v * alpha, alpha, "cumulus_" + variant, crop=True, maxw=640)


# ------------------------------------------------------------------ the cloud sea
def band(name, Hf, seed, rows, fade_bottom=True, soften=0.0, light=(-0.45, -0.85, 0.40), bottom_fill=0.45):
    """A seamless strip of the cloud sea, painted as ROWS of rolling heaps receding into haze.

    rows: list of dicts, back to front — y (row base line as a share of the strip), R (billow
    radius, final px), lift (how high the tallest heaps rise, in R), squash (ellipse y/x), haze
    (0..1, pulled toward the pale middle, less contrast), shade (how deep the row sinks into its own
    shade below its tops).

    Each row is a continuous mass: flat wide billows make its body, heaps of very different widths
    and heights roll along its top (a few tall ones, many low swells), each heap built of lobes with
    buds, the way a cumulus is. A row is lit from above-left and sinks into shade toward its base;
    the next row in front lays its lit tops over that shade."""
    W = 2048 * SS
    H = Hf * SS
    rng = random.Random(seed)
    lum = np.zeros((H, W), np.float32)
    alpha = np.zeros((H, W), np.float32)
    ys = np.arange(H, dtype=np.float32)[:, None]
    for k, row in enumerate(rows):
        s_k = row["R"] * SS
        sq = row.get("squash", 0.6)
        y_row = row["y"] * H
        billows = []
        x = rng.uniform(0, s_k)
        while x < W:
            r = s_k * rng.uniform(0.9, 1.3)
            billows.append((x, y_row + r * 0.35 * sq + s_k * 0.3, r * rng.uniform(2.4, 3.4), r * 0.8 * sq + s_k * 0.35, 0.0))
            x += r * rng.uniform(1.8, 2.6)
        x = rng.uniform(0, s_k)
        while x < W:
            tall = rng.random() < row.get("tall", 0.3)
            span = s_k * (rng.uniform(2.2, 4.0) if tall else rng.uniform(1.2, 3.0))
            lift = s_k * row["lift"] * (rng.uniform(0.6, 1.0) if tall else rng.uniform(0.0, 0.35))
            # never let a heap run off the top of the strip: that cut reads as a ruler line
            lift = max(0.0, min(lift, y_row - s_k * (1.35 if tall else 1.1) * sq - 6 * SS))
            hs = rng.uniform(0.7, 1.35)
            n = max(1, int(span / (s_k * 1.1)))
            peak = rng.uniform(0.3, 0.7)
            for i in range(n):
                u = (i + 0.5) / n
                hump = max(0.0, 1 - abs(u - peak) / max(peak, 1 - peak)) ** 0.8
                r = s_k * hs * rng.uniform(0.6, 0.95) * (0.7 + 0.5 * hump) * (1.1 if tall else 1.0)
                r = min(r, (y_row - lift * hump - 4 * SS) / (sq * (1.5 if tall else 1.25)))
                if r < 0.2 * s_k:
                    continue
                cx = x + u * span + rng.uniform(-0.15, 0.15) * s_k
                cy = y_row - lift * hump - rng.uniform(0, 0.12) * s_k
                sub = []
                lobes(rng, cx, cy, r, r * 0.4 + lift * hump * 0.6, 2 if tall else 1, sub, 1e9, sq)
                billows.extend(sub)
            x += span * rng.uniform(0.7, 1.0) + s_k * (rng.uniform(0.0, 0.6) if rng.random() < 0.7 else rng.uniform(1.0, 2.5))
        rref = s_k * 0.8
        warp = ((fbm(H, W, 60 * SS, seed + 5 * k, 3, wrap=True) - 0.5) * 0.5 * rref,
                (fbm(H, W, 60 * SS, seed + 7 * k + 1, 3, wrap=True) - 0.5) * 0.3 * rref)
        sdf, diff, hmax = soft_domes(W, H, billows, True, light, k_sdf=1.0 / (0.14 * rref), tau=0.34, wrapk=0.40, warp=warp, rref=rref, rim_fade=0.22)
        # the big forms: the same heights blurred to the scale of whole heaps, lit on their own —
        # broad light and shade across a heap, with the billows only modelling detail inside it
        hb = np.where(hmax > -1e8, hmax, 0.0).astype(np.float32)
        hb = blur(hb, 0.9 * s_k, wrap_x=True)
        gy, gx = np.gradient(hb)
        Lv = np.array(light, np.float32); Lv /= np.linalg.norm(Lv)
        nn = np.sqrt(gx * gx + gy * gy + 1.0)
        big = np.clip(((-gx * Lv[0] - gy * Lv[1] + Lv[2]) / nn + 0.35) / 1.35, 0, 1)
        big = (big - big[hmax > -1e8].mean()) * 1.6 + 0.62 if (hmax > -1e8).any() else big
        diff = np.clip(0.55 * big + 0.45 * diff, 0, 1)
        n1 = fbm(H, W, 34 * SS, seed + 11 * k, 4, wrap=True)
        n2 = fbm(H, W, 10 * SS, seed + 13 * k, 3, wrap=True)
        edge = sdf + (n1 - 0.5) * 0.5 * rref + (n2 - 0.5) * 0.22 * rref
        feather = 0.16 * rref
        a = smooth(-feather, feather * 1.5, edge)
        wn = fbm(H, W, 40 * SS, seed + 17 * k, 4, wrap=True)
        wisp = smooth(0.52, 0.8, wn) * smooth(-0.9 * rref, -0.05 * rref, edge) * (1 - smooth(-0.05 * rref, 0.25 * rref, edge))
        a = np.maximum(a, wisp * 0.28)
        diff = blur(diff, 0.03 * rref, wrap_x=True)
        below = smooth(y_row - 0.3 * s_k, y_row + 1.3 * s_k, ys)
        v = 0.05 + 0.95 * diff
        v = v * (1 - row.get("shade", 0.55) * below ** 0.8)
        thin = 1 - smooth(0.0, 0.4 * rref, edge)
        v = v + 0.25 * thin * smooth(0.45, 0.85, diff) * (1 - below)
        v = soft_steps(v, 5, 0.38)
        hz = row.get("haze", 0.0)
        v = v * (1 - hz) + 0.70 * hz
        v = v * (0.975 + 0.05 * fbm(H, W, 22 * SS, seed + 19 * k, 3, wrap=True))
        lum = np.clip(v, 0, 1) * a + lum * (1 - a)
        alpha = a + alpha * (1 - a)
    if soften > 0:
        lum = blur(lum, soften * SS, wrap_x=True)
        alpha = blur(alpha, soften * SS, wrap_x=True)
    # the strip's top rows are always clear, whatever a wisp or a warped heap did
    top = smooth(1 * SS, 10 * SS, ys)
    alpha = alpha * top
    lum = lum * top
    if fade_bottom:
        kf = 1 - smooth(0.62 * H, 0.98 * H, ys)
        alpha = alpha * kf
        lum = lum * kf
    else:
        kf = smooth(0.55 * H, 0.85 * H, ys)
        fill = (1 - alpha) * kf
        lum = lum + bottom_fill * fill
        alpha = alpha + fill
    return save_grey(lum, alpha, name)


def streaks(name, Hf, seed, count, length, thick, alpha_max, y_lo, y_hi, fibre=0.6, tilt=0.0):
    """Stratus and cirrus: long soft veils. Each streak is a lens of density whose thickness and
    strength wander along its length (1-D noise), with a few thinner strands running beside it and
    torn, feathered ends — so it reads as a veil of cloud and not as a line ruled across the sky."""
    W = 2048 * SS
    H = Hf * SS
    rng = random.Random(seed)
    ys = np.arange(H, dtype=np.float32)[:, None]
    xs = np.arange(W, dtype=np.float32)[None, :]
    dens = np.zeros((H, W), np.float32)

    def wander(n_cells, amp):
        g = np.array([rng.uniform(-1, 1) for _ in range(n_cells)] * 3, np.float32)
        up = np.interp(np.linspace(n_cells, 2 * n_cells, W, endpoint=False), np.arange(3 * n_cells), g)
        return (up * amp).astype(np.float32)

    for _ in range(count):
        cx = rng.uniform(0, W)
        cy = rng.uniform(y_lo, y_hi) * H
        L = length * SS * rng.uniform(0.6, 1.5)
        T = thick * SS * rng.uniform(0.7, 1.3)
        amp = rng.uniform(0.55, 1.0)
        centre = wander(int(W / (L * 0.35)) + 3, T * 1.2)
        width = 1 + wander(int(W / (L * 0.25)) + 3, 0.45)
        strength = 1 + wander(int(W / (L * 0.2)) + 3, 0.5)
        for strand in range(rng.randint(1, 3)):
            off_y = (strand - 0.5) * T * rng.uniform(1.2, 2.2) if strand else 0.0
            k = 1.0 if strand == 0 else rng.uniform(0.3, 0.6)
            for off in (-W, 0, W):
                dx = (xs - cx - off) / L
                if np.abs(dx).min() > 1.8:
                    continue
                along = np.exp(-(dx ** 2) * 1.4)
                dy = (ys - cy - off_y - centre[None, :] - dx * tilt * T) / (T * width[None, :] * (1 if strand == 0 else 0.5))
                m = along * np.exp(-dy ** 2) * amp * k * np.clip(strength[None, :], 0.2, 2)
                dens = dens + m * (1 - dens)
    tear = fbm(H, W, 90 * SS, seed + 5, 3, wrap=True)
    d = dens * (0.55 + 0.9 * tear)
    a = np.clip(smooth(0.05, 1.0, d) * alpha_max, 0, 1)
    edge = smooth(1 * SS, 8 * SS, ys) * (1 - smooth(H - 8 * SS, H - 1 * SS, ys))
    a = a * edge
    lum = np.clip(0.82 + 0.18 * smooth(0.2, 0.9, d) - 0.08 * (ys / H), 0, 1)
    return save_grey(lum * a, a, name)


BANDS = {
    "sea_far": lambda: band("sea_far", 150, 101, soften=1.0, rows=[
        dict(y=0.34, R=18, lift=1.2, squash=0.5, haze=0.45, shade=0.4),
        dict(y=0.52, R=26, lift=1.4, squash=0.52, haze=0.30, shade=0.45)]),
    "sea_mid": lambda: band("sea_mid", 230, 202, soften=0.3, rows=[
        dict(y=0.34, R=40, lift=1.3, squash=0.55, haze=0.25, shade=0.5),
        dict(y=0.56, R=62, lift=1.5, squash=0.58, haze=0.08, shade=0.55)]),
    "sea_wisps": lambda: streaks("sea_wisps", 140, 404, 16, 300, 10, 0.55, 0.25, 0.8, fibre=0.55),
    "cirrus": lambda: streaks("cirrus", 160, 505, 12, 420, 7, 0.42, 0.2, 0.85, fibre=0.75, tilt=0.4),
    "sea_near": lambda: band("sea_near", 320, 303, fade_bottom=False, rows=[
        dict(y=0.36, R=90, lift=1.2, squash=0.62, haze=0.12, shade=0.55, tall=0.4),
        dict(y=0.62, R=140, lift=1.0, squash=0.66, haze=0.0, shade=0.6, tall=0.35)]),
}



# ------------------------------------------------------------------ shader noise
def cloud_noise(S=256):
    """Two tileable octaves of smooth noise for MiT/UI Cloud: R coarse, G finer. Written sRGB-encoded,
    so what the shader samples (after the import's sRGB decode) is the uniform value itself."""
    def tile(cells, seed):
        rng = np.random.RandomState(seed)
        g = rng.rand(cells, cells).astype(np.float32)
        g3 = np.tile(g, (3, 3))
        img = Image.fromarray((g3 * 255).astype(np.uint8)).resize((3 * S, 3 * S), Image.BICUBIC)
        return np.asarray(img, np.float32)[S:2 * S, S:2 * S] / 255.0
    r = tile(4, 61) * 0.62 + tile(8, 62) * 0.38
    g = tile(8, 63) * 0.6 + tile(16, 64) * 0.4
    def norm(x):
        lo, hi = np.percentile(x, 1), np.percentile(x, 99)
        return np.clip((x - lo) / (hi - lo), 0, 1)
    def enc(x):
        return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(x, 1 / 2.4) - 0.055)
    r, g = enc(norm(r)), enc(norm(g))
    rgba = np.dstack([r, g, (r + g) * 0.5, np.ones_like(r)])
    Image.fromarray((rgba * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "cloud_noise.png"))
    print("   cloud_noise", (S, S))


# ------------------------------------------------------------------ the old billow painter (gen_cinematic.py)
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
    jobs = dict(BANDS)
    for v in "abc":
        jobs["cumulus_" + v] = (lambda v=v: cumulus(v))
    jobs["cloud_noise"] = cloud_noise
    jobs["sun_disc"] = lambda: disc("sun_disc", 256, 112, 1.0, 0.94)
    jobs["moon_disc"] = lambda: disc("moon_disc", 256, 104, 0.96, 0.90, maria=True)
    jobs["star_dot"] = lambda: star("star_dot", 32, False)
    jobs["star_glint"] = lambda: star("star_glint", 64, True)
    jobs["sun_rays"] = sun_rays
    for name in (sys.argv[1:] or list(jobs)):
        jobs[name]()
