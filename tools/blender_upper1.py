"""Upper floor, part 1: Teacher's Room and Office & Library furniture. Run inside Blender (Blender MCP).

Reuses the toolkit of blender_kitchen.py, and lathe / bar from blender_bathgarage.py plus sphere from blender_props.py.
Origin: centre of the footprint on the floor (wall pieces: bottom centre, back at +Y). Front faces -Y.
Exports Assets/Art/Models/<id>.fbx and saves Blender/furniture_upper1.blend.
"""
import bpy, bmesh, math, os, random
from mathutils import Matrix, Vector

_root = r"S:\Dearlife by Zetazuni"
_TK = "# ---------------------------------------------------------------- pieces (toolkit ends above this line)"
_k = open(os.path.join(_root, "tools", "blender_kitchen.py"), encoding="utf-8").read()
exec(_k.split(_TK)[0])
_b = open(os.path.join(_root, "tools", "blender_bathgarage.py"), encoding="utf-8").read()
exec(_b[_b.index("def lathe("):_b.index("# ---------------------------------------------------------------- bathroom")])
_p = open(os.path.join(_root, "tools", "blender_props.py"), encoding="utf-8").read()
exec(_p[_p.index("def sphere("):_p.index("def fruitbowl(")])

COLORS.update({
    "Bedding_main": (0.85, 0.65, 0.65), "Bedding_dark": (0.12, 0.12, 0.14), "Leather": (0.1, 0.07, 0.06), "Screen": (0.3, 0.55, 0.9),
    "Chalk": (0.06, 0.09, 0.08), "Whiteboard": (0.95, 0.95, 0.96), "Rubber": (0.05, 0.05, 0.055), "Plastic": (0.1, 0.1, 0.11),
    "PlasticWhite": (0.92, 0.92, 0.93), "MapSea": (0.25, 0.5, 0.75), "MapLand": (0.35, 0.6, 0.3), "Globe": (0.2, 0.42, 0.75),
    "BookRed": (0.6, 0.12, 0.1), "BookBlue": (0.15, 0.25, 0.5), "BookGreen": (0.15, 0.4, 0.22), "BookCream": (0.85, 0.8, 0.65),
    "BookDark": (0.15, 0.12, 0.1), "BlueWater": (0.3, 0.6, 0.9), "Beanbag_main": (0.2, 0.3, 0.32), "Mat_main": (0.25, 0.55, 0.55),
    "Wax": (0.95, 0.92, 0.85), "Leaf": (0.2, 0.45, 0.15), "Orange": (0.9, 0.4, 0.05), "Coffee": (0.1, 0.06, 0.04), "Rug_main": (0.9, 0.85, 0.76),
})


# ---------------------------------------------------------------- Teacher's Room

def bed(name, duvet):
    rt = root(name)
    W, L = 1.7, 2.15
    box("plinth", rt, (-W / 2 + 0.05, -L / 2 + 0.05, 0), (W / 2 - 0.05, L / 2 - 0.05, 0.12), "BlackSteel", 0.004)
    box("base", rt, (-W / 2, -L / 2, 0.12), (W / 2, L / 2, 0.32), "Walnut", 0.012)
    box("headboard frame", rt, (-W / 2 - 0.06, L / 2 - 0.02, 0.12), (W / 2 + 0.06, L / 2 + 0.08, 1.15), "Walnut", 0.015)
    box("headboard pad", rt, (-W / 2 + 0.04, L / 2 - 0.09, 0.42), (W / 2 - 0.04, L / 2 - 0.02, 1.08), "Fabric_main", 0.035)
    box("mattress", rt, (-W / 2 + 0.03, -L / 2 + 0.03, 0.32), (W / 2 - 0.03, L / 2 - 0.1, 0.56), "Fabric_main", 0.05)
    box("duvet", rt, (-W / 2 - 0.02, -L / 2 + 0.02, 0.42), (W / 2 + 0.02, 0.28, 0.62), duvet, 0.06)
    box("duvet fold", rt, (-W / 2 - 0.02, 0.24, 0.5), (W / 2 + 0.02, 0.52, 0.66), duvet, 0.06)
    box("throw", rt, (-0.7, -0.75, 0.6), (0.7, -0.5, 0.66), "Fabric_main", 0.03)
    for i, (x, y) in enumerate([(-0.4, 0.78), (0.4, 0.78)]):
        box(f"pillow {i+1}", rt, (x - 0.32, y - 0.2, 0.56), (x + 0.32, y + 0.2, 0.7), "Fabric_main", 0.07)
    for i, x in enumerate((-0.25, 0.25)):
        box(f"back pillow {i+1}", rt, (x - 0.24, 0.9, 0.62), (x + 0.24, 0.98, 0.9), duvet, 0.05)
    return rt


