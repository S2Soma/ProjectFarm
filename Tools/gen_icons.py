"""Small game icons drawn from scratch, for shapes no CC0 pack happened to carry.

    uv run --with pillow python Tools/gen_icons.py
"""
from PIL import Image
import math, os

OUT = "Assets/Resources/Art/gen"
os.makedirs(OUT, exist_ok=True)


def droplet(size=192):
    """A water drop: circular bowl below, tapering to a point on top."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    cx = size / 2.0
    cy = size * 0.62          # centre of the bowl
    r = size * 0.30           # bowl radius
    tipy = size * 0.10        # where the point sits

    for y in range(size):
        for x in range(size):
            fx, fy = x + 0.5, y + 0.5
            if fy >= cy:
                d = math.hypot(fx - cx, fy - cy) - r
            else:
                # taper: the half-width shrinks to zero at the tip
                t = (cy - fy) / (cy - tipy)
                if t > 1.0:
                    continue
                # sqrt(1-t^3) keeps the sides almost straight until it collapses at the
                # very tip, which draws an egg. A plain power curve tapers all the way up.
                half = r * (1.0 - t) ** 0.62
                d = abs(fx - cx) - half
            a = max(0.0, min(1.0, 0.5 - d))
            if a <= 0:
                continue
            # lighter at the top-left so it reads as glass
            sh = 1.0 - 0.22 * ((fx - cx) / size + (fy - cy) / size)
            v = max(0.0, min(1.0, sh))
            px[x, y] = (int(255 * v), int(255 * v), int(255 * v), int(a * 255))

    # highlight blob
    hx, hy, hr = cx - r * 0.38, cy - r * 0.30, r * 0.22
    for y in range(size):
        for x in range(size):
            if px[x, y][3] == 0:
                continue
            d = math.hypot(x + 0.5 - hx, y + 0.5 - hy) - hr
            if d < 0:
                a = px[x, y][3]
                px[x, y] = (255, 255, 255, a)
    return img


def seg_dist(px, py, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    t = ((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy)
    t = max(0.0, min(1.0, t))
    return math.hypot(px - (ax + t * vx), py - (ay + t * vy))


def checkmark(size=192, weight=0.115):
    """Chunky tick. The pack's own check was a hairline and vanished at badge size."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    w = size * weight
    a = (size * 0.20, size * 0.54)
    b = (size * 0.42, size * 0.74)
    c = (size * 0.80, size * 0.28)
    for y in range(size):
        for x in range(size):
            fx, fy = x + 0.5, y + 0.5
            d = min(seg_dist(fx, fy, a[0], a[1], b[0], b[1]),
                    seg_dist(fx, fy, b[0], b[1], c[0], c[1])) - w
            al = max(0.0, min(1.0, 0.5 - d))
            if al > 0:
                px[x, y] = (255, 255, 255, int(al * 255))
    return img


def exclamation(size=192):
    """Tapered bar over a dot. The CC0 one was an 8x16 source — far too small to scale."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    cx = size / 2.0
    top, bot = size * 0.16, size * 0.62
    wtop, wbot = size * 0.105, size * 0.072
    dot_y, dot_r = size * 0.80, size * 0.088
    for y in range(size):
        for x in range(size):
            fx, fy = x + 0.5, y + 0.5
            al = 0.0
            if top <= fy <= bot:
                t = (fy - top) / (bot - top)
                half = wtop + (wbot - wtop) * t
                al = max(al, min(1.0, 0.5 - (abs(fx - cx) - half)))
            al = max(al, min(1.0, 0.5 - (math.hypot(fx - cx, fy - dot_y) - dot_r)))
            al = max(0.0, min(1.0, al))
            if al > 0:
                px[x, y] = (255, 255, 255, int(al * 255))
    return img


for fn, name in ((droplet, "droplet"), (checkmark, "check"), (exclamation, "alert")):
    im = fn()
    im.save(os.path.join(OUT, name + ".png"))
    print(name + ".png", im.size)
