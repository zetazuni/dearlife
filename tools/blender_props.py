"""Small props for the kitchen, bathroom and garage. Run inside Blender (Blender MCP: exec(open(path).read())).

Reuses the toolkit at the top of blender_kitchen.py and the extra helpers of blender_bathgarage.py
(lathe, bar). Origin: centre of the footprint on the surface the prop stands on. Front faces -Y.
Exports Assets/Art/Models/<id>.fbx and saves Blender/props_home.blend.
"""
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

_root = r"S:\Dearlife by Zetazuni"
_k = open(os.path.join(_root, "tools", "blender_kitchen.py"), encoding="utf-8").read()
exec(_k.split("# ---------------------------------------------------------------- pieces (toolkit ends above this line)")[0])
_b = open(os.path.join(_root, "tools", "blender_bathgarage.py"), encoding="utf-8").read()
# pull in lathe, loft and bar without running the bathroom build
exec(_b[_b.index("def lathe("):_b.index("# ---------------------------------------------------------------- bathroom")])

COLORS.update({
    "Apple": (0.6, 0.06, 0.05), "Orange": (0.9, 0.4, 0.05), "Lemon": (0.95, 0.85, 0.15), "Bread": (0.55, 0.32, 0.14),
    "Terracotta": (0.62, 0.3, 0.2), "Leaf": (0.2, 0.45, 0.15), "Cardboard": (0.6, 0.45, 0.28), "Wax": (0.95, 0.92, 0.85),
    "Flame": (1.0, 0.7, 0.2), "Coffee": (0.1, 0.06, 0.04), "Ceramic": (0.96, 0.96, 0.95), "Rattan": (0.6, 0.45, 0.28),
    "Tire": (0.03, 0.03, 0.03), "Rug_main": (0.9, 0.85, 0.76), "PaintRed": (0.7, 0.05, 0.05), "Headlight": (0.95, 0.97, 1.0),
})


def sphere(name, parent, center, r, material, scale=(1, 1, 1), seg=32, rings=16, rot_z=0.0):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ Matrix.Rotation(rot_z, 4, 'Z') @ Matrix.Diagonal((*scale, 1)), verts=bm.verts)
    bm.normal_update()
    return _finish(name, parent, bm, material, 0)


def fruitbowl():
    rt = root("fruitbowl")
    prof = [(0.0, 0.0), (0.07, 0.0), (0.12, 0.02), (0.17, 0.06), (0.19, 0.095), (0.185, 0.097), (0.165, 0.07), (0.11, 0.035), (0.0, 0.025)]
    lathe("bowl", rt, prof, "Walnut", seg=64, subsurf=1)
    fruit = [("apple", "Apple", (0.0, -0.03, 0.09), 0.042, (1, 1, 0.92)), ("apple", "Apple", (0.07, 0.03, 0.085), 0.042, (1, 1, 0.92)),
             ("apple", "Apple", (-0.07, 0.03, 0.085), 0.04, (1, 1, 0.92)), ("orange", "Orange", (-0.05, -0.07, 0.085), 0.04, (1, 1, 1)),
             ("orange", "Orange", (0.08, -0.06, 0.08), 0.04, (1, 1, 1)), ("lemon", "Lemon", (0.0, 0.05, 0.125), 0.032, (1.35, 1, 1)),
             ("lemon", "Lemon", (-0.02, -0.01, 0.13), 0.032, (1, 1.35, 1)), ("apple", "Apple", (0.01, 0.0, 0.16), 0.04, (1, 1, 0.92))]
    for i, (n, m, c, r, sc) in enumerate(fruit):
        sphere(f"{n} {i+1}", rt, c, r, m, sc, rot_z=i * 0.7)
    return rt


def cuttingboard():
    rt = root("cuttingboard")
    box("board", rt, (-0.21, -0.13, 0.0), (0.21, 0.13, 0.025), "Walnut", 0.008)
    sphere("bread", rt, (-0.05, 0.0, 0.07), 0.09, "Bread", (1.5, 0.75, 0.6), seg=32, rings=16, rot_z=0.2)
    for i in range(3):
        box(f"slash {i+1}", rt, (-0.12 + i * 0.06, -0.03, 0.115), (-0.105 + i * 0.06, 0.03, 0.122), "Bread", 0.003)
    box("knife blade", rt, (0.08, -0.09, 0.025), (0.19, -0.075, 0.03), "Stainless", 0.001)
    box("knife handle", rt, (0.05, -0.095, 0.025), (0.08, -0.07, 0.04), "Walnut", 0.004)
    return rt


