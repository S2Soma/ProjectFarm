"""The six floating islands, one style per island, at a resolution that stays sharp.

    uv run --with pillow --with numpy python Tools/gen_islands.py

Why this replaces Art/chrome/island.png
---------------------------------------
That sprite was 660x480 and drawn at 1144x832 world units — about 2.6x upscaled on a
1080p phone at farm zoom, so every edge was soft, and with no mipmaps it shimmered and
broke up when the map zoomed out. It was also one sprite for all six islands, told apart
only by a colour multiplier.

Shape
-----
The top surface is the field's own grid, grown by a yard and rounded: a rounded square in
grid cells, which the isometric view turns into a diamond whose edges run exactly parallel to
the beds. It used to be an ellipse, and nothing else in the scene is round — the fence, the
beds and the bridges all run on the grid's 0.5 slopes, so an oval plateau left wedges of lawn
that belonged to no line, and the fence could only ever touch it at two points.

The numbers below MUST match IslandView (StepX/StepY, RimCells, RimRound, and the sprite rect
IslandSpriteSize / IslandSpriteY): the script paints the island so that world (0, 0) is the
field's centre.

Each island has its own palette, cliff and details, named after what it is:

  0 Vườn Nhà  lush meadow, earth cliff, wildflowers
  1 Đảo Gió   pale windswept grass, sandstone cliff carved into horizontal grooves
  2 Đảo Băng  snowfield, blue slate cliff, icicles hanging off the lip, ice crystals
  3 Đảo Hoả   ash and scorched earth, basalt cliff split by glowing lava veins
  4 Đảo Lôi   storm-teal grass, violet slate cliff studded with charged crystals
  5 Đảo Vàng  golden wheat-grass, warm sandstone cliff seamed with gold
  6 Đảo Nước  riverside meadow: a river one cell wide runs across the island from a spring at the
            back and pours off the front-right edge as a waterfall (IslandSys.RiverHalf, the beds
            sit on the two banks)
  7 Khổng Lồ  deep-green giant's meadow, mossy boulders, roots and vines hanging off a dark cliff

The THEMES index is the island's `style` in IslandSys.Defs, not its place in the archipelago.

Also writes island_N_snow.png: the snow cover for snowy hours, cut from each island's own
top surface so snow follows its outline.
"""
from PIL import Image, ImageDraw, ImageFilter
import numpy as np, math, random, os

OUT = "Assets/Resources/Art/islands"
os.makedirs(OUT, exist_ok=True)

# ---- shared with IslandView ----
STEP_X, STEP_Y = 119.28, 59.64          # one grid cell in world units (84 x 1.42, 42 x 1.42)
RIM, ROUND = 3.0, 0.45                  # island half-size and corner radius, in cells (field is 2)
K = 0.72                                # world units per image pixel
W, H = 2000, 1278                       # -> sprite rect 1440 x 920 world units
SPRITE_Y = -94.0                        # world y of the sprite's centre

CX = W * 0.5
IY0 = H * 0.5 + SPRITE_Y / K            # image row of world y = 0 (the field centre)
PX_PER_CELL = 2 * STEP_X * STEP_Y / math.hypot(STEP_X, STEP_Y) / K   # edge-normal pixels per cell
EXT = 2 * (RIM - ROUND) + math.sqrt(2) * ROUND                        # corner reach, in cells
RX = EXT * STEP_X / K                   # half-width of the top, image px
RY = EXT * STEP_Y / K                   # half-height of the top, image px
CY = IY0
DEPTH = 150.0 / K                       # cliff depth under the middle, image px


def grid_of(x, y):
    """Image pixel -> grid cells (u, v) from the field centre."""
    wx = (x - CX) * K
    wy = (IY0 - y) * K
    return (wx / STEP_X - wy / STEP_Y) * 0.5, (-wx / STEP_X - wy / STEP_Y) * 0.5


