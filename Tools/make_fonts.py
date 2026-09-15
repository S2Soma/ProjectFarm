"""The game's font: Baloo 2 (SIL OFL), cut into three static weights and re-scaled to sit where
Nunito sat.

    uv run --with fonttools python Tools/make_fonts.py

Source: Tools/fonts/Baloo2-Variable.ttf (github.com/google/fonts, ofl/baloo2), licence alongside.

Why the metrics are edited
--------------------------
Every label in the game was sized for Nunito: a line box of about 1.45 x the font size, text
vertically centred by the font's ascent and descent. Baloo 2 as shipped draws smaller (cap height
0.61 em against Nunito's 0.71) inside a much taller line (ascent 1.08 + descent 0.52, room for
Devanagari the game never prints). Dropped in as is, every label would shrink and slide up.

So the glyphs are enlarged 12% by lowering unitsPerEm (1000 -> 893, outlines untouched), and the
ascent/descent are set to Nunito's proportions (1.011 / 0.353 em). Vietnamese stacked marks still
fit: the tallest, "Ấ", reaches 1.025 em, the same as in Nunito.

Output, Assets/Resources/Fonts/:  Baloo2-SemiBold.ttf (body), Baloo2-Bold.ttf (small emphasis),
Baloo2-ExtraBold.ttf (titles, numbers, buttons)
"""
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer
from fontTools import subset
import os, shutil

SRC = "Tools/fonts/Baloo2-Variable.ttf"
OUT = "Assets/Resources/Fonts"
UPM = 893
ASC, DESC = round(1.011 * UPM), round(-0.353 * UPM)

WEIGHTS = [("SemiBold", 600), ("Bold", 700), ("ExtraBold", 800)]

for style, wght in WEIGHTS:
    f = TTFont(SRC)
    f = instancer.instantiateVariableFont(f, {"wght": wght})
    # Latin and Vietnamese only: the Devanagari half of Baloo 2 was 85% of the file
    opts = subset.Options()
    opts.layout_features = ["*"]
    opts.name_IDs = ["*"]
    opts.notdef_outline = True
    sub = subset.Subsetter(opts)
    sub.populate(unicodes=list(range(0x20, 0x7F)) + list(range(0xA0, 0x250)) + list(range(0x300, 0x370))
                 + list(range(0x1E00, 0x1F00)) + list(range(0x2000, 0x2070)) + list(range(0x20A0, 0x20D0))
                 + list(range(0x2100, 0x2200)) + list(range(0x2190, 0x2300)) + list(range(0x25A0, 0x2700)))
    sub.subset(f)
    f["head"].unitsPerEm = UPM
    hh, os2 = f["hhea"], f["OS/2"]
    hh.ascent, hh.descent, hh.lineGap = ASC, DESC, 0
    os2.sTypoAscender, os2.sTypoDescender, os2.sTypoLineGap = ASC, DESC, 0
    os2.usWinAscent, os2.usWinDescent = round(1.08 * UPM), round(0.36 * UPM)
    for rec in f["name"].names:
        if rec.nameID in (1, 16):
            rec.string = "Baloo 2 MiT"
        elif rec.nameID in (2, 17):
            rec.string = style
        elif rec.nameID in (4,):
            rec.string = "Baloo 2 MiT " + style
        elif rec.nameID in (6,):
            rec.string = "Baloo2MiT-" + style
    path = os.path.join(OUT, "Baloo2-%s.ttf" % style)
    f.save(path)
    print("  ", path, os.path.getsize(path))

shutil.copy("Tools/fonts/Baloo2-OFL.txt", os.path.join(OUT, "Baloo2-OFL.txt"))
