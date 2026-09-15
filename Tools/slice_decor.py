"""Cut the owner's painted decor sheets into yard props: Resources/Art/decor/<name>.png.

    uv run --with pillow --with numpy --with scipy --with scikit-image python Tools/slice_decor.py
    uv run ... python Tools/slice_decor.py --sheet out_dir      # also contact sheets (light, dark, checker)
    uv run ... python Tools/slice_decor.py --detect out_dir     # numbered detection pass, to pick indices

The four sheets (project root, "ChatGPT Image Sep 15, 2026, 06_49_*") are the owner's AI-generated
images, in the painted style of the islands and of the older farm props (Art/farm/haystack, signpost…).

What the sheets actually carry
------------------------------
All four have a real alpha channel, but not a clean one:

  * A and B show a blurred colourful / green backdrop in their RGB where alpha is 0, and the object's
    anti-aliased rim is that backdrop mixed in — composited on anything else, every prop wore a
    green or brown halo;
  * C and D are black under alpha 0, with a 20-px fog of alpha 1..10 in saturated noise colours
    round every item (the "fringe halos" a viewer that ignores alpha shows);
  * solid alpha tops out at 250..253, so nothing is ever fully opaque;
  * in C and D neighbouring items touch — grass skirts overlap, a chimney's smoke reaches the
    signboard above it.

So each cut is:

  1. SEGMENT the sheet: cores = solid alpha eroded 16 px (touching items separate there), cores of
     one item merged through the mask eroded only 6 px, then a watershed on the distance field
     assigns every pixel to one item. A cut through two touching grass skirts is feathered 3 px.
  2. ALPHA: rescaled from 20..235 to 0..1 (kills the fog, makes the body opaque), specks dropped.
  3. DEFRINGE: the 3-px rim takes the colour of the nearest solid pixel (the painted outline), so
     no backdrop colour survives at the edge. Wider translucent areas (smoke, water spray, lantern
     glow) keep their own colour, unmixed from the local backdrop estimate on sheets A and B.
  4. FOOT: the footprint's centre, a quarter of the base width above the lowest opaque row (see
     foot_of). The sprite is padded so that point is its horizontal centre; its height above the
     bottom edge is the pivot IslandView stands it on (DecorCatalog.pivotY).
  5. Items from sheet A stand on nothing, so they get a soft contact shadow under that foot.
  6. GROUND variants (<name>_<ground>.png) for the islands whose ground is not grass: the grass skirt
     (green, in the band round the foot — never leaves higher up the item) and the flowers in it are
     recoloured to that ground by luminance, keeping the painting's shading: snow (Đảo Băng), ash
     (Đảo Hoả), slate (Đảo Lôi), sand (Đảo Vàng); "ice" is snow plus the violet crystals turned to ice.
  6b. SAILS: the windmill's painted sails are lifted off into <name>_sails.png and the roof and tower behind them
     rebuilt (split_sails), so IslandLife can turn them.
  6c. SKIRT SNOW: Art/snow/<name>_skirt.png, the skirt alone in snow, faded in with the weather over props on grass.
  7. LIGHTS: lantern flames and fires (LIGHTS, hand-picked sheet points snapped to the brightest warm
     pixels) are written with the pivot into Assets/Scripts/Farm/DecorCatalog.cs, so IslandView hangs its
     evening glow on them — the same flickering pools as the gate lanterns. Crystals also get a light
     mask (<name>_<ground>_glow.png) for MiT/UI Glow.

Only ITEMS marked shipped are written (everything under Resources/ ships in the build); --sheet shows
the spares too. REJECTED says why some obvious candidates are not used.
"""
from PIL import Image, ImageDraw, ImageFilter
import numpy as np
from scipy import ndimage
from skimage.segmentation import watershed
import cv2
import math, os, sys

OUT = "Assets/Resources/Art/decor"
SNOW = "Assets/Resources/Art/snow"            # the skirt snow overlays sit with the snow caps (Tools/gen_snow.py)
CATALOG = "Assets/Scripts/Farm/DecorCatalog.cs"
MAX_SIDE = 320

SHEETS = {
    "A": "ChatGPT Image Sep 15, 2026, 06_49_03 PM.png",   # 5x4, blurred colourful backdrop, NO grass skirts
    "B": "ChatGPT Image Sep 15, 2026, 06_49_15 PM.png",   # 5x4, blurred green backdrop, grass skirts
    "C": "ChatGPT Image Sep 15, 2026, 06_49_24 PM.png",   # 5x4, black + fog, grass skirts
    "D": "ChatGPT Image Sep 15, 2026, 06_49_33 PM.png",   # 10x2, black + fog, grass skirts, items touch
}
BACKDROP = {"A": True, "B": True, "C": False, "D": False}

