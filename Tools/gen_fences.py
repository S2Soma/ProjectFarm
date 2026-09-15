"""Fence segments sheared to the field's isometric slope.

    uv run --with pillow --with numpy python Tools/gen_fences.py

The painted fence segments rise at ~0.35 (fence) and fall at ~0.35 (fence_lamp), but every
edge of the isometric field runs at exactly 0.5. Chained along a 0.5 line, each segment's far
post landed ~20 px off the next segment's near post, so the fence read as a row of separate,
crooked pieces — and the same unflipped sprite was used on BOTH sides of the field, so the
right-hand run zigzagged against its own direction.

A VERTICAL shear fixes the slope without touching the art's character: every column moves as a
unit, so posts stay perfectly vertical and only the rails tilt. The output also records where
each post's base lands, which is what IslandView chains on.
"""
from PIL import Image
import numpy as np, os

SRC = "Assets/Resources/Art/farm"
OUT = "Assets/Resources/Art/gen"

# name: (left post base (x, y), right post base (x, y)) measured on the source, y down
SPECS = {
    "fence":      ((20, 131), (131, 92)),     # rises to the right
    "fence_lamp": ((12, 97), (112, 132)),     # falls to the right
}
TARGET = 0.5

for name, ((xl, yl), (xr, yr)) in SPECS.items():
    src = Image.open(os.path.join(SRC, name + ".png")).convert("RGBA")
    a = np.asarray(src).astype(np.float32)
    H, W = a.shape[:2]
    slope = (yr - yl) / float(xr - xl)            # image rows per column (y down)
    want = -TARGET if slope < 0 else TARGET
    k = want - slope                              # extra rows per column
    shift = np.array([k * (x - xl) for x in range(W)])
    pad_top = int(np.ceil(max(0.0, -shift.min()))) + 2
    pad_bot = int(np.ceil(max(0.0, shift.max()))) + 2
    out = np.zeros((H + pad_top + pad_bot, W, 4), np.float32)
    ys = np.arange(out.shape[0], dtype=np.float32)
    for x in range(W):
        src_y = ys - pad_top - shift[x]           # inverse map, linear filtering
        y0 = np.floor(src_y).astype(int); f = (src_y - y0)[:, None]
        ok0 = (y0 >= 0) & (y0 < H); ok1 = (y0 + 1 >= 0) & (y0 + 1 < H)
        c0 = np.where(ok0[:, None], a[np.clip(y0, 0, H - 1), x], 0)
        c1 = np.where(ok1[:, None], a[np.clip(y0 + 1, 0, H - 1), x], 0)
        out[:, x] = c0 * (1 - f) + c1 * f
    img = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))
    img.save(os.path.join(OUT, name + "_iso.png"))
    bl = (xl, yl + pad_top + shift[xl]); br = (xr, yr + pad_top + shift[xr])
    Hn = out.shape[0]
    print(f"{name}_iso {W}x{Hn}  left base {bl[0]:.1f},{bl[1]:.1f}  right base {br[0]:.1f},{br[1]:.1f}"
          f"  pivot ({bl[0]/W:.4f}, {1 - bl[1]/Hn:.4f})  step ({br[0]-bl[0]:.1f}, {-(br[1]-bl[1]):.1f})")


# ---------------------------------------------------------------------------------------------
# Pieces: one post, one rail span, one lantern gate post.
#
# Chaining whole segments put a post at BOTH ends of every segment, and the two posts that met at
# each joint were never quite on top of each other — the fence read as a row of doubled posts.
# IslandView now places exactly one post per node and a rail between each pair, so a shared post
# cannot double. Pivots are the post's base centre (for the rail: the base of the post it hangs
# off, which lies left of the crop — Unity accepts a pivot outside 0..1).
#
# Measured on the sheared sprites above by column opacity (see the IslandView constants):
#   fence_iso       left post centre x 22.5, base row 160; next post +108 x, -55 rows
#   fence_lamp_iso  right post centre x 115.5, base row 166
# ---------------------------------------------------------------------------------------------
def crop(name, x0, x1, out):
    im = Image.open(os.path.join(OUT, name + ".png"))
    im.crop((x0, 0, x1, im.height)).save(os.path.join(OUT, out + ".png"))
    print(f"{out}: cols {x0}..{x1} of {name}, {x1 - x0}x{im.height}")


crop("fence_iso", 0, 44, "fence_post")        # pivot (22.5/44, 7/167)
crop("fence_iso", 42, 114, "fence_rail")      # pivot ((22.5-42)/72, 7/167)
crop("fence_lamp_iso", 96, 193, "fence_gate") # pivot ((115.5-96)/97, 16/182)
