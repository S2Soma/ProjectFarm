"""Produce icons for the crops that had no painted art — drawn procedurally, painted-style.

    uv run --with pillow --with numpy python Tools/gen_produce.py

Why
---
Nineteen crops used Kenney "Food Kit" preview renders: 52-74 px wide, shown at 90-150 px on a
phone, so every pepper, grape and coconut on the seed sheet was a blurred lump next to the
painted tomato and pumpkin. These replace those files in Art/crop/ at 256 px.

How the painted look is made
----------------------------
Each part is a silhouette mask. Blurring the mask gives a smooth dome whose slope is steepest at
the edge; its gradient is used as a surface normal and lit from the top left. That one trick is
what turns flat shapes into round fruit, and it works for any silhouette, so every crop gets the
same light. On top: a thin outline in a darker shade of the part's own colour (the painted
crops outline in dark green, never black), a specular glint, and per-crop surface detail.
Drawn at 1024 px, reduced with Lanczos.
"""
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
import math, os, re, uuid

OUT = os.environ.get("PRODUCE_OUT", "Assets/Resources/Art/crop")
S = 1024
FINAL = 256
LIGHT = np.array([-0.55, -0.75, 0.95], np.float32)
LIGHT /= np.linalg.norm(LIGHT)
HALF = LIGHT + np.array([0, 0, 1], np.float32)
HALF /= np.linalg.norm(HALF)


def hexc(s):
    s = s.lstrip("#")
    return np.array([int(s[i:i + 2], 16) for i in (0, 2, 4)], np.float32)


class Canvas:
    def __init__(self):
        self.p = np.zeros((S, S, 3), np.float32)
        self.a = np.zeros((S, S), np.float32)

    def paint(self, mask, color, alpha=1.0):
        m = np.clip(mask, 0, 1) * alpha
        col = color if (isinstance(color, np.ndarray) and color.ndim == 3) else np.asarray(color, np.float32)
        self.p = col * m[..., None] + self.p * (1 - m[..., None])
        self.a = m + self.a * (1 - m)

    def image(self):
        a = np.clip(self.a, 1e-6, 1)
        rgb = np.clip(self.p / a[..., None], 0, 255)
        return Image.fromarray(np.dstack([rgb, np.clip(self.a, 0, 1) * 255]).astype(np.uint8))


def L(fn):
    im = Image.new("L", (S, S), 0)
    fn(ImageDraw.Draw(im))
    return np.asarray(im, np.float32) / 255.0


def to_img(m):
    return Image.fromarray((np.clip(m, 0, 1) * 255).astype(np.uint8))


def from_img(im):
    return np.asarray(im, np.float32) / 255.0


def blur(m, r):
    """Gaussian-ish blur in float precision: three box passes per axis.

    PIL blurs an 8-bit layer, and the lighting below differentiates the result — 256 levels
    became visible terraces, contour rings like wood grain across every pepper and orange."""
    sigma = max(0.5, r)
    w = max(1, int(round(math.sqrt(12.0 * sigma * sigma / 3.0 + 1.0))))
    out = np.asarray(m, np.float32)
    for axis in (0, 1):
        for _ in range(3):
            pad = [(0, 0), (0, 0)]
            pad[axis] = (w // 2 + 1, w - w // 2)
            c = np.cumsum(np.pad(out, pad, mode="edge"), axis=axis, dtype=np.float64)
            if axis == 0:
                out = ((c[w:, :] - c[:-w, :]) / w)[: out.shape[0], :].astype(np.float32)
            else:
                out = ((c[:, w:] - c[:, :-w]) / w)[:, : out.shape[1]].astype(np.float32)
    return out


def grow(m, px):
    return np.clip((blur(m, px * 0.5) - 0.02) * 30.0, 0, 1)


def union(*ms):
    out = np.zeros((S, S), np.float32)
    for m in ms:
        out = np.maximum(out, m)
    return out


def rotate(m, deg, cx=512, cy=512):
    return from_img(to_img(m).rotate(deg, center=(cx, cy), resample=Image.BICUBIC))


def ellipse(cx, cy, rx, ry=None):
    ry = rx if ry is None else ry
    return L(lambda d: d.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=255))


def rrect(x0, y0, x1, y1, r):
    return L(lambda d: d.rounded_rectangle((x0, y0, x1, y1), r, fill=255))


