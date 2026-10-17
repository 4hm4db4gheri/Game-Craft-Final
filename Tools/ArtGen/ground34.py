"""Turns the top-down ground textures into 3/4-view ground (the camera looks down at about 55 degrees).

The art brief asked for ground "seen from straight above"; under 3/4 characters and props that reads as a flat
2D carpet. This script, for every ENV_P<n>_Ground_ground:
  * foreshortens it: the 8 x 8-unit source is drawn 8 units wide and SQUASH * 8 units tall, so pebbles, tufts and
    stains become wide ellipses like a floor seen at an angle;
  * builds one seamless texture of UNITS x UNITS arena cells (3 x 4 source repeats) so the 8-unit repeat is gone:
    organic ground mixes mirrored and shifted copies under soft periodic masks; slabbed ground (P3) is shifted so
    its joints sit on the copy edges and each slab gets its own orientation and tone;
  * adds a slow light/dark variation so large areas are not even.
The importer reads ground as 128 px per unit (ArtRules.GroundPixelsPerUnit) and the arena draws it tiled.

Every run reads the untouched file from ArtSource/ArtWave1_Original, so running it again gives the same result.
Run order:  fix_art.py, ground34.py, animate.py, then Unity: Hollow > Art > Apply Incoming Art.
"""
import os
import random

from PIL import Image, ImageChops, ImageEnhance, ImageStat

import artlib as L
import fix_art

SQUASH = 0.75   # must match ArenaTools.GroundSquash (decals use the same tilt)
UNITS = 24      # arena cells covered by one output texture
PPU = 128       # must match ArtRules.GroundPixelsPerUnit
SRC_UNITS = 8   # the brief's ground size

# P1 is organic (soil and grass); P3 is concrete slabs with joints through the middle of the source.
GROUNDS = {
    "ENV_P1_Ground_ground": dict(kind="organic", seed=11),
    "ENV_P2_Ground_ground": dict(kind="organic", seed=12),
    "ENV_P3_Ground_ground": dict(kind="slabs", seed=13, joint=(512, 510)),
}


def periodic_noise(size, cells, seed):
    """Smooth value noise that tiles with period `size`: a random grid repeated 3 x 3, upscaled, centre kept."""
    rnd = random.Random(seed)
    small = Image.new("L", (cells, cells))
    small.putdata([rnd.randint(0, 255) for _ in range(cells * cells)])
    big = Image.new("L", (cells * 3, cells * 3))
    for j in range(3):
        for i in range(3):
            big.paste(small, (i * cells, j * cells))
    return big.resize((size * 3, size * 3), Image.BICUBIC).crop((size, size, size * 2, size * 2))


def soft_threshold(noise, lo, hi):
    return noise.point(lambda v: int(255 * L.smoothstep(lo, hi, v)))


def tiled(tile, size):
    out = Image.new("RGB", (size, size))
    for y in range(0, size, tile.height):
        for x in range(0, size, tile.width):
            out.paste(tile, (x, y))
    return out


def foreshortened(src):
    tw = SRC_UNITS * PPU
    return src.convert("RGB").resize((tw, round(tw * SQUASH)), Image.LANCZOS)


def organic(src, size, seed):
    tile = foreshortened(src)
    tw, th = tile.size
    a = tiled(tile, size)
    b = ImageChops.offset(tiled(tile.transpose(Image.FLIP_LEFT_RIGHT), size), int(tw * 0.37), int(th * 0.61))
    c = ImageChops.offset(tiled(tile.transpose(Image.ROTATE_180), size), int(tw * 0.71), int(th * 0.23))
    out = Image.composite(b, a, soft_threshold(periodic_noise(size, 7, seed), 112, 144))
    return Image.composite(c, out, soft_threshold(periodic_noise(size, 6, seed + 1), 136, 166))


def feather(w, h, across_x):
    """Mask that is solid along the middle of a strip and fades to its long edges."""
    m = Image.new("L", (w, h))
    n = w if across_x else h
    line = [int(255 * L.smoothstep(0, 0.35, min(i, n - 1 - i) / n)) for i in range(n)]
    m.putdata([line[x] if across_x else line[y] for y in range(h) for x in range(w)])
    return m


def shade(img, k):
    return ImageEnhance.Brightness(img).enhance(k)


def vertical_ramp(w, h, top, bottom):
    """'L' mask going from `top` to `bottom` (0-255) down the strip."""
    m = Image.new("L", (w, h))
    m.putdata([int(top + (bottom - top) * (y / max(1, h - 1))) for y in range(h) for _ in range(w)])
    return m


# Slab look, in output pixels (128 per unit).
GAP_X, GAP_Y = 6, 4            # dark joints between slabs (the horizontal one is foreshortened)
LIP, STEP = 7, 18              # front face of every slab, plus this much per level it stands above the slab in front
EDGE_LIT, EDGE_SIDE = 3, 3     # lit top edge (light from the upper left), shaded right edge
ELEVATIONS = [(0, 0.65), (1, 0.2), (-1, 0.15)]  # level of a slab: flush, raised, sunk (damaged ground)


