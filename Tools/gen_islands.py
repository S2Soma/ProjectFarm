"""The six floating islands, one style per island, at a resolution that stays sharp.

    uv run --with pillow --with numpy python Tools/gen_islands.py

Why this replaces Art/chrome/island.png
---------------------------------------
That sprite was 660x480 and drawn at 1144x832 world units — about 2.6x upscaled on a
1080p phone at farm zoom, so every edge was soft, and with no mipmaps it shimmered and
broke up when the map zoomed out. It was also one sprite for all six islands, told apart
only by a colour multiplier.

These are 1600x1164 (same 1.375 aspect, same top-surface placement, so the field, fences,
bridges and props all still land on the grass), imported with mipmaps, and each island has
its own palette, outline, cliff and details, named after what it is:

  0 Vườn Nhà  lush meadow, earth cliff, wildflowers
  1 Đảo Gió   pale windswept grass, sandstone cliff carved into horizontal grooves
  2 Đảo Băng  snowfield, blue slate cliff, icicles hanging off the lip, ice crystals
  3 Đảo Hoả   ash and scorched earth, basalt cliff split by glowing lava veins
  4 Đảo Lôi   storm-teal grass, violet slate cliff studded with charged crystals
  5 Đảo Vàng  golden wheat-grass, warm sandstone cliff seamed with gold

Also writes island_N_snow.png: the snow cover for snowy hours, cut from each island's own
top surface so snow follows its outline.
"""
from PIL import Image, ImageDraw, ImageFilter
import numpy as np, math, random, os

OUT = "Assets/Resources/Art/islands"
os.makedirs(OUT, exist_ok=True)

W, H = 1600, 1164
CX, CY = W * 0.5, H * 0.3875          # the field sits centred here (matches the old sprite)
RX, RY = W * 0.455, H * 0.372
DEPTH = H * 0.17                        # keeps the deepest lump inside the canvas


def hexc(s):
    s = s.lstrip("#")
    return np.array([int(s[i:i + 2], 16) for i in (0, 2, 4)], np.float32)


def noise(shape, scale, seed):
    rng = np.random.RandomState(seed)
    gh, gw = int(shape[0] / scale) + 3, int(shape[1] / scale) + 3
    g = (rng.rand(gh, gw) * 255).astype(np.uint8)
    return np.asarray(Image.fromarray(g).resize((shape[1], shape[0]), Image.BICUBIC), np.float32) / 255.0


def mix(a, b, t):
    t = np.clip(t, 0, 1)[..., None]
    return a * (1 - t) + b * t


THEMES = [
    dict(key="home", seed=11,
         top_hi="#A6DC6E", top_lo="#5EAE46", alt="#86C95A", tuft_d="#4A9638", tuft_l="#C4EC92",
         cliff=["#8E5D36", "#7A4D2C", "#9C6C44"], cliff_d="#452A18", lip="#6FBE4E"),
    dict(key="wind", seed=23,
         top_hi="#D3EBAE", top_lo="#95C47E", alt="#BBDD96", tuft_d="#7FAE68", tuft_l="#F0FAD8",
         cliff=["#DCC392", "#C9AC77", "#E8D3A6"], cliff_d="#8A7050", lip="#A8D08E"),
    dict(key="ice", seed=37,
         top_hi="#F7FBFF", top_lo="#C7DAEC", alt="#E1ECF7", tuft_d="#A7C1DA", tuft_l="#FFFFFF",
         cliff=["#7F9EBA", "#6B8AA7", "#94B2CB"], cliff_d="#35506A", lip="#E6F0FA"),
    dict(key="fire", seed=41,
         top_hi="#8A7358", top_lo="#54453A", alt="#9A6444", tuft_d="#3D332C", tuft_l="#B39472",
         cliff=["#3C3438", "#2E282C", "#4A3F43"], cliff_d="#141015", lip="#6E5B48"),
    dict(key="storm", seed=53,
         top_hi="#94C7BD", top_lo="#4E8784", alt="#73AEAA", tuft_d="#3C6E70", tuft_l="#C4EDE4",
         cliff=["#625D80", "#524D6E", "#716C90"], cliff_d="#282540", lip="#7CB8AE"),
    dict(key="gold", seed=67,
         top_hi="#F6DC84", top_lo="#D2A644", alt="#E8C462", tuft_d="#B7872C", tuft_l="#FFF3C0",
         cliff=["#DDA86A", "#C79354", "#EABB80"], cliff_d="#86592E", lip="#E2BA56"),
]


