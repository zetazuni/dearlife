"""Bathroom and garage furniture for Dearlife. Run inside Blender (Blender MCP: exec(open(path).read())).

It reuses the toolkit at the top of blender_kitchen.py (everything above its "toolkit ends" line), builds
each piece at real size and exports Assets/Art/Models/<id>.fbx. Saved as Blender/furniture_bathgarage.blend.
Origin: centre of the footprint on the floor. Front faces -Y (arrives facing +Z in Unity).
"""
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

_src = open(os.path.join(r"S:\Dearlife by Zetazuni", "tools", "blender_kitchen.py"), encoding="utf-8").read()
exec(_src.split("# ---------------------------------------------------------------- pieces (toolkit ends above this line)")[0])

COLORS.update({
    "Ceramic": (0.96, 0.96, 0.95), "ClearGlass": (0.75, 0.85, 0.88), "Mirror": (0.7, 0.75, 0.78), "Rattan": (0.6, 0.45, 0.28),
    "PaintRed": (0.7, 0.05, 0.05), "Car_main": (0.55, 0.05, 0.08), "CarSilver": (0.7, 0.72, 0.75), "Tire": (0.03, 0.03, 0.03),
    "CarGlass": (0.05, 0.07, 0.09), "Headlight": (0.95, 0.97, 1.0), "Taillight": (0.8, 0.02, 0.02), "Rug_main": (0.9, 0.85, 0.76),
})


# ---------------------------------------------------------------- extra toolkit

def lathe(name, parent, profile, material, seg=64, scale=(1, 1, 1), center=(0, 0, 0), bevel=0.0, subsurf=0):
    """Revolve a closed (radius, height) profile round Z. r == 0 points collapse to a single vertex."""
    bm = bmesh.new()
    rings = []
    for (r, z) in profile:
        if r <= 1e-6:
            rings.append([bm.verts.new((0, 0, z))])
        else:
            rings.append([bm.verts.new((r * math.cos(2 * math.pi * s / seg), r * math.sin(2 * math.pi * s / seg), z)) for s in range(seg)])
    for k in range(len(rings)):
        a, b = rings[k], rings[(k + 1) % len(rings)]
        for s in range(seg):
            s2 = (s + 1) % seg
            if len(a) == 1 and len(b) == 1:
                continue
            if len(a) == 1:
                bm.faces.new((a[0], b[s2], b[s]))
            elif len(b) == 1:
                bm.faces.new((a[s], a[s2], b[0]))
            else:
                bm.faces.new((a[s], a[s2], b[s2], b[s]))
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ Matrix.Diagonal((*scale, 1)), verts=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = _finish(name, parent, bm, material, 0)
    if subsurf:
        m = ob.modifiers.new("Subsurf", 'SUBSURF'); m.levels = subsurf; m.render_levels = subsurf
    return ob