def slabs(src, size, seed, joint):
    """Concrete slabs seen in 3/4: each slab is a top face plus the thin front face the camera sees, with a lit top
    edge and a dark joint around it. Some slabs stand a little higher or sink, so the floor
    shows thickness and depth instead of a flat grid. Rows stay aligned (staggered rows read as a wall). The source's drawn slabs give the top faces (its old joints are
    cropped away); they are shuffled and mirrored so no slab sits next to itself."""
    src = ImageChops.offset(src.convert("RGB"), src.width - joint[0], src.height - joint[1])
    tile = foreshortened(src)
    tw, th = tile.size
    sw, sh = tw // 2, th // 2      # 4 x 3 units on screen = a 4 x 4 slab on the ground
    inset = tw // 48
    quads = [tile.crop((x + inset, y + inset, x + sw - inset, y + sh - inset)) for y in (0, sh) for x in (0, sw)]
    variants = quads + [q.transpose(Image.FLIP_LEFT_RIGHT) for q in quads]
    rnd = random.Random(seed)
    cols, rows = size // sw, size // sh
    offset = [0] * rows  # aligned rows: staggered ones with front faces read as a brick wall

    def under(r, c, dr):
        """The two slabs of row r + dr that touch slab (r, c) along its edge."""
        n = (r + dr) % rows
        k = (c * sw + offset[r] - offset[n]) // sw
        return [(n, k % cols), (n, (k + 1) % cols)]

    pick = [[-1] * cols for _ in range(rows)]
    for r in range(rows):
        for c in range(cols):
            near = {pick[r][c - 1] % 4} | {pick[n][k] % 4 for n, k in under(r, c, -1) if pick[n][k] >= 0}
            if c == cols - 1: near.add(pick[r][0] % 4)
            choices = [i for i in range(len(variants)) if i % 4 not in near] or list(range(len(variants)))
            pick[r][c] = rnd.choice(choices)
    levels, weights = zip(*ELEVATIONS)
    elev = [[rnd.choices(levels, weights)[0] for _ in range(cols)] for _ in range(rows)]

    mean = [int(v) for v in ImageStat.Stat(tile).mean]
    out = Image.new("RGB", (size, size), tuple(int(v * 0.28) for v in mean))
    w = sw - GAP_X
    for r in range(rows):
        for c in range(cols):
            e = elev[r][c]
            front = min(elev[n][k] for n, k in under(r, c, 1))
            behind = max(elev[n][k] for n, k in under(r, c, -1))
            face = LIP + STEP * max(0, e - front)
            top_h = sh - GAP_Y - face
            src_slab = variants[pick[r][c]]
            top = shade(src_slab.resize((w, top_h), Image.LANCZOS), 0.95 + rnd.random() * 0.08 + 0.05 * e)
            # Lit top edge and left edge, shaded right edge.
            top.paste(shade(top.crop((0, 0, w, EDGE_LIT)), 1.25), (0, 0))
            top.paste(shade(top.crop((0, 0, 2, top_h)), 1.1), (0, 0))
            top.paste(shade(top.crop((w - EDGE_SIDE, 0, w, top_h)), 0.78), (w - EDGE_SIDE, 0))
            if behind > e:  # the slab behind stands higher: its edge shades this one
                band = min(top_h, 44)
                top.paste(shade(top.crop((0, 0, w, band)), 0.5), (0, 0), vertical_ramp(w, band, 200, 0))
            # Front face: the slab's own concrete, darker toward the ground.
            lip = src_slab.crop((0, src_slab.height * 2 // 3, src_slab.width, src_slab.height)).resize((w, face), Image.LANCZOS)
            lip = Image.composite(shade(lip, 0.42), shade(lip, 0.68), vertical_ramp(w, face, 0, 255))
            slab = Image.new("RGB", (w, sh - GAP_Y))
            slab.paste(top, (0, 0))
            slab.paste(lip, (0, top_h))
            x = (c * sw + offset[r]) % size
            out.paste(slab, (x, r * sh))
            if x + w > size:
                out.paste(slab, (x - size, r * sh))
    return out


def macro_tone(img, seed):
    """Slow patches of darker ground so large areas are not even (period = the texture)."""
    dark = ImageEnhance.Brightness(img).enhance(0.8)
    mask = periodic_noise(img.width, 5, seed + 7).point(lambda v: int(150 * L.smoothstep(90, 200, v)))
    return Image.composite(dark, img, mask)


def build(name, spec):
    src = fix_art.original(name)
    if name == "ENV_P1_Ground_ground":
        src = fix_art.wrap(src, horizontal=True, vertical=True, band=0.08)  # the wave 1 source has seams
    size = UNITS * PPU
    if spec["kind"] == "slabs":
        img = slabs(src, size, spec["seed"], spec["joint"])
    else:
        img = organic(src, size, spec["seed"])
    img = macro_tone(img, spec["seed"])
    img.save(os.path.join(L.INCOMING, name + ".png"), optimize=True)
    print("3/4 ground", name, img.size)


def main():
    for name, spec in GROUNDS.items():
        if os.path.exists(os.path.join(L.INCOMING, name + ".png")) or os.path.exists(os.path.join(L.ORIGINALS, name + ".png")):
            build(name, spec)


if __name__ == "__main__":
    main()
