"""An original farmer avatar, drawn from scratch: straw hat, round face, overalls.
Generic design — no resemblance to any existing character."""
from PIL import Image, ImageDraw
import math

S = 512
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

SKIN      = (0xF2, 0xC6, 0x9B)
SKIN_SH   = (0xDC, 0xA9, 0x7C)
HAT       = (0xE8, 0xC1, 0x6A)
HAT_DARK  = (0xC9, 0x9E, 0x47)
HAT_BAND  = (0x7E, 0x9E, 0x52)
SHIRT     = (0x5A, 0xA9, 0x6E)
SHIRT_DK  = (0x43, 0x8A, 0x55)
HAIR      = (0x6B, 0x47, 0x2E)
INK       = (0x4A, 0x35, 0x25)

cx = S / 2

# --- background disc so it reads inside the round frame ---
d.ellipse([26, 26, S - 26, S - 26], fill=(0xBF, 0xE4, 0xF5, 255))

# --- shoulders / overalls ---
d.ellipse([cx - 170, 352, cx + 170, 560], fill=SHIRT)
d.ellipse([cx - 170, 352, cx + 170, 560], outline=SHIRT_DK, width=6)
# bib
d.rounded_rectangle([cx - 64, 386, cx + 64, 486], radius=18, fill=SHIRT_DK)
d.rounded_rectangle([cx - 64, 386, cx + 64, 486], radius=18, outline=(0x35,0x70,0x45), width=5)
for sx in (-52, 52):                       # straps
    d.line([cx + sx, 372, cx + sx * 1.5, 470], fill=SHIRT_DK, width=16)

# --- neck ---
d.rounded_rectangle([cx - 34, 306, cx + 34, 382], radius=18, fill=SKIN_SH)

# --- face ---
d.ellipse([cx - 104, 146, cx + 104, 372], fill=SKIN)
# hair peeking under the brim
d.chord([cx - 104, 138, cx + 104, 320], 180, 360, fill=HAIR)

# --- eyes ---
for ex in (-40, 40):
    d.ellipse([cx + ex - 15, 246, cx + ex + 15, 280], fill=INK)
    d.ellipse([cx + ex - 5, 252, cx + ex + 6, 264], fill=(255, 255, 255))

# --- cheeks + smile ---
for ex in (-66, 66):
    d.ellipse([cx + ex - 19, 288, cx + ex + 19, 312], fill=(0xE8, 0x9E, 0x8E, 130))
d.arc([cx - 40, 282, cx + 40, 340], 20, 160, fill=INK, width=8)

# --- straw hat ---
d.ellipse([cx - 196, 150, cx + 196, 268], fill=HAT)          # brim
d.ellipse([cx - 196, 150, cx + 196, 268], outline=HAT_DARK, width=7)
d.ellipse([cx - 104, 84, cx + 104, 232], fill=HAT)           # crown
d.ellipse([cx - 104, 84, cx + 104, 232], outline=HAT_DARK, width=7)
d.rounded_rectangle([cx - 104, 188, cx + 104, 218], radius=12, fill=HAT_BAND)
# Straw texture on its own layer, masked to the brim — drawn straight onto the
# image the strokes shot past the hat silhouette, and line() overwrites alpha
# instead of blending it.
straw = Image.new("RGBA", (S, S), (0, 0, 0, 0))
sd = ImageDraw.Draw(straw)
for i in range(-9, 10):
    x = cx + i * 20
    sd.line([x, 160, x + 8, 258], fill=HAT_DARK + (90,), width=3)

mask = Image.new("L", (S, S), 0)
md = ImageDraw.Draw(mask)
md.ellipse([cx - 196, 150, cx + 196, 268], fill=255)     # brim
md.ellipse([cx - 104, 84, cx + 104, 232], fill=255)      # crown
md.rounded_rectangle([cx - 104, 188, cx + 104, 218], radius=12, fill=0)  # keep the band clean
img.paste(straw, (0, 0), Image.composite(straw.split()[3], Image.new("L", (S, S), 0), mask))

d = ImageDraw.Draw(img)
# re-draw the hat outlines so the texture sits inside them
d.ellipse([cx - 196, 150, cx + 196, 268], outline=HAT_DARK, width=7)
d.ellipse([cx - 104, 84, cx + 104, 232], outline=HAT_DARK, width=7)

img.save("Assets/Resources/Art/gen/farmer.png")
print("ok")
