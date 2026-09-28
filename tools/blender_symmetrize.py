"""Makes a rigged character symmetrical the cheap and exact way: the model is built from separate parts (an arm, a sleeve, a hand,
a pant leg, a shoe...), so a bad part is replaced by the mirrored copy of the good one on the other side.
Run inside Blender (exec this file, then call symmetrize(mesh, armature)). Used by blender_rig_lily.py."""
import bpy, bmesh
from mathutils import Vector


def _swap(name):
    if name.endswith(".L"):
        return name[:-2] + ".R"
    if name.endswith(".R"):
        return name[:-2] + ".L"
    return name


def _islands(bm):
    seen = set()
    out = []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack = [v]; comp = []; seen.add(v.index)
        while stack:
            a = stack.pop(); comp.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen:
                    seen.add(b.index); stack.append(b)
        out.append(comp)
    return out


def symmetrize(mesh, arm, keep_arm="R", keep_leg="L", verbose=True):
    """The arm on side keep_arm and the leg on side keep_leg are the masters: the other arm and leg are thrown away and
    rebuilt as mirrored copies (same materials, mirrored UVs untouched, weights with L and R swapped). The bones of the
    replaced limbs are then moved to the mirrored bones of the master limbs so the rest pose matches."""
    me = mesh.data
    bm = bmesh.new(); bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    dl = bm.verts.layers.deform.verify()
    gname = {vg.index: vg.name for vg in mesh.vertex_groups}
    gindex = {vg.name: vg.index for vg in mesh.vertex_groups}

    isl = _islands(bm)
    sign_of = {"L": 1.0, "R": -1.0}

    def stats(c):
        xs = [v.co.x for v in c]; zs = [v.co.z for v in c]
        return sum(xs) / len(xs), sum(zs) / len(zs), min(xs), max(xs), min(zs), max(zs)

    arm_master, arm_drop, leg_master, leg_drop = [], [], [], []
    for c in isl:
        mx, mz, x0, x1, z0, z1 = stats(c)
        if abs(mx) > 0.12 and 0.65 < mz < 1.4 and z1 < 1.4:                  # arms: sleeve, skin, cuff, hand, nails
            side = "L" if mx > 0 else "R"
            (arm_master if side == keep_arm else arm_drop).append(c)
        elif abs(mx) < 0.10 and mz < 0.75 and abs(mx) > 0.02 and z1 < 1.1:    # legs: pants, skin, shoes
            side = "L" if mx > 0 else "R"
            (leg_master if side == keep_leg else leg_drop).append(c)
    if verbose:
        print("arm master parts", len(arm_master), "dropped", len(arm_drop), "| leg master parts", len(leg_master), "dropped", len(leg_drop))

    # the top of the pant legs sits under the tunic: pull it in a little so it can never poke through (the left leg had a bulge)
    cloth = [i for i, sl in enumerate(mesh.material_slots) if sl.material and "Cloth" in sl.material.name]
    for c in leg_master:
        if not cloth or not all(f.material_index == cloth[0] for v in c for f in v.link_faces):
            continue
        bins = {}
        for v in c:
            b = round(v.co.z / 0.02)
            e = bins.setdefault(b, [0.0, 0.0, 0])
            e[0] += v.co.x; e[1] += v.co.y; e[2] += 1
        for v in c:
            e = bins[round(v.co.z / 0.02)]
            cx, cy = e[0] / e[2], e[1] / e[2]
            t = min(1.0, max(0.0, (v.co.z - 0.66) / 0.24)); t = t * t * (3 - 2 * t)
            k = 1.0 - 0.92 * t
            v.co.x = cx + (v.co.x - cx) * k
            v.co.y = cy + (v.co.y - cy) * k
            u = min(1.0, max(0.0, (v.co.z - 0.9) / 0.1))
            v.co.y -= 0.03 * u * u * (3 - 2 * u)      # and a little forward, well inside the back of the tunic

    def mirrored_copy(parts):
        geom = []
        for c in parts:
            geom.extend(c)
            for v in c:
                geom.extend(v.link_edges)
                geom.extend(v.link_faces)
        geom = list(set(geom))
        res = bmesh.ops.duplicate(bm, geom=geom)["geom"]
        new_verts = [g for g in res if isinstance(g, bmesh.types.BMVert)]
        new_faces = [g for g in res if isinstance(g, bmesh.types.BMFace)]
        for v in new_verts:
            v.co.x = -v.co.x
            old = dict(v[dl].items())
            v[dl].clear()
            for gi, w in old.items():
                v[dl][gindex[_swap(gname[gi])]] = w
        bmesh.ops.reverse_faces(bm, faces=new_faces)
        return new_verts

    # copy first (indices change when things are deleted), then delete the bad parts
    new_verts = mirrored_copy(arm_master) + mirrored_copy(leg_master)
    drop = [v for c in arm_drop + leg_drop for v in c]
    bmesh.ops.delete(bm, geom=drop, context='VERTS')
    bm.to_mesh(me)
    bm.free()
    me.update()

    # rest pose bones: the replaced limbs get the mirror of the master limbs
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.data.edit_bones
    other = lambda s: "L" if s == "R" else "R"
    for base, keep in (("arm", keep_arm), ("forearm", keep_arm), ("leg", keep_leg), ("shin", keep_leg)):
        src = eb[f"{base}.{keep}"]; dst = eb[f"{base}.{other(keep)}"]
        dst.head = Vector((-src.head.x, src.head.y, src.head.z))
        dst.tail = Vector((-src.tail.x, src.tail.y, src.tail.z))
        dst.roll = -src.roll
    bpy.ops.object.mode_set(mode='OBJECT')
    if verbose:
        print("symmetrized: verts now", len(me.vertices))
