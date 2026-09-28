# Rules for this project

These are Amir's rules for Dearlife. Read them before doing anything, every session, on any account. They win over any default habit.

## Amir's rules (set 2026-09-26)

1. **Keep the markdown notes up to date.** This repo carries its own memory so work can pick up in a new conversation or on a different account without getting lost. Start at `CLAUDE.md`, which links everything else. Update `docs/devlog.md` at the end of every session and fix any note that has gone stale.
2. **Public GitHub repo.** The code lives at https://github.com/zetazuni/dearlife (public). Because it is public, never commit tokens, API keys, exported save files, the cloud room code or anything private. Check `git status` before every commit.
3. **S:\ drive first.** Every file, download, cache, tool and install goes on `S:\` unless it truly cannot (the Unity Editor itself already sits in `C:\Program Files\Unity\Hub\Editor`, leave it there). New tools go under `S:\Tools\`.
4. **Write like a person.** Anything the player sees (buttons, toasts, menus, dialogue) and anything a person reads in the back end (commit messages, docs, log lines, code comments) should sound warm and human, never like a machine wrote it. **No em dashes at all.** Use a comma, a period, a colon, brackets or the " · " separator instead. This applies to these docs too.

5. **Cinematic, premium graphics** (added 2026-09-26, raised the same day by rule 6). Lighting and reflections should look exclusive and movie like: physically based lights and camera, filmic tonemapping, soft realistic shadows, contact shadows, ambient occlusion and global illumination, bloom on lights, real reflections on glass, water and polished floors, a proper sky, volumetric light and haze, warm interior lights at night and careful colour grading. Every visual change is judged against "does this look like a film still?".
6. **PC first, high end** (added 2026-09-26). Dearlife is a **PC game** (Windows, DirectX 12), not a web game like the 2D Tiramisu App. That means:
   - **High poly models.** Smooth, detailed geometry (bevels, subdivided cushions, stitching, real proportions). No blocky low poly and no web or iPad polygon budgets.
   - **Lots of texture detail.** Every surface gets real PBR texture sets (colour, normal, roughness, ambient occlusion, height where it helps) at 2K or more, from CC0 sources like Poly Haven or made for the game. Flat single colours are only for placeholders.
   - **Realistic physics.** Everything solid has a collider, things that can move have real masses and physics materials (friction, bounce), small props can be knocked about, and characters and pets will move through physics rather than sliding through things.
   - **Render pipeline: HDRP** (High Definition Render Pipeline), which replaces URP. Screen space reflections and global illumination, volumetric fog and hardware ray tracing are all on the table (the dev PC has an RTX 4050 laptop GPU with 6 GB).
   - **Performance target:** a steady 60 FPS at 1080p on the dev PC (RTX 4050 laptop) in the default graphics mode, with a higher "Ultra" mode (ray tracing) and a lighter mode for weaker PCs. Web, WebGL, iPad and Netlify are no longer targets.

7. **Play like The Sims 4** (added 2026-09-27). Dearlife should imitate the gameplay of The Sims 4 as closely as we can, in its own original art and wording: the **controls** (WASD and edge scrolling to pan, Q and E or right drag to turn, wheel to zoom, floor levels, pause and the three speeds, click a person to select them and click things to get the pie menu), **build and buy mode** (walls, doors and windows, floors, roofs, rooms, a catalogue with prices, drag and drop with grid snapping, rotate, sell), the **money system** (household funds shown as an amount, prices for everything, income, bills), the **need and mood system** (hunger, bladder, energy, fun, social, hygiene, and a mood that follows them, with moodlets), **character status** (a panel with needs, mood, skills, traits and wishes) and **item interaction** (every object offers actions through a pie menu, and people queue actions and walk over to do them, using the object the way its shape allows). Keep it as similar as possible, but stay original: no copied names, icons, sounds, art or Simlish. Rule 5 and the gentle side of the game still win: needs never kill anybody and nobody is punished.
   See `docs/SIMS4_PLAN.md` for the plan and what is done.

## Rules carried over from the 2D Tiramisu App

- **Cozy and gentle.** It is a personal game made by Amir Ariffin for Lily. Keep it warm and pastel. No punishment, no fail states, no permadeath.
- **Keep the personal touches.** Keep the "Amir Ariffin / made for my love, Lily ♥" signature, the love note and "Lily" as the default name.
- **Original art only.** Do not copy copyrighted characters or sticker packs. Ask first if Amir wants a named third-party pack.
- **Claude picks the version numbers** (semver). Bump the version shown in game with each release and note it in the devlog.
- **Releases.** Builds are Windows PC builds now (no Netlify). Only make or share a release build when Amir asks.
- **Commit and push** to GitHub after each finished piece of work, with a clear human message.
- **Ask before big, hard-to-undo steps** (deleting assets, rewriting history, changing the save format).
- **Save compatibility.** Once saves exist, new fields need defaults when loading and the save key never gets renamed.
