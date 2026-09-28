"""Backyard pieces: a tool shed with a stocked interior, a wheelbarrow, a tool rack, a log pile, a watering can and a hose reel.
Run inside Blender (Blender MCP). Reuses the toolkits of blender_upper1.py. Origin: centre of the footprint on the ground,
front faces -Y. Exports Assets/Art/Models/<id>.fbx and saves Blender/furniture_backyard.blend.
"""
import bpy, bmesh, math, os, random
from mathutils import Matrix, Vector

_root = r"S:\Dearlife by Zetazuni"
_u = open(os.path.join(_root, "tools", "blender_upper1.py"), encoding="utf-8").read()
exec(_u[:_u.index("# ---------------------------------------------------------------- Teacher's Room")])
_g = open(os.path.join(_root, "tools", "blender_garden.py"), encoding="utf-8").read()
exec(_g[_g.index("def tilt_box("):_g.index("# ---------------------------------------------------------------- loungers and shade")])
COLORS.update({"RoofMetal": (0.3, 0.32, 0.35), "Teak": (0.55, 0.38, 0.24), "StoneGrey": (0.6, 0.6, 0.58), "Soil": (0.25, 0.17, 0.1)})


def shed():
    """3.0 wide (x), 2.4 deep (y), 2.45 high. The door opening (1.0 x 2.05) is left empty, the builder hangs the door.
    Front (the side with the door) is -Y. The opening is x 0.35 to 1.35 (Blender), which arrives mirrored in Unity."""
    rt = root("shed")
    W, D, H = 3.0, 2.4, 2.45
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    t = 0.09
    box("floor", rt, (x0, y0, 0.0), (x1, y1, 0.12), "StoneGrey", 0.004)
    box("back wall", rt, (x0, y1 - t, 0.12), (x1, y1, H), "Cabinet_main", 0.004)
    box("left wall", rt, (x0, y0, 0.12), (x0 + t, y1, H), "Cabinet_main", 0.004)
    box("right wall", rt, (x1 - t, y0, 0.12), (x1, y1, H), "Cabinet_main", 0.004)
    do0, do1, dh = 0.35, 1.35, 2.15
    box("front left", rt, (x0, y0, 0.12), (do0, y0 + t, H), "Cabinet_main", 0.004)
    box("front right", rt, (do1, y0, 0.12), (x1, y0 + t, H), "Cabinet_main", 0.004)
    box("front lintel", rt, (do0, y0, dh), (do1, y0 + t, H), "Cabinet_main", 0.004)
    # door frame
    box("frame left", rt, (do0 - 0.03, y0 - 0.02, 0.12), (do0, y0 + t + 0.02, dh + 0.03), "BlackSteel", 0.003)
    box("frame right", rt, (do1, y0 - 0.02, 0.12), (do1 + 0.03, y0 + t + 0.02, dh + 0.03), "BlackSteel", 0.003)
    box("frame head", rt, (do0 - 0.03, y0 - 0.02, dh), (do1 + 0.03, y0 + t + 0.02, dh + 0.03), "BlackSteel", 0.003)
    # vertical teak battens on the front and the sides (the same look as the beams on the house)
    for i in range(int((W - 0.1) / 0.14)):
        x = x0 + 0.1 + i * 0.14
        if do0 - 0.08 < x + 0.03 and x < do1 + 0.08:
            continue
        box(f"batten {i}", rt, (x, y0 - 0.05, 0.14), (x + 0.06, y0, H - 0.05), "Teak", 0.003)
    # window on the right of the front wall
    box("window frame", rt, (1.7, y0 - 0.03, 1.0), (2.5, y0 + 0.02, 1.85), "BlackSteel", 0.003)
    box("window glass", rt, (1.75, y0 - 0.005, 1.05), (2.45, y0 + 0.01, 1.8), "ClearGlass", 0.001)
    # flat roof in the same design as the house: metal sheet on top, dark oak soffit underneath, black steel fascia
    lo, hi = (x0 - 0.25, y0 - 0.3), (x1 + 0.25, y1 + 0.15)
    box("roof soffit", rt, (lo[0], lo[1], H), (hi[0], hi[1], H + 0.03), "Walnut", 0.004)
    box("roof", rt, (lo[0], lo[1], H + 0.03), (hi[0], hi[1], H + 0.16), "RoofMetal", 0.006)
    f = 0.05
    box("fascia front", rt, (lo[0] - f, lo[1] - f, H - 0.02), (hi[0] + f, lo[1], H + 0.2), "BlackSteel", 0.004)
    box("fascia back", rt, (lo[0] - f, hi[1], H - 0.02), (hi[0] + f, hi[1] + f, H + 0.2), "BlackSteel", 0.004)
    box("fascia left", rt, (lo[0] - f, lo[1], H - 0.02), (lo[0], hi[1], H + 0.2), "BlackSteel", 0.004)
    box("fascia right", rt, (hi[0], lo[1], H - 0.02), (hi[0] + f, hi[1], H + 0.2), "BlackSteel", 0.004)
    # a light inside
    cyl("lamp", rt, (0.0, 0.0, H - 0.02), 0.11, 0.04, "Bulb", seg=24, bevel=0.004)
    # the inside: a shelf unit on the back wall with tins, boxes and a bucket, tools hanging on the left wall, a workbench
    for i in range(4):
        z = 0.35 + i * 0.5
        box(f"shelf {i}", rt, (-1.35, y1 - 0.5, z), (0.05, y1 - t, z + 0.035), "Teak", 0.004)
    for sx in (-1.35, 0.02):
        box(f"shelf post {sx}", rt, (sx, y1 - 0.5, 0.12), (sx + 0.04, y1 - 0.46, 2.0), "Teak", 0.004)
    rnd = random.Random(4)
    for i in range(4):
        z = 0.35 + i * 0.5 + 0.035
        x = -1.28
        while x < -0.1:
            w = rnd.uniform(0.12, 0.3)
            h = rnd.uniform(0.15, 0.34)
            m = rnd.choice(["PaintRed", "Cardboard", "Stainless", "BookBlue", "Cardboard"])
            if m == "Stainless":
                cyl(f"tin {i} {x:.2f}", rt, (x + w / 2, y1 - 0.28, z + h / 2), min(w, 0.2) / 2, h, m, seg=20, bevel=0.003)
            else:
                box(f"box {i} {x:.2f}", rt, (x, y1 - 0.45, z), (x + w, y1 - 0.12, z + h), m, 0.006)
            x += w + 0.04
    # workbench along the right wall
    box("bench top", rt, (0.75, y1 - 0.8, 0.9), (x1 - t, y1 - t, 0.95), "Walnut", 0.005)
    for sy in (y1 - 0.75, y1 - t - 0.05):
        for sx in (0.8, x1 - t - 0.06):
            box(f"bench leg {sx:.1f}{sy:.1f}", rt, (sx, sy, 0.12), (sx + 0.05, sy + 0.05, 0.9), "BlackSteel", 0.003)
    box("vise", rt, (1.2, y1 - 0.85, 0.95), (1.32, y1 - 0.7, 1.05), "PaintRed", 0.006)
    # pegboard with tools
    box("pegboard", rt, (1.3, y1 - t - 0.02, 1.15), (x1 - t, y1 - t, 2.05), "Cardboard", 0.003)
    for i in range(6):
        x = 1.38 + i * 0.2
        bar(rt, f"hung tool {i}", (x, y1 - t - 0.03, 1.95 - (i % 2) * 0.25), (x, y1 - t - 0.03, 1.4), 0.01, "BlackSteel" if i % 2 else "Teak", 8)
    # a bucket on the floor, a stack of pots
    cyl("bucket", rt, (-1.1, -0.4, 0.31), 0.16, 0.38, "Stainless", r2=0.19, seg=24, bevel=0.004)
    for i in range(3):
        cyl(f"pot {i}", rt, (-0.4, -0.6, 0.22 + i * 0.16), 0.17 - i * 0.01, 0.16, "Terracotta", r2=0.2 - i * 0.01, seg=24, bevel=0.003)
    return rt