def mug():
    rt = root("mug")
    prof = [(0.0, 0.0), (0.038, 0.0), (0.042, 0.006), (0.043, 0.09), (0.04, 0.092), (0.037, 0.09), (0.036, 0.012), (0.0, 0.012)]
    lathe("cup", rt, prof, "Ceramic", seg=48, subsurf=1)
    cyl("coffee", rt, (0, 0, 0.074), 0.036, 0.004, "Coffee", seg=32, bevel=0.0005)
    torus("handle", rt, (0, 0.055, 0.05), 0.024, 0.006, "Ceramic", 32, 12, 'X')
    return rt


def espresso():
    rt = root("espresso")
    box("base", rt, (-0.15, -0.17, 0.0), (0.15, 0.17, 0.03), "BlackSteel", 0.008)
    box("body", rt, (-0.14, 0.02, 0.03), (0.14, 0.17, 0.4), "Stainless", 0.015)
    box("top", rt, (-0.14, -0.14, 0.32), (0.14, 0.17, 0.4), "Stainless", 0.02)
    box("group head", rt, (-0.06, -0.08, 0.26), (0.06, 0.02, 0.32), "BlackSteel", 0.01)
    cyl("portafilter", rt, (0, -0.075, 0.245), 0.032, 0.03, "BlackSteel", seg=32, bevel=0.004)
    box("handle", rt, (-0.015, -0.17, 0.235), (0.015, -0.07, 0.255), "BlackSteel", 0.006)
    box("drip tray", rt, (-0.13, -0.16, 0.03), (0.13, -0.04, 0.045), "Stainless", 0.004)
    cyl("gauge", rt, (0.0, 0.016, 0.2), 0.03, 0.008, "Ceramic", 'Y', seg=32, bevel=0.001)
    cyl("cup", rt, (0, -0.095, 0.075), 0.03, 0.04, "Ceramic", seg=32, bevel=0.004)
    return rt


def utensils():
    rt = root("utensils")
    prof = [(0.0, 0.0), (0.06, 0.0), (0.065, 0.01), (0.065, 0.15), (0.06, 0.152), (0.056, 0.15), (0.056, 0.012), (0.0, 0.012)]
    lathe("crock", rt, prof, "Ceramic", seg=48, subsurf=1)
    for i in range(5):
        a = math.radians(i * 72 + 10)
        lean = 0.14 + 0.03 * (i % 2)
        top = (math.cos(a) * 0.04, math.sin(a) * 0.04, 0.32 - 0.02 * (i % 3))
        bar(rt, f"spoon {i+1}", (math.cos(a) * 0.01, math.sin(a) * 0.01, 0.05), top, 0.007, "Walnut" if i % 2 else "BlackSteel", 12)
        sphere(f"spoon head {i+1}", rt, top, 0.022, "Walnut" if i % 2 else "BlackSteel", (1, 0.6, 0.35), seg=16, rings=8, rot_z=a)
    return rt


def herbs():
    rt = root("herbs")
    prof = [(0.0, 0.0), (0.05, 0.0), (0.065, 0.02), (0.075, 0.13), (0.08, 0.14), (0.07, 0.14), (0.064, 0.125), (0.0, 0.125)]
    lathe("pot", rt, prof, "Terracotta", seg=48, subsurf=1)
    cyl("soil", rt, (0, 0, 0.122), 0.062, 0.006, "Coffee", seg=32, bevel=0.001)
    import random
    rnd = random.Random(7)
    for i in range(34):
        a = rnd.uniform(0, 2 * math.pi)
        rr = rnd.uniform(0.0, 0.05)
        h = rnd.uniform(0.15, 0.3)
        sphere(f"leaf {i+1}", rt, (math.cos(a) * rr, math.sin(a) * rr, h), 0.028, "Leaf", (1, 0.7, 0.25), seg=12, rings=6, rot_z=a)
        bar(rt, f"stem {i+1}", (math.cos(a) * rr * 0.4, math.sin(a) * rr * 0.4, 0.125), (math.cos(a) * rr, math.sin(a) * rr, h), 0.002, "Leaf", 6)
    return rt


