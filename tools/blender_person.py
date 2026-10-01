"""
Turns a rigged person downloaded into Blender (a Sketchfab model, say) into one the game can pick in its character
selection: the visible pose becomes the rest pose, the model is sized to a real height, and the FBX, its textures and a
<id>.person.json (materials and which bone is which) go to Assets/Local/People/<id>.

Assets/Local is gitignored: downloaded characters stay on this computer and never reach the public repo, whatever
their licence says. In Unity, run Dearlife > Import people afterwards.

Run inside Blender (Blender MCP) with the model imported and nothing else in the scene:

    exec(open(r"S:\\Dearlife by Zetazuni\\tools\\blender_person.py").read(), g := {"__name__": "person"})
    g["bake"]()                                   # once, straight after the import
    g["export"]("mia", "Mia", 1.68, feminine=1.0, drop=["Object_25"], bones={...}, kinds={...})
"""
import json
import os

import bpy

OUT = r"S:\Dearlife by Zetazuni\Assets\Local\People"


def rig():
    return next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')


def skinned(arm):
    return [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.find_armature() == arm]


def bake():
    """Models converted from a game often arrive with a rest pose that means nothing (only the posed state looks
    right) under a pile of scaled empties. The shape that is seen is written into the meshes, the pose becomes the rest
    pose, the empties go, rotation and scale are applied, and the shading normals are worked out again."""
    import numpy as np
    sc = bpy.context.scene
    arm = rig()
    meshes = skinned(arm)
    dg = bpy.context.evaluated_depsgraph_get()
    for o in meshes:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        co = np.empty(len(me.vertices) * 3, dtype=np.float32)
        me.vertices.foreach_get('co', co)
        ev.to_mesh_clear()
        o.data.vertices.foreach_set('co', co)
        o.data.update()
    bpy.ops.object.select_all(action='DESELECT')
    arm.hide_set(False)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    for o in [arm] + meshes:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    bpy.ops.object.select_all(action='DESELECT')
    for o in [arm] + meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in meshes:
        o.parent = arm
        for m in o.modifiers:
            if m.type == 'ARMATURE':
                m.object = arm
    for o in list(sc.objects):
        if o.type == 'EMPTY' or (o.type == 'MESH' and o not in meshes):
            bpy.data.objects.remove(o, do_unlink=True)
    # bones keep their heads; the tails left far away by the old rest pose are drawn in
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    for eb in arm.data.edit_bones:
        eb.use_connect = False
    for eb in arm.data.edit_bones:
        if eb.length > 0.3:
            eb.tail = eb.head + (eb.tail - eb.head).normalized() * 0.04
    bpy.ops.object.mode_set(mode='OBJECT')
    for o in meshes:
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        if o.data.has_custom_normals:
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
        bpy.ops.object.shade_smooth()


def height(meshes):
    from mathutils import Vector
    zs = [(o.matrix_world @ Vector(c)).z for o in meshes for c in o.bound_box]
    return min(zs), max(zs)


def image_of(principled, socket):
    """The image feeding a socket of the Principled shader, through whatever nodes sit between."""
    s = principled.inputs.get(socket)
    if s is None or not s.is_linked:
        return None
    n = s.links[0].from_node
    for _ in range(6):
        if n.type == 'TEX_IMAGE':
            return n.image
        linked = [i for i in n.inputs if i.is_linked]
        if not linked:
            return None
        n = linked[0].links[0].from_node
    return None


def save_png(img, path):
    if os.path.exists(path):
        return
    c = img.copy()
    c.filepath_raw = path
    c.file_format = 'PNG'
    c.save()
    bpy.data.images.remove(c)


