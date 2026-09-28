"""Builds the kitchen furniture in Blender and exports one FBX per piece.

Run it inside Blender (Blender MCP: exec(open(path).read())). It recreates the small toolkit from
docs/PIPELINE.md (part, box_uv, rounded boxes, cylinders), clears its own collection, builds every
piece at real size and exports to Assets/Art/Models/<id>.fbx. The scene is saved as
Blender/furniture_kitchen.blend.

Origin of every piece: centre of the footprint on the floor. Front faces -Y (arrives facing +Z in Unity).
"""
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

PROJECT = r"S:\Dearlife by Zetazuni"
MODELS = os.path.join(PROJECT, "Assets", "Art", "Models")

# Blender slot name -> preview colour (real looks live in FurnitureImport.Looks)
COLORS = {
    "Marble_main": (0.93, 0.92, 0.9), "Cabinet_main": (0.13, 0.135, 0.145), "Walnut": (0.35, 0.22, 0.13),
    "BlackSteel": (0.03, 0.03, 0.035), "Stainless": (0.7, 0.7, 0.72), "Brass": (0.75, 0.55, 0.25),
    "Fabric_main": (0.85, 0.8, 0.7), "GlassDark": (0.02, 0.025, 0.03), "Bulb": (1.0, 0.9, 0.7),
}
_mats = {}


def mat(name):
    if name not in _mats:
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.diffuse_color = (*COLORS.get(name, (0.7, 0.7, 0.7)), 1)
        _mats[name] = m
    return _mats[name]


def fresh():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)


def root(name):
    e = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(e)
    return e


def _uv(bm):
    """Box mapping in metres: each face uses the two axes across its dominant normal."""
    layer = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            p = l.vert.co
            l[layer].uv = (p.y, p.z) if ax == 0 else (p.x, p.z) if ax == 1 else (p.x, p.y)


def _finish(name, parent, bm, material, bevel, smooth=True):
    _uv(bm)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    me.materials.append(mat(material))
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    if bevel > 0:
        b = ob.modifiers.new("Bevel", 'BEVEL')
        b.width = bevel
        b.segments = 4
        b.limit_method = 'ANGLE'
        b.angle_limit = math.radians(35)
        b.harden_normals = False
    return ob


