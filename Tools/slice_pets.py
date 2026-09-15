"""Cut the pet sheets in Pets/ into the poses the game uses.

    uv run --with pillow --with numpy --with scipy python Tools/slice_pets.py

Each sheet (Pets/<name>.png, supplied by the project owner, the file name is the pet's name) is a
1536x1024 RGBA board of one character: big front/side/back views, a walk cycle, sitting, sleeping,
happy and dizzy poses. Sprites are found as connected alpha regions (dilated so an eye or a
spike stays with its body), ordered row by row, and picked by index below.

Output, Assets/Resources/Art/pets/<id>/:

  portrait.png   the big front view, for the pet book and the gacha card
  idle.png       small front view, standing on the farm
  walk_N.png     side view facing RIGHT (sheets drawn facing left are mirrored), feet on one line
  sleep.png      lying down (a pet between jobs)
  happy.png      the reaction after a job or a snack

Only the character is kept: shadows, motion lines and "zzz" marks that are separate regions are
left behind, so the game can draw its own shadow under a moving pet.
"""
from PIL import Image, ImageOps
import numpy as np
from scipy import ndimage
import os, re, uuid

SRC = "Pets"
OUT = "Assets/Resources/Art/pets"
META = "Assets/Resources/Art/items/item_can.png.meta"

# id, sheet, portrait, idle, walk frames, walk faces left?, sleep, happy
PETS = [
    ("mit",        "MiT.png",        1, 15, [5, 6, 7, 8, 9, 10],  False, 19, 17),
    ("tim",        "TiM.png",        1, 12, [5, 6, 7, 8, 9, 10],  False, 17, 14),
    ("pinkteriii", "Pinkteriii.png", 4, 11, [6, 7, 8, 9, 10],     False, 16, 14),
    ("shushi",     "Shushi.png",     0, 13, [6, 7, 8],            False, 12, 13),
    ("yummy",      "Yummy.png",      0, 16, [9, 10, 11, 12, 13],  True,  14, 15),
    ("bega",       "bé gà.png",      0, 4,  [7, 8],               False, 9,  12),
]

SIZE = {"portrait": 512, "idle": 256, "walk": 256, "sleep": 256, "happy": 256}


def regions(im):
    a = np.asarray(im)[..., 3]
    mask = a > 40
    lab, _ = ndimage.label(ndimage.binary_dilation(mask, iterations=6))
    boxes = []
    for i, sl in enumerate(ndimage.find_objects(lab)):
        ys, xs = sl
        if (lab[sl] == i + 1).sum() < 1500:
            continue
        boxes.append((xs.start, ys.start, xs.stop, ys.stop, i + 1))
    boxes.sort(key=lambda b: (b[1] // 120, b[0]))
    return boxes, lab


def cut(im, lab, box, keep_marks=False):
    """The region only: pixels of other regions inside the box (a neighbour's shadow) are cleared."""
    x0, y0, x1, y1, label = box
    crop = np.asarray(im.crop((x0, y0, x1, y1))).copy()
    own = lab[y0:y1, x0:x1] == label
    if not keep_marks:
        crop[..., 3] = np.where(own, crop[..., 3], 0)
    out = Image.fromarray(crop, "RGBA")
    return out.crop(out.getbbox())


def largest_part(img):
    """Drop detached bits inside one region (a 'zzz', a sweat drop) and keep the body."""
    a = np.asarray(img)[..., 3] > 40
    lab, n = ndimage.label(a)
    if n <= 1:
        return img
    sizes = ndimage.sum(a, lab, range(1, n + 1))
    keep = lab == (int(np.argmax(sizes)) + 1)
    arr = np.asarray(img).copy()
    arr[..., 3] = np.where(keep, arr[..., 3], 0)
    out = Image.fromarray(arr, "RGBA")
    return out.crop(out.getbbox())


def fit(img, side):
    """Scale into a side x side square, feet on the bottom edge, centred horizontally."""
    k = min(side * 0.94 / img.width, side * 0.94 / img.height)
    w, h = max(1, int(img.width * k)), max(1, int(img.height * k))
    r = img.resize((w, h), Image.LANCZOS)
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.alpha_composite(r, ((side - w) // 2, side - h - int(side * 0.02)))
    return sq


def fit_frames(frames, side):
    """Walk frames share ONE scale, so the pet does not pulse in size between steps."""
    tallest = max(f.height for f in frames)
    widest = max(f.width for f in frames)
    k = min(side * 0.94 / widest, side * 0.94 / tallest)
    out = []
    for f in frames:
        w, h = max(1, int(f.width * k)), max(1, int(f.height * k))
        r = f.resize((w, h), Image.LANCZOS)
        sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        sq.alpha_composite(r, ((side - w) // 2, side - h - int(side * 0.02)))
        out.append(sq)
    return out


def save(img, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)
    meta = path + ".meta"
    guid = None
    if os.path.exists(meta):
        guid = re.search(r"guid: (\w+)", open(meta).read()).group(1)
    m = open(META).read()
    m = re.sub(r"guid: \w+", "guid: " + (guid or uuid.uuid4().hex), m, count=1)
    open(meta, "w").write(m)


if __name__ == "__main__":
    for pid, sheet, portrait, idle, walk, walk_left, sleep, happy in PETS:
        im = Image.open(os.path.join(SRC, sheet)).convert("RGBA")
        boxes, lab = regions(im)
        d = os.path.join(OUT, pid)
        save(fit(largest_part(cut(im, lab, boxes[portrait])), SIZE["portrait"]), os.path.join(d, "portrait.png"))
        save(fit(largest_part(cut(im, lab, boxes[idle])), SIZE["idle"]), os.path.join(d, "idle.png"))
        save(fit(largest_part(cut(im, lab, boxes[sleep])), SIZE["sleep"]), os.path.join(d, "sleep.png"))
        save(fit(largest_part(cut(im, lab, boxes[happy])), SIZE["happy"]), os.path.join(d, "happy.png"))
        frames = [largest_part(cut(im, lab, boxes[k])) for k in walk]
        if walk_left:
            frames = [ImageOps.mirror(f) for f in frames]
        for n, f in enumerate(fit_frames(frames, SIZE["walk"])):
            save(f, os.path.join(d, "walk_%d.png" % n))
        print(pid, len(boxes), "regions")
