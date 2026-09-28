"""Realistic pets (phase 6 of docs/CHARACTER_PLAN.md): Bedah the cat and the dog, from two CC BY Sketchfab models.

Sources (downloaded once, kept outside the repo in S:\\Tools\\pets, credits in Assets/Art/Models/Characters/CREDITS.txt):
- cat_source.blend: "An Animated Cat" by Evil_Katz (63 bone rig, black fur, fur cards for tufts and whiskers)
- dog_source.blend: "Animated Dog Sits Rolls Over Shake Paw" by LasquetiSpice (a Shiba Inu, 122 bone rig)

Each is flattened (no parent empties, no animation, rest pose, scale 1), turned to face -Y like our other characters,
sized, stood on the floor, its bones given clean names (CharacterRig finds them by name), and exported with
blender_rig.export_character. The cat is repainted as a calico (white with orange and black patches, a black tail and
one black ear) by rasterising its UV layout with the 3D position of every texel, keeping the fur detail of the original.

Run in Blender (Blender MCP):  exec(open(r"S:\\Dearlife by Zetazuni\\tools\\blender_pets.py").read())
then Dearlife > Import furniture in Unity.
"""
import bpy, os, re, sys, math
import numpy as np
from mathutils import Matrix

TOOLS = r"S:\Dearlife by Zetazuni\tools"
if TOOLS not in sys.path:
    sys.path.insert(0, TOOLS)
import importlib, blender_rig
importlib.reload(blender_rig)
from blender_rig import export_character, clear_scene, OUT

SRC = r"S:\Tools\pets"


def load(blend):
    clear_scene()
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    # a clean start, so materials keep their names (Fur, not Fur.004) on every run
    for coll in (bpy.data.materials, bpy.data.images, bpy.data.meshes, bpy.data.armatures, bpy.data.actions):
        for x in list(coll):
            coll.remove(x)
    with bpy.data.libraries.load(blend) as (src, dst):
        dst.objects = src.objects
    objs = [o for o in dst.objects if o]
    for o in objs:
        bpy.context.scene.collection.objects.link(o)
    arm = next(o for o in objs if o.type == 'ARMATURE')
    meshes = [o for o in objs if o.type == 'MESH' and any(m.type == 'ARMATURE' for m in o.modifiers)]
    for o in objs:
        if o.type == 'MESH' and o not in meshes:
            bpy.data.objects.remove(o, do_unlink=True)
    return arm, meshes


def flatten(arm, meshes, turn_deg, height):
    """Rest pose, no parents, identity transforms, facing -Y, `height` metres tall, feet on z 0, centred."""
    arm.animation_data_clear()
    for pb in arm.pose.bones:
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()
    for o in [arm] + meshes:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
        if o.data.users > 1:
            o.data = o.data.copy()
    for o in list(bpy.data.objects):
        if o.type == 'EMPTY':
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.context.view_layer.update()

    def apply(objs, m):
        for o in objs:
            o.matrix_world = m @ o.matrix_world
        bpy.context.view_layer.update()
        for o in objs:
            bpy.ops.object.select_all(action='DESELECT')
            o.select_set(True)
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    objs = [arm] + meshes
    apply(objs, Matrix.Rotation(math.radians(turn_deg), 4, 'Z'))
    pts = [v.co for m in meshes for v in m.data.vertices]
    lo = [min(p[i] for p in pts) for i in range(3)]
    hi = [max(p[i] for p in pts) for i in range(3)]
    s = height / (hi[2] - lo[2])
    centre = Matrix.Translation((-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]))
    apply(objs, Matrix.Scale(s, 4) @ centre)
    for m in meshes:
        m.parent = arm
        m.matrix_parent_inverse = Matrix.Identity(4)
        for md in m.modifiers:
            if md.type == 'ARMATURE':
                md.object = arm
    return s


def clean_bone_names(arm):
    """Sketchfab's glTF adds numbers to every bone (j_l_elbow_032_43, L_elbow_jnt.106_104): take them off."""
    for b in arm.data.bones:
        n = re.sub(r'\.\d+_\d+$', '', b.name)
        n = re.sub(r'_\d+_\d+$', '', n)
        n = re.sub(r'^_', '', n)
        if n.endswith('rootJoint'):
            n = 'root_joint' if b.parent is None else 'root_joint2'
        b.name = n


def world_bone(arm, name):
    return arm.matrix_world @ arm.data.bones[name].head_local


# ---------------------------------------------------------------- the calico coat

