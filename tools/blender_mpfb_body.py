"""Phases 0 to 3 of docs/CHARACTER_PLAN.md: a realistic adult body from MPFB2 (MakeHuman for Blender, CC0 assets) and a
wardrobe of clothes that can be changed while the game runs.

Makes a human with the game engine rig (53 bones, fingers included), eyes, brows, lashes and teeth, bakes the body
sliders (gender, weight, muscle, height, proportions, age within adult years) into shape keys on every mesh, records
how far each bone moves per slider (Unity moves the bones to match, see BodyShape.cs) and removes MakeHuman's fitting
helpers. Clothes and hair are exported one file each into Assets/Resources/Clothes with a wardrobe.json; the body under
each garment is not deleted but marked in a second UV channel ("hide", one bit per garment) so Wardrobe.cs can hide it
while the garment is worn. Our own garments (tunic, wide trousers, hijab, pyjamas) are made here from the body's own
surface, so they carry its slider shape keys and skin weights.

Needs the MPFB extension enabled in Blender and the MakeHuman system asset pack loaded (see docs/PIPELINE.md).
Run through the Blender MCP: exec(open(r"S:\\Dearlife by Zetazuni\\tools\\blender_mpfb_body.py").read())
"""
import os, json, shutil
import bpy
from mathutils import Vector
from bl_ext.s_tools.mpfb.services.humanservice import HumanService
from bl_ext.s_tools.mpfb.services.targetservice import TargetService
from bl_ext.s_tools.mpfb.services.locationservice import LocationService
from bl_ext.s_tools.mpfb.entities.objectproperties import HumanObjectProperties

PROJECT = r"S:\Dearlife by Zetazuni"
OUT = os.path.join(PROJECT, "Assets", "Art", "Models", "Characters")
NAME = "mpfb_test"
SKIN = "young_asian_female"
CLOTHES_OUT = os.path.join(PROJECT, "Assets", "Resources", "Clothes")
PARTS = (("eyes", "high-poly", "Eyes"), ("eyebrows", "eyebrow001", "Eyebrows"), ("eyelashes", "eyelashes01", "Eyelashes"), ("teeth", "teeth_base", "Teeth"))
# MakeHuman's own CC0 pieces: (asset folder, asset, MPFB type, our garment id, slot)
MH_WEAR = (("clothes", "female_casualsuit01", "Clothes", "casual", "outfit"), ("clothes", "shoes01", "Clothes", "shoes", "feet"),
           ("hair", "long01", "Hair", "hair_long", "hair"), ("hair", "short02", "Hair", "hair_short", "hair"))
# our own garments, made from the body's surface: (id, slot, colour, region, how far out it stands in metres)
MADE_WEAR = (("tunic", "top", (0.46, 0.55, 0.50), "top", "tunic"),
             ("trousers", "bottom", (0.20, 0.22, 0.30), "bottom", "wide"),
             ("hijab", "head", (0.62, 0.48, 0.50), "hijab", "hijab"),
             ("pyjama_top", "top", (0.78, 0.74, 0.86), "top", "loose"),
             ("pyjama_bottoms", "bottom", (0.78, 0.74, 0.86), "bottom", "loose_bottom"),
             # formal: a fitted shirt and slim trousers; outerwear: a blazer over any top (v0.46.0)
             ("shirt", "top", (0.93, 0.93, 0.93), "fitted_top", "fitted"),
             ("slacks", "bottom", (0.16, 0.17, 0.2), "bottom", "slim"),
             ("blazer", "outer", (0.18, 0.2, 0.26), "blazer", "outer"),
             # sporty
             ("tanktop", "top", (0.75, 0.42, 0.4), "tank", "fitted"),
             ("shorts", "bottom", (0.2, 0.22, 0.28), "shorts", "loose_bottom"),
             # swimwear
             ("swimsuit", "outfit", (0.12, 0.3, 0.45), "swimsuit", "tight"),
             ("swimshorts", "bottom", (0.15, 0.35, 0.5), "swimshorts", "loose_bottom"))
# facial hair: stacked shells of short strands over the beard area of the face (goatee: moustache and chin)
BEARDS = ("beard_full", "beard_goatee")
OUTFITS = {"bearded": ["casual", "shoes", "hair_short", "beard_full"],
           "casual": ["casual", "shoes", "hair_long"],
           "modest": ["tunic", "trousers", "hijab", "shoes"],
           "sleep": ["pyjama_top", "pyjama_bottoms", "hair_long"],
           "formal": ["shirt", "slacks", "blazer", "shoes", "hair_short"],
           "sporty": ["tanktop", "shorts", "shoes", "hair_long"],
           "swim": ["swimsuit", "hair_long"],
           "swim_shorts": ["swimshorts", "hair_short"]}
HIDES_HAIR = {"hijab"}

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

# Face sliders for the character creator (phase 4): each end is one or more of MakeHuman's CC0 face targets (left and
# right together), baked like the body sliders into a shape key on every mesh. (slider, low targets, high targets)
def _lr(t):
    return ["l-" + t, "r-" + t]


FACE = [
    ("faceWidth", ["head-scale-horiz-decr"], ["head-scale-horiz-incr"]),
    ("faceLength", ["head-scale-vert-decr"], ["head-scale-vert-incr"]),
    ("jaw", ["chin-width-decr", "chin-bones-decr"], ["chin-width-incr", "chin-bones-incr"]),
    ("chin", ["chin-prominent-decr"], ["chin-prominent-incr"]),
    ("cheekbones", _lr("cheek-bones-decr"), _lr("cheek-bones-incr")),
    ("eyeSize", _lr("eye-scale-decr"), _lr("eye-scale-incr")),
    ("eyeSpacing", _lr("eye-trans-in"), _lr("eye-trans-out")),
    ("noseWidth", ["nose-scale-horiz-decr"], ["nose-scale-horiz-incr"]),
    ("noseLength", ["nose-scale-vert-decr"], ["nose-scale-vert-incr"]),
    ("lips", ["mouth-upperlip-volume-decr", "mouth-lowerlip-volume-decr"], ["mouth-upperlip-volume-incr", "mouth-lowerlip-volume-incr"]),
    ("mouthWidth", ["mouth-scale-horiz-decr"], ["mouth-scale-horiz-incr"]),
    ("brows", ["eyebrows-trans-down"], ["eyebrows-trans-up"]),
    ("ears", _lr("ear-scale-decr"), _lr("ear-scale-incr")),
]


def set_targets(human, names, weight):
    keys = human.data.shape_keys.key_blocks
    for n in names:
        if n in keys:
            keys[n].value = weight
        elif weight > 0.0:
            TargetService.load_target(human, TargetService.target_full_path(n), weight=weight)
    HumanService.refit(human)


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
    for sub, folder, kind in PARTS + tuple((s, f, k) for s, f, k, _, _ in MH_WEAR):
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


# MakeHuman's own female end is still broad in the shoulders and straight in the waist. On top of it, the female end of
# our gender slider narrows the shoulders and shoulder caps, takes the V out of the torso, pulls in the waist and
# fills out the hips, seat and bust (MakeHuman's CC0 measurement targets, faded in from the neutral middle).
FEMININE = {"measure-shoulder-dist-decr": 0.7, "torso-vshape-decr": 0.5, "measure-waist-circ-decr": 0.8,
            "hip-scale-horiz-incr": 0.35, "buttocks-volume-incr": 0.45, "measure-bust-circ-incr": 0.35,
            "l-upperarm-shoulder-muscle-decr": 0.4, "r-upperarm-shoulder-muscle-decr": 0.4}


def set_macros(human, values):
    for k, v in values.items():
        HumanObjectProperties.set_value(k, v, entity_reference=human)
    TargetService.reapply_macro_details(human)
    fem = max(0.0, (0.5 - values.get("gender", 0.5)) / 0.5)
    keys = human.data.shape_keys.key_blocks
    for name, w in FEMININE.items():
        if name in keys:
            keys[name].value = w * fem
        elif fem > 0.0:
            TargetService.load_target(human, TargetService.target_full_path(name), weight=w * fem)
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


