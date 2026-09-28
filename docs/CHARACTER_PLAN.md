# Character plan: realistic people and a character creator

Written 2026-09-28 (session 43). Follows rule 8 in `docs/RULES.md`: people are hyper realistic and inspired by inZOI, never copied. This is a plan, nothing here is built yet. Work through it phase by phase and tick things off in the devlog.

## 1. Why: what the scan of the current characters found

Lily and James were put on the open road in daylight, their AI switched off, and posed (Stand, Crouch, Wave) while a camera orbited them (four angles per pose, contact sheets in `Assets/Screenshots`, not committed).

| Finding | Where | Cause |
| --- | --- | --- |
| Skin pokes through the tunic at the hip and the lower back when crouching | Lily | The outfit is a separate shell over a full body, and the body is only sunk 6 mm under it (`tools/blender_rig_lily.py`, `fit_layers`). Big bends beat that margin. |
| Hands are rigid mitts, fingers never move | Lily | Her skeleton has 13 bones: no fingers, no twist bones, no face bones. |
| Torso bends as one rigid tube, no belly or hip compression, arms leave a stiff sleeve behind when waving | James | Skin weights come from a generic Sketchfab rig with our joint names mapped on top (`CharacterRig.AmirBones`), and there are no corrective shapes. |
| Crouch and wave look robotic | Both | Poses are made by swinging joints about one axis in code (`CharacterRig`), not from motion capture or animation clips. |
| The "who you're playing" ring floats beside the head, not above it | Lily | `CharacterRig.HeadTop` is measured from a bone that is not at the top of the hijab. |
| Faces are flat and cartoon like, eyes are painted on | Both | Stylised low poly Sketchfab models (Lily 12.6k vertices, James 7.2k), one diffuse texture each, no skin shading. |

None of this can be fixed inside the current models to rule 8's standard, so the plan replaces them (Amir allowed this). The code around them (`Character` AI, `UseSpot`, `LiveMode`, needs) stays: only the body, its rig and its animation change.

## 2. Where the base comes from (licences checked)

The repo is public (rule 2), so whatever we use must be allowed to sit in a public repo as raw files, not only inside a shipped game.

| Source | Look | Licence | Public repo? | In game character creator? | Verdict |
| --- | --- | --- | --- | --- | --- |
| **MPFB2** (MakeHuman for Blender) | Realistic proportions and topology, skins are basic | Assets CC0, add-on GPLv3 (the GPL does not reach exported characters) | Yes | Yes: every slider is a shape key we can ship and drive at run time | **Use as the base** |
| MetaHuman | The most realistic | Since June 2025 allowed in other engines, free under USD 1M a year. Older terms say MetaHuman content is shared only as "UE-only content", and the full content licence is behind an Epic login | Unclear, likely not as raw files | No: faces run on RigLogic, Epic's Unreal only face solver, and editing happens in Unreal | Not for the base. Revisit only if Epic states raw files may be public |
| Unity Digital Human package (The Heretic, Enemies) | Skin, eye, teeth and hair shaders, skin attachment for brows and lashes | Unity Companion License (fine inside a Unity project) | Yes, as a package dependency | Tech, not characters | **Use its shaders and skin attachment**, not its characters |
| Sketchfab scans, Mixamo, paid scan stores | Varies | Mostly no redistribution of raw files | No (unless kept out of git) | Mixed | Only through the private assets folder in section 8 |

The MPFB2 "GameEngine" rig maps 100 percent onto Unity's Humanoid (Mecanim) avatar, so any humanoid animation clip works on every character, whatever their body shape.

"Genuine for the game": the MPFB2 body is only the starting topology and morph system. Our faces, skin maps, hair and clothes are sculpted, painted and designed by us (Blender through MCP), so the finished people are our own.

