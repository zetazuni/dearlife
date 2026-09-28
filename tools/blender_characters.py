"""Pets (cat and dog) as stylised jointed figures (the people it once made are gone, see docs/CHARACTER_PLAN.md). Run inside Blender (Blender MCP).

Every figure is a hierarchy of joint empties with the body parts parented to them, so Unity can swing the joints for walking,
sitting, lying, waving and so on. Origin: on the ground under the figure. Front faces -Y (arrives facing +Z in Unity).
Joint names: pelvis > spine > neck, arm.L/R (> forearm.L/R), and pelvis > leg.L/R (> shin.L/R). Pets: body, head, tail,
legFL, legFR, legBL, legBR (each pivots at its top). Exports Assets/Art/Models/<id>.fbx, saves Blender/characters.blend.
"""
import bpy, bmesh, math, os, random
from mathutils import Matrix, Vector

_root = r"S:\Dearlife by Zetazuni"
_u = open(os.path.join(_root, "tools", "blender_upper1.py"), encoding="utf-8").read()
exec(_u[:_u.index("# ---------------------------------------------------------------- Teacher's Room")])

COLORS.update({
    "Skin": (0.85, 0.65, 0.52), "Hair": (0.09, 0.06, 0.05), "Hijab_main": (0.93, 0.72, 0.76), "Abaya_main": (0.72, 0.65, 0.85),
    "Shirt_main": (0.35, 0.5, 0.65), "Pants": (0.16, 0.17, 0.2), "Shoe": (0.92, 0.92, 0.9), "Eye": (0.02, 0.02, 0.02),
    "Blush": (0.95, 0.55, 0.55), "FurCat": (0.9, 0.55, 0.25), "FurCatLight": (0.97, 0.9, 0.8), "FurDog": (0.82, 0.6, 0.32),
    "FurDogLight": (0.95, 0.88, 0.75), "PetNose": (0.12, 0.08, 0.08), "PetPink": (0.95, 0.6, 0.65), "FurCatBlack": (0.06, 0.055, 0.06),
})


CUR = {}


class Jnt:
    """A joint is only a name and a position here. The FBX stays flat (Blender's axis conversion breaks joint
    hierarchies), every part carries its joint in its name ("leg.L|thigh") and Unity builds the joints at runtime
    from the table in CharacterRig.cs, using the same positions (Blender x, y, z becomes Unity -x, z, -y)."""
    def __init__(self, name, loc):
        self.name = name
        self.loc = tuple(loc)


def J(name, parent, world_loc):
    return Jnt(name, world_loc)


def own(parts, jt):
    for p in parts:
        p.name = f"{jt.name}|{p.name}"
        p.parent = CUR["root"]
    return parts


def stats(rt):
    dg = bpy.context.evaluated_depsgraph_get()
    tris = 0
    for ch in rt.children_recursive:
        if ch.type != 'MESH':
            continue
        e = ch.evaluated_get(dg)
        tris += sum(len(p.vertices) - 2 for p in e.to_mesh().polygons)
    return tris


def sph(name, c, r, m, sc=(1, 1, 1), seg=24, rings=12):
    return sphere(name, None, c, r, m, sc, seg=seg, rings=rings)


# ---------------------------------------------------------------- people

