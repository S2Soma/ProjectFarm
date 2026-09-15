"""Plot beds that tile edge to edge.

    uv run --with pillow --with numpy python Tools/gen_beds.py

Why these replace Art/farm/tile_*.png on the field
--------------------------------------------------
The painted tiles each carry a fence post at all four vertices, a rail and a grass ring.
That only works with grass paths between plots: pushed together, every shared vertex
gets two posts and each bed's rail cuts into its neighbour, so the field had to be spread
out with gaps — which made the 4x4 grid read as a scatter of separate squares instead of
one tilled field in straight rows.

These beds are designed to touch:

  * the CELL is a full diamond of dark furrow soil, drawn 1% oversize so neighbours
    overlap by a pixel and anti-aliasing never opens a grass seam between them;
  * the raised BED sits inset inside it, so the furrow between two beds is exactly two
    insets wide everywhere and the lines run straight across the whole field;
  * furrow ridges run along the same iso axis on every bed, so the rows line up across
    bed boundaries instead of restarting at each one;
  * three soil variants, picked per plot, so sixteen beds do not read as one picture
    stamped sixteen times.

Canvas proportions match the tiles they replace (the diamond is 760 of 780 px wide with
its axis at 47.5% from the bottom), so IslandView's TileArt maths does not change. The
painted palette is sampled from the original art so the beds sit in the same world.

Nothing below the cell's lower edges survives inside the field: front beds draw over the
back ones' skirts. Only the outer rim of the field shows its skirt, which is what makes
the field read as one raised patch sitting on the grass.
"""
from PIL import Image, ImageFilter, ImageDraw
import numpy as np
import math, os, random

OUT = "Assets/Resources/Art/beds"
os.makedirs(OUT, exist_ok=True)

W, H = 780, 640
A, B = 380.0, 190.0                  # cell diamond half-width / half-height (2:1)
CX = W / 2.0
CY = H - H * 0.475                   # axis, in image rows (top-down)
SS = 2                                # supersample for clean edges

CELL_OVER = 1.012                     # neighbours overlap a hair: no AA seams
BED = 0.885                           # raised bed inset
LIP = 0.055                           # rim band width, as a fraction of the diamond
CELL_SKIRT = 22.0                     # the field's outer rim
BED_SKIRT = 20.0                      # the raised bed's front faces


