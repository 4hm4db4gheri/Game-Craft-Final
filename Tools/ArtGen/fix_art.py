"""Repairs known problems in art wave 1 (docs/art/HANDOFF_wave1.md and the wave 1 review in agent.md).

Every fix reads the untouched file from ArtSource/ArtWave1_Original (copied there on the first run), so running
this again gives the same result. Fixes:
  * Boss 2 burst / windup_blast / blast drawn in other colours: matched to the idle frame (glow and flash kept).
  * Quill shooter attack has painted quill streaks: loose blobs off the body are removed (the game fires quills).
  * Explosion strip: frames 2-6 are cut by the cell edge and the tail is empty: frames fit the cell, edges fade,
    and the last frames fade out the smoke.
  * Muzzle flash / impact frames touching the cell edge: edges fade.
  * Seams where border panels repeat (P1 BorderH and BorderV, P3 BorderV) and the P1 ground: made to wrap.
  * Coin, lock and infinity icons (blurry upscales) and the four button states (four sizes): redrawn cleanly.

Run:  python Tools/ArtGen/fix_art.py      then  python Tools/ArtGen/animate.py
"""
import math
import os
import shutil

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageStat

import artlib as L

FIXED = [
    "BOS_B2_IndustrialRobot_burst", "BOS_B2_IndustrialRobot_windup_blast", "BOS_B2_IndustrialRobot_blast",
    "ENM_A2_QuillShooter_attack",
    "FX_GEN_Explosion_burst_f8", "FX_GEN_MuzzleFlash_burst_f3", "FX_GEN_Impact_burst_f4",
    "ENV_P1_BorderH_prop", "ENV_P1_BorderV_prop", "ENV_P3_BorderV_prop", "ENV_P1_Ground_ground",
    "ICO_GEN_Coin_icon", "ICO_GEN_Lock_icon", "ICO_GEN_Infinity_icon",
    "UI_GEN_Button_ui_normal", "UI_GEN_Button_ui_hover", "UI_GEN_Button_ui_pressed", "UI_GEN_Button_ui_disabled",
]


def original(name):
    os.makedirs(L.ORIGINALS, exist_ok=True)
    keep = os.path.join(L.ORIGINALS, name + ".png")
    if not os.path.exists(keep):
        shutil.copy2(os.path.join(L.INCOMING, name + ".png"), keep)
    return Image.open(keep)


def save(img, name):
    img.save(os.path.join(L.INCOMING, name + ".png"), optimize=True)
    print("fixed", name)


# ---- colour match ----------------------------------------------------------------------------------------

def body_mask(img):
    a = img.getchannel("A").point(lambda v: 255 if v > 200 else 0)
    glow = L.tint_mask(img, 1, 50).point(lambda v: 255 if v > 30 else 0)
    flash = L.tint_mask(img, 0, 60).point(lambda v: 255 if v > 30 else 0)
    return ImageChops.subtract(ImageChops.subtract(a, glow), flash)


def match_colours(img, ref):
    """Per-channel mean and spread of the body matched to ref; glowing and flashing pixels keep their colour."""
    ms, rs = ImageStat.Stat(img.convert("RGB"), body_mask(img)), ImageStat.Stat(ref.convert("RGB"), body_mask(ref))
    bands = []
    for c, band in enumerate(img.convert("RGB").split()):
        k = rs.stddev[c] / max(1.0, ms.stddev[c])
        bands.append(band.point(lambda v, c=c, k=k: max(0, min(255, int(round((v - ms.mean[c]) * k + rs.mean[c]))))))
    out = Image.merge("RGB", bands).convert("RGBA")
    out.putalpha(img.getchannel("A"))
    keep = ImageChops.lighter(L.tint_mask(img, 1, 50), L.tint_mask(img, 0, 60)).filter(ImageFilter.GaussianBlur(2))
    return Image.composite(img, out, keep)


def fix_boss_two():
    ref = L.load("BOS_B2_IndustrialRobot_idle")
    for s in ("burst", "windup_blast", "blast"):
        name = f"BOS_B2_IndustrialRobot_{s}"
        save(match_colours(original(name).convert("RGBA"), ref), name)


# ---- loose streaks -----------------------------------------------------------------------------------------