def save_cut(img, path, strands=None):
    """Hair cards that lost their alpha on the way, written as a new image with the alpha in it. Two kinds of upload:
    the alpha was flattened onto one background colour (strands None: everything that is not that exact colour is hair),
    or the strands are painted on black with compression noise in the red channel (strands = (threshold, width): the
    green channel is the alpha, and the background takes the strands' average colour so no dark fringe shows)."""
    import numpy as np
    if os.path.exists(path):
        return
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    if strands is None:
        a = (px[..., :3] * 255).round().astype(np.int32)
        key = a[..., 0] * 65536 + a[..., 1] * 256 + a[..., 2]
        vals, cnt = np.unique(key, return_counts=True)
        bg = a.reshape(-1, 3)[np.argmax(key.ravel() == vals[np.argmax(cnt)])]
        s = (np.abs(a - bg).max(axis=2) > 1).astype(np.float32)
        for _ in range(2):
            s = (s + np.roll(s, 1, 0) + np.roll(s, -1, 0) + np.roll(s, 1, 1) + np.roll(s, -1, 1)) / 5
        px[..., 3] = np.clip(s * 1.6, 0, 1)
    else:
        t, width = strands
        s = np.clip((px[..., 1] - t) / width, 0, 1)
        hair = s > 0.6
        mean = px[..., :3][hair].mean(axis=0) if hair.any() else np.array([0.2, 0.15, 0.1])
        px[..., :3] = np.where((s > 0.3)[..., None], px[..., :3], mean)
        for _ in range(1):
            s = np.maximum(s, (np.roll(s, 1, 1) + np.roll(s, -1, 1) + s) / 3 * 1.5)    # a little fuller sideways
        px[..., 3] = np.clip(s, 0, 1)
    m = bpy.data.images.new("cut_tmp", w, h, alpha=True)
    m.alpha_mode = 'STRAIGHT'
    m.pixels.foreach_set(px.ravel())
    m.filepath_raw = path
    m.file_format = 'PNG'
    m.save()
    bpy.data.images.remove(m)


def save_mask(img, path):
    """glTF keeps roughness in green and metal in blue; HDRP's mask map wants metal in red, occlusion in green and
    smoothness in alpha."""
    import numpy as np
    if os.path.exists(path):
        return
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    out = np.empty_like(px)
    out[..., 0] = px[..., 2]
    out[..., 1] = 1.0
    out[..., 2] = 0.0
    out[..., 3] = 1.0 - px[..., 1]
    m = bpy.data.images.new("mask_tmp", w, h, alpha=True)
    m.colorspace_settings.name = 'Non-Color'
    m.alpha_mode = 'STRAIGHT'
    m.pixels.foreach_set(out.ravel())
    m.filepath_raw = path
    m.file_format = 'PNG'
    m.save()
    bpy.data.images.remove(m)


