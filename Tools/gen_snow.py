"""Snow that lies ON things: caps for the fence and the yard props, and frost on the beds.

    uv run --with pillow --with numpy python Tools/gen_snow.py

Why
---
Snowy weather used to lay one pale sheet over the whole island at 62% alpha. Nothing was
ever white, the grass turned mint, and the fence and the haystack stood in it untouched —
it read as fog on the lens, not snow on a farm. Real snow is decided by what faces the sky:
it piles on the flat top of a post, runs along the top edge of a rail, sits in a mound on the
haystack and leaves the sides bare.

How
---
For each source sprite (read from Resources, never modified):

  1. Find the SKY-FACING edge: opaque pixels whose normal points up (from the gradient of the
     blurred alpha) with nothing opaque a short way above them.
  2. Grow a cap from that edge: a rounded mound UP out of the silhouette and a thinner layer
     DOWN into it, both scaled by how flat the surface is, so a flat post top carries a thick
     pillow and a slanted rail a thin line, and every cap tapers to a rounded end.
  3. Shade it like snow in daylight: white on top, a soft blue-lavender underside, a thin
     cool contour so a white cap still reads against white ground.

The output is the source size plus PAD rows on top (a mound rises above a sprite cropped to
its own silhouette); IslandView stretches the cap over the prop with anchorMax.y = 1 + PAD/h.
Alpha is the whole effect — the game only fades it with the weather and tints it with the
hour's light, so a snowy night gives moonlit caps from the same art.

Also writes Art/beds/bed_frost.png: frost on a bed's rim and snow in the furrow around it, in
the bed frame (780x640), leaving the soil dark so crops stay readable.
"""
from PIL import Image, ImageFilter
import numpy as np
import math, os

ART = "Assets/Resources/Art"
OUT = os.path.join(ART, "snow")
os.makedirs(OUT, exist_ok=True)

SS = 3               # supersampling
PAD_FRAC = 0.16      # extra rows above the sprite, as a share of its height — IslandView.SnowPad


def hexc(s):
    s = s.lstrip("#")
    return np.array([int(s[i:i + 2], 16) for i in (0, 2, 4)], np.float32)


def blur(a, r):
    if r <= 0:
        return a
    im = Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(r))
    return np.asarray(im, np.float32) / 255.0


def blurf(a, r):
    """Gaussian blur of a float field, separable, without 8-bit quantisation."""
    if r <= 0:
        return a
    n = int(math.ceil(r * 3))
    k = np.exp(-0.5 * (np.arange(-n, n + 1, dtype=np.float32) / r) ** 2)
    k /= k.sum()
    p = np.pad(a, ((0, 0), (n, n)), mode="edge")
    t = sum(k[i] * p[:, i:i + a.shape[1]] for i in range(2 * n + 1))
    p = np.pad(t, ((n, n), (0, 0)), mode="edge")
    return sum(k[i] * p[i:i + a.shape[0]] for i in range(2 * n + 1)).astype(np.float32)


