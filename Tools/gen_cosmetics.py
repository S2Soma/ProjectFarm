"""The Trang trí shelf's art: plot borders, particle sprites for the effects, and one shop icon
per cosmetic. All drawn procedurally for this project.

    uv run --with pillow --with numpy python Tools/gen_cosmetics.py          # everything
    uv run --with pillow --with numpy python Tools/gen_cosmetics.py icons    # one group

Outputs
-------
Assets/Resources/Art/beds/skin_<id>.png   780 x 640, the beds' own canvas (Tools/gen_beds.py): a
                                          decorative border sitting on the raised bed's rim,
                                          laid over the bed and under the crop by IslandView
Assets/Resources/Art/fx/p_<name>.png      particle sprites for UI/FxKit (bubble, heart, confetti,
                                          rainbow, diamond, ring, star, spark, drop)
Assets/Resources/Art/items/cos_<id>.png   256 px shop icons for the cosmetics without one
"""
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
import math, os, random, re, sys, uuid

BEDS = "Assets/Resources/Art/beds"
FX = "Assets/Resources/Art/fx"
ITEMS = "Assets/Resources/Art/items"
META_BED = "Assets/Resources/Art/beds/bed_sparkle.png.meta"
META_ITEM = "Assets/Resources/Art/items/item_can.png.meta"

# the beds' geometry, as in Tools/gen_beds.py
W, H = 780, 640
A, B = 380.0, 190.0
CX = W / 2.0
CY = H - H * 0.475
BED = 0.885
SS = 2


def meta_for(path, template):
    meta = path + ".meta"
    guid = None
    if os.path.exists(meta):
        guid = re.search(r"guid: (\w+)", open(meta).read()).group(1)
    m = open(template).read()
    m = re.sub(r"guid: \w+", "guid: " + (guid or uuid.uuid4().hex), m, count=1)
    open(meta, "w").write(m)


def save(img, folder, name, template):
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name + ".png")
    img.save(path)
    meta_for(path, template)
    print("  ", path, img.size)


def hexc(s, a=255):
    s = s.lstrip("#")
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4)) + (a,)


# ============================================================ plot borders
def rim_points(u, step_px):
    """Points round the diamond of radius u, evenly spaced in pixels, with the edge direction."""
    top = (CX, CY - B * u)
    right = (CX + A * u, CY)
    bottom = (CX, CY + B * u)
    left = (CX - A * u, CY)
    pts = []
    for a, b in ((top, right), (right, bottom), (bottom, left), (left, top)):
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        n = max(1, int(L / step_px))
        for i in range(n):
            t = (i + 0.5) / n
            pts.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, math.atan2(b[1] - a[1], b[0] - a[0])))
    return pts


def band_polygon(u0, u1):
    """The ring between two diamonds, as an outer and an inner polygon."""
    outer = [(CX, CY - B * u1), (CX + A * u1, CY), (CX, CY + B * u1), (CX - A * u1, CY)]
    inner = [(CX, CY - B * u0), (CX + A * u0, CY), (CX, CY + B * u0), (CX - A * u0, CY)]
    return outer, inner


def ring_mask(u0, u1):
    m = Image.new("L", (W * SS, H * SS), 0)
    d = ImageDraw.Draw(m)
    o, i = band_polygon(u0, u1)
    d.polygon([(x * SS, y * SS) for x, y in o], fill=255)
    d.polygon([(x * SS, y * SS) for x, y in i], fill=0)
    return m


def finish(canvas):
    return canvas.resize((W, H), Image.LANCZOS)


def skin_wood():
    img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    # a honey-coloured plank frame, lighter than the soil so it reads at a glance: a dark outer
    # lip, a sunlit inner edge, a joint every plank and a nail either side of it
    img.paste(Image.new("RGBA", img.size, hexc("#5A3719")), (0, 0), ring_mask(0.82, 0.995))
    img.paste(Image.new("RGBA", img.size, hexc("#D59A58")), (0, 0), ring_mask(0.835, 0.975))
    img.paste(Image.new("RGBA", img.size, hexc("#F0C27F")), (0, 0), ring_mask(0.835, 0.865))
    img.paste(Image.new("RGBA", img.size, hexc("#B57A3E")), (0, 0), ring_mask(0.94, 0.975))
    d = ImageDraw.Draw(img)
    for x, y, ang in rim_points(0.905, 52):
        nx, ny = -math.sin(ang), math.cos(ang)
        d.line([((x - nx * 30) * SS, (y - ny * 15) * SS), ((x + nx * 30) * SS, (y + ny * 15) * SS)], fill=hexc("#5A3719"), width=4 * SS)
        for side in (-1, 1):
            px, py = x + side * 9 * math.cos(ang), y + side * 9 * math.sin(ang)
            d.ellipse([(px - 3.5) * SS, (py - 2.5) * SS, (px + 3.5) * SS, (py + 2.5) * SS], fill=hexc("#3B2410"))
    return finish(img)


