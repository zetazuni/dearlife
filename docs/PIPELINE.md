# Pipeline: Blender to Unity

How the tools connect and how an asset gets from Blender into the game.

## Tools

| Tool | Version | Where |
| --- | --- | --- |
| Unity Editor | 6000.6.3f1 (Unity 6), HDRP 17.6, DirectX 12 | `C:\Program Files\Unity\Hub\Editor\6000.6.3f1` (Windows build target) |
| Unity Hub | | `S:\Unity\Unity Hub` |
| Blender | 5.2.2 LTS | Blender MCP add-on v1.7 |
| uv / uvx | | `S:\Tools\uv` (runs both MCP servers) |
| MPFB (MakeHuman for Blender) | 2.0.17 | Extension in `S:\Tools\blender-extensions` (Blender repo "S Tools"), zip in `S:\Tools\mpfb` |
| Git + Git LFS | LFS 3.7.1 | GitHub CLI at `C:\Program Files\GitHub CLI`, account `zetazuni` |

## Realistic people (MPFB2)

Rule 8 and `docs/CHARACTER_PLAN.md`. MPFB is installed as a Blender extension in a local repo on S: (`S:\Tools\blender-extensions`). Its user data (the assets) is set to `S:\Tools\mpfb\userdata` in its preferences, with MakeHuman's CC0 system asset pack (`S:\Tools\mpfb\makehuman_system_assets_cc0.zip`) loaded into it. Gotcha: switching the add-on off and on again wipes that preference, and MPFB reads it only when it starts, so after changing it either restart Blender or run `LocationService.__init__()` from `bl_ext.s_tools.mpfb.services.locationservice`.