def loft(name, parent, sections, material, second=None, bevel=0.0, subsurf=0, cutters=(), slot=None, face_slot=None):
    """Skin a list of same-size point rings (each a list of (x, y, z)). Ends are capped.
    second = material for side faces (top faces keep `material`), used for glass cabins."""
    bm = bmesh.new()
    rings = [[bm.verts.new(p) for p in sec] for sec in sections]
    n = len(rings[0])
    for r, (a, b) in enumerate(zip(rings, rings[1:])):
        for i in range(n):
            j = (i + 1) % n
            f = bm.faces.new((a[i], a[j], b[j], b[i]))
            if face_slot:
                f.material_index = face_slot(r, i)
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = _finish(name, parent, bm, material, 0)
    if second:
        ob.data.materials.append(mat(second))
    if subsurf:
        m = ob.modifiers.new("Subsurf", 'SUBSURF'); m.levels = subsurf; m.render_levels = subsurf
    for c in cutters:
        m = ob.modifiers.new("Cut", 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.object = c; m.solver = 'EXACT'
    if subsurf or cutters:
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
        old = ob.data
        ob.modifiers.clear()
        ob.data = me
        bpy.data.meshes.remove(old)
        for p in me.polygons:
            p.use_smooth = True
    if second and slot:
        for p in ob.data.polygons:
            p.material_index = 0 if slot(p.center) else 1
    return ob


def bar(parent, name, a, b, r, material, seg=12):
    """Round bar from point a to point b."""
    a, b = Vector(a), Vector(b)
    c = cyl(name, parent, (0, 0, 0), r, (b - a).length, material, seg=seg, bevel=0.0)
    c.matrix_world = Matrix.Translation((a + b) / 2) @ Vector((0, 0, 1)).rotation_difference((b - a).normalized()).to_matrix().to_4x4()
    return c


# ---------------------------------------------------------------- bathroom

def shower():
    """Fully glazed walk-in cubicle. Blender +X arrives as Unity -X (the model faces the garden, so it is turned
    half a turn), so the room wall side is +X here: the wall is at +X and the back at +Y, the front (with a
    door) and the side panel at -X are glass, and so is the ceiling. No bar across the middle."""
    rt = root("shower")
    W, D, H = 1.4, 1.2, 2.2                 # x, y (back wall at +Y), glass height
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    t = 0.012                               # glass thickness
    f = 0.02                                # frame width
    box("tray", rt, (x0, y0, 0), (x1, y1, 0.03), "Marble_main", 0.004)
    cyl("drain", rt, (-0.15, 0.1, 0.031), 0.05, 0.004, "Stainless", seg=32, bevel=0.0005)

    def pane(nm, a, b, z0, z1):
        """Glass pane with a slim black frame. a and b are the two end points on the floor plan (x, y)."""
        (ax, ay), (bx, by) = a, b
        along_x = abs(bx - ax) > abs(by - ay)
        if along_x:
            lo, hi = (min(ax, bx), ay - t / 2, z0), (max(ax, bx), ay + t / 2, z1)
        else:
            lo, hi = (ax - t / 2, min(ay, by), z0), (ax + t / 2, max(ay, by), z1)
        box(nm + " glass", rt, lo, hi, "ClearGlass", 0.001)
        fl = (lo[0] - (0 if along_x else f / 2 - t / 2), lo[1] - (f / 2 - t / 2 if along_x else 0), lo[2])
        fh = (hi[0] + (0 if along_x else f / 2 - t / 2), hi[1] + (f / 2 - t / 2 if along_x else 0), hi[2])
        for (zz0, zz1) in ((z0, z0 + f), (z1 - f, z1)):
            box(nm + " rail", rt, (fl[0], fl[1], zz0), (fh[0], fh[1], zz1), "BlackSteel", 0.002)
        for k in (0, 1):
            if along_x:
                xx = (lo[0], lo[0] + f) if k == 0 else (hi[0] - f, hi[0])
                box(nm + " stile", rt, (xx[0], fl[1], z0), (xx[1], fh[1], z1), "BlackSteel", 0.002)
            else:
                yy = (lo[1], lo[1] + f) if k == 0 else (hi[1] - f, hi[1])
                box(nm + " stile", rt, (fl[0], yy[0], z0), (fh[0], yy[1], z1), "BlackSteel", 0.002)

    zt = 0.03
    pane("front fixed", (-0.05, y0), (x1, y0), zt, H)                           # fixed front panel (next to the wall)
    pane("front door", (x0, y0 - 0.004), (-0.06, y0 - 0.004), zt, H - 0.02)     # hinged door on the open side
    pane("side", (x0, y0), (x0, y1), zt, H)                                     # side panel
    box("ceiling glass", rt, (x0, y0, H), (x1, y1, H + t), "ClearGlass", 0.001)
    box("ceiling rim front", rt, (x0, y0 - 0.006, H), (x1, y0 + f, H + 0.02), "BlackSteel", 0.002)
    box("ceiling rim side", rt, (x0 - 0.006, y0, H), (x0 + f, y1, H + 0.02), "BlackSteel", 0.002)
    cyl("door handle", rt, (-0.22, y0 - 0.045, 1.05), 0.008, 0.5, "Stainless", 'Z', seg=16, bevel=0.001)
    for hz in (0.3, 1.9):
        box(f"hinge {hz}", rt, (x0, y0 - 0.015, hz), (x0 + 0.03, y0 + 0.012, hz + 0.09), "Stainless", 0.002)
    # rain head on an arm from the back wall, hand shower and valve
    cyl("arm", rt, (0.0, y1 - 0.3, 2.05), 0.012, 0.6, "BlackSteel", 'Y', seg=20, bevel=0.002)
    cyl("rain head", rt, (0.0, y1 - 0.55, 2.03), 0.16, 0.02, "BlackSteel", seg=48, bevel=0.004)
    cyl("rain plate", rt, (0.0, y1 - 0.55, 2.018), 0.14, 0.004, "Stainless", seg=48, bevel=0.0005)
    cyl("valve", rt, (0.2, y1 - 0.012, 1.1), 0.05, 0.024, "BlackSteel", 'Y', seg=32, bevel=0.003)
    cyl("hand rail", rt, (-0.25, y1 - 0.03, 1.15), 0.009, 0.7, "BlackSteel", seg=20, bevel=0.001)
    cyl("hand shower", rt, (-0.25, y1 - 0.07, 1.25), 0.02, 0.2, "BlackSteel", 'Z', seg=20, bevel=0.003)
    return rt


def vanity():
    rt = root("vanity")
    W, D, H = 1.4, 0.5, 0.82
    box("body", rt, (-W / 2, -D / 2, 0.3), (W / 2, D / 2, H - 0.03), "Walnut", 0.006)
    for i, (a, b) in enumerate([(-W / 2 + 0.01, 0.0), (0.005, W / 2 - 0.01)]):
        box(f"drawer {i+1}", rt, (a, -D / 2 - 0.02, 0.32), (b, -D / 2, H - 0.05), "Walnut", 0.005)
        box(f"pull {i+1}", rt, ((a + b) / 2 - 0.12, -D / 2 - 0.025, H - 0.09), ((a + b) / 2 + 0.12, -D / 2 - 0.02, H - 0.083), "Brass", 0.001)
    box("top", rt, (-W / 2 - 0.01, -D / 2 - 0.02, H - 0.03), (W / 2 + 0.01, D / 2, H), "Marble_main", 0.004)
    # vessel basin on the left
    bx = -0.32
    prof = [(0.0, 0.0), (0.12, 0.0), (0.19, 0.06), (0.21, 0.14), (0.205, 0.145), (0.19, 0.14), (0.18, 0.07), (0.11, 0.02), (0.0, 0.02)]
    lathe("basin", rt, prof, "Ceramic", seg=64, center=(bx, -0.02, H), subsurf=1)
    cyl("tap base", rt, (bx, D / 2 - 0.1, H + 0.01), 0.022, 0.02, "BlackSteel", seg=32, bevel=0.002)
    cyl("tap neck", rt, (bx, D / 2 - 0.1, H + 0.15), 0.011, 0.28, "BlackSteel", seg=24, bevel=0.002)
    cyl("tap spout", rt, (bx, D / 2 - 0.185, H + 0.285), 0.01, 0.17, "BlackSteel", 'Y', seg=24, bevel=0.002)
    # a little tray and soap bottle on the right
    box("tray", rt, (0.2, -0.12, H), (0.5, 0.12, H + 0.012), "Walnut", 0.004)
    cyl("soap", rt, (0.3, 0.0, H + 0.08), 0.03, 0.14, "Ceramic", seg=32, bevel=0.004)
    return rt


def bathmirror():
    rt = root("bathmirror")
    box("frame", rt, (-0.55, 0.0, 1.0), (0.55, 0.035, 2.0), "BlackSteel", 0.004)
    box("mirror", rt, (-0.53, -0.004, 1.02), (0.53, 0.02, 1.98), "Mirror", 0.001)
    return rt


def toilet():
    rt = root("toilet")
    box("wall unit", rt, (-0.2, 0.2, 0.0), (0.2, 0.32, 1.0), "Ceramic", 0.01)
    box("flush plate", rt, (-0.12, 0.185, 0.72), (0.12, 0.2, 0.86), "Stainless", 0.004)
    bowl = [(0.0, 0.22), (0.12, 0.22), (0.17, 0.3), (0.19, 0.42), (0.17, 0.43), (0.15, 0.43), (0.14, 0.38), (0.09, 0.3), (0.0, 0.3)]
    lathe("bowl", rt, bowl, "Ceramic", seg=64, scale=(1.0, 1.5, 1), center=(0, 0.0, 0), subsurf=1)
    lid = [(0.0, 0.458), (0.17, 0.458), (0.19, 0.446), (0.19, 0.43), (0.0, 0.43)]
    lathe("lid", rt, lid, "Ceramic", seg=64, scale=(1.0, 1.5, 1), subsurf=1)
    return rt


def bathtub():
    rt = root("bathtub")
    prof = [(0.0, 0.05), (0.3, 0.05), (0.38, 0.12), (0.43, 0.3), (0.43, 0.56), (0.42, 0.6), (0.39, 0.6), (0.38, 0.55), (0.33, 0.25), (0.22, 0.17), (0.0, 0.17)]
    lathe("tub", rt, prof, "Ceramic", seg=80, scale=(2.0, 1.0, 1), subsurf=2)
    for sx in (-1, 1):
        cyl(f"foot {sx}", rt, (sx * 0.55, 0, 0.025), 0.05, 0.05, "BlackSteel", seg=32, bevel=0.004)
    # floor standing tap at one end
    cyl("tap column", rt, (0.98, 0.0, 0.5), 0.016, 1.0, "BlackSteel", seg=24, bevel=0.002)
    cyl("tap arm", rt, (0.88, 0.0, 0.98), 0.012, 0.22, "BlackSteel", 'X', seg=24, bevel=0.002)
    return rt


def towelrack():
    rt = root("towelrack")
    W, H = 0.5, 1.5
    for sx in (-1, 1):
        cyl(f"rail {sx}", rt, (sx * (W / 2 - 0.02), 0.0, 0.05 + (H - 0.1) / 2), 0.013, H - 0.1, "BlackSteel", seg=20, bevel=0.002)
    for i in range(8):
        cyl(f"bar {i+1}", rt, (0, 0.0, 0.2 + i * 0.16), 0.01, W - 0.04, "BlackSteel", 'X', seg=16, bevel=0.001)
    # two folded towels over the bars
    box("towel a", rt, (-0.2, -0.055, 0.78), (0.2, 0.055, 1.1), "Fabric_main", 0.02)
    box("towel b", rt, (-0.2, -0.055, 0.26), (0.2, 0.055, 0.5), "Rug_main", 0.02)
    return rt


def bathmat():
    rt = root("bathmat")
    box("mat", rt, (-0.45, -0.28, 0.0), (0.45, 0.28, 0.018), "Rug_main", 0.008)
    return rt


def _appliance(name, drum_glass=True):
    rt = root(name)
    box("body", rt, (-0.3, -0.31, 0.02), (0.3, 0.32, 0.85), "Ceramic", 0.012)
    box("plinth", rt, (-0.28, -0.29, 0.0), (0.28, 0.3, 0.03), "BlackSteel", 0.002)
    box("control panel", rt, (-0.29, -0.325, 0.75), (0.29, -0.31, 0.84), "BlackSteel", 0.004)
    cyl("dial", rt, (0.18, -0.335, 0.795), 0.025, 0.02, "Stainless", 'Y', seg=32, bevel=0.002)
    cyl("door ring", rt, (0, -0.325, 0.4), 0.235, 0.03, "Stainless", 'Y', seg=64, bevel=0.004)
    cyl("door glass", rt, (0, -0.342, 0.4), 0.185, 0.012, "GlassDark", 'Y', seg=64, bevel=0.002)
    return rt


def washer(): return _appliance("washer")
def dryer(): return _appliance("dryer")


def basket():
    rt = root("basket")
    cyl("body", rt, (0, 0, 0.29), 0.23, 0.56, "Rattan", r2=0.27, seg=48, bevel=0.01)
    cyl("base", rt, (0, 0, 0.01), 0.23, 0.02, "Walnut", seg=48, bevel=0.003)
    torus("rim", rt, (0, 0, 0.57), 0.27, 0.014, "Walnut", 48, 8)
    # a few towels poking out
    box("towel", rt, (-0.14, -0.1, 0.5), (0.14, 0.12, 0.66), "Fabric_main", 0.03)
    return rt


# ---------------------------------------------------------------- garage

def workbench():
    rt = root("workbench")
    W, D, H = 1.9, 0.65, 0.92
    box("top", rt, (-W / 2, -D / 2, H - 0.05), (W / 2, D / 2, H), "Walnut", 0.006)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"leg {sx}{sy}", rt, (sx * (W / 2 - 0.06) - 0.025, sy * (D / 2 - 0.06) - 0.025, 0), (sx * (W / 2 - 0.06) + 0.025, sy * (D / 2 - 0.06) + 0.025, H - 0.05), "BlackSteel", 0.003)
    box("lower shelf", rt, (-W / 2 + 0.04, -D / 2 + 0.04, 0.22), (W / 2 - 0.04, D / 2 - 0.04, 0.26), "Walnut", 0.004)
    box("rail back", rt, (-W / 2 + 0.04, D / 2 - 0.085, 0.5), (W / 2 - 0.04, D / 2 - 0.06, 0.53), "BlackSteel", 0.003)
    # bench vise and a few tools
    box("vise body", rt, (0.65, -D / 2 - 0.03, H), (0.83, -D / 2 + 0.1, H + 0.09), "PaintRed", 0.008)
    box("vise jaw", rt, (0.68, -D / 2 - 0.09, H + 0.02), (0.8, -D / 2 - 0.03, H + 0.09), "BlackSteel", 0.004)
    # pegboard on the wall behind with tool hooks
    box("pegboard", rt, (-W / 2, D / 2 - 0.0, 1.15), (W / 2, D / 2 + 0.02, 2.0), "Rattan", 0.003)
    for i in range(5):
        box(f"tool {i+1}", rt, (-0.7 + i * 0.35, D / 2 - 0.035, 1.35 + (i % 2) * 0.25), (-0.66 + i * 0.35, D / 2, 1.75 + (i % 2) * 0.1), "BlackSteel", 0.003)
    return rt