def skin_stone():
    img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    rng = random.Random(7)
    d = ImageDraw.Draw(img)
    greys = ["#B9B4AC", "#A39E96", "#CFC9BF", "#8F8A83", "#C4BBAE"]
    for x, y, ang in rim_points(0.915, 30):
        r = rng.uniform(15, 21)
        rx, ry = r, r * 0.62
        x += rng.uniform(-4, 4); y += rng.uniform(-2, 2)
        c = hexc(rng.choice(greys))
        d.ellipse([(x - rx) * SS, (y - ry + 5) * SS, (x + rx) * SS, (y + ry + 5) * SS], fill=(40, 30, 20, 150))
        d.ellipse([(x - rx) * SS, (y - ry) * SS, (x + rx) * SS, (y + ry) * SS], fill=c)
        d.ellipse([(x - rx * 0.6) * SS, (y - ry * 0.75) * SS, (x + rx * 0.1) * SS, (y - ry * 0.15) * SS], fill=(255, 255, 255, 90))
    img = img.filter(ImageFilter.GaussianBlur(0.6 * SS))
    return finish(img)


def flower(d, x, y, r, petal, centre):
    for k in range(5):
        a = k * 2 * math.pi / 5 - math.pi / 2
        px, py = x + math.cos(a) * r * 0.75, y + math.sin(a) * r * 0.5
        d.ellipse([(px - r * 0.55) * SS, (py - r * 0.4) * SS, (px + r * 0.55) * SS, (py + r * 0.4) * SS], fill=petal)
    d.ellipse([(x - r * 0.35) * SS, (y - r * 0.26) * SS, (x + r * 0.35) * SS, (y + r * 0.26) * SS], fill=centre)


def skin_flower():
    img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    mask = ring_mask(0.86, 0.97)
    hedge = Image.new("RGBA", img.size, hexc("#5FA646"))
    img.paste(hedge, (0, 0), mask)
    rng = random.Random(11)
    d = ImageDraw.Draw(img)
    for x, y, ang in rim_points(0.915, 16):
        r = rng.uniform(8, 13)
        d.ellipse([(x - r) * SS, (y - r * 0.6) * SS, (x + r) * SS, (y + r * 0.6) * SS],
                  fill=hexc(rng.choice(["#4E9A3C", "#6DBB4F", "#3F8431"])))
    cols = ["#FF8FB1", "#FFD35C", "#FFFFFF", "#B98CFF", "#FF7A6B"]
    for x, y, ang in rim_points(0.915, 44):
        flower(d, x + rng.uniform(-6, 6), y + rng.uniform(-3, 3), rng.uniform(11, 15), hexc(rng.choice(cols)), hexc("#F6B92B"))
    return finish(img.filter(ImageFilter.GaussianBlur(0.4 * SS)))


def skin_crystal():
    img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    rng = random.Random(5)
    glow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).polygon([(x * SS, y * SS) for x, y in band_polygon(0.86, 0.98)[0]], fill=(120, 220, 255, 90))
    ImageDraw.Draw(glow).polygon([(x * SS, y * SS) for x, y in band_polygon(0.86, 0.98)[1]], fill=(0, 0, 0, 0))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(10 * SS)))
    d = ImageDraw.Draw(img)
    cols = [("#8FE3FF", "#E9FBFF"), ("#B79BFF", "#F1EBFF"), ("#7FF0D0", "#E6FFF7")]
    for x, y, ang in rim_points(0.915, 34):
        h = rng.uniform(22, 38); w = rng.uniform(8, 12)
        tilt = rng.uniform(-0.35, 0.35)
        base, light = cols[rng.randrange(3)]
        tipx, tipy = x + math.sin(tilt) * h, y - math.cos(tilt) * h
        poly = [((x - w) * SS, (y + 3) * SS), (tipx * SS, tipy * SS), ((x + w) * SS, (y + 3) * SS)]
        d.polygon([(px, py + 5 * SS) for px, py in poly], fill=(30, 40, 80, 90))
        d.polygon(poly, fill=hexc(base, 235))
        d.polygon([((x - w * 0.2) * SS, (y + 1) * SS), (tipx * SS, tipy * SS), ((x + w) * SS, (y + 3) * SS)], fill=hexc(light, 170))
    return finish(img)