# name -> (sheet, index from the detection pass, grounds to recolour the skirt to, shipped)
# Only shipped items are written to Resources (everything under Resources goes into the build); the rest
# are cut for the contact sheet only, so the choice stays visible. Names must not collide with the
# older props (Art/farm, Art/life): IslandView keys snow caps and moving parts on the name.
ITEMS = {
    # ---- A: no skirt ----
    "flower_barrow":   ("A", 0, [], True),
    "water_tub":       ("A", 1, [], True),
    "bird_bath":       ("A", 2, [], False),
    "barrels":         ("A", 3, [], False),
    "carrot_crate":    ("A", 4, [], False),
    "campfire":        ("A", 5, [], True),
    "bench_lamp":      ("A", 6, [], False),
    "well_vines":      ("A", 7, [], False),
    "gate_lamp":       ("A", 8, [], False),
    "arch_lamp":       ("A", 9, [], False),
    "watering_can":    ("A", 10, [], True),
    "toolbox":         ("A", 11, [], False),
    "birdhouse":       ("A", 12, [], False),
    "log_planter":     ("A", 13, [], False),
    "lamp_stone":      ("A", 14, [], False),
    "squirrel_stump":  ("A", 15, [], False),
    "signpost2":       ("A", 16, [], False),
    "hay_fork":        ("A", 17, [], False),
    "pumpkin_crate":   ("A", 18, [], True),
    "hydrangea":       ("A", 19, [], False),
    # ---- B: grass skirt ----
    "log_bench":       ("B", 0, [], False),
    "flower_cart":     ("B", 1, [], False),
    "bird_bath_g":     ("B", 2, [], True),
    "vine_lamp":       ("B", 3, [], False),
    "lily_pond":       ("B", 4, [], True),
    "hen_nest":        ("B", 5, [], False),
    "paw_sign":        ("B", 6, [], False),
    "log_planter_g":   ("B", 7, [], False),
    "spring_tub":      ("B", 8, [], False),
    "rock_lamp":       ("B", 9, ["slate"], True),
    "rose_arch":       ("B", 10, [], False),
    "stump_shrooms":   ("B", 11, [], True),
    "butterfly_bloom": ("B", 12, [], False),
    "sunflower_fence": ("B", 13, [], True),
    "birdhouse_post":  ("B", 14, [], True),
    "crystals":        ("B", 15, ["slate", "ice"], True),
    "blueberry":       ("B", 16, [], True),
    "berry_basket":    ("B", 17, [], True),
    "giant_shroom":    ("B", 18, [], True),
    "glow_pod":        ("B", 19, [], True),
    # ---- C ----
    "signboard":       ("C", 0, [], False),
    "veg_cart":        ("C", 1, ["sand"], True),
    "well_c":          ("C", 2, [], False),
    "market_c":        ("C", 3, [], False),
    "mill_c":          ("C", 4, [], False),
    "clay_oven":       ("C", 5, ["ash"], True),
    "scarecrow":       ("C", 6, [], True),
    "bird_post":       ("C", 7, [], False),
    "tool_cart":       ("C", 8, [], False),
    "stone_spring":    ("C", 9, [], False),
    "arbor_lamp":      ("C", 10, [], False),
    "hay_barrel":      ("C", 11, [], True),
    "falls_rocks":     ("C", 12, [], False),
    "cottage":         ("C", 13, [], False),
    "banner_lamp":     ("C", 14, ["snow", "ash", "slate", "sand"], True),
    "flower_box_can":  ("C", 15, [], False),
    "campfire_g":      ("C", 16, [], False),
    "park_bench":      ("C", 17, [], False),
    "mailbox":         ("C", 18, [], True),
    "fence_lamp":      ("C", 19, [], False),
    # ---- D ----
    "fence_bit":       ("D", 0, [], False),
    "sign_leaf":       ("D", 1, ["snow", "ash", "slate", "sand"], True),
    "lamp_post":       ("D", 2, [], False),
    "banner_d":        ("D", 3, [], False),
    "hay_bale":        ("D", 4, [], False),
    "well":            ("D", 5, ["snow"], True),
    "apple_cart":      ("D", 6, [], False),
    "mossy_rock":      ("D", 7, ["snow", "ash", "slate", "sand"], True),
    "barrel":          ("D", 8, ["ash", "sand"], True),
    "apple_crate":     ("D", 9, [], False),
    "flower_box":      ("D", 10, [], False),
    "lamp_iron":       ("D", 11, ["snow", "ash", "slate", "sand"], True),
    "bench":           ("D", 12, [], False),
    "scarecrow_d":     ("D", 13, [], False),
    "arrow_sign":      ("D", 14, [], True),
    "mill":            ("D", 15, [], True),
    "spout_well":      ("D", 16, [], False),
    "wheelbarrow":     ("D", 17, [], False),
    "arbor":           ("D", 18, [], False),
    "market":          ("D", 19, ["sand"], True),
}

# Not shipped, and why (the rest of the unshipped cuts are simply spares for later islands).
REJECTED = {
    "bird_bath":    "the skirted bird_bath_g sits on Đảo Nước's grass better",
    "well_vines":   "vines + blue roof duplicate `well`, which carries a cleaner snow cap",
    "mill_c":       "same windmill as `mill`, whose sails are more legible at yard size",
    "cottage":      "a house reads as a building the player should be able to enter",
    "falls_rocks":  "Đảo Nước already has a live river and waterfall — a painted one next to it is a copy",
    "stone_spring": "crowds the river island, which has room for two props behind the field",
    "banner_d":     "same banner as Art/farm/banner (the green islands keep that one)",
    "lamp_post":    "lamp_iron reads better at yard size (taller glass)",
    "gate_lamp":    "a gate standing inside the fence, next to the real gates, reads as a second entrance",
    "fence_bit":    "fence pieces inside the fence",
    "fence_lamp":   "fence pieces inside the fence",
    "paw_sign":     "a pet sign promises something the island does not have",
}