def platformbed(): return bed("platformbed", "Bedding_main")
def platformbed_e(): return bed("platformbed_e", "Bedding_dark")


def nightstand():
    rt = root("nightstand")
    W, D = 0.46, 0.4
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl(f"leg {sx}{sy}", rt, (sx * (W / 2 - 0.03), sy * (D / 2 - 0.03), 0.09), 0.013, 0.18, "BlackSteel", r2=0.01, seg=16, bevel=0.002)
    box("body", rt, (-W / 2, -D / 2, 0.18), (W / 2, D / 2, 0.52), "Walnut", 0.01)
    box("drawer", rt, (-W / 2 + 0.02, -D / 2 - 0.015, 0.22), (W / 2 - 0.02, -D / 2 + 0.005, 0.48), "Walnut", 0.006)
    cyl("pull", rt, (0, -D / 2 - 0.035, 0.45), 0.006, 0.16, "Brass", 'X', seg=16, bevel=0.001)
    return rt


def bedlamp():
    rt = root("bedlamp")
    cyl("base", rt, (0, 0, 0.012), 0.07, 0.024, "Ceramic", seg=48, bevel=0.005)
    cyl("stem", rt, (0, 0, 0.13), 0.014, 0.22, "Brass", seg=20, bevel=0.002)
    lathe("shade", rt, [(0.09, 0.24), (0.14, 0.24), (0.15, 0.245), (0.11, 0.4), (0.105, 0.4), (0.09, 0.4), (0.0, 0.4), (0.0, 0.24)], "Bulb", seg=48, subsurf=1)
    return rt


def wardrobe():
    rt = root("wardrobe")
    W, D, H = 1.6, 0.6, 2.3
    box("plinth", rt, (-W / 2 + 0.03, -D / 2 + 0.05, 0), (W / 2 - 0.03, D / 2, 0.1), "BlackSteel", 0.002)
    box("carcass", rt, (-W / 2, -D / 2 + 0.02, 0.1), (W / 2, D / 2, H), "Cabinet_main", 0.006)
    for i in range(3):
        a = -W / 2 + 0.01 + i * (W - 0.02) / 3
        box(f"door {i+1}", rt, (a + 0.002, -D / 2 - 0.02, 0.11), (a + (W - 0.02) / 3 - 0.002, -D / 2 + 0.02, H - 0.01), "Walnut", 0.006)
        cyl(f"handle {i+1}", rt, (a + (W - 0.02) / 3 - 0.06 if i != 2 else a + 0.06, -D / 2 - 0.045, 1.05), 0.008, 0.7, "Brass", seg=16, bevel=0.001)
    return rt


def _books(rt, x0, x1, y0, y1, z, hmax, rnd, prefix):
    mats = ["BookRed", "BookBlue", "BookGreen", "BookCream", "BookDark"]
    x = x0 + 0.02
    i = 0
    while x < x1 - 0.06:
        w = rnd.uniform(0.02, 0.05)
        h = rnd.uniform(hmax * 0.65, hmax)
        lean = rnd.random() < 0.06
        box(f"{prefix} book {i}", rt, (x, y0 + 0.02, z), (x + w, y1 - 0.03, z + h), rnd.choice(mats), 0.002)
        x += w + 0.003
        i += 1
        if lean:
            x += 0.05
    return rt


def bookcase():
    rt = root("bookcase")
    W, D, H = 1.2, 0.4, 2.0
    rnd = random.Random(3)
    box("left", rt, (-W / 2, -D / 2, 0), (-W / 2 + 0.03, D / 2, H), "Walnut", 0.004)
    box("right", rt, (W / 2 - 0.03, -D / 2, 0), (W / 2, D / 2, H), "Walnut", 0.004)
    box("back", rt, (-W / 2, D / 2 - 0.012, 0), (W / 2, D / 2, H), "Walnut", 0.002)
    box("plinth", rt, (-W / 2, -D / 2 + 0.03, 0), (W / 2, D / 2, 0.08), "BlackSteel", 0.002)
    for i in range(6):
        z = 0.08 + i * 0.37
        box(f"shelf {i+1}", rt, (-W / 2 + 0.03, -D / 2, z), (W / 2 - 0.03, D / 2 - 0.012, z + 0.025), "Walnut", 0.003)
        if i < 5:
            _books(rt, -W / 2 + 0.03, W / 2 - 0.03 - (0.35 if i == 2 else 0), -D / 2 + 0.03, D / 2, z + 0.025, 0.3, rnd, f"s{i}")
    box("top", rt, (-W / 2 - 0.01, -D / 2 - 0.01, H), (W / 2 + 0.01, D / 2, H + 0.03), "Walnut", 0.006)
    return rt


