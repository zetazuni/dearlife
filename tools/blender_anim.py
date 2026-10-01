"""
Animations for the people, from any rigged Blender file (v0.50.0).

The walking speed it prints is only a first guess (it assumes each foot is planted for half the cycle): measure the real
one in the game and put it in CharacterRig (WalkClipSpeedM, WalkClipSpeedF).

Every source rig is different (control bones, IK, twist bones), so the motion is baked onto one plain standard
skeleton of 21 bones named like Unity's humanoid bones, sized to a real person, and exported as a skeleton only FBX to
Assets/Art/Animations/BlendSwap. In Unity, Dearlife > Set up character animation makes them Humanoid and puts them in
the people's controller. The file name says what the clip is for: walk_..., walkf_... (the feminine walk), stand_...

Run with Blender in the background:

    blender -b "<file>.blend" --python tools/blender_anim.py -- bake <armature> <clip name> <first> <last> key=value ...
    blender -b --python tools/blender_anim.py -- idle <clip name>

For bake, the key=value pairs say which of the file's bones is which standard bone (Hips=hips Spine=spine ...); a name
ending in .L or .R may be given once as LeftX=name.L and the right side is found by itself. action=<name> plays that
action instead of what the file has on the rig. turn=180 turns a character that faces +Y round.

The idle is made here from nothing (no recording): a relaxed stand with slow breathing and a slight shift of weight.
"""
import math
import os
import sys

import bpy
from mathutils import Euler, Matrix, Vector

OUT = r"S:\Dearlife by Zetazuni\Assets\Art\Animations\BlendSwap"
HIP_HEIGHT = 0.95      # every clip is sized so the hips rest this high (a person of about 1.7 m)

# standard bone -> its parent
STD = {
    "Hips": None, "Spine": "Hips", "Chest": "Spine", "Neck": "Chest", "Head": "Neck",
    "LeftShoulder": "Chest", "LeftUpperArm": "LeftShoulder", "LeftLowerArm": "LeftUpperArm", "LeftHand": "LeftLowerArm",
    "RightShoulder": "Chest", "RightUpperArm": "RightShoulder", "RightLowerArm": "RightUpperArm", "RightHand": "RightLowerArm",
    "LeftUpperLeg": "Hips", "LeftLowerLeg": "LeftUpperLeg", "LeftFoot": "LeftLowerLeg", "LeftToes": "LeftFoot",
    "RightUpperLeg": "Hips", "RightLowerLeg": "RightUpperLeg", "RightFoot": "RightLowerLeg", "RightToes": "RightFoot",
}


def new_rig(heads, tails=None, mats=None):
    """A standard skeleton with its joints at the given places (world space). mats gives each bone the orientation of
    the source bone it will follow, so copying the source's world transform is exact."""
    data = bpy.data.armatures.new("Rig")
    rig = bpy.data.objects.new("Rig", data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for name in STD:
        eb = data.edit_bones.new(name)
        eb.head = heads[name]
        eb.tail = tails[name] if tails and name in tails else heads[name] + Vector((0, 0, 0.08))
        if (eb.tail - eb.head).length < 0.02:
            eb.tail = eb.head + Vector((0, 0, 0.05))
        if mats and name in mats:
            length = max(0.03, (eb.tail - eb.head).length)
            eb.matrix = mats[name]
            eb.length = length
    for name, parent in STD.items():
        if parent:
            data.edit_bones[name].parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig


def export(rig, name, first, last, fps):
    sc = bpy.context.scene
    sc.frame_start, sc.frame_end = first, last
    sc.render.fps = fps
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE'},
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True,
                             bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
                             bake_anim_simplify_factor=0.0, path_mode='STRIP', embed_textures=False)
    return path