def skin_gold():
    img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
    for u0, u1, col in ((0.84, 0.985, "#9C6A12"), (0.85, 0.975, "#E7B53A"), (0.88, 0.945, "#FFE08A"), (0.895, 0.93, "#F3C24C")):
        img.paste(Image.new("RGBA", img.size, hexc(col)), (0, 0), ring_mask(u0, u1))
    d = ImageDraw.Draw(img)
    # studs along the band, a gem at each corner
    for x, y, ang in rim_points(0.912, 58):
        d.ellipse([(x - 6) * SS, (y - 4) * SS, (x + 6) * SS, (y + 4) * SS], fill=hexc("#FFF6CF"))
        d.ellipse([(x - 3) * SS, (y - 2.5) * SS, (x + 3) * SS, (y + 1) * SS], fill=hexc("#FFFFFF"))
    gems = [("#FF4F6D", (CX, CY - B * 0.912)), ("#4FB6FF", (CX + A * 0.912, CY)),
            ("#3FD69A", (CX, CY + B * 0.912)), ("#B26BFF", (CX - A * 0.912, CY))]
    for col, (x, y) in gems:
        r = 17
        pts = [(x, y - r), (x + r * 1.3, y), (x, y + r), (x - r * 1.3, y)]
        d.polygon([((px) * SS, (py + 3) * SS) for px, py in pts], fill=(60, 40, 0, 140))
        d.polygon([(px * SS, py * SS) for px, py in pts], fill=hexc("#8A5A08"))
        inner = [(x, y - r * 0.72), (x + r * 0.95, y), (x, y + r * 0.72), (x - r * 0.95, y)]
        d.polygon([(px * SS, py * SS) for px, py in inner], fill=hexc(col))
        d.polygon([(x * SS, (y - r * 0.72) * SS), ((x + r * 0.95) * SS, y * SS), (x * SS, y * SS)], fill=(255, 255, 255, 110))
    return finish(img)


def beds():
    for name, fn in (("wood", skin_wood), ("stone", skin_stone), ("flower", skin_flower),
                     ("crystal", skin_crystal), ("gold", skin_gold)):
        save(fn(), BEDS, "skin_" + name, META_BED)


