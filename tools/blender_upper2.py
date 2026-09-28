"""Upper floor, part 2: Engineer's Room and Gym. Run inside Blender (Blender MCP).

Same toolkit as blender_upper1.py. Origin: centre of the footprint on the floor (wall pieces: bottom centre,
back at +Y). Front faces -Y. Exports Assets/Art/Models/<id>.fbx, saves Blender/furniture_upper2.blend.
"""
import bpy, bmesh, math, os, random
from mathutils import Matrix, Vector

_root = r"S:\Dearlife by Zetazuni"
_u = open(os.path.join(_root, "tools", "blender_upper1.py"), encoding="utf-8").read()
exec(_u[:_u.index("# ---------------------------------------------------------------- Teacher's Room")])

COLORS.update({"Mat_main": (0.25, 0.55, 0.55), "PaintRed": (0.7, 0.05, 0.05)})


# ---------------------------------------------------------------- Engineer's Room

def ebench():
    rt = root("ebench")
    W, D, H = 1.8, 0.75, 0.9
    box("top", rt, (-W / 2, -D / 2, H - 0.04), (W / 2, D / 2, H), "Walnut", 0.008)
    box("mat", rt, (-0.55, -0.3, H), (0.55, 0.2, H + 0.003), "Leaf", 0.001)
    for sx in (-1, 1):
        box(f"leg {sx}", rt, (sx * (W / 2 - 0.05) - 0.025, -D / 2 + 0.05, 0), (sx * (W / 2 - 0.05) + 0.025, D / 2 - 0.05, H - 0.04), "BlackSteel", 0.003)
    box("rail", rt, (-W / 2 + 0.05, D / 2 - 0.08, 0.25), (W / 2 - 0.05, D / 2 - 0.05, 0.3), "BlackSteel", 0.003)
    # drawer block on the right
    box("drawers", rt, (0.45, -D / 2 + 0.05, 0.25), (W / 2 - 0.08, D / 2 - 0.05, H - 0.04), "Cabinet_main", 0.006)
    for i in range(3):
        box(f"drawer {i+1}", rt, (0.46, -D / 2 + 0.03, 0.27 + i * 0.2), (W / 2 - 0.09, -D / 2 + 0.05, 0.27 + i * 0.2 + 0.18), "Cabinet_main", 0.004)
    # shelf riser with two monitors, oscilloscope, soldering iron, parts trays
    box("shelf", rt, (-W / 2, D / 2 - 0.22, H + 0.32), (W / 2, D / 2, H + 0.345), "Walnut", 0.006)
    for sx in (-1, 1):
        box(f"shelf post {sx}", rt, (sx * (W / 2 - 0.03) - 0.015, D / 2 - 0.2, H), (sx * (W / 2 - 0.03) + 0.015, D / 2 - 0.02, H + 0.32), "BlackSteel", 0.002)
    for i, x in enumerate((-0.55, 0.0)):
        box(f"monitor {i+1}", rt, (x - 0.26, D / 2 - 0.3, H + 0.14), (x + 0.26, D / 2 - 0.27, H + 0.44), "BlackSteel", 0.006)
        box(f"screen {i+1}", rt, (x - 0.25, D / 2 - 0.305, H + 0.15), (x + 0.25, D / 2 - 0.298, H + 0.43), "Screen", 0.001)
        bar(rt, f"neck {i+1}", (x, D / 2 - 0.25, H), (x, D / 2 - 0.27, H + 0.16), 0.01, "BlackSteel", 10)
    box("scope body", rt, (0.4, D / 2 - 0.42, H), (0.72, D / 2 - 0.08, H + 0.17), "Plastic", 0.01)
    box("scope screen", rt, (0.44, D / 2 - 0.425, H + 0.06), (0.62, D / 2 - 0.418, H + 0.155), "Screen", 0.001)
    for i in range(4):
        cyl(f"knob {i+1}", rt, (0.66 - 0.0, D / 2 - 0.425, H + 0.03 + i * 0.03), 0.008, 0.01, "Stainless", 'Y', seg=12, bevel=0.001)
    # soldering station and a circuit board on the mat
    box("solder station", rt, (-0.05, -0.28, H + 0.003), (0.12, -0.12, H + 0.06), "Plastic", 0.008)
    bar(rt, "iron", (0.0, -0.12, H + 0.06), (-0.3, -0.2, H + 0.02), 0.007, "Stainless", 10)
    box("pcb", rt, (-0.45, -0.22, H + 0.003), (-0.2, -0.05, H + 0.008), "Leaf", 0.001)
    for i in range(6):
        box(f"chip {i+1}", rt, (-0.43 + i * 0.038, -0.17 + (i % 2) * 0.05, H + 0.008), (-0.4 + i * 0.038, -0.14 + (i % 2) * 0.05, H + 0.02), "BlackSteel", 0.001)
    for i in range(4):
        box(f"tray {i+1}", rt, (0.78 - 0.1 * 0 + (i % 2) * 0.09, -0.3 + (i // 2) * 0.1, H), (0.85 + (i % 2) * 0.09, -0.22 + (i // 2) * 0.1, H + 0.03), "Stainless", 0.003)
    # task lamp on an arm
    bar(rt, "lamp arm", (-0.8, -0.2, H), (-0.6, -0.05, H + 0.4), 0.008, "BlackSteel", 10)
    box("lamp head", rt, (-0.68, -0.12, H + 0.38), (-0.52, 0.0, H + 0.42), "BlackSteel", 0.01)
    return rt


def robotarm():
    rt = root("robotarm")
    cyl("plinth", rt, (0, 0, 0.35), 0.22, 0.7, "Stainless", seg=48, bevel=0.01)
    cyl("plinth top", rt, (0, 0, 0.705), 0.235, 0.02, "BlackSteel", seg=48, bevel=0.004)
    cyl("base", rt, (0, 0, 0.78), 0.13, 0.13, "Plastic", seg=48, bevel=0.01)
    sphere("shoulder", rt, (0, 0, 0.93), 0.09, "Orange", seg=32, rings=16)
    p1 = Vector((0, 0.0, 0.93)); p2 = Vector((0, -0.25, 1.4)); p3 = Vector((0, -0.65, 1.42)); p4 = Vector((0, -0.85, 1.25))
    bar(rt, "upper arm", p1, p2, 0.055, "Orange", 24)
    sphere("elbow", rt, p2, 0.07, "Plastic", seg=32, rings=16)
    bar(rt, "forearm", p2, p3, 0.045, "Orange", 24)
    sphere("wrist", rt, p3, 0.055, "Plastic", seg=24, rings=12)
    bar(rt, "hand", p3, p4, 0.03, "Orange", 20)
    for sx in (-1, 1):
        bar(rt, f"claw {sx}", p4 + Vector((sx * 0.03, 0, 0)), p4 + Vector((sx * 0.025, -0.06, -0.09)), 0.012, "Stainless", 10)
    box("cable", rt, (0.0, -0.02, 0.85), (0.012, 0.0, 0.7), "BlackSteel", 0.001)
    return rt


def printer3d():
    rt = root("printer3d")
    # little table
    box("table top", rt, (-0.33, -0.33, 0.7), (0.33, 0.33, 0.74), "Walnut", 0.008)
    for sx in (-1, 1):
        for sy in (-1, 1):
            cyl(f"leg {sx}{sy}", rt, (sx * 0.29, sy * 0.29, 0.35), 0.015, 0.7, "BlackSteel", r2=0.011, seg=16, bevel=0.002)
    # enclosure frame, glass sides, top
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"post {sx}{sy}", rt, (sx * 0.24 - 0.012, sy * 0.24 - 0.012, 0.74), (sx * 0.24 + 0.012, sy * 0.24 + 0.012, 1.2), "BlackSteel", 0.002)
    box("top frame", rt, (-0.26, -0.26, 1.2), (0.26, 0.26, 1.225), "BlackSteel", 0.004)
    box("base frame", rt, (-0.26, -0.26, 0.74), (0.26, 0.26, 0.775), "BlackSteel", 0.004)
    for x0, y0, x1, y1 in [(-0.24, -0.245, 0.24, -0.238), (-0.245, -0.24, -0.238, 0.24), (0.238, -0.24, 0.245, 0.24)]:
        box("glass side", rt, (x0, y0, 0.775), (x1, y1, 1.2), "ClearGlass", 0.0005)
    # print bed, gantry, hot end, a half printed part
    box("bed plate", rt, (-0.19, -0.19, 0.83), (0.19, 0.19, 0.845), "Stainless", 0.003)
    box("gantry", rt, (-0.235, -0.02, 1.0), (0.235, 0.0, 1.03), "BlackSteel", 0.003)
    box("hot end", rt, (-0.03, -0.05, 0.93), (0.03, 0.02, 1.0), "Plastic", 0.005)
    bar(rt, "nozzle", (0, -0.015, 0.93), (0, -0.015, 0.905), 0.006, "Brass", 8)
    lathe("print", rt, [(0.0, 0.845), (0.07, 0.845), (0.06, 0.9), (0.04, 0.95), (0.0, 0.96)], "Orange", seg=32, center=(0, 0.02, 0))
    # filament spool on top
    torus("spool ring", rt, (0, 0.05, 1.31), 0.09, 0.012, "Plastic", 32, 8, 'X')
    cyl("filament", rt, (0, 0.05, 1.31), 0.085, 0.07, "Orange", 'X', seg=32, bevel=0.005)
    box("display", rt, (0.1, -0.265, 0.79), (0.21, -0.255, 0.86), "Screen", 0.001)
    return rt


def beanbag():
    rt = root("beanbag")
    prof = [(0.0, 0.0), (0.3, 0.0), (0.42, 0.1), (0.48, 0.26), (0.42, 0.4), (0.3, 0.5), (0.22, 0.52), (0.14, 0.44), (0.0, 0.42)]
    lathe("sack", rt, prof, "Beanbag_main", seg=48, scale=(1.05, 0.95, 1), subsurf=2)
    for i in range(4):
        an = math.radians(i * 90 + 30)
        sphere(f"seam bump {i+1}", rt, (math.cos(an) * 0.44, math.sin(an) * 0.4, 0.28), 0.03, "Beanbag_main", (0.5, 0.5, 2.2), seg=12, rings=8)
    return rt


# ---------------------------------------------------------------- Gym

def treadmill():
    rt = root("treadmill")
    W, L = 0.85, 1.8                    # user steps on at -Y, console at +Y
    box("deck base", rt, (-W / 2, -L / 2, 0.12), (W / 2, L / 2 - 0.1, 0.24), "BlackSteel", 0.01)
    box("belt", rt, (-W / 2 + 0.08, -L / 2 + 0.05, 0.24), (W / 2 - 0.08, L / 2 - 0.15, 0.255), "Rubber", 0.004)
    for sx in (-1, 1):
        box(f"side rail {sx}", rt, (sx * (W / 2 - 0.04) - 0.04, -L / 2 + 0.02, 0.2), (sx * (W / 2 - 0.04) + 0.04, L / 2 - 0.12, 0.28), "Plastic", 0.015)
        cyl(f"foot {sx}a", rt, (sx * 0.34, -L / 2 + 0.1, 0.05), 0.045, 0.1, "Rubber", seg=20, bevel=0.005)
        cyl(f"foot {sx}b", rt, (sx * 0.34, L / 2 - 0.2, 0.05), 0.045, 0.1, "Rubber", seg=20, bevel=0.005)
        bar(rt, f"upright {sx}", (sx * 0.35, L / 2 - 0.12, 0.24), (sx * 0.3, L / 2 - 0.05, 1.15), 0.025, "BlackSteel", 20)
        bar(rt, f"handrail {sx}", (sx * 0.3, L / 2 - 0.05, 1.1), (sx * 0.3, L / 2 - 0.65, 1.0), 0.022, "Stainless", 20)
    box("motor cover", rt, (-W / 2, L / 2 - 0.3, 0.12), (W / 2, L / 2, 0.42), "Plastic", 0.03)
    box("console", rt, (-0.33, L / 2 - 0.09, 1.1), (0.33, L / 2 - 0.02, 1.4), "BlackSteel", 0.03)
    box("console screen", rt, (-0.28, L / 2 - 0.096, 1.15), (0.28, L / 2 - 0.09, 1.34), "Screen", 0.002)
    bar(rt, "handlebar", (-0.3, L / 2 - 0.65, 1.0), (0.3, L / 2 - 0.65, 1.0), 0.02, "Stainless", 20)
    return rt


def dumbbells():
    rt = root("dumbbells")
    W, D = 1.4, 0.45
    for sx in (-1, 1):
        box(f"leg {sx}", rt, (sx * (W / 2 - 0.05) - 0.02, -D / 2, 0), (sx * (W / 2 - 0.05) + 0.02, D / 2, 0.85), "BlackSteel", 0.003)
    for i, z in enumerate((0.42, 0.8)):
        box(f"rack {i+1}", rt, (-W / 2, -D / 2, z - 0.03), (W / 2, D / 2 * 0.2, z), "BlackSteel", 0.004)
        box(f"rack lip {i+1}", rt, (-W / 2, -D / 2, z), (W / 2, -D / 2 + 0.02, z + 0.03), "BlackSteel", 0.004)
        for k in range(7):
            x = -W / 2 + 0.1 + k * (W - 0.2) / 6
            size = 0.035 + (k + (0 if i == 0 else 7)) * 0.003
            hex_len = 0.14 + size * 1.0
            cyl(f"dumbbell {i}{k} handle", rt, (x, -0.02, z + size + 0.01), 0.012, hex_len, "Stainless", 'Y', seg=16, bevel=0.001)
            for sy in (-1, 1):
                cyl(f"dumbbell {i}{k} head {sy}", rt, (x, -0.02 + sy * hex_len / 2, z + size + 0.01), size, 0.07, "Rubber", 'Y', seg=6, bevel=0.004)
    return rt


def weightbench():
    rt = root("weightbench")
    box("seat", rt, (-0.16, -0.05, 0.42), (0.16, 0.45, 0.5), "Leather", 0.03)
    box("back", rt, (-0.16, -0.65, 0.5), (0.16, -0.05, 0.57), "Leather", 0.03)
    box("frame main", rt, (-0.025, -0.65, 0.36), (0.025, 0.45, 0.4), "PaintRed", 0.005)
    for y in (-0.55, 0.35):
        box(f"foot {y}", rt, (-0.3, y - 0.03, 0), (0.3, y + 0.03, 0.06), "BlackSteel", 0.005)
        box(f"leg {y}", rt, (-0.025, y - 0.025, 0.06), (0.025, y + 0.025, 0.4), "PaintRed", 0.005)
    # power rack and a loaded barbell
    for sx in (-1, 1):
        box(f"upright {sx}", rt, (sx * 0.58 - 0.03, -0.82 - 0.03, 0), (sx * 0.58 + 0.03, -0.82 + 0.03, 1.4), "BlackSteel", 0.004)
        box(f"rack foot {sx}", rt, (sx * 0.58 - 0.03, -1.05, 0), (sx * 0.58 + 0.03, -0.45, 0.05), "BlackSteel", 0.004)
        box(f"j hook {sx}", rt, (sx * 0.58 - 0.03, -0.92, 1.05), (sx * 0.58 + 0.03, -0.85, 1.1), "Stainless", 0.004)
    cyl("barbell", rt, (0, -0.88, 1.12), 0.014, 2.2, "Stainless", 'X', seg=20, bevel=0.001)
    for sx in (-1, 1):
        for k, (r, t) in enumerate([(0.225, 0.04), (0.18, 0.03)]):
            cyl(f"plate {sx}{k}", rt, (sx * (0.75 + k * 0.045), -0.88, 1.12), r, t, "Rubber", 'X', seg=48, bevel=0.004)
    return rt


def spinbike():
    rt = root("spinbike")
    cyl("flywheel", rt, (0, -0.35, 0.36), 0.25, 0.06, "PaintRed", 'X', seg=48, bevel=0.008)
    for sx in (-1, 1):
        box(f"foot {sx}", rt, (sx * 0.28 - 0.03, -0.55, 0), (sx * 0.28 + 0.03, 0.55, 0.05), "BlackSteel", 0.005)
    bar(rt, "fork l", (0.05, -0.35, 0.36), (0.02, -0.28, 0.75), 0.02, "BlackSteel", 16)
    bar(rt, "fork r", (-0.05, -0.35, 0.36), (-0.02, -0.28, 0.75), 0.02, "BlackSteel", 16)
    bar(rt, "main", (0, -0.28, 0.75), (0, 0.15, 1.0), 0.03, "PaintRed", 20)
    bar(rt, "seat post", (0, 0.15, 0.7), (0, 0.22, 1.08), 0.022, "Stainless", 16)
    box("seat", rt, (-0.09, 0.15, 1.08), (0.09, 0.4, 1.13), "Leather", 0.03)
    bar(rt, "bar post", (0, -0.28, 0.8), (0, -0.4, 1.2), 0.022, "Stainless", 16)
    bar(rt, "handlebar", (-0.22, -0.43, 1.18), (0.22, -0.43, 1.18), 0.017, "BlackSteel", 16)
    box("display", rt, (-0.07, -0.42, 1.2), (0.07, -0.38, 1.28), "Screen", 0.004)
    cyl("crank", rt, (0, -0.2, 0.36), 0.02, 0.4, "Stainless", 'X', seg=16, bevel=0.002)
    for sx in (-1, 1):
        bar(rt, f"pedal arm {sx}", (sx * 0.2, -0.2, 0.36), (sx * 0.2, -0.2, 0.2 if sx > 0 else 0.52), 0.014, "Stainless", 12)
        box(f"pedal {sx}", rt, (sx * 0.2 - 0.05, -0.28, (0.2 if sx > 0 else 0.52) - 0.01), (sx * 0.2 + 0.05, -0.13, (0.2 if sx > 0 else 0.52) + 0.01), "BlackSteel", 0.004)
    return rt


def punchbag():
    rt = root("punchbag")
    top = 2.98
    cyl("mount", rt, (0, 0, top - 0.01), 0.07, 0.02, "BlackSteel", seg=32, bevel=0.003)
    for i in range(3):
        bar(rt, f"chain {i+1}", (math.cos(i * 2.09) * 0.02, math.sin(i * 2.09) * 0.02, top - 0.02), (math.cos(i * 2.09) * 0.14, math.sin(i * 2.09) * 0.14, 2.3), 0.004, "Stainless", 6)
    lathe("bag", rt, [(0.0, 1.32), (0.11, 1.32), (0.16, 1.33), (0.175, 1.42), (0.175, 2.22), (0.16, 2.27), (0.12, 2.29), (0.0, 2.29)], "PaintRed", seg=48, subsurf=1)
    torus("strap a", rt, (0, 0, 2.1), 0.176, 0.008, "BlackSteel", 48, 8)
    torus("strap b", rt, (0, 0, 1.55), 0.176, 0.008, "BlackSteel", 48, 8)
    return rt


def yogamat():
    rt = root("yogamat")
    box("mat", rt, (-0.31, -0.9, 0.0), (0.31, 0.9, 0.012), "Mat_main", 0.005)
    cyl("rolled", rt, (0.0, 1.05, 0.06), 0.06, 0.62, "Mat_main", 'X', seg=32, bevel=0.005)
    return rt


def waterdispenser():
    rt = root("waterdispenser")
    box("body", rt, (-0.16, -0.17, 0.05), (0.16, 0.17, 1.0), "PlasticWhite", 0.03)
    box("plinth", rt, (-0.15, -0.16, 0.0), (0.15, 0.16, 0.05), "BlackSteel", 0.005)
    cyl("bottle neck", rt, (0, 0, 1.05), 0.06, 0.06, "ClearGlass", seg=32, bevel=0.005)
    lathe("bottle", rt, [(0.0, 1.07), (0.14, 1.07), (0.15, 1.1), (0.15, 1.35), (0.1, 1.42), (0.05, 1.45), (0.0, 1.45)], "ClearGlass", seg=48, subsurf=1)
    cyl("water", rt, (0, 0, 1.18), 0.135, 0.22, "BlueWater", seg=48, bevel=0.01)
    box("tray", rt, (-0.13, -0.24, 0.35), (0.13, -0.16, 0.37), "Stainless", 0.004)
    for i, (x, m) in enumerate(((-0.06, "PaintRed"), (0.06, "BookBlue"))):
        box(f"tap {i+1}", rt, (x - 0.025, -0.2, 0.62), (x + 0.025, -0.16, 0.66), m, 0.008)
    box("panel", rt, (-0.12, -0.172, 0.7), (0.12, -0.165, 0.9), "Plastic", 0.005)
    return rt


def gymmirror():
    rt = root("gymmirror")
    W, H = 1.6, 1.5
    box("frame", rt, (-W / 2, 0, 0), (W / 2, 0.035, H), "BlackSteel", 0.006)
    box("mirror", rt, (-W / 2 + 0.03, -0.004, 0.03), (W / 2 - 0.03, 0.02, H - 0.03), "Mirror", 0.001)
    return rt


PIECES = [ebench, robotarm, printer3d, beanbag, treadmill, dumbbells, weightbench, spinbike, punchbag, yogamat, waterdispenser, gymmirror]


def build_all():
    fresh()
    out = {}
    for fn in PIECES:
        rt = fn()
        out[rt.name] = stats(rt)
        export(rt, rt.name)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT, "Blender", "furniture_upper2.blend"))
    return out


if globals().get("RUN_UPPER2", True):
    RESULT = build_all()
    print(RESULT)
