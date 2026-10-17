"""Builds animation sheets for every character state from the single drawn frames of art wave 1.

Each drawn frame is bent with a fine mesh: legs swing around the hip, the body bobs, leans, squashes and
recoils, so a state gets real frames instead of one still picture. Every sheet starts or ends on the drawn
frame (death ends on it, so corpses look the same). Missing states (boss 1 move, the Operator's phase two
looks) are made from the nearest drawn state.

Output: Assets/Art/Incoming/Generated/<CAT>_<ID>_<Name>_<state>_f<n>.png. The importer reads them like any
sheet; hand-drawn sheets of the same state always win (ApplyIncomingArt.Collect). A state that already has a
hand-drawn sheet is skipped here.

Run:  python Tools/ArtGen/animate.py [--preview DIR] [only-this-prefix ...]
Then in Unity: Hollow > Art > Apply Incoming Art (with the Game scene open).
"""
import math
import os
import sys
from dataclasses import dataclass, field, replace

from PIL import Image, ImageDraw

import artlib as L

CELL = 512  # pixels per generated frame (a 2-unit character is ~110 px on screen; bosses ~330 px)
TAU = math.pi * 2


@dataclass
class Body:
    """Where an entity bends, as fractions of its drawn bounding box (x from the left, y from the feet up)."""
    kind: str                 # quad, biped, hover
    belly: float = 0.45       # legs start this high
    split: float = 0.5        # front legs are right of this, back legs left of it
    front: float = 0.7        # front leg pivot
    back: float = 0.3         # back leg pivot
    leg: float = 12.0         # leg swing, degrees
    ranged: bool = False      # attack recoils instead of lunging
    open_legs: bool = False   # biped move frame drawn with feet together: the walk opens them
    heavy: float = 1.0        # scales bob and lean (bosses move less)


@dataclass
class Pose:
    dx: float = 0.0       # fractions of height; +x forward (right), +y down
    dy: float = 0.0
    rot: float = 0.0      # degrees about the ground point, + leans forward
    pitch: float = 0.0    # degrees about the body centre
    sx: float = 1.0
    sy: float = 1.0
    breath: float = 0.0   # extra vertical scale of the body above the legs
    front: float = 0.0    # front leg swing, + moves the foot back
    back: float = 0.0
    ground: bool = True   # keep the lowest point on the ground line
    air: float = 0.0      # lift after grounding (fraction of height)
    glow: float = 1.0     # brightness of green glow pixels


def pose_frame(img, body: Body, p: Pose):
    x0, y0, x1, y1 = L.alpha_bbox(img)
    w, h = x1 - x0, y1 - y0
    gx, gy = img.size[0] / 2, y1
    hip = y1 - body.belly * h
    split = x0 + body.split * w
    fpx, bpx = x0 + body.front * w, x0 + body.back * w
    if body.kind == "biped":
        gap = leg_gap(img, x0, x1, y1 - 0.14 * h)
        if gap is not None:
            split = gap
            fpx, bpx = split + 0.05 * w, split - 0.05 * w
    cx, cy = (x0 + x1) / 2, (y0 + hip) / 2
    band, bx = 0.08 * h, 0.06 * w

    def inverse_for(leg):
        def inverse(x, y):
            x, y = x - p.dx * h, y - p.dy * h
            if p.rot:
                x, y = L.rotate_point(x, y, gx, gy, -p.rot)
            if p.pitch:
                x, y = L.rotate_point(x, y, cx, cy, -p.pitch)
            x, y = gx + (x - gx) / p.sx, gy + (y - gy) / p.sy
            if p.breath:
                up = 1 - L.smoothstep(hip - 0.05 * h, hip + 0.05 * h, y)
                y = y + (hip - y) * (1 - 1 / (1 + p.breath)) * up
            wl = L.smoothstep(hip - band, hip + band, y)
            if leg == "front" and p.front:
                x, y = L.rotate_point(x, y, fpx, hip, -p.front * wl)
            elif leg == "back" and p.back:
                x, y = L.rotate_point(x, y, bpx, hip, -p.back * wl)
            elif leg == "blend" and (p.front or p.back):
                wf = L.smoothstep(split - bx, split + bx, x)
                if p.front and wl * wf > 0:
                    x, y = L.rotate_point(x, y, fpx, hip, -p.front * wl * wf)
                if p.back and wl * (1 - wf) > 0:
                    x, y = L.rotate_point(x, y, bpx, hip, -p.back * wl * (1 - wf))
            return x, y
        return inverse

    if body.kind == "biped" and (p.front or p.back):
        # Two-legged: each leg is its own layer, so legs can cross or open without stretching the gap between them.
        front_src, back_src = L.split_x(img, split)
        front = L.warp(front_src, inverse_for("front"))
        back = L.warp(back_src, inverse_for("back"))
        out = L.join_layers(back, front)
    else:
        out = L.warp(img, inverse_for("blend"))
    if p.ground:
        bb = L.alpha_bbox(out)
        if bb:
            out = L.shift(out, 0, gy - bb[3])
    if p.air:
        out = L.shift(out, 0, -p.air * h)
    if p.glow != 1.0:
        out = L.brighten_where(out, L.tint_mask(out, 1, 40), p.glow)
    return out