# ============================================================ particles
def particles():
    S = 128

    def canvas():
        return Image.new("RGBA", (S * SS, S * SS), (0, 0, 0, 0))

    def out(img, name):
        save(img.resize((S, S), Image.LANCZOS), FX, "p_" + name, META_BED)

    c = S * SS / 2

    # bubble: a thin iridescent rim, a clear body, two highlights
    img = canvas(); d = ImageDraw.Draw(img)
    r = S * SS * 0.42
    for k, col in enumerate([(160, 220, 255, 190), (255, 170, 230, 150), (255, 240, 170, 130)]):
        off = (k - 1) * 3 * SS
        d.ellipse([c - r + off, c - r, c + r + off, c + r], outline=col, width=5 * SS)
    d.ellipse([c - r * 0.9, c - r * 0.9, c + r * 0.9, c + r * 0.9], fill=(210, 240, 255, 38))
    d.ellipse([c - r * 0.55, c - r * 0.65, c - r * 0.15, c - r * 0.3], fill=(255, 255, 255, 230))
    d.ellipse([c + r * 0.3, c + r * 0.35, c + r * 0.45, c + r * 0.5], fill=(255, 255, 255, 180))
    out(img, "bubble")

    # heart
    img = canvas(); d = ImageDraw.Draw(img)
    pts = []
    for k in range(200):
        t = k / 200 * 2 * math.pi
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((c + x * S * SS / 40, c - y * S * SS / 40 - 4 * SS))
    d.polygon(pts, fill=(255, 255, 255, 255))
    d.ellipse([c - 26 * SS, c - 30 * SS, c - 10 * SS, c - 16 * SS], fill=(255, 255, 255, 255))
    out(img, "heart")    # white: tinted per effect

    # confetti: a rounded rectangle, white
    img = canvas(); d = ImageDraw.Draw(img)
    d.rounded_rectangle([c - 22 * SS, c - 12 * SS, c + 22 * SS, c + 12 * SS], radius=5 * SS, fill=(255, 255, 255, 255))
    out(img, "confetti")

    # diamond sparkle: a faceted rhombus with a soft glow
    img = canvas(); d = ImageDraw.Draw(img)
    glow = canvas(); ImageDraw.Draw(glow).ellipse([c - 40 * SS, c - 40 * SS, c + 40 * SS, c + 40 * SS], fill=(255, 255, 255, 120))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(10 * SS)))
    d = ImageDraw.Draw(img)
    d.polygon([(c, c - 36 * SS), (c + 22 * SS, c), (c, c + 36 * SS), (c - 22 * SS, c)], fill=(255, 255, 255, 255))
    d.polygon([(c, c - 36 * SS), (c + 22 * SS, c), (c, c)], fill=(215, 240, 255, 255))
    out(img, "diamond")

    # ring: a soft expanding ring for taps
    ys, xs = np.mgrid[0:S * SS, 0:S * SS].astype(np.float32)
    rr = np.hypot(xs - c, ys - c) / c
    a = np.exp(-((rr - 0.78) / 0.08) ** 2)
    arr = np.dstack([np.full_like(a, 255), np.full_like(a, 255), np.full_like(a, 255), a * 255]).astype(np.uint8)
    out(Image.fromarray(arr, "RGBA"), "ring")

    # soft dot for fairy dust / glows
    a = np.clip(1 - rr, 0, 1) ** 2.2
    arr = np.dstack([np.full_like(a, 255), np.full_like(a, 255), np.full_like(a, 255), a * 255]).astype(np.uint8)
    out(Image.fromarray(arr, "RGBA"), "dot")

    # rainbow arc (wider canvas)
    RW, RH = 256 * SS, 140 * SS
    ys, xs = np.mgrid[0:RH, 0:RW].astype(np.float32)
    rr = np.hypot(xs - RW / 2, ys - RH * 0.98) / (RW * 0.46)
    bands = [(255, 90, 90), (255, 170, 70), (255, 230, 90), (110, 220, 120), (90, 180, 255), (170, 120, 255)]
    rgb = np.zeros((RH, RW, 3), np.float32); al = np.zeros((RH, RW), np.float32)
    for k, col in enumerate(bands):
        r0 = 1.0 - k * 0.075
        m = np.clip(1 - np.abs(rr - r0) / 0.042, 0, 1)
        rgb = rgb * (1 - m[..., None]) + np.array(col, np.float32) * m[..., None]
        al = np.maximum(al, m)
    al *= np.clip((RH * 0.98 - ys) / (RH * 0.25), 0, 1)
    arr = np.dstack([rgb, al * 235]).astype(np.uint8)
    save(Image.fromarray(arr, "RGBA").resize((256, 140), Image.LANCZOS), FX, "p_rainbow", META_BED)


# ============================================================ shop icons
BADGE = {
    "Plot": ("#E9C88F", "#B7864C"),
    "Plant": ("#A6E98C", "#4DAA4A"),
    "Water": ("#9EDCFF", "#3F93D6"),
    "Harvest": ("#FFE08A", "#E29B2C"),
    "Toast": ("#FFF2D6", "#D9B77C"),
    "Tap": ("#FFC2DD", "#E06AA2"),
    "Swipe": ("#D6C6FF", "#8A6BE0"),
}


def badge(slot):
    S = 256 * SS
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    top, bottom = BADGE[slot]
    t = np.linspace(0, 1, S)[:, None]
    a = np.array(hexc(top)[:3], np.float32); b = np.array(hexc(bottom)[:3], np.float32)
    grad = (a * (1 - t[..., None]) + b * t[..., None]).repeat(S, axis=1)
    grad_img = Image.fromarray(grad.astype(np.uint8), "RGB").convert("RGBA")
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).rounded_rectangle([14 * SS, 14 * SS, S - 14 * SS, S - 14 * SS], radius=58 * SS, fill=255)
    shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle([14 * SS, 20 * SS, S - 14 * SS, S - 8 * SS], radius=58 * SS, fill=(60, 40, 20, 110))
    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(5 * SS)))
    img.paste(grad_img, (0, 0), m)
    rim = Image.new("L", (S, S), 0)
    ImageDraw.Draw(rim).rounded_rectangle([14 * SS, 14 * SS, S - 14 * SS, S - 14 * SS], radius=58 * SS, outline=255, width=6 * SS)
    img.paste(Image.new("RGBA", (S, S), (255, 255, 255, 150)), (0, 0), rim)
    return img


