"""The tutorial's pointing hand: Microsoft Fluent Emoji "Backhand index pointing up", 3D, light
skin tone (MIT licence, Tools/pointer/LICENSE-fluentui-emoji.txt,
github.com/microsoft/fluentui-emoji). It replaces the hand drawn by gen_items.py, which read as
clip art next to the painted farm.

    uv run --with pillow --with numpy python Tools/make_pointer.py

Adds a soft drop shadow (the hand sits on a dark scrim and on bright soil, and has to read on
both) and prints the fingertip as a pivot for Coach.FingerTip.

Output: Assets/Resources/Art/items/pointer_hand.png (the existing .meta is kept)
"""
from PIL import Image, ImageFilter
import numpy as np

SRC = "Tools/pointer/backhand_index_pointing_up_3d_light.png"
OUT = "Assets/Resources/Art/items/pointer_hand.png"
SIZE = 320          # a little room around the 256 px source for the shadow
SHADOW_OFF = (7, 9)

src = Image.open(SRC).convert("RGBA")
canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
ox = (SIZE - src.width) // 2 - SHADOW_OFF[0] // 2
oy = (SIZE - src.height) // 2 - SHADOW_OFF[1] // 2

a = src.split()[3]
shadow = Image.new("RGBA", src.size, (20, 12, 30, 0))
shadow.putalpha(a.point(lambda v: int(v * 0.42)))
shadow_layer = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
shadow_layer.alpha_composite(shadow, (ox + SHADOW_OFF[0], oy + SHADOW_OFF[1]))
shadow_layer = shadow_layer.filter(ImageFilter.GaussianBlur(6))
canvas.alpha_composite(shadow_layer)
canvas.alpha_composite(src, (ox, oy))
canvas.save(OUT)

# fingertip: the topmost solid row of the source, its middle
alpha = np.asarray(src)[..., 3]
rows = np.where(alpha.max(axis=1) > 200)[0]
top = rows[0]
xs = np.where(alpha[top + 3] > 200)[0]
tip_x = (xs.min() + xs.max()) / 2 + ox
tip_y = top + oy
print("saved", OUT)
print("FingerTip pivot = new Vector2(%.3ff, %.3ff)" % (tip_x / SIZE, 1 - tip_y / SIZE))