def paint_scalp(human, hairs, out_file):
    """Hair cards alone leave bare skin showing at the parting. Real game characters have a hair coloured scalp painted
    on the skin, so do the same: every body vertex within 2 cm of any hair style (above the brows) gets a weight that
    fades out from 1 cm, and those triangles are painted in the skin texture with the first hair's average colour, darkened."""
    import numpy as np
    from mathutils.kdtree import KDTree
    hair = hairs[0]
    body_mat = human.data.materials[0]
    img = next(n.image for n in body_mat.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and "diffuse" in n.name.lower())
    hair_img = next(n.image for n in hair.data.materials[0].node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and "diffuse" in n.name.lower())
    hp = np.array(hair_img.pixels[:], dtype=np.float32).reshape(-1, 4)
    hp = hp[hp[:, 3] > 0.8]
    hair_col = np.median(hp[:, :3], axis=0) * 0.55
    kd = KDTree(sum(len(hh.data.vertices) for hh in hairs))
    i = 0
    for hh in hairs:
        for v in hh.data.vertices:
            kd.insert(hh.matrix_world @ v.co, i)
            i += 1
    kd.balance()
    weight = np.zeros(len(human.data.vertices), dtype=np.float32)
    for i, v in enumerate(human.data.vertices):
        p = human.matrix_world @ v.co
        if p.z < 1.47:
            continue
        d = kd.find(p)[2]
        weight[i] = min(1.0, max(0.0, (0.02 - d) / 0.01))
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    uvs = human.data.uv_layers.active.data
    painted = 0
    for poly in human.data.polygons:
        vw = [weight[j] for j in poly.vertices]
        if max(vw) <= 0.0:
            continue
        pts = [(uvs[l].uv[0] * w, uvs[l].uv[1] * h) for l in poly.loop_indices]
        for k in range(1, len(pts) - 1):
            tri, tw = (pts[0], pts[k], pts[k + 1]), (vw[0], vw[k], vw[k + 1])
            x0, x1 = int(max(0, min(p[0] for p in tri))), int(min(w - 1, max(p[0] for p in tri) + 1))
            y0, y1 = int(max(0, min(p[1] for p in tri))), int(min(h - 1, max(p[1] for p in tri) + 1))
            if x1 < x0 or y1 < y0:
                continue
            xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            (ax, ay), (bx, by), (cx, cy) = tri
            den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(den) < 1e-9:
                continue
            l1 = ((by - cy) * (xs - cx) + (cx - bx) * (ys - cy)) / den
            l2 = ((cy - ay) * (xs - cx) + (ax - cx) * (ys - cy)) / den
            l3 = 1.0 - l1 - l2
            inside = (l1 >= -0.01) & (l2 >= -0.01) & (l3 >= -0.01)
            a = np.clip(l1 * tw[0] + l2 * tw[1] + l3 * tw[2], 0.0, 1.0) * 0.92
            region = px[y0:y1 + 1, x0:x1 + 1, :3]
            blend = np.where(inside[..., None], a[..., None], 0.0)
            px[y0:y1 + 1, x0:x1 + 1, :3] = region * (1.0 - blend) + hair_col * blend
            painted += 1
    out = bpy.data.images.new("scalp_skin", w, h, alpha=True)
    out.pixels = px.ravel()
    out.filepath_raw = out_file
    out.file_format = 'PNG'
    out.save()
    bpy.data.images.remove(out)
    return painted


def dominant_bones(o, bones):
    """For every vertex, the rig bone that moves it most (MakeHuman also has non bone groups such as "Left" and "lips")."""
    names = {g.index: g.name for g in o.vertex_groups if g.name in bones}
    out = []
    for v in o.data.vertices:
        best = max(((g.weight, names[g.group]) for g in v.groups if g.group in names), default=(0.0, ""))
        out.append(best[1])
    return out


WRISTS = []   # hand bone heads, set by run()


def region(kind, co, no, bone):
    """Is this body vertex under a garment of this kind? Heights are for the neutral body (1.67 m, arms in an A pose)."""
    z = co.z
    arm = bone.startswith(("clavicle", "upperarm", "lowerarm"))
    if kind == "top":            # long sleeves to just short of the wrist, crew neck, hem over the hips
        # the cuff stops 4 cm before the wrist joint, or a bent hand would dip into it
        if bone.startswith("lowerarm") and any((co - Vector(w)).length < 0.04 for w in WRISTS):
            return False
        # the crew neck dips at the front, clear of the chin when the head bends forward
        if co.y < -0.02 and z > 1.35 and (bone in ("neck_01", "spine_03") or bone.startswith("clavicle")):
            return False
        if bone.startswith("spine") or arm:
            return True
        if bone == "neck_01":
            return z < 1.41
        # a high-low hem: over the seat at the back, curving up to 0.92 m at the front, clear of the hip crease when
        # the legs bend (a hem lower there folds into the crease, under the trousers)
        hem = 0.86 + 0.06 * min(1.0, max(0.0, (-co.y - 0.02) / 0.06))
        return (bone == "pelvis" or bone.startswith("thigh")) and z >= hem
    if kind == "fitted_top":     # a shirt: like the top, with a straight hem at the hips
        if bone.startswith("lowerarm") and any((co - Vector(w)).length < 0.04 for w in WRISTS):
            return False
        if co.y < -0.02 and z > 1.36 and (bone in ("neck_01", "spine_03") or bone.startswith("clavicle")):
            return False
        if bone.startswith("spine") or arm:
            return True
        if bone == "neck_01":
            return z < 1.40
        hem = 0.86 + 0.06 * min(1.0, max(0.0, (-co.y - 0.02) / 0.06))      # over the seat at the back, up at the front
        return (bone == "pelvis" or bone.startswith("thigh")) and z >= hem
    if kind == "tank":           # no sleeves, two straps over the shoulders, a scooped neck, wide arm holes
        ax = abs(co.x)
        if bone.startswith(("upperarm", "lowerarm")):
            return False
        if z > 1.30 and not (0.07 <= ax <= 0.115):
            return False             # only the straps go up over the shoulders
        if bone.startswith("spine_03") and ax > 0.125 and z > 1.14:
            return False             # the arm holes
        if co.y < -0.02 and z > 1.24 and ax < 0.07:
            return False             # the scoop at the front
        if bone.startswith("spine") or bone.startswith("clavicle"):
            return True
        return (bone == "pelvis" or bone.startswith("thigh")) and z >= 0.90
    if kind == "blazer":         # hip length with long sleeves and a V opening at the front over the shirt
        if bone.startswith("lowerarm") and any((co - Vector(w)).length < 0.045 for w in WRISTS):
            return False
        if co.y < -0.02 and z > 1.10 and abs(co.x) < 0.012 + (z - 1.10) * 0.3:
            return False             # the V
        if co.y < -0.02 and z > 1.36:
            return False
        if bone.startswith("spine") or arm:
            return True
        if bone == "neck_01":
            return co.y > -0.02 and z < 1.41
        return (bone == "pelvis" or bone.startswith("thigh")) and z >= 0.80
    if kind == "shorts":         # waist to above the knee
        if bone == "pelvis" or bone.startswith(("thigh", "calf")):
            return 0.56 <= z <= 0.98
        return bone == "spine_01" and z <= 0.98
    if kind == "swimshorts":     # waist to mid thigh
        if bone == "pelvis" or bone.startswith("thigh"):
            return 0.62 <= z <= 0.97
        return bone == "spine_01" and z <= 0.97
    if kind == "swimsuit":       # a one piece: straps, a scooped neck and back, high cut legs on a smooth line
        ax = abs(co.x)
        if bone.startswith(("upperarm", "lowerarm", "calf", "foot")):
            return False
        if z > 1.28 and not (0.06 <= ax <= 0.10):
            return False
        if bone.startswith("spine_03") and ax > 0.12 and z > 1.13:
            return False
        if co.y < -0.02 and z > 1.21 and ax < 0.06:
            return False             # the scoop at the front
        if co.y >= -0.02 and z > 1.17 and ax < 0.06:
            return False             # and at the back
        if bone == "pelvis" or bone.startswith("thigh"):
            return z >= swim_leg(ax) - 0.035      # cut a little low; make_garment lifts the edge onto the exact line
        return bone.startswith("spine") or bone.startswith("clavicle")
    if kind == "bottom":         # waist to just above the ankle bone
        if bone == "pelvis" or bone.startswith(("thigh", "calf")):
            return 0.115 <= z <= 0.98
        return bone == "spine_01" and z <= 0.98
    if kind == "hijab":          # head, neck and shoulders, with an oval opening for the face
        if bone == "head":
            f = face_oval(co)
            # the middle of the face is always open (lips and nostrils face up and down); near its edge only what faces
            # forward is, so the fabric still passes under the chin
            return not (co.y < -0.04 and z > 1.44 and (f < 0.75 or (f < 1.0 and no.y < -0.2 and no.z > -0.45)))
        if bone == "neck_01":
            return True
        return bone.startswith(("spine_03", "clavicle", "upperarm")) and z > 1.25
    return False