def place(img, sprite_path, cx, cy, size, tint=None, rot=0, alpha=1.0, shadow=True):
    S = 256 * SS
    sp = Image.open(sprite_path).convert("RGBA")
    k = size * SS / max(sp.width, sp.height)
    sp = sp.resize((max(1, int(sp.width * k)), max(1, int(sp.height * k))), Image.LANCZOS)
    if tint is not None:
        arr = np.asarray(sp).astype(np.float32)
        arr[..., :3] = arr[..., :3] * np.array(tint[:3], np.float32) / 255.0
        sp = Image.fromarray(arr.astype(np.uint8), "RGBA")
    if rot:
        sp = sp.rotate(rot, expand=True, resample=Image.BICUBIC)
    if alpha < 1:
        a = sp.split()[3].point(lambda v: int(v * alpha))
        sp.putalpha(a)
    x0, y0 = int(cx * SS - sp.width / 2), int(cy * SS - sp.height / 2)
    if shadow:
        # a soft dark halo, so a pale spark still reads on a light badge
        sh = Image.new("RGBA", sp.size, (40, 25, 10, 0))
        sh.putalpha(sp.split()[3].point(lambda v: int(v * 0.45)))
        pad = Image.new("RGBA", (sp.width + 16 * SS, sp.height + 16 * SS), (0, 0, 0, 0))
        pad.alpha_composite(sh, (8 * SS, 10 * SS))
        img.alpha_composite(pad.filter(ImageFilter.GaussianBlur(3 * SS)), (x0 - 8 * SS, y0 - 8 * SS))
    img.alpha_composite(sp, (x0, y0))


def icon_save(img, name):
    save(img.resize((256, 256), Image.LANCZOS), ITEMS, "cos_" + name, META_ITEM)


