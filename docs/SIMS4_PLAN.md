# Sims 4 style gameplay: plan and progress

Rule 7 in `docs/RULES.md` asks for gameplay that imitates The Sims 4 (original art and words, gentle, no fail states). All eight milestones now exist in a first version (v0.27.0). Bugs are expected, patch them as they are found.

| # | Milestone | Where | State |
|---|---|---|---|
| 1 | Live mode controls | `UI/LiveMode.cs`, `CameraRig/OrbitCamera.cs` | Pick a person (atom marker, portraits), click to walk, right click for menus, Space and 1 2 3 for pause and speed, WASD, Q/E, middle drag |
| 2 | Pie menu and interactions | `UI/LiveMode.cs`, `Sim/Interactions.cs`, `People/Character.cs` | About 30 things to do on about 40 kinds of object (cook, shower, sleep, read, work out, swim, watch TV, work...), queued orders, people and pets have their own menus |
| 3 | Needs and mood | `Sim/SimData.cs` | Hunger, bladder, energy, fun, social, hygiene; mood, feelings, traits; people look after themselves (autonomy); gentle, nobody is hurt |
| 4 | Money | `Sim/Household.cs` | RM funds, prices, sell prices (60%), pay for work, wishes pay, bills every 3 days that can be postponed |
| 5 | Buy mode | `Sim/BuyMode.cs` | Key B: 64 items in 8 categories, place and turn, sell with Delete, purchases are saved |
| 6 | Build mode | `Sim/BuildMode.cs` | Key V: walls, rooms, doorways, windows, floors, paint (also the house), knock down, undo; saved |
| 7 | Status panel | `UI/SimUi.cs` | Key C: mood, needs, feelings, traits, skills, wishes, friends, job, recent money |
| 8 | Polish | `Sim/GameAudio.cs`, `UI/Tutorial.cs`, `UI/SimUi.cs` | Sounds and music made in code, six hint cards (F1), icons drawn in code |

Also done: four seasons (`Graphics/SeasonCycle.cs`), a modern city with traffic and aeroplanes, a working TV.

## Known gaps (ideas for later)
- Build mode: straight walls only, no stairs, no roofs, no diagonal walls, the paint palette is small.
- Pets have needs but only a few things to do (be petted, be fed, sleep).
- Interactions have no animations beyond simple poses (no props like plates or books in hand).
- Careers are only "work at the desk" with a level; no promotions, no skills that unlock things.
- No visitors, no aging, no weather effects on needs.