# Art/farm props with a grass skirt, which get a skirt snow overlay too (they are not cut here).
FARM_SKIRTS = ["haystack", "rock", "signpost", "banner"]

# Lanterns and fires, as sheet pixels; each is snapped to the brightest warm pixels within 22 px.
LIGHTS = {
    "water_tub":   [(543, 150, "lamp")],
    "campfire":    [(250, 415, "lamp"), (172, 492, "fire")],
    "park_bench":  [(928, 742, "lamp")],
    "banner_lamp": [(1508, 575, "lamp")],
    "lamp_iron":   [(359, 489, "lamp")],
    "rock_lamp":   [(1410, 390, "lamp")],
    "clay_oven":   [(206, 418, "fire")],
    "glow_pod":    [(1343, 925, "fire")],
}

# Light masks for MiT/UI Glow (rgb = light, a = where it may sparkle), for props that shine by themselves.
GLOWS = {
    "crystals": {"slate": (0.62, 0.52, 1.0), "ice": (0.70, 0.90, 1.0)},
}

# Ground ramps: shadow, mid, light (sampled from the island paintings' yards).
GROUNDS = {
    "snow":   ("#9FB0D0", "#DCE6F3", "#FFFFFF"),
    "ash":    ("#3A2F28", "#76624E", "#A48D74"),
    "slate":  ("#3E6A6C", "#78ABA4", "#B4DCD3"),
    "sand":   ("#A57C36", "#E0BE68", "#FBE9AA"),
    "ice":    ("#9FB0D0", "#DCE6F3", "#FFFFFF"),     # snow ground + the crystals turned to ice
}


def hexc(s):
    s = s.lstrip("#")
    return np.array([int(s[i:i + 2], 16) for i in (0, 2, 4)], np.float32)


# ============================================================ segmentation
def segment(alpha):
    mask = alpha > 40
    core = ndimage.binary_erosion(alpha > 128, iterations=16)
    lab, n = ndimage.label(core)
    sizes = ndimage.sum(core, lab, range(1, n + 1))
    keep = np.zeros(n + 1, bool)
    keep[1:] = sizes > 800
    lab = np.where(keep[lab], lab, 0)
    mid, _ = ndimage.label(ndimage.binary_erosion(alpha > 128, iterations=6))
    groups = {}
    for m in np.unique(lab):
        if m == 0:
            continue
        ys, xs = np.nonzero(lab == m)
        groups.setdefault(mid[ys[0], xs[0]], []).append(m)
    markers = np.zeros_like(lab)
    for k, ms in enumerate(groups.values(), start=1):
        for m in ms:
            markers[lab == m] = k
    dist = ndimage.distance_transform_edt(mask)
    ws = watershed(-dist, markers, mask=ndimage.binary_dilation(mask, iterations=3))
    # order: rows top to bottom, then left to right
    boxes = []
    for i, sl in enumerate(ndimage.find_objects(ws)):
        if sl is None:
            continue
        boxes.append((sl[0].start, sl[1].start, sl[0].stop, sl[1].stop, i + 1))
    boxes.sort(key=lambda b: (b[0] + b[2]) / 2)
    rows = []
    for b in boxes:
        cy = (b[0] + b[2]) / 2
        if rows and abs(rows[-1][0] - cy) < 110:
            rows[-1][1].append(b)
        else:
            rows.append([cy, [b]])
    ordered = []
    for _, bs in rows:
        ordered += sorted(bs, key=lambda b: b[1])
    return ws, ordered


def backdrop_estimate(rgb, alpha):
    """The blurred backdrop under the items (sheets A, B): normalised convolution of the pixels
    where alpha is 0, so every pixel gets the backdrop colour that would be behind it."""
    w = (alpha < 2).astype(np.float32)
    out = np.zeros_like(rgb)
    den = ndimage.gaussian_filter(w, 14)
    for c in range(3):
        out[..., c] = ndimage.gaussian_filter(rgb[..., c] * w, 14) / np.maximum(den, 1e-4)
    return out


