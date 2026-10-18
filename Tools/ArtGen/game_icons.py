"""Turns the white game-icons.net silhouettes in ArtSource/GameIcons (CC BY 3.0, see docs/CREDITS.md) into
ICO_* icons for the shop: a two-tone fill (light from the top), a dark outline and a soft drop shadow, sized
like the drawn icons (about 410 of 512 pixels). Re-running gives the same files.

    python Tools/ArtGen/game_icons.py
"""
from pathlib import Path

from PIL import Image, ImageChops, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "ArtSource" / "GameIcons"
OUT = ROOT / "Assets" / "Art" / "Incoming"

STEEL = ((222, 228, 234), (120, 130, 142))
OLIVE = ((150, 156, 110), (78, 84, 58))
CYAN = ((200, 250, 255), (40, 170, 200))

# source -> (art code, name, colours)
ICONS = {
    "skoll_baton": ("BAT", "Baton", STEEL),
    "skoll_famas": ("IRF", "IndustrialRifle", STEEL),
    "delapouite_sawed-off-shotgun": ("MSG", "MagShotgun", STEEL),
    "delapouite_warhammer": ("HAM", "Hammer", STEEL),
    "lorc_ray-gun": ("ERF", "EnergyRifle", CYAN),
    "sbed_blaster": ("ESG", "EnergyShotgun", CYAN),
    "lorc_sparkling-sabre": ("EBL", "EnergyBlade", CYAN),
    "skoll_teller-mine": ("MIN", "Mine", OLIVE),
    "lorc_lightning-helix": ("EMP", "EmpPulse", CYAN),
    "lorc_energy-shield": ("SHD", "Shield", CYAN),
}

SIZE, FIT = 512, 410


def gradient(size, top, bottom):
    w, h = size
    col = Image.new("RGBA", (1, h))
    for y in range(h):
        t = y / max(1, h - 1)
        col.putpixel((0, y), tuple(int(a + (b - a) * t) for a, b in zip(top, bottom)) + (255,))
    return col.resize((w, h))


def build(src: Path, colours):
    alpha = Image.open(src).convert("RGBA").getchannel("A")
    alpha = alpha.crop(alpha.getbbox())
    scale = FIT / max(alpha.size)
    alpha = alpha.resize((max(1, round(alpha.width * scale)), max(1, round(alpha.height * scale))), Image.LANCZOS)
    mask = Image.new("L", (SIZE, SIZE), 0)
    mask.paste(alpha, ((SIZE - alpha.width) // 2, (SIZE - alpha.height) // 2))

    box = mask.getbbox()
    fill = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    fill.paste(gradient((box[2] - box[0], box[3] - box[1]), *colours), box[:2])
    fill.putalpha(mask)

    outline = mask.filter(ImageFilter.MaxFilter(9))
    shadow = outline.filter(ImageFilter.GaussianBlur(10)).point(lambda v: v * 0.55)
    out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    out.paste((0, 0, 0, 255), (6, 10), shadow)
    out.alpha_composite(Image.merge("RGBA", (*[Image.new("L", (SIZE, SIZE), 22)] * 3, outline)))
    # a thin highlight along the top edges of the fill
    edge = ImageChops.subtract(mask, ImageChops.offset(mask, 0, 4)).point(lambda v: v * 0.5)
    out.alpha_composite(fill)
    out.alpha_composite(Image.merge("RGBA", (*[Image.new("L", (SIZE, SIZE), 255)] * 3, edge)))
    return out


def main():
    for name, (code, title, colours) in ICONS.items():
        src = SRC / f"{name}.png"
        if not src.exists():
            print("missing", src)
            continue
        dst = OUT / f"ICO_{code}_{title}_icon.png"
        build(src, colours).save(dst)
        print("wrote", dst.relative_to(ROOT))


if __name__ == "__main__":
    main()