def teacherdesk():
    rt = root("teacherdesk")
    W, D, H = 1.4, 0.7, 0.75
    box("top", rt, (-W / 2, -D / 2, H - 0.035), (W / 2, D / 2, H), "Walnut", 0.008)
    for sx in (-1, 1):
        box(f"leg {sx}", rt, (sx * (W / 2 - 0.05) - 0.02, -D / 2 + 0.05, 0), (sx * (W / 2 - 0.05) + 0.02, D / 2 - 0.05, H - 0.035), "BlackSteel", 0.003)
    box("rail", rt, (-W / 2 + 0.05, D / 2 - 0.07, H - 0.16), (W / 2 - 0.05, D / 2 - 0.05, H - 0.035), "BlackSteel", 0.003)
    box("drawer unit", rt, (0.2, -D / 2 + 0.05, 0.4), (W / 2 - 0.08, D / 2 - 0.05, H - 0.035), "Walnut", 0.008)
    box("drawer front", rt, (0.21, -D / 2 + 0.03, 0.45), (W / 2 - 0.09, -D / 2 + 0.05, H - 0.06), "Walnut", 0.005)
    cyl("drawer pull", rt, (0.5, -D / 2 + 0.01, H - 0.11), 0.006, 0.14, "Brass", 'X', seg=16, bevel=0.001)
    # a stack of papers and a pencil pot
    box("papers", rt, (-0.5, -0.1, H), (-0.2, 0.12, H + 0.03), "BookCream", 0.002)
    cyl("pot", rt, (-0.55, 0.22, H + 0.05), 0.04, 0.1, "Ceramic", seg=32, bevel=0.003)
    return rt


def officechair():
    rt = root("officechair")
    for i in range(5):
        an = math.radians(i * 72 + 90)
        end = (math.cos(an) * 0.29, math.sin(an) * 0.29, 0.06)
        bar(rt, f"foot {i+1}", (0, 0, 0.1), end, 0.017, "BlackSteel", 12)
        cyl(f"caster {i+1}", rt, (end[0], end[1], 0.03), 0.03, 0.05, "Rubber", 'X', seg=20, bevel=0.005)
    cyl("column", rt, (0, 0, 0.27), 0.03, 0.34, "Stainless", seg=24, bevel=0.003)
    box("seat", rt, (-0.245, -0.245, 0.44), (0.245, 0.235, 0.5), "Leather", 0.035)
    box("seat base", rt, (-0.2, -0.2, 0.42), (0.2, 0.2, 0.44), "BlackSteel", 0.004)
    bar(rt, "back stem", (0, 0.22, 0.44), (0, 0.25, 0.65), 0.014, "BlackSteel", 12)
    box("back", rt, (-0.235, 0.22, 0.6), (0.235, 0.29, 0.98), "Leather", 0.035)
    for sx in (-1, 1):
        bar(rt, f"arm post {sx}", (sx * 0.26, 0.05, 0.46), (sx * 0.26, 0.05, 0.66), 0.011, "BlackSteel", 12)
        box(f"arm rest {sx}", rt, (sx * 0.26 - 0.03, -0.13, 0.66), (sx * 0.26 + 0.03, 0.15, 0.685), "Leather", 0.01)
    return rt