def hexc(s):
    s = s.lstrip("#")
    return np.array([int(s[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32)


def mix(a, b, t):
    t = np.clip(t, 0.0, 1.0)[..., None]
    return a * (1.0 - t) + b * t


def field():
    """Iso coordinates for every supersampled pixel."""
    ys, xs = np.mgrid[0:H * SS, 0:W * SS].astype(np.float32)
    x = (xs + 0.5) / SS
    y = (ys + 0.5) / SS
    dx = (x - CX) / A                 # -1..1 across
    dy = (CY - y) / B                 # -1..1, positive is FAR (up the screen)
    u = np.abs(dx) + np.abs(dy)       # diamond "radius": 1 on the cell edge
    # iso axes: s runs along the bed's right-leaning edge, t along the left-leaning one
    s = (dx + dy) * 0.5
    t = (dy - dx) * 0.5
    return x, y, dx, dy, u, s, t


def value_noise(shape, scale, seed):
    rng = np.random.RandomState(seed)
    gh, gw = int(shape[0] / scale) + 3, int(shape[1] / scale) + 3
    g = rng.rand(gh, gw).astype(np.float32)
    img = Image.fromarray((g * 255).astype(np.uint8)).resize((shape[1], shape[0]), Image.BICUBIC)
    return np.asarray(img, dtype=np.float32) / 255.0


def skirt_mask(dx, dy, u, inset, depth_px):
    """Pixels hanging below the two FRONT edges of a diamond of radius `inset`."""
    # distance below the near edge, measured vertically in pixels
    edge_y = inset * B * (1.0 - np.abs(dx) / inset)          # |dy| on the edge, for dy<0
    below = (-dy * B) - edge_y                                # >0 once under the edge
    inside_x = np.abs(dx) <= inset
    m = inside_x & (dy < 0) & (below > 0) & (below <= depth_px)
    return m, np.clip(below / depth_px, 0, 1)


def bed(palette, seed, wet=False, gold=False, dry=False):
    x, y, dx, dy, u, s, t = field()
    shape = u.shape
    rgb = np.zeros(shape + (3,), np.float32)
    alpha = np.zeros(shape, np.float32)

    n_big = value_noise(shape, 90 * SS, seed)
    n_small = value_noise(shape, 14 * SS, seed + 7)

    # ---- 1. the cell: furrow soil, plus the field's outer skirt ----
    cm, cd = skirt_mask(dx, dy, u, CELL_OVER, CELL_SKIRT)
    rgb[cm] = mix(palette["cell_skirt_hi"], palette["cell_skirt_lo"], cd)[cm]
    alpha[cm] = 1.0

    cell = u <= CELL_OVER
    furrow = mix(palette["furrow_lo"], palette["furrow_hi"], dy * 0.5 + 0.5)
    furrow = furrow * (0.92 + 0.16 * n_small[..., None])
    rgb[cell] = furrow[cell]
    alpha[cell] = 1.0

    # ---- 2. the raised bed's front faces ----
    bm, bd = skirt_mask(dx, dy, u, BED, BED_SKIRT)
    # left face catches a little more light than the right one
    side = np.where(dx < 0, 1.0, 0.78)[..., None]
    face = mix(palette["bed_skirt_hi"], palette["bed_skirt_lo"], bd) * side
    face = face * (0.94 + 0.10 * n_small[..., None])
    rgb[bm] = face[bm]

    # ---- 3. the bed top ----
    top = u <= BED
    base = mix(palette["soil_lo"], palette["soil_hi"], dy * 0.22 + 0.62)
    base = base * (0.86 + 0.26 * n_big[..., None]) * (0.93 + 0.12 * n_small[..., None])

    # Furrow ridges along one iso axis. Same phase on every bed, so a row of beds reads as
    # one ploughed line running straight across the field. Few, soft and wobbled by noise:
    # a regular high-contrast sine read as wooden planks, not soil.
    wobble = (n_big - 0.5) * 0.9
    ridge = np.sin((t * 6.0 + wobble * 0.35) * math.pi)
    shade = np.clip(ridge, 0, 1) ** 3
    light = np.clip(-ridge, 0, 1) ** 4
    base = mix(base, palette["soil_lo"] * 0.80, shade * 0.20)
    base = mix(base, palette["soil_hi"] * 1.06, light * 0.10)

    # granules — the thing that makes painted soil read as soil at a glance
    n_tiny = value_noise(shape, 2.6 * SS, seed + 29)
    base = mix(base, palette["soil_lo"] * 0.62, np.clip((0.16 - n_tiny) / 0.16, 0, 1) * 0.55)
    base = mix(base, palette["soil_hi"] * 1.18, np.clip((n_tiny - 0.86) / 0.14, 0, 1) * 0.45)

    # soft painted clods, jittered on a lattice so they never line up into a grid
    rng = random.Random(seed)
    for i in range(-2, 3):
        for j in range(-2, 3):
            if rng.random() < 0.35:
                continue
            cs = (i + rng.uniform(-0.35, 0.35)) * 0.17
            ct = (j + rng.uniform(-0.35, 0.35)) * 0.17
            size = rng.uniform(0.035, 0.06)
            d = np.abs(s - cs) + np.abs(t - ct)
            blob = np.clip(1.0 - d / size, 0, 1)
            under = np.clip(1.0 - (np.abs(s - cs - 0.012) + np.abs(t - ct + 0.012)) / size, 0, 1)
            base = mix(base, palette["soil_lo"] * 0.78, (under - blob).clip(0, 1) * 0.55)
            base = mix(base, palette["soil_hi"] * 1.08, blob ** 0.7 * 0.55)

    if wet:
        # A wet bed is darker and cooler, with damp pools where the soil settled. No bright
        # streaks: thin white lines at this size read as scratches, not water.
        base = base * np.array([0.70, 0.68, 0.72], np.float32)
        pool = np.clip((n_big - 0.58) / 0.20, 0, 1)
        base = mix(base, base * np.array([0.78, 0.84, 0.98], np.float32), pool * 0.75)
        sheen = np.clip((n_small - 0.80) / 0.20, 0, 1) * pool
        base = mix(base, np.array([120, 142, 160], np.float32), sheen * 0.35)

    # rim lip: the far edges catch the light, the near edges fall into shade
    lip = (u > BED - LIP) & top
    lt = ((u - (BED - LIP)) / LIP)
    far = dy > 0
    lip_hi = palette["lip_gold"] if gold else palette["lip_hi"]
    lip_col = np.where(far[..., None], lip_hi, palette["lip_lo"])
    lip_col = mix(lip_col, lip_col * 0.80, lt)
    base = np.where(lip[..., None], mix(base, lip_col, np.clip(1.2 - lt, 0.55, 1.0)), base)
    # a thin dark step just inside the lip, which is what sells "raised"
    step = top & (u > BED - LIP - 0.012) & (u <= BED - LIP)
    base = np.where(step[..., None], base * 0.72, base)

    if gold:
        glow = np.clip(1.0 - u / BED, 0, 1) ** 0.5
        base = mix(base, base * 1.12 + np.array([14, 10, 0], np.float32), glow * 0.5)

    if dry:
        # Parched: paler, dustier soil split by a crack network. Owner, 15/9: the first version was too
        # faint to say "needs water" on its own — the cracks are now wider and darker, and the plates
        # paler. The water-drop badge (thirst_badge, IslandView) carries the signal; this is its echo.
        base = mix(base, np.array([184, 146, 104], np.float32), 0.50)
        # Voronoi plates in the bed's own iso space: a crack is where the nearest and second
        # nearest plate centres are almost equally far — straight-ish polygon edges, which is
        # what dried mud actually does (noise isolines read as contour lines on a map).
        prng = np.random.RandomState(seed + 91)
        pts = prng.uniform(-0.55, 0.55, size=(34, 2)).astype(np.float32)
        d1 = np.full(shape, 9.0, np.float32); d2 = np.full(shape, 9.0, np.float32)
        wob = (value_noise(shape, 9 * SS, seed + 93) - 0.5) * 0.018
        for px_, py_ in pts:
            dd = np.sqrt((s - px_) ** 2 + (t - py_) ** 2) + wob
            nd2 = np.where(dd < d1, d1, np.minimum(d2, dd))
            d1 = np.minimum(d1, dd); d2 = nd2
        gap = d2 - d1
        crack = gap < 0.0085
        lip_hi = (gap >= 0.0085) & (gap < 0.017)
        base = np.where((crack & top)[..., None], base * 0.38, base)
        base = np.where((lip_hi & top)[..., None], mix(base, base * 1.16, 0.75), base)
        # plates curl up a little at their edges and darken toward their centres
        base = mix(base, base * 0.92, np.clip(gap / 0.08, 0, 1) * 0.5)

    rgb[top] = base[top]

    # ---- composite, downsample ----
    out = np.dstack([np.clip(rgb, 0, 255), alpha * 255]).astype(np.uint8)
    im = Image.fromarray(out, "RGBA").resize((W, H), Image.LANCZOS)
    return im


def locked(seed, with_lock=True):
    """A staked-out patch of grass. Mostly transparent so every island's own grass tint
    shows through; the lock is lifted from the painted tile so it matches the world."""
    x, y, dx, dy, u, s, t = field()
    shape = u.shape
    rgb = np.zeros(shape + (3,), np.float32)
    alpha = np.zeros(shape, np.float32)

    patch = u <= 0.93
    n = value_noise(shape, 30 * SS, seed)
    rgb[patch] = np.array([28, 62, 24], np.float32)
    alpha[patch] = (0.10 + 0.08 * n)[patch]

    # dashed rope along the bed line — the plot's shape is legible, but it is not soil yet
    ring = (u > 0.86) & (u <= 0.89)
    along = np.where(np.abs(dx) > 1e-3, np.arctan2(dy, dx), 0)
    dash = (np.sin((s - t) * 70.0) > -0.2) | (np.sin((s + t) * 70.0) > -0.2)
    dash = np.sin(np.where(np.abs(s) > np.abs(t), s, t) * 64.0) > -0.1
    rope = ring & dash
    rgb[rope] = np.array([250, 244, 222], np.float32)
    alpha[rope] = 0.62

    out = np.dstack([np.clip(rgb, 0, 255), alpha * 255]).astype(np.uint8)
    im = Image.fromarray(out, "RGBA").resize((W, H), Image.LANCZOS)

    lock = painted_lock() if with_lock else None
    if lock is not None:
        lw = 150
        lh = int(lock.height * lw / lock.width)
        lock = lock.resize((lw, lh), Image.LANCZOS)
        im.alpha_composite(lock, (int(CX - lw / 2), int(CY - lh * 0.62)))
    return im


def painted_lock():
    """The padlock from the original painted tile, with its stone background keyed out.

    Flood-filled from the crop border across the stone, stopping at the lock's dark
    outline; the hole under the shackle is seeded separately because the outline
    encloses it."""
    src = "Assets/Resources/Art/farm/tile_locked.png"
    if not os.path.exists(src):
        return None
    im = Image.open(src).convert("RGBA").crop((140, 70, 250, 190))
    a = np.asarray(im).astype(np.float32)
    h, w = a.shape[:2]
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    lum = 0.3 * r + 0.59 * g + 0.11 * b
    sat = a[..., :3].max(-1) - a[..., :3].min(-1)
    stone = (sat < 40) & (lum > 52)

    keep = np.ones((h, w), bool)
    seeds = [(0, xx) for xx in range(w)] + [(h - 1, xx) for xx in range(w)] + \
            [(yy, 0) for yy in range(h)] + [(yy, w - 1) for yy in range(h)]
    seeds.append((int(h * 0.37), int(w * 0.50)))       # inside the shackle
    stack = [p for p in seeds if stone[p]]
    while stack:
        yy, xx = stack.pop()
        if not keep[yy, xx]:
            continue
        keep[yy, xx] = False
        for ny, nx in ((yy + 1, xx), (yy - 1, xx), (yy, xx + 1), (yy, xx - 1)):
            if 0 <= ny < h and 0 <= nx < w and keep[ny, nx] and stone[ny, nx]:
                stack.append((ny, nx))

    mask = Image.fromarray((keep * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6))
    im.putalpha(mask)
    return im.crop(im.getbbox())


def rim_glow(color, strength=1.0):
    """A soft band of light on the bed's rim, drawn UNDER the crop and pulsed by the game —
    how a plot says "ripe" or "water me" without anything standing in front of the plant."""
    x, y, dx, dy, u, s, t = field()
    band = np.clip(1.0 - np.abs(u - (BED - LIP * 0.5)) / 0.09, 0, 1) ** 1.6
    inner = np.clip(1.0 - u / BED, 0, 1) ** 2.5 * 0.18 * (u <= BED)
    alpha = np.clip(band * 0.95 + inner, 0, 1) * strength
    rgb = np.zeros(u.shape + (3,), np.float32); rgb[:] = np.array(color, np.float32)
    out = np.dstack([rgb, alpha * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA").resize((W, H), Image.LANCZOS)


def sparkles(seed):
    """A handful of four-point glints over a ripe bed, pulsed with the rim glow."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rng = random.Random(seed)
    pts = 0
    while pts < 9:
        px_, py_ = rng.uniform(-0.8, 0.8), rng.uniform(-0.8, 0.8)
        if abs(px_) + abs(py_) > 0.78:
            continue
        cx_, cy_ = CX + px_ * A, CY - py_ * B
        r = rng.uniform(10, 20)
        c = (255, 246, 190, 255)
        d.polygon([(cx_, cy_ - r), (cx_ + r * 0.22, cy_), (cx_, cy_ + r), (cx_ - r * 0.22, cy_)], fill=c)
        d.polygon([(cx_ - r, cy_), (cx_, cy_ - r * 0.22), (cx_ + r, cy_), (cx_, cy_ + r * 0.22)], fill=c)
        pts += 1
    return img.filter(ImageFilter.GaussianBlur(0.8))


def selection():
    """The outline on the plot the seed sheet is planting into: a warm glowing band just inside
    the cell edge, drawn on the same canvas as the beds so it lines up without any maths."""
    x, y, dx, dy, u, s, t = field()
    shape = u.shape
    band = np.clip(1.0 - np.abs(u - 0.90) / 0.045, 0, 1) ** 1.5
    glow = np.clip(1.0 - np.abs(u - 0.90) / 0.16, 0, 1) ** 2 * 0.45
    core = np.clip(1.0 - np.abs(u - 0.90) / 0.018, 0, 1)
    rgb = np.zeros(shape + (3,), np.float32)
    rgb[:] = np.array([255, 226, 120], np.float32)
    rgb = rgb * (1 - core[..., None]) + np.array([255, 252, 236], np.float32) * core[..., None]
    alpha = np.clip(np.maximum(band, glow), 0, 1) * (u <= 1.06)
    out = np.dstack([rgb, alpha * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA").resize((W, H), Image.LANCZOS)


def thirst_rim():
    """The thirsty bed's rim light, stronger than the ripe one's: a saturated water-blue band with a
    bright core line and a soft glow spilling past the bed's edge. MiT/UI Thirst Rim pulses it and runs
    two highlights round it, so a thirsty field is noticed from the corner of the eye."""
    x, y, dx, dy, u, s, t = field()
    c = BED - LIP * 0.5
    band = np.clip(1.0 - np.abs(u - c) / 0.075, 0, 1) ** 1.2
    core = np.clip(1.0 - np.abs(u - c) / 0.016, 0, 1)
    spill = np.clip(1.0 - np.abs(u - (c + 0.05)) / 0.16, 0, 1) ** 2 * 0.45
    inner = np.clip(1.0 - u / BED, 0, 1) ** 2.2 * 0.16 * (u <= BED)
    alpha = np.clip(np.maximum(band, spill) + inner, 0, 1)
    rgb = np.zeros(u.shape + (3,), np.float32)
    rgb[:] = np.array([64, 184, 255], np.float32)
    rgb = mix(rgb, np.array([226, 248, 255], np.float32), core)
    out = np.dstack([rgb, alpha * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA").resize((W, H), Image.LANCZOS)


def _teardrop(cx, bottom, h, w, n=160):
    """A water drop standing on (cx, bottom): round below, pointed above."""
    pts = []
    for k in range(n):
        a = k / n * math.tau
        px_ = math.sin(a) * math.sin(a / 2.0)
        py_ = math.cos(a)                               # 1 at the tip, -1 at the bottom
        pts.append((cx + px_ * (w / 2.0) / 0.7698, bottom - (py_ + 1.0) * h / 2.0))
    return pts


def thirst_badge():
    """Art/beds/thirst_badge.png, three 256 px cells for MiT/UI Thirst (IslandView's thirsty badge):
       0 the drop: painted water drop, white rim, dark outline — stands on the bottom of the cell
       1 a ripple ring on the ground (iso ellipse), expanding and fading in the shader
       2 a soft contact shadow, shrinking as the drop hops"""
    C, S4 = 256, 4
    img = Image.new("RGBA", (C * 3, C), (0, 0, 0, 0))

    # ---- 0: the drop ----
    big = Image.new("RGBA", (C * S4, C * S4), (0, 0, 0, 0))
    def layer(pts, fill):
        m = Image.new("L", big.size, 0)
        ImageDraw.Draw(m).polygon([(px_ * S4, py_ * S4) for px_, py_ in pts], fill=255)
        col = Image.new("RGBA", big.size, fill)
        big.paste(col, (0, 0), m)
        return m
    cx, bottom = C / 2.0, 244.0
    halo = Image.new("L", big.size, 0)
    ImageDraw.Draw(halo).polygon([(px_ * S4, py_ * S4) for px_, py_ in _teardrop(cx, bottom + 2, 214, 166)], fill=150)
    halo = halo.filter(ImageFilter.GaussianBlur(6 * S4))
    shade = Image.new("RGBA", big.size, (10, 30, 60, 255)); shade.putalpha(halo)
    big.alpha_composite(shade)
    layer(_teardrop(cx, bottom, 206, 158), (22, 56, 104, 255))           # outline
    layer(_teardrop(cx, bottom - 7, 190, 142), (248, 252, 255, 255))     # white rim
    body = layer(_teardrop(cx, bottom - 17, 166, 118), (0, 0, 0, 255))
    # body: vertical gradient, darker toward the lower right
    ys, xs = np.mgrid[0:C * S4, 0:C * S4].astype(np.float32) / S4
    top_c, bot_c = hexc("#9AE6FF"), hexc("#2386E4")
    tt = np.clip((ys - 70) / 150.0, 0, 1)[..., None]
    grad = top_c * (1 - tt) + bot_c * tt
    lr = np.clip(((xs - cx) * 0.8 + (ys - 170) * 0.6) / 70.0, 0, 1)[..., None] ** 1.5
    grad = grad * (1 - lr * 0.35) + hexc("#135BAE") * (lr * 0.35)
    bm = np.asarray(body, np.float32) / 255.0
    arr = np.asarray(big).astype(np.float32)
    arr[..., :3] = arr[..., :3] * (1 - bm[..., None]) + grad * bm[..., None]
    big = Image.fromarray(arr.astype(np.uint8), "RGBA")
    # gloss: a tilted highlight up-left and a small dot
    gl = Image.new("L", big.size, 0)
    dg = ImageDraw.Draw(gl)
    dg.ellipse(((cx - 40) * S4, 130 * S4, (cx - 14) * S4, 196 * S4), fill=220)
    dg.ellipse(((cx - 16) * S4, 104 * S4, (cx - 2) * S4, 120 * S4), fill=235)
    gl = gl.filter(ImageFilter.GaussianBlur(1.5 * S4))
    gl = Image.fromarray((np.asarray(gl, np.float32) * bm).astype(np.uint8))
    white = Image.new("RGBA", big.size, (255, 255, 255, 255)); white.putalpha(gl)
    big.alpha_composite(white)
    img.alpha_composite(big.resize((C, C), Image.LANCZOS), (0, 0))

    # ---- 1: ripple ring ----
    ring = Image.new("L", (C * S4, C * S4), 0)
    dr = ImageDraw.Draw(ring)
    rx, ry, th = 104, 52, 9
    dr.ellipse(((C / 2 - rx) * S4, (C / 2 - ry) * S4, (C / 2 + rx) * S4, (C / 2 + ry) * S4), outline=255, width=th * S4)
    ring = ring.filter(ImageFilter.GaussianBlur(2.2 * S4)).resize((C, C), Image.LANCZOS)
    rc = Image.new("RGBA", (C, C), (190, 238, 255, 255)); rc.putalpha(ring)
    img.alpha_composite(rc, (C, 0))

    # ---- 2: contact shadow ----
    sh = Image.new("L", (C, C), 0)
    ImageDraw.Draw(sh).ellipse((C / 2 - 70, C / 2 - 30, C / 2 + 70, C / 2 + 30), fill=170)
    sh = sh.filter(ImageFilter.GaussianBlur(14))
    sc = Image.new("RGBA", (C, C), (14, 32, 58, 255)); sc.putalpha(sh)
    img.alpha_composite(sc, (C * 2, 0))
    return img


SOIL = dict(
    soil_hi=hexc("#9A6440"), soil_lo=hexc("#6E4329"),
    furrow_hi=hexc("#5A3A24"), furrow_lo=hexc("#3F2818"),
    bed_skirt_hi=hexc("#7A4E30"), bed_skirt_lo=hexc("#4A2F1C"),
    cell_skirt_hi=hexc("#5C3E27"), cell_skirt_lo=hexc("#3A2616"),
    lip_hi=hexc("#B98457"), lip_lo=hexc("#7A4D2F"),
    lip_gold=hexc("#EBC46E"),
)

if __name__ == "__main__":
    for v in range(3):
        bed(SOIL, seed=11 + v * 17, dry=True).save(os.path.join(OUT, f"bed_thirsty_{v}.png"))
        bed(SOIL, seed=11 + v * 17).save(os.path.join(OUT, f"bed_empty_{v}.png"))
        bed(SOIL, seed=11 + v * 17, wet=True).save(os.path.join(OUT, f"bed_watered_{v}.png"))
        bed(SOIL, seed=11 + v * 17, gold=True).save(os.path.join(OUT, f"bed_ready_{v}.png"))
        print("variant", v)
    locked(5).save(os.path.join(OUT, "bed_locked.png"))
    # The same staked-out patch without the padlock, for every plot on an island the player
    # does not own yet: one sign on the island says it is locked; sixteen padlocks under it
    # would only repeat that sixteen times.
    locked(5, with_lock=False).save(os.path.join(OUT, "bed_unclaimed.png"))
    selection().save(os.path.join(OUT, "bed_select.png"))
    rim_glow((255, 214, 90)).save(os.path.join(OUT, "bed_glow_ready.png"))
    sparkles(3).save(os.path.join(OUT, "bed_sparkle.png"))
    thirst_rim().save(os.path.join(OUT, "bed_glow_thirst.png"))
    badge = thirst_badge()
    badge.save(os.path.join(OUT, "thirst_badge.png"))
    # the drop alone, for pages that show the thirsty bed as a picture (Menu ▸ Hướng dẫn chơi)
    badge.crop((0, 0, 256, 256)).save(os.path.join(OUT, "thirst_drop.png"))
    print("locked")
