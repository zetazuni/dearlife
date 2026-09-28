# Unity guide for Dearlife

A friendly walkthrough for someone who has never used Unity. It covers what each part of the screen is for, how to move around, how to test the game, and what to do when something looks wrong. Claude does most of the building through MCP, so your job is mostly to look, play and give feedback.

## 1. Opening the project

1. Open **Unity Hub** (`S:\Unity\Unity Hub\Unity Hub.exe`), go to **Projects**, and click **Dearlife by Zetazuni**. If it is not listed, click **Add** (or **Open**) and pick the folder `S:\Dearlife by Zetazuni`.
2. Wait for the editor to load. The first time takes a few minutes.
3. If Unity shows "Administrator Privileges Detected", click **Continue**. It only appears because Claude Code runs as administrator.
4. Open the main scene if it is not open already: in the **Project** pane go to `Assets > Scenes` and double click **Main**.

## 2. The parts of the screen

Unity's window is made of panes (called windows or tabs). You can drag their tabs around. If you mess up the layout, use **Window > Layouts > Default** to get it back.

| Pane | Where (default layout) | What it is for |
| --- | --- | --- |
| **Scene** | Big view in the middle, tab "Scene" | The editing view. You fly around freely to inspect the world. What you do here does not play the game. |
| **Game** | Tab "Game" next to Scene | What the player sees through the game camera. Pressing Play switches here. |
| **Hierarchy** | Left | Everything in the open scene as a tree (House, Ground floor, Walls, Main Camera, Game...). Click an item to select it. |
| **Inspector** | Right | Settings of whatever is selected (position, materials, scripts and their values). |
| **Project** | Bottom | All the files in the project (`Assets` folder: scenes, scripts, materials, models). |
| **Console** | Tab next to Project at the bottom (or **Window > General > Console**) | Messages, warnings (yellow) and errors (red). Check it whenever something looks wrong. |
| **MCP for Unity** | **Window > MCP for Unity** | The bridge Claude uses. Transport must be **Stdio**, session started (green). |

The **toolbar** along the top has the **Play** (▶), **Pause** (⏸) and **Step** (⏭) buttons in the middle.

## 3. Moving around in the Scene view

Click inside the Scene view first, then:

| Action | How |
| --- | --- |
| Look around (fly mode) | Hold **right mouse button** and move the mouse |
| Fly | Hold **right mouse button** and press **W A S D** (forward, left, back, right), **Q E** (down, up). Hold **Shift** to go faster |
| Orbit around a point | Hold **Alt** + **left mouse button** and drag |
| Pan | Hold the **middle mouse button** (wheel) and drag, or **Alt + Ctrl + left drag** |
| Zoom | **Mouse wheel**, or **Alt + right drag** |
| Jump to an object | Select it in the Hierarchy (or click it in the Scene view), move the mouse over the Scene view and press **F** |
| Top, side or front view | Click the axis cone (gizmo) in the top right corner of the Scene view. Click the middle cube to switch between perspective and flat views |

**Selecting things:** click an object in the Scene view or its name in the Hierarchy. Walls and floors are made of many pieces, so clicking often selects one piece. Its parent is shown in the Hierarchy.

**Tools** (keyboard shortcuts while the mouse is over the Scene view): **Q** hand (pan), **W** move, **E** rotate, **R** scale. Please do not move things in the greybox by hand: the house is rebuilt from code (see section 6), so hand edits get wiped.

## 4. Testing the game (Play mode)

1. Press **Play** (▶) at the top middle. The Game view opens and the game runs. The Play button turns blue.
2. Click once inside the Game view so it receives your mouse and keyboard.
3. Try the controls (below).
4. Press **Play** again to stop.

**Important:** changes you make in the Inspector while playing are **thrown away** when you stop. That is handy for experimenting, but write down values you like and tell Claude.

