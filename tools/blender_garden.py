"""Garden furniture for the front yard (like the yard of the 2D Tiramisu App): loungers, parasol, BBQ area,
outdoor dining and sofa, fire pit, hammock, fountain and friends. Run inside Blender (Blender MCP).

Reuses the toolkits of blender_upper1.py (which loads the kitchen, bath and props helpers).
Origin: centre of the footprint on the ground. Front faces -Y. Exports Assets/Art/Models/<id>.fbx and saves
Blender/furniture_garden.blend.
"""
import bpy, bmesh, math, os, random
from mathutils import Matrix, Vector

_root = r"S:\Dearlife by Zetazuni"
_u = open(os.path.join(_root, "tools", "blender_upper1.py"), encoding="utf-8").read()
exec(_u[:_u.index("# ---------------------------------------------------------------- Teacher's Room")])

COLORS.update({
    "Teak": (0.55, 0.38, 0.24), "Umbrella": (0.9, 0.55, 0.42), "StoneGrey": (0.6, 0.6, 0.58), "FlowerPink": (0.95, 0.5, 0.65),
    "FlowerYellow": (0.98, 0.85, 0.2), "FlowerWhite": (0.96, 0.96, 0.94), "Flamingo": (0.98, 0.5, 0.6), "GnomeSkin": (0.9, 0.7, 0.6),
    "OutdoorFabric_main": (0.85, 0.82, 0.75), "CoolerBlue": (0.2, 0.45, 0.75), "FireGlow": (1.0, 0.5, 0.1), "Soil": (0.25, 0.17, 0.1),
    "BallRed": (0.85, 0.1, 0.1), "BallWhite": (0.95, 0.95, 0.95), "BallBlue": (0.15, 0.35, 0.8), "BallYellow": (0.95, 0.8, 0.1),
})


def tilt_box(name, parent, lo, hi, material, pivot, deg, axis='X', bevel=0.01):
    ob = box(name, parent, lo, hi, material, bevel)
    ob.data.transform(Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-Vector(pivot)))
    return ob


# ---------------------------------------------------------------- loungers and shade

def lounger():
    rt = root("lounger")
    W, L = 0.72, 2.0
    for sx in (-1, 1):
        box(f"rail {sx}", rt, (sx * W / 2 - 0.03 if sx > 0 else -W / 2, -L / 2, 0.28), (W / 2 if sx > 0 else -W / 2 + 0.03, L / 2, 0.33), "Teak", 0.006)
        for sy in (-1, 1):
            box(f"leg {sx}{sy}", rt, (sx * (W / 2 - 0.05) - 0.025, sy * (L / 2 - 0.1) - 0.025, 0.0), (sx * (W / 2 - 0.05) + 0.025, sy * (L / 2 - 0.1) + 0.025, 0.3), "Teak", 0.004)
    for i in range(9):
        y = -L / 2 + 0.05 + i * (L * 0.62 / 8)
        box(f"slat {i}", rt, (-W / 2, y, 0.33), (W / 2, y + 0.07, 0.355), "Teak", 0.004)
    box("seat pad", rt, (-W / 2 + 0.03, -L / 2 + 0.02, 0.355), (W / 2 - 0.03, 0.2, 0.42), "OutdoorFabric_main", 0.03)
    piv = (0, 0.22, 0.36)
    tilt_box("back frame", rt, (-W / 2, 0.22, 0.33), (W / 2, 0.9, 0.355), "Teak", piv, 58, 'X', 0.004)
    tilt_box("back pad", rt, (-W / 2 + 0.03, 0.22, 0.355), (W / 2 - 0.03, 0.9, 0.42), "OutdoorFabric_main", piv, 58, 'X', 0.03)
    box("wheel bar", rt, (-W / 2, -L / 2 + 0.02, 0.04), (W / 2, -L / 2 + 0.06, 0.08), "BlackSteel", 0.004)
    return rt