def leg_gap(img, x0, x1, y):
    """Middle of the widest gap between the two legs on row y (feet drawn apart), else the middle of the feet."""
    a = img.getchannel("A")
    runs, start = [], None
    for x in range(int(x0), int(x1) + 1):
        solid = x < x1 and max(a.getpixel((x, int(y) + d)) for d in (-6, 0, 6)) > 60
        if solid and start is None:
            start = x
        elif not solid and start is not None:
            if x - start > 6:
                runs.append((start, x))
            start = None
    if not runs:
        return None
    if len(runs) == 1:
        return (runs[0][0] + runs[0][1]) / 2
    gaps = [(runs[i + 1][0] - runs[i][1], (runs[i][1] + runs[i + 1][0]) / 2) for i in range(len(runs) - 1)]
    return max(gaps)[1]


def frames(img, body, poses):
    return [pose_frame(img, body, p) for p in poses]


# ---- state recipes ---------------------------------------------------------------------------------------

def idle(body, n=8):
    k = body.heavy
    if body.kind == "hover":
        return [Pose(dy=-0.03 * math.sin(TAU * i / n), ground=False) for i in range(n)]
    return [Pose(breath=0.014 * k * math.sin(TAU * i / n), rot=0.5 * k * math.sin(TAU * i / n + 1.0)) for i in range(n)]


def move(body, n=6):
    k = body.heavy
    out = []
    for i in range(n):
        ph = TAU * i / n
        s = math.sin(ph)
        if body.kind == "hover":
            out.append(Pose(rot=6, dy=-0.04 * math.sin(ph * 2) * 0.5 - 0.02, sx=1 + 0.02 * s, ground=False))
        elif body.kind == "quad":
            out.append(Pose(front=body.leg * s, back=-body.leg * s, pitch=2.5 * k * math.cos(ph),
                            sx=1 + 0.025 * k * s, air=0.035 * k * max(0.0, math.sin(ph + 0.5))))
        else:
            c = (1 - math.cos(ph)) / 2
            sign = -1 if body.open_legs else 1
            out.append(Pose(front=sign * body.leg * c, back=-sign * body.leg * c,
                            rot=1.5 * k + 1.2 * k * math.sin(ph), breath=0.01 * k * c))
    return out


def windup(body, n=4):
    out = []
    for i in range(n):
        e = (i / (n - 1)) ** 0.7
        out.append(Pose(sy=1 - 0.05 * e * body.heavy, sx=1 + 0.03 * e * body.heavy, rot=-3 * e * body.heavy,
                        glow=1 + 0.5 * e, ground=body.kind != "hover"))
    return out


def attack(body):
    if body.ranged:
        return [Pose(dx=-0.035, rot=-2.5), Pose(dx=-0.018, rot=-1.2), Pose(dx=-0.006), Pose()]
    return [Pose(sx=1.10, dx=0.05, rot=4), Pose(sx=1.05, dx=0.025, rot=2), Pose(sx=0.98, dx=-0.005), Pose()]


def hit(body):
    g = body.kind != "hover"
    return [Pose(dx=-0.045, rot=-6, sy=0.96, ground=g), Pose(dx=-0.02, rot=-2.5, ground=g), Pose(ground=g)]


