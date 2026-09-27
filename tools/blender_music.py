"""Musical instruments for Tiramisu 3D: an acoustic guitar on a stand and a synth keyboard on a stand.
Run inside Blender (Blender MCP: exec(open(path).read())). It reuses the toolkit at the top of blender_kitchen.py, builds each piece at
real size and exports Assets/Art/Models/<id>.fbx (guitar.fbx, synth.fbx). Saved as Blender/furniture_music.blend.
Origin: centre of the footprint on the floor. Front faces -Y (arrives facing +Z in Unity).
The parts of the guitar itself are named G_...: the game lifts them off the stand while somebody plays it.
"""
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

_src = open(os.path.join(r"S:\Tiramisu Corner by Zetazuni", "tools", "blender_kitchen.py"), encoding="utf-8").read()
exec(_src.split("# ---------------------------------------------------------------- pieces (toolkit ends above this line)")[0])

COLORS.update({
    "Rattan": (0.78, 0.6, 0.36), "Plastic": (0.08, 0.08, 0.09), "PlasticWhite": (0.94, 0.94, 0.92), "Screen": (0.05, 0.1, 0.16),
    "TVBlack": (0.02, 0.02, 0.025), "Walnut": (0.3, 0.17, 0.1),
})


def poly_body(name, parent, outline, depth, top_mat, side_mat, top_inset=0.0):
    """A flat body from a closed outline of (x, z) points, thickness `depth` along Y (front at -Y). Top plate and sides get their own material."""
    n = len(outline)
    # sides and back
    bm = bmesh.new()
    front = [bm.verts.new((x, -depth / 2, z)) for x, z in outline]
    back = [bm.verts.new((x, depth / 2, z)) for x, z in outline]
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], back[i], back[j], front[j]))
    cz = sum(z for x, z in outline) / n
    cb = bm.verts.new((0, depth / 2, cz))
    for i in range(n):
        bm.faces.new((back[(i + 1) % n], back[i], cb))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    sides = _finish(name + " sides", parent, bm, side_mat, 0.004)
    # the front plate
    bm2 = bmesh.new()
    fp = [bm2.verts.new((x * 0.985, -depth / 2 - 0.003, cz + (z - cz) * 0.985)) for x, z in outline]
    cf = bm2.verts.new((0, -depth / 2 - 0.006, cz))
    for i in range(n):
        bm2.faces.new((fp[i], fp[(i + 1) % n], cf))
    bmesh.ops.recalc_face_normals(bm2, faces=bm2.faces)
    _finish(name + " top", parent, bm2, top_mat, 0.0)
    return sides


def guitar_outline(samples=120):
    """The figure of eight of an acoustic guitar body: two circles joined with a soft waist, found by walking out along rays."""
    r_low, z_low = 0.19, 0.19
    r_up, z_up = 0.145, 0.40
    def sdf(x, z):
        d1 = math.hypot(x, z - z_low) - r_low
        d2 = math.hypot(x, z - z_up) - r_up
        k = 0.07
        h = max(k - abs(d1 - d2), 0) / k
        return min(d1, d2) - h * h * k * 0.25
    cx, cz = 0.0, 0.29
    pts = []
    for i in range(samples):
        a = 2 * math.pi * i / samples + math.pi / 2       # start at the top
        dx, dz = math.cos(a), math.sin(a)
        t = 0.0
        while sdf(cx + dx * t, cz + dz * t) < 0 and t < 0.6:
            t += 0.002
        pts.append((cx + dx * t, cz + dz * t))
    return pts