# ============================================================ one item
def cut(sheet_rgba, ws, label, box, bg):
    y0, x0, y1, x1, _ = box
    pad = 8
    Y0, X0 = max(0, y0 - pad), max(0, x0 - pad)
    Y1, X1 = min(ws.shape[0], y1 + pad), min(ws.shape[1], x1 + pad)
    rgb = sheet_rgba[Y0:Y1, X0:X1, :3].astype(np.float32)
    a_raw = sheet_rgba[Y0:Y1, X0:X1, 3].astype(np.float32)
    mine = ws[Y0:Y1, X0:X1] == label
    other = (ws[Y0:Y1, X0:X1] != label) & (ws[Y0:Y1, X0:X1] != 0)

    a = np.clip((a_raw - 20.0) / 215.0, 0, 1) * mine
    # feather a watershed cut through a neighbour's skirt
    if other.any():
        d = ndimage.distance_transform_edt(~other)
        a *= np.clip((d - 1.0) / 3.0, 0, 1)
    # drop specks not attached to the item
    parts, n = ndimage.label(a > 0.3)
    if n > 1:
        sizes = ndimage.sum(a > 0.3, parts, range(1, n + 1))
        big = sizes.max()
        for i, s in enumerate(sizes, start=1):
            if s < max(60, big * 0.004):
                a[ndimage.binary_dilation(parts == i, iterations=2) & (a < 0.99)] *= 0.0
    a[a < 0.02] = 0

    # defringe: a 3-px rim takes the nearest solid colour; wider translucency is unmixed
    solid = ndimage.binary_erosion(a > 0.94, iterations=1)
    if solid.any():
        dist, (iy, ix) = ndimage.distance_transform_edt(~solid, return_indices=True)
        near = rgb[iy, ix]
        rim = (dist <= 3.2) & ~solid
        if bg is not None:
            B = bg[Y0:Y1, X0:X1]
            ar = np.clip(a_raw / 250.0, 0.05, 1)[..., None]
            unmixed = np.clip((rgb - (1 - ar) * B) / ar, 0, 255)
        else:
            unmixed = rgb
        wide = (dist > 3.2) & (a > 0)
        rgb = np.where(rim[..., None], near, np.where(wide[..., None], unmixed, rgb))
    out = np.dstack([rgb, a * 255.0])

    ys, xs = np.nonzero(a > 0.02)
    by0, by1, bx0, bx1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    out = out[by0:by1, bx0:bx1]
    origin = (X0 + bx0, Y0 + by0)            # sheet pixel of the crop's top-left
    return out, origin


def foot_of(a):
    """(row, centre column, width) of the footprint's centre.

    The footprint of anything standing on an iso ground is about half as deep as it is wide, so its
    centre lies a quarter of the base width above the lowest opaque row. The base width is the widest
    row of the lowest quarter (a grass mound, a crate's front corners, a tub's bottom rim). "The widest
    row" alone picked a tub's upper rim, which perspective draws wider than its foot."""
    h = a.shape[0]
    solid = a > 0.5
    rows = np.nonzero(solid.any(1))[0]
    bottom = int(rows.max())
    best_w, best_c = 1, a.shape[1] / 2.0
    for y in range(int(bottom - (bottom - rows.min()) * 0.25), bottom + 1):
        xs = np.nonzero(solid[y])[0]
        if len(xs) == 0:
            continue
        w = xs[-1] - xs[0] + 1
        if w >= best_w:
            best_w, best_c = w, (xs[0] + xs[-1] + 1) / 2.0
    row = int(round(max(bottom - best_w * 0.22, bottom - (bottom - rows.min()) * 0.4)))
    xs = np.nonzero(solid[row])[0]
    centre = (xs[0] + xs[-1] + 1) / 2.0 if len(xs) else best_c
    return row, centre, best_w


def centre_on(img, cx):
    """Pad columns so column cx is the horizontal centre."""
    h, w = img.shape[:2]
    left, right = cx, w - cx
    grow = int(math.ceil(abs(left - right)))
    if left < right:
        img = np.concatenate([np.zeros((h, grow, 4), np.float32), img], axis=1)
        return img, grow
    img = np.concatenate([img, np.zeros((h, grow, 4), np.float32)], axis=1)
    return img, 0


def add_shadow(img, foot_row, width):
    """A soft contact shadow under an item that stands on nothing (sheet A)."""
    h, w = img.shape[:2]
    rx, ry = width * 0.52, width * 0.16
    extra = int(math.ceil(max(0, foot_row + ry * 1.6 - (h - 1))))
    if extra:
        img = np.concatenate([img, np.zeros((extra, w, 4), np.float32)], axis=0)
        h += extra
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = ((xx - w / 2.0) / rx) ** 2 + ((yy - foot_row) / ry) ** 2
    sh = np.clip(1.0 - d, 0, 1) ** 1.6 * 0.34
    sh = ndimage.gaussian_filter(sh, 2.0)
    a = img[..., 3] / 255.0
    col = np.array([34, 30, 44], np.float32)
    out_a = a + sh * (1 - a)
    rgb = (img[..., :3] * a[..., None] + col * (sh * (1 - a))[..., None]) / np.maximum(out_a, 1e-4)[..., None]
    return np.dstack([rgb, out_a * 255.0])


def snap_light(sheet, x, y, r=22):
    """The centre of the brightest warm pixels within r of a hand-picked sheet point."""
    y0, y1, x0, x1 = max(0, y - r), y + r + 1, max(0, x - r), x + r + 1
    p = sheet[y0:y1, x0:x1]
    rr, gg, bb, aa = p[..., 0], p[..., 1], p[..., 2], p[..., 3]
    warm = (rr > 200) & (gg > 130) & (bb < 170) & (rr - bb > 70) & (aa > 120)
    if warm.sum() < 4:
        return float(x), float(y)
    wgt = warm * (rr + gg) ** 2
    ys, xs = np.mgrid[y0:y0 + p.shape[0], x0:x0 + p.shape[1]]
    return float((xs * wgt).sum() / wgt.sum()), float((ys * wgt).sum() / wgt.sum())


def hue_sat_val(rgb):
    mx, mn = rgb.max(-1), rgb.min(-1)
    d = np.maximum(mx - mn, 1e-4)
    rr, gg, bb = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.where(mx == rr, ((gg - bb) / d) % 6, np.where(mx == gg, (bb - rr) / d + 2, (rr - gg) / d + 4)) * 60
    return hue, (mx - mn) / np.maximum(mx, 1e-4), mx