def parasol():
    rt = root("parasol")
    cyl("base", rt, (0, 0, 0.05), 0.32, 0.1, "StoneGrey", seg=48, bevel=0.01)
    cyl("pole", rt, (0, 0, 1.4), 0.025, 2.8, "Teak", seg=20, bevel=0.003)
    outer = [(0.0, 2.95), (0.45, 2.88), (1.0, 2.7), (1.6, 2.42), (1.65, 2.38)]
    inner = [(1.6, 2.36), (1.0, 2.64), (0.45, 2.82), (0.0, 2.88)]
    lathe("canopy", rt, outer + inner, "Umbrella", seg=64, subsurf=1)
    for i in range(8):
        a = math.radians(i * 45)
        bar(rt, f"rib {i}", (0, 0, 2.86), (math.cos(a) * 1.62, math.sin(a) * 1.62, 2.4), 0.008, "Teak", 8)
    cyl("finial", rt, (0, 0, 2.99), 0.03, 0.08, "Teak", seg=16, bevel=0.005)
    return rt


# ---------------------------------------------------------------- BBQ area

def bbqcounter():
    rt = root("bbqcounter")
    W, D, H = 2.4, 0.7, 0.9
    box("plinth", rt, (-W / 2 + 0.03, -D / 2 + 0.04, 0), (W / 2 - 0.03, D / 2, 0.08), "BlackSteel", 0.002)
    box("carcass", rt, (-W / 2, -D / 2 + 0.02, 0.08), (W / 2, D / 2, H - 0.05), "Cabinet_main", 0.004)
    for i in range(3):
        a = -W / 2 + 0.02 + i * (W - 0.04) / 3
        box(f"door {i}", rt, (a + 0.003, -D / 2 - 0.015, 0.1), (a + (W - 0.04) / 3 - 0.003, -D / 2 + 0.02, H - 0.07), "Cabinet_main", 0.004)
        cyl(f"pull {i}", rt, (a + (W - 0.04) / 6, -D / 2 - 0.04, H - 0.15), 0.006, 0.3, "Stainless", 'X', seg=12, bevel=0.001)
    box("top", rt, (-W / 2 - 0.02, -D / 2 - 0.03, H - 0.05), (W / 2 + 0.02, D / 2 + 0.01, H), "StoneGrey", 0.006)
    box("splash", rt, (-W / 2, D / 2 - 0.02, H), (W / 2, D / 2, H + 0.18), "StoneGrey", 0.004)
    box("sink", rt, (-0.7, -0.1, H - 0.004), (-0.2, 0.2, H + 0.003), "GlassDark", 0.002)
    cyl("tap", rt, (-0.45, 0.28, H + 0.15), 0.012, 0.3, "BlackSteel", seg=16, bevel=0.002)
    bar(rt, "tap spout", (-0.45, 0.28, H + 0.3), (-0.45, 0.14, H + 0.3), 0.011, "BlackSteel", 12)
    return rt


def bbq():
    rt = root("bbq")
    W, D = 1.3, 0.6
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl(f"leg {sx}{sy}", rt, (sx * (W / 2 - 0.08), sy * (D / 2 - 0.08), 0.42), 0.02, 0.84, "BlackSteel", seg=16, bevel=0.002)
        cyl(f"wheel {sx}", rt, (sx * (W / 2 - 0.08), D / 2 - 0.08, 0.06), 0.06, 0.04, "Rubber", 'X', seg=24, bevel=0.005)
    box("shelf", rt, (-W / 2 + 0.03, -D / 2 + 0.03, 0.22), (W / 2 - 0.03, D / 2 - 0.03, 0.245), "Stainless", 0.004)
    box("firebox", rt, (-W / 2, -D / 2, 0.84), (W / 2, D / 2, 1.1), "BlackSteel", 0.02)
    box("grate", rt, (-W / 2 + 0.04, -D / 2 + 0.05, 1.1), (W / 2 - 0.04, D / 2 - 0.05, 1.115), "Stainless", 0.002)
    for i in range(10):
        y = -D / 2 + 0.08 + i * (D - 0.16) / 9
        cyl(f"grill bar {i}", rt, (0, y, 1.122), 0.006, W - 0.1, "BlackSteel", 'X', seg=8, bevel=0.0005)
    for i in range(6):
        x = -W / 2 + 0.2 + i * (W - 0.4) / 5
        cyl(f"coal {i}", rt, (x, 0.0, 1.105), 0.05, 0.02, "FireGlow", seg=10, bevel=0.002)
    tilt_box("lid", rt, (-W / 2, -D / 2, 1.1), (W / 2, D / 2, 1.28), "BlackSteel", (0, D / 2, 1.1), -52, 'X', 0.03)
    bar(rt, "lid handle", (-0.4, -D / 2 - 0.12, 1.32), (0.4, -D / 2 - 0.12, 1.32), 0.012, "Stainless", 10)
    box("hinge", rt, (-W / 2, D / 2 - 0.03, 1.1), (W / 2, D / 2, 1.14), "BlackSteel", 0.004)
    cyl("chimney", rt, (0.5, D / 2 - 0.08, 1.35), 0.04, 0.16, "BlackSteel", seg=16, bevel=0.005)
    for i in range(4):
        cyl(f"knob {i}", rt, (-0.36 + i * 0.24, -D / 2 - 0.02, 0.98), 0.028, 0.03, "Stainless", 'Y', seg=16, bevel=0.003)
    box("side table", rt, (W / 2, -D / 2 + 0.05, 0.9), (W / 2 + 0.35, D / 2 - 0.05, 0.925), "Stainless", 0.004)
    return rt