def garageshelf():
    rt = root("garageshelf")
    W, D, H = 1.2, 0.45, 2.0
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"post {sx}{sy}", rt, (sx * (W / 2 - 0.02) - 0.02, sy * (D / 2 - 0.02) - 0.02, 0), (sx * (W / 2 - 0.02) + 0.02, sy * (D / 2 - 0.02) + 0.02, H), "BlackSteel", 0.002)
    for i in range(5):
        z = 0.15 + i * 0.46
        box(f"shelf {i+1}", rt, (-W / 2, -D / 2, z), (W / 2, D / 2, z + 0.03), "Stainless", 0.003)
    # bins and boxes
    for i, (x, z, w, h, m) in enumerate([(-0.3, 0.18, 0.4, 0.3, "PaintRed"), (0.25, 0.18, 0.4, 0.25, "Rattan"), (-0.25, 0.64, 0.42, 0.3, "Rattan"),
                                         (0.3, 0.64, 0.3, 0.2, "PaintRed"), (0.0, 1.1, 0.7, 0.28, "Rattan"), (-0.3, 1.56, 0.4, 0.3, "PaintRed"), (0.25, 1.56, 0.45, 0.25, "Rattan")]):
        box(f"bin {i+1}", rt, (x - w / 2, -0.17, z + 0.03), (x + w / 2, 0.17, z + 0.03 + h), m, 0.012)
    return rt


