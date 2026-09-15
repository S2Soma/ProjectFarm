"""Art for the islands' ambient life (IslandLife): creatures, swaying plants, animated props, and the
tileable noise every ambience shader reads.

    uv run --with pillow --with numpy python Tools/gen_life.py            # everything
    uv run --with pillow --with numpy python Tools/gen_life.py noise bugs # just some groups

Style
-----
The same painted-sticker language as the farm props: one warm dark outline, flat fills lit from the
top left (a light crescent on the upper-left edge, a shade crescent on the lower-right), a small
highlight. Drawn at 4x and reduced with Lanczos (that is the anti-aliasing). Everything the game
tints by the time of day is painted in its real colours; the particle sprites are white.

Atlases for MiT/UI Sway are regular grids (IslandLife.Atlas mirrors the column/row counts): every cell
has transparent padding on both sides so a plant can lean without its tip leaving the cell, and its
root sits on the cell's bottom edge (a hanging vine's anchor on the top edge).

Props are single sprites standing on their bottom-centre, like Art/farm props; the ones with a moving
part (flag pole top, rod orb, vent mouth) have that point listed in ANCHORS and mirrored in IslandLife.
Only props with a moving part or a light of their own are drawn here now: the windmill, the pinwheels and
the crystals were replaced (15/9) by the owner's painted decor (Tools/slice_decor.py), which the flat
sticker style could not stand next to. The wind atlas still carries the blade and pinwheel cells.
"""
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
import math, os, random, sys

OUT = "Assets/Resources/Art/life"
os.makedirs(OUT, exist_ok=True)
INK = (58, 38, 26)
SS = 4


def hexc(s):
    s = s.lstrip("#")
    return np.array([int(s[i:i + 2], 16) for i in (0, 2, 4)], np.float32)


def save(img, name):
    img.save(os.path.join(OUT, name + ".png"))
    print("  ", name, img.size)