def export(pid, name, tall, feminine=0.5, drop=(), bones=None, kinds=None, blend=None, cut=None, credit=""):
    """pid: file name; name: shown in the game; tall: metres from the soles to the top of the head or hair;
    feminine: 1 walks the feminine walk, 0 the masculine one; drop: objects left out (a holstered gun);
    bones: Unity humanoid bone -> the model's bone; kinds: material name -> skin, hair, eye or cloth;
    blend: material name -> opacity, for see through parts with no texture of their own;
    cut: material name -> None or (threshold, width): hair whose alpha is rebuilt from its texture (see save_cut)."""
    import numpy as np
    kinds, blend, cut = kinds or {}, blend or {}, cut or {}
    arm = rig()
    for n in drop:
        o = bpy.data.objects.get(n)
        if o:
            bpy.data.objects.remove(o, do_unlink=True)
    meshes = skinned(arm)
    lo, hi = height(meshes)
    k = tall / (hi - lo)
    arm.location.z -= lo
    arm.scale = (k, k, k)
    arm.location = (arm.location.x * k, arm.location.y * k, arm.location.z * k)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    for o in [arm] + meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    arm.name = "Armature"
    out = os.path.join(OUT, pid)
    os.makedirs(out, exist_ok=True)

    mats = {}
    for o in meshes:
        for m in o.data.materials:
            if m is None or m.name in mats or not m.node_tree:
                continue
            p = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if p is None:
                continue
            info = {"base": None, "normal": None, "mask": None, "emissive": None, "alpha": "opaque", "opacity": 1.0,
                    "color": [round(c, 4) for c in p.inputs['Base Color'].default_value[:3]],
                    "kind": kinds.get(m.name, "cloth")}
            base = image_of(p, 'Base Color')
            if base:
                info["color"] = [1.0, 1.0, 1.0]
                if m.name in cut:
                    tag = "_cut.png" if cut[m.name] is None else "_cut%d.png" % round(cut[m.name][0] * 1000)
                    info["base"] = pid + "_" + os.path.splitext(base.name)[0][-48:] + tag
                    save_cut(base, os.path.join(out, info["base"]), cut[m.name])
                    info["alpha"] = "clip"
                else:
                    info["base"] = pid + "_" + os.path.splitext(base.name)[0][-48:] + ".png"
                    save_png(base, os.path.join(out, info["base"]))
                    a = np.array(base.pixels[:], dtype=np.float32)[3::4]
                    if base.channels == 4 and float(a.min()) < 0.9 and float((a < 0.5).mean()) > 0.02:
                        info["alpha"] = "clip"       # hair cards, lashes, lace
            nrm = image_of(p, 'Normal')
            if nrm:
                info["normal"] = pid + "_" + os.path.splitext(nrm.name)[0][-48:] + ".png"
                save_png(nrm, os.path.join(out, info["normal"]))
            orm = image_of(p, 'Roughness')
            if orm:
                info["mask"] = pid + "_" + os.path.splitext(orm.name)[0][-48:] + "_mask.png"
                save_mask(orm, os.path.join(out, info["mask"]))
            em = image_of(p, 'Emission Color')
            if em:
                info["emissive"] = pid + "_" + os.path.splitext(em.name)[0][-48:] + ".png"
                save_png(em, os.path.join(out, info["emissive"]))
            if m.name in blend:
                info["alpha"], info["opacity"] = "blend", blend[m.name]
            mats[m.name] = info

    have = {b.name for b in arm.data.bones}
    missing = [h for h, b in (bones or {}).items() if b not in have]
    if missing:
        raise RuntimeError("no such bones: " + ", ".join(missing))
    lo, hi = height(meshes)
    # lists, not dictionaries: Unity's own JSON reader takes these as they are
    person = {"id": pid, "name": name, "feminine": feminine, "height": round(hi - lo, 3), "credit": credit,
              "bones": [{"human": h, "bone": b} for h, b in (bones or {}).items()],
              "materials": [dict(v, name=k) for k, v in mats.items()]}
    json.dump(person, open(os.path.join(out, pid + ".person.json"), "w"), indent=1)

    bpy.ops.object.select_all(action='DESELECT')
    for o in [arm] + meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, pid + ".fbx"), use_selection=True, object_types={'ARMATURE', 'MESH'},
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, use_mesh_modifiers=False, mesh_smooth_type='OFF', add_leaf_bones=False,
                             bake_anim=False, path_mode='STRIP', embed_textures=False)
    return f"{pid}: {len(meshes)} meshes, {len(arm.data.bones)} bones, {len(mats)} materials, {person['height']} m"


def humanoid(hips, spine, chest, neck, head, side, upper_chest=None, fingers=None):
    """A Unity humanoid bone map. side(part, s) names an arm or leg bone, s being 'l' or 'r'; part is one of shoulder,
    upperarm, lowerarm, hand, upperleg, lowerleg, foot, toes. fingers(finger, joint, s) names a finger bone (finger is
    thumb, index, middle, ring or little; joint 0 to 2) or returns None."""
    m = {"Hips": hips, "Spine": spine, "Chest": chest, "Neck": neck, "Head": head}
    if upper_chest:
        m["UpperChest"] = upper_chest
    for word, s in (("Left", "l"), ("Right", "r")):
        for human, part in (("Shoulder", "shoulder"), ("UpperArm", "upperarm"), ("LowerArm", "lowerarm"), ("Hand", "hand"),
                            ("UpperLeg", "upperleg"), ("LowerLeg", "lowerleg"), ("Foot", "foot"), ("Toes", "toes")):
            b = side(part, s)
            if b:
                m[word + human] = b
        if fingers:
            for human, f in (("Thumb", "thumb"), ("Index", "index"), ("Middle", "middle"), ("Ring", "ring"), ("Little", "little")):
                for j, joint in enumerate(("Proximal", "Intermediate", "Distal")):
                    b = fingers(f, j, s)
                    if b:
                        m[f"{word} {human} {joint}"] = b
    return m
