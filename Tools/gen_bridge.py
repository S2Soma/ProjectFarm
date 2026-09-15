"""The rope bridge between two islands: wooden planks slung on ropes.

    uv run --with pillow --with numpy python Tools/gen_bridge.py

What it replaces
----------------
A causeway of cloud. It was smooth-lit, so it read as moulded plastic next to the painted
islands, it did not belong to anything else on the map, and its ribbons ended in hard vertical
cuts right on the islands' tips. The farm is built of wood — fences, posts, the gate lanterns —
so the bridge is too: planks on two ropes, a rope handrail either side, a post at each corner,
and two of the gate lanterns' own lamps hanging from the rail.

Outputs (Assets/Resources/Art/bridge/), in COLOUR (wood keeps its hue; the world's time-of-day
light multiplies it like every other prop):

  bridge_strip.png   1024 wide, one row per ribbon; tiles horizontally (U repeats)
                       planks   the walking surface, planks across the path with gaps between
                       edge     the planks' end grain along the near side
                       rail     a twisted rope with a hanging string every 32 world units
  bridge_lantern.png the lamp from the fence gate sprite, cut free of its post

Scale: 3.2 px per world unit; one 1024 px tile is 320 units = 20 planks of 16 = 10 strings of 32.

The row layout below MUST match BridgeRibbon.Rows.
"""
from PIL import Image
import numpy as np
import math, os, random

OUT = "Assets/Resources/Art/bridge"
os.makedirs(OUT, exist_ok=True)
SS = 2
W = 1024
PAD = 24
PPU = 3.2

ROWS = {
    "planks": (PAD, 144),
    "edge":   (PAD + 144 + 2 * PAD, 24),
    "rail":   (PAD + 144 + 24 + 4 * PAD, 112),
}
TEX_H = PAD + 144 + 24 + 112 + 6 * PAD      # 424

WOOD_LIGHT = np.array([209, 154, 88], np.float32)    # measured on the fence post art
WOOD_MID = np.array([148, 100, 56], np.float32)
WOOD_DARK = np.array([107, 68, 36], np.float32)
ROPE_LIGHT = np.array([214, 184, 130], np.float32)
ROPE_DARK = np.array([138, 104, 62], np.float32)


def value_noise(h, w, cell_y, cell_x, seed):
    rng = np.random.RandomState(seed)
    gh, gw = int(h / cell_y) + 3, int(w / cell_x) + 3
    g = (rng.rand(gh, gw) * 255).astype(np.uint8)
    return np.asarray(Image.fromarray(g).resize((int(gw * cell_x), int(gh * cell_y)), Image.BICUBIC),
                      np.float32)[:h, :w] / 255.0


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def plank_layout():
    """Same random planks for the top face and the end grain."""
    rng = random.Random(5)
    pitch = 16 * PPU * SS
    gap = 3.2 * PPU * SS
    out = []
    for k in range(20):
        x0 = k * pitch + rng.uniform(-1.5, 1.5) * SS
        x1 = x0 + pitch - gap + rng.uniform(-1.5, 1.5) * SS
        out.append(dict(x0=x0, x1=x1, top=rng.uniform(1, 7) * SS, bot=rng.uniform(1, 7) * SS,
                        tone=rng.uniform(-0.18, 0.18), knot=rng.random() < 0.3,
                        kx=rng.uniform(0.2, 0.8), ky=rng.uniform(0.25, 0.75)))
    return out


def planks(h):
    """Planks across the path: each its own tone, grain running along it (vertical in the texture,
    since a plank crosses the direction of travel), a lit left bevel, a shaded right one, worn
    ends, and the two ropes they are lashed to running under their ends."""
    Ws, Hs = W * SS, h * SS
    rgb = np.zeros((Hs, Ws, 3), np.float32)
    a = np.zeros((Hs, Ws), np.float32)
    ys, xs = np.mgrid[0:Hs, 0:Ws].astype(np.float32)
    grain = value_noise(Hs, Ws, 60 * SS, 2.2 * SS, 7)
    fine = value_noise(Hs, Ws, 9 * SS, 1.2 * SS, 8)
    for p in plank_layout():
        x0, x1, top, bot = p["x0"], p["x1"], p["top"], Hs - p["bot"]
        base = WOOD_MID * (1 + p["tone"])
        inside = smooth(x0 - 0.8 * SS, x0 + 0.8 * SS, xs) * (1 - smooth(x1 - 0.8 * SS, x1 + 0.8 * SS, xs))
        inside *= smooth(top - 0.8 * SS, top + 0.8 * SS, ys) * (1 - smooth(bot - 0.8 * SS, bot + 0.8 * SS, ys))
        u = np.clip((xs - x0) / max(1, x1 - x0), 0, 1)
        col = base[None, None, :] * (0.90 + 0.20 * grain[..., None]) * (0.96 + 0.08 * fine[..., None])
        col = col + (WOOD_LIGHT - base) * (0.55 * (1 - smooth(0.0, 0.14, u)))[..., None]     # lit bevel
        col = col * (1 - 0.30 * smooth(0.80, 1.0, u))[..., None]                            # shaded side
        ends = np.minimum(ys - top, bot - ys) / SS
        col = col * (1 - 0.25 * (1 - smooth(0, 6, ends)))[..., None]                        # worn ends
        if p["knot"]:
            kx, ky = x0 + (x1 - x0) * p["kx"], top + (bot - top) * p["ky"]
            kd = np.hypot(xs - kx, (ys - ky) / 2.2) / (5 * SS)
            col = col * (1 - 0.35 * np.clip(1 - kd, 0, 1))[..., None]
        m = inside > 0.001
        rgb[m] = col[m] * inside[m][:, None] + rgb[m] * (1 - inside[m][:, None])
        a = np.maximum(a, inside)

    for ry in (14 * SS, Hs - 14 * SS):
        band = smooth(3.8 * SS, 2.2 * SS, np.abs(ys - ry))
        twist = 0.5 + 0.5 * np.sin((xs + (ys - ry) * 1.6) / (3.2 * SS))
        col = ROPE_DARK + (ROPE_LIGHT - ROPE_DARK) * twist[..., None]
        col = col * (1 - 0.35 * smooth(2.2 * SS, 3.6 * SS, np.abs(ys - ry)))[..., None]
        rgb = col * band[..., None] + rgb * (1 - band[..., None])
        a = np.maximum(a, band)
    return rgb, a