def icons():
    P = lambda n: os.path.join(FX, "p_" + n + ".png")
    STAR = os.path.join(FX, "mut_spark.png")
    PETAL = os.path.join(ITEMS, "particle_petal.png")
    LEAF = os.path.join(ITEMS, "particle_leaf.png")
    COIN = os.path.join(ITEMS, "coin.png")
    DROP = "Assets/Resources/Art/gen/droplet.png"
    rng = random.Random(3)

    # plot skins: the border on a small bed
    bed_img = Image.open(os.path.join(BEDS, "bed_empty_0.png")).convert("RGBA")
    for name in ("wood", "stone", "flower", "crystal", "gold"):
        img = badge("Plot")
        tile = bed_img.copy()
        tile.alpha_composite(Image.open(os.path.join(BEDS, "skin_" + name + ".png")).convert("RGBA"))
        tile = tile.crop(tile.getbbox())
        k = 210 * SS / tile.width
        tile = tile.resize((int(tile.width * k), int(tile.height * k)), Image.LANCZOS)
        img.alpha_composite(tile, (int(128 * SS - tile.width / 2), int(138 * SS - tile.height / 2)))
        icon_save(img, "pk_" + name)

    def burst(slot, name, sprites, n, rad, size, tints=None, centre=(128, 128), spin=True):
        img = badge(slot)
        for k in range(n):
            a = k / n * 2 * math.pi + rng.uniform(-0.2, 0.2)
            r = rad * rng.uniform(0.35, 1.0)
            sp = sprites[k % len(sprites)]
            tint = tints[k % len(tints)] if tints else None
            place(img, sp, centre[0] + math.cos(a) * r, centre[1] + math.sin(a) * r * 0.9,
                  size * rng.uniform(0.7, 1.15), tint, rng.uniform(0, 360) if spin else 0)
        return img

    # planting
    icon_save(burst("Plant", "x", [STAR], 9, 78, 46, [hexc("#FFF6B0"), hexc("#FFFFFF")]), "pl_stars")
    icon_save(burst("Plant", "x", [P("bubble")], 7, 72, 52, None, spin=False), "pl_bubbles")
    icon_save(burst("Plant", "x", [LEAF], 8, 76, 48), "pl_sprout")
    icon_save(burst("Plant", "x", [STAR], 12, 86, 50,
                    [hexc("#FF6B6B"), hexc("#FFD35C"), hexc("#6BD0FF"), hexc("#B98CFF"), hexc("#7CF08F")]), "pl_firework")
    # watering
    img = badge("Water")
    place(img, P("rainbow"), 128, 112, 200)
    for k in range(5):
        place(img, DROP, 70 + k * 29, 176 + (k % 2) * 10, 30)
    icon_save(img, "wt_rainbow")
    icon_save(burst("Water", "x", [P("bubble")], 8, 76, 50, None, spin=False), "wt_bubbles")
    icon_save(burst("Water", "x", [P("diamond")], 8, 74, 48, [hexc("#CFF1FF"), hexc("#FFFFFF")], spin=False), "wt_diamond")
    icon_save(burst("Water", "x", [PETAL], 9, 80, 46), "wt_flower")
    # harvest
    icon_save(burst("Harvest", "x", [COIN], 8, 74, 56, spin=False), "hv_coins")
    icon_save(burst("Harvest", "x", [P("confetti")], 16, 90, 50,
                    [hexc("#FF6B8B"), hexc("#FFD35C"), hexc("#6BD0FF"), hexc("#7CF08F"), hexc("#B98CFF")]), "hv_confetti")
    icon_save(burst("Harvest", "x", [STAR], 8, 80, 52, [hexc("#FFE27A"), hexc("#FFFFFF")]), "hv_stars")
    # taps
    img = badge("Tap"); place(img, P("ring"), 128, 128, 170, hexc("#FFFFFF")); place(img, P("ring"), 128, 128, 100, hexc("#FFFFFF"), alpha=0.7)
    icon_save(img, "tp_ring")
    icon_save(burst("Tap", "x", [STAR], 6, 60, 60, [hexc("#FFF3A0")]), "tp_star")
    icon_save(burst("Tap", "x", [P("heart")], 6, 62, 56, [hexc("#FF5C8A"), hexc("#FF9EC0")], spin=False), "tp_heart")
    icon_save(burst("Tap", "x", [LEAF], 6, 62, 56), "tp_leaf")
    # swipes: a diagonal trail
    def trail(name, sprite, tints, size, count=9):
        img = badge("Swipe")
        for k in range(count):
            t = k / (count - 1)
            place(img, sprite, 58 + t * 140, 190 - t * 120 + math.sin(t * 6) * 8, size * (0.45 + 0.55 * t),
                  tints[k % len(tints)] if tints else None, rng.uniform(0, 360), alpha=0.35 + 0.65 * t)
        icon_save(img, name)
    trail("sw_stars", STAR, [hexc("#FFF6B0"), hexc("#FFFFFF")], 54)
    trail("sw_rainbow", P("heart"), [hexc("#FF6B6B"), hexc("#FFB14F"), hexc("#FFE45C"), hexc("#7CF08F"), hexc("#6BC4FF"), hexc("#B98CFF")], 44)
    trail("sw_petals", PETAL, None, 48)
    trail("sw_fairy", P("diamond"), [hexc("#FFF3C4"), hexc("#D9F3FF")], 52, 10)
    # notification frames: a mini pill in the skin's colours
    for name, face, edge, ink in (("wood", "#C68B52", "#6E4524", "#FFF3E0"), ("candy", "#FFC4DC", "#E2609A", "#8A1E4E"),
                                  ("night", "#2A3566", "#8FA3FF", "#EAF0FF"), ("gold", "#FFE08A", "#B9831A", "#6B4404")):
        img = badge("Toast")
        d = ImageDraw.Draw(img)
        d.rounded_rectangle([34 * SS, 96 * SS, 222 * SS, 160 * SS], radius=32 * SS, fill=hexc(edge))
        d.rounded_rectangle([40 * SS, 102 * SS, 216 * SS, 154 * SS], radius=26 * SS, fill=hexc(face))
        for k in range(3):
            d.rounded_rectangle([(66 + k * 44) * SS, 122 * SS, (98 + k * 44) * SS, 134 * SS], radius=6 * SS, fill=hexc(ink))
        if name == "night":
            for sx, sy in ((58, 80), (200, 176), (190, 76)):
                place(img, STAR, sx, sy, 34, hexc("#FFF6B0"))
        if name == "gold":
            place(img, STAR, 206, 90, 40, hexc("#FFFFFF"))
        icon_save(img, "ts_" + name)


if __name__ == "__main__":
    groups = sys.argv[1:] or ["beds", "particles", "icons"]
    for g in groups:
        globals()[g]()