def cap(name, src, thick=1.0, reach=1.0, depth=1.0, leaves=True):
    """Snow cap for one sprite. thick scales the mound, depth the layer lying on the top face,
    reach how far above an edge must be clear."""
    im = Image.open(src).convert("RGBA")
    w0, h0 = im.size
    pad = int(round(h0 * PAD_FRAC))
    W, H = w0 * SS, (h0 + pad) * SS
    big = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    big.paste(im.resize((w0 * SS, h0 * SS), Image.LANCZOS), (0, pad * SS))
    rgba = np.asarray(big, np.float32)
    a = rgba[..., 3] / 255.0
    solid = a > 0.5

    # grass and leaves take a lighter dusting than wood and hay
    r_, g_, b_ = rgba[..., 0], rgba[..., 1], rgba[..., 2]
    green = (g_ > r_ * 1.12) & (g_ > b_ * 1.05) & solid

    # Every fence piece and prop is drawn at ~0.62-0.65 world units per source pixel, so snow is
    # sized in source pixels: the same depth of snow on a post as on the haystack.
    unit = SS
    sm = blurf(a, 1.2 * SS)
    gy, gx = np.gradient(sm)
    mag = np.hypot(gx, gy) + 1e-6
    up = gy / mag                                  # +1 where alpha rises going DOWN: a top edge

    # nothing opaque in the column a little way above
    clear_px = int(max(6, 14 * reach) * SS)
    win = np.zeros_like(solid, dtype=np.int32)
    cs = np.cumsum(solid.astype(np.int32), axis=0)
    for y in range(H):
        y1 = y - 2
        y0 = y - clear_px
        if y1 < 0:
            continue
        s1 = cs[y1]
        s0 = cs[y0] if y0 >= 0 else 0
        win[y] = s1 - s0
    exposed = win == 0

    edge = solid & ~np.vstack([np.zeros((1, W), bool), solid[:-1]])   # opaque with clear pixel directly above
    flat = np.clip((up - 0.40) / 0.45, 0, 1)
    seed = (edge & exposed).astype(np.float32) * flat
    seed = np.where(green, seed * (0.42 if leaves else 0.0), seed)
    # smear the seed along its own line so each cap tapers instead of stopping dead
    seed_s = blurf(seed, 2.2 * SS)
    seed_s = np.clip(seed_s / (seed_s.max() + 1e-6) * 2.4, 0, 1) if seed_s.max() > 0 else seed_s

    UP = 6.5 * unit * thick                       # mound height, px (supersampled)
    DN = 6.0 * unit * depth                       # layer depth into the object: covers a post's top face
    snow_up = np.zeros_like(a)
    snow_dn = np.zeros_like(a)
    nU, nD = int(UP) + 2, int(DN) + 2
    # a pixel k rows ABOVE an edge is snow while the edge's strength still exceeds k/UP (a mound);
    # a pixel k rows BELOW is snow while strength exceeds k/DN
    for k in range(1, nU):
        shifted = np.vstack([seed_s[k:], np.zeros((k, W), np.float32)])
        prof = np.sqrt(np.clip(1 - (k / UP) ** 2, 0, 1))
        snow_up = np.maximum(snow_up, np.clip((shifted - (k / UP) ** 1.6) * 6, 0, 1) * (prof > 0))
    for k in range(0, nD):
        shifted = np.vstack([np.zeros((k, W), np.float32), seed_s[:H - k]]) if k > 0 else seed_s
        snow_dn = np.maximum(snow_dn, np.clip((shifted - k / DN) * 6, 0, 1))
    snow_dn = snow_dn * a                             # the lower layer lies on the object only
    m = np.maximum(snow_up, snow_dn)
    m = blur(m, 0.9 * SS)
    m = np.clip((m - 0.35) * 3.2, 0, 1)                # crisp, soft-edged

    # ---- shading ----
    ys = np.arange(H, dtype=np.float32)[:, None]
    # vertical position inside each cap: 0 at the top of the snow, 1 at its lower edge
    top_dist = blurf(m, 1.2 * SS)
    gy2, gx2 = np.gradient(blurf(m, 1.8 * SS))
    light = np.clip(0.55 - gy2 * 9.0 * unit - gx2 * 4.5 * unit, 0, 1)   # lit from the top-left
    under = np.clip(gy2 * 12.0 * unit, 0, 1)                             # lower rim of a cap
    lit, mid, shade = hexc("#FBFDFF"), hexc("#E3E9F6"), hexc("#AEB6DC")
    col = mid[None, None] * (1 - light[..., None]) + lit[None, None] * light[..., None]
    col = col * (1 - under[..., None] * 0.85) + shade[None, None] * (under[..., None] * 0.85)
    # cool contour: reads on white ground
    ring = np.clip(blur(m, 1.4 * SS) * 1.6, 0, 1) * (1 - m)
    contour = hexc("#7F88B8")
    out_a = np.clip(m + ring * 0.55, 0, 1)
    col = (col * m[..., None] + contour[None, None] * (ring * 0.55)[..., None]) / np.maximum(out_a, 1e-4)[..., None]
    # a faint contact shadow on the object just under each cap
    shadow = np.clip(np.vstack([np.zeros((int(2 * SS), W)), m[:-int(2 * SS)]]) - m, 0, 1) * a * 0.35
    shadow = blur(shadow, 1.0 * SS)
    sh_col = hexc("#3A3552")
    tot_a = out_a + shadow * (1 - out_a)
    col = (col * out_a[..., None] + sh_col[None, None] * (shadow * (1 - out_a))[..., None]) / np.maximum(tot_a, 1e-4)[..., None]

    rgba_out = np.dstack([np.clip(col, 0, 255), np.clip(tot_a * 255, 0, 255)]).astype(np.uint8)
    res = Image.fromarray(rgba_out, "RGBA").resize((w0, h0 + pad), Image.LANCZOS)
    res.save(os.path.join(OUT, name + "_snow.png"))
    return res