def build_guitar():
    rt = root("guitar")
    gt = rt          # every guitar part is named G_...: the game lifts those off the stand while somebody plays
    outline = guitar_outline()
    # the guitar itself is built upright in its own space, then tilted back a little on the stand
    body = poly_body("Body", gt, outline, 0.105, "Rattan", "Walnut")
    # soundhole, rosette and bridge
    cyl("Soundhole", gt, (0, -0.0565, 0.30), 0.048, 0.006, "Plastic", axis='Y', seg=48)
    cyl("Rosette", gt, (0, -0.0555, 0.30), 0.058, 0.004, "Walnut", axis='Y', seg=48)
    box("Bridge", gt, (-0.075, -0.066, 0.150), (0.075, -0.052, 0.170), "Walnut", 0.004)
    box("Saddle", gt, (-0.06, -0.070, 0.168), (0.06, -0.064, 0.174), "PlasticWhite", 0.001)
    box("Pickguard", gt, (0.02, -0.0555, 0.20), (0.11, -0.0545, 0.32), "Plastic", 0.001)
    # neck, fretboard, frets, headstock
    box("Neck", gt, (-0.022, -0.030, 0.50), (0.022, -0.004, 1.03), "Walnut", 0.004)
    box("Fretboard", gt, (-0.024, -0.036, 0.50), (0.024, -0.030, 1.03), "Plastic", 0.002)
    for i in range(14):
        z = 0.53 + (1.03 - 0.53) * (1 - 0.9 ** (i + 1)) / (1 - 0.9 ** 14) * 0.98
        box("Fret %d" % i, gt, (-0.025, -0.0385, z), (0.025, -0.0355, z + 0.003), "Stainless", 0.0005)
    box("Headstock", gt, (-0.036, -0.030, 1.03), (0.036, -0.006, 1.16), "Walnut", 0.005)
    for i in range(3):
        for s in (-1, 1):
            cyl("Peg %d%d" % (i, s), gt, (s * 0.048, -0.018, 1.06 + i * 0.038), 0.006, 0.026, "Stainless", axis='X', seg=16)
    # strings
    for i in range(6):
        x = -0.019 + i * 0.0076
        box("String %d" % i, gt, (x - 0.0006, -0.0645, 0.172), (x + 0.0006, -0.0395, 1.07), "Stainless", 0.0)
    # name the guitar parts, then tilt the guitar back 10 degrees and lift it onto the stand (baked into the parts: turned empties export badly)
    parts = list(rt.children)
    for ch in parts:
        ch.name = "G_" + ch.name
    bpy.context.view_layer.update()
    M = Matrix.Translation((0, 0.05, 0.33)) @ Matrix.Rotation(math.radians(-10), 4, 'X')
    for ch in parts:
        ch.matrix_world = M @ ch.matrix_world
    # the stand: a tripod base, a rear post, a cradle for the body and a yoke for the neck
    st = rt
    for k in range(3):
        a = math.radians(90 + k * 120)
        p = Vector((math.cos(a) * 0.22, 0.10 + math.sin(a) * 0.22, 0.012))
        leg = cyl("Leg %d" % k, st, (p.x / 2, (0.10 + p.y) / 2, 0.012), 0.011, 0.24, "BlackSteel", axis='Y', seg=16)
        leg.rotation_euler = (0, 0, math.atan2(p.y - 0.10, p.x) - math.pi / 2)
        cyl("Foot %d" % k, st, (p.x, p.y, 0.008), 0.016, 0.016, "Rubber" if False else "Plastic", axis='Z', seg=16)
    cyl("Post", st, (0, 0.10, 0.42), 0.012, 0.84, "BlackSteel", axis='Z', seg=16)
    box("Cradle back", st, (-0.13, 0.03, 0.30), (0.13, 0.11, 0.33), "BlackSteel", 0.004)
    box("Cradle left", st, (-0.135, -0.07, 0.30), (-0.115, 0.11, 0.42), "BlackSteel", 0.004)
    box("Cradle right", st, (0.115, -0.07, 0.30), (0.135, 0.11, 0.42), "BlackSteel", 0.004)
    box("Cradle pad", st, (-0.12, -0.06, 0.325), (0.12, 0.10, 0.335), "Plastic", 0.003)
    box("Yoke bar", st, (-0.06, 0.02, 0.86), (0.06, 0.12, 0.88), "BlackSteel", 0.004)
    box("Yoke left", st, (-0.065, -0.04, 0.80), (-0.05, 0.12, 0.90), "BlackSteel", 0.004)
    box("Yoke right", st, (0.05, -0.04, 0.80), (0.065, 0.12, 0.90), "BlackSteel", 0.004)
    return rt


