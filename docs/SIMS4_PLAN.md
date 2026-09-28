# Sims 4 style gameplay: plan and progress

Rule 7 in `docs/RULES.md` asks for gameplay that imitates The Sims 4 (original art and words, gentle, no fail states). All eight milestones existed in a first version by v0.27.0, and several have grown a lot since (lots and the map, careers with promotions, held props, a dog to adopt, stairs and roofs in build mode). Bugs are expected, patch them as they are found. This table is kept in sync with `docs/devlog.md`, which is the source of truth for what actually shipped.

| # | Milestone | Where | State |
|---|---|---|---|
| 1 | Live mode controls | `UI/LiveMode.cs`, `CameraRig/OrbitCamera.cs` | Pick a person (hexagon ring marker, portraits), click to walk, right click for menus, Space and 1 2 3 for pause and speed, WASD, Q/E, middle drag |
| 2 | Pie menu and interactions | `UI/LiveMode.cs`, `Sim/Interactions.cs`, `People/Character.cs` | About 30 things to do on about 40 kinds of object (cook, shower, sleep, read, work out, swim, watch TV, work, play guitar or synth...), queued orders, people and pets have their own menus |
| 3 | Needs and mood | `Sim/SimData.cs` | Hunger, bladder, energy, fun, social, hygiene; mood, feelings, traits; people look after themselves (autonomy); gentle, nobody is hurt |
| 4 | Money | `Sim/Household.cs` | RM funds, prices, sell prices (60%), pay for work, wishes pay, bills every 3 days that can be postponed |
| 5 | Buy mode | `Sim/BuyMode.cs` | Key B (or the Shop tab): items across several categories including Cars, Music and Pets, place and turn, colour swatches, sell with Delete, purchases are saved |
| 6 | Build mode | `Sim/BuildMode.cs` | Key V (or the Build tab): straight and diagonal walls (Shift, 45 degree steps), rooms, doorways, windows, floors, flat and gable roofs, stairs up to the upper storey, paint and eyedropper, knock down, real undo and redo that also refunds the money; on the home lot only Paint and the eyedropper are allowed, empty lots reached from the map (key M) allow everything, including pools |
| 7 | Status panel | `UI/SimUi.cs` | Key C: mood, needs, feelings, traits, skills, wishes, friends, career strip with rank and promotions, recent money |
| 8 | Polish | `Sim/GameAudio.cs`, `UI/Tutorial.cs`, `UI/SimUi.cs` | Sounds and music made in code, six hint cards (F1), icons drawn in code |

Also done: four seasons (`Graphics/SeasonCycle.cs`), a modern city with traffic and aeroplanes, a working TV, lots and the map (`World/Lot.cs`, `World/LotManager.cs`, `UI/MapWindow.cs`), five careers with six ranks and promotions (`Sim/Careers.cs`), adopting a dog (`Sim/PetShop.cs`), a title screen with five save slots (`UI/MainMenu.cs`, `Sim/SaveSystem.cs`).

## Known gaps (ideas for later)
- Pets have needs but only a few things to do (be petted, be fed, sleep). No fetch, play or tricks yet.
- Interactions mostly use simple procedural poses, not authored animation clips; a few now hold props (a book, a mug, weights, a guitar or synth).
- Careers pay and promote by rank, but no skills exist yet that unlock new interactions or careers.
- No visitors, no aging, no weather effects on needs.
- Build mode's paint palette is still small, and there is no way to buy walls or wallpaper for hanging pictures.
- Untested-with-a-real-mouse items keep getting carried forward in the devlog (map clicks, dragging pools and roofs, the eyedropper, career chips) because sessions are run through the Unity MCP with the window in the background; a real hands-on pass by Amir would catch anything those checks miss.