def globe():
    rt = root("globe")
    cyl("foot", rt, (0, 0, 0.012), 0.11, 0.024, "BlackSteel", seg=48, bevel=0.004)
    cyl("stem", rt, (0, 0, 0.11), 0.01, 0.17, "Brass", seg=16, bevel=0.001)
    sphere("ball", rt, (0, 0, 0.32), 0.15, "Globe", seg=48, rings=24)
    rnd = random.Random(5)
    for i in range(9):
        th = rnd.uniform(0.6, 2.5)
        ph = rnd.uniform(0, 6.28)
        r = 0.15
        c = (r * math.sin(th) * math.cos(ph) * 0.99, r * math.sin(th) * math.sin(ph) * 0.99, 0.32 + r * math.cos(th) * 0.99)
        blob = sphere(f"land {i+1}", rt, c, rnd.uniform(0.04, 0.07), "MapLand", (1, 1, 0.35), seg=16, rings=8)
        blob.rotation_euler = (0, 0, 0)
        # tilt the flat blob to lie on the surface
        n = Vector(c) - Vector((0, 0, 0.32))
        blob.data.transform(Matrix.Translation(-Vector(c)))
        blob.data.transform(n.normalized().to_track_quat('Z', 'Y').to_matrix().to_4x4())
        blob.data.transform(Matrix.Translation(Vector(c)))
    torus("meridian", rt, (0, 0, 0.32), 0.165, 0.005, "Brass", 64, 8, 'X')
    return rt


def chalkboard():
    rt = root("chalkboard")
    W, H = 1.4, 0.9
    box("frame", rt, (-W / 2, 0, 0), (W / 2, 0.04, H), "Walnut", 0.012)
    box("board", rt, (-W / 2 + 0.03, -0.006, 0.03), (W / 2 - 0.03, 0.03, H - 0.03), "Chalk", 0.002)
    box("tray", rt, (-W / 2 + 0.03, -0.06, 0.0), (W / 2 - 0.03, 0.0, 0.025), "Walnut", 0.004)
    for i, (x0, z0, x1, z1) in enumerate([(-0.5, 0.6, 0.1, 0.612), (-0.5, 0.5, -0.05, 0.512), (-0.5, 0.4, 0.3, 0.412), (0.2, 0.62, 0.55, 0.632)]):
        box(f"chalk line {i+1}", rt, (x0, -0.009, z0), (x1, -0.006, z1), "Wax", 0.0005)
    box("chalk", rt, (0.3, -0.05, 0.025), (0.36, -0.03, 0.036), "Wax", 0.003)
    return rt


def worldmap():
    rt = root("worldmap")
    W, H = 1.5, 1.0
    box("frame", rt, (-W / 2, 0, 0), (W / 2, 0.03, H), "BlackSteel", 0.004)
    box("sea", rt, (-W / 2 + 0.03, -0.005, 0.03), (W / 2 - 0.03, 0.02, H - 0.03), "MapSea", 0.001)
    lands = [(-0.42, 0.65, 0.12, 0.2), (-0.35, 0.35, 0.09, 0.22), (0.0, 0.62, 0.08, 0.13), (0.0, 0.4, 0.09, 0.18), (0.3, 0.62, 0.22, 0.14),
             (0.42, 0.35, 0.07, 0.07), (0.15, 0.7, 0.08, 0.08), (-0.1, 0.85, 0.08, 0.04)]
    for i, (x, z, w, h) in enumerate(lands):
        sphere(f"land {i+1}", rt, (x, -0.006, z), 1.0, "MapLand", (w, 0.004, h), seg=16, rings=8)
    return rt


# ---------------------------------------------------------------- Office & Library

def officedesk():
    rt = root("officedesk")
    W, D, H = 1.8, 0.8, 0.75
    box("top", rt, (-W / 2, -D / 2, H - 0.04), (W / 2, D / 2, H), "Walnut", 0.01)
    for sx in (-1, 1):
        box(f"side {sx}", rt, (sx * (W / 2 - 0.07) - 0.02, -D / 2 + 0.06, 0), (sx * (W / 2 - 0.07) + 0.02, D / 2 - 0.06, H - 0.04), "BlackSteel", 0.003)
    box("modesty panel", rt, (-W / 2 + 0.1, D / 2 - 0.06, 0.25), (W / 2 - 0.1, D / 2 - 0.04, H - 0.04), "Walnut", 0.004)
    # monitor, keyboard, mouse and a desk lamp
    box("monitor stand", rt, (-0.05, 0.2, H), (0.05, 0.3, H + 0.02), "BlackSteel", 0.005)
    bar(rt, "monitor neck", (0, 0.25, H), (0, 0.25, H + 0.22), 0.012, "BlackSteel", 12)
    box("monitor", rt, (-0.3, 0.22, H + 0.2), (0.3, 0.255, H + 0.5), "BlackSteel", 0.008)
    box("screen", rt, (-0.29, 0.215, H + 0.21), (0.29, 0.222, H + 0.49), "Screen", 0.001)
    box("keyboard", rt, (-0.22, -0.2, H), (0.16, -0.05, H + 0.018), "BlackSteel", 0.006)
    box("mouse", rt, (0.26, -0.15, H), (0.3, -0.08, H + 0.03), "BlackSteel", 0.012)
    cyl("lamp base", rt, (-0.7, 0.28, H + 0.01), 0.07, 0.02, "BlackSteel", seg=32, bevel=0.003)
    bar(rt, "lamp arm", (-0.7, 0.28, H + 0.02), (-0.6, 0.2, H + 0.4), 0.008, "BlackSteel", 10)
    box("lamp head", rt, (-0.66, 0.13, H + 0.38), (-0.5, 0.25, H + 0.42), "BlackSteel", 0.01)
    return rt


