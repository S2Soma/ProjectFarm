"""Mutation effects for the farm: light rays, a ground halo and a spark, all grey + alpha so the
game tints them with the element's colour (Art.Elements[v].glow).

    uv run --with pillow --with numpy python Tools/gen_fx.py

  Art/fx/mut_rays.png    512  soft sunburst, twelve wedges fading outward, rotated in game
  Art/fx/mut_halo.png    512  an isometric ellipse ring (2:1, the bed's diamond) glowing on the soil
  Art/fx/mut_spark.png   128  a four-point star with a soft core, for the motes rising off the plant
  Art/fx/edge_wash.png   256  white, clear in the middle and thickening toward the screen's edges:
                              the weather's tint at the rim of the view instead of over the farm
"""
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
import math, os, random, re, uuid

OUT = "Assets/Resources/Art/fx"
META = "Assets/Resources/Art/beds/bed_sparkle.png.meta"
os.makedirs(OUT, exist_ok=True)


def save(arr_alpha, name, lum=None):
    a = np.clip(arr_alpha, 0, 1)
    l = np.ones_like(a) if lum is None else np.clip(lum, 0, 1)
    rgb = (l * 255).astype(np.uint8)
    img = Image.fromarray(np.dstack([rgb, rgb, rgb, (a * 255).astype(np.uint8)]), "RGBA")
    path = os.path.join(OUT, name + ".png")
    img.save(path)
    meta = path + ".meta"
    guid = None
    if os.path.exists(meta):
        guid = re.search(r"guid: (\w+)", open(meta).read()).group(1)
    m = open(META).read()
    m = re.sub(r"guid: \w+", "guid: " + (guid or uuid.uuid4().hex), m, count=1)
    open(meta, "w").write(m)
    print("  ", name)


def rays(S=512, n=12):
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    c = (S - 1) / 2
    dx, dy = xx - c, yy - c
    r = np.hypot(dx, dy) / c
    ang = np.arctan2(dy, dx)
    wedge = 0.5 + 0.5 * np.cos(ang * n)
    wedge = np.clip((wedge - 0.45) / 0.55, 0, 1) ** 1.6
    fade = np.clip(1 - r, 0, 1) ** 1.3 * np.clip(r / 0.18, 0, 1)
    core = np.clip(1 - r / 0.35, 0, 1) ** 2 * 0.35
    save(wedge * fade * 0.9 + core, "mut_rays")


def halo(W=512, H=256):
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    nx = (xx - (W - 1) / 2) / ((W - 1) / 2)
    ny = (yy - (H - 1) / 2) / ((H - 1) / 2)
    d = np.hypot(nx, ny)
    ring = np.exp(-((d - 0.78) / 0.10) ** 2)
    fill = np.clip(1 - d / 0.8, 0, 1) ** 1.5 * 0.35
    save(np.clip(ring * 0.95 + fill, 0, 1), "mut_halo")


def spark(S=128):
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    c = (S - 1) / 2
    dx, dy = (xx - c) / c, (yy - c) / c
    r = np.hypot(dx, dy)
    arms = np.maximum(np.exp(-(dx / 0.06) ** 2) * np.clip(1 - abs(dy), 0, 1) ** 2,
                      np.exp(-(dy / 0.06) ** 2) * np.clip(1 - abs(dx), 0, 1) ** 2)
    core = np.exp(-(r / 0.22) ** 2)
    save(np.clip(arms + core, 0, 1), "mut_spark")


def edge_wash(S=256):
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    c = (S - 1) / 2
    r = np.hypot((xx - c) / c, (yy - c) / c)
    t = np.clip((r - 0.62) / (1.30 - 0.62), 0, 1)
    save(t * t * (3 - 2 * t), "edge_wash")


def rain_streak():
    """A raindrop in motion: a thin streak, bright at the head and fading up its tail."""
    Wd, Hd = 16, 128
    yy, xx = np.mgrid[0:Hd, 0:Wd].astype(np.float32)
    across = np.exp(-((xx - (Wd - 1) / 2) / 2.2) ** 2)
    along = np.clip(yy / (Hd - 1), 0, 1) ** 1.6
    save(across * along, "p_rain")


def snowflake(S=64):
    """A soft six-armed flake for the near layer; far flakes are plain dots."""
    img = Image.new("L", (S * 4, S * 4), 0)
    d = ImageDraw.Draw(img)
    c = S * 2
    for k in range(6):
        a = k * math.pi / 3
        x1, y1 = c + math.cos(a) * S * 1.6, c + math.sin(a) * S * 1.6
        d.line([(c, c), (x1, y1)], fill=255, width=int(S * 0.16))
        for t in (0.55, 0.85):
            bx, by = c + math.cos(a) * S * 1.6 * t, c + math.sin(a) * S * 1.6 * t
            for side in (-1, 1):
                b = a + side * 0.7
                d.line([(bx, by), (bx + math.cos(b) * S * 0.45, by + math.sin(b) * S * 0.45)], fill=255, width=int(S * 0.12))
    d.ellipse([c - S * 0.3, c - S * 0.3, c + S * 0.3, c + S * 0.3], fill=255)
    img = img.filter(ImageFilter.GaussianBlur(S * 0.06)).resize((S, S), Image.LANCZOS)
    save(np.asarray(img, np.float32) / 255.0, "p_flake")


def bolts():
    """Three lightning bolts: a jagged main channel with forks, a white core and a blue glow."""
    for v in range(3):
        rng = random.Random(40 + v)
        Wd, Hd = 256, 640
        core = Image.new("L", (Wd, Hd), 0)
        d = ImageDraw.Draw(core)

        def channel(x, y, length, width, depth):
            pts = [(x, y)]
            while y < length:
                x += rng.uniform(-26, 26)
                y += rng.uniform(18, 42)
                pts.append((x, y))
                if depth > 0 and rng.random() < 0.12:
                    channel(x, y, min(Hd, y + rng.uniform(80, 200)), max(2, width * 0.5), depth - 1)
            d.line(pts, fill=255, width=int(width), joint="curve")

        channel(Wd / 2 + rng.uniform(-20, 20), 0, Hd * rng.uniform(0.8, 0.98), 9, 2)
        glow = core.filter(ImageFilter.GaussianBlur(14))
        a = np.clip(np.asarray(core, np.float32) / 255.0 + np.asarray(glow, np.float32) / 255.0 * 1.6, 0, 1)
        lum = np.clip(0.75 + np.asarray(core, np.float32) / 255.0 * 0.25, 0, 1)
        save(a, "p_bolt_%d" % v, lum)


if __name__ == "__main__":
    rain_streak()
    snowflake()
    bolts()
    edge_wash()
    rays()
    halo()
    spark()