def cooler():
    rt = root("cooler")
    box("body", rt, (-0.36, -0.2, 0.05), (0.36, 0.2, 0.42), "CoolerBlue", 0.04)
    box("lid", rt, (-0.37, -0.21, 0.42), (0.37, 0.21, 0.5), "PlasticWhite", 0.04)
    box("handle", rt, (-0.12, -0.235, 0.36), (0.12, -0.2, 0.4), "PlasticWhite", 0.01)
    for sx in (-1, 1):
        cyl(f"foot {sx}", rt, (sx * 0.28, 0.12, 0.03), 0.04, 0.06, "Rubber", 'X', seg=16, bevel=0.004)
        box(f"latch {sx}", rt, (sx * 0.3 - 0.03, -0.215, 0.38), (sx * 0.3 + 0.03, -0.2, 0.46), "Stainless", 0.004)
    return rt


def telescope():
    rt = root("telescope")
    top = Vector((0, 0, 1.15))
    for i in range(3):
        a = math.radians(i * 120 + 90)
        bar(rt, f"leg {i}", top, (math.cos(a) * 0.55, math.sin(a) * 0.55, 0.0), 0.014, "BlackSteel", 12)
    cyl("head", rt, (0, 0, 1.17), 0.05, 0.08, "BlackSteel", seg=20, bevel=0.005)
    a, b = Vector((0, 0.25, 1.05)), Vector((0, -0.55, 1.5))
    bar(rt, "tube", a, b, 0.055, "Stainless", 24)
    bar(rt, "tube front", b, b + (b - a).normalized() * 0.06, 0.07, "BlackSteel", 24)
    bar(rt, "finder", a.lerp(b, 0.35) + Vector((0.07, 0, 0.05)), a.lerp(b, 0.75) + Vector((0.07, 0, 0.05)), 0.018, "BlackSteel", 12)
    return rt


def outdoorrug():
    rt = root("outdoorrug")
    box("border", rt, (-1.6, -1.15, 0.0), (1.6, 1.15, 0.014), "RugBorder", 0.004)
    box("field", rt, (-1.5, -1.05, 0.014), (1.5, 1.05, 0.02), "Rug_main", 0.002)
    for i in range(7):
        box(f"stripe {i}", rt, (-1.5, -1.05 + 0.15 + i * 0.28, 0.02), (1.5, -1.05 + 0.2 + i * 0.28, 0.024), "RugBorder", 0.001)
    return rt


# ---------------------------------------------------------------- dining and lounge

def longdining():
    rt = root("longdining")
    L, W, H = 2.6, 0.95, 0.76
    for i in range(9):
        y = -W / 2 + 0.005 + i * (W - 0.01) / 9
        box(f"top plank {i}", rt, (-L / 2, y, H - 0.045), (L / 2, y + (W - 0.01) / 9 - 0.008, H), "Teak", 0.004)
    for sx in (-1, 1):
        box(f"trestle {sx}", rt, (sx * (L / 2 - 0.35) - 0.03, -W / 2 + 0.08, 0), (sx * (L / 2 - 0.35) + 0.03, W / 2 - 0.08, H - 0.045), "BlackSteel", 0.004)
    box("stretcher", rt, (-L / 2 + 0.4, -0.02, 0.3), (L / 2 - 0.4, 0.02, 0.34), "BlackSteel", 0.004)
    for sy in (-1, 1):
        y0 = sy * 0.72
        box(f"bench seat {sy}", rt, (-L / 2 + 0.1, y0 - 0.17, 0.42), (L / 2 - 0.1, y0 + 0.17, 0.46), "Teak", 0.008)
        for sx in (-1, 1):
            box(f"bench leg {sx}{sy}", rt, (sx * (L / 2 - 0.4) - 0.025, y0 - 0.14, 0), (sx * (L / 2 - 0.4) + 0.025, y0 + 0.14, 0.42), "BlackSteel", 0.003)
        box(f"bench pad {sy}", rt, (-L / 2 + 0.25, y0 - 0.15, 0.46), (L / 2 - 0.25, y0 + 0.15, 0.5), "OutdoorFabric_main", 0.02)
    return rt