def build_synth():
    rt = root("synth")
    # X stand
    for s in (-1, 1):
        x = s * 0.34
        for sign in (-1, 1):
            leg = cyl("Leg", rt, (x, 0, 0.42), 0.012, 0.96, "BlackSteel", axis='Z', seg=16)
            leg.rotation_euler = (math.radians(sign * 24), 0, 0)
        box("Foot", rt, (x - 0.02, -0.26, 0.0), (x + 0.02, 0.26, 0.02), "BlackSteel", 0.004)
        box("Rest", rt, (x - 0.03, -0.20, 0.79), (x + 0.03, 0.20, 0.81), "BlackSteel", 0.004)
    box("Cross bar", rt, (-0.34, -0.012, 0.42), (0.34, 0.012, 0.44), "BlackSteel", 0.003)
    # the keyboard: a body, side panels, keys, a control panel with knobs and a little screen
    L, D, top = 1.02, 0.36, 0.81
    box("Body", rt, (-L / 2, -D / 2, top), (L / 2, D / 2, top + 0.075), "TVBlack", 0.01)
    box("Left side", rt, (-L / 2 - 0.01, -D / 2, top - 0.005), (-L / 2 + 0.03, D / 2, top + 0.085), "Walnut", 0.008)
    box("Right side", rt, (L / 2 - 0.03, -D / 2, top - 0.005), (L / 2 + 0.01, D / 2, top + 0.085), "Walnut", 0.008)
    n_white = 36
    w = (L - 0.08) / n_white
    x0 = -L / 2 + 0.04
    zt = top + 0.078
    for i in range(n_white):
        x = x0 + i * w
        box("Key %d" % i, rt, (x + 0.0012, -D / 2 - 0.004, zt), (x + w - 0.0012, -D / 2 + 0.145, zt + 0.014), "PlasticWhite", 0.002)
    black_at = [0, 1, 3, 4, 5]        # after these white keys within each octave of seven
    for i in range(n_white - 1):
        if (i % 7) in black_at:
            x = x0 + (i + 1) * w
            box("Black %d" % i, rt, (x - w * 0.3, -D / 2 + 0.05, zt + 0.010), (x + w * 0.3, -D / 2 + 0.145, zt + 0.032), "Plastic", 0.002)
    # panel
    box("Panel", rt, (-L / 2 + 0.04, -D / 2 + 0.16, top + 0.074), (L / 2 - 0.04, D / 2 - 0.01, top + 0.082), "Plastic", 0.002)
    box("Screen", rt, (-0.10, -D / 2 + 0.20, top + 0.0815), (0.12, D / 2 - 0.05, top + 0.0845), "Screen", 0.0)
    for i in range(6):
        cyl("Knob", rt, (-0.42 + i * 0.05, -D / 2 + 0.24, top + 0.092), 0.012, 0.018, "Stainless", axis='Z', seg=20)
    for i in range(4):
        cyl("Slider", rt, (0.30 + i * 0.05, -D / 2 + 0.235, top + 0.086), 0.006, 0.01, "Stainless", axis='Z', seg=12)
    # a music stand
    box("Music stand post", rt, (-0.012, D / 2 - 0.05, top + 0.075), (0.012, D / 2 - 0.03, top + 0.42), "BlackSteel", 0.003)
    sheet = box("Music stand rest", rt, (-0.20, D / 2 - 0.06, top + 0.36), (0.20, D / 2 - 0.045, top + 0.60), "BlackSteel", 0.003)
    sheet.rotation_euler = (math.radians(-16), 0, 0)
    return rt


fresh()
guitar = build_guitar()
synth = build_synth()
synth.location = (0, 0, 0)
guitar.location = (0, 0, 0)
for rt, fid in ((guitar, "guitar"), (synth, "synth")):
    export(rt, fid)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT, "Blender", "furniture_music.blend"))
def _tris(rt):
    n = 0
    for ch in rt.children_recursive:
        if ch.type == "MESH":
            n += sum(len(p.vertices) - 2 for p in ch.data.polygons)
    return n


RESULT = "guitar %d tris, synth %d tris" % (_tris(guitar), _tris(synth))
print(RESULT)
