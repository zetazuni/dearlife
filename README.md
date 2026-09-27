# Tiramisu 3D

A cozy little 3D house life sim. Decorate a modern two-storey home, look after pets and a family, hold down
a career, build your own house on an empty lot, and spin the camera all the way around to see every corner.

It is a 3D remake of the Tiramisu App, a 2D isometric browser game, rebuilt as a PC game with high end,
film-like graphics (Unity 6 HDRP on DirectX 12), high poly models made in Blender, real PBR textures and
realistic physics.

Made by Amir Ariffin (Zetazuni) for Athirah ♥

## Features

- A pre-built, fully furnished home with a free 360 degree orbit camera and walls that fade away as you look around
- Decorate mode: pick up and place furniture, colour swatches for fabric/paint and wood/stone, undo and redo
- Build mode: walls, rooms, floors, doors, windows, stairs, roofs and pools, on three extra empty lots reached from a map
- A life sim underneath: needs and mood, five careers with promotions, wishes, friendships, a day/night cycle and four seasons
- A shop with furniture, decor, instruments, cars (real licensed models) and a dog to adopt
- Light and dark mode, three graphics presets with DLSS, and a title screen with five independent save slots
- Seasonal lofi music, recorded sound effects, and a cinematic film look (ACES tonemapping, bloom, depth of field)

## Status

Playable. See `docs/devlog.md` for the full session-by-session history, and `CLAUDE.md` for the current
version and a tour of how the project is put together.

## Opening the project

1. Install Unity 6000.6.3f1 through Unity Hub.
2. Clone with Git LFS installed (`git lfs install` once), so models, textures and audio download properly.
3. Open the folder in Unity Hub. A DirectX 12 capable GPU is needed.
4. Press Play, or use **Tiramisu > Build Windows game** in the editor menu to make a standalone `.exe`.

## License

The game's own code is MIT, see `LICENSE`. Third-party art, textures and audio bundled in `Assets/` keep
their own licenses (mostly CC0 and CC BY) - see the `CREDITS.txt` file next to each asset folder.

Notes for contributors (and for Claude) start at `CLAUDE.md`.