def texel_positions(mesh, size):
    """For every texel covered by the mesh's UV layout: the 3D rest position there (NaN where nothing is)."""
    me = mesh.data
    me.calc_loop_triangles()
    uv = me.uv_layers.active.data
    P = np.full((size, size, 3), np.nan, np.float32)
    co = np.array([v.co[:] for v in me.vertices], np.float32)
    for t in me.loop_triangles:
        uvs = np.array([uv[l].uv[:] for l in t.loops], np.float32) * size
        ps = co[list(t.vertices)]
        x0, y0 = np.floor(uvs.min(0) - 1).astype(int)
        x1, y1 = np.ceil(uvs.max(0) + 1).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0)
        x1, y1 = min(x1, size - 1), min(y1, size - 1)
        if x1 < x0 or y1 < y0:
            continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        a, b, c = uvs
        d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(d) < 1e-9:
            continue
        w0 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / d
        w1 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / d
        w2 = 1 - w0 - w1
        inside = (w0 >= -0.03) & (w1 >= -0.03) & (w2 >= -0.03)   # a little over the edge, so seams do not show
        if not inside.any():
            continue
        pos = w0[..., None] * ps[0] + w1[..., None] * ps[1] + w2[..., None] * ps[2]
        sub = P[y0:y1 + 1, x0:x1 + 1]
        empty = np.isnan(sub[..., 0]) & inside
        sub[empty] = pos[empty]
    return P


def box_blur(a, r):
    k = 2 * r + 1
    p = np.pad(a, ((r + 1, r), (r + 1, r)), mode='edge')
    c = p.cumsum(0).cumsum(1)
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)


def calico(arm, mesh, image, out_name):
    size = image.size[0]
    src = np.array(image.pixels[:], np.float32).reshape(size, size, 4)
    P = texel_positions(mesh, size)
    covered = ~np.isnan(P[..., 0])
    x, y, z = P[..., 0], P[..., 1], P[..., 2]   # the cat faces -Y: y is along the body (nose at the lowest y)
    x = np.nan_to_num(x); y = np.nan_to_num(y); z = np.nan_to_num(z)
    head = world_bone(arm, 'j_head'); hips = world_bone(arm, 'j_hips'); tail1 = world_bone(arm, 'j_tail_1')
    L = head[1] - hips[1]                         # negative: from the hips to the head
    H = head[2]

    def warp(u, v, w):                            # ragged patch edges
        return (0.022 * (np.sin(u * 61 + 1.3) * np.sin(v * 47 + 0.4) + np.sin(w * 71 + 2.1) * np.cos(u * 39 + v * 23))
                + 0.008 * np.sin(u * 190 + v * 170 + w * 150)) * (H / 0.3)

    def blob(cx, cy, cz, r):
        d = np.sqrt((x - cx) ** 2 + (y - cy) ** 2 + (z - cz) ** 2) + warp(x, y, z)
        return np.clip((r - d) / (0.012 * H / 0.3) + 0.5, 0, 1)

    def at(fx, f_along, fz):                      # places in body terms: across (+ is the cat's left), along (0 hips, 1 head), up
        return hips[0] + fx * H, hips[1] + f_along * L, fz * H

    orange = np.zeros_like(x); black = np.zeros_like(x)
    for fx, fa, fz, r in [(0.2, 0.45, 1.0, 0.3), (-0.3, 0.75, 0.92, 0.22), (0.3, 0.05, 0.9, 0.22), (0.12, 1.1, 1.12, 0.12), (-0.3, 0.22, 0.82, 0.16), (0.32, 0.8, 0.78, 0.12)]:
        orange = np.maximum(orange, blob(*at(fx, fa, fz), r * H))
    for fx, fa, fz, r in [(-0.22, 0.48, 0.98, 0.22), (0.22, 0.72, 0.95, 0.16), (-0.06, 0.08, 1.02, 0.17), (-0.13, 1.12, 1.12, 0.1), (0.34, 0.3, 0.74, 0.11), (-0.35, 0.95, 0.8, 0.09)]:
        black = np.maximum(black, blob(*at(fx, fa, fz), r * H))
    # the belly, chest, legs and muzzle stay white: patches only on the back and the upper sides
    upper = np.clip((z - 0.55 * H) / (0.1 * H), 0, 1)
    orange *= upper; black *= upper
    # a black tail and one black ear (the right one)
    tail = np.clip((y - (tail1[1] + 0.07 * H)) / (0.04 * H), 0, 1) * np.clip((z - 0.5 * H) / (0.1 * H), 0, 1)
    ear = np.clip((z - (head[2] + 0.07 * H)) / (0.02 * H), 0, 1) * (x < head[0] - 0.02 * H)
    black = np.maximum(black, np.maximum(tail, ear))
    orange *= 1 - black

    lum = src[..., :3] @ np.array([0.3, 0.59, 0.11], np.float32)
    sat = src[..., :3].max(-1) - src[..., :3].min(-1)
    fur = np.clip((0.12 - sat) / 0.06, 0, 1) * covered   # leaves the tongue, gums, paw pads and eyes alone
    detail = np.clip(lum / np.maximum(box_blur(lum, 14), 0.02), 0.2, 1.6)
    white = np.array([0.93, 0.9, 0.85], np.float32)
    ginger = np.array([0.86, 0.44, 0.14], np.float32)
    ink = np.array([0.055, 0.05, 0.045], np.float32)
    coat = white * (1 - orange - black)[..., None] + ginger * orange[..., None] + ink * black[..., None]
    coat = coat * detail[..., None] ** np.where(black > 0.5, 1.0, 0.7)[..., None]
    out = src.copy()
    out[..., :3] = src[..., :3] * (1 - fur[..., None]) + np.clip(coat, 0, 1) * fur[..., None]
    img = bpy.data.images.new(out_name, size, size, alpha=True)
    img.pixels[:] = out.ravel()
    return img