`tools/blender_mpfb_body.py` builds a person: base mesh, the `game_engine` rig (53 bones with fingers, maps onto Unity's Humanoid), eyes, brows, lashes, teeth, skin, an outfit and hair, then bakes the body sliders into shape keys on every mesh (both ends of gender, weight, muscle, height, proportions and adult age; the female end also narrows the shoulders and shapes waist, hips, seat and bust), records how the bones move per slider in a `.bodyshape.json`, deletes MakeHuman's fitting helpers (the body under clothes stays, with a per vertex cover bit in a second UV channel) and exports an FBX plus a `.materials.json`. Hair, brow and lash materials are named `cut_...`, which `FurnitureImport.ImportCharacters` gives alpha clipping. In Unity, `BodyShape` drives the sliders and `CharacterRig` with `kind = "mpfb"` animates the skeleton.

The same run makes the wardrobe (phase 3): every garment and hair style is its own FBX in `Assets/Resources/Clothes` with its `.materials.json`, listed in `wardrobe.json` (slot, cover bit, whether it hides hair, and the outfits). Our own garments are cut from the body surface, pushed out as cloth under tension, re-weighted to the body under them, tucked inside each other layer by layer (inner layers also take the outer layer's bone weights) and cleaned of shape key spikes; MakeHuman's pieces are pushed out over any skin that stays visible. Run it, then **Dearlife > Import furniture**, then check with **Dearlife > Scan characters** in Play mode (report in `Logs/character_scan.txt`). The whole run takes about a minute.

## MCP connections (how Claude drives the tools)

Both are registered in Claude Code at **user scope**, so they work from any folder.

- **Blender** (`blender`): `S:\Tools\uv\uvx.exe blender-mcp`. Add-on source is at `S:\Tools\blender-mcp`. To connect: open Blender, press N in the 3D viewport, open the BlenderMCP tab and start the server. Claude checks with `get_addon_status`.
- **Unity** (`unity`): `S:\Tools\uv\uvx.exe --from mcpforunityserver mcp-for-unity --transport stdio` (Coplay's MCP for Unity). The bridge package `com.coplaydev.unity-mcp` is already in `Packages/manifest.json`. To connect: open this project in Unity, then go to Window > MCP for Unity and click Start Session. Claude Code has to be restarted once after the server is first registered so the tools load.
- **Unity transport must be Stdio.** In the MCP for Unity window, set Transport to `Stdio`, then Start Session. Do not use the setup wizard's "Configure" button for Claude Code: it switches Unity to HTTP mode and adds a `UnityMCP` HTTP entry (http://127.0.0.1:8080/mcp) that needs a separate server nobody starts, so it fails with "[WebSocket] Connection failed". If that entry shows up again, remove it with `claude mcp remove UnityMCP -s local` from this folder.
- If a tool says it cannot connect, the fix is almost always to open the app and start its session, not to reinstall. Unity's own log is `Logs/Editor.log` in this folder.

## Units and axes

- 1 Blender unit = 1 metre = 1 Unity unit. **1 floor tile = 1 m.** Ground floor wall height is 3 m.
- Blender is Z up, Unity is Y up. Export settings (checked and working): Forward `-Z`, Up `Y`, Apply Unit Scale, Apply Scalings "FBX All", **Apply Transform (bake space transform) on**, apply modifiers, smoothing "Face", selected object only, no leaf bones, no animation.
- **Origin:** the centre of the footprint, on the floor (z = 0 in Blender). Move the object to the world origin before exporting.
- **Facing:** the front of a piece faces **-Y in Blender**, which arrives in Unity facing **+Z** (toward the garden) at rotation 0. Verified with the sofa in session 3.
- Room floor plates are 2 cm thick, so furniture is placed at floor height + 0.02 m (`FLOOR_TOP` in the builder).

## Folders

```
Blender/                 source .blend files (Git LFS), one per set: house.blend, furniture_living.blend ...
Assets/Art/Models/       exported .fbx, one file per piece, named by item id
Assets/Art/Materials/    shared palette materials
Assets/Art/Textures/     palette atlas and any real textures
Assets/Scenes/           Main.unity and test scenes
Assets/Scripts/          C# (Camera, Build, Economy, People, Pets, Time, UI, Cloud)
docs/                    these notes
```

## Naming

- Item ids match the 2D game where the piece exists there (`sofa`, `platformbed`, `marbletable`, `mazda3` ...). The id is the Blender object name, the FBX file name and the id in the item catalogue.
- New pieces use short lowercase ids with no spaces.

## Modelling standard (rule 6: PC, high poly)

- Real proportions and real sizes. Soft edges everywhere: bevel modifiers with 3 to 10 segments, subdivision (level 2) on anything upholstered, a Cast (sphere) modifier to puff cushions, 32 to 48 sided cylinders, rounded corners on slabs. Small details are welcome (feet, tassels, shelves, seams).
- **One child mesh per part under an empty named after the item id** (for example `sofa` > `base`, `arm`, `seat 1`...). Unity gives every part its own convex collider, so the physics shape follows the real shape.
- **UVs in metres:** box mapping in object space where 1 UV unit = 1 m (`box_uv` in the Blender toolkit). Unity tiles each texture at its real size, so every piece matches the others.
- Apply modifiers and transforms before export. Export the root empty and its children (object types Empty and Mesh).
- The Blender toolkit (helpers `part`, `rounded_slab`, `cylinder`, `root`, `box_uv`) is recreated at the start of each modelling session; see the session 4 devlog for the recipe.

## Materials and colour options

- Blender material slots use **shared names** (`Fabric_main`, `Walnut`, `Marble_main`, `BlackSteel`, `Rug_main`, `RugBorder`, `Cushion_main` so far). Blender colours are only for previewing.
- On import, **Dearlife > Import furniture** (`Assets/Editor/FurnitureImport.cs`) swaps each slot for the Unity material of the same name in `Assets/Art/Materials/Furniture/`. `FurnitureImport.Looks` says which texture set, tint, smoothness range and metallic each name gets (for example `Fabric_main` = wool boucle, `Walnut` = American walnut veneer, `Marble_main` = marble). Tune the look there.
- A slot name ending in **`_main`** is the part the colour options will tint.
- New material name? Add it to `Looks` (and its texture set to `tools/fetch_textures.py` if needed) or it arrives neutral grey on purpose.

## Physics for a new piece

- Add its id to `PhysicsSetup.Specs` with a real mass in kg, whether it can move, and a drop height if it should fall into place (like cushions).
- Colliders and physics materials are added automatically from the parts and the material names.

## Workflow for one asset

1. Model it in Blender (via MCP or by hand) at real size, origin and facing as above, check it with a viewport screenshot.
2. Save the `.blend` in `Blender/` (one file per set, `furniture_living.blend` holds sofa, marbletable, geomrug, cushion), export `<id>.fbx` into `Assets/Art/Models/`.
3. In Unity run **Dearlife > Build greybox house**. It imports the models, links the materials and places everything listed in the builder's `Layout` table (`id, x, z, rotation, floor`).
4. Check it in play mode from a few angles, then commit the `.blend`, the FBX and the code together.

## Photoscanned models from Poly Haven (glTF)

- `python tools/fetch_models.py` downloads the models listed in its `MODELS` (id and texture resolution) into `Assets/Art/Models/PolyHaven/<id>/` as glTF with textures, and writes `CREDITS.txt`. `--sizes` only prints sizes. Unity imports them with **glTFast** (`com.unity.cloud.gltfast`), which gives HDRP materials automatically, so these skip Blender.
- Many files hold several variants side by side (`shrub_02_a` to `_d`, `pachira_aquatica_01_*_d`...). `Assets/Editor/PropPlacer.cs` keeps one variant, re-centres it so the footprint centre sits on the floor at the given spot, and adds physics: `Static` (collider on the pot or trunk only), `Dynamic` (one rigidbody, one box), `DynamicParts` (every child its own rigidbody, like the 20 books) or `None`.
- Placements live in the builder: `LivingProps` and `GardenProps` (id, variant, position, rotation, scale, body, mass, collider height). Things hung on walls are added to that wall's `WallCutaway.attachments` so they hide when the wall drops.
- Static foliage and wall decor are left out of ray tracing (`rayTracingMode = Off`), because trees are hundreds of thousands to a million triangles each.
- Sizes: trees are mostly geometry (island_tree_02 is 46 MB even at 1K), so reuse one tree file for several trees instead of downloading more. The `.bin` geometry files go through Git LFS.
- **Facing:** Poly Haven furniture faces **-Z** (the opposite of our Blender pieces), so add 180 degrees to the rotation you would use for a Blender piece. Checked with the armchair in session 5.

## Textures, sky and other downloaded assets

- PBR texture sets: `python tools/fetch_textures.py` downloads the sets listed in its `SETS` into `Assets/Art/Textures/<id>/` at 2K, packs the HDRP mask map, writes real sizes to `textures.json` and credits to `CREDITS.txt`. About 275 MB for 20 sets, stored with Git LFS (watch the GitHub LFS quota before adding lots more; 4K sets are 4x bigger).
- The sky is HDRP's physically based sky with a cloud layer (no image needed). The old HDRI (`Assets/Art/Sky`, CC0) is kept in case an HDRI sky option is wanted later.
- Only use CC0 or properly licensed assets, since the repo is public, and always keep a credit file next to them.