def recover(body):
    return [Pose(sy=0.95, sx=1.03), Pose(sy=1.01, sx=0.995), Pose()]


def death_fall(body):
    """First frames of a death: the standing pose tips back (drawn death frame follows)."""
    if body.kind == "hover":
        return [Pose(rot=-12, dx=-0.03, ground=False), Pose(rot=-25, dy=0.05, ground=False)]
    tilt = 10 if body.kind == "quad" else 18
    return [Pose(rot=-tilt * 0.5, dx=-0.02), Pose(rot=-tilt, dx=-0.04, sy=0.97)]


def death_land():
    return [Pose(air=0.06), Pose(sy=0.9, sx=1.05), Pose(sy=1.03, sx=0.99), Pose()]


def fall_over(img, body):
    """A death made from a standing frame only: falls back and lies down (Operator phase two)."""
    out = []
    # Lying down it reaches back by its height: slide forward as it tips so it stays centred on the ground point.
    for i, a in enumerate([0, -20, -45, -70, -86, -86, -86]):
        lift = {5: 0.04}.get(i, 0.0)
        squash = 0.92 if i == 6 else 1.0
        out.append(pose_frame(img, body, Pose(rot=a, dx=0.5 * math.sin(math.radians(-a)), sy=squash, air=lift)))
    out.append(pose_frame(img, body, Pose(rot=-86, dx=0.5 * math.sin(math.radians(86)))))
    return out[:3] + out[3:4] + out[5:]


def shake(n=6, amount=0.02):
    return [Pose(dx=amount * math.sin(i * 2.4) * (1 if i % 2 else -1), rot=1.5 * math.sin(i * 1.7)) for i in range(n)]


# ---- entities --------------------------------------------------------------------------------------------

BODIES = {
    "CHR_PLY_Core": Body("hover"),
    "CHR_OPR_P1": Body("biped", belly=0.46, split=0.5, front=0.55, back=0.45, leg=16, ranged=True),
    "CHR_OPR_P2": Body("biped", belly=0.46, split=0.5, front=0.55, back=0.45, leg=11, ranged=True, open_legs=True),
    "ENM_A1_RunnerWolf": Body("quad", belly=0.5, split=0.55, front=0.72, back=0.3, leg=14),
    "ENM_A2_QuillShooter": Body("quad", belly=0.28, split=0.55, front=0.68, back=0.32, leg=10, ranged=True),
    "ENM_R1_Crawler": Body("quad", belly=0.55, split=0.5, front=0.65, back=0.35, leg=12),
    "ENM_R2_Shooter": Body("biped", belly=0.5, split=0.48, front=0.55, back=0.4, leg=12, ranged=True),
    "ENM_H2_RifleGuard": Body("biped", belly=0.46, split=0.5, front=0.55, back=0.45, leg=16, ranged=True),
    "BOS_B1_LargeBeast": Body("quad", belly=0.42, split=0.55, front=0.7, back=0.3, leg=9, heavy=0.8),
    "BOS_B2_IndustrialRobot": Body("biped", belly=0.4, split=0.5, front=0.6, back=0.4, leg=8, ranged=True, heavy=0.6),
}


def has_hand_drawn_sheet(key, state):
    prefix = f"{key}_{state}_f"
    return any(f.startswith(prefix) and f.endswith(".png") for f in os.listdir(L.INCOMING))


