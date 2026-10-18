"""Brings art wave 2 (docs/art/HANDOFF_wave2.md) into Assets/Art/Incoming, repairing its known problems.

Reads the untouched files from ArtSource/ArtWave2_Original, so running this again gives the same result. Fixes:
  * Magenta key spill: the ChargingBoar, ArmoredTortoise, HowlingWolf, ExplosiveBeetle and Spiderling frames (and
    the spiderling walk sheet) keep pink fringes and pink holes between legs. Each pixel is un-mixed from #FF00FF:
    pure key becomes transparent, edge pixels keep their own colour at lower opacity.
  * ExplosiveBeetle: painted motion lines off the body are removed (loose blobs).
  * BatonGuard windup touches the top edge: left as drawn (the cut is under one screen pixel in game).
  * Entrance props: loose cut-out debris under the drawing is removed.
  * RunnerWolf walk sheet: colours matched to the wave 1 frames, so the walk and the other states look alike.

Run:  python Tools/ArtGen/fix_wave2.py [preview-dir]   then  python Tools/ArtGen/animate.py
"""
import os
import shutil

from PIL import Image, ImageChops

import artlib as L
from fix_art import match_colours

ORIGINALS = os.path.join(L.ROOT, "ArtSource", "ArtWave2_Original")

KEYED = ("ENM_A3_", "ENM_A4_", "ENM_A5_", "ENM_A6_", "ENM_A7c_")
LOOSE_BITS = ("ENM_A6_ExplosiveBeetle_idle", "ENM_A6_ExplosiveBeetle_move", "ENM_A6_ExplosiveBeetle_windup",
              "ENV_P1_Entrance_prop", "ENV_P2_Entrance_prop", "ENV_P3_Entrance_prop")


def unkey_magenta(img):
    """Removes #FF00FF from every pixel: c = (1 - t) * colour + t * magenta, solved for colour and t."""
    raw = bytearray(img.tobytes())
    for i in range(0, len(raw), 4):
        r, g, b, a = raw[i], raw[i + 1], raw[i + 2], raw[i + 3]
        if a == 0:
            continue
        m = min(r, b) - g  # how much magenta beats green
        if m <= 25:
            continue
        t = min(1.0, (m - 25) / 230.0)
        keep = 1.0 - t
        if t >= 0.7:
            raw[i + 3] = 0
            continue
        # The colour under the key, then any pink left over pulled toward grey.
        fr = max(0.0, min(255.0, (r - t * 255) / keep))
        fg = max(0.0, min(255.0, g / keep))
        fb = max(0.0, min(255.0, (b - t * 255) / keep))
        fr, fb = min(fr, fg + 20), min(fb, fg + 20)
        raw[i], raw[i + 1], raw[i + 2] = int(fr), int(fg), int(fb)
        raw[i + 3] = int(a * min(1.0, (0.7 - t) / 0.6))
    return Image.frombytes("RGBA", img.size, bytes(raw))


def drop_loose_bits(img, keep_fraction):
    a = ImageChops.multiply(img.getchannel("A"), L.components_mask(img, keep_fraction=keep_fraction))
    out = img.copy()
    out.putalpha(a)
    return out


def sheet_cells(img, cols, rows):
    cw, ch = img.size[0] // cols, img.size[1] // rows
    return [img.crop((c * cw, r * ch, (c + 1) * cw, (r + 1) * ch)) for r in range(rows) for c in range(cols)], cw, ch


def match_sheet(img, ref, cols, rows):
    cells, cw, ch = sheet_cells(img, cols, rows)
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    for i, c in enumerate(cells):
        out.paste(match_colours(c, ref), ((i % cols) * cw, (i // cols) * ch))
    return out


def main(out=L.INCOMING):
    os.makedirs(out, exist_ok=True)
    for f in sorted(os.listdir(ORIGINALS)):
        if not f.endswith(".png"):
            continue
        name = f[:-4]
        img = Image.open(os.path.join(ORIGINALS, f))
        if img.mode != "RGBA" or not name.startswith(("ENM_", "ENV_", "FX_", "PRJ_")):
            shutil.copy2(os.path.join(ORIGINALS, f), os.path.join(out, f))
            print("copied", name)
            continue
        img = img.convert("RGBA")
        if name.startswith(KEYED):
            img = unkey_magenta(img)
        if name in LOOSE_BITS:
            img = drop_loose_bits(img, 0.04)
        if name == "ENM_A1_RunnerWolf_move_f6":
            img = match_sheet(img, L.load("ENM_A1_RunnerWolf_move"), 3, 2)
        img.save(os.path.join(out, f), optimize=True)
        print("fixed", name)


if __name__ == "__main__":
    import sys
    main(*sys.argv[1:2])