def outdoorsectional():
    rt = root("outdoorsectional")
    W, D = 2.6, 0.95
    box("main base", rt, (-W / 2, -D / 2, 0.08), (W / 2, D / 2, 0.32), "Teak", 0.012)
    box("return base", rt, (W / 2 - D, -D / 2 - 1.55, 0.08), (W / 2, -D / 2, 0.32), "Teak", 0.012)
    for pos in [(-W / 2 + 0.1, D / 2 - 0.1), (W / 2 - 0.1, D / 2 - 0.1), (-W / 2 + 0.1, -D / 2 + 0.1), (W / 2 - D + 0.1, -D / 2 - 1.5)]:
        box("foot", rt, (pos[0] - 0.03, pos[1] - 0.03, 0), (pos[0] + 0.03, pos[1] + 0.03, 0.08), "BlackSteel", 0.003)
    for i in range(3):
        a = -W / 2 + 0.03 + i * (W - 0.06) / 3
        box(f"seat {i}", rt, (a + 0.01, -D / 2 + 0.04, 0.32), (a + (W - 0.06) / 3 - 0.01, D / 2 - 0.2, 0.46), "OutdoorFabric_main", 0.045)
        box(f"back {i}", rt, (a + 0.01, D / 2 - 0.2, 0.32), (a + (W - 0.06) / 3 - 0.01, D / 2 - 0.02, 0.78), "OutdoorFabric_main", 0.05)
    box("return seat", rt, (W / 2 - D + 0.04, -D / 2 - 1.55 + 0.04, 0.32), (W / 2 - 0.2, -D / 2 - 0.02, 0.46), "OutdoorFabric_main", 0.045)
    box("return back", rt, (W / 2 - 0.2, -D / 2 - 1.55 + 0.04, 0.32), (W / 2 - 0.02, -D / 2 - 0.02, 0.78), "OutdoorFabric_main", 0.05)
    box("arm left", rt, (-W / 2 - 0.02, -D / 2, 0.32), (-W / 2 + 0.08, D / 2, 0.6), "Teak", 0.02)
    box("arm end", rt, (W / 2 - D, -D / 2 - 1.57, 0.32), (W / 2, -D / 2 - 1.47, 0.6), "Teak", 0.02)
    for i, (x, y, m) in enumerate([(-0.9, 0.05, "Umbrella"), (-0.55, 0.1, "BookBlue"), (0.9, 0.1, "Umbrella")]):
        tilt_box(f"throw pillow {i}", rt, (x - 0.2, y + 0.05, 0.46), (x + 0.2, y + 0.12, 0.86), m, (x, y, 0.46), -12, 'X', 0.05)
    return rt


def firepit():
    rt = root("firepit")
    ring = [(0.0, 0.0), (0.58, 0.0), (0.62, 0.05), (0.64, 0.4), (0.6, 0.44), (0.5, 0.44), (0.46, 0.4), (0.44, 0.06), (0.0, 0.06)]
    lathe("stone ring", rt, ring, "StoneGrey", seg=48, subsurf=1)
    cyl("ash", rt, (0, 0, 0.1), 0.42, 0.03, "BlackSteel", seg=32, bevel=0.003)
    rnd = random.Random(9)
    for i in range(6):
        a = math.radians(i * 60 + 12)
        bar(rt, f"log {i}", (math.cos(a) * 0.33, math.sin(a) * 0.33, 0.14), (math.cos(a + 3.14) * 0.05, math.sin(a + 3.14) * 0.05, 0.3), 0.045, "Teak", 12)
    for i in range(5):
        a = math.radians(i * 72)
        r = 0.12 if i else 0.0
        h = rnd.uniform(0.22, 0.4)
        sphere(f"flame {i}", rt, (math.cos(a) * r, math.sin(a) * r, 0.28 + h * 0.5), 0.09, "FireGlow", (0.8, 0.8, h * 6), seg=12, rings=8)
    return rt


