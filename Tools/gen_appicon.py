"""The app icon: the home island under a sunburst sky, three ripe crops on it and a gold coin,
the whole group cut out like a sticker so it still reads at 48 px on a busy home screen.

Composed from the game's own art, all of it made for this project:

  Art/islands/island_0.png                  Tools/gen_islands.py
  Art/crops_gen/{corn,tomato,strawberry}_3  crop sheets generated for this project (TASKS.md, Task 1)
  Art/items/coin.png                        Tools/gen_items.py

Nothing from Art/farm or Art/ui, whose provenance is unconfirmed.

    uv run --with pillow --with numpy python Tools/gen_appicon.py

Outputs (Assets/Art/AppIcon/, outside Resources so they never ship as game data):

  app_icon.png      1024  the legacy / iOS / store icon, full bleed
  icon_back.png     1024  Android adaptive background: the sunburst sky
  icon_fore.png     1024  Android adaptive foreground: the sticker group, inside the safe circle
"""
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
import math
import os

OUT = "Assets/Art/AppIcon"
S = 1024
os.makedirs(OUT, exist_ok=True)


def load(p):
    im = Image.open(p).convert("RGBA")
    return im.crop(im.getbbox())


def rgba(arr):
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def background():
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    cx, cy = S * 0.5, S * 0.40
    d = np.hypot(xx - cx, yy - cy) / (S * 0.75)
    inner = np.array([255, 236, 150], np.float32)      # warm sun
    mid = np.array([126, 206, 250], np.float32)        # clear sky
    outer = np.array([36, 128, 206], np.float32)       # deep edge
    t1 = np.clip(d / 0.45, 0, 1)[..., None]
    t2 = np.clip((d - 0.45) / 0.55, 0, 1)[..., None]
    col = inner * (1 - t1) + mid * t1
    col = col * (1 - t2) + outer * t2

    # sunburst: sixteen soft wedges a touch lighter than the sky between them
    ang = np.arctan2(yy - cy, xx - cx)
    rays = 0.5 + 0.5 * np.cos(ang * 16)
    rays = np.clip((rays - 0.35) / 0.4, 0, 1) * np.clip(1 - d * 0.9, 0, 1)
    col = col + (255 - col) * (0.16 * rays)[..., None]

    # a light vignette so the corners do not float
    vig = np.clip((d - 0.7) / 0.6, 0, 1)[..., None]
    col = col * (1 - 0.25 * vig)
    alpha = np.full((S, S, 1), 255, np.float32)
    return rgba(np.concatenate([col, alpha], axis=2))


def paste(canvas, img, width, centre):
    h = int(img.height * width / img.width)
    im = img.resize((int(width), h), Image.LANCZOS)
    canvas.alpha_composite(im, (int(centre[0] - im.width / 2), int(centre[1] - im.height / 2)))
    return im


def group(scale=1.0):
    """The island, three crops and the coin, on a transparent layer, as one sticker."""
    g = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    k = scale
    ox, oy = S / 2, S / 2

    def P(x, y):
        return (ox + (x - 512) * k, oy + (y - 512) * k)

    island = load("Assets/Resources/Art/islands/island_0.png")
    isl = paste(g, island, 800 * k, P(512, 668))
    top = P(512, 668)[1] - isl.height * 0.18          # the grass face, a little above the centre

    def plant(name, width, x, ground):
        im = load("Assets/Resources/Art/crops_gen/%s_3.png" % name)
        h = im.height * width * k / im.width
        w = width * k
        r = im.resize((int(w), int(h)), Image.LANCZOS)
        px, _ = P(x, 0)
        g.alpha_composite(r, (int(px - w / 2), int(ground - h * 0.95)))

    plant("corn", 350, 320, top + 12 * k)
    plant("strawberry", 340, 712, top + 44 * k)
    plant("tomato", 430, 512, top + 76 * k)

    coin = load("Assets/Resources/Art/items/coin.png")
    paste(g, coin, 250 * k, P(806, 250))

    # sticker: a white rim hugging the union, then a soft shadow under everything
    a = np.asarray(g.split()[3], np.float32) / 255.0
    rim_mask = Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(int(2 * int(12 * k) + 1)))
    rim_mask = rim_mask.filter(ImageFilter.GaussianBlur(2.5 * k))
    rim = Image.new("RGBA", (S, S), (255, 255, 255, 0))
    rim.putalpha(rim_mask)
    shadow_mask = rim_mask.filter(ImageFilter.GaussianBlur(18 * k))
    shadow = Image.new("RGBA", (S, S), (10, 40, 70, 0))
    shadow.putalpha(shadow_mask.point(lambda v: int(v * 0.45)))

    out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    out.alpha_composite(shadow, (0, int(14 * k)))
    out.alpha_composite(rim)
    out.alpha_composite(g)

    # two sparkles off the coin
    d = ImageDraw.Draw(out)
    for sx, sy, r in ((905, 150, 36), (705, 180, 22)):
        cx, cy = P(sx, sy)
        rr = r * k
        pts = []
        for i in range(8):
            ang = -math.pi / 2 + i * math.pi / 4
            q = rr if i % 2 == 0 else rr * 0.24
            pts.append((cx + math.cos(ang) * q, cy + math.sin(ang) * q))
        d.polygon(pts, fill=(255, 255, 240, 255))
    return out


if __name__ == "__main__":
    back = background()
    back.save(os.path.join(OUT, "icon_back.png"))

    full = back.copy()
    full.alpha_composite(group(1.0))
    full.save(os.path.join(OUT, "app_icon.png"))

    # adaptive: the launcher shows the middle ~66%; keep the group inside it
    group(0.66).save(os.path.join(OUT, "icon_fore.png"))
    print("app icon ->", OUT)
