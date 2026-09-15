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


def grid_more(size=192):
    """The "more" affordance: a 2x2 of rounded squares.

    Three horizontal dots is the usual idiom, but it is drawn at the bottom of a VERTICAL
    rail here, where a horizontal ellipsis reads as a separator between the buttons above
    and below it rather than as a button itself. A 2x2 block reads as "the rest of them"
    at 44 px, which is the size it actually renders at."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    px = img.load()
    half = size * 0.155          # half-extent of one square
    gap = size * 0.055
    r = size * 0.052             # corner radius
    centres = []
    for sy in (-1, 1):
        for sx in (-1, 1):
            centres.append((size / 2 + sx * (half + gap), size / 2 + sy * (half + gap)))

    for y in range(size):
        for x in range(size):
            fx, fy = x + 0.5, y + 0.5
            best = 1e9
            for cx, cy in centres:
                dx = max(0.0, abs(fx - cx) - (half - r))
                dy = max(0.0, abs(fy - cy) - (half - r))
                best = min(best, math.hypot(dx, dy) - r)
            a = max(0.0, min(1.0, 0.5 - best))
            if a > 0:
                px[x, y] = (255, 255, 255, int(a * 255))
    return img


def speaker(size=192, on=True):
    """Sound on / off: a speaker cone with two sound waves, or with a cross. Supersampled 4x."""
    SSz = size * 4
    big = Image.new("L", (SSz, SSz), 0)
    from PIL import ImageDraw
    d = ImageDraw.Draw(big)
    k = SSz / 192.0
    # box and cone
    d.rounded_rectangle((30 * k, 74 * k, 66 * k, 118 * k), radius=8 * k, fill=255)
    d.polygon([(58 * k, 76 * k), (104 * k, 40 * k), (104 * k, 152 * k), (58 * k, 116 * k)], fill=255)
    d.rounded_rectangle((96 * k, 38 * k, 110 * k, 154 * k), radius=7 * k, fill=255)
    if on:
        for r, w in ((30, 13), (58, 13)):
            d.arc((104 * k - r * k, 96 * k - r * k, 104 * k + r * k + 20 * k, 96 * k + r * k), -50, 50,
                  fill=255, width=int(w * k))
    else:
        for (x0, y0, x1, y1) in ((128, 70, 170, 122), (170, 70, 128, 122)):
            d.line((x0 * k, y0 * k, x1 * k, y1 * k), fill=255, width=int(15 * k))
        for (cx, cy) in ((128, 70), (170, 70), (128, 122), (170, 122)):
            d.ellipse(((cx - 7.5) * k, (cy - 7.5) * k, (cx + 7.5) * k, (cy + 7.5) * k), fill=255)
    a = big.resize((size, size), Image.LANCZOS)
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    img.putalpha(a)
    return img


def _glyph(draw_fn, size=192):
    """White glyph drawn by draw_fn(ImageDraw, k) on a 192-unit grid, supersampled 4x."""
    from PIL import ImageDraw
    SSz = size * 4
    big = Image.new("L", (SSz, SSz), 0)
    draw_fn(ImageDraw.Draw(big), SSz / 192.0)
    a = big.resize((size, size), Image.LANCZOS)
    img = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    img.putalpha(a)
    return img


def upgrade_arrow(size=192):
    """Nâng cấp: a heavy arrow up off a base line. The menu used the barn for it, which is
    also what a warehouse looks like — so "Nâng cấp" and "Kho" were two buildings."""
    def draw(d, k):
        head = [(96 * k, 30 * k), (156 * k, 90 * k), (36 * k, 90 * k)]
        d.polygon(head, fill=255)
        # a thick round-jointed stroke round the head rounds its corners without knobs
        d.line(head + [head[0], head[1]], fill=255, width=int(18 * k), joint="curve")
        d.rounded_rectangle((68 * k, 84 * k, 124 * k, 150 * k), radius=10 * k, fill=255)
        d.rounded_rectangle((40 * k, 158 * k, 152 * k, 180 * k), radius=11 * k, fill=255)
    return _glyph(draw, size)


def album(size=192):
    """Sưu tập: a closed book with a star cut through its cover and a spine line. The pack's
    album glyph was an empty card frame, which at 36 px read as a box."""
    def draw(d, k):
        d.rounded_rectangle((36 * k, 22 * k, 158 * k, 172 * k), radius=18 * k, fill=255)
        d.rectangle((62 * k, 22 * k, 70 * k, 172 * k), fill=0)          # spine
        d.rounded_rectangle((36 * k, 150 * k, 158 * k, 158 * k), radius=3 * k, fill=0)   # page edge
        cx, cy, R, r = 112, 88, 38, 16
        pts = []
        for i in range(10):
            ang = -math.pi / 2 + i * math.pi / 5
            rr = R if i % 2 == 0 else r
            pts.append(((cx + math.cos(ang) * rr) * k, (cy + math.sin(ang) * rr) * k))
        d.polygon(pts, fill=0)
    return _glyph(draw, size)


def account(size=192):
    """Menu ▸ Tài khoản: a head and shoulders."""
    def draw(d, k):
        d.ellipse((64 * k, 22 * k, 128 * k, 86 * k), fill=255)
        d.chord((30 * k, 100 * k, 162 * k, 232 * k), 180, 360, fill=255)
        d.rectangle((30 * k, 160 * k, 162 * k, 170 * k), fill=255)
    return _glyph(draw, size)


def cloud(size=192):
    """Sync status: a cloud (the check / cross is drawn by the UI in colour)."""
    def draw(d, k):
        d.ellipse((24 * k, 84 * k, 88 * k, 148 * k), fill=255)
        d.ellipse((58 * k, 44 * k, 138 * k, 124 * k), fill=255)
        d.ellipse((106 * k, 84 * k, 170 * k, 148 * k), fill=255)
        d.rounded_rectangle((52 * k, 100 * k, 142 * k, 148 * k), radius=10 * k, fill=255)
    return _glyph(draw, size)


def phone(size=192):
    """"Trên máy này" in the save choice: a phone with its screen cut out."""
    def draw(d, k):
        d.rounded_rectangle((52 * k, 14 * k, 140 * k, 178 * k), radius=18 * k, fill=255)
        d.rounded_rectangle((62 * k, 34 * k, 130 * k, 146 * k), radius=6 * k, fill=0)
        d.ellipse((88 * k, 152 * k, 104 * k, 168 * k), fill=0)
    return _glyph(draw, size)


def music(size=192, on=True):
    """Nhạc nền on / off: two beamed eighth notes, or the same notes struck through."""
    def draw(d, k):
        d.ellipse((30 * k, 128 * k, 82 * k, 170 * k), fill=255)          # left note head
        d.ellipse((112 * k, 110 * k, 164 * k, 152 * k), fill=255)        # right note head
        d.rectangle((68 * k, 44 * k, 82 * k, 150 * k), fill=255)         # stems
        d.rectangle((150 * k, 26 * k, 164 * k, 132 * k), fill=255)
        d.polygon([(68 * k, 44 * k), (164 * k, 22 * k), (164 * k, 54 * k), (68 * k, 76 * k)], fill=255)   # beam
        if not on:
            d.line((24 * k, 24 * k, 168 * k, 168 * k), fill=0, width=int(34 * k))
            d.line((24 * k, 24 * k, 168 * k, 168 * k), fill=255, width=int(14 * k))
    return _glyph(draw, size)


ALL = ((droplet, "droplet"), (checkmark, "check"), (exclamation, "alert"),
       (grid_more, "more"), (lambda: speaker(on=True), "sound_on"), (lambda: speaker(on=False), "sound_off"),
       (upgrade_arrow, "nav_upgrade"), (album, "nav_album2"), (account, "account"), (cloud, "cloud"), (phone, "phone"),
       (lambda: music(on=True), "music_on"), (lambda: music(on=False), "music_off"))

if __name__ == "__main__":
    import sys
    want = set(sys.argv[1:])            # e.g. python Tools/gen_icons.py nav_upgrade nav_album2
    for fn, name in ALL:
        if want and name not in want:
            continue
        im = fn()
        im.save(os.path.join(OUT, name + ".png"))
        print(name + ".png", im.size)
