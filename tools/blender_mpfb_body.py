"""Phase 0 of docs/CHARACTER_PLAN.md: a realistic adult test body from MPFB2 (MakeHuman for Blender, CC0 assets).

Makes a human with the game engine rig (53 bones, fingers included), eyes, brows, lashes and teeth, bakes the body
sliders (gender, weight, muscle, height, proportions, age within adult years) into shape keys on every mesh, records
how far each bone moves per slider (Unity moves the bones to match, see BodyShape.cs), removes MakeHuman's fitting
helpers and exports an FBX, a .materials.json (for FurnitureImport.ImportCharacters) and a .bodyshape.json.

Needs the MPFB extension enabled in Blender and the MakeHuman system asset pack loaded (see docs/PIPELINE.md).
Run through the Blender MCP: exec(open(r"S:\\Dearlife by Zetazuni\\tools\\blender_mpfb_body.py").read())
"""
import os, json, shutil
import bpy
from bl_ext.s_tools.mpfb.services.humanservice import HumanService
from bl_ext.s_tools.mpfb.services.targetservice import TargetService
from bl_ext.s_tools.mpfb.services.locationservice import LocationService
from bl_ext.s_tools.mpfb.entities.objectproperties import HumanObjectProperties

PROJECT = r"S:\Dearlife by Zetazuni"
OUT = os.path.join(PROJECT, "Assets", "Art", "Models", "Characters")
NAME = "mpfb_test"
SKIN = "young_asian_female"
PARTS = (("eyes", "high-poly", "Eyes"), ("eyebrows", "eyebrow001", "Eyebrows"), ("eyelashes", "eyelashes01", "Eyelashes"), ("teeth", "teeth_base", "Teeth"))
# a first outfit and hair from MakeHuman's CC0 system pack (phase 3 brings our own clothes and run time outfit changes)
OUTFIT = (("clothes", "female_casualsuit01", "Clothes"), ("clothes", "shoes01", "Clothes"), ("hair", "long01", "Hair"))

# MakeHuman's age slider: 0.1875 is 11 years, 0.5 is 25, 1.0 is 90. Adults only (rule 8 decisions): 18 to 60.
AGE_18 = 0.5 - (25 - 18) / (25 - 11) * (0.5 - 0.1875)
AGE_60 = 0.5 + (60 - 25) / (90 - 25) * 0.5
NEUTRAL = {"gender": 0.5, "age": 0.5, "muscle": 0.5, "weight": 0.5, "height": 0.5, "proportions": 0.5}
# (macro, value, shape key). Unity mixes the two keys of a macro as one slider: -1 = first, 0 = neutral, +1 = second.
SLIDERS = [
    ("gender", 0.0, "gender_female"), ("gender", 1.0, "gender_male"),
    ("weight", 0.0, "weight_low"), ("weight", 1.0, "weight_high"),
    ("muscle", 0.0, "muscle_low"), ("muscle", 1.0, "muscle_high"),
    # MakeHuman's height ends are about 1.2 m and 2.3 m; these values give about 1.50 m and 1.93 m, a real adult range
    ("height", 0.27, "height_short"), ("height", 0.70, "height_tall"),
    ("proportions", 0.0, "proportions_low"), ("proportions", 1.0, "proportions_ideal"),
    ("age", AGE_18, "age_18"), ("age", AGE_60, "age_60"),
]


def select_only(o):
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def build():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.outliner.orphans_purge(do_recursive=True)   # old materials would push the new names to ".001"
    bpy.ops.mpfb.create_human()
    human = bpy.data.objects["Human"]
    select_only(human)
    bpy.context.scene.MPFB_ADR_standard_rig = 'game_engine'
    bpy.context.scene.MPFB_ADR_import_weights = True
    bpy.ops.mpfb.add_standard_rig()
    for sub, folder, kind in PARTS + OUTFIT:
        d = LocationService.get_user_data(os.path.join(sub, folder))
        f = next(os.path.join(d, n) for n in os.listdir(d) if n.endswith(".mhclo"))
        select_only(human)
        bpy.ops.mpfb.load_library_clothes(filepath=f, object_type=kind, material_type='MAKESKIN')
    d = LocationService.get_user_data(os.path.join("skins", SKIN))
    select_only(human)
    bpy.ops.mpfb.load_library_skin(filepath=os.path.join(d, next(n for n in os.listdir(d) if n.endswith(".mhmat"))))
    return human


_MESHES = []


def meshes():
    """The body first, then the eyes, brows, lashes and teeth (object references, so renaming does not lose them)."""
    if not _MESHES:
        human = bpy.data.objects["Human"]
        _MESHES.extend([human] + [o for o in bpy.data.objects if o.type == 'MESH' and o is not human])
    return _MESHES


def set_macros(human, values):
    for k, v in values.items():
        HumanObjectProperties.set_value(k, v, entity_reference=human)
    TargetService.reapply_macro_details(human)
    HumanService.refit(human)