def rim_sdf(u, v, wobble=0.0):
    """Signed distance in cells to the rounded-square rim (negative inside)."""
    qx = np.abs(u) - (RIM - ROUND)
    qy = np.abs(v) - (RIM - ROUND)
    outside = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0))
    inside = np.minimum(np.maximum(qx, qy), 0)
    return outside + inside - ROUND + wobble


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
    dict(key="water", seed=79,
         top_hi="#B4E68A", top_lo="#62B257", alt="#8FD173", tuft_d="#4E9C48", tuft_l="#D6F5B0",
         cliff=["#8A6F52", "#76603F", "#9A8062"], cliff_d="#3E3122", lip="#76C45C"),
    dict(key="giant", seed=83,
         top_hi="#86C95A", top_lo="#3F8A3A", alt="#6BB24C", tuft_d="#2F6E2E", tuft_l="#B4E27E",
         cliff=["#6B5238", "#5A452F", "#7A5F43"], cliff_d="#2A2016", lip="#4F9A3E"),
]

RIVER_HALF = 0.5        # IslandSys.RiverHalf
SPRING_U = -2.25        # where the river rises, in cells


def build(index, th):
    rng = random.Random(th["seed"])
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    dx = (xs - CX) / RX
    dy = (ys - CY) / RY
    gu, gv = grid_of(xs, ys)
    ang = np.arctan2(gv, gu)

    # A rim of its own: a faint wobble along the edge (a few hundredths of a cell), so six
    # islands are not six identical machined tiles. Small enough that the fence, which sits
    # 0.28 cells inside, never meets the edge.
    p = [rng.uniform(0, 6.28) for _ in range(3)]
    wob = 0.030 * np.sin(7 * ang + p[0]) + 0.018 * np.sin(13 * ang + p[1]) + 0.010 * np.sin(23 * ang + p[2])
    sdf = rim_sdf(gu, gv, wob)
    top_a = np.clip(-sdf * PX_PER_CELL + 0.5, 0, 1)
    top = top_a > 0.5

    # ---- the river (Đảo Nước): a band along u, a round spring at its head ----
    river = np.zeros((H, W), np.float32)
    if th["key"] == "water":
        meander = 0.05 * np.sin(gu * 1.7 + 0.6) + 0.02 * np.sin(gu * 4.3)
        half = RIVER_HALF - 0.06 + meander * 0.0
        band = np.clip((half - np.abs(gv - meander)) * PX_PER_CELL + 0.5, 0, 1)
        band = band * np.clip((gu - SPRING_U) * PX_PER_CELL + 0.5, 0, 1)
        spring = np.clip((0.62 - np.hypot(gu - SPRING_U, gv * 1.05)) * PX_PER_CELL + 0.5, 0, 1)
        river = np.maximum(band, spring) * top_a

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
    edge = np.clip(-sdf / 0.16, 0, 1)
    back = dy < 0.15
    topc = np.where((back & (edge < 1))[..., None], mix(topc, topc * 1.13 + 10, (1 - edge) * 0.9), topc)
    topc = np.where(((~back) & (edge < 1))[..., None], mix(topc, topc * 0.78, (1 - edge) * 0.8), topc)

    if th["key"] == "water":
        # damp, darker banks, then the water: deep in the middle, light at the edges, ripples
        bank = np.clip(1 - (np.abs(gv) - RIVER_HALF) / 0.28, 0, 1) * (gu > SPRING_U - 0.6) * top_a
        topc = mix(topc, topc * 0.72, bank * (1 - river) * 0.55)
        depthv = np.clip(1 - np.abs(gv) / RIVER_HALF, 0, 1)
        water = mix(hexc("#8FE0F2"), hexc("#2F8FC8"), np.power(depthv, 0.7))
        rip = np.sin((gu * 9.0 + gv * 3.0) + noise((H, W), 22, th["seed"] + 11) * 6.0)
        water = water + (np.clip(rip - 0.75, 0, 1) * 150)[..., None]
        edge_foam = np.clip(1 - (RIVER_HALF - np.abs(gv)) / 0.07, 0, 1) * (river > 0.5)
        water = mix(water, hexc("#E9FBFF"), edge_foam * 0.7)
        topc = mix(topc, water, river)

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
        u, v = grid_of(x, y)
        if th["key"] == "water" and abs(v) < RIVER_HALF + 0.15 and u > SPRING_U - 0.75:
            return False
        # pad was a share of the old ellipse's radius; in cells it wants to be ~6x that
        return float(rim_sdf(np.float32(u), np.float32(v))) < -pad * 6.0

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
    elif key == "water":
        # the waterfall: every column where the river meets the front edge pours down the cliff
        # and on past the island's underside, thinning into mist
        fall = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        fd = ImageDraw.Draw(fall)
        mouth = []
        for xi in range(W):
            if not has[xi]:
                continue
            ye = int(y_edge[xi])
            if 0 < ye < H and river[max(0, ye - 3), xi] > 0.5:
                mouth.append(xi)
        if mouth:
            x0, x1 = min(mouth), max(mouth)
            for xi in range(x0, x1 + 1):
                ye = y_edge[xi] - 2
                t = (xi - x0) / max(1, x1 - x0)
                core = 1 - abs(t - 0.5) * 2
                col = (int(90 + 120 * core), int(190 + 50 * core), 255, 255)
                fd.line([(xi, ye), (xi, H)], fill=col)
            fall_arr = np.asarray(fall).astype(np.float32)
            yy = np.arange(H, dtype=np.float32)[:, None]
            fade = np.clip(1 - (yy - (y_edge[x0] + 170)) / 150, 0, 1)
            fall_arr[..., 3] *= fade
            # streaks of white water
            streak = noise((H, W), 6, th["seed"] + 13)
            streak = np.clip((streak - 0.55) * 3, 0, 1)
            fall_arr[..., :3] = fall_arr[..., :3] * (1 - streak[..., None] * 0.6) + 255 * streak[..., None] * 0.6
            fall = Image.fromarray(fall_arr.astype(np.uint8), "RGBA")
            fd = ImageDraw.Draw(fall)
            for _ in range(60):
                mx = rng.uniform(x0 - 20, x1 + 20)
                my = y_edge[int(np.clip(mx, x0, x1))] + rng.uniform(150, 280)
                r = rng.uniform(10, 26)
                fd.ellipse([mx - r, my - r * 0.7, mx + r, my + r * 0.7], fill=(255, 255, 255, int(rng.uniform(40, 110))))
            # foam where it leaves the lip
            for _ in range(30):
                mx = rng.uniform(x0, x1)
                my = y_edge[int(mx)] + rng.uniform(-2, 10)
                r = rng.uniform(4, 9)
                fd.ellipse([mx - r, my - r * 0.6, mx + r, my + r * 0.6], fill=(255, 255, 255, 220))
            mouth_range = (x0, x1)
        else:
            mouth_range = None
        # reeds and lily pads along the banks
        for _ in range(70):
            u_ = rng.uniform(SPRING_U - 0.3, 2.7)
            side = rng.choice((-1, 1))
            v_ = side * (RIVER_HALF + rng.uniform(0.02, 0.18))
            wx = (u_ - v_) * STEP_X
            wy = -(u_ + v_) * STEP_Y
            px, py = CX + wx / K, IY0 - wy / K
            if float(rim_sdf(np.float32(u_), np.float32(v_))) > -0.15:
                continue
            for k in range(rng.randint(2, 4)):
                hx = px + rng.uniform(-6, 6)
                h = rng.uniform(14, 26)
                draw.line([hx, py, hx + rng.uniform(-4, 4), py - h], fill=(70, 130, 60, 255), width=3)
                if rng.random() < 0.4:
                    draw.ellipse([hx - 3, py - h - 7, hx + 3, py - h + 3], fill=(120, 80, 40, 255))
        for _ in range(16):
            u_ = rng.uniform(SPRING_U + 0.4, 2.4)
            v_ = rng.uniform(-RIVER_HALF + 0.12, RIVER_HALF - 0.12)
            px, py = CX + (u_ - v_) * STEP_X / K, IY0 + (u_ + v_) * STEP_Y / K
            r = rng.uniform(8, 13)
            draw.ellipse([px - r, py - r * 0.5, px + r, py + r * 0.5], fill=(92, 170, 80, 235))
            if rng.random() < 0.35:
                draw.ellipse([px - 3, py - 3, px + 3, py + 2], fill=(255, 200, 225, 255))
        rocks = ("#8F8A80", "#B9B3A6")
    elif key == "giant":
        # roots and vines hanging off the cliff
        for _ in range(40):
            vx = rng.uniform(CX - RX * 0.85, CX + RX * 0.85)
            xi = int(vx)
            if not has[xi]: continue
            vy = y_edge[xi] + rng.uniform(4, 20)
            L = rng.uniform(40, 150) * (1 - abs(vx - CX) / RX * 0.5)
            pts = [(vx, vy)]
            for k in range(6):
                vx += rng.uniform(-8, 8); vy += L / 6
                pts.append((vx, vy))
            col = rng.choice([(70, 130, 55, 255), (96, 72, 44, 255), (58, 110, 48, 255)])
            draw.line(pts, fill=col, width=int(rng.uniform(3, 7)), joint="curve")
            if col[1] > 100:
                for (lx, ly) in pts[1::2]:
                    draw.ellipse([lx - 6, ly - 3, lx + 6, ly + 3], fill=(96, 170, 70, 255))
        # mossy boulders in the yard
        for _ in range(14):
            fx, fy = rng.uniform(CX - RX, CX + RX), rng.uniform(CY - RY, CY + RY)
            u_, v_ = grid_of(fx, fy)
            if not on_top(fx, fy, 0.05) or (abs(u_) < 2.1 and abs(v_) < 2.1): continue
            r = rng.uniform(14, 26)
            draw.ellipse([fx - r, fy - r * 0.55 + 5, fx + r, fy + r * 0.55 + 5], fill=(40, 50, 30, 120))
            draw.ellipse([fx - r, fy - r * 0.6, fx + r, fy + r * 0.5], fill=(128, 124, 112, 255))
            draw.ellipse([fx - r * 0.8, fy - r * 0.65, fx + r * 0.5, fy - r * 0.1], fill=(96, 160, 72, 255))
        for _ in range(110):
            fx, fy = rng.uniform(CX - RX, CX + RX), rng.uniform(CY - RY, CY + RY)
            if not on_top(fx, fy, 0.04): continue
            s_ = rng.uniform(10, 20)
            col = (46, 110, 44, 170) if rng.random() < 0.6 else (180, 226, 126, 150)
            draw.line([fx - s_ * 0.3, fy, fx - s_ * 0.05, fy - s_], fill=col, width=3)
            draw.line([fx + s_ * 0.3, fy, fx + s_ * 0.1, fy - s_ * 0.9], fill=col, width=3)
        rocks = ("#5A5048", "#7E7468")
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
    if key == "water" and mouth_range is not None:
        img = Image.alpha_composite(img, fall)
    img.save(os.path.join(OUT, f"island_{index}.png"))

    # snow cover for snowy hours, following this island's top
    lum = (0.3 * topc[..., 0] + 0.59 * topc[..., 1] + 0.11 * topc[..., 2]) / 255.0
    lit = np.clip((lum - 0.25) / 0.6, 0, 1)[..., None]
    snowc = hexc("#B4C6E0") * (1 - lit) + hexc("#FBFDFF") * lit
    sa = top_a * (0.82 + 0.18 * noise((H, W), 18, th["seed"] + 9)) * (1 - river)
    Image.fromarray(np.dstack([np.clip(snowc, 0, 255), np.clip(sa * 255, 0, 255)]).astype(np.uint8), "RGBA") \
        .save(os.path.join(OUT, f"island_{index}_snow.png"))
    return img


if __name__ == "__main__":
    import sys
    only = [int(a) for a in sys.argv[1:]]
    for i, th in enumerate(THEMES):
        if only and i not in only:
            continue
        build(i, th)
        print("island", i, th["key"])
