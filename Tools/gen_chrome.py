"""Bake every piece of UI chrome to a real PNG.

The runtime used to synthesise these into a texture atlas on startup. Baking them
gives real, inspectable, replaceable asset files — and lets the shapes carry a soft
bevel that would be wasteful to compute every launch. Shading is baked as white
luminance, so Unity's colour tint (a multiply) keeps it when the plate is recoloured.

    uv run --with pillow python Tools/gen_chrome.py              # everything
    uv run --with pillow python Tools/gen_chrome.py materials    # only the material shapes
"""
from PIL import Image
import math, os, random

OUT = "Assets/Resources/Art/chrome"
os.makedirs(OUT, exist_ok=True)


def save(img, name):
    img.save(os.path.join(OUT, name + ".png"))
    print("  ", name, img.size)


def round_dist(x, y, w, h, r):
    dx = max(r - x, x - (w - r))
    dy = max(r - y, y - (h - r))
    if dx > 0 and dy > 0:
        return math.hypot(dx, dy) - r
    return max(dx, dy) - r


def rounded(radius):
    """White rounded rect with a soft top-lit bevel."""
    size = radius * 2 + 4
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    for y in range(size):
        for x in range(size):
            d = round_dist(x + 0.5, y + 0.5, size, size, radius)
            a = max(0.0, min(1.0, 0.5 - d))
            if a <= 0:
                continue
            # top edge lifts, bottom edge sinks — a bevel that survives tinting
            t = y / (size - 1.0)
            lum = 1.0
            edge = min(1.0, -d / 3.0) if d < 0 else 0.0
            lum -= (1.0 - edge) * 0.10          # darken right at the rim
            lum += (1.0 - t) * 0.10 - t * 0.08
            lum = max(0.0, min(1.0, lum))
            v = int(255 * lum)
            px[x, y] = (v, v, v, int(a * 255))
    return img


def shadow(radius, blur=20):
    rr = radius + blur
    size = int(rr) * 2 + 4
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    for y in range(size):
        for x in range(size):
            d = round_dist(x + 0.5, y + 0.5, size, size, rr)
            a = max(0.0, min(1.0, 1.0 - (d + blur) / blur))
            a *= a
            if a > 0:
                px[x, y] = (255, 255, 255, int(a * 255))
    return img


def circle(size=192):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    c, r = size / 2.0, size / 2.0 - 1.0
    for y in range(size):
        for x in range(size):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c)
            a = max(0.0, min(1.0, r - d))
            if a <= 0:
                continue
            t = y / (size - 1.0)
            lum = max(0.0, min(1.0, 1.0 + (1.0 - t) * 0.12 - t * 0.10))
            v = int(255 * lum)
            px[x, y] = (v, v, v, int(a * 255))
    return img


def ring(thickness, size=192):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    c = size / 2.0
    ro = c - 1.0
    ri = ro * (1.0 - thickness)
    for y in range(size):
        for x in range(size):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c)
            a = max(0.0, min(1.0, ro - d)) * max(0.0, min(1.0, d - ri))
            if a > 0:
                px[x, y] = (255, 255, 255, int(min(1.0, a) * 255))
    return img


def glow(size=192):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    c = size / 2.0
    for y in range(size):
        for x in range(size):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c) / c
            a = max(0.0, min(1.0, 1.0 - d)) ** 3
            if a > 0:
                px[x, y] = (255, 255, 255, int(a * 255))
    return img


def vignette(size=384):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    c = size / 2.0
    for y in range(size):
        for x in range(size):
            d = math.hypot(x + 0.5 - c, y + 0.5 - c) / c
            a = max(0.0, min(1.0, (d - 0.55) / 0.75)) ** 2
            px[x, y] = (0, 0, 0, int(a * 130))
    return img


def ramp(stops, w=16, h=512):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    for y in range(h):
        # PNG row 0 is the top; stops are given bottom-first
        t = (1.0 - y / (h - 1.0)) * (len(stops) - 1)
        i = min(len(stops) - 2, int(t))
        f = t - i
        c = tuple(int(stops[i][k] + (stops[i + 1][k] - stops[i][k]) * f) for k in range(3))
        for x in range(w):
            px[x, y] = (c[0], c[1], c[2], 255)
    return img


def hexc(s):
    s = s.lstrip("#")
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