def toolchest():
    rt = root("toolchest")
    W, D, H = 0.75, 0.46, 1.0
    box("body", rt, (-W / 2, -D / 2, 0.12), (W / 2, D / 2, H - 0.04), "PaintRed", 0.008)
    box("lid", rt, (-W / 2 - 0.005, -D / 2 - 0.005, H - 0.05), (W / 2 + 0.005, D / 2 + 0.005, H), "BlackSteel", 0.008)
    z = 0.16
    for i, h in enumerate([0.12, 0.14, 0.18, 0.18, 0.2]):
        box(f"drawer {i+1}", rt, (-W / 2 + 0.02, -D / 2 - 0.025, z), (W / 2 - 0.02, -D / 2, z + h - 0.008), "PaintRed", 0.006)
        box(f"handle {i+1}", rt, (-0.2, -D / 2 - 0.045, z + h - 0.055), (0.2, -D / 2 - 0.025, z + h - 0.04), "Stainless", 0.004)
        z += h
    for sx in (-1, 1):
        cyl(f"caster {sx}", rt, (sx * 0.3, 0.0, 0.05), 0.05, 0.05, "BlackSteel", 'Y', seg=24, bevel=0.005)
        cyl(f"caster b {sx}", rt, (sx * 0.3, 0.12, 0.05), 0.05, 0.05, "BlackSteel", 'Y', seg=24, bevel=0.005)
    return rt