def bed_frost():
    """Frost on a bed's raised rim and snow in the furrow, in the bed frame (see gen_beds.py)."""
    W, H = 780, 640
    A, B = 380.0, 190.0
    CX, CY = W / 2.0, H - H * 0.475
    S2 = 2
    ys, xs = np.mgrid[0:H * S2, 0:W * S2].astype(np.float32)
    x = (xs + 0.5) / S2
    y = (ys + 0.5) / S2
    dx = (x - CX) / A
    dy = (CY - y) / B
    u = np.abs(dx) + np.abs(dy)
    rng = np.random.RandomState(7)

    def vn(scale, seed):
        r = np.random.RandomState(seed)
        g = r.rand(int(H * S2 / scale) + 3, int(W * S2 / scale) + 3)
        return np.asarray(Image.fromarray((g * 255).astype(np.uint8)).resize((W * S2, H * S2), Image.BICUBIC), np.float32) / 255.0

    n1, n2 = vn(40, 3), vn(9, 4)
    far = (dy > 0).astype(np.float32)          # the two far edges catch more
    # furrow (outside the raised bed, inside the cell): a snowy band
    furrow = np.clip((u - 0.905) / 0.02, 0, 1) * np.clip((1.012 - u) / 0.012, 0, 1)
    furrow = furrow * np.clip(0.75 + (n1 - 0.5) * 1.2, 0, 1)
    # rim of the raised bed: a thin frost line, heavier on the two far edges
    rim = np.clip(1 - np.abs(u - 0.866) / 0.016, 0, 1) * (0.35 + 0.45 * far) * np.clip(0.7 + (n1 - 0.5), 0, 1)
    # a faint rime creeping in from the rim on the far edges only, fading toward the middle
    rime = np.clip((u - 0.76) / 0.08, 0, 1) * np.clip((0.848 - u) / 0.015, 0, 1) * np.clip((n2 - 0.55) * 3, 0, 1) * 0.35 * far
    m = np.clip(np.maximum(np.maximum(furrow * 0.92, rim), rime), 0, 1)

    lit, shade = hexc("#F7FAFF"), hexc("#B7C0E2")
    t = np.clip(0.5 + 0.5 * (dy * 0.8 - dx * 0.2) + (n2 - 0.5) * 0.4, 0, 1)
    col = shade[None, None] * (1 - t[..., None]) + lit[None, None] * t[..., None]
    # sparse sparkles
    sp = (rng.rand(H * S2, W * S2) > 0.9993).astype(np.float32) * (m > 0.4)
    sp = blur(sp, 0.8)
    col = col + (sp * 90)[..., None]
    a = np.clip(m + sp, 0, 1)
    out = np.dstack([np.clip(col, 0, 255), a * 255]).astype(np.uint8)
    Image.fromarray(out, "RGBA").resize((W, H), Image.LANCZOS).save(os.path.join(ART, "beds", "bed_frost.png"))


