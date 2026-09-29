"""Masks for the character creator's details (makeup, freckles, beards, tattoos), painted on the MPFB2 body's UV layout.

Every texel of the body texture is given its 3D position (blender_texel.texel_positions), and each mask is a rule in 3D
round landmarks found on the exported test person: the eyes (MakeHuman's eye mesh), the mouth (the teeth), the nose and
the arm bones. At run time PersonLook blends them onto the skin with the person's own colours (Hidden/Dearlife/SkinLayers).

Writes three linear RGBA masks to Assets/Art/Textures/Characters:
  skin_layers_a.png  R lips, G blush, B eyeshadow, A eyeliner
  skin_layers_b.png  R freckles, G full beard, B moustache and goatee, A stubble shadow (the whole beard area, faint)
  skin_layers_c.png  R forearm band tattoo (left), G shoulder rose tattoo (right), B wrist star tattoo (left), A scalp

Run in Blender (Blender MCP):  exec(open(r"S:\\Dearlife by Zetazuni\\tools\\blender_skin_layers.py").read())
Our own masks, CC0 like the rest of the MPFB2 people.
"""
import bpy, os, sys, math
import numpy as np
from mathutils import Vector, kdtree

PROJECT = r"S:\Dearlife by Zetazuni"
TOOLS = os.path.join(PROJECT, "tools")
if TOOLS not in sys.path:
    sys.path.insert(0, TOOLS)
import importlib, blender_texel
importlib.reload(blender_texel)
from blender_texel import texel_positions, box_blur, smoothstep, uv_islands

FBX = os.path.join(PROJECT, "Assets", "Art", "Models", "Characters", "mpfb_test.fbx")
SKIN = os.path.join(PROJECT, "Assets", "Art", "Models", "Characters", "mpfb_test_body.png")
OUT = os.path.join(PROJECT, "Assets", "Art", "Textures", "Characters")
SIZE = 2048


def load():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=FBX)
    objs = {o.name: o for o in bpy.data.objects}
    return objs


def world(o):
    m = np.array(o.matrix_world, np.float32)
    co = np.array([v.co[:] for v in o.data.vertices], np.float32)
    return co @ m[:3, :3].T + m[:3, 3]


def hash3(x, y, z, cell, seed):
    """A random number per 3D cell (so dots are round on the skin whatever the UV stretch)."""
    i = np.floor(x / cell); j = np.floor(y / cell); k = np.floor(z / cell)
    h = np.sin(i * 127.1 + j * 311.7 + k * 74.7 + seed * 19.3) * 43758.5453
    return h - np.floor(h), (x / cell - i), (y / cell - j), (z / cell - k)


def dots(x, y, z, cell, keep, seed, size=0.32):
    """Round dots, one in some of the cells of a 3D grid: `keep` of the cells get one."""
    r, fx, fy, fz = hash3(x, y, z, cell, seed)
    r2, _, _, _ = hash3(x, y, z, cell, seed + 7)
    ox, oy, oz = 0.3 + 0.4 * r2, 0.3 + 0.4 * r, 0.5
    d = np.sqrt((fx - ox) ** 2 + (fy - oy) ** 2 + (fz - oz) ** 2)
    rad = size * (0.6 + 0.8 * r2)
    return (r < keep) * smoothstep(rad, rad * 0.55, d)