def lantern():
    rt = root("lantern")
    box("base", rt, (-0.11, -0.11, 0.0), (0.11, 0.11, 0.05), "BlackSteel", 0.008)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"post {sx}{sy}", rt, (sx * 0.1 - 0.01, sy * 0.1 - 0.01, 0.05), (sx * 0.1 + 0.01, sy * 0.1 + 0.01, 0.42), "BlackSteel", 0.002)
    box("glass", rt, (-0.098, -0.098, 0.055), (0.098, 0.098, 0.41), "ClearGlass", 0.002)
    cyl("candle", rt, (0, 0, 0.16), 0.045, 0.22, "Wax", seg=24, bevel=0.005)
    sphere("flame", rt, (0, 0, 0.3), 0.022, "Flame", (0.8, 0.8, 1.8), seg=12, rings=8)
    box("cap", rt, (-0.13, -0.13, 0.42), (0.13, 0.13, 0.46), "BlackSteel", 0.01)
    torus("handle", rt, (0, 0, 0.5), 0.09, 0.008, "BlackSteel", 32, 8, 'X')
    return rt


# ---------------------------------------------------------------- lawn things

def hammock():
    rt = root("hammock")
    L = 3.2
    for sy in (-1, 1):
        for sx in (-1, 1):
            bar(rt, f"a leg {sx}{sy}", (sx * 0.35, sy * (L / 2), 0.0), (0, sy * (L / 2 - 0.12), 1.6), 0.03, "Teak", 12)
        cyl(f"hook {sy}", rt, (0, sy * (L / 2 - 0.12), 1.58), 0.04, 0.05, "BlackSteel", 'Y', seg=12, bevel=0.005)
    bar(rt, "top bar", (0, -L / 2 + 0.12, 1.62), (0, L / 2 - 0.12, 1.62), 0.02, "Teak", 12) if False else None
    n = 24
    secs = []
    for i in range(n + 1):
        t = i / n
        y = -1.45 + 2.9 * t
        z = 1.35 - 0.62 * (1 - (2 * t - 1) ** 2)
        wd = 0.42 + 0.06 * math.sin(math.pi * t)
        secs.append([(-wd, y, z + 0.03), (wd, y, z + 0.03), (wd, y, z - 0.02), (-wd, y, z - 0.02)])
    loft("bed", rt, secs, "Umbrella", subsurf=1)
    for sy in (-1, 1):
        for sx in (-0.2, 0.2):
            bar(rt, f"rope {sy}{sx}", (sx, sy * 1.45, 1.36), (0, sy * (L / 2 - 0.12), 1.56), 0.006, "Rug_main", 6)
    box("pillow", rt, (-0.2, 0.8, 0.86), (0.2, 1.15, 0.97), "OutdoorFabric_main", 0.04)
    return rt


def gnome():
    rt = root("gnome")
    for sx in (-1, 1):
        sphere(f"boot {sx}", rt, (sx * 0.06, -0.03, 0.04), 0.05, "BlackSteel", (1, 1.4, 0.7), seg=16, rings=8)
    lathe("coat", rt, [(0.0, 0.06), (0.11, 0.06), (0.14, 0.14), (0.11, 0.28), (0.09, 0.32), (0.0, 0.32)], "BookBlue", seg=24, subsurf=1)
    sphere("beard", rt, (0, -0.07, 0.31), 0.08, "PlasticWhite", (1.0, 0.6, 1.2), seg=20, rings=10)
    sphere("face", rt, (0, -0.06, 0.37), 0.05, "GnomeSkin", seg=16, rings=8)
    sphere("nose", rt, (0, -0.11, 0.37), 0.02, "FlowerPink", seg=10, rings=6)
    cone = [(0.0, 0.62), (0.03, 0.55), (0.09, 0.42), (0.1, 0.39), (0.0, 0.39)]
    lathe("hat", rt, cone, "PaintRed", seg=24, subsurf=1)
    for sx in (-1, 1):
        sphere(f"arm {sx}", rt, (sx * 0.12, -0.03, 0.2), 0.035, "BookBlue", (0.8, 1, 1.5), seg=12, rings=8)
    return rt


