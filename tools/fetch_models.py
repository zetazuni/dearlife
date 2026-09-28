"""
Downloads CC0 photoscanned 3D models from Poly Haven (glTF + textures) into
Assets/Art/Models/PolyHaven/<id>/. Unity imports them with glTFast (HDRP materials included).

Usage (from the project folder):
    python tools/fetch_models.py          download everything in MODELS (skips what is already there)
    python tools/fetch_models.py --sizes  only print how big each download would be

Add a model: put its Poly Haven id and texture resolution in MODELS.
Credits go to Assets/Art/Models/PolyHaven/CREDITS.txt.
"""
import json, os, sys, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Models", "PolyHaven")
UA = {"User-Agent": "Dearlife-model-fetch/1.0"}

MODELS = {
    # garden (trees are mostly geometry, 1k keeps the repo sane; copies of one tree share the same file)
    "island_tree_02": "1k", "searsia_lucida": "1k",
    "shrub_01": "1k", "shrub_02": "1k", "shrub_04": "1k",
    # indoor plants
    "potted_plant_01": "1k", "potted_plant_04": "1k", "pachira_aquatica_01": "2k", "calathea_orbifolia_01": "1k",
    # living room
    "modern_arm_chair_01": "2k", "side_table_01": "1k", "desk_lamp_arm_01": "1k",
    "book_encyclopedia_set_01": "1k", "ceramic_vase_03": "1k", "hanging_picture_frame_02": "1k",
}


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA)) as r:
        return r.read()


def files_for(mid, res):
    files = json.loads(get(f"https://api.polyhaven.com/files/{mid}"))
    if "gltf" not in files:
        return None
    g = files["gltf"].get(res) or files["gltf"][sorted(files["gltf"].keys())[0]]
    return g["gltf"]


def main():
    sizes_only = "--sizes" in sys.argv
    os.makedirs(OUT, exist_ok=True)
    credits = []
    total = 0
    for mid, res in MODELS.items():
        info = json.loads(get(f"https://api.polyhaven.com/info/{mid}"))
        credits.append(f"{info.get('name', mid)} by {', '.join(info.get('authors', {}).keys())}, Poly Haven, CC0. https://polyhaven.com/a/{mid}")
        g = files_for(mid, res)
        if g is None:
            print(f"{mid}: no glTF version on Poly Haven, skipped")
            credits.pop()
            continue
        size = g.get("size", 0) + sum(f.get("size", 0) for f in g.get("include", {}).values())
        total += size
        if sizes_only:
            print(f"{mid:28s} {res}  {size / 1e6:6.1f} MB")
            continue
        folder = os.path.join(OUT, mid)
        main_file = os.path.join(folder, f"{mid}.gltf")
        if os.path.exists(main_file):
            print("skip", mid)
            continue
        os.makedirs(folder, exist_ok=True)
        for rel, f in g.get("include", {}).items():
            path = os.path.join(folder, rel.replace("/", os.sep))
            os.makedirs(os.path.dirname(path), exist_ok=True)
            open(path, "wb").write(get(f["url"]))
        open(main_file, "wb").write(get(g["url"]))
        print(f"got {mid} ({size / 1e6:.1f} MB)")
    print(f"total {total / 1e6:.0f} MB")
    if not sizes_only:
        with open(os.path.join(OUT, "CREDITS.txt"), "w", encoding="utf-8") as f:
            f.write("All models below are CC0 (public domain) from Poly Haven.\n\n" + "\n".join(credits) + "\n")


if __name__ == "__main__":
    main()