def poly(pts):
    return L(lambda d: d.polygon([tuple(p) for p in pts], fill=255))


def line(pts, w):
    # A wide polyline in PIL leaves hairline gaps between its segments; under the outline pass
    # they showed as dark stripes across every stem. A tiny blur closes them.
    m = L(lambda d: d.line([tuple(p) for p in pts], fill=255, width=int(w), joint="curve"))
    return np.clip(blur(m, 2.0) * 1.8, 0, 1)


def bezier(p0, p1, p2, n=40):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1])
            for t in (i / n for i in range(n + 1))]


def leaf_shape(base, tip, width, bend=0.0):
    """A lens-shaped leaf from base to tip; bend pushes its midrib sideways."""
    bx, by = base
    tx, ty = tip
    dx, dy = tx - bx, ty - by
    ln = math.hypot(dx, dy)
    nx, ny = -dy / ln, dx / ln
    mid = (bx + dx * 0.5 + nx * bend * ln, by + dy * 0.5 + ny * bend * ln)
    left = bezier(base, (mid[0] + nx * width, mid[1] + ny * width), tip)
    right = bezier(tip, (mid[0] - nx * width, mid[1] - ny * width), base)
    return poly(left + right), bezier(base, mid, tip)


def shade(m, base, dark, lite=None, r=48, spec=0.55, spec_pow=36, flat=1.0):
    """Light a silhouette as a rounded volume. flat < 1 makes it read flatter (leaves)."""
    h = blur(m, r)
    h = np.clip((h - 0.3) / 0.7, 0, 1) ** 0.65
    gy, gx = np.gradient(h)
    k = r * 2.2 * flat
    n = np.dstack([-gx * k, -gy * k, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    d = n @ LIGHT
    base, dark = hexc(base), hexc(dark)
    t = np.clip((d - 0.05) / 0.75, 0, 1)[..., None]
    col = dark * (1 - t) + base * t
    if lite is not None:
        u = np.clip((d - 0.82) / 0.18, 0, 1)[..., None]
        col = col * (1 - u) + hexc(lite) * u
    s = np.clip(n @ HALF, 0, 1) ** spec_pow * spec
    col = col + (255 - col) * s[..., None]
    return col


def part(cv, m, base, dark, outline, lite=None, lw=9, **kw):
    cv.paint(grow(m, lw), hexc(outline))
    cv.paint(m, shade(m, base, dark, lite, **kw))


def dots(cv, clip, pts, r, color, alpha=1.0):
    m = union(*[ellipse(x, y, r) for x, y in pts]) * clip
    cv.paint(m, hexc(color), alpha)


def leaf(cv, base, tip, width, bend=0.0, col=("#7ACB5A", "#2F7A2E", "#1E5A22"), vein="#C8F0A0"):
    shape, rib = leaf_shape(base, tip, width, bend)
    part(cv, shape, col[0], col[1], col[2], r=26, flat=0.55, spec=0.25)
    cv.paint(line(rib[2:-6], 9) * shape, hexc(vein), 0.65)


def stem(cv, pts, w, col="#7A5230", outline="#3E2612"):
    cv.paint(grow(line(pts, w), 8), hexc(outline))
    cv.paint(line(pts, w), hexc(col))


def save(cv, name):
    img = cv.image()
    bbox = img.getbbox()
    # centre the drawing and leave a little air, so every icon fills its box the same way
    crop = img.crop(bbox)
    side = int(max(crop.width, crop.height) * 1.08)
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.alpha_composite(crop, ((side - crop.width) // 2, (side - crop.height) // 2))
    sq = sq.resize((FINAL, FINAL), Image.LANCZOS)
    path = os.path.join(OUT, name + ".png")
    sq.save(path)
    meta = path + ".meta"
    if os.path.exists(meta):
        m = open(meta).read()
        m = m.replace("enableMipMap: 0", "enableMipMap: 1")
        m = re.sub(r"(textureSettings:\s*\n\s*serializedVersion: \d+\s*\n\s*filterMode:) \d", r"\1 2", m)
        open(meta, "w").write(m)
    print("  ", name)


# ------------------------------------------------------------------ crops
def pepper():
    cv = Canvas()
    body = union(ellipse(390, 600, 175, 265), ellipse(634, 600, 175, 265), ellipse(512, 640, 200, 285))
    part(cv, body, "#F0452F", "#8C1B12", "#5E120B", lite="#FF8A6A", r=70)
    for x in (445, 580):
        cv.paint(line(bezier((x, 420), (x + (x - 512) * 0.2, 640), (x - (x - 512) * 0.3, 880)), 16) * body,
                 hexc("#8C1B12"), 0.35)
    cap = union(ellipse(512, 360, 150, 60), poly([(380, 360), (512, 440), (644, 360)]))
    part(cv, cap, "#62B84A", "#2C6B24", "#1D4A18", r=24)
    stem(cv, bezier((512, 350), (500, 260), (570, 180)), 44, col="#4E9A3A", outline="#1D4A18")
    save(cv, "pepper")


def eggplant():
    cv = Canvas()
    body = union(ellipse(560, 620, 240, 300), ellipse(430, 380, 130, 150))
    body = rotate(union(body, poly([(330, 420), (560, 320), (760, 560), (400, 700)])), 32)
    part(cv, body, "#8E4FB8", "#2E0F48", "#1F0A33", lite="#C79BE8", r=80, spec=0.8, spec_pow=50)
    cap = rotate(union(ellipse(420, 330, 150, 95),
                       poly([(300, 330), (330, 470), (420, 380)]),
                       poly([(540, 330), (520, 480), (440, 390)])), 32)
    part(cv, cap, "#6CBF4E", "#2C6B24", "#1D4A18", r=22)
    stem(cv, bezier((330, 290), (280, 220), (300, 140)), 46, col="#5EA83E", outline="#1D4A18")
    save(cv, "eggplant")


def grape():
    cv = Canvas()
    stem(cv, bezier((520, 300), (530, 200), (600, 120)), 34)
    leaf(cv, (540, 240), (800, 150), 110, 0.1)
    rows = [(330, [(-3, 0), (-1, 0), (1, 0), (3, 0)]), (455, [(-2, 0), (0, 0), (2, 0), (4, -0.3)]),
            (580, [(-3, 0.3), (-1, 0), (1, 0), (3, 0)]), (705, [(-2, 0), (0, 0), (2, 0)]),
            (825, [(-1, 0), (1, 0)]), (930, [(0, 0)])]
    for y, xs in rows:
        for gx, dy in xs:
            m = ellipse(512 + gx * 78, y + dy * 40, 86, 90)
            part(cv, m, "#9A62D8", "#34165E", "#231040", lite="#D2B0F5", r=40, spec=0.9, spec_pow=60, lw=7)
    save(cv, "grape")


def cherries():
    cv = Canvas()
    stem(cv, bezier((400, 640), (430, 380), (600, 180)), 22, col="#6E9A3A", outline="#2E4A18")
    stem(cv, bezier((680, 610), (660, 380), (600, 180)), 22, col="#6E9A3A", outline="#2E4A18")
    leaf(cv, (600, 190), (840, 250), 90, -0.15)
    for cx, cy in ((390, 730), (690, 700)):
        m = union(ellipse(cx - 60, cy, 130, 140), ellipse(cx + 60, cy, 130, 140))
        part(cv, m, "#E0283F", "#5E0716", "#420511", lite="#FF7F8E", r=60, spec=0.95, spec_pow=70)
    save(cv, "cherries")


def banana():
    cv = Canvas()

    def banana_shape(ox, oy, rot):
        outer = bezier((200, 380), (380, 900), (860, 700))
        inner = bezier((820, 640), (420, 760), (280, 360))
        m = poly(outer + inner)
        return rotate(m, rot, 512, 512), m

    for ox, rot, tone in ((0, 18, 0.9), (0, 0, 1.0), (0, -18, 0.95)):
        m, _ = banana_shape(ox, 0, rot)
        part(cv, m, "#FFD84A", "#C0800E", "#6A4208", lite="#FFF1A0", r=40, spec=0.4)
        tipr = rotate(ellipse(850, 690, 30), rot, 512, 512)
        cv.paint(tipr * grow(m, 2), hexc("#5A3A12"))
    top = rotate(rrect(180, 330, 290, 400, 30), -30, 235, 365)
    part(cv, top, "#8E8A3A", "#4A4214", "#2E2A0C", r=18)
    save(cv, "banana")


def pineapple():
    cv = Canvas()
    for ang, ln, w in ((-40, 330, 70), (-20, 380, 75), (0, 420, 80), (20, 380, 75), (40, 330, 70)):
        a = math.radians(ang - 90)
        leaf(cv, (512, 420), (512 + math.cos(a) * ln, 420 + math.sin(a) * ln), w, 0.05 * (1 if ang > 0 else -1),
             col=("#6CC45A", "#256B30", "#174A20"), vein="#B8F09A")
    body = ellipse(512, 660, 240, 300)
    part(cv, body, "#F5B640", "#9A5310", "#5E320A", lite="#FFE08A", r=70)
    grid = np.zeros((S, S), np.float32)
    for k in range(-8, 9):
        grid = np.maximum(grid, line([(512 + k * 80 - 400, 300), (512 + k * 80 + 400, 1100)], 12))
        grid = np.maximum(grid, line([(512 + k * 80 + 400, 300), (512 + k * 80 - 400, 1100)], 12))
    cv.paint(grid * body, hexc("#8A4A0E"), 0.45)
    save(cv, "pineapple")


def coconut():
    cv = Canvas()
    whole = ellipse(430, 520, 270, 260)
    part(cv, whole, "#9A6A42", "#3A2210", "#26160A", lite="#C89A6A", r=80, spec=0.2)
    rng = np.random.default_rng(3)
    fib = np.zeros((S, S), np.float32)
    for _ in range(90):
        x, y = rng.uniform(220, 640), rng.uniform(300, 740)
        a = rng.uniform(-0.6, 0.6) + 1.2
        fib = np.maximum(fib, line([(x, y), (x + math.cos(a) * 40, y + math.sin(a) * 40)], 7))
    cv.paint(fib * whole, hexc("#4A2E16"), 0.5)
    dots(cv, whole, [(390, 420), (460, 420), (425, 480)], 22, "#2A180A")
    shell = ellipse(650, 730, 250, 190)
    part(cv, shell, "#8A5A35", "#34200E", "#26160A", r=60, spec=0.2)
    flesh = ellipse(650, 700, 205, 140)
    cv.paint(grow(flesh, 6), hexc("#D8CDB8"))
    cv.paint(flesh, shade(flesh, "#FFFFFF", "#E2D6C0", r=50, flat=0.5, spec=0.2))
    water = ellipse(650, 712, 120, 78)
    cv.paint(water, shade(water, "#F3F0E6", "#CFC3AC", r=30, flat=0.4, spec=0.5))
    save(cv, "coconut")


def avocado():
    cv = Canvas()
    skin = union(ellipse(512, 660, 280, 300), ellipse(512, 380, 190, 220))
    part(cv, skin, "#4E8A32", "#1C3E12", "#12290B", r=60, spec=0.3)
    flesh = union(ellipse(512, 668, 240, 262), ellipse(512, 392, 158, 186))
    cv.paint(flesh, shade(flesh, "#E6F09A", "#9CC450", "#F8FFD0", r=70, flat=0.5, spec=0.2))
    rimm = flesh - np.clip(blur(flesh, 26) - 0.55, 0, 1) * 3
    cv.paint(np.clip(rimm, 0, 1) * flesh, hexc("#8CBF3C"), 0.35)
    pit = ellipse(512, 680, 132, 140)
    part(cv, pit, "#9A5A30", "#3E1E0A", "#2A1406", lite="#D69A6A", r=50, spec=0.8, spec_pow=40, lw=6)
    save(cv, "avocado")


def lemon():
    cv = Canvas()
    body = rotate(union(ellipse(512, 540, 320, 235), ellipse(210, 540, 60, 45), ellipse(814, 540, 60, 45)), -18)
    part(cv, body, "#FFE14A", "#C4870E", "#7A5208", lite="#FFF6B0", r=75, spec=0.5)
    rng = np.random.default_rng(5)
    dots(cv, body, [(rng.uniform(250, 780), rng.uniform(360, 720)) for _ in range(70)], 6, "#E0A81A", 0.35)
    leaf(cv, (600, 330), (820, 180), 80, 0.12)
    save(cv, "lemon")


def orange():
    cv = Canvas()
    body = ellipse(512, 580, 320)
    part(cv, body, "#FF9E22", "#B0500A", "#6E3208", lite="#FFD27A", r=85, spec=0.45)
    rng = np.random.default_rng(9)
    dots(cv, body, [(rng.uniform(220, 820), rng.uniform(280, 880)) for _ in range(110)], 6, "#D77410", 0.3)
    stem(cv, [(512, 280), (520, 230)], 30, col="#6A8A2A", outline="#2E3A12")
    leaf(cv, (525, 250), (760, 160), 85, 0.1)
    save(cv, "orange")


def pear():
    cv = Canvas()
    body = union(ellipse(512, 660, 270, 260), ellipse(500, 400, 150, 170),
                 poly([(360, 470), (650, 470), (760, 640), (270, 640)]))
    body = grow(blur(body, 30), 0) * 0 + np.clip((blur(body, 30) - 0.5) * 8, 0, 1)
    part(cv, body, "#CFE04A", "#6A861C", "#3E5410", lite="#F2F7A8", r=80, spec=0.4)
    blush = ellipse(640, 700, 150, 130) * body
    cv.paint(blur(blush, 40) * body, hexc("#F08A40"), 0.35)
    stem(cv, bezier((500, 250), (500, 190), (540, 130)), 30)
    leaf(cv, (515, 210), (730, 150), 75, 0.15)
    save(cv, "pear")


def broccoli():
    cv = Canvas()
    stalk = poly([(430, 620), (594, 620), (570, 920), (454, 920)])
    part(cv, stalk, "#A8D46A", "#5C8A2C", "#3A5A1A", r=40, flat=0.7)
    for bx, by, br in ((330, 520, 170), (512, 430, 200), (690, 520, 170), (420, 640, 150), (610, 640, 150)):
        m = ellipse(bx, by, br)
        part(cv, m, "#5AAE42", "#1E5418", "#143A10", lite="#9AD878", r=60, spec=0.2)
        rng = np.random.default_rng(bx)
        dots(cv, m, [(bx + rng.uniform(-br, br) * 0.8, by + rng.uniform(-br, br) * 0.8) for _ in range(40)],
             14, "#2E6E22", 0.45)
    save(cv, "broccoli")


def cauliflower():
    cv = Canvas()
    for base, tip, w in (((512, 760), (180, 520), 150), ((512, 760), (844, 520), 150),
                         ((512, 780), (300, 900), 120), ((512, 780), (724, 900), 120)):
        leaf(cv, base, tip, w, 0.08, col=("#6CBF52", "#246A24", "#174A18"), vein="#CFF3B0")
    for bx, by, br in ((380, 520, 150), (512, 440, 175), (650, 520, 150), (450, 620, 150), (580, 620, 150)):
        m = ellipse(bx, by, br)
        part(cv, m, "#FFF6DE", "#C2A874", "#8A7248", lite="#FFFFFF", r=55, spec=0.15, lw=7)
        rng = np.random.default_rng(bx + by)
        dots(cv, m, [(bx + rng.uniform(-br, br) * 0.75, by + rng.uniform(-br, br) * 0.75) for _ in range(34)],
             12, "#D8C49A", 0.45)
    save(cv, "cauliflower")


def cabbage():
    cv = Canvas()
    outer = ellipse(512, 560, 360, 330)
    part(cv, outer, "#6DB84E", "#23601E", "#164416", lite="#A8E08A", r=90, spec=0.2)
    for cx, cy, rx, ry in ((420, 520, 230, 220), (600, 500, 230, 220), (512, 420, 190, 170)):
        m = ellipse(cx, cy, rx, ry)
        part(cv, m, "#9BD774", "#3C8A30", "#235A1E", lite="#D8F5B8", r=70, spec=0.2, lw=7)
        cv.paint(line(bezier((cx, cy + ry * 0.8), (cx - rx * 0.1, cy), (cx + rx * 0.2, cy - ry * 0.7)), 10) * m,
                 hexc("#E4F7CC"), 0.6)
    save(cv, "cabbage")


def mushroom():
    cv = Canvas()
    stem_m = union(rrect(420, 480, 604, 880, 80), ellipse(512, 880, 110, 50))
    part(cv, stem_m, "#F6EBD0", "#B39C74", "#6E5A3A", r=50)
    gills = ellipse(512, 500, 300, 70)
    part(cv, gills, "#E8D6B0", "#9A8058", "#6E5A3A", r=20, flat=0.3)
    cap = ellipse(512, 470, 350, 300) * rrect(0, 0, S, 500, 0)
    cap = np.clip(blur(cap, 6) * 1.5, 0, 1)
    part(cv, cap, "#D65A30", "#6E2410", "#4A1808", lite="#FF9A6A", r=80, spec=0.5)
    dots(cv, cap, [(400, 300), (560, 250), (660, 360), (470, 410), (300, 420), (700, 440)], 34, "#FFF3DA", 0.95)
    save(cv, "mushroom")


def leek():
    cv = Canvas()
    for tip, w in (((260, 120), 60), ((520, 80), 64), ((780, 160), 60)):
        leaf(cv, (512, 520), tip, w, 0.02, col=("#5DB84A", "#1F6A24", "#154A18"), vein="#B8F09A")
    shaft = rrect(440, 430, 584, 900, 70)
    grad = np.zeros((S, S, 3), np.float32)
    t = np.clip((np.arange(S) - 430) / 470.0, 0, 1)[:, None, None]
    part(cv, shaft, "#F4F1DC", "#B8B28A", "#6E6A48", r=40, flat=0.8)
    cv.paint(shaft * (1 - np.clip((np.arange(S)[:, None] - 430) / 250.0, 0, 1)), hexc("#8ACB5A"), 0.75)
    roots = union(*[line([(512, 890), (512 + dx, 960)], 10) for dx in (-60, -30, 0, 30, 60)])
    cv.paint(grow(roots, 5), hexc("#6E6A48"))
    cv.paint(roots, hexc("#E8E0C0"))
    save(cv, "leek")


def garlic():
    """Hành Bổ — a red onion, cut to show its rings beside a whole one."""
    cv = Canvas()
    bulb = union(ellipse(450, 600, 280, 270), poly([(330, 420), (450, 170), (570, 420)]))
    bulb = np.clip((blur(bulb, 26) - 0.5) * 8, 0, 1)
    part(cv, bulb, "#B24A86", "#4E1438", "#34092A", lite="#E6A0CA", r=80, spec=0.5)
    for k in (-2, -1, 1, 2):
        cv.paint(line(bezier((450 + k * 40, 340), (450 + k * 130, 600), (450 + k * 60, 860)), 10) * bulb,
                 hexc("#E8B0D2"), 0.35)
    stem(cv, bezier((450, 200), (455, 150), (480, 100)), 26, col="#C9A070", outline="#5A3A1A")
    half = ellipse(700, 760, 220, 170)
    part(cv, half, "#A23E78", "#4E1438", "#34092A", r=40, spec=0.2)
    for i, rr in enumerate((190, 150, 110, 70, 32)):
        ring = ellipse(700, 755, rr, rr * 0.75)
        cv.paint(ring, hexc("#F7E8F0" if i % 2 == 0 else "#D7A6C4"))
    save(cv, "garlic")


def radish():
    cv = Canvas()
    leaf(cv, (512, 420), (300, 120), 90, -0.1)
    leaf(cv, (512, 420), (720, 110), 90, 0.1)
    leaf(cv, (512, 420), (520, 70), 80, 0.0)
    body = union(ellipse(512, 590, 230, 220), poly([(360, 690), (664, 690), (520, 960), (504, 960)]))
    body = np.clip((blur(body, 22) - 0.5) * 8, 0, 1)
    part(cv, body, "#EE4A74", "#86163A", "#5A0E28", lite="#FF9AB6", r=70, spec=0.6)
    tip = body * np.clip((np.arange(S)[:, None] - 740) / 120.0, 0, 1)
    cv.paint(tip, hexc("#FFF2F4"), 0.95)
    save(cv, "radish")


def beetroot():
    cv = Canvas()
    for base, tip, w, bend in (((512, 440), (260, 120), 100, -0.1), ((512, 440), (760, 110), 100, 0.1),
                               ((512, 440), (500, 60), 90, 0.03)):
        stem(cv, [base, ((base[0] + tip[0]) / 2, (base[1] + tip[1]) / 2 + 30)], 22, col="#B0285A", outline="#4A0A22")
        leaf(cv, ((base[0] + tip[0]) / 2, (base[1] + tip[1]) / 2 + 30), tip, w, bend,
             col=("#5CAE4A", "#1E5A22", "#143E16"), vein="#E04A7A")
    body = union(ellipse(512, 640, 270, 250), poly([(400, 780), (624, 780), (530, 990), (500, 990)]))
    body = np.clip((blur(body, 22) - 0.5) * 8, 0, 1)
    part(cv, body, "#A8245A", "#3E0820", "#2A0616", lite="#E26A98", r=80, spec=0.5)
    save(cv, "beetroot")


# ------------------------------------------------------------------ yard decorations (cosmetics)
def rose_bush():
    """Bụi Hồng: a low round bush with roses, painted like the crops so it sits with the props."""
    cv = Canvas()
    rng = np.random.default_rng(12)
    body = union(ellipse(512, 640, 330, 230), ellipse(360, 600, 180, 170), ellipse(670, 590, 190, 170), ellipse(512, 500, 210, 180))
    body = np.clip((blur(body, 18) - 0.5) * 8, 0, 1)
    part(cv, body, "#5FAE48", "#1F5A22", "#153E16", lite="#A8E08A", r=70, spec=0.15)
    for _ in range(60):
        x, y = rng.uniform(220, 800), rng.uniform(380, 820)
        a = rng.uniform(0, 180)
        lm = rotate(ellipse(x, y, 34, 16), a, x, y) * body
        cv.paint(lm, hexc("#7ACB5A" if rng.random() < 0.5 else "#2F7A2E"), 0.55)
    for x, y, r in ((380, 520, 58), (560, 440, 64), (700, 560, 54), (470, 660, 56), (640, 700, 50), (300, 680, 44)):
        rose = ellipse(x, y, r)
        part(cv, rose, "#F0506E", "#8A1230", "#5A0A20", lite="#FF9AB0", r=26, spec=0.35, lw=7)
        for k, rr in enumerate((0.72, 0.46, 0.22)):
            arc = L(lambda d, rr=rr, k=k: d.arc((x - r * rr, y - r * rr, x + r * rr, y + r * rr), 200 + k * 70, 470 + k * 70, fill=255, width=9))
            cv.paint(arc * rose, hexc("#8A1230"), 0.55)
    save_to(cv, "decor_roses")


def sunflowers():
    """Hướng Dương: three sunflowers on stems over a leafy clump."""
    cv = Canvas()
    for x0, x1, y1 in ((420, 380, 250), (560, 600, 180), (660, 720, 330)):
        stem(cv, bezier((x0, 900), ((x0 + x1) / 2 + 20, 600), (x1, y1 + 40)), 26, col="#4E9A3A", outline="#1D4A18")
    for base, tip, w in (((470, 860), (270, 700), 80), ((560, 860), (780, 690), 80), ((520, 880), (420, 720), 60), ((600, 880), (700, 770), 55)):
        leaf(cv, base, tip, w, 0.1, col=("#6CBF4E", "#256B24", "#174A18"))
    for x, y, r in ((380, 250, 120), (600, 180, 132), (720, 330, 110)):
        petals = np.zeros((S, S), np.float32)
        for k in range(14):
            a = k * math.pi * 2 / 14
            px, py = x + math.cos(a) * r * 0.78, y + math.sin(a) * r * 0.78
            petals = np.maximum(petals, rotate(ellipse(px, py, r * 0.42, r * 0.17), -math.degrees(a), px, py))
        part(cv, petals, "#FFD23F", "#D07A08", "#7A4A08", lite="#FFF0A0", r=20, spec=0.2, lw=7)
        centre = ellipse(x, y, r * 0.48)
        part(cv, centre, "#7A4A22", "#3A200C", "#241406", r=24, spec=0.1, lw=6)
        rng = np.random.default_rng(int(x))
        dots(cv, centre, [(x + rng.uniform(-0.4, 0.4) * r, y + rng.uniform(-0.4, 0.4) * r) for _ in range(30)], 6, "#A87040", 0.6)
    save_to(cv, "decor_sunflowers")


def save_to(cv, name):
    global OUT
    keep = OUT
    OUT = "Assets/Resources/Art/items"
    save(cv, name)
    OUT = keep


if __name__ == "__main__" and len(__import__("sys").argv) > 1 and __import__("sys").argv[1] == "decor":
    rose_bush()
    sunflowers()
    raise SystemExit(0)

if __name__ == "__main__":
    print("produce ->", OUT)
    for fn in (pepper, eggplant, grape, cherries, banana, pineapple, coconut, avocado, lemon, orange,
               pear, broccoli, cauliflower, cabbage, mushroom, leek, garlic, radish, beetroot):
        fn()