def bake(arm_name, clip, first, last, names, action=None, turn=0.0):
    sc = bpy.context.scene
    src = bpy.data.objects[arm_name]
    fps = sc.render.fps
    if action:
        if not src.animation_data:
            src.animation_data_create()
        act = bpy.data.actions[action]
        src.animation_data.action = act
        try:
            if act.slots and src.animation_data.action_slot is None:
                src.animation_data.action_slot = act.slots[0]
        except AttributeError:
            pass
        for t in src.animation_data.nla_tracks:
            t.mute = True

    # the left side's names stand for the right side's too
    full = {}
    for std, bone in names.items():
        full[std] = bone
        if std.startswith("Left"):
            other = bone.replace(".L", ".R") if ".L" in bone else bone.replace("_L", "_R")
            full.setdefault("Right" + std[4:], other)
    missing = [s for s in STD if s not in full or full[s] not in src.data.bones]
    if missing:
        raise RuntimeError("bones not given or not found: " + ", ".join(missing))

    # the source is sized (and turned) as a whole, so its own IK and constraints keep working
    hip0 = (src.matrix_world @ src.data.bones[full["Hips"]].head_local).z
    k = HIP_HEIGHT / hip0
    src.matrix_world = Matrix.Rotation(math.radians(turn), 4, 'Z') @ Matrix.Scale(k, 4) @ src.matrix_world
    bpy.context.view_layer.update()

    mw = src.matrix_world
    heads = {s: mw @ src.data.bones[b].head_local for s, b in full.items()}
    tails = {s: mw @ src.data.bones[b].tail_local for s, b in full.items()}
    rot = mw.to_quaternion().to_matrix().to_4x4()
    mats = {}
    for s, b in full.items():
        m = rot @ src.data.bones[b].matrix_local
        m.translation = heads[s]
        mats[s] = m
    rig = new_rig(heads, tails, mats)
    for s, b in full.items():
        c = rig.pose.bones[s].constraints.new('COPY_TRANSFORMS')
        c.target = src
        c.subtarget = b
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    bpy.ops.pose.select_all(action='SELECT')
    bpy.ops.nla.bake(frame_start=first, frame_end=last, step=1, only_selected=True, visual_keying=True,
                     clear_constraints=True, use_current_action=True, bake_types={'POSE'})
    bpy.ops.object.mode_set(mode='OBJECT')

    # what a walk covers: how far a planted foot travels back under the body, per second
    ys = []
    for f in range(first, last + 1):
        sc.frame_set(f)
        ys.append((rig.matrix_world @ rig.pose.bones["LeftFoot"].head).y - (rig.matrix_world @ rig.pose.bones["Hips"].head).y)
    travel = max(ys) - min(ys)
    back = 2.0 * travel / max((last - first) / fps, 0.01)       # one full cycle: each foot slides back for half of it
    path = export(rig, clip, first, last, fps)
    toe = heads["LeftToes"].y - heads["LeftFoot"].y
    return f"{clip}: frames {first} to {last} at {fps} fps, sized by {k:.3f}, faces {'-Y' if toe < 0 else '+Y (use turn=180)'}, foot travel {travel:.2f} m, walking speed {back:.2f} m/s -> {path}"