**Game view tips**
- The dropdown at the top of the Game view (usually "Free Aspect") sets the screen size. Pick **1920x1080** (Full HD, our performance target) or **2560x1440** to see how it looks on a bigger monitor.
- **Maximize on Play** (in the Game view's top bar, sometimes under a small menu) makes the game fill the window while playing.
- **Stats** shows frames per second (FPS) and draw calls. Handy for checking performance. For a fair number set the Game view to **1920x1080** first (Claude sets a "Full HD benchmark" size for you) and press F9.

### Game controls (v0.9)

| Action | Mouse | Keyboard | Touch screen |
| --- | --- | --- | --- |
| Spin around the house | Left drag | **Q / E** | One finger drag |
| Tilt up and down | Left drag up or down | | One finger drag up or down |
| Move (pan) | Right or middle drag | **W A S D** or arrows | Two finger drag |
| Zoom | Mouse wheel | **+ / -** | Pinch |
| Ground floor / upper floor / whole house | Buttons on the left | **1 / 2 / 3** | Buttons |
| Jump to a room | Room buttons on the left | | Buttons |
| Walls: automatic, always up, always down | "Walls" button | **Tab** | Button |
| See the whole house | "See the whole house" button | **F** | Button |
| Shove something (physics test) | **Click** a sofa, chair, book, vase or cushion without dragging | | Tap |
| Move furniture (decorate mode) | "Move furniture" button | **M** | |
| ...pick up, carry and put down | Press on a piece, drag, let go. It follows any surface under the mouse (a mug can go from the island to the floor). Things standing on it ride along | | |
| ...turn it | Hold the left button on it and turn the **mouse wheel**, one degree per notch (no zoom while doing this) | **R** turns 15 degrees (Shift+R the other way) | |
| ...put it back where it was | Right click | **Esc** | |
| ...free placement without the 5 cm grid | | Hold **Ctrl** | |
| ...reset the whole house | "Reset layout" button (always there, puts furniture and windows back) | | |
| Time of day | Slider and Morning, Noon, Sunset, Night buttons top right, "Let time run" | | |
| Open a sliding door | Hover the mouse over it (people and pets open them by walking up) | | |
| Move or resize a window (decorate mode) | Press on the window and drag along its wall, **R** changes the size | | |
| Draw the curtains shut or open | **Click** a curtain or the window | | |
| Nudge a cushion, book or mug | **Click** it: it shifts a little and settles back in a fifth of a second | | |
| Drop a stone in the pool | **Click** the water | | |
| Graphics mode (Ultra, Quality, Performance) | "Graphics" button | **G** | Button |
| Run the frame rate benchmark (about a minute) | | **F9** | |

### Decorate mode (v0.9)

Press **M**. Press on a sofa, chair, plant, mug, car or bike and drag it. A green outline means it fits, red means a wall or another piece is in the way (letting go there puts it back at the last free spot). Built in things (kitchen counter, shower, toilet, bath, vanity, lamps hung from the ceiling, the punching bag and everything on walls) cannot be moved. Your layout is saved automatically and is there next time; **Put everything back** resets it.

### Doors and windows (v0.10)

Every doorway between rooms has a sliding door. Hover over one and it glides open, move away and it closes. Windows sit in the concrete walls. In decorate mode (**M**) press on a window, drag it along the wall and it snaps every 10 cm. The wall re-forms around it. It turns red where it would hit a door, another window or the end of the wall.

## 5. Test checklist

Run through this after each update and tell Claude anything that feels off (with a screenshot if you can: **Win + Shift + S**).

- [ ] Press Play. No red errors appear in the Console.
- [ ] Spin a full circle around the house. The walls closest to you drop down and pop back up smoothly.
- [ ] Tilt from low to high. The camera never goes under the ground or flips.
- [ ] Zoom all the way in and out. It stays smooth.
- [ ] Pan to each end of the house and into the garden. The camera stops at the edges.
- [ ] Press 1, 2 and 3. The upper floor and the roof show and hide correctly.
- [ ] Click each room button. The camera glides to that room.
- [ ] Cycle the walls button through all three modes.
- [ ] The picture looks like a film still: warm light, soft shadows, reflections on glass, water and floors (rule 5).
- [ ] Up close, surfaces show real texture: wood grain in the parquet, the bouclé weave on the sofa, veins in the marble, grass on the lawn (rule 6).
- [ ] Press **M**, drag the sofa across the living room: the cushions ride along, the outline turns red near a wall. Press **R** to turn it. Close the game, open it again: it is still there.
- [ ] Slide the time to Night: lights come on in every room, pendants glow, the garden path lights and the pool light up, stars and a moon show. Set it to Sunset for the golden light.
- [ ] Push a cushion into the pool with the mouse (click it) or click on the water: rings spread across the surface and bounce off the edges, things float and bob.
- [ ] Physics: the two cushions drop onto the sofa when Play starts and settle naturally. Clicking a cushion sends it flying, clicking the sofa only nudges it (rule 6).
- [ ] Stats shows a steady frame rate (60 FPS or more at 1920x1080 in Quality mode).
- [ ] Press F9 once in a while: it measures every graphics mode and writes the results to the Console (and to `benchmark.txt` in the game's data folder). Send Claude the numbers if anything looks slow.

## 6. How the project is built (so nothing surprises you)

- **The house is generated.** The menu **Dearlife > Build greybox house** (menu bar at the top) rebuilds the whole `Main` scene: house, garden, lights, camera and furniture placements. Claude changes the code and reruns it. If the scene ever gets messed up, running this menu puts it back.
- **Dearlife > Set up render pipeline** redoes the graphics setup (HDRP, shadows, reflections, global illumination, DirectX 12) and the physics settings.
- **Furniture comes from Blender.** Source files live in `Blender/`, exported models in `Assets/Art/Models/`. See `docs/PIPELINE.md`.
- **Screenshots** Claude takes while testing land in `Assets/Screenshots/`. They are not saved to GitHub, delete them whenever you like.

## 7. When something goes wrong

| Problem | Try this |
| --- | --- |
| Red errors in the Console | Take a screenshot of the Console and send it to Claude. Double clicking an error opens the script it came from. |
| Play does nothing or the game view is grey | Make sure the **Main** scene is open (Project pane, `Assets > Scenes > Main`). |
| Claude says it cannot reach Unity | Open **Window > MCP for Unity**, check Transport is **Stdio** and click **Start Session**. Unity must stay open. |
| Unity asks to "Enter Safe Mode" | There is a script error. Choose **Ignore** (or Exit Safe Mode) and ask Claude to fix it. |
| The layout of panes is a mess | **Window > Layouts > Default**. |
| Everything is pink | A material's shader broke. Run **Dearlife > Set up render pipeline**, then **Dearlife > Build greybox house**. |
| Lots of red NullReferenceException errors mentioning HDRenderPipeline right after a big graphics change | Close Unity and open the project again. HDRP sometimes needs a fresh start after its settings change. |
| The picture is very dark or very bright for a second | That is the auto exposure adapting, like a real camera walking from outside to inside. It settles in a moment. |
| Unity is slow or frozen right after Claude changed scripts | It is recompiling. Watch the small spinner in the bottom right corner and wait. |

## 8. Glossary

- **Scene:** a level or world file (ours is `Main`).
- **GameObject:** anything in the scene (a wall piece, a light, the camera). Listed in the Hierarchy.
- **Component:** a piece of behaviour on a GameObject (a light, a mesh, one of our scripts like `OrbitCamera`). Shown in the Inspector.
- **Prefab:** a saved, reusable GameObject (a sofa, for example) that can be placed many times.
- **Material:** how a surface looks (colour, shine, metal, glass).
- **HDRP:** High Definition Render Pipeline, the high end graphics system we use (it replaced URP in v0.3).
- **PBR texture set:** the colour, normal (bumps), roughness and ambient occlusion images that make a surface look real.
- **Rigidbody:** the component that makes an object obey physics (mass, gravity, collisions).
- **Post-processing:** film-style effects applied to the whole picture (tonemapping, bloom, colour grading, depth of field).
- **Reflection probe:** a little camera that captures the surroundings so shiny surfaces can reflect them.
- **Play mode:** running the game inside the editor.