def bicycle():
    rt = root("bicycle")
    R = 0.34
    fy, ry = -0.55, 0.55
    for n, y in (("front", fy), ("rear", ry)):
        torus(f"{n} tyre", rt, (0, y, R), R - 0.02, 0.022, "Tire", 64, 10, 'X')
        torus(f"{n} rim", rt, (0, y, R), R - 0.045, 0.006, "Stainless", 64, 8, 'X')
        cyl(f"{n} hub", rt, (0, y, R), 0.02, 0.06, "Stainless", 'X', seg=16, bevel=0.002)
    for n, y in (("front", fy), ("rear", ry)):
        for i in range(12):
            an = math.radians(i * 30)
            bar(rt, f"{n} spoke {i+1}", (0.004 * (1 if i % 2 else -1), y, R), (0, y + math.sin(an) * (R - 0.045), R + math.cos(an) * (R - 0.045)), 0.0015, "Stainless", 6)

    def tube(nm, a, b, r=0.014, m="PaintRed"):
        bar(rt, nm, a, b, r, m, 16)
    bb = (0, 0.18, 0.3)
    seat = (0, 0.3, 0.9)
    head = (0, -0.42, 0.9)
    tube("seat tube", bb, seat)
    tube("top tube", seat, head)
    tube("down tube", bb, head)
    tube("chain stay l", bb, (0.05, ry, R)); tube("chain stay r", bb, (-0.05, ry, R))
    tube("seat stay l", seat, (0.05, ry, R)); tube("seat stay r", seat, (-0.05, ry, R))
    tube("fork l", (0.05, fy, R), (0.02, -0.44, 0.92)); tube("fork r", (-0.05, fy, R), (-0.02, -0.44, 0.92))
    tube("handlebar", (-0.28, -0.44, 1.0), (0.28, -0.44, 1.0), 0.011, "BlackSteel")
    tube("stem", (0, -0.42, 0.9), (0, -0.44, 1.0), 0.013, "BlackSteel")
    box("saddle", rt, (-0.07, 0.2, 0.93), (0.07, 0.42, 0.98), "BlackSteel", 0.02)
    tube("seat post", seat, (0, 0.3, 0.94), 0.012, "BlackSteel")
    cyl("chainring", rt, (0.03, 0.18, 0.3), 0.09, 0.01, "Stainless", 'X', seg=32, bevel=0.001)
    for sx in (-1, 1):
        box(f"pedal {sx}", rt, (sx * 0.15 - 0.05, 0.11, 0.26), (sx * 0.15 + 0.05, 0.25, 0.28), "BlackSteel", 0.004)
    return rt