def wheelbarrow():
    rt = root("wheelbarrow")
    tilt_box("tray", rt, (-0.35, -0.55, 0.42), (0.35, 0.35, 0.62), "PaintRed", (0, 0, 0.42), 0, 'X', 0.05)
    box("tray floor", rt, (-0.3, -0.5, 0.4), (0.3, 0.3, 0.46), "PaintRed", 0.02)
    cyl("wheel", rt, (0, -0.55, 0.19), 0.19, 0.08, "Rubber", 'X', seg=32, bevel=0.02)
    cyl("hub", rt, (0, -0.55, 0.19), 0.06, 0.1, "Stainless", 'X', seg=16, bevel=0.005)
    for sx in (-1, 1):
        bar(rt, f"handle {sx}", (sx * 0.25, 0.2, 0.42), (sx * 0.28, 0.75, 0.62), 0.022, "Teak", 12)
        bar(rt, f"leg {sx}", (sx * 0.26, 0.3, 0.42), (sx * 0.27, 0.32, 0.0), 0.02, "BlackSteel", 12)
        bar(rt, f"fork {sx}", (sx * 0.06, -0.55, 0.19), (sx * 0.2, -0.3, 0.42), 0.015, "BlackSteel", 10)
    return rt


def toolrack():
    rt = root("toolrack")
    W = 1.3
    for sx in (-1, 1):
        box(f"post {sx}", rt, (sx * W / 2 - 0.03, -0.1, 0.0), (sx * W / 2 + 0.03, 0.1, 1.5), "Teak", 0.004)
    box("top rail", rt, (-W / 2, -0.03, 1.35), (W / 2, 0.03, 1.42), "Teak", 0.004)
    box("low rail", rt, (-W / 2, -0.03, 0.4), (W / 2, 0.03, 0.46), "Teak", 0.004)
    box("foot", rt, (-W / 2 - 0.1, -0.2, 0.0), (W / 2 + 0.1, 0.2, 0.04), "Teak", 0.004)
    tools = [(-0.5, "spade"), (-0.25, "rake"), (0.0, "hoe"), (0.25, "fork"), (0.5, "broom")]
    for x, kind in tools:
        bar(rt, f"{kind} handle", (x, 0.0, 0.05), (x, 0.0, 1.45), 0.014, "Teak", 10)
        if kind == "spade":
            box("spade blade", rt, (x - 0.08, -0.01, 0.05), (x + 0.08, 0.01, 0.32), "Stainless", 0.004)
        elif kind == "rake":
            box("rake head", rt, (x - 0.15, -0.01, 0.03), (x + 0.15, 0.01, 0.07), "BlackSteel", 0.003)
        elif kind == "hoe":
            box("hoe blade", rt, (x - 0.02, -0.11, 0.03), (x + 0.02, 0.0, 0.1), "Stainless", 0.003)
        elif kind == "fork":
            for i in range(4):
                box(f"fork tine {i}", rt, (x - 0.08 + i * 0.053, -0.008, 0.02), (x - 0.062 + i * 0.053, 0.008, 0.3), "Stainless", 0.002)
        else:
            box("broom head", rt, (x - 0.1, -0.03, 0.02), (x + 0.1, 0.03, 0.3), "Rattan", 0.01)
    return rt