def flamingo():
    rt = root("flamingo")
    for sx in (-1, 1):
        bar(rt, f"leg {sx}", (sx * 0.03, 0, 0.0), (sx * 0.03, 0.02, 0.42), 0.006, "Flamingo", 8)
    sphere("body", rt, (0, 0.05, 0.5), 0.1, "Flamingo", (0.8, 1.5, 0.9), seg=20, rings=12)
    pts = [(0, -0.05, 0.55), (0, -0.14, 0.7), (0, -0.12, 0.85), (0, -0.04, 0.95)]
    for i in range(3):
        bar(rt, f"neck {i}", pts[i], pts[i + 1], 0.02 - i * 0.003, "Flamingo", 10)
    sphere("head", rt, pts[3], 0.032, "Flamingo", seg=14, rings=8)
    bar(rt, "beak", pts[3], (0, pts[3][1] - 0.07, pts[3][2] - 0.03), 0.014, "BlackSteel", 8)
    sphere("tail", rt, (0, 0.17, 0.52), 0.05, "Flamingo", (0.6, 1.4, 0.4), seg=12, rings=8)
    return rt


def beachball():
    rt = root("beachball")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=14, radius=0.25)
    bmesh.ops.translate(bm, vec=(0, 0, 0.25), verts=bm.verts)
    for f in bm.faces:
        c = f.calc_center_median()
        a = math.atan2(c.y, c.x) % (2 * math.pi)
        f.material_index = int(a / (2 * math.pi) * 6) % 4
    ob = _finish("ball", rt, bm, "BallRed", 0)
    for m in ("BallWhite", "BallBlue", "BallYellow"):
        ob.data.materials.append(mat(m))
    return rt


def fountain():
    rt = root("fountain")
    lathe("basin", rt, [(0.0, 0.0), (1.0, 0.0), (1.1, 0.12), (1.12, 0.55), (1.05, 0.58), (0.98, 0.5), (0.9, 0.3), (0.0, 0.3)], "StoneGrey", seg=64, subsurf=1)
    lathe("column", rt, [(0.0, 0.3), (0.2, 0.3), (0.16, 0.55), (0.12, 1.0), (0.14, 1.1), (0.0, 1.1)], "StoneGrey", seg=32, subsurf=1)
    lathe("bowl", rt, [(0.0, 1.05), (0.5, 1.12), (0.58, 1.22), (0.5, 1.2), (0.0, 1.12)], "StoneGrey", seg=48, subsurf=1)
    lathe("top column", rt, [(0.0, 1.15), (0.09, 1.15), (0.07, 1.5), (0.0, 1.52)], "StoneGrey", seg=24, subsurf=1)
    lathe("top bowl", rt, [(0.0, 1.45), (0.28, 1.5), (0.32, 1.58), (0.26, 1.56), (0.0, 1.5)], "StoneGrey", seg=32, subsurf=1)
    sphere("finial", rt, (0, 0, 1.68), 0.06, "StoneGrey", seg=16, rings=10)
    return rt


def mailbox():
    rt = root("mailbox")
    box("post", rt, (-0.04, -0.04, 0), (0.04, 0.04, 1.0), "BlackSteel", 0.004)
    box("box", rt, (-0.2, -0.16, 1.0), (0.2, 0.16, 1.32), "BlackSteel", 0.03)
    box("flap", rt, (-0.18, -0.175, 1.04), (0.18, -0.16, 1.3), "Stainless", 0.004)
    box("slot", rt, (-0.12, -0.18, 1.22), (0.12, -0.16, 1.25), "GlassDark", 0.002)
    box("number plate", rt, (-0.07, -0.18, 1.08), (0.07, -0.165, 1.16), "PlasticWhite", 0.003)
    return rt