FACE_OVAL = (0.064, 1.525, 0.090)   # half width, centre height, half height of the hijab's face opening


def swim_leg(ax):
    """The swimsuit's leg line (ax, the distance from the middle, a number or an array): low between the legs, rising
    over the hips."""
    import numpy as np
    return 0.79 + 0.10 * np.clip((np.asarray(ax, dtype=float) - 0.03) / 0.08, 0.0, 1.0)


def face_oval(co):
    w, zc, h = FACE_OVAL
    return (co[0] / w) ** 2 + ((co[2] - zc) / h) ** 2


# (tension passes, share of the offset the fabric always keeps from the skin)
SMOOTHING = {"tunic": (100, 0.7), "loose": (100, 0.7), "wide": (60, 0.7), "loose_bottom": (60, 0.7), "hijab": (120, 0.6),
             "fitted": (80, 0.75), "outer": (100, 0.75), "slim": (60, 0.75), "tight": (30, 0.85)}


def offset(style, co, no, bone):
    """How far the fabric stands off the skin, in metres."""
    z = co.z
    if style in ("tunic", "loose") and not (bone.startswith("thigh") or bone == "pelvis"):
        base = 0.012 if style == "tunic" else 0.018
        if bone.startswith("lowerarm"):
            base += 0.004
        return base
    if style in ("tunic", "loose"):     # the hem, over the hips and outside the trousers' waist
        return 0.020 if style == "tunic" else 0.024
    if style == "fitted":
        if bone.startswith("thigh") or bone == "pelvis":
            return 0.019
        if bone.startswith("spine") and z < 1.02:          # easing out towards the hem, over a waistband
            return 0.007 + 0.010 * min(1.0, (1.02 - z) / 0.1)
        if co.y < -0.04 and 1.24 < z < 1.40:                 # over the chest, where a crouch pushes it forward
            return 0.0095
        return 0.007 + (0.003 if bone.startswith("lowerarm") else 0.0)
    if style == "outer":             # a blazer has room for a tunic or a shirt under it
        if bone.startswith("thigh") or bone == "pelvis":
            return 0.036
        return (0.022 + (0.004 if bone.startswith("lowerarm") else 0.0)) if bone.startswith(("upperarm", "lowerarm")) else 0.028
    if style == "tight":
        return 0.0035 if z > 0.92 else 0.0035 + 0.009 * min(1.0, (0.92 - z) / 0.08)   # more room over the hip creases
    if style in ("wide", "loose_bottom", "slim"):
        t = min(1.0, max(0.0, (0.95 - z) / 0.85))
        off = (0.010 + 0.030 * t) if style == "wide" else (0.012 + 0.016 * t) if style == "loose_bottom" else (0.008 + 0.010 * t)
        # the inner thighs nearly touch: keep the fabric there close, or the two legs would meet
        if z > 0.55 and co.x * no.x < 0 and abs(no.x) > 0.3:
            off = min(off, 0.008)
        return off
    if style == "hijab":
        import math
        # a little fullness at the back of the head, where the hair is gathered under the scarf
        bun = 0.014 * math.exp(-((z - 1.56) / 0.05) ** 2) if co.y > 0.0 else 0.0
        # room under the chin and at the throat, where the chin comes down when the head bends forward
        throat = 0.014 if (co.y < -0.02 and 1.37 < z < 1.47) else 0.0
        if z > 1.45:
            return 0.016 + bun + throat
        # and at the front of the shoulders, where a raised arm lifts the sleeve under the drape
        front_shoulder = 0.008 if (co.y < -0.04 and abs(co.x) > 0.12 and z < 1.32) else 0.0
        return 0.016 + bun + throat + front_shoulder + 0.020 * min(1.0, (1.45 - z) / 0.12)
    return 0.01


def tri_normals(co, tris):
    import numpy as np
    a, b, c = co[tris[:, 0]], co[tris[:, 1]], co[tris[:, 2]]
    fn = np.cross(b - a, c - a)
    vn = np.zeros_like(co)
    for k in range(3):
        np.add.at(vn, tris[:, k], fn)
    return vn / np.maximum(np.linalg.norm(vn, axis=1, keepdims=True), 1e-9)


def neighbour_mean(co, e0, e1, deg):
    import numpy as np
    s = np.zeros_like(co)
    np.add.at(s, e0, co[e1])
    np.add.at(s, e1, co[e0])
    return s / np.maximum(deg, 1)[:, None]


