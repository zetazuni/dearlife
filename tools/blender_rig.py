"""Rigs an imported static character mesh for Dearlife. Run inside Blender (Blender MCP).

Usage (after importing a model): exec this file, then call
    rig_and_export(name, mesh_objects, bones, neutral, out_dir)
bones: {name: (parent, head, tail)} in world space (Blender coordinates, +Y = the way the character walks away from,
front faces -Y, feet at z 0). neutral: {bone: target direction} used to bake a neutral standing pose into the mesh
(arms hanging, legs straight) before the rig is created, so the rest pose is a relaxed A-pose.
The weights are computed from the distance to each bone segment (heat weighting fails on separate, non-manifold
sculpted parts). The bone names are the joints CharacterRig.cs drives: pelvis, spine, neck, arm.L, forearm.L,
arm.R, forearm.R, leg.L, shin.L, leg.R, shin.R (pets: body, head, tail, legFL, legFR, legBL, legBR).
"""
import bpy, math, os, json
import numpy as np
from mathutils import Vector, Matrix, Quaternion

PROJECT = r"S:\Dearlife by Zetazuni"
OUT = os.path.join(PROJECT, "Assets", "Art", "Models", "Characters")


def join_meshes(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True) if False else None
    bpy.context.view_layer.objects.active = objs[0]
    for o in objs:
        o.parent = None
    for o in objs:
        o.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    return ob


def seg_dist(P, a, b):
    a = np.array(a); b = np.array(b)
    ab = b - a
    t = np.clip(((P - a) @ ab) / max(float(ab @ ab), 1e-9), 0, 1)
    proj = a + t[:, None] * ab
    return np.linalg.norm(P - proj, axis=1)


def build_armature(name, bones):
    arm_data = bpy.data.armatures.new(name)
    arm = bpy.data.objects.new(name + "_rig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    eb = {}
    for n, (par, h, t) in bones.items():
        b = arm_data.edit_bones.new(n)
        b.head = Vector(h); b.tail = Vector(t)
        if (b.tail - b.head).length < 0.02:
            b.tail = b.head + Vector((0, 0, 0.05))
        eb[n] = b
    for n, (par, h, t) in bones.items():
        if par:
            eb[n].parent = eb[par]
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


def weights_for(mesh, bones, head_bone="neck", head_z=None, falloff=6.0):
    P = np.array([(mesh.matrix_world @ v.co)[:] for v in mesh.data.vertices])
    names = list(bones)
    D = np.stack([seg_dist(P, bones[n][1], bones[n][2]) for n in names], axis=1)
    W = 1.0 / (D + 0.012) ** falloff
    dmin = D.min(axis=1, keepdims=True)
    W[D > dmin * 1.6 + 0.03] = 0.0          # only the bones that are close
    if head_z is not None and head_bone in names:
        hi = (P[:, 2] > head_z) & (np.abs(P[:, 0]) < 0.16)   # not the raised hand
        W[hi] = 0.0
        W[hi, names.index(head_bone)] = 1.0
    W /= np.maximum(W.sum(axis=1, keepdims=True), 1e-9)
    return names, W


def skin(mesh, arm, bones, head_z=None, falloff=6.0, only=None):
    use = {n: bones[n] for n in (only or bones)}
    names, W = weights_for(mesh, use, head_z=head_z, falloff=falloff)
    for vg in list(mesh.vertex_groups):
        mesh.vertex_groups.remove(vg)
    groups = {n: mesh.vertex_groups.new(name=n) for n in names}
    for i in range(W.shape[0]):
        for j, n in enumerate(names):
            if W[i, j] > 0.01:
                groups[n].add([i], float(W[i, j]), 'REPLACE')
    for m in list(mesh.modifiers):
        if m.type == 'ARMATURE':
            mesh.modifiers.remove(m)
    mod = mesh.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    mesh.parent = arm


def pose_neutral(mesh, arm, neutral):
    """Rotate bones (parents first) so their direction matches the target, bake that pose into the mesh and the rest pose."""
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    for n, target in neutral.items():
        pb = arm.pose.bones[n]
        bpy.context.view_layer.update()
        cur = (pb.tail - pb.head)
        cur = (arm.matrix_world.to_3x3() @ cur).normalized()
        q = cur.rotation_difference(Vector(target).normalized())
        head_w = arm.matrix_world @ pb.head
        R = Matrix.Translation(head_w) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head_w)
        pb.matrix = arm.matrix_world.inverted() @ (R @ (arm.matrix_world @ pb.matrix))
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode='OBJECT')
    # bake into the mesh
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    bpy.ops.object.modifier_apply(modifier="Armature")
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.pose.armature_apply()
    bpy.ops.object.mode_set(mode='OBJECT')
    mod = mesh.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm


