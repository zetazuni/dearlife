"""Things for the pets: a food and a water bowl on a silicone mat, and a round pet bed.
Run inside Blender (Blender MCP: exec(open(path).read())). Reuses the toolkit at the top of blender_kitchen.py and exports
Assets/Art/Models/petbowls.fbx and petbed.fbx. Saved as Blender/furniture_pets.blend.
Origin: centre of the footprint on the floor. Front faces -Y (arrives facing +Z in Unity).
The kibble is one object named "Kibble": the game shows it only while there is food in the bowl.
"""
import bpy, bmesh, math, os, random
from mathutils import Matrix, Vector

_src = open(os.path.join(r"S:\Dearlife by Zetazuni", "tools", "blender_kitchen.py"), encoding="utf-8").read()
exec(_src.split("# ---------------------------------------------------------------- pieces (toolkit ends above this line)")[0])

COLORS.update({
    "Ceramic": (0.95, 0.95, 0.93), "Mat_main": (0.22, 0.5, 0.5), "Bread": (0.5, 0.28, 0.11), "BlueWater": (0.3, 0.6, 0.9),
    "Beanbag_main": (0.22, 0.34, 0.36), "Fabric_main": (0.85, 0.8, 0.7),
})


def lathe(name, parent, profile, material, center=(0, 0, 0), seg=96, smooth=True):
    """Spins a profile of (radius, height) points round Z. The profile runs from the outside bottom over the rim to the inside."""
    bm = bmesh.new()
    rings = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        ca, sa = math.cos(a), math.sin(a)
        rings.append([bm.verts.new((center[0] + r * ca, center[1] + r * sa, center[2] + z)) for r, z in profile])
    for i in range(seg):
        a, b = rings[i], rings[(i + 1) % seg]
        for k in range(len(profile) - 1):
            bm.faces.new((a[k], b[k], b[k + 1], a[k + 1]))
    # close the bottom and the inside floor with fans
    for k in (0, len(profile) - 1):
        c = bm.verts.new((center[0], center[1], center[2] + profile[k][1]))
        for i in range(seg):
            bm.faces.new((rings[(i + 1) % seg][k], rings[i][k], c) if k == 0 else (rings[i][k], rings[(i + 1) % seg][k], c))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _finish(name, parent, bm, material, 0, smooth)


def bowl_profile(r_base, r_top, h, wall):
    pts = []
    # outside: a rounded foot, the flared side, the rolled rim
    for t in range(9):
        a = t / 8
        pts.append((r_base * (0.92 + 0.08 * math.sin(a * math.pi / 2)), h * 0.06 * a))
    for t in range(1, 13):
        a = t / 12
        r = r_base + (r_top - r_base) * (1 - (1 - a) ** 1.6)
        pts.append((r, h * 0.06 + (h * 0.94) * a))
    for t in range(1, 9):
        a = math.pi * t / 8
        pts.append((r_top - wall / 2 + wall / 2 * math.cos(a), h + wall / 2 * math.sin(a)))
    # inside, down to the floor of the bowl
    for t in range(1, 13):
        a = t / 12
        r = (r_top - wall) - (r_top - r_base) * (a ** 1.4)
        pts.append((max(r, 0.004), h - (h - wall * 1.4) * a))
    return pts


def kibble(parent, center, r, depth, count, seed):
    """A heap of little brown biscuits filling the bottom of the food bowl, one object."""
    random.seed(seed)
    bm = bmesh.new()
    for i in range(count):
        rr = r * math.sqrt(random.random())
        a = random.random() * 2 * math.pi
        z = center[2] + depth * (0.35 + 0.65 * random.random()) * (1 - 0.5 * (rr / r) ** 2)
        s = random.uniform(0.0075, 0.0095)
        m = (Matrix.Translation((center[0] + rr * math.cos(a), center[1] + rr * math.sin(a), z))
             @ Matrix.Rotation(random.uniform(0, math.pi), 4, 'Z') @ Matrix.Rotation(random.uniform(-0.6, 0.6), 4, 'X')
             @ Matrix.Diagonal((s, s * 0.9, s * 0.55, 1)))
        geom = bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=1.0)
        bmesh.ops.transform(bm, matrix=m, verts=geom["verts"])
    bm.normal_update()
    return _finish("Kibble", parent, bm, "Bread", 0)


def rounded_mat(name, parent, w, d, h, r, material, seg=10):
    bm = bmesh.new()
    outline = []
    for cx, cy, a0 in ((w / 2 - r, d / 2 - r, 0), (-w / 2 + r, d / 2 - r, 90), (-w / 2 + r, -d / 2 + r, 180), (w / 2 - r, -d / 2 + r, 270)):
        for i in range(seg + 1):
            a = math.radians(a0 + 90 * i / seg)
            outline.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    bot = [bm.verts.new((x, y, 0)) for x, y in outline]
    top = [bm.verts.new((x, y, h)) for x, y in outline]
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((bot[i], bot[j], top[j], top[i]))
    bm.faces.new(top)
    bm.faces.new(list(reversed(bot)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _finish(name, parent, bm, material, 0.002)


def build_bowls():
    rt = root("petbowls")
    rounded_mat("Mat", rt, 0.46, 0.27, 0.006, 0.05, "Mat_main")
    # food bowl (glazed ceramic) on the left, water bowl (steel) on the right
    lathe("Food bowl", rt, bowl_profile(0.062, 0.085, 0.058, 0.008), "Ceramic", center=(-0.11, 0, 0.006))
    kibble(rt, (-0.11, 0, 0.006 + 0.012), 0.058, 0.03, 140, 7)
    lathe("Water bowl", rt, bowl_profile(0.06, 0.082, 0.05, 0.004), "Stainless", center=(0.11, 0, 0.006))
    cyl("Water", rt, (0.11, 0, 0.006 + 0.036), 0.072, 0.002, "BlueWater", seg=64, bevel=0)
    return rt


def build_bed():
    rt = root("petbed")
    R = 0.3
    # a flat base, a soft round bolster all round and a plump cushion inside
    cyl("Base", rt, (0, 0, 0.03), R, 0.06, "Beanbag_main", seg=96, bevel=0.02)
    torus("Bolster", rt, (0, 0, 0.1), R - 0.065, 0.065, "Beanbag_main", seg=96, rings=24)
    lathe("Cushion", rt, [(0.2, 0.06), (0.214, 0.075), (0.212, 0.095), (0.195, 0.112), (0.15, 0.123), (0.08, 0.128), (0.004, 0.13)], "Fabric_main")
    return rt


fresh()
bowls = build_bowls()
bed = build_bed()
bed.location = (0, 0, 0)
for rt, fid in ((bowls, "petbowls"), (bed, "petbed")):
    export(rt, fid)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT, "Blender", "furniture_pets.blend"))
RESULT = "petbowls %d tris, petbed %d tris" % (stats(bowls), stats(bed))
print(RESULT)
