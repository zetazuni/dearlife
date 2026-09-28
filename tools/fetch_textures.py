"""
Downloads CC0 PBR texture sets from Poly Haven into Assets/Art/Textures/<id>/ and packs
an HDRP mask map (R metallic, G ambient occlusion, B detail mask, A smoothness).

Usage (from the project folder):  python tools/fetch_textures.py
Add a set: put its Poly Haven id in SETS. Already downloaded sets are skipped.
The real world size of each set is written to Assets/Art/Textures/textures.json so the
builder can tile it at true scale, and credits go to Assets/Art/Textures/CREDITS.txt.
"""
import io, json, os, sys, urllib.request
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Textures")
RES = "2k"

SETS = [
    # architecture
    "herringbone_parquet", "dark_wooden_planks", "white_plaster_02", "brushed_concrete",
    "marble_tiles", "large_grey_tiles", "concrete_floor", "rubber_tiles", "grey_cartago_03", "forest_ground_06",
    "leafy_grass", "sparse_grass", "wood_floor_deck", "precast_stone_paving",
    "blue_floor_tiles_01", "box_profile_metal_sheet", "bark_brown_02",
    # furniture
    "wool_boucle", "american_walnut_veneer", "marble_01", "poly_wool_herringbone", "rough_linen",
]

UA = {"User-Agent": "Dearlife-texture-fetch/1.0"}

# How much of the original light and dark variation the neutral copy keeps (1 = all of it).
# Lower it for surfaces that should read smooth from a distance, like painted plaster.
NEUTRAL_CONTRAST = {"white_plaster_02": 0.3, "brushed_concrete": 0.6, "concrete_floor": 0.7}


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA)) as r:
        return r.read()


def pick(files, *keys):
    for k in keys:
        if k in files and RES in files[k]:
            f = files[k][RES]
            return f.get("jpg", f.get("png"))["url"]
    return None


def make_neutral(folder, tid):
    """Greyscale copy of the colour map, brightened to an average of 0.75, so a tint in Unity sets
    the colour while the texture keeps the detail (weave, grain, blades of grass). Used for surfaces
    where the photo's own colour is wrong for us, and for anything the colour options will tint."""
    src = os.path.join(folder, f"{tid}_diff.jpg")
    dst = os.path.join(folder, f"{tid}_neutral.jpg")
    if os.path.exists(dst):
        return
    img = np.asarray(Image.open(src).convert("RGB"), dtype=np.float32) / 255.0
    lum = img[..., 0] * 0.2126 + img[..., 1] * 0.7152 + img[..., 2] * 0.0722
    mean = max(lum.mean(), 1e-3)
    k = NEUTRAL_CONTRAST.get(tid, 1.0)
    lum = (mean + (lum - mean) * k) * (0.75 / mean)
    Image.fromarray((np.clip(lum, 0, 1) * 255).astype(np.uint8)).convert("RGB").save(dst, quality=92)


def main():
    os.makedirs(OUT, exist_ok=True)
    manifest_path = os.path.join(OUT, "textures.json")
    manifest = json.load(open(manifest_path)) if os.path.exists(manifest_path) else {}
    credits = {}

    for tid in SETS:
        folder = os.path.join(OUT, tid)
        info = json.loads(get(f"https://api.polyhaven.com/info/{tid}"))
        authors = ", ".join(info.get("authors", {}).keys())
        credits[tid] = f"{info.get('name', tid)} by {authors}, Poly Haven, CC0. https://polyhaven.com/a/{tid}"
        size_m = (info.get("dimensions") or [2000, 2000])
        manifest[tid] = {"name": info.get("name", tid), "width_m": size_m[0] / 1000.0, "height_m": size_m[1] / 1000.0}

        if os.path.exists(os.path.join(folder, f"{tid}_mask.png")):
            make_neutral(folder, tid)
            print("skip", tid)
            continue
        os.makedirs(folder, exist_ok=True)
        files = json.loads(get(f"https://api.polyhaven.com/files/{tid}"))

        diff = pick(files, "Diffuse", "diff")
        nor = pick(files, "nor_gl", "Normal")
        rough = pick(files, "Rough", "rough")
        ao = pick(files, "AO", "ao")
        metal = pick(files, "Metal", "metal")
        disp = pick(files, "Displacement", "disp")

        open(os.path.join(folder, f"{tid}_diff.jpg"), "wb").write(get(diff))
        open(os.path.join(folder, f"{tid}_nor_gl.jpg"), "wb").write(get(nor))
        if disp:
            open(os.path.join(folder, f"{tid}_height.png"), "wb").write(get(disp))

        r_img = Image.open(io.BytesIO(get(rough))).convert("L")
        w, h = r_img.size
        smooth = 255 - np.asarray(r_img, dtype=np.uint8)
        ao_ch = np.asarray(Image.open(io.BytesIO(get(ao))).convert("L").resize((w, h)), dtype=np.uint8) if ao else np.full((h, w), 255, np.uint8)
        met_ch = np.asarray(Image.open(io.BytesIO(get(metal))).convert("L").resize((w, h)), dtype=np.uint8) if metal else np.zeros((h, w), np.uint8)
        detail = np.zeros((h, w), np.uint8)
        mask = np.dstack([met_ch, ao_ch, detail, smooth])
        Image.fromarray(mask).save(os.path.join(folder, f"{tid}_mask.png"), optimize=True)
        make_neutral(folder, tid)
        print("got", tid, "ao" if ao else "no ao", "metal" if metal else "", "height" if disp else "")

    json.dump(manifest, open(manifest_path, "w"), indent=2)
    with open(os.path.join(OUT, "CREDITS.txt"), "w", encoding="utf-8") as f:
        f.write("All texture sets below are CC0 (public domain) from Poly Haven.\n\n")
        for tid in sorted(manifest):
            if tid in credits:
                f.write(credits[tid] + "\n")
    print("done", len(SETS), "sets")


if __name__ == "__main__":
    main()
