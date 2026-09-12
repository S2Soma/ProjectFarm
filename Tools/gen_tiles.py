"""Draw the four plot tiles from scratch.

Proportions deliberately match the sheet these replace (390x320 with the diamond
760/780 of the width and its axis at 47.5% from the bottom), so none of the layout
maths in FarmView has to change — only the pixels do.

Drawn at 2x for crispness on high-DPI screens.

    uv run --with pillow python Tools/gen_tiles.py
"""
from PIL import Image
import math, os, random

OUT = "Assets/Resources/Art/tiles"
os.makedirs(OUT, exist_ok=True)

W, H = 780, 640
A, B = 380.0, 190.0          # diamond half-width / half-height (2:1)
CX = W / 2.0
CY = H * 0.475               # diamond axis, measured from the bottom
DEPTH = 104.0                # soil skirt below the lower edge
RIM = 0.17           # rim band, as a fraction of the diamond                  # rim band, as a fraction of the diamond


def lerp(c1, c2, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(3))


def hexc(s):
    s = s.lstrip("#")
    return tuple(int(s[i:i+2], 16) for i in (0, 2, 4))


def tile(face_hi, face_lo, rim_hi, rim_lo, skirt_hi, skirt_lo,
         furrows=True, seed=3, **_):
    """One plot bed: tilled top face, raised rim, soil skirt.

    No corner posts. The sheet these replace carried a post at all four vertices, so
    neighbouring beds each drew one at the shared corner and the field looked doubled
    up. With beds now separated by grass paths a clean raised rim reads better.
    """
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    px = img.load()
    rng = random.Random(seed)

    RIM_IN = 1.0 - RIM            # where the rim band starts

    for yi in range(H):
        y = yi + 0.5
        for xi in range(W):
            x = xi + 0.5
            dx = abs(x - CX) / A
            if dx > 1.0:
                continue
            half = B * (1.0 - dx)
            top, low = CY + half, CY - half

            if low <= y <= top:
                u = dx + abs(y - CY) / B           # 0 at centre, 1 at the edge
                a = max(0.0, min(1.0, (1.0 - u) * 26.0))
                if u > RIM_IN:
                    # raised border: catches light on the far side, shades on the near
                    t = (u - RIM_IN) / RIM
                    lit = 1.0 if y > CY else 0.45        # far half faces the light
                    col = lerp(rim_lo, rim_hi, lit * (1.0 - t * 0.55))
                    if t < 0.16:                          # inner lip, a crisp step down
                        col = lerp(col, rim_lo, 0.55)
                else:
                    k = (y - CY) / B * 0.5 + 0.5
                    col = lerp(face_lo, face_hi, k)
                    if furrows:
                        # furrows run along the bed's long axis
                        f = math.sin(((x - CX) / A + (y - CY) / B) * 11.0)
                        col = lerp(col, face_lo, (f * 0.5 + 0.5) * 0.30)
                    if u > RIM_IN - 0.05:                 # shadow cast by the rim
                        col = lerp(col, face_lo, (u - (RIM_IN - 0.05)) / 0.05 * 0.5)
                    if rng.random() < 0.03:
                        col = lerp(col, face_hi, 0.28)
            elif low - DEPTH <= y < low:
                d = (low - y) / DEPTH
                col = lerp(skirt_hi, skirt_lo, d * 0.9)
                sv = math.sin(x * 0.075 + math.sin(x * 0.021) * 2.0)
                col = lerp(col, skirt_lo, abs(sv) * 0.35)
                a = max(0.0, min(1.0, (1.0 - dx) * 26.0))
                a = min(a, max(0.0, min(1.0, (1.0 - d) * 5.0)))
            else:
                continue

            if a > 0:
                px[xi, H - 1 - yi] = (col[0], col[1], col[2], int(a * 255))

    return img


VARIANTS = {
    "tile_empty":   dict(face_hi=hexc("#A87542"), face_lo=hexc("#7E5430"),
                         rim_hi=hexc("#C6B393"),  rim_lo=hexc("#93826A"),
                         skirt_hi=hexc("#7A5130"), skirt_lo=hexc("#4E331D")),
    "tile_watered": dict(face_hi=hexc("#7A5637"), face_lo=hexc("#54381F"),
                         rim_hi=hexc("#A99578"), rim_lo=hexc("#7C6C56"),
                         skirt_hi=hexc("#5E3E24"), skirt_lo=hexc("#3B2614")),
    "tile_ready":   dict(face_hi=hexc("#B98249"), face_lo=hexc("#8C5E34"),
                         rim_hi=hexc("#E8CF92"),  rim_lo=hexc("#B39A63"),
                         skirt_hi=hexc("#7A5130"), skirt_lo=hexc("#4E331D")),
    "tile_locked":  dict(face_hi=hexc("#8B958A"), face_lo=hexc("#6B756B"),
                         rim_hi=hexc("#A8B0A4"),  rim_lo=hexc("#7A8278"),
                         skirt_hi=hexc("#6E766C"), skirt_lo=hexc("#4A514A"),
                         furrows=False),
}

for name, kw in VARIANTS.items():
    im = tile(**kw)
    im.save(os.path.join(OUT, name + ".png"))
    print(name + ".png", im.size)
