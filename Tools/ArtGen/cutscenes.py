"""Story cutscene stills: ArtSource/Cutscenes/<n>.png -> Assets/Resources/Cutscenes/<Shot>.png.

The source pictures carry a black subtitle band under the art; the game draws its own band,
so only the art is kept (the top 716 rows: every picture's art reaches at least that far,
and 1672x716 divides by 4 for block compression). Picture 3 (A03) was never drawn; that
shot reuses A02 with a closer framing (Dialogue.json "Shots").
"""
import os
import sys
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "ArtSource", "Cutscenes")
DST = os.path.join(ROOT, "Assets", "Resources", "Cutscenes")
SHOTS = {1: "A01", 2: "A02", 4: "B01", 5: "B02", 6: "B03", 7: "C01",
         8: "C02", 9: "C03", 10: "D01", 11: "D02", 12: "D03", 13: "D04"}
ART_ROWS = 716


def main():
    os.makedirs(DST, exist_ok=True)
    for number, shot in SHOTS.items():
        path = os.path.join(SRC, f"{number}.png")
        if not os.path.exists(path):
            print("missing", path)
            continue
        image = Image.open(path).convert("RGB")
        art = image.crop((0, 0, image.width, ART_ROWS))
        art.save(os.path.join(DST, f"{shot}.png"), optimize=True)
        print(shot, art.size)


if __name__ == "__main__":
    sys.exit(main())