def build():
    objs = load()
    body = objs["mpfb_test_body"]; eye = objs["mpfb_test_high-poly"]; teeth = objs["mpfb_test_teeth_base"]; arm = objs["mpfb_test"]
    P, F = texel_positions(body, SIZE, body.matrix_world, "UVMap", faces=True)
    cov = ~np.isnan(P[..., 0])
    x = np.nan_to_num(P[..., 0]); y = np.nan_to_num(P[..., 1]); z = np.nan_to_num(P[..., 2])
    front = y < -0.02                                   # the person faces -Y

    # MakeHuman's body has other UV islands that lie in the same places (the inside of the mouth, helpers): the face
    # masks stay on the island of the face, the tattoos on the island of the body
    island = uv_islands(body, "UVMap")
    tex_island = np.where(F >= 0, island[np.maximum(F, 0)], -1)
    def biggest(where):
        ids, counts = np.unique(tex_island[where & (tex_island >= 0)], return_counts=True)
        return ids[np.argmax(counts)]
    body_island = tex_island == biggest(cov & (z > 0.9) & (z < 1.3))

    ev = world(eye); tv = world(teeth)
    eyes = [ev[ev[:, 0] < 0], ev[ev[:, 0] > 0]]
    ec = [(e.min(0) + e.max(0)) / 2 for e in eyes]       # eye centres
    ew = [(e.max(0) - e.min(0))[0] / 2 for e in eyes]    # eye half widths
    mouth = np.array([0.0, tv[:, 1].min(), (tv[:, 2].min() * 0.45 + tv[:, 2].max() * 0.55)])
    mw = (tv[:, 0].max() - tv[:, 0].min()) / 2
    print("eyes", [c.round(3) for c in ec], "mouth", mouth.round(3), "half width", round(mw, 3))


    skin_img = bpy.data.images.load(SKIN, check_existing=True)
    skin = np.array(skin_img.pixels[:], np.float32).reshape(skin_img.size[1], skin_img.size[0], 4)
    if skin.shape[0] != SIZE:
        idx = (np.arange(SIZE) * skin.shape[0] / SIZE).astype(int)
        skin = skin[idx][:, idx]
    # the head is several islands (face, eyelids, ears); the inside of the mouth is the one painted deep red
    red_all = skin[..., 0] / np.maximum(skin[..., 1], 1e-3)
    head_island = np.zeros_like(cov)
    for i in np.unique(tex_island[cov & (z > 1.38)]):
        m = tex_island == i
        if np.median(red_all[m]) < 1.9:
            head_island |= m
    face = cov & front & (z > 1.40) & head_island

    # ---- lips: round the mouth, where the skin texture is redder than the face round it
    ell = ((x / (mw * 0.95)) ** 2 + ((z - mouth[2]) / 0.0145) ** 2)
    near = face & (ell < 1.6) & (y < mouth[1] + 0.012)
    red = skin[..., 0] / np.maximum(skin[..., 1], 1e-3)
    vals = red[near]
    lo, hi = np.percentile(vals, 35), np.percentile(vals, 85)
    lips = near * smoothstep(lo, hi, red) * smoothstep(1.6, 1.0, ell)
    lips = np.clip(box_blur(lips.astype(np.float32), 2) * 1.25, 0, 1)

    # ---- blush: soft on the apples of the cheeks
    blush = np.zeros_like(x)
    for c in ec:
        cx, cz = c[0] * 1.35, c[2] - 0.037
        d = np.sqrt(((x - cx) / 0.026) ** 2 + ((z - cz) / 0.018) ** 2)
        blush = np.maximum(blush, face * np.exp(-d * d * 1.6))

    # ---- eyeshadow and eyeliner, round each eye
    shadow = np.zeros_like(x); liner = np.zeros_like(x)
    tree = kdtree.KDTree(len(ev))
    for i, v in enumerate(ev):
        tree.insert(Vector(v.tolist()), i)
    tree.balance()
    for c, w in zip(ec, ew):
        dx = (x - c[0]) / (w * 1.25); dz = (z - c[2])
        lid = face & (dz > -0.002) & (dz < 0.021) & (np.abs(dx) < 1.0) & (y < c[1] + 0.02)
        s = lid * smoothstep(1.0, 0.55, np.abs(dx)) * smoothstep(0.021, 0.006, dz) * smoothstep(-0.002, 0.002, dz)
        shadow = np.maximum(shadow, s)
    # the liner hugs the lash line: skin within a few millimetres of the eyeball, upper half, with a small wing outwards
    ys, xs = np.nonzero(face & (z > ec[0][2] - 0.02) & (z < ec[0][2] + 0.02) & (np.abs(x) < 0.07))
    for py, px in zip(ys, xs):
        p = P[py, px]
        co, _, dist = tree.find(Vector(p.tolist()))
        c = ec[0] if p[0] < 0 else ec[1]
        up = p[2] - c[2]
        if up < -0.001:
            continue
        outer = abs(p[0]) - abs(c[0])
        val = max(0.0, 1.0 - dist / 0.0028) * min(1.0, (up + 0.001) / 0.003)
        # the wing: a flick up and out past the outer corner
        if outer > 0.008:
            wing = up - (outer - 0.008) * 0.55
            val = max(0.0, 1.0 - abs(wing) / 0.0018) if outer < 0.02 else 0.0
        liner[py, px] = max(liner[py, px], val)
    liner = np.clip(box_blur(liner, 1) * 1.3, 0, 1)

    # ---- freckles: across the nose and the tops of the cheeks
    region = np.exp(-(((x) / 0.022) ** 2 + ((z - (ec[0][2] - 0.026)) / 0.016) ** 2))
    for c in ec:
        region = np.maximum(region, np.exp(-(((x - c[0] * 1.3) / 0.022) ** 2 + ((z - (c[2] - 0.028)) / 0.013) ** 2)))
    freckles = face * region * dots(x, y, z, 0.0026, 0.34, 3, 0.24)

    # ---- beards: the area below a line from the sideburn to beside the nose, down under the jaw, round the lips
    ear_z, nose_z = ec[0][2] - 0.012, mouth[2] + 0.024
    ax = np.abs(x)
    top = nose_z + (ear_z - nose_z) * smoothstep(0.022, 0.075, ax)       # the cheek line
    below = smoothstep(top + 0.003, top - 0.006, z)
    back = smoothstep(0.02, -0.01, y)                                    # the front of the head and the sides, not the back
    jaw_z = 1.425 + smoothstep(0.03, 0.075, ax) * 0.05                    # just under the jaw, rising towards the ears
    under = smoothstep(jaw_z - 0.006, jaw_z + 0.006, z)
    beard_area = cov & head_island & (z > 1.38) & (z < 1.58) & (y < 0.0) & (ax < 0.085)
    lipzone = ((x / (mw * 1.05)) ** 2 + ((z - mouth[2]) / 0.0125) ** 2) < 1.0
    area = beard_area * below * under * (~lipzone) * back
    area = np.clip(box_blur(area.astype(np.float32), 2), 0, 1)
    follicle = dots(x, y, z, 0.0012, 0.75, 11, 0.36)
    full = area * np.clip(0.55 + follicle, 0, 1)
    stubble = area * np.clip(0.15 + 0.85 * dots(x, y, z, 0.0011, 0.6, 17, 0.28), 0, 1)
    # moustache (above the upper lip) and goatee (round the chin)
    # a moustache over the upper lip, tapering to the corners, joined down the sides of the mouth to a rounded goatee
    dz = z - mouth[2]
    must = (dz > 0.004) & (dz < 0.02) & (ax < mw * 1.1 - np.clip(dz - 0.008, 0, None) * 1.4) & (y < mouth[1] + 0.02)
    sides = (ax > mw * 0.8) & (ax < mw * 1.15) & (dz > -0.03) & (dz < 0.008) & (y < mouth[1] + 0.025)
    chin = ((x / 0.022) ** 2 + ((dz + 0.034) / 0.022) ** 2 < 1) & (y < mouth[1] + 0.035)
    goatee = cov & head_island & (must | sides | chin) & ~lipzone
    goatee = np.clip(box_blur(goatee.astype(np.float32), 3), 0, 1) * np.clip(0.55 + follicle, 0, 1)

    # ---- tattoos
    def bone(n):
        b = arm.data.bones[n]
        return np.array(arm.matrix_world @ b.head_local), np.array(arm.matrix_world @ b.tail_local)

    def along(a, b):
        axis = b - a; L = np.linalg.norm(axis); axis = axis / L
        rel = P - a
        t = np.nan_to_num(rel @ axis) / L
        radial = np.nan_to_num(rel - (rel @ axis)[..., None] * axis)
        return t, radial, axis, L

    # a geometric band round the left forearm
    a, b = bone("lowerarm_l")
    t, radial, axis, L = along(a, b)
    rad = np.linalg.norm(radial, axis=-1)
    ref = np.cross(axis, [0, 0, 1.0]); ref /= np.linalg.norm(ref); ref2 = np.cross(axis, ref)
    ang = np.arctan2(radial @ ref2, radial @ ref)
    on = cov & body_island & (rad < 0.06) & (t > 0.42) & (t < 0.62)
    v = (t - 0.42) / 0.2
    u = (ang / (2 * math.pi) * 14) % 1.0
    border = (np.abs(v - 0.06) < 0.035) | (np.abs(v - 0.94) < 0.035)
    tri = (v > 0.16) & (v < 0.84) & (np.abs(u - 0.5) * 2 < (v - 0.16) / 0.68) & (np.abs(u - 0.5) * 2 > (v - 0.16) / 0.68 - 0.22)
    band = on & (border | tri)

    # a rose on the outside of the right upper arm
    a, b = bone("upperarm_r")
    t, radial, axis, L = along(a, b)
    rad = np.linalg.norm(radial, axis=-1)
    outward = np.array([-1.0, 0.0, 0.0]); outward -= axis * (outward @ axis); outward /= np.linalg.norm(outward)
    side = np.cross(axis, outward)
    cu = (t - 0.42) * L * 100                      # centimetres along the arm from the centre of the design
    cv = np.nan_to_num(radial @ side) * 100        # centimetres round the arm
    facing = np.nan_to_num(radial @ outward) > 0.01
    r = np.sqrt(cu ** 2 + cv ** 2); th = np.arctan2(cv, cu)
    rose = (np.abs(r - 1.9) < 0.1) | ((r < 1.75) & (np.abs(np.sin(th * 2.5 + r * 2.4)) < 0.16))
    for sx in (-1, 1):                              # two leaves
        lu, lv = cu - 1.9, cv - sx * 2.0
        e = (lu / 1.3) ** 2 + (lv / 0.55) ** 2
        rose |= (np.abs(e - 1) < 0.18) | ((np.abs(lv) < 0.05) & (np.abs(lu) < 1.2) & (e < 1))
    rose = cov & body_island & facing & (rad < 0.07) & rose & (r < 4.0)

    # a small star on the inside of the left wrist
    a, b = bone("lowerarm_l")
    t, radial, axis, L = along(a, b)
    rad = np.linalg.norm(radial, axis=-1)
    inward = np.array([-1.0, 0.0, 0.0]); inward -= axis * (inward @ axis); inward /= np.linalg.norm(inward)
    side = np.cross(axis, inward)
    su = (t - 0.88) * L * 100; sv = np.nan_to_num(radial @ side) * 100
    inner = np.nan_to_num(radial @ inward) > 0.01
    sr = np.sqrt(su ** 2 + sv ** 2); sth = np.arctan2(sv, su)
    star_r = 0.55 + 0.45 * np.abs(np.cos(sth * 2.5))            # five points
    star = cov & body_island & inner & (rad < 0.05) & (np.abs(sr - 0.9 * star_r) < 0.09)

    def img(name, chans):
        out = np.zeros((SIZE, SIZE, 4), np.float32)
        for i, ch in enumerate(chans):
            out[..., i] = np.clip(ch, 0, 1)
        out *= cov[..., None]
        im = bpy.data.images.get(name) or bpy.data.images.new(name, SIZE, SIZE, alpha=True)
        im.colorspace_settings.name = 'Non-Color'
        im.pixels[:] = out.ravel()
        im.filepath_raw = os.path.join(OUT, name + ".png")
        im.file_format = 'PNG'
        im.save()
        return im

    tat = lambda m: np.clip(box_blur(m.astype(np.float32), 1) * 1.4, 0, 1)
    img("skin_layers_a", [lips, blush, shadow, liner])
    img("skin_layers_b", [freckles, full, goatee, stubble])
    # the scalp: the texture has short dark hair painted over the top of the head; this marks it so it can take the
    # person's own hair colour
    skin_l = skin[..., :3] @ np.array([0.3, 0.59, 0.11], np.float32)
    face_l = np.median(skin_l[face & (z < 1.52) & (z > 1.46)])
    scalp = cov & head_island & (z > 1.5) & (skin_l < face_l * 0.55)
    scalp = np.clip(box_blur(scalp.astype(np.float32), 2), 0, 1) * smoothstep(face_l * 0.6, face_l * 0.3, skin_l)
    img("skin_layers_c", [tat(band), tat(rose), tat(star), scalp])
    return {"lips": float(lips.sum()), "blush": float(blush.sum()), "shadow": float(shadow.sum()), "liner": float(liner.sum()),
            "freckles": float(freckles.sum()), "beard": float(full.sum()), "goatee": float(goatee.sum()),
            "band": float(band.sum()), "rose": float(rose.sum()), "star": float(star.sum())}


RESULT = build()
print(RESULT)