Sources: [MPFB closed source FAQ](https://static.makehumancommunity.org/mpfb/faq/use_in_closed_source.html), [MPFB exporting](https://static.makehumancommunity.org/mpfb/docs/exporting.html), [MPFB 2 overview (CG Channel)](https://www.cgchannel.com/2025/03/check-out-open-source-blender-character-generation-plugin-mpfb-2/), [MetaHuman licence change (CG Channel)](https://www.cgchannel.com/2025/06/you-can-now-sell-metahumans-or-use-them-in-unity-or-godot/), [MetaHuman Creator EULA](https://www.unrealengine.com/eula/mhc), [Unity Digital Human package](https://github.com/Unity-Technologies/com.unity.demoteam.digital-human).

## 3. The look: what "like inZOI" means here

Inspired by, not copied: no inZOI names, UI layouts, presets or art.

- **Skin:** HDRP Lit with the Subsurface Scattering material type and a skin diffusion profile, a detail normal map for pores, a roughness map (oily T zone, matte cheeks), and layers for freckles, moles, blush and stubble.
- **Eyes:** the HDRP Eye shader (cornea refraction, iris depth, a wet highlight), separate lash and brow meshes kept on the skin with the digital human package's skin attachment.
- **Hair:** hair cards with the HDRP Hair shader first (cheap, looks right from game distance), with strand hair considered later for the character creator close ups only.
- **Proportions:** real adult proportions from MPFB2's macros (age, weight, muscle, height, proportions), no big heads or big eyes.
- **Lighting:** the creator gets its own studio lighting (key, fill, rim, a soft HDRI), the way portrait photographers light people.

## 4. Building a body (Blender, through MCP)

1. **Check MPFB2 on Blender 5.2.2.** It is documented for 4.2 and later. If it misbehaves on 5.2, install a side by side Blender 4.5 LTS on S:\ just for character work.
2. **Base mesh:** the MPFB2 base mesh with the GameEngine rig. Keep every macro and face target as shape keys (do not bake them), because they are the sliders.
3. **Add our own shapes:** sculpted face shapes (brow ridge, cheekbones, jaw, lips, nose bridge) as extra shape keys on the same topology, so they mix with MPFB2's.
4. **Corrective shapes:** for elbows, knees, hips, shoulders and wrists, sculpt a shape at the bent pose that switches on with the joint angle (a pose space deformation driven from the bone rotation in Unity). This removes the "rigid tube" and candy wrapper twist James shows now.
5. **Twist and finger bones:** the GameEngine rig has fingers. Add forearm and upper arm twist bones if it does not, weight the wrists over them.
6. **Face:** blendshapes for expressions (a basic set: blink, smile, frown, jaw open, the vowels for talking), driven by the talk and mood systems.
7. **Level of detail:** LOD0 for the creator and close camera, LOD1 and LOD2 made by decimating while keeping the shape keys (test that Unity keeps blendshapes on the LODs).

## 5. Clothes that change with the body (the hard part)

MPFB2 clothes (`.mhclo`) are fitted by tying every clothing vertex to a triangle on the body (three body vertices, weights and an offset). That is what we reuse:

- **In Blender:** each garment is modelled on the base body, then fitted with MPFB2's clothes tools, which write the binding and a **delete group**: the body faces the garment covers.
- **In Unity, at run time:** a `ClothingFit` component recomputes each clothing vertex from its three body vertices every time a body slider changes (a Burst job, not every frame). The garment is skinned to the same skeleton, so it bends with the body.
- **No clipping by design:** the body faces under a garment are hidden (a mask texture or a sub mesh switched off) using the delete group, so skin cannot poke through, whatever the pose. This is the real fix for Lily's hip.
- **Layering:** underwear, then tops, then outerwear, then accessories. Each layer is fitted to the layer under it where they overlap, with a small push out.
- **Loose clothing** (a hijab, long skirt, abaya, coat tails): a few extra bones driven by a light spring simulation, not full cloth simulation, to keep the frame rate.
- **Per garment colour and pattern:** a mask with up to three channels (like the furniture's two channel tint in `Furniture.SetTint`), so one garment gives many looks.

## 6. Animation (replaces the code posing)

- Switch from `CharacterRig`'s joint swinging to a Unity Animator on the Humanoid avatar, with a blend tree for walking by speed, and clips for every pose in `CharacterRig.Pose`.
- Clips: motion capture that may sit in a public repo, cleaned up and retargeted in Blender. The [CMU motion capture library](https://mocap.cs.cmu.edu/) fits: its data may be used in commercial products and shared, it just may not be resold on its own ("even in converted form"), which a free public repo does not do. Credit it in a `CREDITS.txt`. No Mixamo files in git.
- Keep `CharacterRig.Pose` as the interface the AI already uses, so `Character`, `UseSpot`, `LiveMode` and the needs code do not change. `CharacterRig` becomes a thin layer that sets Animator parameters.
- Animation Rigging package: foot IK on stairs and slopes, hand IK on held props (guitar, mug, book) and on seats, so sitting lines up with every chair and bed through `UseSpot`.

## 7. The character creator (inZOI inspired, our own design)

A dedicated screen reached from New Game, and later from a mirror or the Family tab.

- **Studio:** a small lit room, orbit camera, presets for face, upper body and full body, a turntable, a lighting switcher (day, evening, studio).
- **Start:** pick a random person or a preset, then adjust. Presets are our own characters, made with the same sliders.
- **Face:** sliders grouped by region (head shape, brows, eyes, nose, mouth, cheeks, jaw, ears), and later dragging directly on the face: hover a region, drag to push or pull it (each region maps to a pair of shape keys).
- **Body:** height, weight, muscle, proportions, then regions (shoulders, chest, waist, hips, arms, legs).
- **Skin:** tone (a realistic range), undertone, freckles, moles, blemishes, blush, age detail, tattoos as a decal layer.
- **Hair and makeup:** hair style and colour (root and tip), brows, lashes, beard and stubble, makeup layers (foundation, blush, lipstick, eyeliner).
- **Clothes:** by outfit slot (everyday, formal, sleepwear, swimwear, outerwear), each piece with colour and pattern, modest options (hijab styles, long sleeves) as first class choices.
- **Traits, name, career:** from the existing `Sim` data (traits, job).
- **Household:** create up to the cap (start with four), set relationships, then move in.
- **Saving:** a character is a small JSON (shape key weights, material values, garment ids and colours), saved in the save slot through `SaveSystem.Key`, and as shareable preset files.

## 8. Free assets only (decided 2026-09-28)

Nothing is bought. Every file the characters use is CC0, our own work, or under a licence that allows it in a public repo (like the Unity Companion License for Unity packages), and is credited in a `CREDITS.txt` next to it. If a free source is not good enough (realistic skin maps are the likely gap), we make it ourselves in Blender: sculpted pore detail baked to normal maps, painted colour and roughness maps.

## 9. Frame rate budget (rule 6: 60 FPS at 1080p on the RTX 4050 laptop)

- Up to 8 people and pets on screen, LOD0 only within about 4 m of the camera.
- LOD0 around 40k to 60k triangles with hair and clothes, LOD2 under 8k.
- Blendshape recompute only when a slider changes, never per frame. Expression blendshapes (a small set) are cheap.
- Measure with the F9 benchmark before and after each phase, and note the numbers in the devlog.

## 10. Phases

| Phase | What | Done when |
| --- | --- | --- |
| 0 | MPFB2 working on S:\ (Blender 5.2 or a 4.5 LTS side by side), one test body exported to Unity as a Humanoid with shape keys | **Done 2026-09-28** (session 44): MPFB 2.0.17 runs on Blender 5.2.2, `mpfb_test` walked with the old AI, `BodyShape` sliders reshape body, clothes, hair and skeleton in Play mode. Still a Generic rig, Humanoid comes in phase 2 |
| 1 | Realistic materials: skin, eyes, lashes and brows, teeth, one hair style | **Done 2026-09-28** (session 45) in daylight close ups: HDRP Skin, Eye and Hair shaders, our own pore map, eyeballs, scalp tint and strand map. Left for later: a faint light line at the very top of the parting in direct sun, and a proper studio light (comes with the creator, phase 4) |
| 2 | Animation: Animator, clips for every `CharacterRig.Pose`, IK for seats and props | **Done 2026-09-28** (session 46): 17 CMU clips on a Humanoid Animator, all 21 poses play (Guitar borrows the piano clip, lying uses the standing hold tipped over by `UseSpot`), hips exact and feet planted on all ten seat types. Hand IK for props is left for later |
| 3 | Clothes: run time refit, delete groups, layers, three first outfits (casual, modest with a hijab, sleepwear) | The clipping scan (below) passes for all outfits at slider extremes |
| 4 | Character creator screen and saving | A new household can be made, saved, loaded |
| 5 | Replace Lily, James and the old models with a fresh default household made in the creator (new people, new names); old models and `tools/blender_rig*.py` removed | Nothing uses the old FBX files |
| 6 | Realistic pets (Bedah the cat, the dog) to match | Pets no longer look cartoon next to people |

## 11. The clipping scan, as a tool

What was done by hand this session becomes an editor menu, **Dearlife > Scan characters**: for every body preset at the slider extremes, every outfit and every pose, it renders a four angle contact sheet and runs a penetration check (rays from body vertices along their normal: a hit on the outside of the garment within 2 mm is flagged). It writes a report to `Assets/Screenshots/scan/` (git ignored). Run it at the end of phases 3, 4 and 5.

## 12. Decisions (Amir, 2026-09-28)

1. **Free only**: no bought assets (section 8).
2. **Adults only** to start: one adult age range in the creator, no teens, children or elders for now. MPFB2's age macro is limited to the adult range.
3. **Start fresh**: Lily and James are not remade. Phase 5 replaces them with a new default household made in the creator, with new names; the old names and models go.
