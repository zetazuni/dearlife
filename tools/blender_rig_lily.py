"""Rigs the imported Sketchfab model "Female - Style 4 (Pose 1)" by mifusaja (CC BY) as Lily. Import it first (Blender MCP
download_sketchfab_model uid 577be48ee19c46708a469ab502aae3d8, target_size 1.6), then exec this file."""
import os
exec(open(os.path.join(r"S:\Dearlife by Zetazuni", "tools", "blender_rig.py"), encoding="utf-8").read())
objs = {o.name: o for o in bpy.data.objects if o.type == 'MESH'}
for o in objs.values():
    mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
bpy.context.view_layer.update()
for n in [o.name for o in bpy.data.objects if o.type == 'EMPTY']:
    bpy.data.objects.remove(bpy.data.objects[n], do_unlink=True)


HANDS = [Vector((0.28, -0.07, 1.52)), Vector((-0.31, -0.10, 0.72))]   # where the two hands are in the original greeting pose


def fit_layers(objs):
    """The outfit is a separate shell over the body. When a joint bends the body can poke through the cloth ("skin pops out"),
    so the body is sunk 8 mm under everything the clothes cover and the cloth and hijab are pushed out 5 mm."""
    from mathutils.kdtree import KDTree
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs.values():
        o.select_set(True)
    bpy.context.view_layer.objects.active = list(objs.values())[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    body = objs["Object_2"]
    layers = [objs["Object_3"], objs["Object_6"]]
    kd = KDTree(sum(len(l.data.vertices) for l in layers))
    i = 0
    for l in layers:
        for v in l.data.vertices:
            kd.insert(l.matrix_world @ v.co, i); i += 1
    kd.balance()
    moved = 0
    for v in body.data.vertices:
        co = body.matrix_world @ v.co
        if co.z < 0.05 or co.z > 1.38:      # feet and the head (the face must keep its shape) stay as they are
            continue
        if any((co - h).length < 0.13 for h in HANDS):
            continue
        found = kd.find(co)
        if found[2] < 0.06:
            v.co = v.co - v.normal * 0.006
            moved += 1
    for l in layers:
        for v in l.data.vertices:
            v.co = v.co + v.normal * 0.003
    print("body verts sunk under the outfit:", moved)


fit_layers(objs)
B = {
 "pelvis": (None, (0,-0.05,0.90), (0,-0.05,1.0)),
 "spine": ("pelvis", (0,-0.05,1.0), (0,-0.08,1.36)),
 "neck": ("spine", (0,-0.08,1.36), (0,-0.10,1.62)),
 "arm.L": ("spine", (0.14,-0.03,1.30), (0.25,-0.03,1.10)),
 "forearm.L": ("arm.L", (0.25,-0.03,1.10), (0.28,-0.07,1.52)),
 "arm.R": ("spine", (-0.14,-0.02,1.30), (-0.18,-0.02,1.06)),
 "forearm.R": ("arm.R", (-0.18,-0.02,1.06), (-0.31,-0.10,0.72)),
 "leg.L": ("pelvis", (0.07,-0.04,0.90), (0.065,0.0,0.47)),
 "shin.L": ("leg.L", (0.065,0.0,0.47), (0.09,0.17,0.02)),
 "leg.R": ("pelvis", (-0.07,-0.05,0.90), (-0.036,-0.11,0.47)),
 "shin.R": ("leg.R", (-0.036,-0.11,0.47), (-0.04,-0.25,0.02)),
}
arm = build_armature("lily", B)
for n, o in objs.items():
    if n == "Object_6": skin(o, arm, B, only=["spine", "neck", "pelvis"])
    else: skin(o, arm, B, head_z=1.42, falloff=3.0)


def copy_body_weights(objs):
    """Cloth and hijab take the skin weights of the nearest body vertex, so the shells bend exactly like the body under them
    and the skin cannot pop out through the outfit."""
    from mathutils.kdtree import KDTree
    body = objs["Object_2"]
    kd = KDTree(len(body.data.vertices))
    for v in body.data.vertices:
        kd.insert(body.matrix_world @ v.co, v.index)
    kd.balance()
    names = {vg.index: vg.name for vg in body.vertex_groups}
    bw = {v.index: [(names[g.group], g.weight) for g in v.groups] for v in body.data.vertices}
    for key in ("Object_3", "Object_6"):
        o = objs[key]
        for vg in list(o.vertex_groups):
            o.vertex_groups.remove(vg)
        groups = {n: o.vertex_groups.new(name=n) for n in names.values()}
        for v in o.data.vertices:
            _, idx, _ = kd.find(o.matrix_world @ v.co)
            for n, w in bw[idx]:
                groups[n].add([v.index], w, 'REPLACE')




def rigid_hands(body):
    """A hand is one rigid piece on the forearm bone, so the fingers (the pinky above all) cannot be stretched towards other bones."""
    names = {vg.index: vg.name for vg in body.vertex_groups}
    for hand, side in zip(HANDS, ("forearm.L", "forearm.R")):
        for v in body.data.vertices:
            co = body.matrix_world @ v.co
            if (co - hand).length < 0.2 and abs(co.x) > 0.19:      # beyond the width of the body, so not the head or the hip
                for gi in [g.group for g in v.groups]:
                    body.vertex_groups[gi].remove([v.index])
                body.vertex_groups[side].add([v.index], 1.0, 'REPLACE')


rigid_hands(objs["Object_2"])
copy_body_weights(objs)
mesh = join_meshes(list(objs.values()), "lily_mesh")
mesh.parent = arm
if not any(m.type == 'ARMATURE' for m in mesh.modifiers):
    mod = mesh.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
N = {"arm.L": (0.15,0,-1), "forearm.L": (0.05,0,-1), "arm.R": (-0.12,0,-1), "forearm.R": (-0.08,0,-1),
     "leg.L": (0,0,-1), "shin.L": (0,0.05,-1), "leg.R": (0,0,-1), "shin.R": (0,-0.05,-1)}
pose_neutral(mesh, arm, N)



def add_feet(mesh, arm):
    """Foot bones at the ankles, so the shoes can stay flat while the leg bends. Everything below 11 cm follows the foot,
    blending into the shin over the next 5 cm."""
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.data.edit_bones
    ankle = {}
    for side in ("L", "R"):
        sh = eb["shin." + side]
        a = sh.tail.copy()
        ankle[side] = a
        f = eb.new("foot." + side)
        f.head = Vector((a.x, a.y, 0.08))
        f.tail = Vector((a.x, a.y - 0.14, 0.03))
        f.parent = sh
    bpy.ops.object.mode_set(mode='OBJECT')
    groups = {side: mesh.vertex_groups.new(name="foot." + side) for side in ("L", "R")}
    n = 0
    for v in mesh.data.vertices:
        z = (mesh.matrix_world @ v.co).z
        if z >= 0.11:
            continue
        t = min(1.0, (0.11 - z) / 0.05)
        side = "L" if v.co.x > 0 else "R"
        old = [(g.group, g.weight) for g in v.groups if mesh.vertex_groups[g.group].name not in ("foot.L", "foot.R")]
        for gi, w in old:
            mesh.vertex_groups[gi].add([v.index], w * (1.0 - t), 'REPLACE')
        groups[side].add([v.index], t, 'REPLACE')
        n += 1
    print("foot vertices:", n)


exec(open(os.path.join(r"S:\Dearlife by Zetazuni", "tools", "blender_symmetrize.py"), encoding="utf-8").read())
symmetrize(mesh, arm)   # left arm = mirrored right arm, right leg = mirrored left leg
add_feet(mesh, arm)
info = export_character("lily", mesh, arm)
