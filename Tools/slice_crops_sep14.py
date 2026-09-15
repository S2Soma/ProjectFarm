"""Cut the Sep 14/15 crop sheets into Resources/Art/crops_gen/<crop>_<stage>.png.

    uv run --with pillow --with numpy --with scipy python Tools/slice_crops_sep14.py

The three sheets (project root, "ChatGPT Image Sep 14, 2026, …") carry REAL alpha, so plants are
found as connected regions of solid alpha rather than keyed off a painted background. Only the
plant is kept: the soft glow painted round some of them (mushrooms, pineapple) is dropped — the
game draws its own glow for mutations — and nothing below the plant's own base is taken.

Stages are 1 sprout, 2 growing/flowering, 3 ripe, which is what Art.Plant expects.
"""
from PIL import Image
import numpy as np
from scipy import ndimage
import os, re, uuid

OUT = "Assets/Resources/Art/crops_gen"
META = os.path.join(OUT, "tomato_3.png.meta")
SHEETS = {
    "A": "ChatGPT Image Sep 14, 2026, 08_05_29 PM.png",
    "B": "ChatGPT Image Sep 14, 2026, 08_05_38 PM.png",
    "C": "ChatGPT Image Sep 14, 2026, 08_08_13 PM.png",
    # Sep 15: the big crops (banana, coconut, orange, apple) and a new pumpkin
    "D": "ChatGPT Image Sep 15, 2026, 08_10_46 AM.png",
}
# crop -> three (sheet, index[, "top"|"bottom"]) in stage order; indices from the detection pass
PICKS = {
    "tomato":     [("A", 15), ("A", 16), ("A", 17)],
    "watermelon": [("B", 0), ("B", 1), ("B", 2)],
    "strawberry": [("B", 9), ("B", 10), ("B", 11)],
    "corn":       [("B", 15), ("B", 16), ("B", 17)],
    "wheat":      [("B", 30), ("B", 31), ("B", 32)],
    "grape":      [("C", 0), ("C", 1), ("C", 2)],
    "mushroom":   [("C", 3), ("C", 4), ("C", 5)],
    "eggplant":   [("C", 6), ("C", 7), ("C", 8, "top")],
    "pineapple":  [("C", 9), ("C", 10), ("C", 8, "bottom")],
    "banana":     [("D", 2), ("D", 3), ("D", 0)],
    "coconut":    [("D", 4), ("D", 1, "left"), ("D", 1, "right")],
    "pumpkin":    [("D", 5), ("D", 6), ("D", 7)],
    "orange":     [("D", 10), ("D", 8), ("D", 9)],
    "apple":      [("D", 11), ("D", 12), ("D", 13)],
}


def detect(im):
    a = np.asarray(im)[..., 3]
    solid = a > 90
    lab, n = ndimage.label(ndimage.binary_dilation(solid, iterations=6))
    boxes = []
    for i, sl in enumerate(ndimage.find_objects(lab)):
        area = (solid[sl] & (lab[sl] == i + 1)).sum()
        if area < 800:
            continue
        ys, xs = sl
        boxes.append((xs.start, ys.start, xs.stop, ys.stop, i + 1))
    boxes.sort(key=lambda b: ((b[1] + b[3]) // 2 // 120, b[0]))
    return lab, solid, boxes


def cut(im, lab, solid, box, half=None):
    x0, y0, x1, y1, label = box
    arr = np.asarray(im).copy()
    region = lab == label
    if half in ("top", "bottom"):
        # two plants merged into one box: split on the emptiest row in its middle third
        rows = solid[y0:y1, x0:x1].sum(axis=1)
        lo, hi = (y1 - y0) // 3, 2 * (y1 - y0) // 3
        cutrow = y0 + lo + int(np.argmin(rows[lo:hi]))
        mask = np.zeros_like(region)
        if half == "top":
            mask[:cutrow] = True
        else:
            mask[cutrow:] = True
        region &= mask
    elif half in ("left", "right"):
        # side by side: split on the emptiest column in the middle third
        colsum = (solid[y0:y1, x0:x1] & region[y0:y1, x0:x1]).sum(axis=0)
        lo, hi = (x1 - x0) // 3, 2 * (x1 - x0) // 3
        cutcol = x0 + lo + int(np.argmin(colsum[lo:hi]))
        mask = np.zeros_like(region)
        if half == "left":
            mask[:, :cutcol] = True
        else:
            mask[:, cutcol:] = True
        region &= mask
    keep = region & solid
    if half in ("left", "right"):
        # a split through overlapping leaves leaves slivers of the neighbour behind: keep the plant
        parts, n = ndimage.label(ndimage.binary_dilation(keep, iterations=2))
        if n > 1:
            sizes = ndimage.sum(keep, parts, range(1, n + 1))
            keep &= parts == (int(np.argmax(sizes)) + 1)
    # the plant plus a 3 px anti-aliased rim; the painted halo beyond it goes
    rim = ndimage.binary_dilation(keep, iterations=3)
    a = arr[..., 3].astype(np.float32)
    a[~rim] = 0
    arr[..., 3] = a.astype(np.uint8)
    ys, xs = np.where(arr[..., 3] > 8)
    bx0, bx1, by0, by1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    pad = 4
    crop = Image.fromarray(arr).crop((max(0, bx0 - pad), max(0, by0 - pad), bx1 + pad, by1 + pad))
    return crop


def write_meta(path):
    meta = path + ".meta"
    if os.path.exists(meta):
        return
    m = open(META).read()
    m = re.sub(r"guid: \w+", "guid: " + uuid.uuid4().hex, m, count=1)
    open(meta, "w").write(m)


if __name__ == "__main__":
    cache = {}
    for key, f in SHEETS.items():
        im = Image.open(f).convert("RGBA")
        cache[key] = (im,) + detect(im)
    for crop, picks in PICKS.items():
        for stage, pick in enumerate(picks, start=1):
            im, lab, solid, boxes = cache[pick[0]]
            img = cut(im, lab, solid, boxes[pick[1]], pick[2] if len(pick) > 2 else None)
            path = os.path.join(OUT, "%s_%d.png" % (crop, stage))
            img.save(path)
            write_meta(path)
            print("  %-18s %s" % (os.path.basename(path), img.size))