# ============================================================ painter
class Pic:
    """Premultiplied float canvas at SS x, painted with masks; coordinates in final pixels."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.W, self.H = w * SS, h * SS
        self.p = np.zeros((self.H, self.W, 3), np.float32)
        self.a = np.zeros((self.H, self.W), np.float32)

    # ---- shapes (final-pixel coordinates) ----
    def _m(self, fn):
        im = Image.new("L", (self.W, self.H), 0)
        fn(ImageDraw.Draw(im))
        return np.asarray(im, np.float32) / 255.0

    def ellipse(self, cx, cy, rx, ry=None, rot=0.0):
        ry = rx if ry is None else ry
        if abs(rot) < 1e-3:
            return self._m(lambda d: d.ellipse(((cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS), fill=255))
        pts = []
        for k in range(48):
            a = k / 48 * math.tau
            x, y = math.cos(a) * rx, math.sin(a) * ry
            pts.append((cx + x * math.cos(rot) - y * math.sin(rot), cy + x * math.sin(rot) + y * math.cos(rot)))
        return self.poly(pts)

    def poly(self, pts):
        return self._m(lambda d: d.polygon([(x * SS, y * SS) for x, y in pts], fill=255))

    def line(self, pts, width):
        def fn(d):
            q = [(x * SS, y * SS) for x, y in pts]
            d.line(q, fill=255, width=max(1, int(width * SS)), joint="curve")
            r = width * SS * 0.5
            for x, y in (q[0], q[-1]):
                d.ellipse((x - r, y - r, x + r, y + r), fill=255)
        return self._m(fn)

    def rect(self, x0, y0, x1, y1, r=0):
        return self._m(lambda d: d.rounded_rectangle((x0 * SS, y0 * SS, x1 * SS, y1 * SS), r * SS, fill=255))

    # ---- mask ops ----
    @staticmethod
    def union(*ms):
        out = np.zeros_like(ms[0])
        for m in ms:
            out = np.maximum(out, m)
        return out

    def grow(self, m, px):
        im = Image.fromarray((np.clip(m, 0, 1) * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(px * SS * 0.5))
        a = np.asarray(im, np.float32) / 255.0
        return np.clip((a - 0.02) * 25.0, 0, 1)

    def shift(self, m, dx, dy):
        sx, sy = int(round(dx * SS)), int(round(dy * SS))
        out = np.zeros_like(m)
        H, W = m.shape
        ys0, ys1 = max(0, sy), min(H, H + sy)
        xs0, xs1 = max(0, sx), min(W, W + sx)
        out[ys0:ys1, xs0:xs1] = m[ys0 - sy:ys1 - sy, xs0 - sx:xs1 - sx]
        return out

    def soft(self, m, px):
        im = Image.fromarray((np.clip(m, 0, 1) * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(px * SS))
        return np.asarray(im, np.float32) / 255.0

    # ---- paint ----
    def fill(self, m, col, alpha=1.0):
        mm = np.clip(m, 0, 1) * alpha
        c = col if isinstance(col, np.ndarray) and col.ndim == 3 else np.asarray(col, np.float32)
        self.p = c * mm[..., None] + self.p * (1 - mm[..., None])
        self.a = mm + self.a * (1 - mm)

    def ink(self, m, width=1.6):
        self.fill(self.grow(m, width), INK)

    def lit(self, m, base, hi=None, lo=None, d=2.0, hi_a=0.9, lo_a=0.9):
        """Fill with a light crescent top-left and a shade crescent bottom-right."""
        b = hexc(base)
        hi = hexc(hi) if hi else np.clip(b * 1.22 + 18, 0, 255)
        lo = hexc(lo) if lo else b * 0.72
        self.fill(m, b)
        shade = m * (1 - self.shift(m, -d, -d))
        light = m * (1 - self.shift(m, d * 0.8, d * 0.8))
        self.fill(self.soft(shade, 0.5) * m, lo, lo_a)
        self.fill(self.soft(light, 0.5) * m, hi, hi_a)

    def light_mask(self):
        """For MiT/UI Glow: rgb = light (colour x amount, black where none), a = where it may sparkle."""
        rgb = np.clip(self.p, 0, 255)
        out = np.dstack([rgb, np.clip(self.a, 0, 1) * 255]).astype(np.uint8)
        return Image.fromarray(out, "RGBA").resize((self.w, self.h), Image.LANCZOS)

    def image(self):
        a = np.clip(self.a, 1e-6, 1)
        rgb = np.clip(self.p / a[..., None], 0, 255)
        out = np.dstack([rgb, np.clip(self.a, 0, 1) * 255]).astype(np.uint8)
        return Image.fromarray(out, "RGBA").resize((self.w, self.h), Image.LANCZOS)


def grad_v(pic, top, bottom, y0, y1):
    t = np.clip((np.arange(pic.H, dtype=np.float32) / SS - y0) / max(1e-3, y1 - y0), 0, 1)
    c0, c1 = hexc(top), hexc(bottom)
    col = c0[None, :] * (1 - t[:, None]) + c1[None, :] * t[:, None]
    return np.broadcast_to(col[:, None, :], (pic.H, pic.W, 3)).copy()


# ============================================================ noise
def periodic_noise(size, cells, seed):
    rng = np.random.RandomState(seed)
    g = rng.rand(cells, cells).astype(np.float32)
    tiled = np.tile(g, (3, 3))
    im = Image.fromarray((tiled * 255).astype(np.uint8)).resize((size * 3, size * 3), Image.BICUBIC)
    return np.asarray(im, np.float32)[size:2 * size, size:2 * size] / 255.0


def fbm(size, base, octaves, seed):
    total = np.zeros((size, size), np.float32)
    amp, norm, cells = 1.0, 0.0, base
    for o in range(octaves):
        total += periodic_noise(size, cells, seed + o * 17) * amp
        norm += amp
        amp *= 0.5
        cells *= 2
    t = total / norm
    return (t - t.min()) / (t.max() - t.min() + 1e-6)


def gen_noise():
    S = 256
    r, g, b = fbm(S, 4, 4, 11), fbm(S, 8, 3, 23), fbm(S, 16, 3, 37)
    img = np.dstack([r, g, b, np.ones_like(r)]) * 255
    save(Image.fromarray(img.astype(np.uint8), "RGBA"), "tile_noise")


# ============================================================ creatures
def butterfly(name, wing, wing_hi, edge, spot):
    """Seen from above, wings open; the game flaps it by squashing x."""
    P = Pic(96, 80)
    cx, cy = 48, 40
    up_l = P.union(P.ellipse(cx - 21, cy - 12, 21, 15, rot=-0.55), P.ellipse(cx - 12, cy - 5, 10, 8))
    lo_l = P.union(P.ellipse(cx - 15, cy + 15, 13, 11, rot=0.7), P.ellipse(cx - 7, cy + 6, 7, 7))
    up_r, lo_r = up_l[:, ::-1], lo_l[:, ::-1]
    wings = P.union(up_l, up_r, lo_l, lo_r)
    body = P.ellipse(cx, cy + 3, 3.0, 15)
    head = P.ellipse(cx, cy - 12, 3.8)
    antennae = P.union(P.line([(cx - 1, cy - 14), (cx - 6, cy - 25), (cx - 9, cy - 27)], 1.1),
                       P.line([(cx + 1, cy - 14), (cx + 6, cy - 25), (cx + 9, cy - 27)], 1.1))
    P.ink(P.union(wings, body, head, antennae), 1.7)
    for m in (lo_l, lo_r):
        P.lit(m, wing, wing_hi, d=2.2)
        P.fill(np.clip((m - P.soft(m, 2.2)) * 3.0, 0, 1), hexc(edge), 0.9)
    for m in (up_l, up_r):
        P.lit(m, wing, wing_hi, d=2.6)
        P.fill(np.clip((m - P.soft(m, 2.6)) * 3.0, 0, 1), hexc(edge), 0.9)
        # a soft vein fan from the body
    for side in (-1, 1):
        for (dx, dy, r) in ((36, -8, 2.2), (32, -17, 1.9), (25, -23, 1.6), (22, 21, 1.8), (26, 15, 1.4)):
            P.fill(P.ellipse(cx + side * dx, cy + dy, r), hexc(spot))
        P.fill(P.ellipse(cx + side * 18, cy - 14, 6, 4, rot=-0.5 * side), hexc(wing_hi), 0.55)
    P.fill(P.union(body, head), hexc("#3A2A22"))
    P.fill(P.ellipse(cx - 1, cy - 2, 1.1, 7), hexc("#6B5040"), 0.8)
    P.fill(antennae, INK)
    save(P.image(), name)


def bee():
    P = Pic(96, 96)
    cx, cy = 50, 56
    body = P.ellipse(cx, cy, 30, 24)
    head = P.ellipse(cx - 27, cy - 6, 14, 13)
    wing_l = P.ellipse(cx - 6, cy - 30, 13, 20, rot=-0.5)
    wing_r = P.ellipse(cx + 12, cy - 28, 11, 18, rot=0.45)
    sting = P.poly([(cx + 28, cy - 3), (cx + 40, cy + 2), (cx + 28, cy + 8)])
    P.ink(P.union(body, head, sting, wing_l, wing_r), 2.0)
    P.lit(sting, "#4A3524")
    P.lit(body, "#F7C63F", "#FFE38A", "#D89A22", d=4)
    # stripes follow the body's curve
    for sx in (-4, 10, 23):
        stripe = P.ellipse(cx + sx, cy, 5.5, 26) * body
        P.fill(stripe, hexc("#4A3524"))
    P.lit(head, "#4A3524", "#6D5440", "#2E2018", d=3)
    P.fill(P.ellipse(cx - 32, cy - 9, 3.4), hexc("#FFFFFF"))
    P.fill(P.ellipse(cx - 32.5, cy - 8.5, 1.8), hexc("#201510"))
    P.fill(P.ellipse(cx - 20, cy - 16, 5, 3), hexc("#FFFFFF"), 0.25)
    # fuzz highlight
    P.fill(P.ellipse(cx - 6, cy - 14, 14, 6, rot=-0.2) * body, hexc("#FFF3C4"), 0.55)
    for w in (wing_l, wing_r):
        P.fill(w, hexc("#E6F6FF"), 0.72)
        P.fill(P.soft(w * (1 - P.shift(w, 2, 2)), 0.4) * w, hexc("#FFFFFF"), 0.8)
    # antennae
    ant = P.union(P.line([(cx - 33, cy - 16), (cx - 40, cy - 30)], 1.6), P.line([(cx - 27, cy - 18), (cx - 28, cy - 33)], 1.6))
    P.fill(ant, INK)
    P.fill(P.union(P.ellipse(cx - 40, cy - 31, 2.6), P.ellipse(cx - 28, cy - 34, 2.6)), INK)
    save(P.image(), "bee")


def bird():
    """A gull gliding, wings up in a soft M; the game flaps it by squashing y."""
    P = Pic(128, 64)
    cx, cy = 64, 34
    wl = P.poly([(cx - 4, cy - 2), (cx - 22, cy - 16), (cx - 44, cy - 20), (cx - 60, cy - 12), (cx - 40, cy - 10), (cx - 22, cy - 4), (cx - 6, cy + 5)])
    wr = wl[:, ::-1]
    body = P.ellipse(cx, cy + 1, 9, 6)
    head = P.ellipse(cx - 1, cy - 5, 5.2)
    tail = P.poly([(cx - 4, cy + 4), (cx + 4, cy + 4), (cx, cy + 14)])
    sil = P.union(wl, wr, body, head, tail)
    P.ink(sil, 1.6)
    P.lit(P.union(wl, wr), "#F4F6FA", "#FFFFFF", "#C8CFDC", d=2.0)
    tips = P.union(P.poly([(cx - 44, cy - 21), (cx - 61, cy - 12), (cx - 46, cy - 11)]), P.poly([(cx + 44, cy - 21), (cx + 61, cy - 12), (cx + 46, cy - 11)]))
    P.fill(tips * sil, hexc("#5E6675"))
    P.lit(P.union(body, tail), "#F7F8FB", "#FFFFFF", "#CBD2DE", d=1.6)
    P.lit(head, "#FAFBFD", "#FFFFFF", "#D0D6E2", d=1.2)
    P.fill(P.poly([(cx - 1.8, cy - 3), (cx + 1.8, cy - 3), (cx, cy + 2)]), hexc("#F2A93B"))
    save(P.image(), "bird")


def fish():
    """A little koi, side view, head to the right: the game rotates it along its jump."""
    P = Pic(96, 48)
    body = P.ellipse(52, 24, 26, 11)
    tail = P.poly([(30, 24), (10, 10), (15, 24), (10, 38)])
    fin_t = P.poly([(46, 14), (58, 5), (64, 15)])
    fin_b = P.poly([(50, 33), (58, 42), (62, 33)])
    sil = P.union(body, tail, fin_t, fin_b)
    P.ink(sil, 1.6)
    P.lit(P.union(tail, fin_t, fin_b), "#F48A4A", "#FFC08A", "#C8602C", d=1.6)
    P.lit(body, "#FFF4EA", "#FFFFFF", "#E1CFC2", d=2.5)
    for (x, y, rx, ry) in ((56, 19, 9, 6), (40, 27, 7, 5), (68, 27, 6, 4)):
        P.fill(P.ellipse(x, y, rx, ry) * body, hexc("#F26A2E"))
    P.fill(P.ellipse(70, 20, 2.4), INK)
    P.fill(P.ellipse(70.6, 19.4, 0.8), hexc("#FFFFFF"))
    P.fill(P.ellipse(50, 16, 12, 2.5) * body, hexc("#FFFFFF"), 0.6)
    save(P.image(), "fish")


def dragonfly():
    P = Pic(96, 64)
    cx, cy = 48, 32
    body = P.line([(cx - 34, cy + 1), (cx + 8, cy)], 4.2)
    thorax = P.ellipse(cx + 12, cy, 7, 5.5)
    head = P.ellipse(cx + 22, cy, 5.5, 5.5)
    wings = [P.ellipse(cx + 4, cy - 14, 22, 5.5, rot=-0.18), P.ellipse(cx + 4, cy + 14, 22, 5.5, rot=0.18),
             P.ellipse(cx + 18, cy - 13, 18, 4.5, rot=-0.45), P.ellipse(cx + 18, cy + 13, 18, 4.5, rot=0.45)]
    P.ink(P.union(body, thorax, head), 1.4)
    for w in wings:
        P.fill(P.grow(w, 1.0), hexc("#2E5A60"), 0.45)
        P.fill(w, hexc("#DDF6F8"), 0.55)
        P.fill(P.soft(w * (1 - P.shift(w, 1.5, 1.5)), 0.3) * w, hexc("#FFFFFF"), 0.7)
    P.lit(body, "#2FA7A0", "#7FE0D2", "#1C6F6A", d=1.2)
    for x in range(-30, 6, 6):
        P.fill(P.ellipse(cx + x, cy + 0.5, 0.9, 2.2), hexc("#155450"))
    P.lit(thorax, "#2E8FA8", "#79D2E6", "#1C5E70", d=1.4)
    P.lit(head, "#3AA0B8", "#9AE6F4", "#205E70", d=1.2)
    P.fill(P.ellipse(cx + 24, cy - 2.5, 2.2), hexc("#123840"))
    P.fill(P.ellipse(cx + 24, cy + 2.5, 2.2), hexc("#123840"))
    save(P.image(), "dragonfly")


def gen_bugs():
    butterfly("butterfly_a", "#F59A3C", "#FFD08A", "#5A3322", "#FFF4DA")
    butterfly("butterfly_b", "#9CC8F5", "#DDF0FF", "#46528A", "#FFFFFF")
    bee()
    bird()
    fish()
    dragonfly()


# ============================================================ plants
LEAF_INK = hexc("#2C4A22")


def bez(p0, c, p1, t):
    return ((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * c[0] + t * t * p1[0],
            (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * c[1] + t * t * p1[1])


def blade(P, x, y, length, angle, bend, width, n=12, taper=0.9):
    """A tapered curved blade from (x, y) upward; angle from vertical (radians, + leans right)."""
    tip = (x + math.sin(angle) * length, y - math.cos(angle) * length)
    ca = angle + bend
    c = (x + math.sin(ca) * length * 0.55, y - math.cos(ca) * length * 0.55)
    left, right = [], []
    for i in range(n + 1):
        t = i / n
        px, py = bez((x, y), c, tip, t)
        qx, qy = bez((x, y), c, tip, min(1, t + 1e-3))
        dx, dy = qx - px, qy - py
        L = math.hypot(dx, dy) + 1e-6
        nx, ny = -dy / L, dx / L
        w = width * 0.5 * (1 - t) ** taper + 0.25
        left.append((px + nx * w, py + ny * w))
        right.append((px - nx * w, py - ny * w))
    return P.poly(left + right[::-1]), tip


def stem(P, x, y, length, angle, bend, width):
    tip = (x + math.sin(angle) * length, y - math.cos(angle) * length)
    ca = angle + bend
    c = (x + math.sin(ca) * length * 0.55, y - math.cos(ca) * length * 0.55)
    pts = [bez((x, y), c, tip, i / 10) for i in range(11)]
    return P.line(pts, width), tip


def petals(P, cx, cy, n, r, pr, squash=0.8, rot=0.0):
    ms = []
    for k in range(n):
        a = rot + k / n * math.tau
        ms.append(P.ellipse(cx + math.cos(a) * r, cy + math.sin(a) * r * squash, pr, pr * 0.62, rot=a))
    return P.union(*ms)


def cell_grass(P, ox, oy, cw, ch, rng, greens=("#5FAE45", "#7CC456", "#4C9A3A"), tall=1.0, n=7):
    base_x, base_y = ox + cw / 2, oy + ch - 3
    blades = []
    for k in range(n):
        ang = (k / (n - 1) - 0.5) * 1.1 + rng.uniform(-0.12, 0.12)
        L = rng.uniform(0.42, 0.7) * ch * tall * (1 - abs(ang) * 0.35)
        m, _ = blade(P, base_x + ang * 10, base_y, L, ang, ang * 0.6, rng.uniform(6, 9))
        blades.append((m, rng.choice(greens)))
    sil = P.union(*[m for m, _ in blades])
    P.fill(P.grow(sil, 1.3), LEAF_INK)
    for m, g in blades:
        P.lit(m, g, d=1.2)
    return sil


def cell_flowers(P, ox, oy, cw, ch, rng, petal, centre, n_heads=3, petal_n=8, head_r=7.5, leaf="#5AA844"):
    base_x, base_y = ox + cw / 2, oy + ch - 3
    parts = []
    for k in range(7):
        ang = (k / 6 - 0.5) * 1.5
        m, _ = blade(P, base_x + ang * 10, base_y, rng.uniform(0.26, 0.40) * ch, ang, ang * 0.8, 11)
        parts.append(("leaf", m))
    heads = []
    for k in range(n_heads):
        ang = (k / max(1, n_heads - 1) - 0.5) * 0.95 + rng.uniform(-0.08, 0.08)
        L = rng.uniform(0.38, 0.62) * ch
        sm, tip = stem(P, base_x + ang * 6, base_y, L, ang, -ang * 0.4, 2.6)
        parts.append(("stem", sm))
        heads.append(tip)
    sil = P.union(*[m for _, m in parts])
    head_ms = [petals(P, hx, hy, petal_n, head_r * 0.9, head_r * 0.75, rot=rng.uniform(0, 1)) for hx, hy in heads]
    P.fill(P.grow(P.union(sil, *head_ms), 1.3), LEAF_INK)
    for kind, m in parts:
        P.lit(m, leaf if kind == "leaf" else "#4E9A3C", d=1.1)
    for (hx, hy), hm in zip(heads, head_ms):
        P.lit(hm, petal, d=1.4)
        c = P.ellipse(hx, hy, head_r * 0.42)
        P.lit(c, centre, d=1.0)


def cell_fern(P, ox, oy, cw, ch, rng, green="#4F9E3E", scale=1.0):
    base_x, base_y = ox + cw / 2, oy + ch - 3
    fronds = []
    for k in range(5):
        ang = (k / 4 - 0.5) * 1.6
        L = ch * rng.uniform(0.62, 0.82) * (1 - abs(ang) * 0.2)
        tip = (base_x + math.sin(ang) * L, base_y - math.cos(ang) * L * 0.85)
        c = (base_x + math.sin(ang * 0.4) * L * 0.5, base_y - L * 0.75)
        pts = [bez((base_x, base_y), c, tip, i / 12) for i in range(13)]
        ms = [P.line(pts, 2.2 * scale)]
        for i in range(2, 12):
            px, py = pts[i]
            qx, qy = pts[i + 1]
            dx, dy = qx - px, qy - py
            Ld = math.hypot(dx, dy) + 1e-6
            nx, ny = -dy / Ld, dx / Ld
            lw = (1 - i / 13) * 9 * scale + 2
            for sgn in (-1, 1):
                ms.append(P.ellipse(px + nx * lw * 0.55 * sgn, py + ny * lw * 0.55 * sgn, lw * 0.6, 2.4 * scale,
                                    rot=math.atan2(ny * sgn, nx * sgn)))
        fronds.append(P.union(*ms))
    sil = P.union(*fronds)
    P.fill(P.grow(sil, 1.2), LEAF_INK)
    for f in fronds:
        P.lit(f, green, d=1.1)


def cell_reeds(P, ox, oy, cw, ch, rng, cattails=False):
    base_x, base_y = ox + cw / 2, oy + ch - 3
    blades = []
    for k in range(6):
        ang = (k / 5 - 0.5) * 0.5 + rng.uniform(-0.05, 0.05)
        L = rng.uniform(0.6, 0.92) * ch
        m, _ = blade(P, base_x + ang * 14, base_y, L, ang, ang * 0.3 + rng.uniform(-0.15, 0.15), 6.5, taper=1.1)
        blades.append((m, rng.choice(("#4E8F3E", "#5E9E48", "#437F36"))))
    heads = []
    if cattails:
        for k in range(3):
            ang = (k - 1) * 0.16
            L = rng.uniform(0.7, 0.9) * ch
            sm, tip = stem(P, base_x + ang * 10, base_y, L, ang, 0, 2.2)
            blades.append((sm, "#5E8E40"))
            heads.append(P.ellipse(tip[0] + math.sin(ang) * 10, tip[1] + 12, 5, 13, rot=ang))
    sil = P.union(*[m for m, _ in blades], *heads) if heads else P.union(*[m for m, _ in blades])
    P.fill(P.grow(sil, 1.2), LEAF_INK)
    for m, g in blades:
        P.lit(m, g, d=1.0)
    for h in heads:
        P.lit(h, "#7A4A2A", "#A87450", "#4E2E18", d=1.4)


def atlas(name, cols, rows, cw, ch, painters, seed=1):
    """Each cell is painted on a canvas of its own (every mask op is full-canvas, so a shared
    atlas-sized canvas made each stroke 8x the work) and pasted into place."""
    out = Image.new("RGBA", (cols * cw, rows * ch), (0, 0, 0, 0))
    rng = random.Random(seed)
    for i, fn in enumerate(painters):
        if fn is None:
            continue
        P = Pic(cw, ch)
        fn(P, 0, 0, cw, ch, rng)
        out.paste(P.image(), ((i % cols) * cw, (i // cols) * ch))
    save(out, name)


def gen_meadow():
    atlas("life_meadow", 4, 2, 128, 160, [
        lambda P, ox, oy, cw, ch, r: cell_grass(P, ox, oy, cw, ch, r, n=8, tall=0.8),
        lambda P, ox, oy, cw, ch, r: (cell_grass(P, ox, oy, cw, ch, r, greens=("#77B64C", "#8FC45E", "#6AA640"), tall=1.15, n=6),
                                      [P.lit(P.ellipse(ox + cw / 2 + dx, oy + ch * 0.28 + dy, 3.4, 9, rot=dx * 0.02), "#D9C27A", d=1.0)
                                       for dx, dy in ((-16, 14), (4, -2), (20, 18))]),
        lambda P, ox, oy, cw, ch, r: cell_flowers(P, ox, oy, cw, ch, r, "#FFFFFF", "#F6C433", n_heads=4, petal_n=10, head_r=10),
        lambda P, ox, oy, cw, ch, r: cell_flowers(P, ox, oy, cw, ch, r, "#F59AC0", "#FFE07A", n_heads=4, petal_n=5, head_r=10.5),
        lambda P, ox, oy, cw, ch, r: cell_flowers(P, ox, oy, cw, ch, r, "#FFD23C", "#F29A1E", n_heads=5, petal_n=5, head_r=8),
        lambda P, ox, oy, cw, ch, r: cell_fern(P, ox, oy, cw, ch, r, scale=1.0),
        lambda P, ox, oy, cw, ch, r: cell_reeds(P, ox, oy, cw, ch, r),
        lambda P, ox, oy, cw, ch, r: cell_reeds(P, ox, oy, cw, ch, r, cattails=True),
    ], seed=5)


# ============================================================ giant flora (Khổng Lồ)
def giant_mushroom(P, ox, oy, cw, ch, rng, cap="#E2463A", cap_hi="#FF8A6E", spots=True, scale=1.0, dx=0.0):
    bx, by = ox + cw / 2 + dx, oy + ch - 4
    sh = 150 * scale
    stem_m = P.poly([(bx - 22 * scale, by), (bx + 22 * scale, by), (bx + 15 * scale, by - sh), (bx - 15 * scale, by - sh)])
    cap_y = by - sh
    dome = P.ellipse(bx, cap_y, 92 * scale, 64 * scale)
    cut = P.rect(ox, cap_y + 12 * scale, ox + cw, oy + ch)
    dome = dome * (1 - cut)
    under = P.ellipse(bx, cap_y + 10 * scale, 84 * scale, 14 * scale)
    skirt = P.ellipse(bx, by - sh * 0.55, 24 * scale, 8 * scale)
    sil = P.union(stem_m, dome, under)
    P.fill(P.grow(sil, 1.8), INK)
    P.lit(stem_m, "#F3E6CC", "#FFF8EA", "#C9B592", d=3 * scale)
    P.lit(skirt, "#EADBC0", d=1.5)
    P.lit(under, "#C9A98A", "#E2C8AA", "#9C7B5C", d=2)
    for k in range(-6, 7):
        P.fill(P.line([(bx + k * 12 * scale, cap_y + 8 * scale), (bx + k * 9 * scale, cap_y + 14 * scale)], 1.2) * under, hexc("#8E6C4E"), 0.6)
    P.lit(dome, cap, cap_hi, None, d=5 * scale)
    P.fill(P.ellipse(bx - 38 * scale, cap_y - 36 * scale, 26 * scale, 12 * scale, rot=-0.5) * dome, hexc("#FFFFFF"), 0.28)
    if spots:
        for (sx, sy, r) in ((-50, -22, 11), (-8, -46, 13), (36, -30, 10), (62, -6, 8), (-70, 2, 7), (14, -10, 8)):
            P.lit(P.ellipse(bx + sx * scale, cap_y + sy * scale, r * scale, r * 0.8 * scale) * dome, "#FFF7EC", d=1.5)


def giant_pair(P, ox, oy, cw, ch, rng):
    giant_mushroom(P, ox, oy, cw, ch, rng, cap="#C98A4E", cap_hi="#EDB47A", spots=False, scale=0.62, dx=34)
    giant_mushroom(P, ox, oy, cw, ch, rng, cap="#B9733E", cap_hi="#E0A064", spots=False, scale=0.46, dx=-52)


def giant_flower(P, ox, oy, cw, ch, rng):
    bx, by = ox + cw / 2, oy + ch - 4
    sm, tip = stem(P, bx, by, 220, 0.06, -0.15, 9)
    l1, _ = blade(P, bx, by - 40, 90, -1.0, 0.5, 34, taper=0.7)
    l2, _ = blade(P, bx + 2, by - 80, 80, 1.05, -0.5, 30, taper=0.7)
    hx, hy = tip
    pet = petals(P, hx, hy, 11, 38, 30, squash=0.82, rot=0.2)
    centre = P.ellipse(hx, hy, 22, 18)
    P.fill(P.grow(P.union(sm, l1, l2, pet, centre), 1.8), LEAF_INK)
    P.lit(sm, "#4E9A3C", d=2)
    P.lit(l1, "#5DAE48", d=3)
    P.lit(l2, "#58A644", d=3)
    P.lit(pet, "#E65FA8", "#FF9ACD", "#B63C80", d=3.5)
    P.lit(centre, "#FFC93C", "#FFE68A", "#D8901E", d=2.5)
    for k in range(9):
        a = k / 9 * math.tau
        P.fill(P.ellipse(hx + math.cos(a) * 10, hy + math.sin(a) * 8, 2.2), hexc("#B8741A"), 0.8)


def giant_bells(P, ox, oy, cw, ch, rng):
    bx, by = ox + cw / 2 - 40, oy + ch - 4
    pts = [bez((bx, by), (bx - 10, by - 260), (bx + 130, by - 230), i / 16) for i in range(17)]
    sm = P.line(pts, 7)
    leaves = [blade(P, bx, by, 110, -0.35, 0.3, 28, taper=0.7)[0], blade(P, bx + 4, by, 100, 0.25, -0.2, 26, taper=0.7)[0]]
    bells = []
    for i, t in enumerate((0.45, 0.62, 0.78, 0.93)):
        px, py = pts[int(t * 16)]
        r = 20 - i * 1.5
        body = P.union(P.ellipse(px, py + r * 1.2, r * 0.8, r * 1.0), P.poly([(px - r * 1.05, py + r * 2.1), (px + r * 1.05, py + r * 2.1), (px + r * 0.7, py + r * 0.8), (px - r * 0.7, py + r * 0.8)]))
        lip = P.union(*[P.ellipse(px + k * r * 0.52, py + r * 2.1, r * 0.34, r * 0.28) for k in (-1.5, -0.5, 0.5, 1.5)])
        hang = P.line([(px, py), (px, py + r * 0.4)], 2.5)
        bells.append((P.union(body, lip), hang))
    sil = P.union(sm, *leaves, *[b for b, _ in bells], *[h for _, h in bells])
    P.fill(P.grow(sil, 1.6), LEAF_INK)
    P.lit(sm, "#4E9A3C", d=1.6)
    for l in leaves:
        P.lit(l, "#5AAA46", d=2.5)
    for b, h in bells:
        P.fill(h, hexc("#4E9A3C"))
        P.lit(b, "#6F86E8", "#AFC0FF", "#4A58B8", d=3)


def giant_leaf(P, ox, oy, cw, ch, rng):
    """Two elephant-ear leaves on long stalks, tipped toward the viewer."""
    bx, by = ox + cw / 2, oy + ch - 4
    parts = []
    for (ang, L, sc, col) in ((-0.32, 150, 0.85, "#3F9A4A"), (0.22, 190, 1.0, "#48A650")):
        sm, tip = stem(P, bx, by, L, ang, -ang * 0.6, 7)
        lx, ly = tip
        tilt = ang * 1.4
        # a pointed heart: two lobes at the stalk end, narrowing to a tip that droops away
        pts = []
        for k in range(41):
            t = k / 40 * math.tau
            r = (1 - math.sin(t)) * 0.5
            x = math.sin(t) * math.cos(t) * math.log(abs(t) + 1e-3 + 1) * 0 + 16 * math.sin(t) ** 3
            y = -(13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t))
            pts.append((x, y))
        # heart curve points up; flip so the point hangs down-outward and scale to size
        leaf_pts = []
        for x, y in pts:
            X, Y = x * 4.2 * sc, -y * 5.2 * sc
            leaf_pts.append((lx + X * math.cos(tilt) - Y * math.sin(tilt), ly + 58 * sc + X * math.sin(tilt) + Y * math.cos(tilt)))
        leaf = P.poly(leaf_pts)
        parts.append((sm, leaf, lx, ly, sc, tilt, col))
    P.fill(P.grow(P.union(*[m for pr in parts for m in pr[:2]]), 1.8), LEAF_INK)
    for sm, leaf, lx, ly, sc, tilt, col in parts:
        P.lit(sm, "#4E9A3C", d=2)
        P.lit(leaf, col, "#86D680", "#2A6E34", d=5)
        cx, cy = lx, ly + 58 * sc
        tipx, tipy = cx - math.sin(tilt) * 70 * sc, cy + math.cos(tilt) * 70 * sc
        P.fill(P.line([(lx, ly + 8), (tipx, tipy)], 2.4) * leaf, hexc("#9ADE8E"), 0.85)
        for j in range(1, 4):
            t = j / 4
            px, py = lx + (tipx - lx) * t, ly + 8 + (tipy - ly - 8) * t
            for sgn in (-1, 1):
                ex = px + sgn * math.cos(tilt) * 44 * sc * (1.1 - t)
                ey = py + sgn * math.sin(tilt) * 44 * sc * (1.1 - t) - 18 * sc
                P.fill(P.line([(px, py), (ex, ey)], 1.5) * leaf, hexc("#7FCB78"), 0.6)


def giant_clover(P, ox, oy, cw, ch, rng):
    bx, by = ox + cw / 2, oy + ch - 4
    sil = []
    parts = []
    for k, (ang, L) in enumerate(((-0.45, 120), (0.1, 150), (0.55, 110))):
        sm, tip = stem(P, bx + k * 4 - 4, by, L, ang, -ang * 0.5, 6)
        hx, hy = tip
        lobes = []
        for j in range(3):
            a = -math.pi / 2 + (j - 1) * 2.1 + ang
            cx, cy = hx + math.cos(a) * 24, hy + math.sin(a) * 18
            heart = P.union(P.ellipse(cx + math.cos(a + 1.57) * 10, cy + math.sin(a + 1.57) * 8, 20, 16, rot=a),
                            P.ellipse(cx - math.cos(a + 1.57) * 10, cy - math.sin(a + 1.57) * 8, 20, 16, rot=a))
            lobes.append(heart)
        parts.append((sm, lobes))
        sil += [sm] + lobes
    P.fill(P.grow(P.union(*sil), 1.6), LEAF_INK)
    for sm, lobes in parts:
        P.lit(sm, "#4E9A3C", d=1.5)
        for l in lobes:
            P.lit(l, "#58B04C", "#9ADE82", "#3A8038", d=3)
            P.fill(P.soft(l, 3) * l * 0 + P.ellipse(0, 0, 0.1) * 0, hexc("#FFFFFF"), 0)


def giant_fern(P, ox, oy, cw, ch, rng):
    cell_fern(P, ox, oy, cw, ch, rng, green="#3F9046", scale=2.6)


def hanging_vine(P, ox, oy, cw, ch, rng):
    """Anchored at the TOP centre of its cell, hanging down."""
    bx, by = ox + cw / 2, oy + 4
    pts = [(bx + math.sin(i * 0.55) * 10 * (i / 20), by + i * (ch - 20) / 20) for i in range(21)]
    vine = P.line(pts, 4)
    leaves = []
    for i in range(2, 20, 2):
        px, py = pts[i]
        sgn = 1 if i % 4 == 0 else -1
        leaves.append(P.ellipse(px + sgn * 11, py + 4, 11, 6.5, rot=sgn * 0.6))
    P.fill(P.grow(P.union(vine, *leaves), 1.4), LEAF_INK)
    P.lit(vine, "#5E7A3A", d=1.2)
    for l in leaves:
        P.lit(l, rng.choice(("#56A844", "#62B24C", "#4E9A40")), d=1.8)


def gen_giant():
    atlas("life_giant", 4, 2, 256, 320, [
        giant_mushroom, giant_pair, giant_flower, giant_bells,
        giant_leaf, giant_clover, giant_fern, hanging_vine,
    ], seed=9)


# ============================================================ wind (Đảo Gió)
def windmill_blades(P, ox, oy, cw, ch, rng):
    cx, cy = ox + cw / 2, oy + ch / 2
    arms, sails, lattice = [], [], []
    for k in range(4):
        a = k * math.pi / 2 + 0.35
        ux, uy = math.cos(a), math.sin(a)
        nx, ny = -uy, ux
        tip = (cx + ux * 118, cy + uy * 118)
        arms.append(P.line([(cx, cy), tip], 6))
        r0, r1, w = 26, 116, 34
        q = [(cx + ux * r0, cy + uy * r0), (cx + ux * r1, cy + uy * r1),
             (cx + ux * r1 + nx * w, cy + uy * r1 + ny * w), (cx + ux * r0 + nx * w * 0.8, cy + uy * r0 + ny * w * 0.8)]
        sails.append(P.poly(q))
        for j in range(1, 5):
            t = r0 + (r1 - r0) * j / 5
            lattice.append(P.line([(cx + ux * t, cy + uy * t), (cx + ux * t + nx * w * (0.8 + 0.2 * j / 5), cy + uy * t + ny * w * (0.8 + 0.2 * j / 5))], 1.6))
        lattice.append(P.line([(cx + ux * r0 + nx * w * 0.45, cy + uy * r0 + ny * w * 0.45), (cx + ux * r1 + nx * w * 0.5, cy + uy * r1 + ny * w * 0.5)], 1.4))
    hub = P.ellipse(cx, cy, 13)
    P.fill(P.grow(P.union(*arms, *sails, hub), 2.0), INK)
    for sm in sails:
        P.lit(sm, "#F3E4C4", "#FFF8E8", "#CDB58C", d=2.5)
    for l in lattice:
        P.fill(l * P.union(*sails), hexc("#9A6A40"), 0.9)
    for a in arms:
        P.lit(a, "#8A5A34", "#B07A4C", "#5E3A20", d=1.2)
    P.lit(hub, "#6E4A2C", "#9A6C44", "#4A2E1A", d=2)
    P.fill(P.ellipse(cx - 3, cy - 3, 4), hexc("#C8A070"))


def pinwheel(P, ox, oy, cw, ch, rng, cols=("#E8534A", "#F6C23E", "#4DA6E0", "#5CC46A")):
    cx, cy = ox + cw / 2, oy + ch / 2
    parts = []
    for k in range(4):
        a = k * math.pi / 2
        ux, uy = math.cos(a), math.sin(a)
        nx, ny = -uy, ux
        tri = P.poly([(cx, cy), (cx + ux * 100, cy + uy * 100), (cx + ux * 42 + nx * 62, cy + uy * 42 + ny * 62)])
        fold = P.poly([(cx, cy), (cx + ux * 100, cy + uy * 100), (cx + ux * 58 + nx * 26, cy + uy * 58 + ny * 26)])
        parts.append((tri, fold, cols[k]))
    pin = P.ellipse(cx, cy, 10)
    P.fill(P.grow(P.union(*[t for t, _, _ in parts], pin), 2.0), INK)
    for tri, fold, c in parts:
        P.lit(tri, c, d=3)
        P.fill(fold, np.clip(hexc(c) * 0.78, 0, 255))
        P.fill(fold * (1 - P.shift(fold, 2, 2)), hexc("#FFFFFF"), 0.35)
    P.lit(pin, "#F4F0E6", "#FFFFFF", "#BDB6A8", d=1.5)


def flag_cloth(P, ox, oy, cw, ch, rng, kind=0):
    """Anchored on the LEFT edge of the cell (the pole side), vertically centred."""
    x0, cy = ox + 6, oy + ch / 2
    if kind == 0:
        cloth = P.poly([(x0, cy - 48), (x0 + 236, cy - 30), (x0 + 196, cy), (x0 + 236, cy + 30), (x0, cy + 48)])
        P.fill(P.grow(cloth, 2.0), INK)
        P.lit(cloth, "#E4574A", "#FF8A76", "#B03A30", d=3)
        stripe = P.poly([(x0, cy - 14), (x0 + 220, cy - 8), (x0 + 220, cy + 8), (x0, cy + 14)]) * cloth
        P.fill(stripe, hexc("#FFF1D6"))
    else:
        cloth = P.rect(x0, cy - 58, x0 + 214, cy + 58, 8)
        P.fill(P.grow(cloth, 2.0), INK)
        P.lit(cloth, "#2FA4A0", "#6ED8CE", "#1D7570", d=3)
        sun = P.ellipse(x0 + 108, cy, 26)
        rays = P.union(*[P.line([(x0 + 108 + math.cos(k * 0.785) * 32, cy + math.sin(k * 0.785) * 32),
                                 (x0 + 108 + math.cos(k * 0.785) * 44, cy + math.sin(k * 0.785) * 44)], 5) for k in range(8)])
        P.fill(P.union(sun, rays) * cloth, hexc("#FFE07A"))
        P.fill(P.rect(x0, cy + 40, x0 + 214, cy + 50) * cloth, hexc("#F6F0DC"), 0.9)


def dandelions(P, ox, oy, cw, ch, rng):
    base_x, base_y = ox + cw / 2, oy + ch - 4
    leaves = [blade(P, base_x + (k - 2) * 6, base_y, rng.uniform(40, 60), (k - 2) * 0.55, (k - 2) * 0.3, 16, taper=0.8)[0] for k in range(5)]
    heads, stems = [], []
    for k in range(3):
        ang = (k - 1) * 0.35
        sm, tip = stem(P, base_x + (k - 1) * 8, base_y, rng.uniform(120, 170), ang, -ang * 0.4, 3)
        stems.append(sm)
        heads.append(tip)
    puffs = [P.ellipse(hx, hy, 26) for hx, hy in heads]
    P.fill(P.grow(P.union(*leaves, *stems), 1.4), LEAF_INK)
    for l in leaves:
        P.lit(l, "#5AA844", d=1.5)
    for sm in stems:
        P.lit(sm, "#7AB654", d=1)
    for (hx, hy), pm in zip(heads, puffs):
        P.fill(pm, hexc("#FFFFFF"), 0.45)
        for j in range(26):
            a = j / 26 * math.tau
            P.fill(P.line([(hx, hy), (hx + math.cos(a) * 24, hy + math.sin(a) * 24)], 1.0), hexc("#FFFFFF"), 0.8)
        P.fill(P.ellipse(hx, hy, 5), hexc("#C9B98A"))


def gen_wind():
    atlas("life_wind", 4, 2, 256, 256, [
        windmill_blades,
        lambda P, ox, oy, cw, ch, r: pinwheel(P, ox, oy, cw, ch, r),
        lambda P, ox, oy, cw, ch, r: pinwheel(P, ox, oy, cw, ch, r, cols=("#F29AC0", "#8FD3F4", "#FFE27A", "#B8E48C")),
        lambda P, ox, oy, cw, ch, r: flag_cloth(P, ox, oy, cw, ch, r, 0),
        lambda P, ox, oy, cw, ch, r: flag_cloth(P, ox, oy, cw, ch, r, 1),
        lambda P, ox, oy, cw, ch, r: cell_grass(P, ox, oy, cw, ch, r, greens=("#C8B25A", "#D9C674", "#A99A48"), tall=1.0, n=9),
        dandelions,
        None,
    ], seed=13)


# ============================================================ props
# Anchor points (in sprite pixels, from the top-left) that IslandLife hangs a moving part on.
ANCHORS = {
    "prop_flagpole": (32, 44),     # top of the cloth's pole edge
    "prop_rod": (55, 34),          # centre of the glass orb
    "prop_vent": (100, 58),        # mouth
}


def glow_mask(name, w, h, painter):
    """A light mask for MiT/UI Glow (see UIGlow.shader): rgb = light, a = where it may sparkle."""
    P = Pic(w, h)
    painter(P)
    save(P.light_mask(), name)


def stone_blocks(P, m, base, seed, rows=5):
    rng = random.Random(seed)
    P.lit(m, base, d=4)
    ys = np.nonzero(m.max(1) > 0.5)[0]
    if len(ys) == 0:
        return
    y0, y1 = ys[0] / SS, ys[-1] / SS
    for r in range(1, rows):
        y = y0 + (y1 - y0) * r / rows
        P.fill(P.rect(0, y - 0.8, P.w, y + 0.8) * m, hexc("#6E6458"), 0.6)
        off = rng.uniform(0, 30)
        for x in np.arange(off, P.w, 34):
            P.fill(P.rect(x - 0.8, y, x + 0.8, y + (y1 - y0) / rows) * m, hexc("#6E6458"), 0.5)


def prop_flagpole():
    P = Pic(64, 320)
    pole = P.rect(26, 22, 36, 318, 4)
    ball = P.ellipse(31, 16, 10)
    P.ink(P.union(pole, ball), 1.6)
    P.lit(pole, "#9A7A58", "#C8A67E", "#6A5038", d=2)
    P.lit(ball, "#F2C14E", "#FFE9A0", "#B8862A", d=2)
    for y in (58, 150):
        band = P.rect(24, y, 38, y + 6, 2)
        P.lit(band, "#6A6E78", d=1)
    save(P.image(), "prop_flagpole")


def prop_rod():
    P = Pic(110, 300)
    cx = 55
    plinth = P.poly([(cx - 40, 298), (cx + 40, 298), (cx + 32, 250), (cx - 32, 250)])
    cap = P.rect(cx - 36, 244, cx + 36, 256, 3)
    pole = P.rect(cx - 5, 56, cx + 5, 250, 3)
    orb = P.ellipse(cx, 34, 26)
    collar = P.rect(cx - 12, 54, cx + 12, 64, 3)
    sil = P.union(plinth, cap, pole, orb, collar)
    P.ink(sil, 2.0)
    stone_blocks(P, plinth, "#8A86A0", 5, rows=2)
    P.lit(cap, "#6E6A84", d=2)
    P.lit(pole, "#5C6270", "#9AA2B2", "#3A3E48", d=2)
    for y in range(80, 240, 16):
        coil = P.ellipse(cx, y, 10, 3.2)
        P.fill(P.grow(coil, 0.8), INK, 0.8)
        P.lit(coil, "#D8844A", "#FFB47A", "#9A5028", d=1)
    P.lit(collar, "#D8844A", d=1.5)
    P.fill(orb, hexc("#2E3F6E"), 0.9)
    P.fill(P.ellipse(cx, 36, 18) * orb, hexc("#56B8E8"), 0.8)
    P.fill(P.ellipse(cx, 38, 9), hexc("#CFF6FF"), 0.95)
    P.fill(P.ellipse(cx - 10, 24, 7, 5, rot=-0.6), hexc("#FFFFFF"), 0.8)
    save(P.image(), "prop_rod")


def prop_pine():
    P = Pic(180, 320)
    cx = 90
    trunk = P.rect(cx - 10, 270, cx + 10, 316, 3)
    tiers = []
    for k, (y, hw, h) in enumerate(((250, 78, 90), (196, 64, 84), (146, 50, 78), (100, 36, 72))):
        tiers.append((P.poly([(cx - hw, y + 16), (cx + hw, y + 16), (cx, y - h)]), y, hw, h))
    P.ink(P.union(trunk, *[t for t, *_ in tiers]), 2.0)
    P.lit(trunk, "#7A5234", d=2)
    for t, y, hw, h in tiers:
        P.lit(t, "#2F7A5A", "#4FA07A", "#1E5440", d=4)
        snow = P.union(P.poly([(cx - hw, y + 16), (cx + hw, y + 16), (cx + hw * 0.7, y + 4), (cx - hw * 0.7, y + 4)]),
                       *[P.ellipse(cx + (j - 2) * hw * 0.42, y + 12, hw * 0.2, 7) for j in range(5)])
        cap = P.poly([(cx - hw * 0.28, y - h * 0.62), (cx + hw * 0.28, y - h * 0.62), (cx, y - h)])
        P.fill(P.union(snow, cap) * P.grow(t, 1.5), hexc("#F6FAFF"))
        P.fill((P.union(snow, cap) * P.grow(t, 1.5)) * (1 - P.shift(P.union(snow, cap), 0, -2.5)), hexc("#B8C2E6"), 0.8)
    save(P.image(), "prop_pine")


def prop_vent():
    P = Pic(200, 110)
    cx, cy = 100, 62
    rocks = []
    rng = random.Random(4)
    for k in range(11):
        a = k / 11 * math.tau
        rocks.append(P.ellipse(cx + math.cos(a) * 62, cy + math.sin(a) * 24, rng.uniform(16, 24), rng.uniform(11, 16), rot=a))
    crater = P.ellipse(cx, cy, 52, 18)
    P.ink(P.union(*rocks, crater), 2.0)
    P.fill(crater, hexc("#2A1C18"))
    lava = P.ellipse(cx, cy + 2, 36, 11)
    P.fill(lava, hexc("#E8551E"))
    P.fill(P.ellipse(cx - 4, cy + 1, 22, 6), hexc("#FFB03A"))
    P.fill(P.ellipse(cx - 6, cy, 10, 3), hexc("#FFE9A0"))
    back = [r for i, r in enumerate(rocks) if math.sin(i / 11 * math.tau) < 0.1]
    front = [r for i, r in enumerate(rocks) if math.sin(i / 11 * math.tau) >= 0.1]
    for r in back + front:
        P.lit(r, "#3E3438", "#6A5A5E", "#221A1E", d=3)
    for r in front:
        P.fill(r * (1 - P.shift(r, 0, 3)), hexc("#FF8A3A"), 0.35)
    save(P.image(), "prop_vent")

    def glow(G):
        G.fill(G.soft(G.ellipse(cx, cy, 48, 17), 7), hexc("#FF7A2A"), 1.0)
        G.fill(G.ellipse(cx, cy + 2, 34, 10), hexc("#FFC060"), 1.0)
    glow_mask("prop_vent_glow", 200, 110, glow)


def coin_pile(P, cx, by, rx, ry, rng, n=46):
    heap = P.ellipse(cx, by - ry, rx, ry)
    P.ink(heap, 1.8)
    P.lit(heap, "#E8B23A", "#FFE08A", "#B07A1E", d=4)
    coins = []
    for _ in range(n):
        a, r = rng.uniform(0, math.tau), math.sqrt(rng.random())
        x, y = cx + math.cos(a) * rx * 0.85 * r, by - ry + math.sin(a) * ry * 0.8 * r
        coins.append((x, y))
    coins.sort(key=lambda c: c[1])
    for x, y in coins:
        c = P.ellipse(x, y, 10, 6)
        P.fill(P.grow(c, 1.0), hexc("#8A5A14"), 0.9)
        P.lit(c, "#F4C24A", "#FFEFA8", "#C08A26", d=1.4)
    return heap


def prop_chest():
    P = Pic(220, 180)
    rng = random.Random(21)
    cx, by = 110, 176
    lid = P.poly([(cx - 78, 70), (cx + 78, 70), (cx + 70, 18), (cx - 70, 18)])
    box = P.rect(cx - 84, 84, cx + 84, 170, 8)
    P.ink(P.union(lid, box), 2.4)
    P.lit(lid, "#8A5630", "#B87A4C", "#5C361C", d=4)
    P.fill(P.rect(cx - 70, 26, cx + 70, 62, 6) * lid, hexc("#4A2A18"), 0.6)
    heap = coin_pile(P, cx, 100, 76, 26, rng, n=38)
    P.lit(box, "#9A6034", "#C4885A", "#64381C", d=4)
    for x in (cx - 60, cx + 52):
        band = P.rect(x, 84, x + 10, 170, 2)
        P.lit(band, "#D8A640", "#FFE08A", "#9A6A1E", d=1.5)
    lock = P.rect(cx - 12, 110, cx + 12, 136, 4)
    P.ink(lock, 1.4)
    P.lit(lock, "#E8B23A", d=1.5)
    for (gx, gy, col) in ((cx - 30, 70, "#E84860"), (cx + 26, 66, "#46C8E8"), (cx + 2, 60, "#62D86A")):
        gem = P.poly([(gx, gy - 11), (gx + 9, gy - 2), (gx, gy + 9), (gx - 9, gy - 2)])
        P.ink(gem, 1.2)
        P.lit(gem, col, d=1.5)
        P.fill(P.ellipse(gx - 3, gy - 4, 2.4), hexc("#FFFFFF"), 0.9)
    save(P.image(), "prop_chest")

    def glow(G):
        G.fill(G.ellipse(cx, 100 - 26, 74, 24), hexc("#FFE28A"), 0.9)
        for (gx, gy) in ((cx - 30, 70), (cx + 26, 66), (cx + 2, 60)):
            G.fill(G.ellipse(gx, gy, 8), hexc("#FFFFFF"), 1.0)
        for x in (cx - 55, cx + 57):
            G.fill(G.rect(x - 5, 84, x + 5, 170), hexc("#FFD870"), 0.7)
    glow_mask("prop_chest_glow", 220, 180, glow)


def prop_coins():
    P = Pic(150, 90)
    rng = random.Random(8)
    coin_pile(P, 75, 86, 62, 26, rng, n=34)
    gem = P.poly([(88, 34), (98, 44), (88, 56), (78, 44)])
    P.ink(gem, 1.2)
    P.lit(gem, "#E84860", d=1.5)
    save(P.image(), "prop_coins")

    def glow(G):
        G.fill(G.ellipse(75, 60, 58, 22), hexc("#FFE28A"), 0.9)
        G.fill(G.ellipse(88, 44, 8), hexc("#FFFFFF"), 1.0)
    glow_mask("prop_coins_glow", 150, 90, glow)


def gen_props():
    prop_flagpole()
    prop_rod()
    prop_pine()
    prop_vent()
    prop_chest()
    prop_coins()


# ============================================================ particles (white; tinted in the game)
def gen_particles():
    P = Pic(64, 64)
    star = P.union(P.poly([(32, 2), (36, 28), (62, 32), (36, 36), (32, 62), (28, 36), (2, 32), (28, 28)]))
    P.fill(P.soft(P.ellipse(32, 32, 12), 4), hexc("#FFFFFF"), 0.9)
    P.fill(star, hexc("#FFFFFF"))
    save(P.image(), "p_star")

    P = Pic(64, 64)
    stalk = P.line([(32, 60), (32, 30)], 1.6)
    P.fill(stalk, hexc("#E8E2D0"))
    for j in range(13):
        a = -math.pi / 2 + (j - 6) * 0.2
        P.fill(P.line([(32, 30), (32 + math.cos(a) * 24, 30 + math.sin(a) * 24)], 1.1), hexc("#FFFFFF"), 0.9)
    P.fill(P.ellipse(32, 58, 3, 5), hexc("#B8A078"))
    save(P.image(), "p_seed")


GROUPS = {
    "noise": gen_noise,
    "bugs": gen_bugs,
    "meadow": gen_meadow,
    "giant": gen_giant,
    "wind": gen_wind,
    "props": gen_props,
    "particles": gen_particles,
}

if __name__ == "__main__":
    want = sys.argv[1:] or list(GROUPS)
    for k in want:
        print(k)
        GROUPS[k]()