def crystal_mask(img):
    rgb = img[..., :3] / 255.0
    hue, sat, val = hue_sat_val(rgb)
    m = (hue > 245) & (hue < 320) & (sat > 0.22) & (img[..., 3] > 128)
    return ndimage.binary_opening(m, iterations=1)


def to_ice(img):
    """Violet crystals turned to ice: hue to a pale cyan, less saturated, lighter."""
    rgb = img[..., :3] / 255.0
    m = ndimage.gaussian_filter(crystal_mask(img).astype(np.float32), 0.7)
    lum = rgb @ np.array([0.3, 0.59, 0.11], np.float32)
    ice = np.stack([lum * 0.70 + 0.12, lum * 0.92 + 0.16, lum * 1.0 + 0.30], -1)
    new = rgb * (1 - m[..., None]) + np.clip(ice, 0, 1) * m[..., None]
    return np.dstack([new * 255.0, img[..., 3]])


def glow_mask(img, colour):
    """rgb = the crystal's own light (brighter facets glow more), a = where glints may run."""
    m = crystal_mask(img)
    rgb = img[..., :3] / 255.0
    lum = rgb @ np.array([0.3, 0.59, 0.11], np.float32)
    k = ndimage.gaussian_filter(m.astype(np.float32), 1.2) * np.clip(lum * 1.5, 0.25, 1.0)
    light = np.array(colour, np.float32)[None, None] * k[..., None] * 0.85
    return np.dstack([np.clip(light, 0, 1) * 255.0, np.clip(ndimage.gaussian_filter(m.astype(np.float32), 0.8), 0, 1) * 255.0])


def recolour_ground(img, foot_row, ground, weight_out=None):
    """The grass skirt and its flowers take the island's ground colour, shading kept. weight_out (a list) receives the
    per-pixel share of the skirt that was recoloured."""
    h, w = img.shape[:2]
    rgb = img[..., :3] / 255.0
    a = img[..., 3] / 255.0
    mx, mn = rgb.max(-1), rgb.min(-1)
    sat = (mx - mn) / np.maximum(mx, 1e-4)
    rr, gg, bb = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue, _, _ = hue_sat_val(rgb)
    green = (hue > 58) & (hue < 165) & (sat > 0.25) & (mx > 0.10) & (a > 0.02)
    bottom = int(np.nonzero(a.max(1) > 0.1)[0].max())
    half = max(8, bottom - foot_row)
    top = foot_row - half * 1.25
    band = np.clip((np.arange(h, dtype=np.float32) - top) / (half * 0.5), 0, 1)[:, None]
    grass = green * band
    # flowers and pebbles sitting in the grass
    near = ndimage.binary_dilation(green, iterations=12)
    lum = 0.3 * rr + 0.59 * gg + 0.11 * bb
    bloom = near & ~green & (a > 0.02) & ((lum > 0.62) | ((sat > 0.45) & (hue < 58))) & (np.arange(h)[:, None] > top + half * 0.5)
    wgt = np.maximum(grass, bloom * band * 0.9)
    wgt = ndimage.gaussian_filter(wgt.astype(np.float32), 0.8)
    lo, mid, hi = (hexc(c) / 255.0 for c in GROUNDS[ground])
    sel = green & (band[:, 0][:, None] > 0.5)
    if sel.sum() > 20:
        l0, l1 = np.percentile(lum[sel], 4), np.percentile(lum[sel], 97)
    else:
        l0, l1 = 0.1, 0.8
    t = np.clip((lum - l0) / max(1e-3, l1 - l0), 0, 1)
    ramp = np.where((t < 0.5)[..., None], lo + (mid - lo) * (t / 0.5)[..., None], mid + (hi - mid) * ((t - 0.5) / 0.5)[..., None])
    # the painted dark outline stays dark
    ramp = np.where((lum < 0.16)[..., None], lo * 0.55, ramp)
    new = rgb * (1 - wgt[..., None]) + ramp * wgt[..., None]
    if weight_out is not None:
        weight_out.append(np.clip(wgt, 0, 1))
    return np.dstack([np.clip(new, 0, 1) * 255.0, img[..., 3]])


def skirt_snow(img, foot_row):
    """Snow over the grass skirt only, for snowy weather on a grass island: the skirt's pixels in the snow ramp, alpha =
    the prop's alpha x how much of the pixel is skirt. IslandView lays it over the prop like a snow cap, so a prop on a
    snowed-over yard stops standing on a green mound."""
    wgt = []
    snowy = recolour_ground(img, foot_row, "snow", wgt)
    a = img[..., 3] / 255.0 * np.clip(wgt[0] * 1.15, 0, 1)
    return np.dstack([snowy[..., :3], a * 255.0])


# The painted windmill's sails are painted onto its body. To turn them (MiT/UI Sway, spin), they are lifted off:
# measured on Art/decor/mill.png as cut (sprite pixels) — the hub, and the centre of each sail's tip (UR, UL, LL, LR).
MILL = {"hub": (119.5, 78.0), "tips": [(174.25, 10.0), (71.25, 25.0), (66.25, 137.0), (176.75, 137.0)], "half": 12.5}