def export_character(name, mesh, arm, out_dir=OUT):
    """mesh may be one object or a list (a character sculpted in several skinned parts)."""
    os.makedirs(out_dir, exist_ok=True)
    meshes = mesh if isinstance(mesh, (list, tuple)) else [mesh]
    if len(meshes) == 1:
        meshes[0].name = name + "_mesh"
    # remember which image every material uses, Unity builds its own HDRP materials from this
    info = {}
    slots = [sl for me in meshes for sl in me.material_slots]
    for slot in slots:
        m = slot.material
        if m is None or m.name in info:
            continue
        img = None
        if m and m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == 'TEX_IMAGE' and n.image:
                    img = n.image; break
        base = (0.8, 0.8, 0.8, 1.0)
        bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if m and m.use_nodes else None
        if bsdf:
            base = tuple(bsdf.inputs['Base Color'].default_value)
        fn = None
        if img:
            fn = name + "_" + m.name.replace(" ", "_") + ".png"
            img.filepath_raw = os.path.join(out_dir, fn)
            img.file_format = 'PNG'
            try:
                img.save()
            except Exception:
                img.pack(); img.save_render(os.path.join(out_dir, fn))
        info[m.name] = {"texture": fn, "color": base[:3]}
    json.dump(info, open(os.path.join(out_dir, name + ".materials.json"), "w"), indent=1)
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    for me in meshes:
        me.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, name + ".fbx"), use_selection=True, object_types={'ARMATURE', 'MESH'},
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False,
                             bake_anim=False, path_mode='COPY', embed_textures=False)
    return info


def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)
    for a in list(bpy.data.armatures):
        bpy.data.armatures.remove(a)


def pose_neutral_pairs(meshes, arm, pairs):
    """Like pose_neutral, for a rig whose bones do not point along the limb (imported rigs): each entry is
    bone: (child bone, target direction). The limb direction is measured from the bone's head to the child's head."""
    arm.animation_data_clear()
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.pose.transforms_clear()
    arm.data.pose_position = 'POSE'
    for n, (child, target) in pairs.items():
        bpy.context.view_layer.update()
        pb = arm.pose.bones[n]
        head_w = arm.matrix_world @ pb.head
        cur = ((arm.matrix_world @ arm.pose.bones[child].head) - head_w).normalized()
        q = cur.rotation_difference(Vector(target).normalized())
        R = Matrix.Translation(head_w) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head_w)
        pb.matrix = arm.matrix_world.inverted() @ (R @ (arm.matrix_world @ pb.matrix))
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode='OBJECT')
    for me in meshes:
        bpy.ops.object.select_all(action='DESELECT')
        bpy.context.view_layer.objects.active = me
        me.select_set(True)
        for md in me.modifiers:
            if md.type == 'ARMATURE':
                bpy.ops.object.modifier_apply(modifier=md.name)
                break
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.pose.armature_apply()
    bpy.ops.object.mode_set(mode='OBJECT')
    for me in meshes:
        mod = me.modifiers.new("Armature", 'ARMATURE')
        mod.object = arm