PIECES = [
    # name, source, mound, clear reach, depth on the top face (a post's top face is ~11 px tall)
    ("fence_post", "gen/fence_post", 1.0, 1.0, 2.0),
    ("fence_rail", "gen/fence_rail", 0.55, 0.6, 0.75),
    ("fence_gate", "gen/fence_gate", 0.85, 0.8, 1.8),
    ("haystack", "farm/haystack", 1.35, 1.0, 3.2),
    ("rock", "farm/rock", 1.2, 0.35, 2.0),
    ("signpost", "farm/signpost", 0.9, 1.0, 1.0),
    ("banner", "farm/banner", 0.8, 1.0, 0.9),
    # the islands' own props with a moving part (Tools/gen_life.py); the pines carry painted snow already,
    # the vents are too hot for it
    ("flagpole", "life/prop_flagpole", 0.7, 1.0, 1.2),
    ("rod", "life/prop_rod", 0.8, 1.0, 1.4),
    ("chest", "life/prop_chest", 1.1, 1.0, 1.8),
    ("coins", "life/prop_coins", 0.8, 1.0, 1.2),
    # the painted yard props cut from the owner's decor sheets (Tools/slice_decor.py). One cap per prop: the
    # ground variants (_snow, _ash, _slate, _sand, _ice) only recolour the skirt, the silhouette is the same.
    ("well", "decor/well", 1.2, 1.0, 2.0),
    ("watering_can", "decor/watering_can", 0.8, 0.8, 1.0),
    ("birdhouse_post", "decor/birdhouse_post", 1.0, 1.0, 1.4),
    ("pumpkin_crate", "decor/pumpkin_crate", 1.0, 0.8, 1.4),
    ("flower_barrow", "decor/flower_barrow", 0.9, 0.8, 1.2),
    ("mailbox", "decor/mailbox", 0.9, 1.0, 1.2),
    ("water_tub", "decor/water_tub", 0.9, 1.0, 1.2),
    ("lily_pond", "decor/lily_pond", 0.9, 0.6, 1.2),
    ("bird_bath_g", "decor/bird_bath_g", 0.9, 0.8, 1.2),
    ("giant_shroom", "decor/giant_shroom", 1.3, 1.0, 2.2),
    ("stump_shrooms", "decor/stump_shrooms", 1.1, 0.8, 1.8),
    ("blueberry", "decor/blueberry", 0.9, 0.8, 1.2),
    ("glow_pod", "decor/glow_pod", 0.9, 0.8, 1.2),
    ("berry_basket", "decor/berry_basket", 0.9, 0.8, 1.2),
    ("arrow_sign", "decor/arrow_sign", 0.9, 1.0, 1.0),
    ("mill", "decor/mill", 1.1, 1.0, 1.8),
    ("hay_barrel", "decor/hay_barrel", 1.3, 1.0, 3.0),
    ("scarecrow", "decor/scarecrow", 0.9, 1.0, 1.2),
    ("sunflower_fence", "decor/sunflower_fence", 0.8, 0.8, 1.0),
    ("sign_leaf", "decor/sign_leaf", 0.9, 1.0, 1.0),
    ("crystals", "decor/crystals", 0.8, 0.5, 1.2),
    ("banner_lamp", "decor/banner_lamp", 0.8, 1.0, 1.0),
    ("mossy_rock", "decor/mossy_rock", 1.2, 0.35, 2.0),
    ("clay_oven", "decor/clay_oven", 1.2, 1.0, 2.0),
    ("campfire", "decor/campfire", 0.8, 1.0, 1.0),
    ("barrel", "decor/barrel", 1.1, 1.0, 1.8),
    ("rock_lamp", "decor/rock_lamp", 1.1, 0.5, 1.8),
    ("lamp_iron", "decor/lamp_iron", 0.8, 1.0, 1.2),
    ("market", "decor/market", 1.1, 1.0, 1.8),
    ("veg_cart", "decor/veg_cart", 0.9, 0.8, 1.2),
]


if __name__ == "__main__":
    import sys
    only = set(sys.argv[1:])
    for name, src, thick, reach, depth in PIECES:
        if only and name not in only:
            continue
        cap(name, os.path.join(ART, src + ".png"), thick, reach, depth)
        print("cap", name)
    if not only or "bed" in only:
        bed_frost()
        print("bed_frost")