def _rect_mask(w, h, hub, tip, half, r0=0.0, ext=3.0, ss=4):
    m = Image.new("L", (w * ss, h * ss), 0)
    hb, t = np.array(hub), np.array(tip)
    v = t - hb
    u = v / np.linalg.norm(v)
    n = np.array([-u[1], u[0]])
    p0, p1 = hb + u * r0, t + u * ext
    pts = [p0 + n * half, p1 + n * half, p1 - n * half, p0 - n * half]
    ImageDraw.Draw(m).polygon([(x * ss, y * ss) for x, y in pts], fill=255)
    return np.asarray(m.resize((w, h), Image.LANCZOS), np.float32) / 255


def split_sails(img, spec):
    """(body, sails, side): the windmill with its sails lifted off and the roof and tower rebuilt behind them, and a
    square sails sprite with the hub at its centre — four copies of the cleanest sail (the upper right one, which
    crosses only sky) at right angles, so the cross turns without a lopsided sail.

    Behind the sails: the roof is a cone and the tower a tapered drum, both symmetric about the tower's axis, so the
    hidden half of the roof is the mirror of the visible one and the tower's outline is its taper fitted on rows no
    sail crosses. Inside those outlines every hidden pixel takes, row by row, the colours of the nearest visible roof or
    wall pixels either side (shingles and stone courses run horizontally); anything else the sails covered is sky."""
    h, w = img.shape[:2]
    rgb, a = img[..., :3], img[..., 3] / 255.0
    hub, tips, half = spec["hub"], spec["tips"], spec["half"]
    hue, sat, val = hue_sat_val(rgb / 255.0)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    hub_d = np.hypot(xx - hub[0], yy - hub[1])

    # ---- the sails ----
    roofish = ((hue < 24) | (hue > 340)) & (sat > 0.45) & (val > 0.35)
    tmpl = _rect_mask(w, h, hub, tips[0], half, r0=6.0) * (~roofish)
    R = int(math.ceil(max(math.hypot(t[0] - hub[0], t[1] - hub[1]) for t in tips) + 6))
    side = 2 * R + 2

    def square(arr):
        out = np.zeros((side, side, 4), np.float32)
        ox, oy = int(round(hub[0])) - side // 2, int(round(hub[1])) - side // 2
        for y in range(side):
            sy = oy + y
            if 0 <= sy < h:
                x0, x1 = max(0, ox), min(w, ox + side)
                out[y, x0 - ox:x1 - ox] = arr[sy, x0:x1]
        return out

    one = img.copy()
    one[..., 3] = img[..., 3] * tmpl
    one = to_image(square(one))
    sails = Image.new("RGBA", one.size, (0, 0, 0, 0))
    for k in range(4):
        sails.alpha_composite(one.rotate(90 * k, resample=Image.BICUBIC, center=(side / 2.0, side / 2.0)))
    hb = img.copy()
    hb[..., 3] = img[..., 3] * np.clip(13.5 - hub_d, 0, 1)
    sails.alpha_composite(to_image(square(hb)))

    # ---- the body behind them ----
    cover = np.maximum(np.maximum.reduce([_rect_mask(w, h, hub, t, half + 4.0, ext=6.0) for t in tips]), np.clip(16.0 - hub_d, 0, 1))
    gone = cover >= 0.02
    whiteish = (sat < 0.36) & (val > 0.42) & (hue > 8) & (hue < 70)
    roof_src = roofish & ~gone & (a > 0.5) & (yy < hub[1] + 40) & (xx > hub[0] - 60)
    wall_src = whiteish & ~gone & (a > 0.5) & (yy < hub[1] + 122)
    rows, halves, centres = [], [], []
    for y in range(int(hub[1] + 80), int(hub[1] + 122)):
        xs = np.nonzero(wall_src[y] | ((a[y] > 0.5) & ~gone[y] & (val[y] > 0.42) & (sat[y] < 0.5)))[0]
        xs = xs[(xs > hub[0] - 40) & (xs < hub[0] + 80)]
        if len(xs) >= 20:
            rows.append(y); halves.append((xs[-1] - xs[0]) / 2.0); centres.append((xs[-1] + xs[0]) / 2.0)
    axis = float(np.median(centres))
    b1, b0 = np.polyfit(rows, halves, 1)
    roof = np.zeros((h, w), np.uint8)
    cv2.fillConvexPoly(roof, cv2.convexHull(np.argwhere(roof_src)[:, ::-1].astype(np.int32)), 1)
    roof = np.maximum(roof, roof[:, np.clip(np.round(2 * axis - np.arange(w)).astype(int), 0, w - 1)])
    # one cone, not two peaks: the hull of the roof and its mirror
    both = np.argwhere(roof > 0)[:, ::-1].astype(np.int32)
    roof = np.zeros((h, w), np.uint8)
    cv2.fillConvexPoly(roof, cv2.convexHull(both), 1)
    roof_bottom = int(np.nonzero(roof.any(1))[0].max())
    wall = np.zeros((h, w), np.uint8)
    for y in range(roof_bottom - 3, int(hub[1] + 122)):
        hw = b0 + b1 * y
        wall[y, max(0, int(round(axis - hw))):min(w, int(round(axis + hw)) + 1)] = 1

    out_rgb, out_a = rgb.copy(), a.copy()
    out_a[gone] = 0.0

    def row_fill(zone, src):
        for y in range(h):
            xs = np.nonzero(zone[y])[0]
            good = np.nonzero(src[y])[0]
            if len(xs) == 0 or len(good) == 0:
                continue
            for x in xs:
                li, ri = good[good < x], good[good > x]
                if len(li) and len(ri):
                    t = (x - li[-1]) / max(1, ri[0] - li[-1])
                    out_rgb[y, x] = rgb[y, li[-1]] * (1 - t) + rgb[y, ri[0]] * t
                else:
                    out_rgb[y, x] = rgb[y, li[-1] if len(li) else ri[0]]
                out_a[y, x] = 1.0

    roof_zone = gone & (roof > 0)
    wall_zone = gone & (wall > 0) & ~roof_zone
    row_fill(roof_zone, roof_src)
    row_fill(wall_zone, wall_src)
    soft = ndimage.uniform_filter(out_rgb, size=(3, 3, 1))
    out_rgb = np.where((roof_zone | wall_zone)[..., None], soft, out_rgb)
    return np.dstack([out_rgb, out_a * 255.0]), sails, side


