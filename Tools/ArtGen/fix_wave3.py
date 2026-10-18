"""Brings art wave 3 (docs/art/HANDOFF_wave3.md) into Assets/Art/Incoming, repairing its known problems.

Reads the untouched files from ArtSource/ArtWave3_Original, so running this again gives the same result. Fixes:
  * Magenta key spill: the robot and human frames keep a thin pink outline (worst on the saw charger and the drone
    deaths) and pink smoke wisps. Each pixel is un-mixed from #FF00FF (fix_wave2.unkey_magenta).
  * ShieldGuard: the clear riot shield shows the magenta background through it. Its pink pixels become cool,
    see-through glass instead of being erased; only the thin outer fringe is un-keyed.
  * Boss 3 windup_throw is a copy of throw: not used (animate.py builds the throw from the throw frame).
  * Frames touching their cell edge (saw charger death, drone move/attack, shield guard idle): left as drawn.

Run:  python Tools/ArtGen/fix_wave3.py [preview-dir]   then  python Tools/ArtGen/animate.py
"""
import os
import shutil

from PIL import Image, ImageFilter

import artlib as L
from fix_wave2 import unkey_magenta

ORIGINALS = os.path.join(L.ROOT, "ArtSource", "ArtWave3_Original")

KEYED = ("ENM_R", "ENM_H")
SKIPPED = ("BOS_B3_FormerCompanion_windup_throw",)


def glass_shield(img):
    """Pink seen through the riot shield becomes pale steel-blue glass; the edge fringe is left for unkey_magenta."""
    alpha = img.getchannel("A")
    # Inside the drawing (not the outer rim): an eroded alpha mask.
    inner = alpha.point(lambda v: 255 if v > 200 else 0).filter(ImageFilter.MinFilter(9))
    raw = bytearray(img.tobytes())
    mask = inner.tobytes()
    for i in range(0, len(raw), 4):
        if not mask[i // 4]:
            continue
        r, g, b, a = raw[i], raw[i + 1], raw[i + 2], raw[i + 3]
        m = min(r, b) - g
        if m <= 22:
            continue
        lum = 0.3 * r + 0.55 * g + 0.15 * b
        k = min(1.0, (m - 22) / 60.0)  # how much of it was the background showing through
        glass = (lum * 0.86, lum * 0.97, min(255.0, lum * 1.1))
        raw[i] = int(r + (glass[0] - r) * k)
        raw[i + 1] = int(g + (glass[1] - g) * k)
        raw[i + 2] = int(b + (glass[2] - b) * k)
        raw[i + 3] = int(a * (1.0 - 0.3 * k))  # clear plastic: a little see-through
    return Image.frombytes("RGBA", img.size, bytes(raw))


def main(out=L.INCOMING):
    os.makedirs(out, exist_ok=True)
    for f in sorted(os.listdir(ORIGINALS)):
        if not f.endswith(".png"):
            continue
        name = f[:-4]
        target = os.path.join(out, f)
        if name in SKIPPED:
            if os.path.exists(target):
                os.remove(target)
            print("skipped", name)
            continue
        if not name.startswith(KEYED):
            shutil.copy2(os.path.join(ORIGINALS, f), target)
            print("copied", name)
            continue
        img = Image.open(os.path.join(ORIGINALS, f)).convert("RGBA")
        if name.startswith("ENM_H4_"):
            img = glass_shield(img)
        img = unkey_magenta(img)
        img.save(target, optimize=True)
        print("fixed", name)


if __name__ == "__main__":
    import sys
    main(*sys.argv[1:2])