def edge(h):
    """End grain along the near side: dark wood, the same planks and gaps."""
    Ws, Hs = W * SS, h * SS
    ys, xs = np.mgrid[0:Hs, 0:Ws].astype(np.float32)
    rgb = np.zeros((Hs, Ws, 3), np.float32)
    a = np.zeros((Hs, Ws), np.float32)
    for p in plank_layout():
        x0, x1 = p["x0"], p["x1"]
        inside = smooth(x0 - 0.8 * SS, x0 + 0.8 * SS, xs) * (1 - smooth(x1 - 0.8 * SS, x1 + 0.8 * SS, xs))
        inside *= 1 - smooth(Hs - 5 * SS, Hs - 2 * SS, ys)
        t = ys / Hs
        col = (WOOD_DARK * (1 + p["tone"]))[None, None, :] * (1 - 0.35 * t)[..., None]
        m = inside > 0.001
        rgb[m] = col[m] * inside[m][:, None] + rgb[m] * (1 - inside[m][:, None])
        a = np.maximum(a, inside)
    return rgb, a


def rail(h):
    """A twisted rope along the top, and a string hanging from it every 32 units."""
    Ws, Hs = W * SS, h * SS
    ys, xs = np.mgrid[0:Hs, 0:Ws].astype(np.float32)
    rgb = np.zeros((Hs, Ws, 3), np.float32)
    a = np.zeros((Hs, Ws), np.float32)
    ry = 9 * SS
    pitch = 32 * PPU * SS
    for k in range(10):
        cx = (k + 0.5) * pitch
        band = smooth(3.0 * SS, 1.6 * SS, np.abs(xs - cx)) * smooth(ry, ry + 4 * SS, ys) \
            * (1 - smooth(Hs - 6 * SS, Hs - 2 * SS, ys))
        shade = 0.85 + 0.15 * np.sin((ys + xs) / (2.5 * SS))
        rgb = (ROPE_DARK * shade[..., None]) * band[..., None] + rgb * (1 - band[..., None])
        a = np.maximum(a, band)
        knot = smooth(5.5 * SS, 3.5 * SS, np.hypot(xs - cx, (ys - (Hs - 8 * SS)) * 1.3))
        rgb = (ROPE_DARK * 0.85) * knot[..., None] + rgb * (1 - knot[..., None])
        a = np.maximum(a, knot)
    band = smooth(6.0 * SS, 4.2 * SS, np.abs(ys - ry))
    twist = 0.5 + 0.5 * np.sin((xs + (ys - ry) * 1.8) / (4.0 * SS))
    col = ROPE_DARK + (ROPE_LIGHT - ROPE_DARK) * twist[..., None]
    col = col * (1 - 0.35 * smooth(3.8 * SS, 6.0 * SS, np.abs(ys - ry)))[..., None]
    col = col * (1 - 0.12 * smooth(ry - 2 * SS, ry + 6 * SS, ys))[..., None]
    rgb = col * band[..., None] + rgb * (1 - band[..., None])
    a = np.maximum(a, band)
    return rgb, a


def strip():
    tex = Image.new("RGBA", (W, TEX_H), (0, 0, 0, 0))
    for name, fn in (("planks", planks), ("edge", edge), ("rail", rail)):
        top, h = ROWS[name]
        rgb, al = fn(h)
        img = Image.fromarray(np.dstack([np.clip(rgb, 0, 255), np.clip(al * 255, 0, 255)]).astype(np.uint8))
        tex.alpha_composite(img.resize((W, h), Image.LANCZOS), (0, top))
    arr = np.asarray(tex).copy()
    # transparent texels keep a wood colour, so bilinear filtering never pulls in dark fringes
    empty = arr[..., 3] == 0
    arr[empty, 0], arr[empty, 1], arr[empty, 2] = 120, 80, 45
    Image.fromarray(arr).save(os.path.join(OUT, "bridge_strip.png"))
    print("  bridge_strip", tex.size, ROWS)


def lantern():
    """The lamp off the fence gate sprite, cut free of its arm, so the bridge lanterns are the
    same object as the ones on the gates."""
    src = Image.open("Assets/Resources/Art/gen/fence_gate.png").convert("RGBA")
    crop = src.crop((60, 40, 96, 114))
    a = np.asarray(crop).copy()
    a[:14, :8, 3] = 0              # what is left of the arm, above the lamp's cap
    Image.fromarray(a).save(os.path.join(OUT, "bridge_lantern.png"))
    print("  bridge_lantern", crop.size)


if __name__ == "__main__":
    print("bridge ->", OUT)
    for old in ("bridge_cap.png", "bridge_cap.png.meta", "bridge_lamp.png", "bridge_lamp.png.meta"):
        p = os.path.join(OUT, old)
        if os.path.exists(p):
            os.remove(p)
    strip()
    lantern()