def to_image(arr):
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


# ============================================================ run
def main():
    args = sys.argv[1:]
    sheet_dir = args[args.index("--sheet") + 1] if "--sheet" in args else None
    detect_dir = args[args.index("--detect") + 1] if "--detect" in args else None
    catalog = args[args.index("--catalog") + 1] if "--catalog" in args else CATALOG
    os.makedirs(OUT, exist_ok=True)

    cache = {}
    for key, f in SHEETS.items():
        im = np.asarray(Image.open(f).convert("RGBA")).astype(np.float32)
        ws, boxes = segment(im[..., 3])
        bg = backdrop_estimate(im[..., :3], im[..., 3]) if BACKDROP[key] else None
        cache[key] = (im, ws, boxes, bg)
        print(key, len(boxes), "items")
        if detect_dir:
            vis = Image.new("RGBA", (im.shape[1], im.shape[0]), (60, 60, 70, 255))
            vis.alpha_composite(to_image(im))
            dr = ImageDraw.Draw(vis)
            for i, (y0, x0, y1, x1, _) in enumerate(boxes):
                dr.rectangle((x0, y0, x1, y1), outline=(255, 0, 0, 255), width=2)
                dr.text((x0 + 4, y0 + 2), str(i), fill=(255, 255, 0, 255))
            os.makedirs(detect_dir, exist_ok=True)
            vis.convert("RGB").save(os.path.join(detect_dir, "detect_" + key + ".png"))

    rows = []
    cuts = {}
    for name, (key, index, grounds, shipped) in ITEMS.items():
        if not shipped and not sheet_dir:
            continue
        im, ws, boxes, bg = cache[key]
        box = boxes[index]
        img, (ox, oy) = cut(im, ws, box[4], box, bg)
        frow, fcx, fw = foot_of(img[..., 3] / 255.0)
        lights = []
        for sx, sy, kind in LIGHTS.get(name, []):
            x, y = snap_light(im, sx, sy)
            lights.append((x - ox, y - oy, kind))
        img, shift = centre_on(img, fcx)
        lights = [(x + shift, y, k) for x, y, k in lights]
        if key == "A":
            img = add_shadow(img, frow, fw)
        h, w = img.shape[:2]
        scale = min(1.0, MAX_SIDE / max(h, w))
        size = (max(1, round(w * scale)), max(1, round(h * scale)))

        def emit(arr, suffix):
            pic = to_image(arr)
            if scale < 1.0:
                pic = pic.resize(size, Image.LANCZOS)
            if shipped:
                pic.save(os.path.join(OUT, name + suffix + ".png"))
            return pic

        spin = None
        if name == "mill":
            img, sails, side = split_sails(img, MILL)
            if shipped:
                sails.save(os.path.join(OUT, name + "_sails.png"))
            hub = MILL["hub"]
            spin = (hub[0] / w, 1.0 - hub[1] / h, side / w)
        base = emit(img, "")
        variants = {}
        for g in grounds:
            v = recolour_ground(img, frow, g)
            if g == "ice":
                v = to_ice(v)
            variants[g] = emit(v, "_" + g)
            if name in GLOWS and g in GLOWS[name]:
                emit(glow_mask(v, GLOWS[name][g]), "_" + g + "_glow")
        if shipped and key != "A":
            sk = to_image(skirt_snow(img, frow))
            if scale < 1.0:
                sk = sk.resize(size, Image.LANCZOS)
            sk.save(os.path.join(SNOW, name + "_skirt.png"))
        cuts[name] = (base, variants, shipped)
        if shipped:
            rows.append((name, (h - frow) / h, fw / w, [(x / w, 1.0 - y / h, k) for x, y, k in lights], size, grounds, key != "A", spin))
        print("  %-16s %s%-3d %s %4dx%-4d pivot %.3f foot %.2f lights %d %s" % (
            name, key, index, "ship" if shipped else "    ", size[0], size[1], (h - frow) / h, fw / w, len(lights), " ".join(grounds)))

    # the older painted props (Art/farm) stand on the same grass skirts: they get the same snow over it
    for name in FARM_SKIRTS:
        arr = np.asarray(Image.open(os.path.join("Assets/Resources/Art/farm", name + ".png")).convert("RGBA")).astype(np.float32)
        frow, _, _ = foot_of(arr[..., 3] / 255.0)
        to_image(skirt_snow(arr, frow)).save(os.path.join(SNOW, name + "_skirt.png"))
        print("  skirt snow for farm/" + name)

    write_catalog(catalog, rows)
    if sheet_dir:
        contact(sheet_dir, cuts)