def make_garment(human, gid, colour, kind, style, bones):
    """A copy of the body faces under the garment, with the body's slider shape keys and skin weights, subdivided once
    and pushed outward along each shape's own normals so the fabric follows every body shape. Returns the object and
    the body vertices it covers."""
    import numpy as np
    dom = dominant_bones(human, bones)
    inside = [region(kind, v.co, v.normal, dom[v.index]) for v in human.data.vertices]
    # a stray vertex or two left out of the region would be a hole in the fabric: fill any small uncovered island
    links = [[] for _ in inside]
    for e in human.data.edges:
        a, b = e.vertices
        links[a].append(b)
        links[b].append(a)
    seen = [False] * len(inside)
    for start in range(len(inside)):
        if inside[start] or seen[start]:
            continue
        island, stack = [], [start]
        seen[start] = True
        while stack:
            i = stack.pop()
            island.append(i)
            for j in links[i]:
                if not inside[j] and not seen[j]:
                    seen[j] = True
                    stack.append(j)
        if len(island) < 40:
            for i in island:
                inside[i] = True
    # vertices whose every neighbour is also under the garment: only those hide the body, so a ring of skin stays
    # under the hem and cuffs and no gap shows between skin and fabric
    covered = list(inside)
    for e in human.data.edges:
        a, b = e.vertices
        if inside[a] != inside[b]:
            covered[a] = covered[b] = False
    if style == "tight":  # the fabric ends exactly on the leg line: the skin is hidden above it and shown below it
        for v in human.data.vertices:
            b = dom[v.index]
            if (b == "pelvis" or b.startswith("thigh")) and 0.7 < v.co.z < 1.0:
                covered[v.index] = inside[v.index] and v.co.z >= swim_leg(abs(v.co.x)) + 0.004
    if kind == "hijab":   # the face edge is later slid onto the true oval: keep one more ring of skin round the face
        edge = [not c for c in covered]
        for e in human.data.edges:
            a, b = e.vertices
            if human.data.vertices[a].co.z > 1.40 and (edge[a] or edge[b]):
                covered[a] = covered[b] = False
    g = human.copy()
    g.data = human.data.copy()
    g.name = gid
    bpy.context.collection.objects.link(g)
    for m in list(g.modifiers):
        if m.type != 'ARMATURE':
            g.modifiers.remove(m)
    select_only(g)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.object.mode_set(mode='OBJECT')
    for v in g.data.vertices:
        v.select = not inside[v.index]
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.delete(type='VERT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.subdivide(number_cuts=1, smoothness=0.0)   # shape keys and weights are carried to the new vertices
    bpy.ops.object.mode_set(mode='OBJECT')
    if style == "tight":
        # the swimsuit is cut a little low and its leg line is an alpha mask: faces wholly below the line go, so only the
        # row of faces the line crosses is left for the mask to cut
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_mode(type='FACE')
        bpy.ops.mesh.select_all(action='DESELECT')
        bpy.ops.object.mode_set(mode='OBJECT')
        for pl in g.data.polygons:
            pl.select = all(g.data.vertices[i].co.z < swim_leg(abs(g.data.vertices[i].co.x)) - 0.003 for i in pl.vertices)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.delete(type='FACE')
        bpy.ops.mesh.select_mode(type='VERT')
        bpy.ops.object.mode_set(mode='OBJECT')
    me = g.data
    n = len(me.vertices)
    me.calc_loop_triangles()
    tris = np.array([t.vertices[:] for t in me.loop_triangles], dtype=np.int64)
    ev = np.array([e.vertices[:] for e in me.edges], dtype=np.int64)
    deg = np.bincount(ev.ravel(), minlength=n).astype(np.float64)
    counts = {}
    for p in me.polygons:
        for ek in p.edge_keys:
            counts[ek] = counts.get(ek, 0) + 1
    border = np.array([ek for ek, c in counts.items() if c == 1], dtype=np.int64).reshape(-1, 2)
    on_border = np.zeros(n, dtype=bool)
    on_border[border.ravel()] = True
    bdeg = np.bincount(border.ravel(), minlength=n).astype(np.float64)
    ring = np.full(n, 99)                  # edges away from the garment's edge
    ring[on_border] = 0
    for r in range(1, 8):
        near = np.zeros(n, dtype=bool)
        prev = ring == r - 1
        near[ev[:, 1][prev[ev[:, 0]]]] = True
        near[ev[:, 0][prev[ev[:, 1]]]] = True
        ring[near & (ring == 99)] = r
    # small shapes fabric bridges over instead of following: ears under a hijab, nipples under a top, the crotch and
    # the cleft of the seat under trousers. They become a membrane stretched over them (the body under is hidden).
    flat = np.zeros(n, dtype=bool)
    snap = np.zeros((n, 3))
    groups = {"hijab": ("ears",), "tunic": ("nipple", "nippleTip"), "loose": ("nipple", "nippleTip"), "fitted": ("nipple", "nippleTip"),
              "outer": ("nipple", "nippleTip"), "tight": ("nipple", "nippleTip")}.get(style, ())
    gids = {g.vertex_groups[x].index for x in groups if x in g.vertex_groups}
    for v in me.vertices:
        flat[v.index] = any(x.group in gids and x.weight > 0.1 for x in v.groups)
        if style in ("wide", "loose_bottom", "slim", "tight") and abs(v.co.x) < 0.05 and 0.70 < v.co.z < 0.87:
            flat[v.index] = True
        if style in ("slim", "loose_bottom") and abs(v.co.x) < 0.05 and v.co.y > 0.02 and v.co.z < 0.95:
            flat[v.index] = True     # the top of the seat's cleft, bridged like the shirt over it
        if style in ("tunic", "loose", "fitted", "outer", "tight") and abs(v.co.x) < 0.05 and v.co.y > 0.02 and v.co.z < 0.95:
            flat[v.index] = True     # the top of the seat's cleft, bridged like the trousers under it
    if flat.any():
        for _ in range(2 if style in ("wide", "loose_bottom", "slim", "tight") else 5):   # and a little around them
            grow = flat.copy()
            grow[ev[:, 1][flat[ev[:, 0]]]] = True
            grow[ev[:, 0][flat[ev[:, 1]]]] = True
            flat = grow
    flat &= ~on_border
    flat &= ring >= 6    # never next to an edge: by the hijab's face opening the skin shows, and fabric must stay over it
    if style == "hijab":
        # the face opening follows the mesh's quads in steps: slide its edge onto the true oval
        for v in me.vertices:
            if on_border[v.index] and v.co.z > 1.40 and v.co.y < -0.02:
                f = face_oval(v.co)
                if f > 1.0:
                    k = 1.0 / f ** 0.5
                    w, zc, h = FACE_OVAL
                    snap[v.index] = (v.co.x * k - v.co.x, 0.0, (zc + (v.co.z - zc) * k) - v.co.z)
    if style == "tight":
        # corners of the faces the leg line crosses may still hang well below it: lift just those to 3 mm under the line
        # (the alpha mask cuts the rest), so no fabric sits deep in the skin below the opening
        for v in me.vertices:
            if v.co.z < 1.0:
                low = swim_leg(abs(v.co.x)) - 0.003
                if v.co.z < low:
                    snap[v.index] = (0.0, 0.0, low - v.co.z)
    gdom = dominant_bones(g, bones)
    basis = np.array([v.co[:] for v in me.vertices])
    bn = tri_normals(basis, tris)
    from mathutils import Vector
    offs = np.array([offset(style, Vector(basis[i]), Vector(bn[i]), gdom[i]) for i in range(n)])
    if style == "hijab":   # the face opening wraps close around the face, like an underscarf
        face_edge = np.array([gdom[i] == "head" for i in range(n)]) & (ring < 6)
        offs = np.where(face_edge, offs * (0.15 + 0.85 * ring / 6.0), offs)
    passes, keep = SMOOTHING[style]
    for kb in me.shape_keys.key_blocks:
        skin = np.empty(n * 3)
        kb.data.foreach_get("co", skin)
        skin = skin.reshape(n, 3) + snap
        # the skin the fabric is laid on has no ears, nipples or crotch: those parts become a membrane over the hollow
        for _ in range(60 if flat.any() else 0):
            skin = np.where(flat[:, None], neighbour_mean(skin, ev[:, 0], ev[:, 1], deg), skin)
        nrm = tri_normals(skin, tris)
        co = skin + nrm * offs[:, None]
        # cloth under tension: each pass pulls every vertex towards its neighbours (a stretched membrane bridges the
        # hollows: under the bust, the navel, the spine, the neck) and then pushes it back out to its least distance
        # from the skin. Edge vertices only follow the edge, which rounds off the stepped hems.
        lowest = offs * keep
        for _ in range(passes):
            move = neighbour_mean(co, ev[:, 0], ev[:, 1], deg) - co
            if len(border):
                bmove = neighbour_mean(co, border[:, 0], border[:, 1], bdeg) - co
                move = np.where(on_border[:, None], bmove, move)
            co = co + 0.5 * move
            s = ((co - skin) * nrm).sum(axis=1)
            co = co + nrm * np.maximum(0.0, lowest - s)[:, None]
        kb.data.foreach_set("co", co.ravel())
        if kb == me.shape_keys.key_blocks[0]:
            me.vertices.foreach_set("co", co.ravel())
    me.update()
    despike(g)
    rebind_weights(g, human, bones)
    me.materials.clear()
    mat = bpy.data.materials.new("cloth_" + gid)
    mat.diffuse_color = (*colour, 1.0)
    me.materials.append(mat)
    for p in me.polygons:
        p.material_index = 0
    return g, covered


LAYER = {"feet": 0, "bottom": 1, "top": 2, "outfit": 2, "outer": 2.5, "head": 3}


def clash(a, b):
    return {a, b} in ({"outfit", "top"}, {"outfit", "bottom"})


# each bottom is worn with its own top (the creator's everyday looks); the blazer, the hijab and shoes go with any of them
TOP_FOR = {"trousers": {"tunic"}, "slacks": {"shirt"}, "shorts": {"tanktop"}, "pyjama_bottoms": {"pyjama_top"}}
TOPS = {"tunic", "shirt", "tanktop", "pyjama_top"}


# the blazer goes over the T-shirt, the shirt or the tank top (over a long tunic it would hang wrong), and their bottoms
UNDER_BLAZER = {"casual", "shirt", "tanktop", "slacks", "shorts", "shoes"}


def worn_together(inner, outer):
    if inner in TOP_FOR and outer in TOPS:
        return outer in TOP_FOR[inner]
    if outer == "blazer":
        return inner in UNDER_BLAZER
    return True


def beard_marks(human_parts):
    """Where the eyes and the mouth are on the neutral body (from MakeHuman's eye and teeth meshes)."""
    import numpy as np
    eye = next(o for o in human_parts if o.name.endswith("high-poly"))
    teeth = next(o for o in human_parts if o.name.endswith("teeth_base"))
    ev = np.array([v.co[:] for v in eye.data.vertices])
    tv = np.array([v.co[:] for v in teeth.data.vertices])
    e = ev[ev[:, 0] > 0]
    ec = (e.min(0) + e.max(0)) / 2
    mouth = np.array([0.0, tv[:, 1].min(), tv[:, 2].min() * 0.45 + tv[:, 2].max() * 0.55])
    mw = (tv[:, 0].max() - tv[:, 0].min()) / 2
    return {"eye": ec, "mouth": mouth, "mw": mw}


def _ss(e0, e1, x):
    t = min(1.0, max(0.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)


def beard_region(which, co, no, m):
    """Is this body vertex under the beard (the same shapes as the painted beard of tools/blender_skin_layers.py)?"""
    x, y, z = co
    ax = abs(x)
    mouth, mw, ec = m["mouth"], m["mw"], m["eye"]
    if y > 0.0 or z < 1.38 or z > 1.58:
        return False
    # the outside of the face only (the inside of the mouth faces inwards)
    out = (co[0], co[1] + 0.04, co[2] - 1.5)
    if no[0] * out[0] + no[1] * out[1] + no[2] * out[2] < 0.0:
        return False
    if (x / (mw * 1.12)) ** 2 + ((z - mouth[2]) / 0.0145) ** 2 < 1.0:
        return False                                            # the lips
    dz = z - mouth[2]
    if which == "beard_goatee":
        must = 0.004 < dz < 0.02 and ax < mw * 1.1 - max(0.0, dz - 0.008) * 1.4 and y < mouth[1] + 0.02
        sides = mw * 0.8 < ax < mw * 1.15 and -0.03 < dz < 0.008 and y < mouth[1] + 0.025
        chin = (x / 0.022) ** 2 + ((dz + 0.034) / 0.022) ** 2 < 1 and y < mouth[1] + 0.035
        return must or sides or chin
    ear_z, nose_z = ec[2] - 0.012, mouth[2] + 0.024
    top = nose_z + (ear_z - nose_z) * _ss(0.022, 0.075, ax)
    jaw_z = 1.425 + _ss(0.03, 0.075, ax) * 0.05
    return ax < 0.085 and y < 0.01 and jaw_z <= z <= top


def beard_strands(n_shells, size=1024, seed=5):
    """An atlas of n_shells tiles side by side: hair follicles (round dots in alpha), fewer and finer towards the top
    shell, so the stacked shells read as short tapering hairs. Grey, the hair shader tints it."""
    import numpy as np
    rnd = np.random.default_rng(seed)
    W = size * n_shells
    img = np.zeros((size, W, 4), np.float32)
    spacing = 5.0                     # pixels between hairs: a tile is 0.2 m, about 0.2 mm a pixel
    n = int((size / spacing) ** 2)
    px = rnd.uniform(0, size, n); py = rnd.uniform(0, size, n)
    length = rnd.uniform(0.45, 1.0, n)   # how many of the shells each hair reaches
    shade = rnd.uniform(0.62, 0.82, n)
    yy, xx = np.mgrid[-3:4, -3:4]
    for i in range(n_shells):
        h = (i + 0.5) / n_shells
        live = length > h
        r = 1.9 * (1.0 - 0.55 * h)
        for k in np.nonzero(live)[0]:
            cx, cy = px[k], py[k]
            ix, iy = int(cx), int(cy)
            d = np.sqrt((xx + ix - cx) ** 2 + (yy + iy - cy) ** 2)
            a = np.clip(r + 0.5 - d, 0, 1)
            ys = (yy + iy) % size; xs = (xx + ix) % size + i * size
            img[ys, xs, 3] = np.maximum(img[ys, xs, 3], a)
            img[ys, xs, 0:3] = np.maximum(img[ys, xs, 0:3], shade[k] * a[..., None])
    img[..., 0:3] = np.where(img[..., 3:4] > 0, img[..., 0:3] / np.maximum(img[..., 3:4], 1e-3), 0.72)
    return img


def make_beard(human, gid, marks, n_shells=6):
    """Facial hair as n_shells stacked copies of the beard area of the skin, each a little further out and down (the
    hair grows downwards), with the strands of beard_strands in its alpha. Carries the body's shape keys and weights."""
    import numpy as np, bmesh, tempfile
    inside = [beard_region(gid, v.co, v.normal, marks) for v in human.data.vertices]
    g = human.copy()
    g.data = human.data.copy()
    g.name = gid
    bpy.context.collection.objects.link(g)
    for mod in list(g.modifiers):
        if mod.type != 'ARMATURE':
            g.modifiers.remove(mod)
    select_only(g)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.object.mode_set(mode='OBJECT')
    for v in g.data.vertices:
        v.select = not inside[v.index]
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.delete(type='VERT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.subdivide(number_cuts=1, smoothness=0.0)
    bpy.ops.object.mode_set(mode='OBJECT')
    me = g.data
    n = len(me.vertices)
    me.calc_loop_triangles()
    tris = np.array([t.vertices[:] for t in me.loop_triangles], dtype=np.int64)
    # rings in from the edge: the outer shells leave the edge out, so the beard thins off softly
    ev = np.array([e.vertices[:] for e in me.edges], dtype=np.int64)
    counts = {}
    for pl in me.polygons:
        for ek in pl.edge_keys:
            counts[ek] = counts.get(ek, 0) + 1
    ring = np.full(n, 99)
    for ek, c in counts.items():
        if c == 1:
            ring[list(ek)] = 0
    for r in range(1, 8):
        prev = ring == r - 1
        near = np.zeros(n, dtype=bool)
        near[ev[:, 1][prev[ev[:, 0]]]] = True
        near[ev[:, 0][prev[ev[:, 1]]]] = True
        ring[near & (ring == 99)] = r
    keys = [(kb.name, np.array([d.co[:] for d in kb.data])) for kb in me.shape_keys.key_blocks]
    basis = keys[0][1]
    shells = []
    for i in range(n_shells):
        d = g.copy(); d.data = g.data.copy(); d.name = f"{gid} shell {i}"
        bpy.context.collection.objects.link(d)
        t = 0.0012 + i * 0.0016
        for name, co in keys:
            nrm = tri_normals(co, tris)
            out = co + nrm * t + np.array([0.0, -0.15, -1.0]) * (t * 0.45)   # a little down and forward, like combed hair
            kb = d.data.shape_keys.key_blocks[name]
            kb.data.foreach_set("co", out.ravel())
            if name == keys[0][0]:
                d.data.vertices.foreach_set("co", out.ravel())
        uv = d.data.uv_layers.get("UVMap") or d.data.uv_layers.new(name="UVMap")
        for loop in d.data.loops:
            p = basis[loop.vertex_index]
            u = min(0.999, max(0.001, (p[0] + 0.1) / 0.2)); v = min(0.999, max(0.001, (p[2] - 1.38) / 0.2))
            uv.data[loop.index].uv = ((i + u) / n_shells, v)
        # the outer shells leave out the faces at the edge
        bm = bmesh.new(); bm.from_mesh(d.data)
        bm.verts.ensure_lookup_table()
        edge_faces = [f for f in bm.faces if min(ring[vv.index] for vv in f.verts) < i // 2]
        bmesh.ops.delete(bm, geom=edge_faces, context='FACES')
        bm.to_mesh(d.data); bm.free()
        shells.append(d)
    bpy.data.objects.remove(g, do_unlink=True)
    select_only(shells[0])
    for d in shells[1:]:
        d.select_set(True)
    bpy.context.view_layer.objects.active = shells[0]
    bpy.ops.object.join()
    b = bpy.context.view_layer.objects.active
    b.name = gid
    for l in list(b.data.uv_layers):
        if l.name != "UVMap":
            b.data.uv_layers.remove(l)
    # the strands texture, on a material the importer gives the hair shader ("cut_" + the piece)
    img = beard_strands(n_shells)
    path = os.path.join(tempfile.gettempdir(), "beard_strands.png")
    im = bpy.data.images.new("beard_strands", img.shape[1], img.shape[0], alpha=True)
    im.pixels[:] = img[::-1].ravel()
    im.filepath_raw = path; im.file_format = 'PNG'; im.save()
    b.data.materials.clear()
    mat = bpy.data.materials.new(gid); mat.use_nodes = True
    tex = mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.name = "diffuse"; tex.image = im
    b.data.materials.append(mat)
    for pl in b.data.polygons:
        pl.material_index = 0
    return b


def swim_mask(body, size=2048):
    """The swimsuit's leg openings as an alpha mask on the body's UV layout (the garment keeps the body's UVs): opaque
    above the smooth leg line, clear below it. The fabric's own edge lies a little lower, so the line is exact."""
    import sys, numpy as np
    tools = os.path.join(PROJECT, "tools")
    if tools not in sys.path:
        sys.path.insert(0, tools)
    import importlib, blender_texel
    importlib.reload(blender_texel)
    P = blender_texel.texel_positions(body, size, None, "UVMap")
    x = np.nan_to_num(P[..., 0]); z = np.nan_to_num(P[..., 2], nan=2.0)
    line = swim_leg(np.abs(x))
    alpha = np.clip((z - line) / 0.0015 + 0.5, 0, 1)          # a soft 1.5 mm edge
    alpha[z > 1.0] = 1.0
    img = np.ones((size, size, 4), np.float32)
    img[..., 3] = alpha
    return img


def wear_group(gid):
    """Pieces are only fitted inside pieces they can be worn with: pyjamas with pyjamas, swimwear with nothing."""
    if gid.startswith("pyjama"):
        return "sleep"
    if gid.startswith("swim"):
        return "swim"
    if gid.startswith("beard"):
        return "beard"
    return "day"


def despike(o, limit=0.01):
    """In every shape key, a vertex whose change from the basis strays more than limit from its neighbours' changes
    (a stray spike from the smoothing in a hollow) takes their average change instead."""
    import numpy as np
    me = o.data
    n = len(me.vertices)
    ev = np.array([e.vertices[:] for e in me.edges], dtype=np.int64)
    deg = np.bincount(ev.ravel(), minlength=n).astype(np.float64)
    keys = me.shape_keys.key_blocks
    base = np.empty(n * 3); keys[0].data.foreach_get("co", base); base = base.reshape(n, 3)
    fixed = 0
    for kb in keys[1:]:
        co = np.empty(n * 3); kb.data.foreach_get("co", co); co = co.reshape(n, 3)
        for _ in range(2):
            d = co - base
            avg = neighbour_mean(d, ev[:, 0], ev[:, 1], deg)
            bad = np.linalg.norm(d - avg, axis=1) > limit
            fixed += int(bad.sum())
            co = np.where(bad[:, None], base + avg, co)
        kb.data.foreach_set("co", co.ravel())
    if fixed:
        print(f"despike {o.name}: {fixed}")


def rebind_weights(g, human, bones):
    """Smoothing slides garment vertices over the skin, but each kept the skin weights of the body point it was copied
    from; two layers at one spot would then follow slightly different bones and cross when a joint bends. Every garment
    vertex takes the weights of the body point right under it now (interpolated over that body face)."""
    from mathutils.bvhtree import BVHTree
    from mathutils.interpolate import poly_3d_calc
    body = human.data
    bvh = BVHTree.FromPolygons([v.co for v in body.vertices], [tuple(p.vertices) for p in body.polygons])
    names = {grp.index: grp.name for grp in human.vertex_groups if grp.name in bones}
    bw = [{names[x.group]: x.weight for x in v.groups if x.group in names} for v in body.vertices]
    dst = {name: g.vertex_groups.get(name) or g.vertex_groups.new(name=name) for name in names.values()}
    everyone = list(range(len(g.data.vertices)))
    for grp in dst.values():
        grp.remove(everyone)
    for v in g.data.vertices:
        loc, nor, idx, d = bvh.find_nearest(v.co)
        if loc is None:
            continue
        poly = body.polygons[idx].vertices
        f = poly_3d_calc([body.vertices[i].co for i in poly], loc)
        mix = {}
        for i, fw in zip(poly, f):
            for name, w in bw[i].items():
                mix[name] = mix.get(name, 0.0) + w * fw
        total = sum(mix.values()) or 1.0
        for name, w in mix.items():
            if w / total > 0.001:
                dst[name].add([v.index], w / total, 'REPLACE')


def cover_under(human, o, delete_cov):
    """MakeHuman's delete groups are cautious (the armpits and the collar of a T shirt stay), so a body vertex also counts
    as covered when the garment lies right over it: its nearest garment vertex is within 2.5 cm, not on the garment's
    edge, and outside the skin. MakeHuman's garments hug the skin at their edges, so no ring of skin is kept under them
    (it would show through the fabric there)."""
    import bmesh
    from mathutils.kdtree import KDTree
    bm = bmesh.new()
    bm.from_mesh(o.data)
    edge = {v.index for v in bm.verts if v.is_boundary}
    bm.free()
    kd = KDTree(len(o.data.vertices))
    for v in o.data.vertices:
        kd.insert(v.co, v.index)
    kd.balance()
    inside = []
    for v in human.data.vertices:
        co, i, d = kd.find(v.co)
        near = d < 0.025 and i not in edge and (co - v.co).dot(v.normal) > -0.003
        inside.append(near or bool(delete_cov and delete_cov[v.index]))
    return inside


def stand_off(o, base=0.0015, edge=0.004, rings=3):
    """MakeHuman's garments lie right on the skin; a bent neck or shoulder then pushes skin through a collar or a
    sleeve's end. Lift them a little off the skin, more at their edges, in every shape key."""
    import numpy as np, bmesh
    bm = bmesh.new()
    bm.from_mesh(o.data)
    n = len(bm.verts)
    ring = np.full(n, 99)
    for v in bm.verts:
        if v.is_boundary:
            ring[v.index] = 0
    for r in range(1, rings + 1):
        for e in bm.edges:
            a, b = e.verts[0].index, e.verts[1].index
            if ring[a] == r - 1 and ring[b] > r:
                ring[b] = r
            if ring[b] == r - 1 and ring[a] > r:
                ring[a] = r
    bm.free()
    lift = np.where(ring <= rings, edge - (edge - base) * ring / rings, base)
    o.data.calc_loop_triangles()
    tris = np.array([t.vertices[:] for t in o.data.loop_triangles], dtype=np.int64)
    for kb in o.data.shape_keys.key_blocks:
        co = np.empty(n * 3); kb.data.foreach_get("co", co); co = co.reshape(n, 3)
        co = co + tri_normals(co, tris) * lift[:, None]
        kb.data.foreach_set("co", co.ravel())
        if kb == o.data.shape_keys.key_blocks[0]:
            o.data.vertices.foreach_set("co", co.ravel())
    o.data.update()


def fit_over_skin(o, human, cov, clearance=0.005):
    """Wherever a garment lies under skin that stays visible while it is worn (a tight sleeve end, a collar), push it
    out over the skin, in every shape key, with the push smoothed so the hem stays a clean line."""
    import numpy as np
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    n = len(o.data.vertices)
    o.data.calc_loop_triangles()
    tris = np.array([t.vertices[:] for t in o.data.loop_triangles], dtype=np.int64)
    ev = np.array([e.vertices[:] for e in o.data.edges], dtype=np.int64)
    deg = np.bincount(ev.ravel(), minlength=n).astype(np.float64)
    polys = [tuple(p.vertices) for p in human.data.polygons if not all(cov[v] for v in p.vertices)]
    moved = 0
    for kb in o.data.shape_keys.key_blocks:
        bk = human.data.shape_keys.key_blocks.get(kb.name)
        if bk is None:
            continue
        bodyco = [d.co.copy() for d in bk.data]
        bvh = BVHTree.FromPolygons(bodyco, polys)
        co = np.empty(n * 3); kb.data.foreach_get("co", co); co = co.reshape(n, 3)
        gn = tri_normals(co, tris)
        push = np.zeros((n, 3))
        for i in range(n):
            loc, nor, idx, d = bvh.find_nearest(Vector(co[i]), 0.03)
            if loc is None or Vector(gn[i]).dot(nor) <= 0.0:
                continue
            along = (Vector(co[i]) - loc).dot(nor)
            if along < clearance:
                push[i] = np.array(nor) * (clearance - along)
                moved += 1
        for _ in range(3):
            avg = neighbour_mean(push, ev[:, 0], ev[:, 1], deg)
            mag, amag = np.linalg.norm(push, axis=1), np.linalg.norm(avg, axis=1)
            push = np.where((amag > mag)[:, None], 0.5 * (push + avg), push)
        co = co + push
        kb.data.foreach_set("co", co.ravel())
        if kb == o.data.shape_keys.key_blocks[0]:
            o.data.vertices.foreach_set("co", co.ravel())
    o.data.update()
    print(f"fit_over_skin {o.name}: {moved} moves")


def layer_fit(inner, outer, margin=0.008, move_outer=False):
    """Pulls the inner garment in wherever it is not at least margin inside the outer one, in every shape key (measured
    against the outer garment's exact surface). With move_outer the outer one is pushed out instead (a trouser hem goes
    round a shoe, the shoe is not squeezed)."""
    import numpy as np
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    n_in, n_out = len(inner.data.vertices), len(outer.data.vertices)
    polys = [tuple(p.vertices) for p in outer.data.polygons]
    moved = 0
    # twice: the de-spike after the first pull can put a lone vertex back out through the outer layer
    for rep in range(1 if move_outer else 2):
      for kb in inner.data.shape_keys.key_blocks:
          ok = outer.data.shape_keys.key_blocks.get(kb.name)
          if ok is None:
              continue
          p = np.empty(n_in * 3); kb.data.foreach_get("co", p); p = p.reshape(n_in, 3)
          q = np.empty(n_out * 3); ok.data.foreach_get("co", q); q = q.reshape(n_out, 3)
          bvh = BVHTree.FromPolygons([Vector(x) for x in q], polys)
          push = np.zeros(n_out)
          for i in range(n_in):
              loc, nor, idx, d = bvh.find_nearest(Vector(p[i]), 0.03)
              if loc is None:
                  continue
              off = Vector(p[i]) - loc
              along = off.dot(nor)
              # only where the outer layer lies over this spot, not beside its edge
              if (off - nor * along).length > abs(along) * 1.5 + 0.01:
                  continue
              # the second pass only fixes what the de-spike put back out through the outer layer
              if along > (-margin if rep == 0 else 0.0):
                  if move_outer:
                      for j in polys[idx]:
                          push[j] = max(push[j], along + margin)
                  else:
                      p[i] -= np.array(nor) * (along + margin)
                  moved += 1
          if move_outer:
              q += tri_normals_poly(q, polys) * push[:, None]
              ok.data.foreach_set("co", q.ravel())
              if ok == outer.data.shape_keys.key_blocks[0]:
                  outer.data.vertices.foreach_set("co", q.ravel())
          else:
              kb.data.foreach_set("co", p.ravel())
              if kb == inner.data.shape_keys.key_blocks[0]:
                  inner.data.vertices.foreach_set("co", p.ravel())
      inner.data.update()
      outer.data.update()
      if rep == 0:
          despike(outer if move_outer else inner)
    if not move_outer:
        follow_weights(inner, outer)
    print(f"layer_fit {inner.name} under {outer.name}: {moved} moves")


def follow_weights(inner, outer, reach=0.04):
    """Where one garment lies under another, it takes the other's skin weights at that spot, so both bend exactly alike
    and a bent hip or shoulder cannot fold the outer layer in through the inner one."""
    from mathutils.bvhtree import BVHTree
    from mathutils.interpolate import poly_3d_calc
    od = outer.data
    bvh = BVHTree.FromPolygons([v.co for v in od.vertices], [tuple(p.vertices) for p in od.polygons])
    groups = {grp.index: grp.name for grp in outer.vertex_groups}
    ow = [{groups[x.group]: x.weight for x in v.groups} for v in od.vertices]
    bones = {b.name for b in bpy.data.objects["Human.rig"].data.bones}
    for v in inner.data.vertices:
        loc, nor, idx, d = bvh.find_nearest(v.co, reach)
        if loc is None:
            continue
        off = v.co - loc
        along = off.dot(nor)
        if along > 0 or (off - nor * along).length > abs(along) * 1.5 + 0.01:
            continue   # not under the outer layer
        poly = od.polygons[idx].vertices
        f = poly_3d_calc([od.vertices[i].co for i in poly], loc)
        mix = {}
        for i, fw in zip(poly, f):
            for name, w in ow[i].items():
                if name in bones:
                    mix[name] = mix.get(name, 0.0) + w * fw
        total = sum(mix.values())
        if total <= 0:
            continue
        for grp in inner.vertex_groups:
            if grp.name in bones:
                grp.remove([v.index])
        for name, w in mix.items():
            if w / total > 0.001:
                grp = inner.vertex_groups.get(name) or inner.vertex_groups.new(name=name)
                grp.add([v.index], w / total, 'REPLACE')


def tri_normals_poly(co, polys):
    import numpy as np
    tris = np.array([(pp[0], pp[k], pp[k + 1]) for pp in polys for k in range(1, len(pp) - 1)], dtype=np.int64)
    return tri_normals(co, tris)


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
    for name, low, high in FACE:
        for end, targets in (("low", low), ("high", high)):
            set_targets(human, targets, 1.0)
            captured.append((f"{name}_{end}",) + capture())
            set_targets(human, targets, 0.0)

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

    # our own skin texture: MakeHuman's with a hair coloured scalp under the hair
    os.makedirs(OUT, exist_ok=True)
    hair_names = [f for s, f, k, _, _ in MH_WEAR if k == "Hair"]
    hairs = [o for o in meshes() if any(o.name.endswith(h) for h in hair_names)]
    skin_file = os.path.join(OUT, f"{NAME}_body.png")
    scalp = paint_scalp(human, hairs, skin_file) if hairs else 0

    # bone movement per slider, in Blender rig space (BodyShape.cs turns it into Unity space)
    # lists, not dictionaries, so Unity's JsonUtility can read it
    shape = {"sliders": [], "bones": []}
    for macro in dict.fromkeys(m for m, _, _ in SLIDERS):
        ends = [k for m, _, k in SLIDERS if m == macro]
        shape["sliders"].append({"name": macro, "low": ends[0], "high": ends[1]})
    for name, _, _ in FACE:
        shape["sliders"].append({"name": name, "low": name + "_low", "high": name + "_high"})
    for bone, (h0, t0) in base_bones.items():
        moves = []
        for key, _, bones in captured:
            h1 = bones[bone][0]
            d = [h1[i] - h0[i] for i in range(3)]
            if max(abs(x) for x in d) > 1e-5:
                moves.append({"key": key, "d": [round(x, 6) for x in d]})
        shape["bones"].append({"name": bone, "head": [round(x, 6) for x in h0], "moves": moves})

    # MakeHuman's fitting helpers (eye, teeth, skirt and tights stand-ins) are not part of the body; shape keys survive
    # deletion. The body under clothes stays: Wardrobe.cs hides it only while something covers it.
    select_only(human)
    keep = human.vertex_groups["body"].index
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.object.mode_set(mode='OBJECT')
    for v in human.data.vertices:
        v.select = not any(g.group == keep and g.weight > 0.5 for g in v.groups)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.delete(type='VERT')
    bpy.ops.object.mode_set(mode='OBJECT')
    for o in meshes():
        for m in list(o.modifiers):
            if m.type in ('MASK', 'SUBSURF'):
                o.modifiers.remove(m)

    # the wardrobe: MakeHuman's pieces cover the body in their delete groups, ours where they were cut from it
    bones = set(base_bones)
    rig = bpy.data.objects["Human.rig"]
    WRISTS[:] = [tuple(rig.data.bones[h].head_local) for h in ("hand_l", "hand_r")]
    wear = []          # (id, slot, object, covered vertex flags or None)
    for sub, folder, kind, gid, slot in MH_WEAR:
        o = next(o for o in meshes() if o.name.endswith(folder))
        grp = human.vertex_groups.get("Delete." + folder)
        cov = None
        if grp:
            cov = [any(g.group == grp.index and g.weight > 0.5 for g in v.groups) for v in human.data.vertices]
        if slot != "hair":
            cov = cover_under(human, o, cov)
            stand_off(o)
            fit_over_skin(o, human, cov)
        wear.append((gid, slot, o, cov))
    for gid, slot, colour, kind, style in MADE_WEAR:
        g, cov = make_garment(human, gid, colour, kind, style, bones)
        wear.append((gid, slot, g, cov))
        if gid == "swimsuit":
            # painted from the swimsuit's own surface: the body's UV layout overlaps itself round the crotch
            global SWIM_MASK
            SWIM_MASK = swim_mask(g)
    marks = beard_marks(meshes())
    for gid in BEARDS:
        wear.append((gid, "face", make_beard(human, gid, marks), None))
    # inner layers stay inside outer ones in every body shape: the tunic under the hijab, trousers under the tunic's hem,
    # shoes under a trouser hem. Outermost first, so a piece already tucked in is where the next one is fitted to.
    order = sorted(((a, b) for a in wear for b in wear
                    if a[1] in LAYER and b[1] in LAYER and LAYER[a[1]] < LAYER[b[1]] and not clash(a[1], b[1])
                    and wear_group(a[0]) == wear_group(b[0]) and wear_group(a[0]) not in ("swim", "beard") and worn_together(a[0], b[0])),
                   key=lambda ab: (ab[0][1] != "feet", -LAYER[ab[0][1]]))   # hems round the shoes first: their de-spike must not undo a tuck made later
    for a, b in order:
        # a trouser waist sits well inside a top's hem: bending at the hips folds the hem into the crease
        # (under a close fitting shirt or tank top, a little more: they sit near the skin)
        layer_fit(a[2], b[2], margin=(0.03 if b[0] in ("shirt", "tanktop") else 0.018) if a[1] == "bottom" else 0.008, move_outer=a[1] == "feet")
    # a last sweep: a later fit's de-spike can put a vertex tucked in earlier back out (a pyjama waist through its top)
    for a, b in order:
        if a[1] != "feet":
            layer_fit(a[2], b[2], margin=(0.03 if b[0] in ("shirt", "tanktop") else 0.018) if a[1] == "bottom" else 0.008)
    bits = {}
    for gid, _, _, cov in wear:
        if cov and any(cov):
            bits[gid] = len(bits)
    mask = [0] * len(human.data.vertices)
    for gid, _, _, cov in wear:
        if gid in bits:
            for i, c in enumerate(cov):
                if c:
                    mask[i] |= 1 << bits[gid]
    hide = human.data.uv_layers.new(name="hide")    # Unity reads it as mesh.uv2 (channel 1): u is the garment bit mask
    for loop in human.data.loops:
        hide.data[loop.index].uv = (float(mask[loop.vertex_index]), 0.0)
    human.data.uv_layers.active_index = 0
    for l in human.data.uv_layers:
        l.active_render = l.name != "hide"
    human.data.uv_layers[0].active_render = True

    # every shape key starts at 0 (MakeHuman's bake leaves them at 1, and the FBX carries that as a starting weight)
    for o in set(meshes()) | {w[2] for w in wear}:
        for kb in o.data.shape_keys.key_blocks[1:]:
            kb.value = 0.0

    # readable names, then the files
    wear_objs = {id(o) for _, _, o, _ in wear}
    body_parts = [o for o in meshes() if id(o) not in wear_objs]
    rename = {"Human": "body"}
    for o in body_parts:
        part = rename.get(o.name, o.name.replace("Human.", ""))
        o.name = f"{NAME}_{part}"
    for gid, _, o, _ in wear:
        o.name = gid
    rig = bpy.data.objects["Human.rig"]
    rig.name = NAME
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(CLOTHES_OUT, exist_ok=True)

    def material_info(objs, folder, prefix, part_of):
        info = {}
        for o in objs:
            for m in o.data.materials:
                if not m or m.name in info:
                    continue
                part = part_of(o)
                if m.name.startswith("cloth_"):      # ours: a plain fabric colour, CharacterLook adds the weave
                    tex = None
                    if m.name == "cloth_swimsuit":   # the leg openings are cut by an alpha mask
                        tex = "swimsuit_mask.png"
                        im = bpy.data.images.new("swimsuit_mask", 2048, 2048, alpha=True)
                        im.pixels[:] = SWIM_MASK.ravel()
                        im.filepath_raw = os.path.join(folder, tex); im.file_format = 'PNG'; im.save()
                    info[m.name] = {"texture": tex, "color": list(m.diffuse_color[:3])}
                    continue
                # hair, brows and lashes are cards with see-through gaps: CharacterLook gives "cut_" materials alpha clipping
                m.name = ("cut_" + part) if any(part.startswith(p) for p in ("eyebrow", "eyelashes", "hair_", "beard_")) else part
                img = None
                if m.use_nodes:
                    img = next((n.image for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image and "diffuse" in n.name.lower()), None)
                fn = None
                if part == "body" and scalp:
                    fn = os.path.basename(skin_file)          # already written by paint_scalp
                elif img:
                    src = bpy.path.abspath(img.filepath)
                    fn = f"{prefix}{m.name}{os.path.splitext(src)[1]}"
                    shutil.copyfile(src, os.path.join(folder, fn))
                info[m.name] = {"texture": fn, "color": [0.8, 0.8, 0.8]}
        return info

    def export(objs, path):
        bpy.ops.object.select_all(action='DESELECT')
        rig.select_set(True)
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = rig
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                                 axis_forward='-Z', axis_up='Y', apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                                 bake_space_transform=True, use_mesh_modifiers=False, mesh_smooth_type='FACE', add_leaf_bones=False,
                                 bake_anim=False, path_mode='STRIP', embed_textures=False)   # textures come from the .materials.json

    info = material_info(body_parts, OUT, NAME + "_", lambda o: o.name.replace(NAME + "_", ""))
    json.dump(info, open(os.path.join(OUT, NAME + ".materials.json"), "w"), indent=1)
    json.dump(shape, open(os.path.join(OUT, NAME + ".bodyshape.json"), "w"), indent=1)
    export(body_parts, os.path.join(OUT, NAME + ".fbx"))

    manifest = {"items": [], "outfits": []}
    for gid, slot, o, _ in wear:
        info = material_info([o], CLOTHES_OUT, "", lambda o: o.name)
        json.dump(info, open(os.path.join(CLOTHES_OUT, gid + ".materials.json"), "w"), indent=1)
        export([o], os.path.join(CLOTHES_OUT, gid + ".fbx"))
        manifest["items"].append({"id": gid, "slot": slot, "bit": bits.get(gid, -1), "hidesHair": gid in HIDES_HAIR})
    for name, items in OUTFITS.items():
        manifest["outfits"].append({"name": name, "items": items})
    json.dump(manifest, open(os.path.join(CLOTHES_OUT, "wardrobe.json"), "w"), indent=1)
    return {o.name: (len(o.data.vertices), len(o.data.shape_keys.key_blocks)) for o in body_parts + [w[2] for w in wear]}, bits


print(run())