def evcharger():
    rt = root("evcharger")
    box("body", rt, (-0.13, 0.0, 1.0), (0.13, 0.12, 1.42), "Ceramic", 0.03)
    box("front", rt, (-0.1, -0.006, 1.06), (0.1, 0.0, 1.36), "GlassDark", 0.005)
    cyl("light", rt, (0, -0.008, 1.3), 0.012, 0.004, "Headlight", 'Y', seg=16, bevel=0.0005)
    cyl("holster", rt, (0.0, -0.03, 1.1), 0.035, 0.05, "BlackSteel", 'Y', seg=24, bevel=0.004)
    cyl("cable", rt, (0.0, -0.03, 0.75), 0.01, 0.7, "BlackSteel", seg=12, bevel=0.001)
    return rt


# ---------------------------------------------------------------- cars

def _cross_body(y, zb, zt, hb, ht):
    hm = max(hb, ht) + 0.02
    dz = zt - zb
    pts = [(-hb, zb), (-hm, zb + 0.14 * dz), (-hm, zt - 0.16 * dz), (-ht, zt)]
    return [(x, y, z) for (x, z) in pts + [(-x, z) for (x, z) in reversed(pts)]]


def _cross_cabin(y, zb, zr, hb, hr):
    """Eight points round the cabin: sill, window base, window top and roof edge on each side.
    Segments 1 and 5 are the side windows, 2 to 4 the shoulders and roof."""
    L0, L3 = (-hb, zb), (-hr, zr)
    lerp = lambda t: (L0[0] + (L3[0] - L0[0]) * t, L0[1] + (L3[1] - L0[1]) * t)
    left = [L0, lerp(0.12), lerp(0.86), L3]
    pts = left + [(-x, z) for (x, z) in reversed(left)]
    return [(x, y, z) for (x, z) in pts]


