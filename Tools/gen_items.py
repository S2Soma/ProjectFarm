"""Item icons for the shop, and the farm coin — drawn from scratch as one consistent set.

    uv run --with pillow --with numpy python Tools/gen_items.py

Why
---
The shop sold "Bình tưới vàng" under a lime slice, "Phân bón thần kỳ" under an avocado and
"Dự báo thời tiết" under a lemon: Kenney food previews borrowed as placeholders, 74x80 px and
shown at 150 px on a phone. The coin was a Kenney glyph that read as a fidget spinner.

Style
-----
Sticker icons: one warm dark outline around each silhouette, flat two-tone fills lit from the
top left, a white specular accent. Drawn at 1024 px and reduced to 256 px with Lanczos, which
is the anti-aliasing — PIL's ImageDraw does none of its own.
"""
from PIL import Image, ImageDraw, ImageFilter, ImageFont
import numpy as np
import math, os, re, uuid

OUT = "Assets/Resources/Art/items"
META_TEMPLATE = "Assets/Resources/Art/gen/check.png.meta"
FONT = "Tools/fonts/Nunito-Black.ttf"   # the icons keep the face their lettering was drawn in
S = 1024
FINAL = 256
INK = (59, 37, 19)          # the one outline colour
os.makedirs(OUT, exist_ok=True)


def hexc(s):
    s = s.lstrip("#")
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


# ------------------------------------------------------------------ canvas
class Canvas:
    """Premultiplied RGBA in floats, painted with masks."""

    def __init__(self):
        self.p = np.zeros((S, S, 3), np.float32)
        self.a = np.zeros((S, S), np.float32)

    def paint(self, mask, color, alpha=1.0):
        m = np.clip(mask, 0, 1) * alpha
        col = color if isinstance(color, np.ndarray) else np.array(color, np.float32)
        self.p = col * m[..., None] + self.p * (1 - m[..., None])
        self.a = m + self.a * (1 - m)

    def image(self):
        a = np.clip(self.a, 1e-6, 1)
        rgb = np.clip(self.p / a[..., None], 0, 255)
        out = np.dstack([rgb, np.clip(self.a, 0, 1) * 255]).astype(np.uint8)
        return Image.fromarray(out, "RGBA")


def mask(fn):
    """Draw with fn(ImageDraw) into a fresh 'L' layer; returns a float mask."""
    im = Image.new("L", (S, S), 0)
    fn(ImageDraw.Draw(im))
    return np.asarray(im, np.float32) / 255.0


def union(*ms):
    out = np.zeros((S, S), np.float32)
    for m in ms:
        out = np.maximum(out, m)
    return out


def grow(m, px):
    """Dilate a mask by about px pixels, with a round join."""
    im = Image.fromarray((np.clip(m, 0, 1) * 255).astype(np.uint8), "L")
    im = im.filter(ImageFilter.GaussianBlur(px * 0.5))
    a = np.asarray(im, np.float32) / 255.0
    return np.clip((a - 0.02) * 30.0, 0, 1)


def vgrad(top, bottom, y0=0, y1=S):
    t = np.clip((np.arange(S, dtype=np.float32) - y0) / max(1, (y1 - y0)), 0, 1)
    c0, c1 = np.array(hexc(top), np.float32), np.array(hexc(bottom), np.float32)
    col = c0[None, :] * (1 - t[:, None]) + c1[None, :] * t[:, None]
    return np.broadcast_to(col[:, None, :], (S, S, 3)).copy()


def rgrad(inner, outer, cx, cy, r):
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    t = np.clip(np.hypot(xx - cx, yy - cy) / r, 0, 1)[..., None]
    c0, c1 = np.array(hexc(inner), np.float32), np.array(hexc(outer), np.float32)
    return c0 * (1 - t) + c1 * t


def sticker(cv, silhouette, width=26):
    """The outline: the silhouette grown by width, in ink. Paint fills on top afterwards."""
    cv.paint(grow(silhouette, width), INK)


def ellipse(cx, cy, rx, ry=None):
    ry = rx if ry is None else ry
    return mask(lambda d: d.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=255))


def rrect(x0, y0, x1, y1, r):
    return mask(lambda d: d.rounded_rectangle((x0, y0, x1, y1), r, fill=255))


def poly(pts):
    return mask(lambda d: d.polygon(pts, fill=255))


def star4(cx, cy, r, thin=0.22):
    pts = []
    for i in range(8):
        ang = -math.pi / 2 + i * math.pi / 4
        rr = r if i % 2 == 0 else r * thin
        pts.append((cx + math.cos(ang) * rr, cy + math.sin(ang) * rr))
    return poly(pts)


def sparkle(cv, cx, cy, r, color="#FFFFFF"):
    m = star4(cx, cy, r)
    cv.paint(grow(m, 14), INK)
    cv.paint(m, hexc(color))


