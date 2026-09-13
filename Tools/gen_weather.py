"""Six weather glyphs, drawn from scratch.

    /usr/bin/python3 Tools/gen_weather.py

Each has to be distinguishable by SILHOUETTE at 26 px, because that is the size the HUD's
"next weather" pip renders at. So they differ in outline — a disc, a cloud with strokes below,
a cloud with lines beside, a star, a bolt, a cracked sun — rather than only in colour. Anything
that relies on colour to be told apart fails the moment two of them sit side by side in the
season sheet's trước/nay/kế strip.

Drawn white so the game can tint them per weather; tint multiplies, so the art must be bright.
"""
from PIL import Image
import math
import os

OUT = "Assets/Resources/Art/weather"
S = 192


def blank():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def put(px, x, y, a, v=1.0):
    """Accumulate coverage, keeping the brightest value written so far."""
    if a <= 0 or not (0 <= x < S and 0 <= y < S):
        return
    a = max(0.0, min(1.0, a))
    r, g, b, old = px[x, y]
    na = max(old, int(255 * a))
    c = int(255 * v)
    px[x, y] = (c, c, c, na)


def disc(px, cx, cy, r, v=1.0):
    for y in range(max(0, int(cy - r - 2)), min(S, int(cy + r + 3))):
        for x in range(max(0, int(cx - r - 2)), min(S, int(cx + r + 3))):
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy) - r
            put(px, x, y, 0.5 - d, v)


def stroke(px, x0, y0, x1, y1, w, v=1.0):
    """A round-capped line."""
    dx, dy = x1 - x0, y1 - y0
    n = max(1, int(math.hypot(dx, dy)))
    for i in range(n + 1):
        t = i / n
        disc(px, x0 + dx * t, y0 + dy * t, w, v)


def cloud(px, cx, cy, scale=1.0, v=1.0):
    """Three overlapping lobes on a flat base — the shape reads as a cloud at any size."""
    r = 26 * scale
    disc(px, cx - 26 * scale, cy + 4 * scale, r * 0.86, v)
    disc(px, cx + 26 * scale, cy + 4 * scale, r * 0.80, v)
    disc(px, cx - 2 * scale, cy - 14 * scale, r * 1.05, v)
    # base slab so the underside is flat rather than scalloped
    for y in range(int(cy), int(cy + 22 * scale)):
        for x in range(int(cx - 48 * scale), int(cx + 48 * scale)):
            put(px, x, y, 1.0, v)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    img.save(os.path.join(OUT, name + ".png"))
    print("  ", name)


# ------------------------------------------------------------------ sunny
def sunny():
    img = blank(); px = img.load()
    disc(px, S / 2, S / 2, 40)
    for i in range(8):
        a = i * math.pi / 4
        x0, y0 = S / 2 + math.cos(a) * 54, S / 2 + math.sin(a) * 54
        x1, y1 = S / 2 + math.cos(a) * 76, S / 2 + math.sin(a) * 76
        stroke(px, x0, y0, x1, y1, 7)
    return img


# ------------------------------------------------------------------- rain
def rain():
    img = blank(); px = img.load()
    cloud(px, S / 2, S * 0.40, 0.95)
    for i, x in enumerate((-40, -8, 24)):
        top = S * 0.62 + (i % 2) * 8
        stroke(px, S / 2 + x, top, S / 2 + x - 10, top + 34, 6)
    return img


# ------------------------------------------------------------------- wind
def wind():
    img = blank(); px = img.load()
    cloud(px, S * 0.44, S * 0.36, 0.78)
    # three trailing gusts, each ending in a curl so it is not just a bar chart
    for i, (y, ln) in enumerate(((0.60, 86), (0.72, 66), (0.84, 46))):
        y = S * y
        stroke(px, S * 0.18, y, S * 0.18 + ln, y, 7)
        if i < 2:
            for k in range(10):
                t = k / 9.0
                a = -math.pi / 2 * t
                disc(px, S * 0.18 + ln + math.sin(a) * 14, y - 14 + math.cos(a) * 14, 3.5)
    return img


# ------------------------------------------------------------------- snow
def snow():
    img = blank(); px = img.load()
    cx = cy = S / 2
    for i in range(6):
        a = i * math.pi / 3
        ex, ey = cx + math.cos(a) * 66, cy + math.sin(a) * 66
        stroke(px, cx, cy, ex, ey, 7)
        # barbs, which is what makes it a snowflake rather than an asterisk
        for t in (0.52, 0.78):
            bx, by = cx + math.cos(a) * 66 * t, cy + math.sin(a) * 66 * t
            for s in (-1, 1):
                b = a + s * math.pi / 4
                stroke(px, bx, by, bx + math.cos(b) * 18, by + math.sin(b) * 18, 5)
    disc(px, cx, cy, 9)
    return img


# ------------------------------------------------------------------ storm
def storm():
    img = blank(); px = img.load()
    cloud(px, S / 2, S * 0.34, 0.95)
    # a filled bolt: polygon scan rather than strokes, so the zigzag stays solid at 26 px
    pts = [(S * 0.54, S * 0.52), (S * 0.36, S * 0.74), (S * 0.48, S * 0.74),
           (S * 0.40, S * 0.94), (S * 0.66, S * 0.66), (S * 0.52, S * 0.66)]
    ys = [p[1] for p in pts]
    for y in range(int(min(ys)), int(max(ys)) + 1):
        xs = []
        for i in range(len(pts)):
            (x0, y0), (x1, y1) = pts[i], pts[(i + 1) % len(pts)]
            if (y0 <= y < y1) or (y1 <= y < y0):
                xs.append(x0 + (x1 - x0) * (y - y0) / (y1 - y0))
        xs.sort()
        for i in range(0, len(xs) - 1, 2):
            for x in range(int(xs[i]), int(xs[i + 1]) + 1):
                put(px, x, y, 1.0)
    return img


# ---------------------------------------------------------------- drought
def drought():
    img = blank(); px = img.load()
    # a low sun over cracked ground: the horizon line is what separates it from "sunny"
    disc(px, S / 2, S * 0.38, 34)
    for i in range(5):
        a = math.pi + i * math.pi / 4
        x0, y0 = S / 2 + math.cos(a) * 46, S * 0.38 + math.sin(a) * 46
        x1, y1 = S / 2 + math.cos(a) * 66, S * 0.38 + math.sin(a) * 66
        stroke(px, x0, y0, x1, y1, 6)
    stroke(px, S * 0.14, S * 0.72, S * 0.86, S * 0.72, 7)
    for x, d in ((0.30, 1), (0.50, -1), (0.70, 1)):
        stroke(px, S * x, S * 0.72, S * (x + 0.04 * d), S * 0.92, 5)
    return img


print("vẽ icon thời tiết:")
save(sunny(), "w_sunny")
save(rain(), "w_rain")
save(wind(), "w_wind")
save(snow(), "w_snow")
save(storm(), "w_storm")
save(drought(), "w_drought")
print("xong —", OUT)
