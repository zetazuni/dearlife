"""Phase 2 of docs/CHARACTER_PLAN.md: re-saves the CMU motion capture clips (S:\\Tools\\cmu\\fbx, from the CMU Graphics Lab
Motion Capture Database, free to use and share, not to resell) at 30 frames a second into Assets/Art/Animations/CMU.

The FBX conversions we downloaded have a frame rate of 0 in their header; Unity then assumes 1 frame a second and its
Humanoid import resamples every curve at that rate, which leaves about three poses per clip. Blender reads the key times in
seconds, so importing at 30 fps and exporting baked at 30 fps gives Unity proper clips.
Run through the Blender MCP: exec(open(r"S:\\Dearlife by Zetazuni\\tools\\blender_cmu_clips.py").read())
"""
import os
import bpy

SRC = r"S:\Tools\cmu\fbx"
OUT = r"S:\Dearlife by Zetazuni\Assets\Art\Animations\CMU"
CLIPS = {
    "111_28": "stand", "02_01": "walk", "114_05": "sit", "13_26": "wave", "111_17": "crouch",
    "79_69": "happy", "79_12": "eat", "79_13": "cook", "79_80": "read", "79_93": "exercise", "79_85": "work",
    "79_90": "wash", "79_02": "swim", "79_41": "drink", "79_19": "keys", "60_01": "dance", "18_08": "talk",
}
FPS = 30


def convert(cmu_id, name):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.render.fps_base = 1.0
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, cmu_id + ".fbx"), automatic_bone_orientation=False)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    act = arm.animation_data.action
    start, end = act.frame_range
    scene.frame_start, scene.frame_end = int(start), int(round(end))
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    out = os.path.join(OUT, f"{name}_cmu_{cmu_id}.fbx")
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE'}, add_leaf_bones=False,
                             bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
                             bake_anim_step=1.0, bake_anim_simplify_factor=0.0, apply_unit_scale=True)
    return (end - start) / FPS


print({name: round(convert(k, name), 2) for k, name in CLIPS.items()})