def logpile():
    rt = root("logpile")
    for sx in (-1, 1):
        box(f"post {sx}", rt, (sx * 0.5 - 0.03, -0.3, 0.0), (sx * 0.5 + 0.03, 0.3, 1.0), "BlackSteel", 0.004)
        box(f"foot {sx}", rt, (sx * 0.5 - 0.05, -0.32, 0.0), (sx * 0.5 + 0.05, 0.32, 0.05), "BlackSteel", 0.004)
    rnd = random.Random(8)
    for row, n in enumerate((6, 5, 6, 5, 4)):
        for i in range(n):
            x = -0.42 + i * (0.84 / max(n - 1, 1)) if n > 1 else 0
            z = 0.12 + row * 0.17
            cyl(f"log {row}.{i}", rt, (x, 0.0, z), 0.075 + rnd.uniform(-0.01, 0.01), 0.55, "Walnut", 'Y', seg=16, bevel=0.004)
    return rt


def wateringcan():
    rt = root("wateringcan")
    lathe("body", rt, [(0.0, 0.0), (0.11, 0.0), (0.12, 0.02), (0.11, 0.3), (0.09, 0.32), (0.0, 0.32)], "BlackSteel", seg=32, subsurf=1)
    bar(rt, "spout", (0.0, -0.1, 0.12), (0.0, -0.36, 0.34), 0.02, "BlackSteel", 12)
    cyl("rose", rt, (0.0, -0.37, 0.35), 0.05, 0.025, "BlackSteel", 'Y', seg=16, bevel=0.003)
    torus("handle", rt, (0.0, 0.11, 0.2), 0.11, 0.012, "BlackSteel", 24, 8, 'X')
    return rt


def hosereel():
    rt = root("hosereel")
    for sx in (-1, 1):
        box(f"side {sx}", rt, (sx * 0.16 - 0.01, -0.2, 0.0), (sx * 0.16 + 0.01, 0.2, 0.36), "BlackSteel", 0.003)
    box("base", rt, (-0.18, -0.2, 0.0), (0.18, 0.2, 0.03), "BlackSteel", 0.003)
    cyl("drum", rt, (0, 0, 0.22), 0.12, 0.3, "Leaf", 'X', seg=32, bevel=0.005)
    for k in range(5):
        torus(f"coil {k}", rt, (-0.12 + k * 0.06, 0, 0.22), 0.13, 0.012, "Leaf", 32, 8, 'X')
    return rt


PIECES = [shed, wheelbarrow, toolrack, logpile, wateringcan, hosereel]


def build_all():
    fresh()
    out = {}
    for fn in PIECES:
        rt = fn()
        out[rt.name] = stats(rt)
        export(rt, rt.name)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT, "Blender", "furniture_backyard.blend"))
    return out


if globals().get("RUN_BACKYARD", True):
    RESULT = build_all()
    print(RESULT)