# ---------------------------------------------------------------- the two pets

def material_image(mat, kind='Base Color'):
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    def up(sock):
        for l in sock.links:
            n = l.from_node
            if n.type == 'TEX_IMAGE':
                return n
            for i in n.inputs:
                r = up(i)
                if r:
                    return r
        return None
    return up(bsdf.inputs[kind])


def only_texture(mat, img):
    """export_character takes the first image node of a material: make it the one we want."""
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    for n in list(nt.nodes):
        if n.type == 'TEX_IMAGE':
            nt.nodes.remove(n)
    t = nt.nodes.new('ShaderNodeTexImage')
    t.image = img
    nt.links.new(t.outputs['Color'], bsdf.inputs['Base Color'])


def save_extra(img, fname):
    img.filepath_raw = os.path.join(OUT, fname)
    img.file_format = 'PNG'
    try:
        img.save()
    except Exception:
        img.save_render(os.path.join(OUT, fname))


def make_cat():
    arm, meshes = load(os.path.join(SRC, "cat_source.blend"))
    flatten(arm, meshes, -90, 0.34)
    clean_bone_names(arm)
    body = max(meshes, key=lambda m: len(m.data.vertices))
    cards = [m for m in meshes if m is not body and m.data.materials[0].surface_render_method == 'BLENDED']
    eyes = [m for m in meshes if m is not body and m not in cards]
    body.data.materials[0].name = "Fur"
    for e in eyes:
        e.data.materials[0].name = "Eye"
    for c in cards:
        c.data.materials[0].name = "FurCards"
        # tufts and whiskers were drawn for a black cat: a calico's are pale (the strokes stay in the alpha)
        img = material_image(c.data.materials[0]).image
        px = np.array(img.pixels[:], np.float32).reshape(-1, 4)
        shade = 0.78 + 0.16 * (px[:, :3].mean(1) > 0.5)
        px[:, :3] = np.array([0.95, 0.93, 0.89], np.float32) * shade[:, None]
        pale = bpy.data.images.new("bedah_tufts", img.size[0], img.size[1], alpha=True)
        pale.pixels[:] = px.ravel()
        only_texture(c.data.materials[0], pale)
    src = material_image(body.data.materials[0]).image
    coat = calico(arm, body, src, "bedah_coat")
    only_texture(body.data.materials[0], coat)
    # the body is only 3.9k vertices: one level of smoothing, weights follow
    sub = body.modifiers.new("Smooth", 'SUBSURF'); sub.levels = 1; sub.render_levels = 1
    export_character("bedah", [body] + eyes + cards, arm)
    return arm


def make_dog():
    arm, meshes = load(os.path.join(SRC, "dog_source.blend"))
    flatten(arm, meshes, 0, 0.55)
    clean_bone_names(arm)
    body = meshes[0]
    mat = body.data.materials[0]
    mat.name = "Fur"
    normal = material_image(mat, 'Normal')
    base = material_image(mat).image
    if normal:
        save_extra(normal.image, "dog_Fur_normal.png")
    only_texture(mat, base)
    export_character("dog", [body], arm)
    return arm


# PETS = "cat" or "dog" before exec() makes only that one
_which = globals().get("PETS", "both")
if _which in ("both", "cat"):
    make_cat()
if _which in ("both", "dog"):
    make_dog()
print("pets exported to", OUT)