def plan(key):
    """state -> list of frames for one entity."""
    body = BODIES[key]
    src = lambda s: L.load(f"{key}_{s}") if L.exists(f"{key}_{s}") else None
    idle_img = src("idle")
    out = {}

    def add(state, img, poses):
        if img is not None:
            out[state] = frames(img, body, poses)

    add("idle", idle_img, idle(body))
    move_img = src("move")
    if move_img is None and body.kind == "quad":
        move_img = idle_img  # boss 1 has no drawn move frame
    if move_img is None and key == "CHR_OPR_P2":
        move_img = idle_img
    add("move", move_img, move(body))

    hit_img = src("hit")
    if body.kind != "hover" and not key.startswith("BOS"):
        add("hit", hit_img or idle_img, hit(body))
    elif hit_img is not None:
        add("hit", hit_img, hit(body))

    if key.startswith("ENM"):
        add("windup", src("windup"), windup(body))
        add("attack", src("attack"), attack(body))
        add("recover", idle_img, recover(body))
    if key.startswith("CHR_OPR"):
        add("attack", src("attack") or idle_img, attack(body))

    death_img = src("death")
    if death_img is not None:
        start = hit_img or idle_img
        out["death"] = frames(start, body, death_fall(body)) + frames(death_img, body, death_land())
    elif key == "CHR_OPR_P2":
        out["death"] = fall_over(idle_img, body)

    if key == "BOS_B1_LargeBeast":
        heavy = replace(body, heavy=1.0)
        add("windup_charge", src("windup_charge"), windup(heavy))
        charge_body = replace(body, leg=14, heavy=1.2)
        add("charge", src("charge"), move(charge_body))
        add("recover", src("recover"), recover(body))
        rear = [Pose(sy=1 + 0.015 * i, rot=-1.5 * i) for i in range(4)]
        add("windup_strike", src("windup_strike"), rear)
        add("strike", src("strike"), [Pose(sy=0.9, sx=1.07, dx=0.03), Pose(sy=0.95, sx=1.03), Pose(sy=1.01), Pose()])
    if key == "BOS_B2_IndustrialRobot":
        add("windup_burst", src("windup_burst"), windup(body))
        add("burst", src("burst"), [Pose(dx=-0.012, rot=-0.8), Pose(dx=0.004), Pose(dx=-0.009, rot=-0.5), Pose(dx=0.002)])
        pulse = [Pose(sy=1 + 0.01 * i, glow=1 + 0.35 * i) for i in range(4)]
        add("windup_blast", src("windup_blast"), pulse)
        add("blast", src("blast"), [Pose(sy=0.92, sx=1.05, glow=1.6), Pose(sy=0.96, glow=1.3), Pose(sy=1.01, glow=1.1), Pose()])
        add("stunned", src("stunned"), shake())
    return out


def save(key, states, preview_dir=None):
    os.makedirs(L.GENERATED, exist_ok=True)
    written = []
    for state, frs in states.items():
        if has_hand_drawn_sheet(key, state):
            print(f"  skip {key} {state}: a hand-drawn sheet exists")
            continue
        name = f"{key}_{state}_f{len(frs)}.png"
        cell = CELL
        L.save_sheet([L.downscale(f, cell) for f in frs], os.path.join(L.GENERATED, name), cell)
        written.append(name)
        if preview_dir:
            preview(frs, os.path.join(preview_dir, name))
    # Remove sheets of this entity from earlier runs that this run did not write (e.g. frame count changed).
    for f in os.listdir(L.GENERATED):
        if f.startswith(key + "_") and f.endswith(".png") and f not in written:
            os.remove(os.path.join(L.GENERATED, f))
            meta = os.path.join(L.GENERATED, f + ".meta")
            if os.path.exists(meta):
                os.remove(meta)
    return written


def preview(frs, path, size=200):
    strip = Image.new("RGBA", (size * len(frs), size), (92, 104, 88, 255))
    d = ImageDraw.Draw(strip)
    for i, f in enumerate(frs):
        strip.alpha_composite(L.downscale(f, size), (i * size, 0))
        d.line([(i * size, int(size * 0.9)), ((i + 1) * size, int(size * 0.9))], fill=(255, 255, 255, 90))
        d.line([(i * size, 0), (i * size, size)], fill=(0, 0, 0, 120))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    strip.save(path)


def main(argv):
    preview_dir = None
    if "--preview" in argv:
        i = argv.index("--preview")
        preview_dir = argv[i + 1]
        argv = argv[:i] + argv[i + 2:]
    keys = [k for k in BODIES if not argv or any(k.startswith(a) for a in argv)]
    for key in keys:
        if not L.exists(f"{key}_idle"):
            print(f"{key}: no idle frame, skipped")
            continue
        names = save(key, plan(key), preview_dir)
        print(f"{key}: {len(names)} sheet(s)")


if __name__ == "__main__":
    main(sys.argv[1:])