def filecabinet():
    rt = root("filecabinet")
    W, D, H = 0.42, 0.6, 1.3
    box("body", rt, (-W / 2, -D / 2 + 0.02, 0.03), (W / 2, D / 2, H), "Stainless", 0.008)
    for i in range(4):
        z = 0.06 + i * 0.3
        box(f"drawer {i+1}", rt, (-W / 2 + 0.015, -D / 2 - 0.01, z), (W / 2 - 0.015, -D / 2 + 0.03, z + 0.28), "Cabinet_main", 0.006)
        box(f"handle {i+1}", rt, (-0.07, -D / 2 - 0.03, z + 0.2), (0.07, -D / 2 - 0.01, z + 0.22), "Stainless", 0.004)
        box(f"label {i+1}", rt, (-0.05, -D / 2 - 0.012, z + 0.1), (0.05, -D / 2 - 0.006, z + 0.16), "Whiteboard", 0.001)
    return rt


def uplight():
    rt = root("uplight")
    cyl("base", rt, (0, 0, 0.012), 0.16, 0.024, "BlackSteel", seg=48, bevel=0.005)
    cyl("pole", rt, (0, 0, 0.8), 0.011, 1.6, "Brass", seg=20, bevel=0.002)
    lathe("cup", rt, [(0.0, 1.5), (0.07, 1.52), (0.13, 1.62), (0.135, 1.63), (0.128, 1.63), (0.06, 1.545), (0.0, 1.535)], "BlackSteel", seg=48, subsurf=1)
    cyl("glow", rt, (0, 0, 1.62), 0.11, 0.012, "Bulb", seg=32, bevel=0.001)
    return rt


def whiteboard():
    rt = root("whiteboard")
    W, H = 1.6, 1.0
    box("frame", rt, (-W / 2, 0, 0), (W / 2, 0.035, H), "Stainless", 0.008)
    box("board", rt, (-W / 2 + 0.02, -0.005, 0.02), (W / 2 - 0.02, 0.03, H - 0.02), "Whiteboard", 0.001)
    box("tray", rt, (-0.4, -0.06, 0.0), (0.4, 0.0, 0.02), "Stainless", 0.004)
    for i, (x0, z0, x1, z1, m) in enumerate([(-0.6, 0.7, 0.1, 0.708, "BookBlue"), (-0.6, 0.6, -0.1, 0.608, "BookBlue"), (0.2, 0.75, 0.6, 0.758, "BookRed"), (-0.6, 0.45, 0.4, 0.458, "BookGreen")]):
        box(f"scribble {i+1}", rt, (x0, -0.008, z0), (x1, -0.005, z1), m, 0.0005)
    for i, m in enumerate(("BookBlue", "BookRed", "BookGreen")):
        cyl(f"marker {i+1}", rt, (-0.2 + i * 0.09, -0.04, 0.03), 0.009, 0.11, m, 'X', seg=12, bevel=0.001)
    return rt


PIECES = [platformbed, platformbed_e, nightstand, bedlamp, wardrobe, bookcase, teacherdesk, officechair, globe, chalkboard, worldmap,
          officedesk, filecabinet, uplight, whiteboard]


def build_all():
    fresh()
    out = {}
    for fn in PIECES:
        rt = fn()
        out[rt.name] = stats(rt)
        export(rt, rt.name)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT, "Blender", "furniture_upper1.blend"))
    return out


if globals().get("RUN_UPPER1", True):
    RESULT = build_all()
    print(RESULT)