def box(name, parent, lo, hi, material, bevel=0.006):
    """Axis aligned box from corner lo to corner hi (metres, piece space)."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    c = [(lo[i] + hi[i]) / 2 for i in range(3)]
    s = [max(hi[i] - lo[i], 0.001) for i in range(3)]
    for v in bm.verts:
        v.co = Vector((v.co.x * s[0] + c[0], v.co.y * s[1] + c[1], v.co.z * s[2] + c[2]))
    bm.normal_update()
    return _finish(name, parent, bm, material, min(bevel, min(s) * 0.45))


def cyl(name, parent, center, r, h, material, axis='Z', r2=None, seg=48, bevel=0.004):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h)
    rot = {'Z': Matrix.Identity(4), 'X': Matrix.Rotation(math.radians(90), 4, 'Y'), 'Y': Matrix.Rotation(math.radians(90), 4, 'X')}[axis]
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ rot, verts=bm.verts)
    bm.normal_update()
    return _finish(name, parent, bm, material, min(bevel, r * 0.4, h * 0.4))


def torus(name, parent, center, R, r, material, seg=64, rings=16, axis='Z'):
    """Ring round the given axis ('Z' lies flat, 'X' stands up like a wheel), centred at `center`."""
    bm = bmesh.new()
    for i in range(seg):
        a = 2 * math.pi * i / seg
        for j in range(rings):
            b = 2 * math.pi * j / rings
            bm.verts.new(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b)))
    bm.verts.ensure_lookup_table()
    for i in range(seg):
        for j in range(rings):
            a = bm.verts[i * rings + j]; b = bm.verts[i * rings + (j + 1) % rings]
            c = bm.verts[((i + 1) % seg) * rings + (j + 1) % rings]; d = bm.verts[((i + 1) % seg) * rings + j]
            bm.faces.new((a, d, c, b))
    rot = Matrix.Rotation(math.radians(90), 4, 'Y') if axis == 'X' else Matrix.Identity(4)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ rot, verts=bm.verts)
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _finish(name, parent, bm, material, 0)


def export(rt, fid):
    bpy.ops.object.select_all(action='DESELECT')
    rt.select_set(True)
    for ch in rt.children_recursive:
        ch.select_set(True)
    bpy.context.view_layer.objects.active = rt
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(MODELS, fid + ".fbx"), use_selection=True, object_types={'EMPTY', 'MESH'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE',
        add_leaf_bones=False, bake_anim=False, path_mode='COPY', embed_textures=False)


def stats(rt):
    depsg = bpy.context.evaluated_depsgraph_get()
    tris = 0
    for ch in rt.children_recursive:
        e = ch.evaluated_get(depsg)
        tris += sum(len(p.vertices) - 2 for p in e.to_mesh().polygons)
    return tris


# ---------------------------------------------------------------- pieces (toolkit ends above this line)

def handle(name, parent, x, y, z, length, vertical=True):
    """Slim brass bar handle standing off the door face (front is -Y)."""
    if vertical:
        cyl(name, parent, (x, y - 0.03, z), 0.006, length, "Brass", 'Z', seg=16, bevel=0.001)
    else:
        cyl(name, parent, (x, y - 0.03, z), 0.006, length, "Brass", 'X', seg=16, bevel=0.001)
    for dz in ((-length / 2 + 0.03, length / 2 - 0.03) if vertical else (0,)):
        pass
    if vertical:
        for zz in (z - length / 2 + 0.03, z + length / 2 - 0.03):
            cyl(name + " post", parent, (x, y - 0.0125, zz), 0.005, 0.025, "Brass", 'Y', seg=12, bevel=0.0005)
    else:
        for xx in (x - length / 2 + 0.03, x + length / 2 - 0.03):
            cyl(name + " post", parent, (xx, y - 0.0125, z), 0.005, 0.025, "Brass", 'Y', seg=12, bevel=0.0005)


def kitchenrun():
    rt = root("kitchenrun")
    W, D = 4.4, 0.62                    # width along X, depth (back at +D/2)
    x0, x1 = -W / 2, W / 2
    yb, yf = D / 2, -D / 2              # back and front
    PL, BH, WT = 0.1, 0.86, 0.04        # plinth, cabinet height, worktop thickness
    top = BH + WT                        # 0.9 m
    gap = 0.004

    box("plinth", rt, (x0, yf + 0.06, 0), (x1 - 0.7, yb, PL), "BlackSteel", 0.002)
    # base units left to right: drawers 0.8, sink 1.0, drawers 0.9, cooktop 1.0
    units = [("drawers", 0.8), ("sink", 1.0), ("drawers", 0.9), ("cooktop", 1.0)]
    x = x0
    for i, (kind, w) in enumerate(units):
        a, b = x, x + w
        box(f"carcass {i+1}", rt, (a, yb - 0.56, PL), (b, yb, BH), "Cabinet_main", 0.003)
        if kind == "sink":
            # two doors under the sink
            for k in range(2):
                da = a + gap + k * w / 2
                box(f"sink door {i+1}.{k}", rt, (da, yf, PL + 0.02), (da + w / 2 - gap * 2, yf + 0.02, BH - 0.01), "Cabinet_main", 0.004)
                handle(f"handle {i+1}.{k}", rt, da + (w / 2 - 0.05 if k == 0 else 0.05), yf, BH - 0.2, 0.28)
        else:
            # three stacked drawers
            hs = [0.2, 0.28, 0.32]
            z = PL + 0.02
            for k, h in enumerate(hs):
                box(f"drawer {i+1}.{k}", rt, (a + gap, yf, z), (b - gap, yf + 0.02, z + h - gap), "Cabinet_main", 0.004)
                handle(f"drawer bar {i+1}.{k}", rt, (a + b) / 2, yf, z + h - 0.05, w * 0.6, vertical=False)
                z += h
        x = b
    # worktop, marble, 2 cm overhang at the front
    box("worktop", rt, (x0 - 0.005, yf - 0.02, BH), (x1 - 0.7 - 0.005 + 0.005, yb, top), "Marble_main", 0.004)
    box("backsplash", rt, (x0, yb - 0.02, top), (x1 - 0.7, yb, top + 0.62), "Marble_main", 0.003)

    # sink: stainless rim + dark basin + tap
    sx = x0 + 0.8 + 0.5
    box("sink rim", rt, (sx - 0.4, -0.16, top), (sx + 0.4, 0.14, top + 0.004), "Stainless", 0.002)
    box("sink basin", rt, (sx - 0.36, -0.13, top + 0.004), (sx + 0.36, 0.11, top + 0.006), "GlassDark", 0.001)
    cyl("tap base", rt, (sx, 0.2, top + 0.02), 0.025, 0.04, "BlackSteel", 'Z', seg=32, bevel=0.003)
    cyl("tap neck", rt, (sx, 0.2, top + 0.2), 0.012, 0.32, "BlackSteel", 'Z', seg=24, bevel=0.002)
    cyl("tap spout", rt, (sx, 0.14, top + 0.34), 0.011, 0.14, "BlackSteel", 'Y', seg=24, bevel=0.002)

    # cooktop: black glass plate with four rings
    cx = x0 + 0.8 + 1.0 + 0.9 + 0.5
    box("cooktop glass", rt, (cx - 0.3, -0.24, top), (cx + 0.3, 0.2, top + 0.008), "GlassDark", 0.003)
    for k, (dx, dy, r) in enumerate([(-0.15, -0.11, 0.09), (0.15, -0.11, 0.07), (-0.15, 0.09, 0.07), (0.15, 0.09, 0.09)]):
        torus(f"hob ring {k+1}", rt, (cx + dx, dy, top + 0.009), r, 0.0025, "Stainless", 48, 8)

    # hood over the cooktop: slim canopy and a chimney to the ceiling
    box("hood canopy", rt, (cx - 0.45, yb - 0.5, 1.72), (cx + 0.45, yb, 1.82), "Stainless", 0.006)
    box("hood glass", rt, (cx - 0.44, yb - 0.5, 1.71), (cx + 0.44, yb - 0.46, 1.72), "GlassDark", 0.002)
    box("hood chimney", rt, (cx - 0.2, yb - 0.25, 1.82), (cx + 0.2, yb, 3.0), "Stainless", 0.004)

    # wall units on the left half
    for k, (a, b) in enumerate([(x0, x0 + 0.9), (x0 + 0.9, x0 + 1.8)]):
        box(f"wall carcass {k+1}", rt, (a, yb - 0.34, 1.45), (b, yb, 2.2), "Cabinet_main", 0.003)
        # the door stands in front of the carcass (front at yb - 0.34), not inside it, or the two faces fight and flicker
        box(f"wall door {k+1}", rt, (a + gap, yb - 0.36, 1.45 + gap), (b - gap, yb - 0.335, 2.2 - gap), "Walnut", 0.004)
        handle(f"wall handle {k+1}", rt, b - 0.06, yb - 0.36, 1.6, 0.25)

    # oven tower at the right end
    tx0, tx1 = x1 - 0.7, x1
    box("tower carcass", rt, (tx0, yb - 0.6, PL), (tx1, yb, 2.3), "Cabinet_main", 0.003)
    box("tower plinth", rt, (tx0, yf + 0.06, 0), (tx1, yb, PL), "BlackSteel", 0.002)
    box("tower door low", rt, (tx0 + gap, yf, PL + 0.02), (tx1 - gap, yf + 0.02, 0.62), "Cabinet_main", 0.004)
    handle("tower handle low", rt, (tx0 + tx1) / 2, yf, 0.55, 0.45, vertical=False)
    for k, (za, zb) in enumerate([(0.7, 1.3), (1.34, 1.94)]):
        box(f"oven frame {k+1}", rt, (tx0 + 0.03, yf, za), (tx1 - 0.03, yf + 0.03, zb), "Stainless", 0.004)
        box(f"oven glass {k+1}", rt, (tx0 + 0.07, yf - 0.002, za + 0.12), (tx1 - 0.07, yf + 0.004, zb - 0.05), "GlassDark", 0.002)
        handle(f"oven bar {k+1}", rt, (tx0 + tx1) / 2, yf, zb - 0.06, 0.5, vertical=False)
    box("tower door top", rt, (tx0 + gap, yf, 1.98), (tx1 - gap, yf + 0.02, 2.28), "Cabinet_main", 0.004)
    return rt


def fridge():
    rt = root("fridge")
    W, D, H = 0.85, 0.72, 1.9
    box("body", rt, (-W / 2, -D / 2 + 0.03, 0.03), (W / 2, D / 2, H), "Stainless", 0.01)
    box("plinth", rt, (-W / 2 + 0.02, -D / 2 + 0.08, 0), (W / 2 - 0.02, D / 2 - 0.02, 0.05), "BlackSteel", 0.002)
    box("door freezer", rt, (-W / 2 + 0.006, -D / 2, 0.08), (W / 2 - 0.006, -D / 2 + 0.05, 0.62), "Stainless", 0.012)
    box("door fridge", rt, (-W / 2 + 0.006, -D / 2, 0.64), (W / 2 - 0.006, -D / 2 + 0.05, H - 0.02), "Stainless", 0.012)
    cyl("handle fridge", rt, (-W / 2 + 0.05, -D / 2 - 0.035, 1.3), 0.011, 0.7, "BlackSteel", 'Z', seg=24, bevel=0.002)
    cyl("handle freezer", rt, (-W / 2 + 0.05, -D / 2 - 0.035, 0.36), 0.011, 0.35, "BlackSteel", 'Z', seg=24, bevel=0.002)
    for zz, n in ((1.55, "a"), (1.05, "b")):
        cyl("handle post " + n, rt, (-W / 2 + 0.05, -D / 2 - 0.015, zz), 0.006, 0.03, "BlackSteel", 'Y', seg=12, bevel=0.0005)
    return rt


def kitchenisland():
    rt = root("kitchenisland")
    L, D, H = 2.6, 1.0, 0.92
    # cabinet body on the back side (+Y), walnut slats on the seating side (-Y)
    box("plinth", rt, (-L / 2 + 0.05, -0.32, 0), (L / 2 - 0.05, 0.42, 0.1), "BlackSteel", 0.002)
    box("body", rt, (-L / 2 + 0.03, -0.3, 0.1), (L / 2 - 0.03, 0.5 - 0.02, H - 0.05), "Cabinet_main", 0.004)
    # waterfall marble: top slab and two side panels that run to the floor
    box("top", rt, (-L / 2, -0.5, H - 0.05), (L / 2, 0.5, H), "Marble_main", 0.006)
    for s, n in ((-1, "left"), (1, "right")):
        xa, xb = (-L / 2, -L / 2 + 0.05) if s < 0 else (L / 2 - 0.05, L / 2)
        box(f"waterfall {n}", rt, (xa, -0.5, 0), (xb, 0.5, H - 0.05), "Marble_main", 0.006)
    # slatted walnut panel under the seating overhang
    for i in range(24):
        x = -L / 2 + 0.09 + i * (L - 0.18) / 23
        box(f"slat {i+1}", rt, (x - 0.02, -0.34, 0.1), (x + 0.02, -0.3, H - 0.06), "Walnut", 0.003)
    # doors and drawers on the back side (+Y, the kitchen side)
    for i in range(4):
        a = -L / 2 + 0.06 + i * 0.62
        box(f"door {i+1}", rt, (a + 0.003, 0.5, 0.12), (a + 0.617, 0.52, H - 0.07), "Cabinet_main", 0.004)
        cyl(f"pull {i+1}", rt, (a + 0.31, 0.545, H - 0.15), 0.006, 0.3, "Brass", 'X', seg=16, bevel=0.001)
    # cooking sized hob-less prep zone: a small steel sink insert is left out on purpose
    return rt


def barstool():
    rt = root("barstool")
    cyl("seat cushion", rt, (0, 0, 0.66), 0.19, 0.06, "Fabric_main", seg=48, bevel=0.02)
    cyl("seat base", rt, (0, 0, 0.62), 0.185, 0.02, "BlackSteel", seg=48, bevel=0.004)
    for i in range(4):
        a = math.radians(45 + 90 * i)
        # splayed legs from the seat edge down to the floor
        top = Vector((0.13 * math.cos(a), 0.13 * math.sin(a), 0.61))
        bot = Vector((0.2 * math.cos(a), 0.2 * math.sin(a), 0.0))
        mid = (top + bot) / 2
        ln = (top - bot).length
        c = cyl(f"leg {i+1}", rt, (0, 0, 0), 0.013, ln, "BlackSteel", seg=20, bevel=0.002)
        d = (top - bot).normalized()
        rot = Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4()
        c.matrix_world = Matrix.Translation(mid) @ rot
    torus("foot ring", rt, (0, 0, 0.27), 0.17, 0.007, "Brass", 48, 10)
    return rt


def diningtable():
    rt = root("diningtable")
    L, W, H = 1.9, 0.95, 0.75
    box("top", rt, (-L / 2, -W / 2, H - 0.045), (L / 2, W / 2, H), "Walnut", 0.02)
    box("apron", rt, (-L / 2 + 0.12, -W / 2 + 0.08, H - 0.1), (L / 2 - 0.12, W / 2 - 0.08, H - 0.045), "BlackSteel", 0.004)
    for sx in (-1, 1):
        for sy in (-1, 1):
            c = cyl(f"leg {sx}{sy}", rt, (sx * (L / 2 - 0.13), sy * (W / 2 - 0.11), (H - 0.045) / 2), 0.03, H - 0.045, "BlackSteel", r2=0.019, seg=32, bevel=0.002)
    return rt


def diningchair():
    """Walnut framed chair: padded seat, four slightly tapered legs, rear posts flush with a curved back pad."""
    rt = root("diningchair")
    SH = 0.46                                   # seat top
    box("seat pad", rt, (-0.225, -0.215, SH - 0.075), (0.225, 0.215, SH), "Fabric_main", 0.03)
    box("seat frame", rt, (-0.21, -0.2, SH - 0.11), (0.21, 0.2, SH - 0.075), "Walnut", 0.006)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * 0.19, sy * 0.185
            if sy > 0:      # rear leg keeps going up as the back post
                box(f"rear post {sx}", rt, (x - 0.017, y - 0.017, 0), (x + 0.017, y + 0.017, 0.84), "Walnut", 0.008)
            else:
                cyl(f"front leg {sx}", rt, (x, y, (SH - 0.11) / 2), 0.019, SH - 0.11, "Walnut", r2=0.012, seg=24, bevel=0.002)
    # curved back pad between the posts
    n, half = 20, 0.19
    z0, z1 = 0.56, 0.82
    bm = bmesh.new()
    for i in range(n + 1):
        x = -half + 2 * half * i / n
        y = 0.185 + 0.035 * (1 - (x / half) ** 2)
        for (yy, zz) in ((y - 0.02, z0), (y + 0.02, z0), (y - 0.02, z1), (y + 0.02, z1)):
            bm.verts.new((x, yy, zz))
    bm.verts.ensure_lookup_table()
    def V(i, k): return bm.verts[i * 4 + k]
    for i in range(n):
        bm.faces.new((V(i, 0), V(i + 1, 0), V(i + 1, 2), V(i, 2)))   # front
        bm.faces.new((V(i, 1), V(i, 3), V(i + 1, 3), V(i + 1, 1)))   # back
        bm.faces.new((V(i, 2), V(i + 1, 2), V(i + 1, 3), V(i, 3)))   # top
        bm.faces.new((V(i, 0), V(i, 1), V(i + 1, 1), V(i + 1, 0)))   # bottom
    bm.faces.new((V(0, 0), V(0, 2), V(0, 3), V(0, 1)))
    bm.faces.new((V(n, 0), V(n, 1), V(n, 3), V(n, 2)))
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    _finish("back pad", rt, bm, "Fabric_main", 0.012)
    return rt


def pendant():
    rt = root("pendant")
    top = 2.98
    cyl("canopy", rt, (0, 0, top - 0.015), 0.055, 0.03, "BlackSteel", seg=32, bevel=0.004)
    cyl("cord", rt, (0, 0, top - 0.5), 0.004, 0.97, "BlackSteel", seg=12, bevel=0.0005)
    # dome shade: a lathe of a quarter arc, brass outside
    bm = bmesh.new()
    prof = [(0.05, 2.18), (0.07, 2.2), (0.16, 2.14), (0.24, 2.02), (0.27, 1.94), (0.28, 1.92)]
    seg = 48
    rings = []
    for (r, z) in prof:
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * s / seg), r * math.sin(2 * math.pi * s / seg), z)) for s in range(seg)])
    for k in range(len(rings) - 1):
        for s in range(seg):
            bm.faces.new((rings[k][s], rings[k][(s + 1) % seg], rings[k + 1][(s + 1) % seg], rings[k + 1][s]))
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    shade = _finish("shade", rt, bm, "Brass", 0)
    sm = shade.modifiers.new("Solidify", 'SOLIDIFY'); sm.thickness = 0.004; sm.offset = -1
    ss = shade.modifiers.new("Subsurf", 'SUBSURF'); ss.levels = 2; ss.render_levels = 2
    bulb = bmesh.new()
    bmesh.ops.create_uvsphere(bulb, u_segments=32, v_segments=16, radius=0.07)
    bmesh.ops.translate(bulb, vec=(0, 0, 1.98), verts=bulb.verts)
    _finish("bulb", rt, bulb, "Bulb", 0)
    return rt


PIECES = [kitchenrun, fridge, kitchenisland, barstool, diningtable, diningchair, pendant]


def build_all():
    fresh()
    out = {}
    for fn in PIECES:
        rt = fn()
        out[rt.name] = stats(rt)
        export(rt, rt.name)
        # hide finished piece so the next one starts from a clean view; kept in the file for later edits
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT, "Blender", "furniture_kitchen.blend"))
    return out


if globals().get("RUN_KITCHEN", True):
    RESULT = build_all()
    print(RESULT)