def build(index, th):
    rng = random.Random(th["seed"])
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    dx = (xs - CX) / RX
    dy = (ys - CY) / RY
    rho = np.sqrt(dx * dx + dy * dy)
    ang = np.arctan2(dy, dx)

    # an outline of its own: low harmonics only, small enough that the field always fits
    p = [rng.uniform(0, 6.28) for _ in range(3)]
    m = 1 + 0.020 * np.sin(3 * ang + p[0]) + 0.013 * np.sin(5 * ang + p[1]) + 0.007 * np.sin(8 * ang + p[2])
    top_a = np.clip((m - rho) * min(RX, RY) + 0.5, 0, 1)
    top = top_a > 0.5

    # ---- front edge of the top, per column ----
    rows = np.arange(H)[:, None]
    has = top.any(0)
    y_edge = np.where(has, (top * rows).max(0), 0).astype(np.float32)

    # ---- cliff ----
    cols = (np.arange(W) - CX) / RX
    side = np.clip(np.abs(cols) / 1.0, 0, 1)
    n1 = np.asarray(Image.fromarray((np.random.RandomState(th["seed"] + 1).rand(1, W // 40 + 3) * 255).astype(np.uint8))
                    .resize((W, 1), Image.BICUBIC), np.float32)[0] / 255.0
    n2 = np.asarray(Image.fromarray((np.random.RandomState(th["seed"] + 2).rand(1, W // 22 + 3) * 255).astype(np.uint8))
                    .resize((W, 1), Image.BICUBIC), np.float32)[0] / 255.0
    depth = DEPTH * np.power(np.clip(1 - side * side, 0, 1), 0.55) * (0.80 + 0.26 * n1) + (n2 - 0.5) * 16
    # a few rounded lumps hanging under the island — chunky rock, not a comb of spikes
    xs1 = np.arange(W, dtype=np.float32)
    for _ in range(7):
        bx = rng.uniform(CX - RX * 0.75, CX + RX * 0.75)
        bw = rng.uniform(50, 130)
        bh = rng.uniform(18, 44) * (1 - abs(bx - CX) / RX)
        depth = depth + bh * np.exp(-((xs1 - bx) / bw) ** 2)
    y_bot = np.minimum(y_edge + np.maximum(depth, 0), H - 12)   # never cut flat by the canvas edge
    cliff_a = np.clip(np.minimum(rows - (y_edge - 2), y_bot - rows) + 0.5, 0, 1) * has
    cliff_a = cliff_a * (1 - top_a)

    v = np.clip((rows - y_edge) / np.maximum(y_bot - y_edge, 1), 0, 1)
    nl = noise((H, W), 60, th["seed"] + 3)
    nm = noise((H, W), 14, th["seed"] + 4)
    nf = noise((H, W), 3, th["seed"] + 5)

    c = [hexc(h) for h in th["cliff"]]
    s = v * 4.6 + (nl - 0.5) * 0.9 + np.sin(xs / 61.0 + th["seed"]) * 0.10
    band = np.floor(s).astype(int) % 3
    frac = s - np.floor(s)
    cliff = np.where((band == 0)[..., None], c[0], np.where((band == 1)[..., None], c[1], c[2]))
    nxt = np.where((band == 0)[..., None], c[1], np.where((band == 1)[..., None], c[2], c[0]))
    cliff = mix(cliff, nxt, np.clip((frac - 0.86) / 0.14, 0, 1))
    # a light edge on top of every stratum, the thing that reads as "layered rock"
    cliff = mix(cliff, cliff * 1.22, np.clip(1 - np.abs(frac - 0.04) / 0.05, 0, 1) * 0.6)
    cliff = cliff * (0.95 + 0.10 * nm[..., None]) * (0.97 + 0.05 * nf[..., None])
    cliff = cliff * (1 - 0.42 * np.power(np.abs(cols), 3)[None, :, None])        # rounded sides
    cliff = mix(cliff, hexc(th["cliff_d"]), np.clip((0.16 - v) / 0.16, 0, 1) * 0.75)   # under the lip
    cliff = mix(cliff, hexc(th["cliff_d"]), np.clip((v - 0.70) / 0.30, 0, 1) * 0.55)   # underside

    # ---- top surface ----
    g = np.clip((ys - (CY - RY)) / (2 * RY), 0, 1)
    topc = mix(hexc(th["top_hi"]), hexc(th["top_lo"]), np.power(g, 1.15))
    topc = mix(topc, hexc(th["alt"]), (noise((H, W), 170, th["seed"] + 6) - 0.40) * 0.55)
    topc = topc * (0.965 + 0.07 * noise((H, W), 48, th["seed"] + 7)[..., None])
    topc = topc * (0.985 + 0.03 * nf[..., None])
    edge = np.clip((m - rho) / 0.045, 0, 1)
    back = dy < 0.15
    topc = np.where((back & (edge < 1))[..., None], mix(topc, topc * 1.13 + 10, (1 - edge) * 0.9), topc)
    topc = np.where(((~back) & (edge < 1))[..., None], mix(topc, topc * 0.78, (1 - edge) * 0.8), topc)

    rgb = cliff * cliff_a[..., None]
    alpha = cliff_a.copy()
    rgb = rgb * (1 - top_a[..., None]) + topc * top_a[..., None]
    alpha = np.maximum(alpha, top_a)

    base = Image.fromarray(np.dstack([np.clip(rgb, 0, 255), np.clip(alpha * 255, 0, 255)]).astype(np.uint8), "RGBA")
    # Details go on their OWN layer and are alpha-composited at the end. ImageDraw writes RGBA
    # straight into the pixel, so a 50%-opaque stroke drawn on the island itself punched a
    # 50%-transparent hole in it — every grass tuft showed the sea through.
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    def on_top(x, y, pad=0.02):
        ddx, ddy = (x - CX) / RX, (y - CY) / RY
        a = math.atan2(ddy, ddx)
        mm = 1 + 0.020 * math.sin(3 * a + p[0]) + 0.013 * math.sin(5 * a + p[1]) + 0.007 * math.sin(8 * a + p[2])
        return math.hypot(ddx, ddy) < mm - pad

    def on_cliff(x, y):
        xi = int(x)
        return 0 <= xi < W and has[xi] and y_edge[xi] + 10 < y < y_bot[xi] - 8

    def rgba(h, a=255):
        c = hexc(h); return (int(c[0]), int(c[1]), int(c[2]), a)

    # ---- grass fringe hanging over the cliff lip ----
    lip = rgba(th["lip"])
    x = int(CX - RX * 0.97)
    while x < CX + RX * 0.97:
        if has[x] and rng.random() < 0.55:
            w_ = rng.uniform(7, 16)
            L = rng.uniform(8, 22)
            ye = y_edge[x] - 3
            draw.polygon([(x - w_, ye), (x + w_, ye), (x + rng.uniform(-3, 3), ye + L)], fill=lip)
        x += int(rng.uniform(14, 30))

    # ---- tufts and texture strokes on the top ----
    tl, td = rgba(th["tuft_l"], 120), rgba(th["tuft_d"], 105)
    for _ in range(70):
        ccx, ccy = rng.uniform(CX - RX, CX + RX), rng.uniform(CY - RY, CY + RY)
        if not on_top(ccx, ccy, 0.05):
            continue
        for _ in range(rng.randint(5, 11)):
            tx, ty = ccx + rng.gauss(0, 22), ccy + rng.gauss(0, 11)
            if not on_top(tx, ty, 0.04):
                continue
            s_ = rng.uniform(6, 12)
            col = tl if rng.random() < 0.4 else td
            draw.line([tx - s_ * 0.35, ty, tx - s_ * 0.1, ty - s_], fill=col, width=2)
            draw.line([tx + s_ * 0.35, ty, tx + s_ * 0.15, ty - s_ * 0.9], fill=col, width=2)

    key = th["key"]

    # ---- per-island details ----
    if key == "home":
        for _ in range(90):
            fx, fy = rng.uniform(CX - RX, CX + RX), rng.uniform(CY - RY, CY + RY)
            if not on_top(fx, fy, 0.04): continue
            col = rng.choice([(255, 255, 255, 235), (255, 196, 214, 235), (255, 226, 110, 235)])
            r = rng.uniform(3.5, 6)
            for k in range(5):
                a = k * 1.2566
                draw.ellipse([fx + math.cos(a) * r - r * 0.6, fy + math.sin(a) * r * 0.6 - r * 0.5,
                              fx + math.cos(a) * r + r * 0.6, fy + math.sin(a) * r * 0.6 + r * 0.5], fill=col)
            draw.ellipse([fx - 2.2, fy - 2, fx + 2.2, fy + 2], fill=(255, 200, 60, 255))
        rocks = ("#8F8A80", "#B9B3A6")
    elif key == "wind":
        # wind-carved grooves along the sandstone
        for _ in range(60):
            gx = rng.uniform(CX - RX * 0.9, CX + RX * 0.9)
            xi = int(gx)
            if not has[xi]: continue
            gy = rng.uniform(y_edge[xi] + 20, max(y_edge[xi] + 21, y_bot[xi] - 20))
            L = rng.uniform(40, 130)
            draw.line([gx, gy, gx + L, gy + rng.uniform(-4, 4)], fill=(255, 246, 222, 120), width=3)
            draw.line([gx, gy + 4, gx + L, gy + 4], fill=(120, 96, 60, 90), width=2)
        for _ in range(70):
            fx, fy = rng.uniform(CX - RX, CX + RX), rng.uniform(CY - RY, CY + RY)
            if not on_top(fx, fy, 0.04): continue
            r = rng.uniform(4, 7)
            draw.ellipse([fx - r, fy - r, fx + r, fy + r], fill=(255, 255, 255, 170))
            draw.ellipse([fx - 1.6, fy - 1.6, fx + 1.6, fy + 1.6], fill=(236, 220, 150, 255))
        rocks = ("#C8B894", "#E6DAB8")
    elif key == "ice":
        # icicles off the front lip
        x = int(CX - RX * 0.92)
        while x < CX + RX * 0.92:
            if has[x]:
                ye = y_edge[x] + 8
                L = rng.uniform(22, 70) * (1 - abs(x - CX) / RX * 0.6)
                w = rng.uniform(6, 12)
                draw.polygon([(x - w, ye), (x + w, ye), (x + rng.uniform(-2, 2), ye + L)], fill=(214, 238, 255, 235))
                draw.line([x - w * 0.4, ye + 2, x, ye + L * 0.8], fill=(255, 255, 255, 220), width=2)
            x += int(rng.uniform(16, 34))
        for _ in range(40):
            cx_, cy_ = rng.uniform(CX - RX * 0.85, CX + RX * 0.85), 0
            xi = int(cx_)
            if not has[xi]: continue
            cy_ = rng.uniform(y_edge[xi] + 40, max(y_edge[xi] + 41, y_bot[xi] - 20))
            h_ = rng.uniform(16, 34)
            draw.polygon([(cx_, cy_ - h_), (cx_ + h_ * 0.35, cy_), (cx_, cy_ + h_ * 0.4), (cx_ - h_ * 0.35, cy_)],
                         fill=(150, 214, 255, 230))
            draw.line([cx_, cy_ - h_, cx_, cy_ + h_ * 0.4], fill=(235, 250, 255, 230), width=2)
        for _ in range(160):
            fx, fy = rng.uniform(CX - RX, CX + RX), rng.uniform(CY - RY, CY + RY)
            if not on_top(fx, fy, 0.03): continue
            r = rng.uniform(1.5, 3.2)
            draw.ellipse([fx - r, fy - r, fx + r, fy + r], fill=(255, 255, 255, 255))
        rocks = ("#8FA8BE", "#C6D8E8")
    elif key == "fire":
        glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        gd = ImageDraw.Draw(glow)
        for _ in range(13):
            vx = rng.uniform(CX - RX * 0.85, CX + RX * 0.85)
            xi = int(vx)
            if not has[xi]: continue
            vy = y_edge[xi] + rng.uniform(10, 40)
            pts = [(vx, vy)]
            for _ in range(rng.randint(3, 6)):
                vx += rng.uniform(-26, 26); vy += rng.uniform(18, 46)
                xi = int(np.clip(vx, 0, W - 1))
                if not has[xi] or vy > y_bot[xi] - 6: break
                pts.append((vx, vy))
            if len(pts) > 1:
                gd.line(pts, fill=(255, 120, 30, 255), width=16, joint="curve")
                draw.line(pts, fill=(255, 150, 50, 255), width=7, joint="curve")
                draw.line(pts, fill=(255, 226, 140, 255), width=3, joint="curve")
        for _ in range(22):
            fx, fy = rng.uniform(CX - RX, CX + RX), rng.uniform(CY - RY, CY + RY)
            if not on_top(fx, fy, 0.05): continue
            pts = [(fx, fy)]
            for _ in range(3):
                fx += rng.uniform(-18, 18); fy += rng.uniform(-8, 8)
                pts.append((fx, fy))
            gd.line(pts, fill=(255, 110, 30, 200), width=7)
            draw.line(pts, fill=(255, 176, 70, 230), width=2)
        glow = glow.filter(ImageFilter.GaussianBlur(7))
        img = Image.alpha_composite(glow, img)
        draw = ImageDraw.Draw(img)
        rocks = ("#3A3236", "#5A4E52")
    elif key == "storm":
        glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        gd = ImageDraw.Draw(glow)
        for _ in range(34):
            cx_ = rng.uniform(CX - RX * 0.85, CX + RX * 0.85)
            xi = int(cx_)
            if not has[xi]: continue
            cy_ = rng.uniform(y_edge[xi] + 30, max(y_edge[xi] + 31, y_bot[xi] - 16))
            h_ = rng.uniform(18, 40); tilt = rng.uniform(-8, 8)
            poly = [(cx_ + tilt, cy_ - h_), (cx_ + h_ * 0.28, cy_), (cx_, cy_ + h_ * 0.3), (cx_ - h_ * 0.28, cy_)]
            gd.polygon(poly, fill=(90, 240, 255, 170))
            draw.polygon(poly, fill=(150, 240, 255, 245))
            draw.line([poly[0], poly[2]], fill=(235, 255, 255, 255), width=2)
        glow = glow.filter(ImageFilter.GaussianBlur(9))
        img = Image.alpha_composite(glow, img)
        draw = ImageDraw.Draw(img)
        rocks = ("#6A6688", "#8E8AAE")
    else:  # gold
        for _ in range(60):
            cx_ = rng.uniform(CX - RX * 0.88, CX + RX * 0.88)
            xi = int(cx_)
            if not has[xi]: continue
            cy_ = rng.uniform(y_edge[xi] + 24, max(y_edge[xi] + 25, y_bot[xi] - 14))
            r = rng.uniform(5, 11)
            draw.ellipse([cx_ - r, cy_ - r * 0.8, cx_ + r, cy_ + r * 0.8], fill=(246, 196, 64, 255))
            draw.ellipse([cx_ - r * 0.5, cy_ - r * 0.6, cx_ + r * 0.1, cy_ - r * 0.1], fill=(255, 244, 190, 255))
        for _ in range(140):
            fx, fy = rng.uniform(CX - RX, CX + RX), rng.uniform(CY - RY, CY + RY)
            if not on_top(fx, fy, 0.03): continue
            r = rng.uniform(2, 4)
            draw.line([fx - r, fy, fx + r, fy], fill=(255, 250, 220, 230), width=1)
            draw.line([fx, fy - r, fx, fy + r], fill=(255, 250, 220, 230), width=1)
        rocks = ("#D6A868", "#F0CC96")

    # rocks embedded in the cliff face, lit from the top-left
    for _ in range(26):
        rx_ = rng.uniform(CX - RX * 0.9, CX + RX * 0.9)
        if not on_cliff(rx_, 0) and not has[int(rx_)]: continue
        xi = int(rx_)
        ry_ = rng.uniform(y_edge[xi] + 30, max(y_edge[xi] + 31, y_bot[xi] - 22))
        if not on_cliff(rx_, ry_): continue
        a_, b_ = rng.uniform(8, 18), rng.uniform(6, 11)
        draw.ellipse([rx_ - a_, ry_ - b_, rx_ + a_, ry_ + b_], fill=rgba(rocks[0], 255))
        draw.ellipse([rx_ - a_ * 0.7, ry_ - b_ * 0.9, rx_ + a_ * 0.2, ry_ - b_ * 0.1], fill=rgba(rocks[1], 255))

    # keep every stroke inside the island's own silhouette, then lay the details on the land
    a_arr = np.asarray(img).astype(np.float32)
    sil = np.maximum(top_a, cliff_a)
    a_arr[..., 3] = np.minimum(a_arr[..., 3], sil * 255)
    img = Image.alpha_composite(base, Image.fromarray(a_arr.astype(np.uint8), "RGBA"))
    img.save(os.path.join(OUT, f"island_{index}.png"))

    # snow cover for snowy hours, following this island's top
    lum = (0.3 * topc[..., 0] + 0.59 * topc[..., 1] + 0.11 * topc[..., 2]) / 255.0
    lit = np.clip((lum - 0.25) / 0.6, 0, 1)[..., None]
    snowc = hexc("#B4C6E0") * (1 - lit) + hexc("#FBFDFF") * lit
    sa = top_a * (0.82 + 0.18 * noise((H, W), 18, th["seed"] + 9))
    Image.fromarray(np.dstack([np.clip(snowc, 0, 255), np.clip(sa * 255, 0, 255)]).astype(np.uint8), "RGBA") \
        .save(os.path.join(OUT, f"island_{index}_snow.png"))
    return img


if __name__ == "__main__":
    for i, th in enumerate(THEMES):
        build(i, th)
        print("island", i, th["key"])