def towelstack():
    rt = root("towelstack")
    z = 0.0
    for i, (m, w, d, h) in enumerate([("Fabric_main", 0.36, 0.24, 0.05), ("Rug_main", 0.34, 0.23, 0.05), ("Fabric_main", 0.36, 0.24, 0.05)]):
        off = (i - 1) * 0.008
        box(f"towel {i+1}", rt, (-w / 2 + off, -d / 2, z), (w / 2 + off, d / 2, z + h), m, 0.02)
        z += h
    box("band", rt, (-0.02, -0.122, 0.0), (0.02, 0.122, z), "Rattan", 0.004)
    return rt


def candles():
    rt = root("candles")
    box("tray", rt, (-0.2, -0.09, 0.0), (0.2, 0.09, 0.015), "Stainless", 0.006)
    for i, (x, y, h, r) in enumerate([(-0.11, 0.0, 0.16, 0.038), (0.0, 0.02, 0.1, 0.03), (0.1, -0.015, 0.22, 0.04)]):
        cyl(f"candle {i+1}", rt, (x, y, 0.015 + h / 2), r, h, "Wax", seg=32, bevel=0.003)
        bar(rt, f"wick {i+1}", (x, y, 0.015 + h), (x, y, 0.015 + h + 0.014), 0.0015, "BlackSteel", 6)
        sphere(f"flame {i+1}", rt, (x, y, 0.015 + h + 0.03), 0.009, "Flame", (0.8, 0.8, 2.2), seg=12, rings=8)
    return rt


def cardboardboxes():
    rt = root("cardboardboxes")
    for i, (lo, hi) in enumerate([((-0.32, -0.2, 0.0), (0.12, 0.2, 0.32)), ((0.15, -0.18, 0.0), (0.5, 0.18, 0.26)), ((-0.25, -0.16, 0.32), (0.1, 0.16, 0.56))]):
        box(f"box {i+1}", rt, lo, hi, "Cardboard", 0.006)
        box(f"tape {i+1}", rt, ((lo[0] + hi[0]) / 2 - 0.025, lo[1] - 0.001, lo[2] + 0.02), ((lo[0] + hi[0]) / 2 + 0.025, hi[1] + 0.001, hi[2] + 0.001), "Wax", 0.002)
    return rt


def paintcans():
    rt = root("paintcans")
    for i, (x, y, z) in enumerate([(-0.11, 0.0, 0.0), (0.11, 0.0, 0.0), (0.0, 0.0, 0.2)]):
        cyl(f"can {i+1}", rt, (x, y, z + 0.095), 0.1, 0.19, "Stainless", seg=48, bevel=0.004)
        cyl(f"label {i+1}", rt, (x, y, z + 0.095), 0.1015, 0.09, ("PaintRed", "Rattan", "Leaf")[i], seg=48, bevel=0.0005)
        torus(f"lid ring {i+1}", rt, (x, y, z + 0.19), 0.09, 0.006, "Stainless", 48, 8)
        cyl(f"handle base {i+1}", rt, (x, y, z + 0.215), 0.07, 0.004, "BlackSteel", seg=32, bevel=0.0005)
    return rt


def sparetyres():
    rt = root("sparetyres")
    for i in range(3):
        torus(f"tyre {i+1}", rt, (0, 0, 0.09 + i * 0.17), 0.21, 0.09, "Tire", 48, 16)
        cyl(f"wall {i+1}", rt, (0, 0, 0.09 + i * 0.17), 0.13, 0.15, "Tire", seg=48, bevel=0.01)
    return rt


def planter():
    """Tall ceramic planter for the money trees. The tree stands on the soil, 0.38 m up."""
    rt = root("planter")
    prof = [(0.0, 0.0), (0.15, 0.0), (0.2, 0.03), (0.25, 0.2), (0.27, 0.38), (0.275, 0.42), (0.262, 0.425), (0.245, 0.395), (0.235, 0.36), (0.0, 0.36)]
    lathe("pot", rt, prof, "Planter_main", seg=64, subsurf=1)
    cyl("soil", rt, (0, 0, 0.375), 0.235, 0.012, "Coffee", seg=48, bevel=0.002)
    cyl("stones", rt, (0, 0, 0.383), 0.2, 0.006, "Cardboard", seg=32, bevel=0.001)
    return rt


PIECES = [planter, fruitbowl, cuttingboard, mug, espresso, utensils, herbs, towelstack, candles, cardboardboxes, paintcans, sparetyres]


def build_all():
    fresh()
    out = {}
    for fn in PIECES:
        rt = fn()
        out[rt.name] = stats(rt)
        export(rt, rt.name)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT, "Blender", "props_home.blend"))
    return out


if globals().get("RUN_PROPS", True):
    RESULT = build_all()
    print(RESULT)