def person(name, kind):
    """kind: 'lily' (hijab and abaya) or 'amir' (short hair, shirt and trousers)."""
    rt = root(name)
    CUR["root"] = rt
    pelvis = J("pelvis", rt, (0, 0, 0.95))
    spine = J("spine", pelvis, (0, 0, 0.95))
    neck = J("neck", spine, (0, 0, 1.44))
    armL = J("arm.L", spine, (0.2, 0, 1.4)); foreL = J("forearm.L", armL, (0.2, 0, 1.13))
    armR = J("arm.R", spine, (-0.2, 0, 1.4)); foreR = J("forearm.R", armR, (-0.2, 0, 1.13))
    legL = J("leg.L", pelvis, (0.085, 0, 0.93)); shinL = J("shin.L", legL, (0.085, 0, 0.5))
    legR = J("leg.R", pelvis, (-0.085, 0, 0.93)); shinR = J("shin.R", legR, (-0.085, 0, 0.5))
    hij = kind == "lily"
    top = "Abaya_main" if hij else "Shirt_main"

    # torso
    torso = lathe("torso", None, [(0.0, 0.94), (0.13, 0.94), (0.16, 1.1), (0.175, 1.3), (0.16, 1.4), (0.0, 1.43)], top, seg=32, scale=(1, 0.66, 1), subsurf=1)
    own([torso], spine)
    own([cyl("neck", None, (0, 0, 1.47), 0.042, 0.1, "Skin", seg=16, bevel=0.004)], neck)
    # head
    face = sph("face", (0, -0.005 if not hij else -0.02, 1.6), 0.1, "Skin", (0.95, 1.0, 1.08))
    parts = [face,
             sph("eye L", (0.036, -0.1 if not hij else -0.115, 1.61), 0.011, "Eye", seg=10, rings=6),
             sph("eye R", (-0.036, -0.1 if not hij else -0.115, 1.61), 0.011, "Eye", seg=10, rings=6),
             sph("nose", (0, -0.11 if not hij else -0.125, 1.585), 0.014, "Skin", seg=10, rings=6),
             sph("cheek L", (0.06, -0.09 if not hij else -0.105, 1.575), 0.018, "Blush", (1, 0.4, 0.8), seg=10, rings=6),
             sph("cheek R", (-0.06, -0.09 if not hij else -0.105, 1.575), 0.018, "Blush", (1, 0.4, 0.8), seg=10, rings=6)]
    if hij:
        parts.append(sph("hijab", (0, 0.012, 1.6), 0.122, "Hijab_main", (1.0, 1.04, 1.07), seg=32, rings=16))
        drape = lathe("hijab drape", None, [(0.11, 1.52), (0.15, 1.44), (0.21, 1.33), (0.24, 1.2), (0.225, 1.18), (0.19, 1.31), (0.135, 1.42), (0.095, 1.5)], "Hijab_main", seg=32, scale=(1, 0.85, 1), subsurf=1)
        parts.append(drape)
    else:
        parts.append(sph("hair", (0, 0.022, 1.622), 0.108, "Hair", (1.0, 1.02, 0.98), seg=32, rings=16))
        parts.append(sph("hair tuft", (0, -0.05, 1.7), 0.05, "Hair", (1.3, 0.9, 0.6), seg=16, rings=8))
    own(parts, neck)

    # arms: upper arm, forearm (sleeve to the wrist for the abaya) and a hand
    for s, a, f in ((1, armL, foreL), (-1, armR, foreR)):
        own([cyl(f"upper arm {s}", None, (s * 0.2, 0, 1.27), 0.05, 0.28, top, seg=16, bevel=0.005),
             sph(f"shoulder {s}", (s * 0.2, 0, 1.4), 0.055, top, seg=12, rings=8)], a)
        own([cyl(f"forearm {s}", None, (s * 0.2, 0, 1.0), 0.042, 0.25, "Abaya_main" if hij else "Skin", seg=16, bevel=0.005),
             sph(f"hand {s}", (s * 0.2, 0, 0.86), 0.045, "Skin", (1, 0.8, 1.15), seg=12, rings=8)], f)
    # legs
    for s, l, sh in ((1, legL, shinL), (-1, legR, shinR)):
        own([cyl(f"thigh {s}", None, (s * 0.085, 0, 0.72), 0.075, 0.46, "Abaya_main" if hij else "Pants", seg=16, bevel=0.006)], l)
        own([cyl(f"shin {s}", None, (s * 0.085, 0, 0.29), 0.06, 0.44, "Abaya_main" if hij else "Pants", r2=0.05, seg=16, bevel=0.005),
             box(f"shoe {s}", None, (s * 0.085 - 0.05, -0.15, 0.0), (s * 0.085 + 0.05, 0.06, 0.075), "Shoe", 0.025)], sh)
    if hij:
        # the long abaya skirt hangs from the pelvis and hides the legs (only the shoes show)
        skirt = lathe("abaya skirt", None, [(0.0, 1.0), (0.16, 1.0), (0.185, 0.9), (0.24, 0.55), (0.3, 0.2), (0.32, 0.11), (0.0, 0.11)], "Abaya_main", seg=32, scale=(1, 0.85, 1), subsurf=1)
        own([skirt], pelvis)
    return rt


# ---------------------------------------------------------------- pets

