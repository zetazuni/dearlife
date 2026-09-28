"""Living room pieces (v0.22). Run inside Blender (Blender MCP: exec(open(path).read())).

Reuses the toolkit at the top of blender_kitchen.py. Origin: centre of the footprint on the floor, front faces -Y.
Exports Assets/Art/Models/<id>.fbx. The screen of the TV is its own part with a material called "Screen": the game
(TvScreen.cs) swaps that material for a live, glowing picture.
"""
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

_root = r"S:\Dearlife by Zetazuni"
_k = open(os.path.join(_root, "tools", "blender_kitchen.py"), encoding="utf-8").read()
exec(_k.split("# ---------------------------------------------------------------- pieces (toolkit ends above this line)")[0])

COLORS.update({"Screen": (0.01, 0.012, 0.016), "TVBlack": (0.02, 0.02, 0.022)})


def tvunit():
    """A low walnut media console with a flat screen TV and a soundbar on it."""
    rt = root("tvunit")
    W, D, H = 1.8, 0.42, 0.48
    # console: body, top slab, legs, three doors with brass pulls
    box("body", rt, (-W / 2, -D / 2 + 0.02, 0.11), (W / 2, D / 2, H), "Walnut", 0.01)
    box("top", rt, (-W / 2 - 0.012, -D / 2 - 0.012, H), (W / 2 + 0.012, D / 2 + 0.012, H + 0.028), "Walnut", 0.008)
    box("plinth shadow", rt, (-W / 2 + 0.03, -D / 2 + 0.03, 0.09), (W / 2 - 0.03, D / 2 - 0.02, 0.115), "BlackSteel", 0.003)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"leg {sx}{sy}", rt, (sx * (W / 2 - 0.09) - 0.02, sy * (D / 2 - 0.07) - 0.02, 0.0), (sx * (W / 2 - 0.09) + 0.02, sy * (D / 2 - 0.07) + 0.02, 0.11), "BlackSteel", 0.004)
    dw = (W - 0.06) / 3
    for i in range(3):
        x0 = -W / 2 + 0.03 + i * dw
        box(f"door {i}", rt, (x0 + 0.004, -D / 2 - 0.012, 0.13), (x0 + dw - 0.004, -D / 2 + 0.022, H - 0.03), "Walnut", 0.005)
        cyl(f"pull {i}", rt, (x0 + dw / 2, -D / 2 - 0.03, H - 0.09), 0.006, 0.14, "Brass", 'X', seg=16, bevel=0.001)
    # soundbar and the TV on a slim foot
    box("soundbar", rt, (-0.5, -0.11, H + 0.028), (0.5, -0.045, H + 0.075), "BlackSteel", 0.01)
    box("foot", rt, (-0.30, -0.14, H + 0.028), (0.30, 0.10, H + 0.038), "BlackSteel", 0.004)
    box("neck", rt, (-0.06, 0.0, H + 0.038), (0.06, 0.05, H + 0.16), "BlackSteel", 0.004)
    z0 = H + 0.13
    box("tv bezel", rt, (-0.64, -0.002, z0), (0.64, 0.05, z0 + 0.76), "TVBlack", 0.006)
    box("screen", rt, (-0.615, -0.011, z0 + 0.02), (0.615, -0.003, z0 + 0.74), "Screen", 0.0008)
    return rt


PIECES = [tvunit]


def build_all():
    out = {}
    for fn in PIECES:
        rt = fn()
        out[rt.name] = stats(rt)
        export(rt, rt.name)
    return out


if globals().get("RUN_LIVING", True):
    fresh()
    RESULT = build_all()
    print(RESULT)