def fix_quill_attack():
    name = "ENM_A2_QuillShooter_attack"
    img = original(name).convert("RGBA")
    mask = L.components_mask(img, keep_fraction=0.08)
    img.putalpha(ImageChops.multiply(img.getchannel("A"), mask))
    save(img, name)


# ---- effects -----------------------------------------------------------------------------------------------

def vignette(size, inner=0.40, outer=0.49):
    v = Image.new("L", (size, size), 0)
    px = v.load()
    c = size / 2
    for y in range(size):
        for x in range(size):
            r = math.hypot(x + 0.5 - c, y + 0.5 - c) / size
            px[x, y] = int(255 * (1 - L.smoothstep(inner, outer, r)))
    return v


def scale_brightness(img, k):
    """Additive effect frames: colour and alpha fade together."""
    return img.point(lambda v: int(min(255, v * k)))


def scaled(img, s):
    size = img.size[0]
    n = int(round(size * s))
    small = img.convert("RGBa").resize((n, n), Image.LANCZOS).convert("RGBA")
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.paste(small, ((size - n) // 2, (size - n) // 2))
    return out


def fx_frames(img, count):
    w = img.width // count
    return [img.crop((i * w, 0, i * w + w, img.height)) for i in range(count)]


def fx_join(frames):
    w = frames[0].width
    out = Image.new("RGBA", (w * len(frames), frames[0].height), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        out.paste(f, (i * w, 0))
    return out


def fade_edges(f, v):
    f = f.copy()
    f.putalpha(ImageChops.multiply(f.getchannel("A"), v))
    rgb = Image.merge("RGB", [ImageChops.multiply(b, v) for b in f.convert("RGB").split()])
    rgb.putalpha(f.getchannel("A"))
    return rgb


def fix_explosion():
    name = "FX_GEN_Explosion_burst_f8"
    fr = fx_frames(original(name).convert("RGBA"), 8)
    v = vignette(fr[0].width)
    sizes = [1.0, 0.84, 0.78, 0.82, 0.86, 0.9, 0.94, 0.98]
    out = [fade_edges(scaled(fr[i], sizes[i]), v) for i in range(5)]
    smoke = fr[4]
    for i, k in zip(range(5, 8), (0.7, 0.45, 0.22)):
        out.append(fade_edges(scale_brightness(scaled(smoke, sizes[i]), k), v))
    save(fx_join(out), name)


def fix_fx_edges(name, count):
    fr = fx_frames(original(name).convert("RGBA"), count)
    v = vignette(fr[0].width, 0.42, 0.5)
    save(fx_join([fade_edges(f, v) for f in fr]), name)


# ---- seams -------------------------------------------------------------------------------------------------

def wrap(img, horizontal=True, vertical=False, band=0.18):
    """Makes an image tile without a seam: near its edges it shows itself rolled by half (whose edges meet)."""
    w, h = img.size
    rolled = ImageChops.offset(img, w // 2 if horizontal else 0, h // 2 if vertical else 0)
    mask = Image.new("L", (w, h), 0)
    px = mask.load()
    for y in range(h):
        for x in range(w):
            e = 1.0
            if horizontal:
                e = min(e, L.smoothstep(0, band, min(x, w - 1 - x) / w))
            if vertical:
                e = min(e, L.smoothstep(0, band, min(y, h - 1 - y) / h))
            px[x, y] = int(255 * e)
    mode = img.mode
    if mode == "RGBA":
        return Image.composite(img.convert("RGBa"), rolled.convert("RGBa"), mask).convert("RGBA")
    return Image.composite(img, rolled, mask)


def fence_column():
    """P3 fence: a capped column of five panels with an empty foot. Rebuilt from whole panels (cut just above a
    joint each time) so it repeats panel after panel; loose keying specks on its side are dropped."""
    src = original("ENV_P3_BorderV_prop").convert("RGBA")
    src.putalpha(ImageChops.multiply(src.getchannel("A"), L.components_mask(src, keep_fraction=0.02)))
    top, bottom, second = 214, 854, 534  # rows just above the panel joints in the source
    four = src.crop((0, top, src.width, bottom))
    two = src.crop((0, top, src.width, second))
    col = Image.new("RGBA", (src.width, four.height + two.height), (0, 0, 0, 0))
    col.paste(four, (0, 0))
    col.paste(two, (0, four.height))
    return col.convert("RGBa").resize(src.size, Image.LANCZOS).convert("RGBA")


def fix_seams():
    save(wrap(original("ENV_P1_BorderH_prop"), horizontal=True), "ENV_P1_BorderH_prop")
    # Thorn wall: its top is ragged and see-through, so only the dense part is made to wrap.
    thorns = original("ENV_P1_BorderV_prop").convert("RGBA")
    thorns = thorns.crop((0, 150, thorns.width, 1000)).resize(thorns.size, Image.LANCZOS)
    save(wrap(thorns, horizontal=False, vertical=True, band=0.12), "ENV_P1_BorderV_prop")
    save(fence_column(), "ENV_P3_BorderV_prop")
    save(wrap(original("ENV_P1_Ground_ground"), horizontal=True, vertical=True, band=0.08), "ENV_P1_Ground_ground")


# ---- redrawn UI --------------------------------------------------------------------------------------------

SS = 4  # supersampling for clean edges


def canvas(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def finish(img, w, h):
    return img.convert("RGBa").resize((w, h), Image.LANCZOS).convert("RGBA")


def coin_icon():
    s = 512
    im = canvas(s, s)
    d = ImageDraw.Draw(im)
    c, r = s * SS / 2, s * SS * 0.40
    d.ellipse([c - r + 10 * SS, c - r + 18 * SS, c + r + 10 * SS, c + r + 18 * SS], fill=(20, 14, 6, 140))
    d.ellipse([c - r, c - r, c + r, c + r], fill=(150, 104, 30, 255))
    r2 = r * 0.90
    d.ellipse([c - r2, c - r2 - 6 * SS, c + r2, c + r2 - 6 * SS], fill=(236, 186, 74, 255))
    r3 = r * 0.70
    d.ellipse([c - r3, c - r3 - 6 * SS, c + r3, c + r3 - 6 * SS], outline=(170, 118, 34, 255), width=10 * SS)
    # a gear-like hub: salvaged parts are the currency
    for i in range(8):
        a = i * math.pi / 4
        x, y = c + math.cos(a) * r * 0.42, c - 6 * SS + math.sin(a) * r * 0.42
        d.ellipse([x - 18 * SS, y - 18 * SS, x + 18 * SS, y + 18 * SS], fill=(186, 132, 40, 255))
    r4 = r * 0.38
    d.ellipse([c - r4, c - r4 - 6 * SS, c + r4, c + r4 - 6 * SS], fill=(186, 132, 40, 255))
    r5 = r * 0.16
    d.ellipse([c - r5, c - r5 - 6 * SS, c + r5, c + r5 - 6 * SS], fill=(96, 64, 18, 255))
    d.arc([c - r2 * 0.92, c - r2 * 0.92 - 6 * SS, c + r2 * 0.92, c + r2 * 0.92 - 6 * SS], 200, 290, fill=(255, 236, 170, 255), width=14 * SS)
    return finish(im, s, s)


def lock_icon():
    s = 512
    im = canvas(s, s)
    d = ImageDraw.Draw(im)
    u = SS
    d.rounded_rectangle([118 * u, 238 * u, 404 * u, 452 * u], 34 * u, fill=(20, 22, 26, 150))
    d.arc([160 * u, 70 * u, 352 * u, 300 * u], 180, 360, fill=(70, 76, 84, 255), width=46 * u)
    d.arc([160 * u, 70 * u, 352 * u, 300 * u], 200, 340, fill=(196, 204, 212, 255), width=26 * u)
    d.rectangle([160 * u, 180 * u, 206 * u, 236 * u], fill=(70, 76, 84, 255))
    d.rectangle([306 * u, 180 * u, 352 * u, 236 * u], fill=(70, 76, 84, 255))
    d.rounded_rectangle([108 * u, 222 * u, 404 * u, 440 * u], 30 * u, fill=(104, 112, 122, 255))
    d.rounded_rectangle([122 * u, 234 * u, 390 * u, 424 * u], 22 * u, fill=(176, 186, 196, 255))
    d.rounded_rectangle([122 * u, 234 * u, 390 * u, 262 * u], 14 * u, fill=(220, 228, 236, 255))
    d.ellipse([228 * u, 286 * u, 284 * u, 342 * u], fill=(28, 30, 34, 255))
    d.polygon([(240 * u, 330 * u), (272 * u, 330 * u), (264 * u, 388 * u), (248 * u, 388 * u)], fill=(28, 30, 34, 255))
    return finish(im, s, s)


def infinity_icon():
    s = 512
    im = canvas(s, s)
    d = ImageDraw.Draw(im)
    pts = []
    for i in range(721):
        t = i / 720 * math.tau
        den = 1 + math.sin(t) ** 2
        x = math.cos(t) / den
        y = math.sin(t) * math.cos(t) / den
        pts.append(((256 + x * 190) * SS, (256 + y * 190) * SS))
    for width, col, dy in ((58, (20, 22, 26, 150), 10), (54, (88, 96, 106, 255), 0), (34, (210, 218, 226, 255), -3)):
        d.line([(x, y + dy * SS) for x, y in pts], fill=col, width=width * SS, joint="curve")
    return finish(im, s, s)


def button(state):
    w, h = 512, 208
    im = canvas(w, h)
    d = ImageDraw.Draw(im)
    u = SS
    border = {"normal": (196, 204, 212, 255), "hover": (110, 228, 255, 255), "pressed": (120, 130, 140, 255), "disabled": (84, 88, 94, 255)}[state]
    fill_top = {"normal": (44, 54, 62, 255), "hover": (40, 62, 72, 255), "pressed": (24, 30, 36, 255), "disabled": (36, 38, 42, 255)}[state]
    fill_bot = {"normal": (24, 30, 36, 255), "hover": (22, 36, 44, 255), "pressed": (30, 36, 42, 255), "disabled": (30, 32, 36, 255)}[state]
    inset = 6 if state == "pressed" else 0
    if state == "hover":
        glow = canvas(w, h)
        ImageDraw.Draw(glow).rounded_rectangle([14 * u, 14 * u, (w - 14) * u, (h - 14) * u], 30 * u, outline=(80, 210, 255, 200), width=22 * u)
        im.alpha_composite(glow.filter(ImageFilter.GaussianBlur(10 * u)))
    box = [22 * u, (22 + inset) * u, (w - 22) * u, (h - 22 + inset) * u]
    d.rounded_rectangle(box, 26 * u, fill=fill_bot, outline=border, width=8 * u)
    # vertical gradient inside
    inner = [box[0] + 8 * u, box[1] + 8 * u, box[2] - 8 * u, box[3] - 8 * u]
    grad = Image.new("RGBA", (inner[2] - inner[0], inner[3] - inner[1]))
    gd = ImageDraw.Draw(grad)
    for y in range(grad.height):
        t = y / max(1, grad.height - 1)
        gd.line([(0, y), (grad.width, y)], fill=tuple(int(fill_top[i] * (1 - t) + fill_bot[i] * t) for i in range(4)))
    m = Image.new("L", grad.size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, grad.width - 1, grad.height - 1], 18 * u, fill=255)
    im.paste(grad, (inner[0], inner[1]), m)
    if state != "disabled":
        d.line([(box[0] + 34 * u, box[1] + 18 * u), (box[2] - 34 * u, box[1] + 18 * u)], fill=(255, 255, 255, 40), width=4 * u)
    return finish(im, w, h)


def fix_ui():
    for name, f in (("ICO_GEN_Coin_icon", coin_icon), ("ICO_GEN_Lock_icon", lock_icon), ("ICO_GEN_Infinity_icon", infinity_icon)):
        original(name)
        save(f(), name)
    for state in ("normal", "hover", "pressed", "disabled"):
        name = f"UI_GEN_Button_ui_{state}"
        original(name)
        save(button(state), name)


def main():
    fix_boss_two()
    fix_quill_attack()
    fix_explosion()
    fix_fx_edges("FX_GEN_MuzzleFlash_burst_f3", 3)
    fix_fx_edges("FX_GEN_Impact_burst_f4", 4)
    fix_seams()
    fix_ui()


if __name__ == "__main__":
    main()