def save(cv, name, fit=False):
    img = cv.image()
    if fit:
        # centre the drawing in a square with a little air (for off-centre compositions)
        crop = img.crop(img.getbbox())
        side = int(max(crop.width, crop.height) * 1.06)
        sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        sq.alpha_composite(crop, ((side - crop.width) // 2, (side - crop.height) // 2))
        img = sq
    img = img.resize((FINAL, FINAL), Image.LANCZOS)
    path = os.path.join(OUT, name + ".png")
    img.save(path)
    meta = path + ".meta"
    guid = None
    if os.path.exists(meta):
        guid = re.search(r"guid: (\w+)", open(meta).read()).group(1)
    m = open(META_TEMPLATE).read()
    m = re.sub(r"guid: \w+", "guid: " + (guid or uuid.uuid4().hex), m, count=1)
    # Shown from 18 px (the coin in a price pill) to 150 px (a shop card): mipmaps, trilinear.
    m = m.replace("enableMipMap: 0", "enableMipMap: 1")
    m = re.sub(r"filterMode: \d", "filterMode: 2", m, count=1)
    open(meta, "w").write(m)
    print("  ", name)


# ------------------------------------------------------------------ icons
def coin():
    cv = Canvas()
    cx, cy = 512, 500
    rim = ellipse(cx, cy + 34, 404)          # the coin's edge, seen just below its face
    face = ellipse(cx, cy, 404)
    sticker(cv, union(rim, face), 30)
    cv.paint(rim, hexc("#B8660C"))
    cv.paint(face, vgrad("#FFE27A", "#F29A16", 110, 900))
    inner = ellipse(cx, cy, 314)
    cv.paint(grow(inner, 18), hexc("#D98511"), 0.9)          # engraved groove
    cv.paint(inner, vgrad("#F7B52C", "#FFD35A", 190, 820))   # dished: dark top, light bottom

    # embossed sprout: two leaves on a stem, with a gap between them so it survives at 20 px.
    # A light offset copy under the dark one reads as relief.
    def leaf(lx, ly, rx, ry, rot):
        m = ellipse(lx, ly, rx, ry)
        im = Image.fromarray((m * 255).astype(np.uint8)).rotate(rot, center=(lx, ly), resample=Image.BICUBIC)
        return np.asarray(im, np.float32) / 255.0

    def sprout(dx, dy):
        return union(
            rrect(cx - 24 + dx, cy - 30 + dy, cx + 24 + dx, cy + 220 + dy, 24),
            leaf(cx - 118 + dx, cy - 70 + dy, 128, 70, -35),
            leaf(cx + 118 + dx, cy - 110 + dy, 128, 70, 35),
        )
    cv.paint(sprout(7, 11), hexc("#FFF0B0"), 0.9)
    cv.paint(sprout(0, 0), hexc("#C8740E"))

    # specular arc on the upper left of the rim
    arc = mask(lambda d: d.arc((cx - 370, cy - 370, cx + 370, cy + 370), 200, 255, fill=255, width=34))
    cv.paint(arc, (255, 255, 255), 0.85)
    # no sparkle: the coin is mostly shown at 20-56 px, where a star on its rim is noise
    save(cv, "coin")


def watering_can():
    cv = Canvas()
    body = rrect(250, 430, 690, 860, 90)
    lid = ellipse(470, 440, 220, 70)
    spout = poly([(640, 640), (660, 760), (905, 420), (860, 370)])
    rose = ellipse(900, 360, 78, 64)
    # a loop behind the body: the body covers its right half, leaving a "C" at the back
    handle = mask(lambda d: d.ellipse((120, 450, 420, 780), outline=255, width=64))
    sil = union(body, lid, spout, rose, handle)
    sticker(cv, sil, 28)

    gold = vgrad("#FFE27A", "#E48A0E", 300, 880)
    cv.paint(handle, gold)
    cv.paint(spout, vgrad("#FFD65C", "#D9800C", 380, 760))
    cv.paint(body, gold)
    band = rrect(250, 590, 690, 660, 0) * body
    cv.paint(band, hexc("#D27A0B"))
    cv.paint(lid, vgrad("#FFF0A8", "#F2B233", 370, 510))
    cv.paint(rose, vgrad("#FFE27A", "#D9800C", 300, 430))
    for hx, hy in ((880, 340), (915, 372), (890, 395), (925, 335)):
        cv.paint(ellipse(hx, hy, 9), hexc("#9A5A08"))
    # highlight down the body's left
    cv.paint(rrect(300, 470, 350, 800, 25) * body, (255, 255, 255), 0.55)
    for px, py, r in ((960, 520, 34), (905, 600, 28), (980, 660, 24)):
        d = union(ellipse(px, py, r), poly([(px - r * 0.8, py - r * 0.5), (px, py - r * 2.2), (px + r * 0.8, py - r * 0.5)]))
        cv.paint(grow(d, 12), INK)
        cv.paint(d, hexc("#6CCBF5"))
    save(cv, "item_can")


def fertilizer():
    cv = Canvas()
    sack = rrect(230, 380, 794, 910, 150)
    neck = poly([(360, 230), (440, 270), (512, 225), (590, 270), (664, 230), (640, 410), (384, 410)])
    tie = rrect(350, 360, 674, 430, 34)
    sil = union(sack, neck, tie)
    sticker(cv, sil, 28)
    burlap = vgrad("#E8BE7E", "#B07A3E", 230, 910)
    cv.paint(neck, burlap)
    cv.paint(sack, burlap)
    # stitched seam shading on the right
    cv.paint(rrect(640, 420, 794, 900, 140) * sack, hexc("#9A6632"), 0.35)
    cv.paint(tie, vgrad("#C0563A", "#8B3322", 360, 430))
    label = ellipse(500, 660, 170)
    cv.paint(grow(label, 16), INK)
    cv.paint(label, vgrad("#FFF7E2", "#EFDDB6", 490, 830))
    leaf = union(
        mask(lambda d: d.chord((370, 520, 640, 790), 200, 20, fill=255)),
        mask(lambda d: d.chord((360, 530, 630, 800), 20, 200, fill=255)) * 0,
    )
    leaf = mask(lambda d: d.ellipse((400, 540, 600, 780), fill=255))
    leaf = leaf * poly([(500, 520), (640, 660), (500, 800), (360, 660)])
    cv.paint(grow(leaf, 12), INK)
    cv.paint(leaf, vgrad("#7EE08F", "#34A557", 540, 790))
    cv.paint(mask(lambda d: d.line((500, 560, 500, 770), fill=255, width=14)), hexc("#1F7A3A"))
    cv.paint(rrect(290, 470, 340, 800, 25) * sack, (255, 255, 255), 0.35)
    sparkle(cv, 800, 250, 110, "#FFE066")
    sparkle(cv, 190, 300, 64)
    save(cv, "item_fert")


def charm():
    cv = Canvas()
    cx, cy = 512, 600

    def hexagon(r, rot=0.0):
        return poly([(cx + math.cos(rot + i * math.pi / 3) * r, cy + math.sin(rot + i * math.pi / 3) * r) for i in range(6)])

    loop = mask(lambda d: d.ellipse((422, 80, 602, 260), outline=255, width=40))
    bail = rrect(462, 220, 562, 320, 30)
    frame = hexagon(350, math.pi / 6)
    sticker(cv, union(loop, bail, frame), 28)
    cv.paint(loop, vgrad("#FFE27A", "#D9800C", 80, 260))
    cv.paint(bail, vgrad("#FFE27A", "#D9800C", 220, 320))
    cv.paint(frame, vgrad("#FFE27A", "#DB840D", 250, 950))
    gem = hexagon(268, math.pi / 6)
    cv.paint(grow(gem, 10), hexc("#8C4E0A"))
    cv.paint(gem, vgrad("#D9A6FF", "#6A2FC4", 330, 870))
    # facets: a light upper-left wedge and a dark lower-right one
    cv.paint(poly([(cx, cy), (cx - 232, cy - 134), (cx, cy - 268)]) * gem, (255, 255, 255), 0.35)
    cv.paint(poly([(cx, cy), (cx + 232, cy + 134), (cx, cy + 268)]) * gem, hexc("#3E1685"), 0.35)
    inner = hexagon(130, math.pi / 6)
    cv.paint(inner, hexc("#B57BFF"), 0.8)
    cv.paint(poly([(cx - 110, cy - 150), (cx - 40, cy - 190), (cx - 150, cy + 20), (cx - 190, cy - 10)]) * gem,
             (255, 255, 255), 0.75)
    sparkle(cv, 840, 360, 100)
    sparkle(cv, 190, 820, 70, "#E7C8FF")
    save(cv, "item_charm")


def seed_bag():
    cv = Canvas()
    packet = rrect(250, 190, 774, 880, 56)
    sticker(cv, packet, 28)
    cv.paint(packet, vgrad("#F7DDA8", "#D6A862", 190, 880))
    teeth = [(250, 290)]
    for i in range(13):
        x = 250 + i * 40.3
        teeth += [(x + 20, 190), (x + 40, 290)]
    crimp = poly(teeth + [(774, 290), (774, 190), (250, 190)]) * packet
    cv.paint(rrect(250, 190, 774, 300, 56) * packet, hexc("#C8964F"))
    cv.paint(mask(lambda d: d.line((250, 300, 774, 300), fill=255, width=14)), INK)
    window = rrect(320, 380, 704, 760, 70)
    cv.paint(grow(window, 12), INK)
    cv.paint(window, vgrad("#FFFBEF", "#F1E2C2", 380, 760))
    font = ImageFont.truetype(FONT, 330)
    q = mask(lambda d: d.text((512, 566), "?", font=font, anchor="mm", fill=255))
    cv.paint(grow(q, 20), INK)
    cv.paint(q, vgrad("#7BE38F", "#2FA956", 420, 720))
    cv.paint(rrect(290, 330, 330, 840, 20) * packet, (255, 255, 255), 0.35)
    for sx, sy, rot in ((700, 900, 20), (820, 850, -30), (600, 945, 60)):
        s = mask(lambda d: d.ellipse((sx - 42, sy - 28, sx + 42, sy + 28), fill=255))
        s = np.asarray(Image.fromarray((s * 255).astype(np.uint8)).rotate(rot, center=(sx, sy)), np.float32) / 255
        cv.paint(grow(s, 12), INK)
        cv.paint(s, hexc("#9A6232"))
    save(cv, "item_seedbag")


def energy():
    cv = Canvas()
    bulb = ellipse(512, 640, 290)
    neck = rrect(420, 250, 604, 450, 20)
    lip = rrect(386, 214, 638, 290, 36)
    cork = rrect(432, 110, 592, 240, 36)
    sil = union(bulb, neck, lip, cork)
    sticker(cv, sil, 28)
    cv.paint(cork, vgrad("#C98A52", "#8A5328", 110, 240))
    glass = union(bulb, neck)
    cv.paint(glass, vgrad("#F3F8FF", "#CFE0F5", 250, 930))
    wave = mask(lambda d: d.polygon(
        [(0, 590)] + [(x, 590 + 26 * math.sin(x / 70.0)) for x in range(0, S + 1, 16)] + [(S, S), (0, S)], fill=255))
    liquid = ellipse(512, 640, 250) * wave
    cv.paint(liquid, vgrad("#D69BFF", "#6B2FD6", 560, 900))
    for bx, by, r in ((440, 760, 34), (560, 700, 22), (600, 820, 28), (480, 660, 16)):
        cv.paint(ellipse(bx, by, r), (255, 255, 255), 0.7)
    cv.paint(lip, vgrad("#FFFFFF", "#C9D9EE", 214, 290))
    cv.paint(mask(lambda d: d.arc((270, 400, 754, 884), 150, 215, fill=255, width=40)) * bulb, (255, 255, 255), 0.95)
    sparkle(cv, 820, 300, 104, "#FFE066")
    sparkle(cv, 180, 420, 70)
    save(cv, "item_energy")


def forecast():
    cv = Canvas()
    sun = ellipse(610, 370, 190)
    rays = np.zeros((S, S), np.float32)
    for i in range(8):
        ang = i * math.pi / 4 + math.pi / 8
        c, s_ = math.cos(ang), math.sin(ang)
        tip = (610 + c * 330, 370 + s_ * 330)
        l = (610 + c * 215 - s_ * 55, 370 + s_ * 215 + c * 55)
        r = (610 + c * 215 + s_ * 55, 370 + s_ * 215 - c * 55)
        rays = np.maximum(rays, poly([l, tip, r]))
    sticker(cv, union(sun, rays), 26)
    cv.paint(rays, vgrad("#FFD23F", "#F59A16", 40, 700))
    cv.paint(sun, rgrad("#FFF3A0", "#FFA91F", 560, 320, 230))
    cloud = union(ellipse(360, 650, 170), ellipse(560, 580, 210), ellipse(760, 670, 150),
                  rrect(200, 640, 900, 830, 95))
    sticker(cv, cloud, 28)
    cv.paint(cloud, vgrad("#FFFFFF", "#CFE3F4", 400, 830))
    cv.paint(rrect(240, 760, 880, 830, 60) * cloud, hexc("#B9D3EA"), 0.6)
    cv.paint(mask(lambda d: d.arc((420, 400, 700, 680), 200, 290, fill=255, width=34)) * cloud, (255, 255, 255), 1.0)
    save(cv, "item_forecast")


def reroll():
    cv = Canvas()
    paper = rrect(220, 150, 690, 820, 44)
    sticker(cv, paper, 28)
    cv.paint(paper, vgrad("#FFFDF4", "#EBDDBF", 150, 820))
    for i, w in enumerate((330, 270, 300, 200)):
        cv.paint(rrect(290, 280 + i * 110, 290 + w, 316 + i * 110, 18), hexc("#C9B08A"))
    badge = ellipse(690, 700, 230)
    sticker(cv, badge, 26)
    cv.paint(badge, vgrad("#7BE38F", "#2FA956", 470, 930))
    cv.paint(mask(lambda d: d.arc((490, 500, 890, 900), 200, 250, fill=255, width=34)) * badge, (255, 255, 255), 0.45)
    # two chasing arrows
    arrows = union(
        mask(lambda d: d.arc((560, 570, 820, 830), 205, 350, fill=255, width=48)),
        mask(lambda d: d.arc((560, 570, 820, 830), 25, 170, fill=255, width=48)),
        poly([(790, 610), (850, 720), (720, 700)]),
        poly([(590, 790), (530, 680), (660, 700)]),
    )
    cv.paint(grow(arrows, 12), hexc("#1C7439"))
    cv.paint(arrows, (255, 255, 255))
    save(cv, "item_reroll")


def greenhouse():
    cv = Canvas()
    house = poly([(210, 470), (512, 190), (814, 470), (814, 850), (210, 850)])
    base = rrect(170, 820, 854, 910, 40)
    sil = union(house, base)
    sticker(cv, sil, 28)
    cv.paint(house, vgrad("#E3F7FF", "#8ED0EE", 200, 850))
    # a plant inside, seen through the glass
    leaves = union(
        mask(lambda d: d.ellipse((380, 560, 520, 820), fill=255)),
        mask(lambda d: d.ellipse((500, 520, 650, 820), fill=255)),
        mask(lambda d: d.ellipse((440, 620, 590, 850), fill=255)),
    ) * house
    cv.paint(leaves, vgrad("#8EE38A", "#3E9E4E", 520, 850), 0.9)
    bars = union(
        mask(lambda d: d.line((512, 200, 512, 850), fill=255, width=30)),
        mask(lambda d: d.line((210, 600, 814, 600), fill=255, width=30)),
        mask(lambda d: d.line((361, 330, 361, 850), fill=255, width=26)),
        mask(lambda d: d.line((663, 330, 663, 850), fill=255, width=26)),
    ) * house
    cv.paint(bars, hexc("#FFFFFF"))
    edge = grow(house, 1) - np.clip((grow(house, 1) - 0), 0, 1) * 0
    cv.paint(mask(lambda d: d.line([(210, 470), (512, 190), (814, 470)], fill=255, width=40)), hexc("#3FAF5E"))
    for x0 in (250, 560):
        cv.paint(poly([(x0, 820), (x0 + 60, 820), (x0 + 200, 480), (x0 + 140, 480)]) * house, (255, 255, 255), 0.45)
    cv.paint(base, vgrad("#B98A5A", "#7C5230", 820, 910))
    sparkle(cv, 850, 220, 90)
    save(cv, "item_green")


def star5(cx, cy, r, inner=0.48, rot=-math.pi / 2):
    pts = []
    for i in range(10):
        ang = rot + i * math.pi / 5
        rr = r if i % 2 == 0 else r * inner
        pts.append((cx + math.cos(ang) * rr, cy + math.sin(ang) * rr))
    return poly(pts)


def xp_charm():
    """Bùa kinh nghiệm: a blue medallion carrying the XP star, "x2" on a ribbon across it."""
    cv = Canvas()
    disc = ellipse(512, 470, 330)
    ribbon = rrect(150, 640, 874, 820, 50)
    sticker(cv, union(disc, ribbon), 28)
    cv.paint(disc, vgrad("#8FD3FF", "#2F74D0", 140, 800))
    cv.paint(ellipse(512, 470, 270), vgrad("#5DB2F5", "#2360B8", 200, 740))
    cv.paint(mask(lambda d: d.arc((200, 160, 824, 784), 200, 280, fill=255, width=34)), (255, 255, 255), 0.55)
    st = star5(512, 450, 215)
    cv.paint(grow(st, 14), INK)
    cv.paint(st, vgrad("#FFF1A0", "#F2A91E", 240, 660))
    cv.paint(poly([(512, 450), (512, 235), (440, 390)]) * st, (255, 255, 255), 0.45)
    cv.paint(ribbon, vgrad("#FF8A6B", "#D8452C", 640, 820))
    cv.paint(rrect(150, 640, 874, 670, 30) * ribbon, (255, 255, 255), 0.35)
    font = ImageFont.truetype(FONT, 170)
    t = mask(lambda d: d.text((512, 728), "x2", font=font, anchor="mm", fill=255))
    cv.paint(grow(t, 18), hexc("#7A1E10"))
    cv.paint(t, (255, 255, 255))
    sparkle(cv, 860, 180, 96)
    sparkle(cv, 170, 230, 64, "#FFF3A0")
    save(cv, "item_xpcharm")


def tonic():
    """Thuốc lớn nhanh: a green growth potion, a sprout where the cork would be, speed marks."""
    cv = Canvas()
    bulb = ellipse(540, 650, 270)
    neck = rrect(460, 300, 620, 460, 20)
    lip = rrect(430, 262, 650, 330, 32)
    sil = union(bulb, neck, lip)
    sticker(cv, sil, 28)
    glass = union(bulb, neck)
    cv.paint(glass, vgrad("#F2FFF4", "#CDEBD6", 300, 920))
    wave = mask(lambda d: d.polygon(
        [(0, 560)] + [(x, 560 + 24 * math.sin(x / 64.0)) for x in range(0, S + 1, 16)] + [(S, S), (0, S)], fill=255))
    liquid = ellipse(540, 650, 232) * wave
    cv.paint(liquid, vgrad("#9BF07A", "#23A24A", 540, 880))
    for bx, by, r in ((470, 760, 30), (590, 690, 20), (620, 800, 26)):
        cv.paint(ellipse(bx, by, r), (255, 255, 255), 0.7)
    cv.paint(lip, vgrad("#FFFFFF", "#C4DDCB", 262, 330))
    # the sprout
    stem = mask(lambda d: d.line((540, 270, 540, 150), fill=255, width=30))
    l1 = mask(lambda d: d.ellipse((380, 90, 540, 190), fill=255))
    l2 = mask(lambda d: d.ellipse((540, 40, 720, 160), fill=255))
    sprout = union(stem, l1, l2)
    cv.paint(grow(sprout, 16), INK)
    cv.paint(sprout, vgrad("#8EE38A", "#2F9A48", 40, 270))
    cv.paint(mask(lambda d: d.arc((290, 400, 790, 900), 150, 215, fill=255, width=36)) * bulb, (255, 255, 255), 0.95)
    # speed marks on the left
    for y, x0, w in ((560, 90, 150), (660, 60, 170), (760, 100, 130)):
        m = rrect(x0, y - 18, x0 + w, y + 18, 18)
        cv.paint(grow(m, 12), INK)
        cv.paint(m, hexc("#FFE066"))
    sparkle(cv, 860, 330, 90)
    save(cv, "item_tonic")


def seed_bag_gold():
    """Túi hạt quý: the seed packet in gold, a star in the window instead of the question mark."""
    cv = Canvas()
    packet = rrect(250, 190, 774, 880, 56)
    sticker(cv, packet, 28)
    cv.paint(packet, vgrad("#FFE58A", "#E0A321", 190, 880))
    cv.paint(rrect(250, 190, 774, 300, 56) * packet, hexc("#C98A12"))
    cv.paint(mask(lambda d: d.line((250, 300, 774, 300), fill=255, width=14)), INK)
    window = rrect(320, 380, 704, 760, 70)
    cv.paint(grow(window, 12), INK)
    cv.paint(window, vgrad("#FFFBEF", "#F6E6BE", 380, 760))
    st = star5(512, 580, 150)
    cv.paint(grow(st, 16), INK)
    cv.paint(st, vgrad("#7BE38F", "#2FA956", 430, 730))
    cv.paint(rrect(290, 330, 330, 840, 20) * packet, (255, 255, 255), 0.45)
    for sx, sy, rot in ((700, 900, 20), (820, 850, -30), (600, 945, 60)):
        sd = mask(lambda d: d.ellipse((sx - 42, sy - 28, sx + 42, sy + 28), fill=255))
        sd = np.asarray(Image.fromarray((sd * 255).astype(np.uint8)).rotate(rot, center=(sx, sy)), np.float32) / 255
        cv.paint(grow(sd, 12), INK)
        cv.paint(sd, hexc("#9A6232"))
    sparkle(cv, 820, 200, 100, "#FFF3A0")
    sparkle(cv, 200, 150, 60)
    save(cv, "item_seedgold")


def pet_egg():
    """Trứng thú cưng: an upright cream egg with pastel spots and a small crack at the crown.

    The silhouette is an analytic egg — an ellipse whose half-width grows toward the bottom —
    rather than a union of ellipses: the old union read as a lopsided, dented blob at 60 px."""
    cv = Canvas()
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    cx, cy, a, b = 512.0, 540.0, 262.0, 372.0
    v = (yy - cy) / b                                   # -1 at the crown, +1 at the base
    half = a * (1.0 + 0.14 * v)                         # narrower top, fuller bottom
    d = ((xx - cx) / np.maximum(half, 1.0)) ** 2 + v ** 2
    egg = np.clip((1.0 - d) * 90.0, 0, 1)               # ~1 px soft edge at this scale
    sticker(cv, egg, 30)
    cv.paint(egg, rgrad("#FFFDF6", "#EFD29E", 440, 400, 560))
    # a soft shade down the right flank so it reads as round, not flat
    shade = np.clip(((xx - cx) / np.maximum(half, 1.0) - 0.25) * 1.6, 0, 1) * egg
    cv.paint(shade, hexc("#E2BD83"), 0.45)
    for sx, sy, r, col in ((430, 330, 50, "#FFB3C7"), (620, 470, 70, "#9FD8FF"), (400, 640, 62, "#B9F0A6"),
                           (600, 760, 54, "#FFD36E"), (540, 250, 30, "#C9B3FF")):
        cv.paint(ellipse(sx, sy, r, r * 0.9) * egg, hexc(col), 0.92)
    crack = mask(lambda dr: dr.line([(452, 196), (486, 250), (462, 292), (512, 342), (494, 380)], fill=255, width=15))
    cv.paint(crack * egg, INK, 0.85)
    # the gloss: a long highlight on the upper-left shoulder
    gloss = mask(lambda dr: dr.arc((312, 210, 700, 880), 196, 244, fill=255, width=36)) * egg
    cv.paint(gloss, (255, 255, 255), 0.85)
    sparkle(cv, 850, 240, 96, "#FFF3A0")
    sparkle(cv, 170, 360, 60)
    sparkle(cv, 840, 800, 52)
    save(cv, "item_egg")


def harvest_hand():
    """The harvest action: a hand pulling a carrot out by its leaves. Drawn for the HUD's
    Thu hoạch pill, where it has to read at 30 px — so one fist, one carrot, nothing else."""
    cv = Canvas()

    def rot(m, deg, cx, cy):
        im = Image.fromarray((np.clip(m, 0, 1) * 255).astype(np.uint8)).rotate(deg, center=(cx, cy), resample=Image.BICUBIC)
        return np.asarray(im, np.float32) / 255.0

    # carrot, pointing down-left, drawn first so the fist closes over its shoulder
    carrot = poly([(430, 470), (640, 520), (300, 950)])
    carrot = np.maximum(carrot, ellipse(535, 495, 108, 70))
    carrot = rot(carrot, 8, 520, 600)
    leaves = union(
        rot(ellipse(600, 250, 56, 150), -25, 600, 330),
        rot(ellipse(700, 290, 50, 130), -55, 680, 360),
        rot(ellipse(520, 230, 46, 120), 5, 540, 320),
    )
    sticker(cv, union(carrot, leaves), 26)
    cv.paint(leaves, vgrad("#8EE38A", "#2F9A48", 100, 420))
    cv.paint(carrot, vgrad("#FFB14A", "#E0620F", 440, 950))
    for y in (620, 720, 820):
        cv.paint(mask(lambda d: d.line((340 + (y - 600) * 0.1, y, 480 - (y - 600) * 0.25, y + 16), fill=255, width=14)) * carrot,
                 hexc("#B5480A"), 0.55)

    # fist gripping the top of the carrot
    palm = rot(rrect(380, 300, 760, 520, 100), -12, 570, 410)
    thumb = rot(ellipse(470, 470, 120, 62), -30, 470, 470)
    cuff = rot(rrect(700, 330, 900, 520, 50), -12, 800, 425)
    sticker(cv, union(palm, thumb, cuff), 26)
    cv.paint(cuff, vgrad("#6CC6F5", "#2B8BD3", 330, 520))
    skin = vgrad("#FFDDB8", "#E9A676", 280, 540)
    cv.paint(palm, skin)
    for k in range(3):
        x = 470 + k * 85
        knuckle = rot(mask(lambda d: d.line((x, 330, x - 18, 500), fill=255, width=12)), -12, 570, 410) * palm
        cv.paint(knuckle, hexc("#C27D52"), 0.7)
    cv.paint(grow(thumb, 10), INK)
    cv.paint(thumb, vgrad("#FFE3C4", "#EFB283", 410, 530))
    cv.paint(rot(rrect(420, 320, 700, 350, 15), -12, 570, 410) * palm, (255, 255, 255), 0.45)
    save(cv, "icon_harvest", fit=True)


def frame(kind):
    """A portrait frame: a thick ring, lit from the top, with an ornament at the top and two at the
    sides. Drawn to the same box as the HUD's avatar ring (the portrait shows through the middle)."""
    top, bot, groove, at, ab = FRAMES[kind]
    cv = Canvas()
    c = 512
    outer, inner = 470, 372
    ring = np.clip(ellipse(c, c, outer) - ellipse(c, c, inner), 0, 1)
    sticker(cv, ring, 22)
    cv.paint(ring, vgrad(top, bot, 60, 980))
    mid = np.clip(ellipse(c, c, 432) - ellipse(c, c, 410), 0, 1)
    cv.paint(mid, hexc(groove), 0.55)
    cv.paint(mask(lambda d: d.arc((c - 452, c - 452, c + 452, c + 452), 200, 300, fill=255, width=26)), (255, 255, 255), 0.75)

    def ornament(cx, cy, s):
        if kind == "gold":        # a sprig of two leaves and a wheat ear
            for ang, dx in ((-35, -0.55), (35, 0.55)):
                leaf_m = ellipse(cx + dx * 90 * s, cy + 10 * s, 70 * s, 32 * s)
                im = Image.fromarray((leaf_m * 255).astype(np.uint8)).rotate(ang, center=(cx + dx * 90 * s, cy + 10 * s), resample=Image.BICUBIC)
                lm = np.asarray(im, np.float32) / 255.0
                cv.paint(grow(lm, 12), INK)
                cv.paint(lm, vgrad(at, ab, cy - 40 * s, cy + 50 * s))
            ear = union(*[ellipse(cx + (12 if i % 2 else -12) * s, cy - 20 * s - i * 26 * s, 20 * s, 15 * s) for i in range(4)])
            cv.paint(grow(ear, 10), INK)
            cv.paint(ear, vgrad("#FFF0A0", "#E0A020", cy - 110 * s, cy - 10 * s))
        else:                      # a cut gem in a setting
            setting = ellipse(cx, cy, 64 * s)
            cv.paint(grow(setting, 12), INK)
            cv.paint(setting, vgrad(top, bot, cy - 64 * s, cy + 64 * s))
            gem = poly([(cx, cy - 46 * s), (cx + 42 * s, cy), (cx, cy + 46 * s), (cx - 42 * s, cy)])
            cv.paint(grow(gem, 8), INK)
            cv.paint(gem, vgrad(at, ab, cy - 46 * s, cy + 46 * s))
            cv.paint(poly([(cx, cy - 46 * s), (cx + 42 * s, cy), (cx, cy)]) * gem, (255, 255, 255), 0.45)

    ornament(c, c - 430, 1.0)
    ornament(c - 430, c, 0.7)
    ornament(c + 430, c, 0.7)
    save(cv, "frame_" + kind)


def badge_farmer():
    """Huy hiệu Nhà Nông: a green shield with a golden wheat ear."""
    cv = Canvas()
    shield = poly([(512, 110), (860, 220), (840, 560), (512, 920), (184, 560), (164, 220)])
    shield = np.clip(np.asarray(Image.fromarray((shield * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(10)), np.float32) / 255 * 1.6, 0, 1)
    sticker(cv, shield, 28)
    cv.paint(shield, vgrad("#6BDB85", "#1F7A3A", 110, 920))
    inner = poly([(512, 190), (780, 275), (765, 540), (512, 830), (259, 540), (244, 275)])
    cv.paint(grow(inner, 14), hexc("#FFE27A"), 0.9)
    cv.paint(inner, vgrad("#4FC46A", "#257F42", 190, 830))
    stem = mask(lambda d: d.line((512, 760, 512, 330), fill=255, width=26))
    grains = union(*[ellipse(512 + (48 if i % 2 else -48), 660 - i * 62, 46, 30) for i in range(6)], ellipse(512, 300, 30, 44))
    cv.paint(grow(union(stem, grains), 12), INK)
    cv.paint(stem, hexc("#E0A020"))
    cv.paint(grains, vgrad("#FFF3A8", "#E8A424", 280, 700))
    cv.paint(mask(lambda d: d.arc((190, 130, 834, 900), 205, 260, fill=255, width=30)), (255, 255, 255), 0.6)
    save(cv, "badge_farmer")


def badge_star():
    """Huy hiệu Ngôi Sao: a round blue medal with a gold star."""
    cv = Canvas()
    disc = ellipse(512, 512, 400)
    sticker(cv, disc, 28)
    cv.paint(disc, vgrad("#7FD0FF", "#2A6FC4", 110, 910))
    ring = np.clip(ellipse(512, 512, 330) - ellipse(512, 512, 300), 0, 1)
    cv.paint(ring, hexc("#FFE27A"), 0.9)
    pts = [(512 + math.cos(-math.pi / 2 + i * math.pi / 5) * (250 if i % 2 == 0 else 104),
            512 + math.sin(-math.pi / 2 + i * math.pi / 5) * (250 if i % 2 == 0 else 104)) for i in range(10)]
    star = poly(pts)
    cv.paint(grow(star, 14), INK)
    cv.paint(star, vgrad("#FFF3A8", "#F29A16", 260, 760))
    cv.paint(poly([pts[0], pts[1], (512, 512), pts[9]]) * star, (255, 255, 255), 0.35)
    cv.paint(mask(lambda d: d.arc((140, 140, 884, 884), 200, 255, fill=255, width=30)), (255, 255, 255), 0.7)
    save(cv, "badge_star")


def leaf_shape(cx, cy, length, width, ang):
    """A pointed leaf: a lens between two arcs, rotated. (Ellipses read as capsules.)"""
    def bez(p0, p1, p2, n=30):
        return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
                 (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]) for t in (i / n for i in range(n + 1))]
    a, b = (cx - length, cy), (cx + length, cy)
    pts = bez(a, (cx, cy - width * 2), b) + bez(b, (cx, cy + width * 2), a)
    m = poly(pts)
    im = Image.fromarray((m * 255).astype(np.uint8)).rotate(ang, center=(cx, cy), resample=Image.BICUBIC)
    return np.asarray(im, np.float32) / 255.0


def fx_leaves():
    """Shop icon for the harvest trail, and the particle itself (particle_leaf)."""
    cv = Canvas()
    for cx, cy, ang, col in ((380, 600, 30, ("#9BE37A", "#3E9E4E")), (620, 430, -25, ("#C8E860", "#6AA630")), (560, 760, 70, ("#7ED67A", "#2F8A46"))):
        lm = leaf_shape(cx, cy, 190, 90, ang)
        cv.paint(grow(lm, 22), INK)
        cv.paint(lm, vgrad(col[0], col[1], cy - 190, cy + 190))
        vein = mask(lambda d: d.line((cx - 150 * math.cos(math.radians(ang)), cy + 150 * math.sin(math.radians(ang)),
                                      cx + 150 * math.cos(math.radians(ang)), cy - 150 * math.sin(math.radians(ang))), fill=255, width=14))
        cv.paint(vein * lm, (255, 255, 255), 0.45)
    for sx, sy, r in ((800, 250, 60), (220, 300, 44), (820, 760, 40)):
        pts = [(sx + math.cos(-math.pi / 2 + i * math.pi / 4) * (r if i % 2 == 0 else r * 0.25),
                sy + math.sin(-math.pi / 2 + i * math.pi / 4) * (r if i % 2 == 0 else r * 0.25)) for i in range(8)]
        cv.paint(poly(pts), (255, 255, 255), 0.95)
    save(cv, "fx_leaves")

    p = Canvas()
    lm = leaf_shape(512, 512, 380, 170, 35)
    p.paint(grow(lm, 26), INK)
    p.paint(lm, vgrad("#A8E888", "#3E9E4E", 150, 870))
    save_small(p, "particle_leaf", 64)


def fx_petals():
    cv = Canvas()
    for cx, cy, ang, col in ((380, 560, 20, ("#FFD0E0", "#F07AA0")), (640, 420, -40, ("#FFE3EC", "#F59AB8")), (600, 740, 80, ("#FFC0D4", "#E0608C"))):
        pm = leaf_shape(cx, cy, 150, 110, ang)
        cv.paint(grow(pm, 22), INK)
        cv.paint(pm, vgrad(col[0], col[1], cy - 150, cy + 150))
        cv.paint(leaf_shape(cx - 30, cy - 30, 50, 24, ang) , (255, 255, 255), 0.5)
    save(cv, "fx_petals")

    p = Canvas()
    pm = leaf_shape(512, 512, 300, 220, 20)
    p.paint(grow(pm, 26), INK)
    p.paint(pm, vgrad("#FFE3EC", "#F07AA0", 200, 820))
    save_small(p, "particle_petal", 64)


def save_small(cv, name, size):
    img = cv.image().resize((size, size), Image.LANCZOS)
    img.save(os.path.join(OUT, name + ".png"))
    print("  ", name)


MEDALS = {
    # disc top, disc bottom, groove, emblem, ribbon top, ribbon bottom
    "bronze":  ("#F2B98A", "#A8612E", "#8E4E22", "#7A401A", "#E2574C", "#9E2F27"),
    "silver":  ("#FFFFFF", "#A9B6C2", "#8A98A6", "#6E7D8C", "#4E9BE0", "#2A62A6"),
    "gold":    ("#FFE27A", "#E48A0E", "#C8740E", "#B0620A", "#5CC46F", "#2F8F4C"),
    "diamond": ("#D8F6FF", "#4FB6E0", "#2E8EBF", "#1E6E9C", "#9A6BE0", "#5E36A8"),
}


def medal(tier):
    """Contract grade: a medal on a V-cut ribbon. Replaces a coloured text pill, which read as
    one more button in rows that already had one."""
    top, bot, groove, emblem, rt, rb = MEDALS[tier]
    cv = Canvas()
    left = poly([(330, 520), (470, 560), (420, 960), (350, 890), (270, 930)])
    right = poly([(694, 520), (554, 560), (604, 960), (674, 890), (754, 930)])
    disc = ellipse(512, 430, 330)
    sticker(cv, union(left, right, disc), 28)
    cv.paint(left, vgrad(rt, rb, 520, 960))
    cv.paint(right, vgrad(rt, rb, 520, 960))
    cv.paint(poly([(360, 560), (400, 570), (380, 900), (350, 880)]) * left, (255, 255, 255), 0.25)
    cv.paint(disc, vgrad(top, bot, 100, 760))
    inner = ellipse(512, 430, 250)
    cv.paint(grow(inner, 16), hexc(groove), 0.9)
    cv.paint(inner, vgrad(bot, top, 180, 680))
    if tier == "diamond":
        gem = poly([(512, 250), (650, 380), (512, 620), (374, 380)])
        cv.paint(grow(gem, 12), hexc(emblem))
        cv.paint(gem, vgrad("#FFFFFF", "#7FD6F5", 250, 620))
        cv.paint(poly([(512, 250), (650, 380), (512, 380)]) * gem, hexc("#BFEFFF"), 0.8)
        cv.paint(poly([(374, 380), (512, 380), (512, 620)]) * gem, hexc("#5AB8E0"), 0.7)
    else:
        star = poly([(512 + math.cos(-math.pi / 2 + i * math.pi / 5) * (170 if i % 2 == 0 else 72),
                      430 + math.sin(-math.pi / 2 + i * math.pi / 5) * (170 if i % 2 == 0 else 72)) for i in range(10)])
        cv.paint(np.roll(star, 8, axis=0), (255, 255, 255), 0.55)
        cv.paint(star, hexc(emblem))
    cv.paint(mask(lambda d: d.arc((212, 130, 812, 730), 200, 255, fill=255, width=30)), (255, 255, 255), 0.8)
    save(cv, "medal_" + tier)


CHESTS = [
    # body top, body bottom, band top, band bottom, dark, lock
    ("#C98A52", "#8A5328", "#A9B4BE", "#6E7A86", "#5A3418", "#D9DEE3"),
    ("#B3713E", "#7A4420", "#FFE27A", "#E0900E", "#4A2810", "#FFE27A"),
    ("#A477E6", "#5A2FA8", "#FFE27A", "#E0900E", "#2E1266", "#7FE3FF"),
    ("#FFD35A", "#E08A0E", "#FF7D70", "#C23A2E", "#7A3A08", "#FF5A7A"),
]


def chest(tier, name=None):
    """One chest per tier. The four tiers used to share a single gold box tinted 34% toward a
    colour, which made the difference between common and legendary a hue shift."""
    btop, bbot, ntop, nbot, dark, lockc = CHESTS[tier]
    cv = Canvas()
    base = rrect(190, 470, 834, 880, 50)
    lid = union(rrect(170, 300, 854, 500, 40), ellipse(512, 330, 342, 120) * rrect(0, 180, S, 400, 0))
    sticker(cv, union(base, lid), 28)
    cv.paint(base, vgrad(btop, bbot, 470, 880))
    # planks
    for y in (600, 740):
        cv.paint(rrect(190, y, 834, y + 12, 0) * base, hexc(dark), 0.35)
    cv.paint(lid, vgrad(btop, bbot, 210, 500))
    cv.paint(rrect(170, 480, 854, 520, 0), hexc(dark), 0.9)
    for x in (270, 710):
        band = union(rrect(x, 250, x + 64, 880, 14))
        cv.paint(band * union(base, lid), vgrad(ntop, nbot, 250, 880))
    cv.paint(rrect(190, 820, 834, 880, 40) * base, vgrad(ntop, nbot, 820, 880))
    plate = rrect(432, 440, 592, 640, 34)
    cv.paint(grow(plate, 14), INK)
    cv.paint(plate, vgrad(ntop, nbot, 440, 640))
    if tier >= 2:
        gem = poly([(512, 470), (560, 530), (512, 600), (464, 530)])
        cv.paint(grow(gem, 10), INK)
        cv.paint(gem, vgrad("#FFFFFF", lockc, 470, 600))
    else:
        hole = union(ellipse(512, 520, 26), poly([(496, 530), (528, 530), (540, 600), (484, 600)]))
        cv.paint(hole, hexc(dark))
    cv.paint(rrect(210, 330, 800, 360, 15) * lid, (255, 255, 255), 0.4)
    if tier >= 1:
        sparkle(cv, 860, 250, 90 if tier < 3 else 120, "#FFFFFF" if tier < 3 else "#FFF3A0")
    if tier >= 2:
        sparkle(cv, 160, 420, 70, "#FFFFFF")
    if tier == 3:
        sparkle(cv, 820, 900, 60, "#FFF3A0")
    save(cv, name or "chest_%d" % tier)


def chest_item():
    """The shop's Rương quý: the tier-1 chest under the item_ key the shelf looks up."""
    chest(1, "item_chest")


if __name__ == "__main__":
    import sys
    print("items ->", OUT)
    if len(sys.argv) > 1:
        # regenerate only the named icons: python Tools/gen_items.py pet_egg
        for name in sys.argv[1:]:
            globals()[name]()
        sys.exit(0)
    coin()
    watering_can()
    fertilizer()
    charm()
    seed_bag()
    energy()
    forecast()
    reroll()
    greenhouse()
    xp_charm()
    tonic()
    seed_bag_gold()
    chest_item()
    pet_egg()
    harvest_hand()
    for k in FRAMES:
        frame(k)
    badge_farmer()
    badge_star()
    fx_leaves()
    fx_petals()
    for t in MEDALS:
        medal(t)
    for t in range(4):
        chest(t)
