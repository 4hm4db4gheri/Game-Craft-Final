"""Shared helpers for the Hollow art scripts: loading, mesh warps, masks, sheets.

Only Pillow is needed (no numpy). Coordinates are pixels of the 1024 px source cell, y pointing down.
"""
import math
import os
from PIL import Image, ImageChops, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
INCOMING = os.path.join(ROOT, "Assets", "Art", "Incoming")
GENERATED = os.path.join(INCOMING, "Generated")
ORIGINALS = os.path.join(ROOT, "ArtSource", "ArtWave1_Original")

MESH = 32  # warp grid cell in source pixels


def load(name, folder=INCOMING):
    return Image.open(os.path.join(folder, name + ".png")).convert("RGBA")


def exists(name):
    return os.path.exists(os.path.join(INCOMING, name + ".png"))


def alpha_bbox(img, threshold=40):
    return img.getchannel("A").point(lambda a: 255 if a > threshold else 0).getbbox()


def smoothstep(e0, e1, v):
    if e1 == e0:
        return 1.0 if v >= e1 else 0.0
    t = min(1.0, max(0.0, (v - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def rotate_point(x, y, cx, cy, deg):
    """Rotates (x, y) around (cx, cy); positive is clockwise on screen (y down)."""
    a = math.radians(deg)
    dx, dy = x - cx, y - cy
    return cx + dx * math.cos(a) - dy * math.sin(a), cy + dx * math.sin(a) + dy * math.cos(a)


def warp(img, inverse):
    """Mesh warp: inverse(x, y) gives the source point drawn at output (x, y). Premultiplied, so edges stay clean."""
    w, h = img.size
    data = []
    for gy in range(0, h, MESH):
        for gx in range(0, w, MESH):
            x1, y1 = gx + MESH, gy + MESH
            ul, ll, lr, ur = inverse(gx, gy), inverse(gx, y1), inverse(x1, y1), inverse(x1, gy)
            data.append(((gx, gy, x1, y1), (*ul, *ll, *lr, *ur)))
    pre = img.convert("RGBa")
    out = pre.transform(img.size, Image.MESH, data, resample=Image.BICUBIC)
    return out.convert("RGBA")


def shift(img, dx, dy):
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.paste(img, (int(round(dx)), int(round(dy))))
    return out


def downscale(img, size):
    return img.convert("RGBa").resize((size, size), Image.LANCZOS).convert("RGBA")


def tint_mask(img, channel, margin):
    """Pixels where one channel (0 r, 1 g, 2 b) beats both others by `margin`: glow, flashes."""
    r, g, b, a = img.split()
    chans = [r, g, b]
    main = chans[channel]
    others = ImageChops.lighter(*[c for i, c in enumerate(chans) if i != channel])
    diff = ImageChops.subtract(main, others)
    return ImageChops.multiply(diff.point(lambda v: 255 if v > margin else int(v * 255 / max(1, margin)) if v > margin * 0.5 else 0), a)


def brighten_where(img, mask, factor):
    from PIL import ImageEnhance
    bright = ImageEnhance.Brightness(img).enhance(factor)
    bright.putalpha(img.getchannel("A"))
    return Image.composite(bright, img, mask)


def grid_layout(frames):
    """Cells per row and rows for a sheet; must be what ArtRules.Grid (ArtImport.cs) reads back."""
    if frames <= 3:
        return frames, 1
    if frames == 4:
        return 2, 2
    if frames <= 6:
        return 3, 2
    return 4, 2


def unity_grid(frames, width, height):
    """Python port of ArtRules.Grid, used to check the sheets this script writes."""
    if frames <= 1 or height <= 0:
        return 1, 1
    aspect = width / height
    best, best_err = (frames, 1), abs(math.log(aspect / frames))
    for rows in range(2, frames + 1):
        cols = (frames + rows - 1) // rows
        if cols * rows - frames >= cols:
            continue
        err = abs(math.log(aspect * rows / cols))
        if err < best_err - 0.01:
            best, best_err = (cols, rows), err
    return best


def save_sheet(frames, path, cell):
    n = len(frames)
    cols, rows = grid_layout(n)
    sheet = Image.new("RGBA", (cols * cell, rows * cell), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        f = f if f.size == (cell, cell) else downscale(f, cell)
        sheet.paste(f, ((i % cols) * cell, (i // cols) * cell))
    assert unity_grid(n, *sheet.size) == (cols, rows), (path, n, cols, rows, unity_grid(n, *sheet.size))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sheet.save(path, optimize=True)
    return sheet


def components_mask(img, keep_fraction=0.05, scale=4, threshold=60):
    """Alpha mask keeping only the connected blobs at least `keep_fraction` of the largest one (drops loose streaks)."""
    w, h = img.size
    small = img.getchannel("A").resize((w // scale, h // scale), Image.BILINEAR)
    sw, sh = small.size
    px = small.load()
    label = [[0] * sw for _ in range(sh)]
    sizes = [0]
    for y in range(sh):
        for x in range(sw):
            if px[x, y] <= threshold or label[y][x]:
                continue
            lid = len(sizes)
            sizes.append(0)
            stack = [(x, y)]
            label[y][x] = lid
            while stack:
                cx, cy = stack.pop()
                sizes[lid] += 1
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1), (cx + 1, cy + 1), (cx - 1, cy - 1), (cx + 1, cy - 1), (cx - 1, cy + 1)):
                    if 0 <= nx < sw and 0 <= ny < sh and not label[ny][nx] and px[nx, ny] > threshold:
                        label[ny][nx] = lid
                        stack.append((nx, ny))
    biggest = max(sizes)
    keep = {i for i, s in enumerate(sizes) if i and s >= biggest * keep_fraction}
    mask = Image.new("L", (sw, sh), 0)
    mp = mask.load()
    for y in range(sh):
        row = label[y]
        for x in range(sw):
            if row[x] in keep:
                mp[x, y] = 255
    return mask.filter(ImageFilter.MaxFilter(5)).resize((w, h), Image.BILINEAR)


def split_x(img, x):
    """Two copies of img: pixels right of x, and pixels left of it."""
    w, h = img.size
    right = Image.new("L", (w, h), 0)
    right.paste(255, (int(round(x)), 0, w, h))
    left = ImageChops.invert(right)
    a = img.getchannel("A")
    r, l = img.copy(), img.copy()
    r.putalpha(ImageChops.multiply(a, right))
    l.putalpha(ImageChops.multiply(a, left))
    return r, l


def join_layers(back, front):
    """Rejoins split layers: where they only touch (the cut line) they add up exactly; where they overlap, front wins."""
    added = ImageChops.add(back.convert("RGBa"), front.convert("RGBa")).convert("RGBA")
    over = back.copy()
    over.alpha_composite(front)
    both = ImageChops.multiply(back.getchannel("A"), front.getchannel("A"))
    mask = both.point(lambda v: 0 if v < 150 else 255 if v > 220 else int((v - 150) * 255 / 70))
    return Image.composite(over, added, mask)