def idle(clip, seconds=6.0, fps=30):
    """A relaxed stand: arms hanging a little off the body, slow breathing in the chest and shoulders, the weight
    easing from one leg to the other and back once in the loop, the head following a touch late."""
    sc = bpy.context.scene
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    s = 0.10            # half the hip width
    P = {
        "Hips": (0, 0, 0.95), "Spine": (0, 0.01, 1.06), "Chest": (0, 0.01, 1.20), "Neck": (0, 0.02, 1.42), "Head": (0, 0.0, 1.52),
        "LeftShoulder": (0.03, 0.02, 1.38), "LeftUpperArm": (0.17, 0.02, 1.38), "LeftLowerArm": (0.45, 0.02, 1.38), "LeftHand": (0.70, 0.02, 1.38),
        "LeftUpperLeg": (s, 0, 0.90), "LeftLowerLeg": (s, -0.01, 0.50), "LeftFoot": (s, 0.02, 0.09), "LeftToes": (s, -0.10, 0.02),
    }
    for k, v in list(P.items()):
        if k.startswith("Left"):
            P["Right" + k[4:]] = (-v[0], v[1], v[2])
    heads = {k: Vector(v) for k, v in P.items()}
    child = {"Hips": "Spine", "Spine": "Chest", "Chest": "Neck", "Neck": "Head"}
    for side in ("Left", "Right"):
        child.update({side + "Shoulder": side + "UpperArm", side + "UpperArm": side + "LowerArm", side + "LowerArm": side + "Hand",
                      side + "UpperLeg": side + "LowerLeg", side + "LowerLeg": side + "Foot", side + "Foot": side + "Toes"})
    tails = {k: heads[c] for k, c in child.items()}
    tails["Head"] = heads["Head"] + Vector((0, 0, 0.18))
    for side, x in (("Left", 1), ("Right", -1)):
        tails[side + "Hand"] = heads[side + "Hand"] + Vector((0.09 * x, 0, 0))
        tails[side + "Toes"] = heads[side + "Toes"] + Vector((0, -0.07, 0))
    rig = new_rig(heads, tails)
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='POSE')
    pb = rig.pose.bones
    for b in pb:
        b.rotation_mode = 'QUATERNION'
    rest = {n: rig.data.bones[n].matrix_local.to_quaternion() for n in STD}

    def world_turn(name, x=0.0, y=0.0, z=0.0):
        """Turns a bone by degrees about the world's axes (x right, y back, z up), whatever way the bone itself points."""
        q = Euler((math.radians(x), math.radians(y), math.radians(z)), 'XYZ').to_quaternion()
        return rest[name].inverted() @ q @ rest[name]

    n = int(seconds * fps)
    for f in range(n + 1):
        t = f / n * 2 * math.pi
        breath = math.sin(t * 2)                 # two breaths in the loop
        sway = math.sin(t)                       # one shift of weight, there and back
        late = math.sin(t - 0.5)
        sc.frame_set(f)
        pb["Hips"].location = rig.data.bones["Hips"].matrix_local.to_quaternion().inverted() @ Vector((0.012 * sway, 0.0, -0.003 * abs(sway)))
        pb["Hips"].rotation_quaternion = world_turn("Hips", y=1.6 * sway, z=1.0 * sway)
        pb["Spine"].rotation_quaternion = world_turn("Spine", x=-0.5 * breath, y=-1.2 * sway)
        pb["Chest"].rotation_quaternion = world_turn("Chest", x=-0.9 * breath, y=-0.8 * sway, z=-0.8 * sway)
        pb["Neck"].rotation_quaternion = world_turn("Neck", x=0.5 * breath, y=0.3 * late)
        pb["Head"].rotation_quaternion = world_turn("Head", x=0.4 * breath + 1.0, z=-1.5 * late)
        for side, x in (("Left", 1), ("Right", -1)):
            lift = 0.7 * breath
            pb[side + "Shoulder"].rotation_quaternion = world_turn(side + "Shoulder", y=x * (4.0 - lift))
            # arms down from the T pose, 9 degrees off the body, a little forward, the elbows softly bent
            pb[side + "UpperArm"].rotation_quaternion = world_turn(side + "UpperArm", y=x * (77.0 + lift), z=-x * 6.0)
            pb[side + "LowerArm"].rotation_quaternion = world_turn(side + "LowerArm", z=-x * (14.0 + 1.0 * breath))
            pb[side + "Hand"].rotation_quaternion = world_turn(side + "Hand", z=-x * 4.0)
            # the hips roll 1.6 degrees and move 12 mm: the legs undo the roll and lean 0.8 degrees the other way, so
            # the feet stay where they are, flat, a little apart and turned out
            pb[side + "UpperLeg"].rotation_quaternion = world_turn(side + "UpperLeg", y=-0.8 * sway - x * 1.2, z=-1.0 * sway)
            pb[side + "LowerLeg"].rotation_quaternion = world_turn(side + "LowerLeg")
            pb[side + "Foot"].rotation_quaternion = world_turn(side + "Foot", y=-0.8 * sway + x * 1.2, z=x * 5.0)
        for b in pb:
            b.keyframe_insert("rotation_quaternion", frame=f)
        pb["Hips"].keyframe_insert("location", frame=f)
    bpy.ops.object.mode_set(mode='OBJECT')
    return f"{clip}: {seconds:.0f} s at {fps} fps, made here -> {export(rig, clip, 0, n, fps)}"


if __name__ == "__main__" and "--" in sys.argv:
    a = sys.argv[sys.argv.index("--") + 1:]
    if a[0] == "idle":
        print("ANIM", idle(a[1]))
    elif a[0] == "bake":
        kv = dict(x.split("=", 1) for x in a[5:])
        action = kv.pop("action", None)
        turn = float(kv.pop("turn", 0))
        print("ANIM", bake(a[1], a[2], int(a[3]), int(a[4]), kv, action, turn))