def car(name, body_mat, body_st, cabin_st, roles, wheelbase, track, wr, roof_rails=False):
    rt = root(name)
    # wheel arch cutters
    cutters = []
    for sx in (-1, 1):
        for sy in (-1, 1):
            c = cyl(f"cut {sx}{sy}", rt, (sx * track, sy * wheelbase / 2, wr), wr + 0.06, 0.5, "BlackSteel", 'X', seg=32, bevel=0)
            cutters.append(c)
    loft("body", rt, [_cross_body(*s) for s in body_st], body_mat, subsurf=2, cutters=cutters)
    roof_top = max(c[2] for c in cabin_st)

    def face_slot(r, i):
        """0 = paint, 1 = glass. roles[r] is (side windows glass?, top glass?) for that stretch of the cabin."""
        side, top = roles[r]
        return 1 if ((side and i in (1, 5)) or (top and i in (2, 3, 4))) else 0
    loft("cabin", rt, [_cross_cabin(*s) for s in cabin_st], body_mat, second="CarGlass", subsurf=3, face_slot=face_slot)
    for c in cutters:
        bpy.data.objects.remove(c, do_unlink=True)
    ymin = min(s[0] for s in body_st); ymax = max(s[0] for s in body_st)
    zb = body_st[0][1]
    # wheels
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * track, sy * wheelbase / 2
            cyl(f"tyre {sx}{sy}", rt, (x, y, wr), wr, 0.22, "Tire", 'X', seg=48, bevel=0.03)
            cyl(f"rim {sx}{sy}", rt, (x + sx * 0.005, y, wr), wr * 0.68, 0.22, "Stainless", 'X', seg=48, bevel=0.01)
            cyl(f"disc {sx}{sy}", rt, (x + sx * 0.112, y, wr), wr * 0.6, 0.012, "BlackSteel", 'X', seg=48, bevel=0.002)
            for i in range(5):
                an = math.radians(72 * i + 18)
                bar(rt, f"spoke {sx}{sy}.{i+1}", (x + sx * 0.12, y, wr), (x + sx * 0.12, y + math.sin(an) * wr * 0.6, wr + math.cos(an) * wr * 0.6), 0.02, "Stainless", 12)
            cyl(f"hub {sx}{sy}", rt, (x + sx * 0.125, y, wr), wr * 0.14, 0.02, "Stainless", 'X', seg=24, bevel=0.003)
    # lights and details
    fy = ymin
    for sx in (-1, 1):
        box(f"headlight {sx}", rt, (sx * 0.46 - 0.16, fy + 0.03, zb + 0.16), (sx * 0.46 + 0.16, fy + 0.2, zb + 0.25), "Headlight", 0.02)
        box(f"taillight bezel {sx}", rt, (sx * 0.42 - 0.2, ymax - 0.2, zb + 0.26), (sx * 0.42 + 0.2, ymax + 0.02, zb + 0.5), "BlackSteel", 0.02)
        box(f"taillight {sx}", rt, (sx * 0.42 - 0.17, ymax - 0.2, zb + 0.29), (sx * 0.42 + 0.17, ymax + 0.045, zb + 0.47), "Taillight", 0.02)
        box(f"mirror {sx}", rt, (sx * (track + 0.1) - 0.05, ymin + 1.35, body_st[3][2] + 0.03), (sx * (track + 0.1) + 0.05, ymin + 1.5, body_st[3][2] + 0.14), body_mat, 0.02)
    box("light bar", rt, (-0.28, ymax - 0.1, zb + 0.34), (0.28, ymax + 0.03, zb + 0.4), "Taillight", 0.01)
    box("grille", rt, (-0.4, fy - 0.01, zb + 0.14), (0.4, fy + 0.1, zb + 0.28), "BlackSteel", 0.01)
    box("front splitter", rt, (-0.72, fy - 0.02, zb - 0.07), (0.72, fy + 0.3, zb + 0.06), "BlackSteel", 0.01)
    box("rear diffuser", rt, (-0.7, ymax - 0.3, zb - 0.07), (0.7, ymax + 0.02, zb + 0.06), "BlackSteel", 0.01)
    if roof_rails:
        for sx in (-1, 1):
            cyl(f"roof rail {sx}", rt, (sx * 0.62, 0.4, roof_top + 0.02), 0.012, 1.7, "Stainless", 'Y', seg=16, bevel=0.002)
    return rt