def cat():
    rt = root("cat")
    CUR["root"] = rt
    body = J("body", rt, (0, 0, 0.2))
    own([lathe("body mesh", None, [(0.0, -0.3), (0.09, -0.26), (0.13, -0.1), (0.135, 0.1), (0.12, 0.24), (0.0, 0.3)], "FurCatLight", seg=24, scale=(1, 1, 1), subsurf=1)], body)
    body.data if False else None
    # the lathe stands on Z: lay it along Y by rotating the mesh data
    for o in rt.children:
        if o.name.startswith("body|body mesh"):
            o.data.transform(Matrix.Rotation(math.radians(90), 4, 'X'))
            o.data.transform(Matrix.Translation((0, 0, 0.2)))
    patches = [sph("patch back", (0.03, 0.02, 0.325), 0.12, "FurCat", (0.9, 1.8, 0.45)), sph("patch side", (0.115, -0.05, 0.22), 0.07, "FurCatBlack", (0.4, 1.3, 1.0)),
               sph("patch rump", (-0.07, 0.19, 0.29), 0.09, "FurCatBlack", (1.0, 1.1, 0.8)), sph("patch flank", (-0.12, 0.0, 0.2), 0.07, "FurCat", (0.4, 1.4, 1.0))]
    own(patches, body)
    head = J("head", body, (0, -0.27, 0.3))
    own([sph("head mesh", (0, -0.29, 0.31), 0.09, "FurCatLight", (1.05, 0.95, 0.9)),
         sph("head patch", (0.03, -0.3, 0.335), 0.085, "FurCat", (0.75, 0.9, 0.7)),
         sph("head patch b", (-0.05, -0.27, 0.35), 0.05, "FurCatBlack", (0.7, 0.9, 0.6), seg=12, rings=8),
         sph("muzzle", (0, -0.36, 0.29), 0.04, "FurCatLight", (1, 0.8, 0.7), seg=12, rings=8),
         sph("nose", (0, -0.395, 0.3), 0.012, "PetNose", seg=8, rings=6),
         sph("eye L", (0.04, -0.365, 0.335), 0.014, "Eye", seg=8, rings=6), sph("eye R", (-0.04, -0.365, 0.335), 0.014, "Eye", seg=8, rings=6),
         lathe("ear L", None, [(0.0, 0.0), (0.035, 0.0), (0.0, 0.08)], "FurCat", seg=8, center=(0.055, -0.27, 0.37)),
         lathe("ear R", None, [(0.0, 0.0), (0.035, 0.0), (0.0, 0.08)], "FurCatBlack", seg=8, center=(-0.055, -0.27, 0.37))], head)
    tail = J("tail", body, (0, 0.3, 0.26))
    pts = [(0, 0.3, 0.26), (0, 0.42, 0.32), (0, 0.5, 0.42), (0, 0.5, 0.52)]
    tp = [bar(None, f"tail {i}", pts[i], pts[i + 1], 0.028 - i * 0.003, "FurCatBlack" if i else "FurCat", 10) for i in range(3)]
    tp.append(sph("tail tip", pts[3], 0.026, "FurCatLight", seg=10, rings=6))
    own(tp, tail)
    for nm, x, y in (("legFL", 0.07, -0.16), ("legFR", -0.07, -0.16), ("legBL", 0.08, 0.16), ("legBR", -0.08, 0.16)):
        lg = J(nm, body, (x, y, 0.16))
        own([cyl(nm + " mesh", None, (x, y, 0.08), 0.032, 0.17, "FurCat", seg=12, bevel=0.004), sph(nm + " paw", (x, y - 0.01, 0.015), 0.036, "FurCatLight", (1, 1.3, 0.7), seg=10, rings=6)], lg)
    return rt


def dog():
    rt = root("dog")
    CUR["root"] = rt
    body = J("body", rt, (0, 0, 0.36))
    own([lathe("body mesh", None, [(0.0, -0.4), (0.12, -0.34), (0.17, -0.15), (0.18, 0.1), (0.15, 0.3), (0.0, 0.38)], "FurDog", seg=24, subsurf=1)], body)
    for o in rt.children:
        if o.name.startswith("body|body mesh"):
            o.data.transform(Matrix.Rotation(math.radians(90), 4, 'X'))
            o.data.transform(Matrix.Translation((0, 0, 0.36)))
    head = J("head", body, (0, -0.36, 0.5))
    own([sph("head mesh", (0, -0.38, 0.52), 0.11, "FurDog", (1.0, 1.0, 0.95)),
         sph("snout", (0, -0.49, 0.48), 0.06, "FurDogLight", (0.9, 1.3, 0.75), seg=16, rings=8),
         sph("nose", (0, -0.55, 0.5), 0.02, "PetNose", seg=8, rings=6),
         sph("eye L", (0.05, -0.47, 0.555), 0.016, "Eye", seg=8, rings=6), sph("eye R", (-0.05, -0.47, 0.555), 0.016, "Eye", seg=8, rings=6),
         sph("ear L", (0.11, -0.37, 0.5), 0.05, "FurDog", (0.5, 0.9, 1.4), seg=12, rings=8), sph("ear R", (-0.11, -0.37, 0.5), 0.05, "FurDog", (0.5, 0.9, 1.4), seg=12, rings=8),
         sph("tongue", (0, -0.5, 0.435), 0.022, "PetPink", (0.8, 1.2, 0.5), seg=8, rings=6)], head)
    tail = J("tail", body, (0, 0.38, 0.44))
    pts = [(0, 0.38, 0.44), (0, 0.5, 0.52), (0, 0.56, 0.66)]
    own([bar(None, f"tail {i}", pts[i], pts[i + 1], 0.03 - i * 0.008, "FurDog", 10) for i in range(2)] + [sph("tail tip", pts[2], 0.03, "FurDogLight", seg=10, rings=6)], tail)
    for nm, x, y in (("legFL", 0.09, -0.24), ("legFR", -0.09, -0.24), ("legBL", 0.1, 0.24), ("legBR", -0.1, 0.24)):
        lg = J(nm, body, (x, y, 0.3))
        own([cyl(nm + " mesh", None, (x, y, 0.15), 0.045, 0.32, "FurDog", seg=12, bevel=0.005), sph(nm + " paw", (x, y - 0.015, 0.03), 0.05, "FurDogLight", (1, 1.3, 0.7), seg=10, rings=6)], lg)
    return rt


def build_all():
    fresh()
    out = {}
    # (the stylised people are no longer exported: people are MPFB2 persons made in the character creator)
    for fn, nm in ((cat, "cat"), (dog, "dog")):
        rt = fn()
        out[rt.name] = stats(rt)
        export(rt, rt.name)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PROJECT, "Blender", "characters.blend"))
    return out


if globals().get("RUN_CHARACTERS", True):
    RESULT = build_all()
    print(RESULT)