def write_catalog(path, rows):
    lines = [
        "// GENERATED by Tools/slice_decor.py — do not edit by hand; change the script and run it again.",
        "using System.Collections.Generic;",
        "using UnityEngine;",
        "",
        "namespace LQFarm",
        "{",
        "    /// <summary>The painted yard props cut from the owner's decor sheets (Resources/Art/decor), measured by",
        "    /// Tools/slice_decor.py: where each one touches the ground, how wide its footprint is, and where its",
        "    /// lanterns and fires are. IslandView.Places says which island stands what where.</summary>",
        "    public static class DecorCatalog",
        "    {",
        "        public struct Light { public Vector2 at; public bool fire; }",
        "",
        "        public struct Entry",
        "        {",
        "            /// <summary>Height of the footprint's centre above the sprite's bottom edge, 0..1 of its height.</summary>",
        "            public float pivotY;",
        "            /// <summary>Footprint width as a share of the sprite's width.</summary>",
        "            public float footW;",
        "            /// <summary>Aspect: height / width.</summary>",
        "            public float aspect;",
        "            /// <summary>Lantern flames and fires, normalised (0,0 bottom-left .. 1,1 top-right).</summary>",
        "            public Light[] lights;",
        "            /// <summary>Grounds this prop has a recoloured skirt for (snow, ash, slate, sand, ice).</summary>",
        "            public string[] grounds;",
        "            /// <summary>Painted standing on a grass skirt (false: it stands on nothing and has a contact shadow).</summary>",
        "            public bool skirt;",
        "            /// <summary>A part lifted off to turn (the windmill's sails, Art/decor/<name>_sails): its hub, normalised like",
        "            /// the lights, and its square's side as a share of the sprite's width. spinSize 0: nothing turns.</summary>",
        "            public Vector2 spinAt;",
        "            public float spinSize;",
        "        }",
        "",
        "        public static readonly Dictionary<string, Entry> All = new Dictionary<string, Entry>",
        "        {",
    ]
    inv = lambda v: ("%.3f" % v) + "f"
    for name, py, fw, lights, size, grounds, skirt, spin in rows:
        ls = ", ".join("new Light { at = new Vector2(%s, %s), fire = %s }" % (inv(x), inv(y), "true" if k == "fire" else "false") for x, y, k in lights)
        gs = ", ".join('"%s"' % g for g in grounds)
        lines.append('            { "%s", new Entry { pivotY = %s, footW = %s, aspect = %s, skirt = %s, %slights = new Light[] { %s }, grounds = new string[] { %s } } },'
                     % (name, inv(py), inv(fw), inv(size[1] / size[0]), "true" if skirt else "false",
                        ("spinAt = new Vector2(%s, %s), spinSize = %s, " % (inv(spin[0]), inv(spin[1]), inv(spin[2]))) if spin else "", ls, gs))
    lines += [
        "        };",
        "",
        "        public static bool Has(string name) { return All.ContainsKey(name); }",
        "    }",
        "}",
        "",
    ]
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print("catalog ->", path)


def contact(out_dir, cuts):
    """Every cut on light, dark and checker grounds, with its ground variants; unshipped cuts marked."""
    os.makedirs(out_dir, exist_ok=True)
    tiles = []
    for name, (im, variants, shipped) in cuts.items():
        tiles.append((name + ("" if shipped else "  (spare)"), im))
        for g, v in variants.items():
            tiles.append((name + "_" + g, v))
    cell = 340
    per_row = 6
    for bgname in ("light", "dark", "checker"):
        n_rows = (len(tiles) + per_row - 1) // per_row
        for page in range(0, n_rows, 4):
            chunk = tiles[page * per_row:(page + 4) * per_row]
            if not chunk:
                break
            rows_here = (len(chunk) + per_row - 1) // per_row
            W, H = per_row * cell, rows_here * (cell + 18)
            sheet = Image.new("RGBA", (W, H), (236, 232, 222, 255) if bgname == "light" else (34, 36, 44, 255))
            d = ImageDraw.Draw(sheet)
            if bgname == "checker":
                for yy in range(0, H, 16):
                    for xx in range(0, W, 16):
                        c = (200, 60, 200) if ((xx // 16 + yy // 16) % 2) else (60, 200, 90)
                        d.rectangle((xx, yy, xx + 15, yy + 15), fill=c)
            for i, (name, im) in enumerate(chunk):
                x, y = (i % per_row) * cell, (i // per_row) * (cell + 18)
                t = im if max(im.size) <= cell - 10 else im.resize((int(im.width * (cell - 10) / max(im.size)), int(im.height * (cell - 10) / max(im.size))), Image.LANCZOS)
                sheet.alpha_composite(t, (x + (cell - t.width) // 2, y + 18 + (cell - t.height) // 2))
                d.text((x + 4, y + 2), name, fill=(200, 30, 30) if bgname == "light" else (255, 230, 90))
            sheet.convert("RGB").save(os.path.join(out_dir, "decor_%s_%d.png" % (bgname, page // 4)))
    print("contact sheets ->", out_dir)


if __name__ == "__main__":
    main()