def sedan():
    body = [(2.33, 0.42, 0.92, 0.70, 0.62), (2.28, 0.35, 0.98, 0.84, 0.72), (2.0, 0.32, 1.0, 0.89, 0.78), (1.2, 0.30, 0.98, 0.9, 0.82),
            (0.0, 0.28, 0.96, 0.9, 0.84), (-1.0, 0.28, 0.98, 0.9, 0.82), (-1.6, 0.30, 0.96, 0.89, 0.76), (-2.1, 0.36, 0.84, 0.85, 0.70),
            (-2.3, 0.42, 0.66, 0.72, 0.58), (-2.33, 0.45, 0.6, 0.6, 0.5)]
    cabin = [(1.28, 0.96, 0.98, 0.80, 0.70), (1.0, 0.96, 1.30, 0.79, 0.66), (0.6, 0.96, 1.42, 0.79, 0.64), (0.25, 0.96, 1.43, 0.79, 0.64),
             (0.15, 0.96, 1.43, 0.79, 0.64), (-0.2, 0.96, 1.43, 0.79, 0.64), (-0.7, 0.96, 1.38, 0.80, 0.66), (-1.0, 0.96, 1.15, 0.82, 0.72),
             (-1.22, 0.96, 0.98, 0.84, 0.78)]
    roles = [(0, 1), (0, 1), (1, 0), (0, 0), (1, 0), (1, 0), (0, 1), (0, 1)]   # per stretch between stations
    return car("sedan", "Car_main", body, cabin, roles, 2.8, 0.83, 0.32)


def mpv():
    body = [(2.10, 0.40, 1.05, 0.68, 0.62), (2.05, 0.34, 1.12, 0.80, 0.72), (1.4, 0.32, 1.12, 0.83, 0.76), (0.0, 0.30, 1.08, 0.84, 0.78),
            (-1.0, 0.30, 1.06, 0.84, 0.78), (-1.45, 0.32, 1.02, 0.82, 0.72), (-1.95, 0.38, 0.86, 0.78, 0.66), (-2.1, 0.42, 0.7, 0.66, 0.56)]
    cabin = [(1.95, 1.10, 1.12, 0.78, 0.70), (1.85, 1.10, 1.5, 0.78, 0.70), (1.4, 1.10, 1.66, 0.78, 0.70), (0.5, 1.10, 1.68, 0.78, 0.70),
             (0.4, 1.10, 1.68, 0.78, 0.70), (-0.6, 1.10, 1.68, 0.78, 0.70), (-1.0, 1.10, 1.62, 0.79, 0.70), (-1.35, 1.08, 1.22, 0.8, 0.76),
             (-1.5, 1.05, 1.05, 0.82, 0.8)]
    roles = [(0, 1), (0, 1), (1, 0), (0, 0), (1, 0), (1, 0), (0, 1), (0, 1)]
    return car("mpv", "CarSilver", body, cabin, roles, 2.65, 0.78, 0.33, roof_rails=True)


PIECES = [shower, vanity, bathmirror, toilet, bathtub, towelrack, bathmat, washer, dryer, basket,
          workbench, garageshelf, toolchest, bicycle, evcharger, sedan, mpv]


def build_all():
    fresh()
    out = {}
    for fn in PIECES:
        rt = fn()
        out[rt.name] = stats(rt)
        export(rt, rt.name)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT, "Blender", "furniture_bathgarage.blend"))
    return out


if globals().get("RUN_BATHGARAGE", True):
    RESULT = build_all()
    print(RESULT)