def capture():
    """Vertex positions of every mesh (no armature, no subdivision) and bone heads and tails in rig space."""
    dg = bpy.context.evaluated_depsgraph_get()
    coords = {}
    for o in meshes():
        saved = [(m, m.show_viewport) for m in o.modifiers]
        for m, _ in saved:
            m.show_viewport = False
        dg.update()
        ev = o.evaluated_get(dg)
        coords[o.name] = [tuple(v.co) for v in ev.data.vertices]
        for m, s in saved:
            m.show_viewport = s
    rig = bpy.data.objects["Human.rig"]
    bones = {b.name: (tuple(b.head_local), tuple(b.tail_local)) for b in rig.data.bones}
    return coords, bones


def run():
    _MESHES.clear()
    human = build()
    set_macros(human, NEUTRAL)
    base_coords, base_bones = capture()
    captured = []
    for macro, value, key in SLIDERS:
        set_macros(human, dict(NEUTRAL, **{macro: value}))
        captured.append((key,) + capture())
    set_macros(human, NEUTRAL)

    # the neutral body becomes the basis, every slider end a shape key on every mesh
    TargetService.bake_targets(human)
    for o in meshes():
        if not o.data.shape_keys:
            o.shape_key_add(name="Basis", from_mix=False)
        n = len(o.data.vertices)
        for key, coords, _ in captured:
            sk = o.shape_key_add(name=key, from_mix=False)
            pts = coords[o.name]
            if len(pts) != n:
                raise RuntimeError(f"{o.name}: {len(pts)} captured vs {n} vertices for {key}")
            for i in range(n):
                sk.data[i].co = pts[i]

    # bone movement per slider, in Blender rig space (BodyShape.cs turns it into Unity space)
    # lists, not dictionaries, so Unity's JsonUtility can read it
    shape = {"sliders": [], "bones": []}
    for macro in dict.fromkeys(m for m, _, _ in SLIDERS):
        ends = [k for m, _, k in SLIDERS if m == macro]
        shape["sliders"].append({"name": macro, "low": ends[0], "high": ends[1]})
    for bone, (h0, t0) in base_bones.items():
        moves = []
        for key, _, bones in captured:
            h1 = bones[bone][0]
            d = [h1[i] - h0[i] for i in range(3)]
            if max(abs(x) for x in d) > 1e-5:
                moves.append({"key": key, "d": [round(x, 6) for x in d]})
        shape["bones"].append({"name": bone, "head": [round(x, 6) for x in h0], "moves": moves})

    # MakeHuman's fitting helpers (eye, teeth, skirt and tights stand-ins) are not part of the body, and the body under the
    # clothes is never seen (its delete groups); shape keys survive deletion. Phase 3 hides covered faces at run time instead.
    select_only(human)
    keep = human.vertex_groups["body"].index
    hidden = {g.index for g in human.vertex_groups if g.name.startswith("Delete.")}
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.object.mode_set(mode='OBJECT')
    for v in human.data.vertices:
        groups = {g.group for g in v.groups if g.weight > 0.5}
        v.select = keep not in groups or bool(groups & hidden)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.delete(type='VERT')
    bpy.ops.object.mode_set(mode='OBJECT')
    for o in meshes():
        for m in list(o.modifiers):
            if m.type in ('MASK', 'SUBSURF'):   # masks are done by the deletion above
                o.modifiers.remove(m)

    # readable names, then the files
    rename = {"Human": "body"}
    for o in meshes():
        part = rename.get(o.name, o.name.replace("Human.", ""))
        o.name = f"{NAME}_{part}"
    rig = bpy.data.objects["Human.rig"]
    rig.name = NAME
    os.makedirs(OUT, exist_ok=True)
    info = {}
    for o in meshes():
        for m in o.data.materials:
            if not m or m.name in info:
                continue
            part = o.name.replace(NAME + "_", "")
            # hair, brows and lashes are cards with see-through gaps: FurnitureImport gives "cut_" materials alpha clipping
            m.name = ("cut_" + part) if any(part.startswith(p) for p in ("eyebrow", "eyelashes", "long", "short", "bob", "braid", "ponytail", "afro")) else part
            img = None
            if m.use_nodes:
                img = next((n.image for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and "diffuse" in n.name.lower()), None)
            fn = None
            if img:
                src = bpy.path.abspath(img.filepath)
                fn = f"{NAME}_{m.name}{os.path.splitext(src)[1]}"
                shutil.copyfile(src, os.path.join(OUT, fn))
            info[m.name] = {"texture": fn, "color": [0.8, 0.8, 0.8]}
    json.dump(info, open(os.path.join(OUT, NAME + ".materials.json"), "w"), indent=1)
    json.dump(shape, open(os.path.join(OUT, NAME + ".bodyshape.json"), "w"), indent=1)

    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    for o in meshes():
        o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + ".fbx"), use_selection=True, object_types={'ARMATURE', 'MESH'},
                             axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, use_mesh_modifiers=False, mesh_smooth_type='FACE', add_leaf_bones=False,
                             bake_anim=False, path_mode='STRIP', embed_textures=False)   # textures come from the .materials.json
    return {o.name: (len(o.data.vertices), len(o.data.shape_keys.key_blocks)) for o in meshes()}


print(run())