def island(W=660, H=480):
    """Grass cap over a soil body: irregular edge, rim light, striations."""
    a, b, cyTop, depth = 300.0, 175.0, 290.0, 96.0
    cx, cyBot = W / 2.0, cyTop - depth
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = img.load()
    rng = random.Random(7)

    def lerp(c1, c2, t):
        return tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(3))

    G_HI, G_MD, G_LO = hexc("#9EDC76"), hexc("#86CB5E"), hexc("#63A849")
    S_HI, S_MD, S_LO = hexc("#9A663A"), hexc("#7E512D"), hexc("#5A381E")

    for yi in range(H):
        y = yi + 0.5
        for xi in range(W):
            x = xi + 0.5
            dxr = x - cx
            th = math.atan2(y - cyTop, dxr if dxr else 1e-6)
            k = 1 + 0.028 * math.sin(th * 5 + 0.7) + 0.018 * math.sin(th * 9 + 2.1)
            aa, bb = a * k, b * k
            dx = dxr / aa
            dyT, dyB = (y - cyTop) / bb, (y - cyBot) / bb
            rT, rB = dx * dx + dyT * dyT, dx * dx + dyB * dyB
            sM = (1 - abs(dx)) if cyBot <= y <= cyTop else -1
            inside = max(1 - rT, 1 - rB, sM)
            if inside <= 0:
                continue
            edge = max(0.0, min(1.0, inside * 26))

            if rT <= 1:
                t = max(0.0, min(1.0, (dyT + 1) * 0.5))
                col = lerp(G_LO, G_HI, min(1.0, t * 1.15))
                if t > 0.86:
                    col = lerp(col, hexc("#C4EEA0"), (t - 0.86) / 0.14 * 0.7)
                if rng.random() < 0.05:
                    col = lerp(col, G_HI, 0.35)
            else:
                dpt = max(0.0, min(1.0, (cyTop - y) / (depth + bb)))
                col = lerp(S_HI, S_LO, dpt)
                sv = math.sin(x * 0.11 + math.sin(x * 0.037) * 2)
                col = lerp(col, S_MD if sv > 0 else S_LO, abs(sv) * 0.55)
                if dpt < 0.10:
                    col = lerp(col, S_LO, (0.10 - dpt) / 0.10 * 0.45)

            px[xi, H - 1 - yi] = (col[0], col[1], col[2], int(255 * edge))
    return img


def shape(r, top_only=False):
    """Pure white rounded square, radius r px, 1 px anti-aliased edge and NO baked shading.

    These are the material system's building blocks (see Assets/Scripts/UI/Surface.cs): a
    surface is several of them stacked — edge, rim, fill — each tinted and given a vertex
    gradient in code. Baked shading would fight the gradient, so there is none.

    They are drawn large and shown with Image.pixelsPerUnitMultiplier > 1, so a corner is
    always minified (sharp) rather than magnified (soft) on a 1.5x-3x phone screen. The old
    round_N plates were 1x and every corner on a 1080p screen was a blurred 1.5x upscale."""
    w = 2 * r + 2
    h = (r + 2) if top_only else w
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    for y in range(h):
        for x in range(w):
            # a top-only shape is the upper half of a full one, so bottom corners stay square
            d = round_dist(x + 0.5, y + 0.5, w, w if not top_only else 4 * r + 4, r)
            a = max(0.0, min(1.0, 0.5 - d))
            if a > 0:
                px[x, y] = (255, 255, 255, int(round(a * 255)))
    return img


def soft(r, blur):
    """Gaussian-falloff rounded rect: a drop shadow whose inner shape has radius r px and
    whose penumbra is blur px wide, both scaled together by the multiplier."""
    rr = r + blur
    w = 2 * rr + 2
    img = Image.new("RGBA", (w, w), (0, 0, 0, 0))
    px = img.load()
    sigma = blur / 2.6
    for y in range(w):
        for x in range(w):
            # distance to the INNER shape, which sits blur px inside the canvas
            d = round_dist(x + 0.5 - blur, y + 0.5 - blur, w - 2 * blur, w - 2 * blur, r)
            a = 0.5 * math.erfc(d / (sigma * math.sqrt(2.0)))
            if a > 0.002:
                px[x, y] = (255, 255, 255, int(round(min(1.0, a) * 255)))
    return img


META_TEMPLATE = "Assets/Resources/Art/chrome/round_26.png.meta"


def save_sliced(img, name, border):
    """Save a PNG plus a meta with its 9-slice border and mipmaps on. Mipmaps matter here:
    these sprites are always shown minified, and without them a 4x minified corner aliases."""
    save(img, name)
    meta = os.path.join(OUT, name + ".png.meta")
    import re, uuid
    if os.path.exists(meta):
        guid = re.search(r"guid: (\w+)", open(meta).read()).group(1)
    else:
        guid = uuid.uuid4().hex
    m = open(META_TEMPLATE).read()
    m = re.sub(r"guid: \w+", "guid: " + guid, m, count=1)
    m = m.replace("enableMipMap: 0", "enableMipMap: 1")
    m = re.sub(r"spriteBorder: \{[^}]*\}", "spriteBorder: {x: %d, y: %d, z: %d, w: %d}" % border, m)
    m = re.sub(r"filterMode: \d", "filterMode: 2", m, count=1)   # trilinear
    open(meta, "w").write(m)


def materials():
    for r in (16, 48, 128):
        save_sliced(shape(r), "shape_%d" % r, (r, r, r, r))
    save_sliced(shape(48, top_only=True), "top_48", (48, 0, 48, 48))
    # two penumbra ratios: tight (blur = r/2) for HUD chips and buttons, wide (blur = r) for sheets
    save_sliced(soft(64, 32), "soft_tight", (96, 96, 96, 96))
    save_sliced(soft(64, 64), "soft_wide", (128, 128, 128, 128))


import sys
if len(sys.argv) > 1 and sys.argv[1] == "materials":
    materials()
    sys.exit(0)

print("chrome ->", OUT)
materials()
for r in (6, 12, 18, 26):
    save(rounded(r), "round_%d" % r)
save(circle(), "circle")
save(ring(0.12), "ring_12")
save(ring(0.17), "ring_17")
save(glow(), "glow")
save(vignette(), "vignette")
# sky, sea and island used to be baked here; the sky is Tools/gen_sky.py now and each island has
# its own painting from Tools/gen_islands.py