def planterbox():
    rt = root("planterbox")
    W, D, H = 1.2, 0.4, 0.42
    box("box", rt, (-W / 2, -D / 2, 0), (W / 2, D / 2, H), "Cabinet_main", 0.012)
    for i in range(6):
        y = -D / 2 - 0.004
        x0 = -W / 2 + 0.05 + i * (W - 0.1) / 6
        box(f"slat {i}", rt, (x0, y, 0.05), (x0 + (W - 0.1) / 6 - 0.02, y + 0.008, H - 0.05), "Teak", 0.003)
    cyl("soil", rt, (0, 0, H - 0.02), 0.01, 0.01, "Coffee", seg=8, bevel=0.0) if False else box("soil", rt, (-W / 2 + 0.03, -D / 2 + 0.03, H - 0.04), (W / 2 - 0.03, D / 2 - 0.03, H - 0.01), "Soil", 0.003)
    rnd = random.Random(11)
    for i in range(9):
        x = -W / 2 + 0.12 + i * (W - 0.24) / 8
        sphere(f"shrub {i}", rt, (x, rnd.uniform(-0.05, 0.05), H + 0.14), rnd.uniform(0.11, 0.16), "Leaf", (1, 1, 1.2), seg=12, rings=8)
    for i in range(14):
        sphere(f"flower {i}", rt, (rnd.uniform(-W / 2 + 0.1, W / 2 - 0.1), rnd.uniform(-0.1, 0.1), H + rnd.uniform(0.2, 0.34)), 0.03, rnd.choice(["FlowerPink", "FlowerWhite", "FlowerYellow"]), seg=8, rings=6)
    return rt


def flowerbed():
    rt = root("flowerbed")
    W, D = 2.2, 1.1
    for nm, lo, hi in [("edge front", (-W / 2, -D / 2, 0), (W / 2, -D / 2 + 0.1, 0.22)), ("edge back", (-W / 2, D / 2 - 0.1, 0), (W / 2, D / 2, 0.22)),
                       ("edge left", (-W / 2, -D / 2 + 0.1, 0), (-W / 2 + 0.1, D / 2 - 0.1, 0.22)), ("edge right", (W / 2 - 0.1, -D / 2 + 0.1, 0), (W / 2, D / 2 - 0.1, 0.22))]:
        box(nm, rt, lo, hi, "StoneGrey", 0.02)
    box("soil", rt, (-W / 2 + 0.1, -D / 2 + 0.1, 0.02), (W / 2 - 0.1, D / 2 - 0.1, 0.16), "Soil", 0.005)
    rnd = random.Random(21)
    cols = ["FlowerPink", "FlowerWhite", "FlowerYellow", "PaintRed", "BookBlue"]
    for i in range(70):
        x, y = rnd.uniform(-W / 2 + 0.16, W / 2 - 0.16), rnd.uniform(-D / 2 + 0.16, D / 2 - 0.16)
        h = rnd.uniform(0.16, 0.42)
        sphere(f"leaf {i}", rt, (x, y, 0.2), rnd.uniform(0.05, 0.08), "Leaf", (1, 1, 0.8), seg=8, rings=6)
        bar(rt, f"stem {i}", (x, y, 0.16), (x, y, 0.16 + h), 0.004, "Leaf", 5)
        sphere(f"flower {i}", rt, (x, y, 0.18 + h), rnd.uniform(0.028, 0.045), rnd.choice(cols), seg=8, rings=6)
    return rt


def gardenbench():
    rt = root("gardenbench")
    L = 1.6
    for i in range(5):
        y = -0.2 + i * 0.09
        box(f"seat {i}", rt, (-L / 2, y, 0.44), (L / 2, y + 0.075, 0.47), "Teak", 0.004)
    for i in range(3):
        z = 0.6 + i * 0.13
        tilt_box(f"back {i}", rt, (-L / 2, 0.2, z), (L / 2, 0.225, z + 0.1), "Teak", (0, 0.2, 0.5), -12, 'X', 0.004)
    for sx in (-1, 1):
        box(f"leg {sx}", rt, (sx * (L / 2 - 0.08) - 0.025, -0.2, 0), (sx * (L / 2 - 0.08) + 0.025, 0.25, 0.44), "BlackSteel", 0.003)
        box(f"arm {sx}", rt, (sx * (L / 2 - 0.08) - 0.03, -0.2, 0.62), (sx * (L / 2 - 0.08) + 0.03, 0.2, 0.65), "Teak", 0.004)
    return rt


PIECES = [lounger, parasol, bbqcounter, bbq, cooler, telescope, outdoorrug, longdining, outdoorsectional, firepit, lantern, hammock, gnome, flamingo,
          beachball, fountain, mailbox, planterbox, flowerbed, gardenbench]


def build_all():
    fresh()
    out = {}
    for fn in PIECES:
        rt = fn()
        out[rt.name] = stats(rt)
        export(rt, rt.name)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT, "Blender", "furniture_garden.blend"))
    return out


if globals().get("RUN_GARDEN", True):
    RESULT = build_all()
    print(RESULT)
