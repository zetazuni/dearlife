# Devlog: Dearlife

## Session 1: setup (2026-09-26) · v0.0.1

- Connected Claude Code to Unity and Blender through MCP. Unity uses Coplay's MCP for Unity (server `unity`, bridge package in `Packages/manifest.json`). Blender uses the Blender MCP add-on v1.7 on Blender 5.2.2 LTS, confirmed live.
- Created this Unity 6 project (6000.6.3f1) in `S:\Dearlife by Zetazuni`. For now it is an empty project on the built-in render pipeline.
- Read through the 2D Tiramisu App notes to plan the remake. Wrote `CLAUDE.md`, `docs/RULES.md`, `docs/GAME_DESIGN.md`, `docs/PIPELINE.md` and this devlog.
- Set up git with LFS for Blender files, models, textures and audio, and pushed to the new public repo `zetazuni/dearlife`.

**Next**
- Open the project in Unity and start the MCP session, then restart Claude Code so the Unity tools load.
- Add URP, the Input System and Cinemachine (through the Unity MCP so the pipeline asset gets set up properly).
- Phase 1: greybox the house at 1 m per tile and build the 360 degree orbit camera with automatic wall fading.
- Confirm the target platform with Amir (assumed WebGL on Netlify).

## Session 2: greybox house and the 360 degree camera (2026-09-26) · v0.1.0

- Unity MCP connected (after switching the bridge to Stdio). Installed URP 17.6, made the project linear colour, and kept the legacy Input Manager so no extra input package is needed.
- **Greybox house built from the 2D floor plan** by an editor menu (Dearlife > Build greybox house). Ground floor: living room, kitchen, stair hall with floating oak stairs, bathroom, garage. Upper floor: Teacher's Room, Office & Library, landing with a glass rail over the stairwell, Engineer's Room, Gym. Flat roof for now. Garden: deck, pool, stepping stones, mailbox spot, a few trees.
- **Orbit camera:** spin all the way around, tilt within limits, zoom, pan, with smooth easing. Mouse, touch (pinch, twist, two finger pan) and keys all work.
- **Walls cut away on their own:** any wall between the camera and the room you are looking at drops to a short stub, like The Sims. There is also a manual Auto, Always up, Always down switch. Whole house view keeps every wall up and shows the roof.
- **Floor views:** Ground floor, Upper floor (ground still visible under it), Whole house. Room buttons jump the camera to each room.
- Tested in play mode through the MCP: views from the garden side and from behind (the right walls cut each time), upper floor view, whole house view, no console errors or warnings. Found and fixed two things along the way: the ground filled the pool hole, and the front glass would not cut when the camera looked at the deck (the check point is now kept inside the house).
- Not tested yet: real touch on the iPad (needs a WebGL build), and how it feels by hand. Please have a spin in the editor.

**Next**
- Amir tries the camera in the editor (press Play) and says how it feels.
- Start the Blender side: house shell and the first furniture set in the soft low poly style.
- Decide the target platform (still assuming WebGL on Netlify).

## Session 3: film look, first furniture, Unity guide (2026-09-26) · v0.2.0

- **New rule 5:** lighting and reflections must look exclusive and movie like. Added to `docs/RULES.md` and the design doc. Two decisions came with it: models use realistic materials instead of blocky low poly, and the house shell stays generated in Unity while Blender makes everything inside it.
- **Film look:** HDRI sky from Poly Haven (CC0) for sky, ambient light and reflections. Soft 4096 shadows with 4 cascades, ambient occlusion, Forward+ lighting, a warm light and a reflection probe in every room, filmic ACES tonemapping, bloom, warm grading with cool shadows, vignette, light film grain and depth of field that follows what you look at. Floors, glass and water got shinier so the reflections show. First attempt was badly overexposed. Tuned by eye in play mode (sun 1.3, ambient 0.55, exposure -0.3).
- **First Blender furniture:** modern three-seat sofa with separate cushions and a walnut plinth, marble coffee table with a walnut shelf and black steel legs, and a two-tone rug. They're in `Blender/furniture_living.blend` and exported as FBX. Unity links their materials by name to proper furniture materials and places them in the living room from a small layout table. Found and fixed two things: the sofa faced the wall (Blender -Y arrives as Unity +Z, now documented), and the rug was hidden inside the 2 cm floor plate.
- **`docs/UNITY_GUIDE.md`:** a beginner's guide to the Unity window, moving around the Scene view, testing in Play mode, the game controls, a test checklist, how the project is built and a troubleshooting table.
- Tested in play mode through the MCP (overview, low cinematic angle, living room close up), no console errors or warnings.

**Next**
- Amir runs through the test checklist in `docs/UNITY_GUIDE.md` and says how the camera and the look feel.
- Realistic textures for the architecture (oak planks, concrete, grass, tiles) from Poly Haven, since flat colours are now the weakest part of the picture.
- More living room and kitchen furniture in Blender.
- Night lighting (sun sets, room lights and LED strips glow) to really sell the film look.

## Session 4: PC first, HDRP, textures, high poly, physics (2026-09-26) · v0.3.0

- **New rule 6 (PC first):** Dearlife is now a Windows PC game, not a web game. High poly models, detailed PBR textures, realistic physics, HDRP on DirectX 12, 60 FPS at 1080p on the dev PC (RTX 4050 laptop, 6 GB) as the target. Web, WebGL, iPad and Netlify dropped. Rules, design doc, pipeline notes and the Unity guide all updated.
- **URP replaced by HDRP 17.6.** Physically based sky with a cloud layer, volumetric fog, automatic exposure (EV 9 to 13.8), screen space reflections (also on glass), screen space global illumination, ambient occlusion, contact shadows, 4096 soft PCSS sun shadows, lights in real units (sun 100000 lux, 900 lumen room lights), ACES and a warm grade, TAA, ray tracing support switched on for a future Ultra mode. URP and its leftover assets were removed. Unity needed a restart after the switch (HDRP threw NullReferenceExceptions until then).
- **Textures:** 20 CC0 Poly Haven sets at 2K (`tools/fetch_textures.py`), tiled at true size with Planar (floors), Triplanar (walls) or UV0 (furniture) mapping. Several sets did not look like their names (leafy grass is autumn yellow, linen is blue, the boucle is plaid, the roof metal is rusty), so the script also makes a neutral greyscale copy and those surfaces take their colour from a tint. That also sets up the furniture colour options. Plaster gets reduced contrast so walls read as smooth paint.
- **High poly furniture remake in Blender:** sofa (15 parts, about 13k faces, puffy subdivided cushions, rounded arms, steel feet), marble table with rounded corners and 48 sided legs, rug with 128 tassels, two new linen throw cushions. Real UVs in metres. One child mesh per part.
- **Physics:** 90 Hz, 12/4 solver iterations, 8 surface physics materials matched by material name, fitted box collider per furniture part (PhysX convex hulls are capped at 256 polygons, too few for high poly parts), real masses. Cushions drop onto the sofa at start and settle. Click something to shove it (`PhysicsPoke`). Tested: a shoved cushion flew 2 m and came to rest on the floor, the 70 kg sofa only slid 5 cm and rocked back.
- **Tuning found by testing:** auto exposure pinned at EV 15 looked gloomy, capped at 13.8. Physical camera depth of field kept reading a 10 m camera focus and blurred the house, replaced by manual focus ranges that follow the orbit distance. Motion blur removed (smeared every camera spin).
- About 150 FPS in the small editor Game view (880x377). Not yet measured at 1080p.

**Next**
- Measure FPS at 1920x1080 (Game view set to 1080p, or a Windows build) and add graphics modes (Ultra with ray tracing, Quality, Performance) plus DLSS if the NVIDIA package works.
- One leftover "missing script" warning comes from an old URP asset, not the scene. Track it down.
- Real trees and plants (the lollipop trees are the weakest thing on screen now), a proper roof, window frames.
- More furniture for the living room and kitchen.
- Night lighting.

## Session 5: real trees, photoscanned props, graphics modes and DLSS (2026-09-26) · v0.4.0

- **Measured performance at 1920x1080** in the editor (Game view set to Full HD): the v0.3 scene ran at 65 FPS, then 54 once the new trees and props went in.
- **Graphics modes with DLSS** (NVIDIA module + dynamic resolution in HDRP): Ultra (DLSS Quality, ray traced reflections, full res SSGI), Quality (default, DLSS Quality, half res low SSGI), Performance (DLSS Balanced, no SSGI, no volumetric fog, lighter shadows). G key or the HUD button. DLSS is detected on the RTX 4050.
- **Benchmark tool** (F9): measures every mode and Quality with each effect off. It showed SSGI was the big cost (6.4 ms), so Quality now runs it at half resolution on the low preset. Final numbers at 1920x1080, DLSS on, RTX 4050 laptop, in the editor: **Performance 112 FPS, Quality 78 FPS, Ultra 44 FPS** (Ultra went from 20 to 44 by leaving static foliage out of ray tracing).
- **Photoscanned CC0 models from Poly Haven** through glTFast (`tools/fetch_models.py`, `Assets/Editor/PropPlacer.cs`): two kinds of olive-like trees, shrubs, a potted plant and a money tree, a mid-century lounge chair, side table, arm lamp, 20 encyclopedias (each its own physics body), a vase and a picture frame. Trees are huge (island_tree_02 is 46 MB and a million triangles even at 1K), so one tree file is reused.
- **Architecture:** black steel window mullions and rails on every glass wall (they cut away with the walls), a flat roof with a deep front overhang, oak soffit and black steel fascia. The lollipop trees are gone.
- The armchair first faced away from the rug: Poly Haven furniture faces -Z, the opposite of our Blender pieces. Documented in the pipeline notes.
- Project art is about 425 MB (Git LFS stores a bit more because of older versions). Worth keeping an eye on the GitHub LFS quota before adding many more big models.

**Next**
- Grass that looks like grass up close (the lawn is a flat texture; HDRP terrain details or scattered grass clumps), and something on the horizon (hills or distant trees).
- Art for the empty picture frame (something personal, original).
- More rooms: kitchen next.
- Night lighting and a day and night cycle.
- One old "missing script" warning from a URP leftover asset is still to be found.

## Session 6: stair hall fix and the kitchen (2026-09-26)

- **Fixed the black partition in the stair hall.** It was the "Stair spine", a full height steel slab (0.2 m wide, floor to upper floor) running down the middle of the stairs. It is now a slim steel support under each step, so the stairs really float. Rebuilt the scene and checked from the bottom of the stairs.

- **Kitchen furnished** (v0.5.0). New script `tools/blender_kitchen.py` builds and exports seven pieces in one go (run it inside Blender): a 4.4 m counter run (charcoal cabinets, marble worktop and splashback, sink with tap, glass cooktop with hood, wall units, oven tower with two ovens, brass handles), a stainless fridge, a waterfall marble island with a walnut slat front, three bar stools, a walnut dining table, four dining chairs and three brass pendant lamps with glowing bulbs and a warm light each. Between them about 75k triangles. Placed by the builder's `Layout` table; fixed pieces (run, fridge, island, pendants) are static, stools, table and chairs are real rigid bodies.
- Tested: rebuilt the scene, no errors, looked from the garden and from inside the kitchen. Not yet played by hand, so please try shoving a stool.

- **Dining chairs redone.** The first ones had posts poking above a floating back. Now a padded seat, walnut legs and rear posts flush with a curved back pad.
- **Bathroom furnished** (v0.6.0): walk-in shower with glass and rain head, floating walnut vanity with a vessel basin, mirror, wall hung toilet, freestanding tub, towel ladder, bath mat, washer, dryer and laundry basket, plus a plant.
- **Garage furnished:** a red sedan and a silver MPV (built in Blender by lofting cross sections, with subdivision, boolean wheel arches, glass, alloy wheels, lights), EV charger, workbench with pegboard, metal shelving with bins, tool chest and a bicycle. `tools/blender_bathgarage.py` builds and exports all 17 pieces.
- Found by looking: bike wheels lay flat (the torus helper only made flat rings, now has an `axis` option).
- Tested: rebuilt the scene, checked the bathroom, garage and dining corner from inside. The car window edges are a bit jagged up close, worth a proper remodel later.

- **Shower fixed:** now a fully glazed cubicle (fixed front panel, hinged door with handle, side panel and a glass ceiling, black frames), the left and back sides are the room walls. The steel bar from the wall to the glass is gone. Moved 23 cm left so it sits against the wall.
- **Cars:** much brighter tail lights (emission x5, wrap-around light bar) and a red point light behind each light cluster.
- **Props pass** (v0.7.0): `tools/blender_props.py` builds 11 small props (fruit bowl, bread on a board, mugs, espresso machine, utensil crock, herb pot, towel stack, candles, cardboard boxes, paint cans, spare tyres). The builder got a `Tabletop` table so things can stand on surfaces at a given height. All are real rigid bodies, so they can be knocked about.

- **Car windows fixed:** the cabin now has real edge loops at the window sill and the top of the glass, and glass or paint is picked per face before subdivision, so the window edges are clean. Also 4 pillar stretches (A, B, C) stay paint.
- **Upper floor furnished** (v0.8.0), 27 new models from two scripts: Teacher's Room (bed with pillows and throw, nightstands and lamps, wardrobe, bookcase full of books, desk and chair, globe, chalkboard, world map), Office & Library (two bookcases, desk with monitor, two chairs, file cabinet, uplight, whiteboard, Poly Haven armchair and plants), Engineer's Room (charcoal bed, electronics bench with monitors and an oscilloscope, robot arm, 3D printer with filament, beanbag), Gym (two treadmills, dumbbell rack, bench with barbell, two spin bikes, punching bag on chains, yoga mats, water dispenser, three wall mirrors). New materials: bedding, leather, glowing screens, chalk and whiteboard, rubber, plastics, map colours, book colours and more.
- **Tested in Play mode:** 50 rigid bodies, none moving or fallen after settling, no console errors. Looked at all four upper rooms and the gym from the garden.
- Unity note: `Dearlife > Build greybox house` refuses to run while Play mode is on, so stop Play before rebuilding.

- **Feedback round from Amir (v0.9.0).** Taller plants: the 27 cm succulent pots are gone, replaced by money trees (1.9 m) and the 1.8 m plant. The shower still had an open side: Blender +X arrives as Unity -X, so the glass was on the wall side. Mirrored the shower in Blender (documented in CLAUDE.md), it is now closed on every side. The tail lights are now proper blocks with a dark bezel, sticking out of the body, emission x12.
- **Kitchen and bathroom props moved:** for the same reason the counter run is mirrored (oven tower next to the fridge, cooktop on the left), so the espresso machine, utensils and herbs moved, and the towel stack went on the dryer.
- **Pool ripples** (new `PoolRipples`), tested by simulating 6 seconds of physics: a coffee table dropped in floats half submerged, a cushion floats on the surface, rings spread and bounce off the walls.
- **Decorate mode** (new `DecorateMode` and `Furniture`): tested the rules through the API (wall and furniture overlaps are rejected, free spots accepted, the sofa carried its two cushions to a new place and turned 90 degrees with them, the saved layout survived a restart and reset cleanly). Not tested with a real mouse, please try it.
- **Night lighting and day and night cycle** (new `DayNightCycle`, `SwitchableLight`). Warm interiors, glowing pendants and lamps, garden bollards, roof downlights, cyan pool lamps, moon and stars. First attempt was blown out (exposure floor too low), tuned to EV 4.2. Exposure adapts in about 2 seconds now.
- Unity gotcha found on the way: a MonoBehaviour must live in its own file named after the class, or scene references turn into "missing script".

## Session 7: night look fixes (2026-09-26) · v0.9.1

Amir's notes on the first night: too glowy and reflective, lights too strong and pointy, plants need a vase, cars too shiny, kitchen wall cabinet flickering.

- **The glow was the reflection probes.** They were baked in full daylight (thousands of nits). At 10 percent they still lit every rough surface like a sunny day. At night they now go to 0.02 percent, so the plants stopped glowing white and the chair went back to its real colour. Bloom and exposure compensation also drop a little at night, and emissive lamps, bulbs and screens are about half as strong.
- **Softer lights.** Room lights are now big rectangle panels in the ceiling (no more hot spots), pendants and downlights and lamps and pool lamps are small soft area lights that face where the light really goes (down, up into the ceiling, into the pool). Fewer lumens, more even.
- **Less shine:** floors, wood, marble, ceramic, steel and brass have lower smoothness. Car paint went from a mirror to satin (smoothness 0.55, metallic 0.3 to 0.35).
- **Planters:** a new `planter` model (ceramic, soil and pebbles) under all five money trees, the trees stand on it.
- **Flicker found and fixed:** the kitchen wall doors sat inside their cabinet with front faces exactly level, so the two surfaces fought (diagonal stripes, flickering when the camera moved). The doors now stand 1 cm in front. I ruled out shadows, screen space reflections, global illumination and contact shadows by switching each off in Play mode first. Also gave the sun and moon more shadow bias.
- Tested in Play mode with the time at 21:30 and at 13:00: living room, kitchen, whole house and the cars from behind.

## Session 8: doors, windows, steadier furniture (2026-09-26) · v0.10.0

- **Furniture cannot be tipped any more.** Every movable body is frozen upright (it can still turn on the spot), damped hard, and the nudge is sideways only and small. Test: 12 shoves each on the table, sofa, cushion, armchair, side table, stool and chair, all shoved at the height that tips things worst: no tilt, at most 2 cm of movement. A **Reset furniture and windows** button in the HUD is always there and puts everything back upright.
- **Room lights: several soft panels per room** (2 to 4, 64 area lights in the house) in a cooler colour, 4300 K, and 5200 K in the garage, gym, bathroom and office. The light is even, with no bright centre.
- **The money tree in the engineer's room** is centred on its trunk now (the placer used the middle of the leaves, which hang to one side). All plants use the trunk.
- **Stairwell glass removed.**
- **Sliding doors** in all eight doorways between rooms, glass in glass walls and walnut in concrete walls, black rail, long bar handle. Open on hover and for anything carrying `DoorOpener` (hooks for the people and pets to come). Tested through the API: a door slid its full 2.1 m open on hover, a test pet stepping into another door's sensor opened it.
- **Movable windows:** 11 windows in the concrete outer walls (living room, stair hall, bathroom high window, garage, teacher's room, landing, gym). Drag along the wall in decorate mode, R for the width. Tested through the API: it refuses to overlap a neighbour or the wall end, accepts a nudge, cycles the width, and a reset restores everything.
- Not tested with a real mouse: hovering over doors, dragging windows. Please try both.

## Session 9: curtains, front doors, garage shutter, paint (2026-09-26) · v0.11.0

- **Small things stick to their spot.** Everything under 3 kg (cushions, books, towels, mug, fruit bowl, vases, lamps and so on, 40 pieces) is kinematic and anchored. A click shifts it 3 cm and tilts it 5 degrees and it eases back in 200 ms. Carried by the piece it sits on in decorate mode (the cushions go with the sofa). Tested: a nudged cushion moved 0.030 m and stayed kinematic.
- **Upper floor sliding doors hid properly:** they were outside the upper floor group. Now inside it.
- **Curtains** on all 11 windows, click to draw or open. Found by testing that toggling did nothing after Play started (the editor built ones were not known to the script), fixed, then tested again.
- **Gym light** 3.6x and a lighter floor, garage 1.3x. Compared before and after at night from the same spot.
- **Front doors:** pairs of walnut hinged doors in a black frame in the garden facade (living room, kitchen, bathroom, no door for the garage), they swing out. Opening tested through the API (both leaves swing 100 degrees).
- **Garage shutter door** in the end wall opening onto a new driveway (apron, ramp) and a road with kerbs and lane lines along the east side. Rolls up on hover or when something with a `DoorOpener` comes near. Tested through the API: it rolled up to 6 percent of its height.
- **Room paints:** every room has its own colour, both faces of the interior walls, both the plaster on the outer walls. Looks right from the upper floor view (coral gym, slate engineer, blue office, blush teacher).
- Not tested with a real mouse: clicking curtains, hovering the new doors. Please try.

## Session 10: front yard, LED strips, marble kitchen (2026-09-26) · v0.12.0

- **Floating cushions fixed.** Found by simulating them in Play mode: they settle with their bottom at 0.578 m on a seat that tops out at 0.58 m, but the layout placed them at 0.71 and 0.75 m. Placed at 0.612 (their origin) now.
- **Kitchen floor:** large format polished marble (Poly Haven `marble_01`). I looked at the Grey Cartago sets first but they are rusty brown and grey, not something for a kitchen, so those downloads were removed again.
- **Garage front:** a zig-zag glass door that folds to both sides. Cars parked in a row nose to the shutter. Tested through the API: the panels fold, the doorway clears.
- **Front yard populated** like the 2D game's yard (20 new models, about 110k triangles): see CLAUDE.md. The beach ball floats in the pool and rings spread around it.
- **Light strips and string lights**, both only glow at night (146 glowing surfaces, 71 lights). The LED strips on the roof were first left floating in the sky in the ground floor view, they now belong to the roof.
- **Curtains fluffier,** two rounds of thickness and depth tuning. Please judge them by eye and tell me if you want them even fuller.
- Not tested with a real mouse: hovering the garage folding door, dragging the new yard furniture.

## Session 11: pool, fountains, skylights, night fixes (2026-09-26) · v0.13.0

Amir's notes: night too dark outside, skylights, lounger backrest upside down, ball should float submerged and roll with the ripples, flower bed soil, floating cushions, floating weeds, black marble pool with a jacuzzi and a wall fountain, brighter LED strips, a real moving round fountain.

- All done, see CLAUDE.md for how. Checked by looking (day, night, close ups) and by scripts: the ball drifted and turned, five ripple surfaces run, cushions settle within 2.4 cm after the first pass (threshold tightened to 4 mm after).
- **Cushions:** my earlier fix was right for a fresh build, but an old saved layout on Amir's machine could still hold the old floating heights. Small things now save relative to their host and always settle on what is under them.
- **Weeds:** the scanned shrub has stem tips above its lowest leaf, so it hung in the air. Sunk into the lawn.
- Not tested by hand: dragging the yard furniture, hovering the folding door, clicking the water.

## Session 12: fence, gates, attached small things (2026-09-26) · v0.14.0

- Weeds removed, hammock away from the pavement, bathtub moved to the side of the garage door.
- **Small things are now one piece with what they sit on.** First attempt attached them at the start of Play and left a 1.5 cm gap under the cushions, because the sofa pushes itself out of the floor in the first moments. Now it waits 1.2 s, settles on the highest surface, then attaches. Also found on the way: a host with a small thing already attached to it was refused as a host for the next one (destroying a component only takes effect at the end of the frame), so only 7 of 20 attached at first.
- **Turn buttons** in the HUD.
- **Lawn under and behind the house, fence with wall lights, front gate on the pavement axis, back gate, open garage lane.** Checked from above by day, the gates' swing direction by script (both inward).
- **Testing note:** when the Unity window is in the background it renders only when asked, so game time barely advances. Timed things (the 1.2 s wait, door opening) were checked by calling the code directly.
- Not tested by hand: gates opening by hover, the turn buttons with a mouse.

## Session 13: back yard, gate, beams, colours (2026-09-26) · v0.15.0

- **The flickering lights were a real limit:** HDRP draws at most 64 area lights on screen unless told otherwise. We have about 105. Raised to 512.
- **Cushions are part of the sofa model now** (remodelled in Blender, exported again). Checked: two cushion parts inside the sofa, no separate cushion pieces left.
- **Carrying a piece freezes all others.** Checked by counting: 43 moving bodies, 0 while carrying, 43 again after.
- **Back yard** with a shed (door opens on hover), tools, logs, planters, bench, path from the back gate.
- **Sliding lane gate,** bulbs on the fountain, pastel brown outer walls, dark brown slab edges, vertical beam screens. Found by looking: the upper floor beam sets first floated in the sky in the ground floor view, they now belong to the upper floor.
- Not tested by hand: the gate and shed door opening on hover, dragging things near others.

## Session 14: hedges, sidewalk, wall lights (2026-09-26) · v0.16.0

- Beams: the upper floor ones were still visible because the scene had been built by the older code (the build ran while Unity was still recompiling). Rebuilt, and checked by looking up the group by name in the ground view: hidden.
- Placeable area, leaning cushions, shed roof and bulbs, string light fix, moon 50 percent dimmer, hedges, sidewalk and moved road, wall lights: see CLAUDE.md. Checked by day and night screenshots from the back, the road side and the sofa.
- Lesson: after changing code, call the refresh and wait for it before the build menu, then look at the result, do not trust the last screenshot.

## Session 15: rectangular hedges, house sidewalk, wheel turning, jacuzzi jets (2026-09-26) · v0.17.0

- Hedges are rectangular. A sidewalk runs round the house with strips to both gates. Turn a piece with hold click plus the wheel (one degree per notch, zoom blocked), the HUD turn buttons are gone. The jacuzzi bubbles are replaced by two real water jets. Checked by looking (the two arcs show clearly in the tub) and from above (hedges, sidewalk and the gate strips). The wheel turning was not tested with a real mouse.

## Session 16: people and pets (2026-09-26) · v0.18.0

- Two people and two pets live in the house now. They wander through the doors (which open for them), sit on the sofa and chairs, lie on beds and loungers, chat with speech bubbles, pat the pets, and the pets sit, groom and sleep. Checked in Play mode: the NavMesh baked (1043 triangles), all four moved and picked activities on their own, and Amir was seen lying on a lounger by the pool.
- **Found on the way:** the joint hierarchy did not survive the FBX export (every limb rotated 90 degrees and the figures looked like hanging sausages), fixed by building the skeleton in Unity. The navigation package from the registry does not compile on this Unity version, so a patched local copy is used. And Unity throttles a background window, so timed tests were run with the window brought to the front.
- Not done yet: the characters do not use the stairs on purpose (they can, the mesh links the floors), pets do not use furniture, no sounds, no customising their looks, and the chat lines are short and generic.

## Session 17: real character models (2026-09-26) · v0.19.0

- Lily, Amir and Bedah are now downloaded, rigged models with real faces, replacing the stylised figures. They walk, sit, crouch to pat and chat; the bones deform the skin (checked: petting pose, two of them chatting outside).
- Not perfect: Lily's raised-hand mesh was baked into a lowered arm and the hand is slightly distorted on that side, Amir's hair is auburn and Lily's outfit is pink (their own texture colours), the cat has big cartoon eyes and is more black than white.

**Next**
- Amir looks at them and says what to change, or picks other models.
- Face expressions, walk cycles from animation clips, customising looks, pet sounds, the mailbox and love letters.

## Session 18: looks, walking, night sky (2026-09-26) · v0.19.1

- Lily: fairer skin, glasses hidden (`CharacterRig.hideMaterial`). Amir: black hair, glasses (`glasses.fbx`, parented to his head bone by `GreyboxBuilder.PutOnGlasses`, fitted by looking at a close-up).
- Walking: the stride phase follows the distance actually travelled (no more feet sliding), with knee lift, arm swing, hip bob and a small torso twist; pets too. Not checked frame by frame, only that the code runs.
- Night sky: atmosphere multiplier and clouds fade out at night, 2200 stars per cubemap face, a moon disk with a generated texture (HDRP celestial body), less bloom. **Found:** the depth of field blurred the sky (infinitely far) into fuzzy blobs, so the far blur is off at night (`CinematicFocus`). The moon still glows a lot, the disk detail is not visible.

## Session 19: living room fixes (2026-09-26) · v0.19.2

- The two loose sofa cushions were 3 cm in the air (the model puts them there): `SettleSofaCushions` lowers each until its lowest point rests on the seat. The living room armchair had its back to the room (the model's front is +Z, the sit spot was also turned round): now faces the sofa and the sit spot matches. One book of the coffee table set stood at the table's end and slipped down beside a leg: the set is moved to the middle of the table.
- **Gotcha:** the layout saved in PlayerPrefs (`dearlife.layout`) overrides the defaults for any piece that was moved, so a changed default only shows after "Reset layout".

## Session 20: seats that fit, no green cushions, lower shrubs (2026-09-26) · v0.19.3

- Sitting and lying now follow the piece (`UseSpot` fields, `CharacterRig.SitTargets` and `LieTargets`): the torso leans back like the backrest (sofa 16, armchair 22, beanbag 38 degrees), the legs are solved from the seat height so the feet reach the floor or the bar stool foot ring (knees high on the beanbag, thighs sloping on the stool), the lounger raises the torso 58 degrees like its backrest, the hammock curves the body up at both ends. Checked in Play mode on the sofa, lounger and hammock; the bar stool, beanbag and beds were not seen in the final pose (Unity was in the background, time stood still).
- The green sofa cushions are removed. The `shrub_02` plants sit 16 cm lower so they touch the lawn.
- **Lesson:** my test scripts turned off the orbit camera and depth of field in Play mode and the scene got saved that way, so the mouse did nothing. Always check `git status` and the scene diff for `m_Enabled: 0` after testing.

## Session 21: no skin poke-through, ghosts in decorate mode, default layout, Sims 4 rule (2026-09-27) · v0.20.0

- **Skin popping out of Lily's outfit:** the body and the cloth were skinned as separate shells, so at bent hips and knees the skin came through. `tools/blender_rig_lily.py` now sinks the body 6 mm under the outfit, pushes the cloth and hijab out 3 mm and, most important, gives the cloth and hijab the skin weights of the nearest body vertex (`copy_body_weights`), so the shells bend together. Checked seated on the sofa and walking: no more patches. The fairer skin texture is kept (the export overwrites the PNGs, copy the edited ones back after re-running the script).
- **Decorate mode:** everyone stops where they are and turns into a see-through blue silhouette (`Character.SetAllFrozen`, `Ghost.mat`), and returns to normal when it is switched off. People who are sitting stay on their seat if the piece is moved.
- **Default layout:** the layout Amir left the house in (22 pieces, from the saved PlayerPrefs) is in `Assets/Editor/DefaultLayout.json` and `GreyboxBuilder.ApplyDefaultLayout` puts it in on every build, carrying small things along with the piece they stand on. The saved copy in PlayerPrefs is cleared. To change the default again: copy the saved `dearlife.layout` into that file and rebuild.
- **Lounger:** checked with a real pose: the body lies along the backrest with the head up (it was upside down before the v0.19.3 sign fix).
- **New rule 7:** play like The Sims 4 (controls, build and buy mode, money, needs, pie menu). Plan in `docs/SIMS4_PLAN.md`. None of it is built yet except what was already there.
- **Test tip:** Unity does not tick game frames while in the background from the MCP, so pause and call `EditorApplication.Step()` in a loop from `execute_code`.

## Session 22: live mode and the pie menu (2026-09-27) · v0.21.0

- First Sims 4 milestone (see `docs/SIMS4_PLAN.md`): `LiveMode` (pick a person, plumbob, click to walk, round menu on furniture and people, speed controls) and an order queue in `Character` (`GiveOrder`, `Order`, `StartOrder`, `CancelOrders`). People and pets are picked by screen distance, not colliders.
- Checked in Play mode by stepping frames: an order to walk took Amir across the house to the spot, a "Sit down" order from the pie menu made him walk to the sofa and sit, the menu and the green diamond draw correctly. **Not tested with a real mouse**, so clicking, hover colours and the right click to close the menu need a try.
- Key changes: 1, 2, 3 are speeds now (floors moved to Page Up, Page Down, Home), the middle mouse turns the camera and the right mouse only moves it.

## Session 23: face, hand, feet, lounger, atom marker (2026-09-27) · v0.21.1

- **Lily's face** had been flattened by the outfit fitting (the face skin was sunk under the hijab while the eyes stayed put): the fitting now leaves the head and feet alone. **Her pinky** stuck straight up because part of that hand was weighted to the upper arm: hands are now one rigid piece on the forearm (radius 0.2 beyond the width of the body). **Her feet** were stretched because there were no foot bones: `foot.L` and `foot.R` are added at the ankles and `CharacterRig.FeetTargets` keeps them flat (with toe-off and heel strike when walking). All in `tools/blender_rig_lily.py`. Checked in Play mode: face, hand, walking feet.
- **Lounger:** the seat point is lifted out of the pad (0.55 up, 0.16 back) so she lies on it, not in it.
- **Picked person marker:** an atom (glowing ball with three spinning rings) replaces the green diamond, and follows the head (`CharacterRig.HeadTop`), also when sitting or lying.

## Session 24: Lily made symmetrical (2026-09-27) · v0.21.2

- The Sketchfab model is built from separate parts (79 islands: each sleeve, arm, hand, nail, pant leg, shoe...), and the two sides came from a posed figure (left arm raised in a wave, legs mid stride), so after straightening she was lopsided. `tools/blender_symmetrize.py` now throws away her left arm parts and right leg parts and rebuilds them as mirrored copies of the right arm and the left leg (same materials and UVs, weights with L and R swapped), then moves the bones of those limbs to the mirror of the master bones. Result: front, side and back views match on both sides.
- **Side effect handled:** the mirrored left pant leg poked through the back of the tunic (the tunic is a bit flatter on that side), so the top of the pant legs is pulled in and forward under the tunic.
- Run order in `tools/blender_rig_lily.py`: fit the layers, skin, hands, copy weights, join, straighten, symmetrize, feet, export. After exporting, copy the edited skin texture back (the export overwrites the PNGs).
- Checked in Play mode: front view standing and mid stride, hands, legs.

## Session 25: living room, working TV, shed, flower beds (2026-09-27) · v0.22.0

- **Living room:** a walnut TV console (`tools/blender_living.py`, `tvunit.fbx`) faces the sofa, book cases on the west wall, a bean bag, two floor lamps, a second side table, two plants, a globe and candles. The layout keeps the paths open (checked with a NavMesh grid: the two people can reach the sofa and every seat).
- **Working TV (`TvScreen`):** the "Screen" part of the model shows a made up moving picture (sunset, colour waves, a game show, all invented), glows and lights the room, softer at night. Click it for a pie menu: "Turn on/off" (the person walks to it first) and "Watch TV" (sits on the nearest free seat that faces it, and switches it on). People who sit down in front of it on their own put it on about half the time, and it goes off when the last watcher who switched it on leaves.
- **Seats:** `Character.GoToApproach` finds another way in when the usual approach spot is blocked (the coffee table), and `SampleFloor` stops people picking the top of a table or sofa as a floor point (that was why "I can't get there" came up).
- **Atom marker:** the three rings now each spin about their own axis (x, y, z) at different speeds.
- **Shed:** the floating black square and sticks were the model's window and pegboard with hung tools outside the west wall: removed. Tool rack moved beside the shed door wall.
- **Flower beds** in the yards are on one line each (z 19.8 at the back, z -2.8 at the front, so they run parallel to the pavement).
- **Default layout:** the six pieces moved since last time (wheelbarrow, sectional, armchair, two lanterns, beach ball) were added to `Assets/Editor/DefaultLayout.json`.

## Session 26: four seasons, colourful atom, petting pose (2026-09-27) · v0.23.0

- **Seasons** (`SeasonCycle.cs`): see `docs/SIMS4_PLAN.md`. Recolours copies of the Lawn, LawnEdge, Hedge and outdoor leaf materials (never the assets, so git stays clean), hides flowers and leaves in winter, and drives `DayNightCycle` through static values (sun peak 32 to 74 degrees, sunrise 5:30 to 7:00, sunset 17:15 to 19:00, warmth, clouds, haze). Weather is four particle systems over the plot. Checked all four by looking (winter needed a very bright lawn tint to read as snow; the first autumn was muddy, so the warm shift and haze were toned down). The chosen season is saved (`dearlife.season`).
- **Petting pose:** the crouch is now solved from the leg length so the feet are on the floor and the hip is at 0.5 m, with a forward lean. **Found:** Amir's spine and neck bones are turned the other way round to Lily's, so the sign is flipped for him in `CharacterRig.Set` (this also changes how he leans when sitting: back against the sofa now).
- **Atom marker:** higher, and each part has its own colour that drifts round the rainbow.
- **Removed** from the living room: potted plant 4, the calathea and the globe. Default layout updated again.

## Session 27: the city around the plot, leaves, season fade (2026-09-27) · v0.24.0

- **City** (`Assets/Editor/CityBuilder.cs`, run from the greybox build): a grid of streets (six north-south and four east-west, the road past the garage is one of them), sidewalks, lane lines, street lamps and street trees, lots filled with modern flat-roofed houses near the plot (2 to 3 floors), apartment blocks further out (4 to 9) and glass towers on the horizon (up to about 110 m). About 130 meshes (one per block and material, saved in `Assets/Art/Meshes/City`, cast shadows only near the plot, ray tracing off). Facades come from a generated window texture (world space triplanar) with lit windows that come on at dusk together with the lamps (`CityNight`, works on material copies). The plot, fence and its road are untouched; buildings are cut down to strips around the plot's rectangle. **The greybox build now takes about 5 to 10 minutes** because of the meshes, do not think it hung.
- **Seasons in the city:** street trees follow the season colour (`CityLeaf`).
- **Falling leaves** are now real leaf shapes in three colours (rust, gold, brown) that tumble on all axes, instead of glowing dots that looked like ash. **Found:** HDRP unlit particles ignore the particle start colour, so each colour needs its own material and system.
- **Season change:** what is still in the air of the old season fades away in about a second (`SeasonCycle.Fade`).
- Not done: shops or people in the city, traffic, city sounds; the windows on far towers repeat the same pattern.

## Session 28: city in the house's style, traffic, planes, tree seasons (2026-09-27) · v0.25.0

- **City style:** three generated facade textures like the house: floor to ceiling glass with slim black mullions and a slab edge every floor, timber slats with wide windows, white plaster with big windows (`CityBuilder.Facade`). Houses near the plot are a glass ground floor with a timber or white upper floor hanging over one side and a thin white roof slab that overhangs; apartment blocks and towers are glass with a timber or white core and a glass roof pavilion. Windows light warm at night. The older sand and dark facades are gone.
- **Traffic** (`CityTraffic.cs`, built from boxes at start): 14 cars in six colours (saloon, hatchback, van) drive on the LEFT on the road grid at 7 to 13 m/s, wrapping round at the edge, headlights and tail lights on at night. **An aeroplane** crosses the sky at 260 to 340 m every 2 to 4 minutes (about 95 m/s), with a blinking beacon and wing strobes. Cars do not react to each other at crossings, they can pass through each other there (rare).
- **Particles halved** (petals 18/s, leaves 35/s per colour, snow 550/s, fireflies 13/s).
- **Yard trees follow the seasons:** the leaf cards are recoloured on the GPU (`Assets/Shaders/Recolor.shader`, done once per tree texture): cherry blossom pink in spring, orange, gold and rust in autumn, original green in summer, bare in winter with white snow on the branches and trunk. The shrubs stay all year (white in winter). **No online tree import was needed.**

## Session 29: winter trees (2026-09-27) · v0.25.1

- **Bug:** the yard trees vanished in winter. The trunk and branches share one renderer with the leaf cards, and winter switched that renderer off. Now the leaf cards are cut out (alpha 0 on their material copy) and the renderer stays on, so bare branches with snow show. Checked by looking at the yard in winter.

## Session 30: seasons on the roof (2026-09-27) · v0.26.0

- The house roof now takes part in the seasons (`SeasonCycle.MakeRoofLayers`, one set per roof piece, 38 in all): **snow** grows into a slab over the winter and melts away after it; **fallen leaves** in autumn and **fallen petals** in spring are cut out sprites laid over the roof that appear a few at a time (each sprite has its own threshold in the alpha, the material's alpha cutoff sweeps from 1 to 0.45 as the season builds) and thin out again when it ends. The city roofs and roof slabs go white in winter (tinted copies of `CityRoof` and `CitySlab`).
- Checked in the whole house view in winter, autumn and spring. I saw one turquoise flash on the roof right after switching to autumn that I could not reproduce, keep an eye out for it.
- Only the house has leaves and petals on its roof, the city roofs only get snow.

## Session 31: the eight Sims 4 milestones, first version (2026-09-27) · v0.27.0

- **Backup first:** a copy of v0.26.0 (without Library) is in `S:\Dearlife by Zetazuni Backups0.26.0 (2026-09-27)`.
- **Needs and mood** (`Sim/SimData.cs`), **things to do** (`Sim/Interactions.cs`, about 30 interactions on about 40 kinds of object, plus the pool), **autonomy** in `Character` (emptiest need first, chat or pet for friends, jobs at day time, random fun), **money** (`Sim/Household.cs`), **shop** (`Sim/BuyMode.cs`, `Catalog` filled by the builder, `FurnitureFactory` makes pieces at runtime, purchases and sales saved), **build mode** (`Sim/BuildMode.cs`), **status panel and icons** (`UI/SimUi.cs`), **sound** (`Sim/GameAudio.cs`, everything synthesised), **hints** (`UI/Tutorial.cs`).
- Time now runs by default (`dn.auto = true`).
- Checked in Play mode by stepping frames: an order to cook took Lily to the kitchen, hunger rose while she cooked and RM 20 was charged; Amir on his own read a book, cooked, used the toilet, took a bath and went to pet Bedah when his needs were low; a purchase follows the mouse (cancel puts it back free); a room, a doorway and paying for them worked; the status panel and the round menu draw. **Not tested with a real mouse or a full session:** clicking in the shop and the build panel, placing a bought piece with a click, painting, knocking down, selling, the sleep and sunbathe interactions, working for pay, the sounds.
- **Test leftovers cleaned:** funds, built walls, purchases and hints are reset in the saved settings after the tests.
- Known gaps are listed in `docs/SIMS4_PLAN.md`.

## Session 32: the look and sound of the 2D app (2026-09-27) · v0.28.0

- **Sound:** copied what the 2D game already had: the seasonal lofi music (progressions, BPM, hooks, swing, echo, vinyl crackle; rendered in a background task, about a second per season, swaps with the season), the coin, pop, buy, love, level up and error jingles, the recorded cat meow and purr (Bedah meows when petted and now and then on her own, purrs while petted). Filled in what it never had: sizzling when cooking, running water for showers, baths, washing and swimming. Volume sliders in Settings. The guitar, synth and dog recordings are copied into `Assets/Resources/Audio` but nothing plays them yet (no guitar, keyboard or dog in the 3D house).
- **UI remake:** every panel now follows the 2D game (see the UI paragraph in `CLAUDE.md`). New: top bar with title, money, date and speed; camera buttons; side panel with Home, Family, Shop, Build and Settings tabs (the shop and build tool moved from a bottom bar into it); the shop has pictures (64 clay renders from Blender); needs card, person cards, status window, toasts, round menu, name tags, speech bubbles and the hint card all restyled; a splash intro like the 2D one (click skips). Fonts installed: Nunito (Regular, Bold, ExtraBold, cut from the variable font) and Great Vibes, both OFL.
- Checked in Play mode by looking at each tab, the status window, the hint card and the round menu, and by measuring that the music clip is made and playing. **Not tested:** real mouse clicks on every button, the scroll wheel in the panel, tiny windows, the meow and purr by ear, the water and sizzle by ear.
- Not changed: the scene (all of this is code, `Main.unity` was not rebuilt). `M` still moves furniture, `P` hides and shows the side panel.

## Session 33: dark mode, settings, colours, undo, cars, small things, build mode (2026-09-27) · v0.29.0

- **UI:** the settings moved out of the side panel into a window opened from the top bar (Sound, Time, Picture, Game). The Hide panel button is now a vertical tab on the edge of the panel. **Dark mode** copies the 2D game's neon night look (pink and ice blue, gradient borders and glows), switch in Settings, Game.
- **Colours:** Sims 4 style swatches (fabric and paint, wood and stone) in the round menu and in the shop. Saved. The sofa in the shop no longer has the two loose green cushions.
- **Decorate mode:** click once to pick a piece up, click again to put it down. Undo and redo for moves, buying, selling and colours (Ctrl+Z, Ctrl+Y, top bar buttons).
- **Small things** are separate pieces now (before, they were parented to whatever they stood on): mug, fruit bowl, candles, books, vase, desk lamp, plants, boxes, towels and more can be picked up alone, and are in the shop under Decor. Also added: armchair, side table, potted plant.
- **Cars:** four models from Sketchfab in the shop (see `CREDITS.txt`), 30 to 400 thousand triangles each. The old sedan and MPV in the garage are unchanged. Prices are far above the RM 15,000 start so they are goals.
- **Build mode** rewritten (see `CLAUDE.md`). New shop pictures for all the new things (`tools/blender_thumbs.py`, now also reads glTF and glb).
- **Scene rebuilt** (the shop lists live in the generated scene). Checked afterwards that the orbit camera is on.
- Checked in Play mode: settings window and dark mode, the vertical tab, colours on the sofa and a Porsche, undo and redo of colours, undo and redo of a room with a window, an archway and a diagonal half wall (money came back and went again exactly), picking a sofa up (it follows the mouse until cancelled), the shop with pictures, the cars in the shop list. **Not tested with a real mouse:** the grid and measurement labels while dragging, the opening previews, the eyedropper, clicking the colour swatches, click to place, Ctrl+Z, the meow and other sounds. Please try those.
- Not done: stairs and roofs in build mode, walls in the shop for hanging pictures, more paint options for the cars' wheels and glass.

## Session 34: lots, the map, pools, stairs, careers, instruments, a dog, build prep (2026-09-27) · v0.30.0

- **Keys:** M is the map now, P moves furniture, H hides the panel. **Delete sells furniture again:** since the click to pick up change, carrying a piece counted as a new purchase, so Delete only cancelled it (and Sell threw it away for nothing). Now Delete while carrying a piece from the house sells it, and with nothing in the hand it sells the piece under the mouse.
- **Home is locked:** build mode only allows Paint (and the Eyedropper) on the home lot, the other buttons are greyed with a note.
- **Three empty lots** you reach from the map (M), each 38 x 30 m, with a For sale sign and trees. Build a whole house there (walls, rooms, floors, archways, windows, stairs, two storeys, roofs) and **pools** (draw one or pick a layout).
- **Known gaps closed:** stairs and roofs, careers with promotions, the guitar, synth and dog recordings are used, held props and more poses (interactions still use simple procedural motion, not authored animation).
- **Must-fix before building:** models are readable (the 'does not allow read access' warnings are gone), `BuildPrep` sets the player settings, keeps the shaders that are found by name and makes the exe. **Not done yet at the time of writing:** a real test of the built exe (see the next entry).
- Checked in Play mode: the map and travel (three people and the pets arrive on the lot and stand on the navmesh), a room with a window and an archway, stairs, a flat roof, a pool with a person swimming in it, the greyed build tools on the home lot, selling and undoing a sale, adopting a dog, a guitar in Amir's hands, the career strip in the status window. **Not tested with a real mouse:** the map clicks, dragging pools and roofs, placing pool layouts and stairs with R, clicking the career chips.


## Session 35: loading screen, Graphics settings, the exe finally renders, disk cleanup (2026-09-27) · v0.31.0

- **Loading screen** (Sims 4 style): the place name in script, a rose/violet/cyan bar that really fills, a rotating "did you know" tip. Shows at startup (until the walkable ground and the seasonal music are ready) and on every trip to another lot, replacing the plain fade.
- **Settings: Picture → Graphics.** New window mode dropdown (Windowed, Borderless windowed, Fullscreen; borderless is the default), a resolution dropdown (greyed out in borderless), V-Sync toggle, and a frame limit dropdown (30/60/120/144/Unlimited, greyed out while V-Sync is on). Saved per PC, applied on the exe's startup (the Editor's Game view is left alone). The Ultra/Quality/Performance picture-quality buttons moved into the same tab.
- **The black-window bug is fixed.** The console had been warning "Default Volume Profile has been modified to ensure all components are present" — saved that profile (it was missing a `VisualEnvironment` override) and rebuilt. Confirmed in a real run of the exe: renders fully, `Player.log` has zero `NullReferenceException`s (was 1823 before).
- **Found a second build bug along the way:** Unity 6's incremental player-build cache can report "Succeeded" without actually rewriting the exe, especially right after the Editor's own install path changed (see below) — three builds in a row silently no-op'd while still logging a plausible success message. Fix: delete the output folder (`S:\Dearlife Builds\Dearlife`) before building so there's nothing to skip. Worth remembering for future builds if the exe timestamp doesn't move.
- **Disk cleanup, ~48 GB reclaimed on C:** the imported cars' glTF shader was failing thousands of DXR ray-tracing variant compiles (`Opcode Sample not valid in shader model...`, still visible in the console — cosmetic, not build-breaking), and every attempt was writing to NVIDIA's shader cache: it had grown to ~22 GB in one day. Cleared it, plus Windows' Delivery Optimization cache (~12 GB, unrelated to this project). Then **moved the Unity Editor install itself off C:**: `6000.6.3f1` now lives at `S:\Unity\Editor\6000.6.3f1`, with a directory junction left at the old `C:\Program Files\Unity\Hub\Editor\6000.6.3f1` path so Hub and every shortcut still work unchanged (another ~15 GB back).
- **New: `S:\Tools\DiskUsage\`.** `snapshot.ps1` records folder sizes on C: and S:, `report.ps1` diffs the two newest snapshots into a markdown report, and a weekly Windows scheduled task ("Dearlife Disk Usage", Mondays 9am) runs both automatically from now on — so a sudden free-space drop can be traced exactly instead of retraced from file timestamps after the fact.
- Checked in Play mode: the loading screen at startup and on a trip to Maple Court, the Graphics tab (window mode, resolution, V-Sync, frame limit dropdowns, picture quality buttons). Checked as a real standalone run: the exe opens, renders the house and characters, no black window, no `NullReferenceException`. **Not tested:** actually switching window mode or resolution inside the running exe (only the settings UI itself, and the saved-value plumbing).

**Next**
- Try changing window mode/resolution live in the exe and confirm it takes effect without a restart.
- Pet actions beyond Pet, Play and Feed are still a known gap.
- Untested-with-a-real-mouse items carried over from Session 34 (map clicks, dragging pools/roofs, pool layouts and stairs with R, career chips, grid/measurement labels, previews, the eyedropper, click-to-place) are still open.

## Session 36: the exe's own icon (2026-09-27) · v0.31.0

- The built exe was still showing Unity's default star/shield icon in File Explorer and the taskbar, despite `BuildPrep.Prepare()` already calling `PlayerSettings.SetIcons`. Two bugs: it only passed one texture when Unity expects one per required size (1024 down to 16, silently ignoring the whole call if the count doesn't match), and even fixed up to the full set, in-memory-only `Texture2D`s didn't stick - `SetIcons` needs real imported assets.
- Fixed: `Prepare()` now resizes `Assets/Art/Icon/icon.png` (a GPU blit + readback, so the source doesn't need Read/Write enabled) into `Assets/Art/Icon/Generated/icon_<size>.png` for every required size, imports them, and sets the icon from those. Confirmed by extracting the built exe's actual icon resource: it's the cat-in-a-room artwork now, not the default.

## Session 37: title screen, a dark-mode toast fix, Malaysian lot names, save controls (2026-09-27) · v0.32.0

- **Fixed:** the level-up/money toast (`+RM 350: ...`) was unreadable in dark mode — its background was hardcoded to a light cream hex instead of the theme-aware `Ui.Card`, so white text sat on a near-white box. Now flips with the theme like everything else.
- **The three empty lots are renamed** to Malaysian township names: Gamuda Gardens, Savannah Suites, Kiara Greens (were Maple Court, Lantern Row, Willow Lane). Updated both `GreyboxBuilder.cs` (source of truth for a future scene regen) and the live scene's `Lot` components directly.
- **New title screen** (`Scripts/UI/MainMenu.cs`), shown at boot after the splash and loading screen, and again from Settings' "Back to title screen": a cream-on-indigo panel on the left (title in script, tagline, New Game/Start, Load Game, Settings) fading into the actual house on the right, seen through a slow cinematic camera that drifts and softly zooms between the ground-floor rooms on its own (built from `RoomMarker.All`, so no hand-tuned coordinates) — `OrbitCamera` gained `ExternalControl`/`SetImmediate` so the menu can drive it without fighting the player's own input. Bottom-left: version number. Bottom-right: "by Zetazuni" and a GitHub button (`Application.OpenURL` to the repo). "New Game" asks for confirmation before erasing an existing save (kept separate from settings, see `SaveSystem` below); "Load Game" is greyed out until there's a save to load. The HUD, map, build and decorate mode all stay closed while the menu is up (`MainMenu.Active` guards, the same pattern as `Splash.Showing`).
- **New `Scripts/Sim/SaveSystem.cs`**: the one place that knows every PlayerPrefs key the game's save state uses (separate from settings like audio/dark mode/graphics, which New Game leaves alone). `SaveNow()` flushes everything and records when; `HasSave` and `NewGame()` back the title screen.
- **Settings, Game tab:** a new Save section ("Save now" plus a last-saved time) and a Game section with "Back to title screen" and "Exit game" (a confirmation dialog, then saves and quits — `Application.Quit()`, or stops Play mode in the editor).
- Checked in Play mode: the title screen's cinematic drift and blend between shots, New Game confirming before wiping, Load Game handing control back to the HUD, the toast in dark mode, all three renamed lots on the map, Settings' Save now (last-saved text updates) and the Exit confirmation dialog, Back to title screen re-opening the menu. **Not tested:** clicking the GitHub button for real (the URL open call itself is standard and unit-testable only by trusting the OS to handle it).

## Session 38: a livelier title screen with ten independent save slots (2026-09-27) · v0.33.0

- **Fixed:** Settings opened from the title screen used to draw behind the fade (`SettingsWindow` has no explicit `GUI.depth`, so it lost to `MainMenu`'s -500). Fixed properly, not papered over: the title screen no longer opens the gameplay `SettingsWindow` at all - it has its own Settings page now (see below), drawn by `MainMenu` itself at the same depth as everything else on the title screen.
- **Ten independent save slots.** This was the big one: every system that saves (`Household`, `BuildMode`, `PurchaseSave`/`BuyMode`, `Furniture`, `WindowWall`, `PetShop`, `DayNightCycle`, `SeasonCycle`, `Sim`'s careers, `Tutorial`) now reads and writes its PlayerPrefs key through `SaveSystem.Key(...)`, which suffixes it with the active slot (slot 1 keeps the old unsuffixed keys, so every save made before slots existed still loads fine). `SaveSystem` gained `ActiveSlot`, `SetActiveSlot`, `SlotHasSave`, `SlotSummary` and `SlotLastSaved` (for the picker), and `NewGame(slot)` wipes one slot only. Settings (audio, dark mode, graphics, display) are deliberately **not** in the slot-scoped list, so they carry over regardless of which slot you're in.
  - **How switching slots actually works:** the title screen is independent of whatever's loaded, so choosing a slot calls `SaveSystem.SetActiveSlot`, optionally `NewGame` to wipe it, sets a static `pendingEnter` flag, and reloads the scene (`SceneManager.LoadScene`, now registered in Build Settings). Every system's `Awake()` runs fresh and reads the newly active slot's keys; `MainMenu.Awake()` sees `pendingEnter` and sets `Active = false` instead of showing the menu again, so the reload drops straight into play. Verified end to end in Play mode: switched from a slot with RM 11,780 and existing furniture into a brand new slot 2 and landed on RM 15,000, the default layout and the hints from scratch, then switched back and slot 1 was completely untouched. The one gotcha found along the way: `PurchaseSave` in `BuyMode.cs` is a plain static class that caches its loaded data - static fields survive a scene reload (only a domain reload clears them), so `SaveSystem.SetActiveSlot` now also clears that cache.
- **Title page is livelier**, per feedback that it felt empty: the title is much bigger (118pt) with a soft glow behind it and the gradient rule from the splash screen underneath, sixteen slow floating sparkles drift up the panel, and a row of small feature chips (Decorate · Careers · Pets · Build your own home) sits above the version number.
- **The fade is now 30:70** (was roughly centred): the panel plus its gradient take up 30% of the width, the cinematic view gets the remaining 70%, uncovered.
- **Start and Load Game now open a slot picker** (up to 10, 2 columns): each tile shows "Empty" or a one-line summary (money, season, time of day) and when it was last saved. New Game on a used slot asks to confirm before erasing it; Load Game only accepts used slots.
- **The title screen's own Settings** is deliberately smaller than the in-game one: Look (light/dark), Sound (just the master volume and mute), and the whole Graphics section reused directly from `SettingsWindow.DrawGraphics` (made `public static`, along with the shared dropdown helper, so there's one implementation, not two) - no Time tab, no hints/layout reset, nothing that only makes sense mid-game. Exit game is still here since quitting from the title is reasonable.
- **Escape backs out one layer at a time** on every title-screen page: closes an open dropdown, then a confirmation dialog, then returns to the title itself. A visible back arrow does the same for anyone not using the keyboard.
- Checked in Play mode: the bigger title with its glow and sparkles, the 30:70 fade, the New Game slot grid (including the overwrite confirmation on a used slot), the title screen's own Settings page with a working Graphics section, and the full slot-switch-and-reload flow itself (the highest risk part) confirmed correct by inspecting `Household.Funds` and `SaveSystem.ActiveSlot` immediately after the reload. **Not tested:** Escape/back with real key events (verified by code review and by driving the same state by hand, not by an actual keypress in this session), the Load Game page specifically (shares its drawing code with New Game, which was tested).

## Session 39: title screen polish, a lit spinning hexagon ring, James and Lily (2026-09-27) · v0.34.0

- **Title glow centred properly**: it was eyeballed against a fixed rect before; now it's built from `Ui.TextWidth("Dearlife", ...)` so it's centred on the actual rendered glyphs, at any resolution.
- **Exit game moved to the title page itself** (a fourth button under Settings) and removed from the title screen's own Settings page, where it felt buried.
- **Slot picker is one column of 5**, not two of ten: taller cards (84px) with room for the summary and the last-saved time to breathe, easier to scan. `SaveSystem.MaxSlots` is now 5.
- **The fade no longer bands.** It was 40 flat, hard-edged rects stepping down in alpha, visible as stripes especially over a bright background. Replaced with a single 64-pixel gradient texture (`FadeTex()`, bilinear filtered) stretched over the fade zone - one draw call, no seams.
- **The head marker is a lit, spinning, pastel hexagon ring**, not the old glowing atom (a ball with three tumbling rainbow rings on an Unlit shader, so it ignored every light in the room). `LiveMode.Torus` already supported any path segment count, so the fix was mostly about what to build with it: one hexagon-path ring (`seg: 6`) with a proper tube thickness so it clearly reads as a ring, not a wire, in an `HDRP/Lit` material (was `HDRP/Unlit`) with modest smoothness and a low metallic so it actually catches the room's real lighting like the furniture does, plus a small emissive boost so it still has a bit of its own glow. It spins steadily round Y at a tilt, and its colour eases through the game's own pastel palette (pink, lavender, ice blue, soft gold) instead of a raw saturated HSV rainbow. Moved lower (`0.20` above head instead of `0.36`) so it no longer overlaps the name tag drawn above it.
- **The two playable sims are James and Lily** (were the developer and a loved one's real names, kept out of the game from here on). Touched every place a name was hardcoded: `GreyboxBuilder.MoveIn` (the model files themselves kept their old internal keys at the time - only the display name changed; both were renamed to neutral keys in the Dearlife rebrand, see below), `SimData.Setup`'s traits/job/starting-friendship switch, `SaveSystem`'s state-key list (so New Game wipes the right career keys), and the "Welcome home" hint. The already-built scene's two `Character` components were renamed directly (same approach as the lot renaming in session 37) so no scene regen was needed.
- Checked in Play mode: the centred glow and seamless fade on the title screen, Exit game from the title page, the one-column five-slot picker, and - up close, with the camera parked over James's head - the hexagon ring's shape, visible thickness, lighting response (it shades across its surface like a lit object, not a flat colour), spin and pastel colour drift, confirmed clear of his name tag. Confirmed "James" and "Lily" show correctly in the HUD, the family bar and the status card.

## Session 40: rebrand to Dearlife (2026-09-28) · v0.35.0

- **Renamed the whole project from Tiramisu 3D to Dearlife**, ahead of making it more widely public: no real
  names left in the project at all now, past the dedication line itself (which now reads "for Lily"). Touched
  every layer: the `Tiramisu` C# namespace (now `Dearlife`), every `MenuItem("Tiramisu/...")` path, every
  `PlayerPrefs`/save key (`tiramisu.*` to `dearlife.*`, which orphans old local saves), the shader name
  (`Hidden/Dearlife/Recolor`), the HDRP settings asset (`Dearlife_HDRP.asset`), `GameInfo.RepoUrl`, the title
  screen text and the in-game "Quit Dearlife?" and Settings credit line, `TiramisuNav.cs` renamed to
  `DearlifeNav.cs`, and every doc (`CLAUDE.md`, `README.md`, `docs/*.md`) and Blender tool script.
- **The character internally keyed `athirah` is now `lily`** (the display name was already "Lily" since
  session 39, only the internal key lagged): `GreyboxBuilder.MoveIn`'s `Person(...)` call, `CharacterRig`'s
  `hideMaterial` check, and every exported asset (`athirah.fbx`/`.materials.json` and the six
  `athirah_FemaleStyle4*` material/texture pairs, all renamed to `lily*`). The old, already-tuned Sketchfab
  rig itself was not re-exported from Blender (too much risk for zero visible difference) - only its output
  files, the internal string key, and the export script (`tools/blender_rig_lily.py`) were renamed; some
  obscure mesh-node metadata inside the `.fbx` binary may still read `athirah_mesh` if opened outside Unity.
- **`Main.unity` and `Dearlife_HDRP.asset` were regenerated through the project's own tooling**
  (`Dearlife > Set up render pipeline`, `Dearlife > Import furniture`, `Dearlife > Build greybox house`)
  rather than hand-edited, the same way any other generated-content change is made here. Every doc's
  reference to the real, unrenamed sibling 2D project (`S:\Tiramisu App by Zetazuni`, repo
  `zetazuni/athirah-cozy-corner`) is untouched, since that's a different, real project.
- The project folder moved to `S:\Dearlife by Zetazuni`, the backups folder to
  `S:\Dearlife by Zetazuni Backups` (name only, contents left as a historical record), and the stale build at
  `S:\Tiramisu Builds\` was deleted (it predates this rebrand and will regenerate at
  `S:\Dearlife Builds\Dearlife\Dearlife.exe` next time someone runs `Dearlife > Build Windows game`). The
  GitHub repo was renamed `zetazuni/tiramisu-3d` to `zetazuni/dearlife`, its description updated, and its
  git history rewritten to a single fresh commit so no old commit message or diff mentions the old names
  either.

## Session 41: title screen polish, hide-names setting, missing-script fix (2026-09-28) · v0.35.1

- **The glow behind the title now bleeds off the left edge of the screen**, with a slow breathing drift
  (`Mathf.Sin(Time.unscaledTime * 0.22f) * 16f`) instead of sitting still, and reaches far enough right to
  softly spill into the fade toward the cinematic view.
- **Fixed the "Load Game unlocks once you've played" hint overlapping the Settings button** below it: the
  hint now reserves its own row instead of being squeezed into the existing button gap, and the gap between
  every title screen button grew from 14 to 20 for more breathing room.
- **New Settings toggle: hide names** (`Ui.ShowNames`, saved as `dearlife.showNames`, Game tab, on by
  default). `CharacterHud` only draws a name tag when it's on; speech bubbles are unaffected. The title
  screen's cinematic shots never show names regardless of the setting, since a menu background isn't the
  place for it - `CharacterHud` checks `!MainMenu.Active` unconditionally.
- **Fixed the pre-existing "missing script" bug** on `Catalog` and `InteractionSetup` flagged in the last
  session: both lived in a multi-class file that didn't match their own name (`BuyMode.cs`,
  `Interactions.cs`), the same issue already documented here for `LotManager`/`Lot.cs`. Moved each into its
  own file (`Catalog.cs`, `InteractionSetup.cs`); confirmed with a scan of `Main.unity` that zero components
  on the root `Game` object have a broken `m_Script` reference now, versus two before.
- Checked in Play mode: title screen screenshot confirmed the wider glow, the fixed button spacing, and no
  name tag over the person visible in the background room; Console had zero errors or missing-script
  warnings after rebuilding the scene.

## Session 42: cheats on the mailbox, Sims style hover glow (2026-09-28) · v0.36.0

- **Cheat mode** (`Sim/Cheats.cs`): switched on in Settings, Game tab ("Cheats are on (right click the
  mailbox)", saved as `dearlife.cheats`, off by default). With it on, a right click on the mailbox opens a
  round menu of cheats instead of the mailbox's usual menu (`LiveMode.OpenCheats`, pages Money, Needs,
  "Skills, career and mood", "Time and season", each with a Back button). Every cheat reopens its own page in
  the same spot (`ShowMenuHere`), so it can be clicked again and again. Money: +RM 1,000 / 10,000 / 50,000 /
  250,000. Needs (for whoever you're playing): fill all, fill each of the six on its own, fill everyone's
  (pets too), and "Needs never drop" (`Cheats.NeedsFrozen`, checked in `Sim.Update`; needs still fill up when
  used). Life: max all skills (level 10), promote to the next career rank (goes through `Sim.WorkXp`, so the
  bonus, toast and save happen as normal), grant a wish (pays its reward), "Feel fantastic" moodlet, everyone
  best friends. Time: morning, noon, sunset, night, next season.
- **Hover glow** (`House/HoverHighlight.cs`): the thing under the mouse gets a soft white shell, a little
  bigger than the piece (3.5 cm, clamped to 1.2 to 6 percent of its size), that eases in and out
  (`1 - exp(-14 dt)` on unscaled time, so it works while paused). In live mode only things with something to
  do light up (`LiveMode.SeatOwner`, plus the TV), not when a click would go to a person instead, and the piece
  whose round menu is open stays lit (`LiveMode.MenuPiece`). In decorate mode anything movable lights up
  (`DecorateMode.HoverTarget`, same rules as picking up, windows included) and the piece or window you are
  carrying stays lit the whole time (`HeldPiece`, `HeldWindow`). The shell is copies of the piece's meshes
  with an `HDRP/Unlit` transparent material and no colliders, in its own object, rebuilt when the piece's
  parts change (windows rebuild while sliding), so the real piece and its physics are never scaled or
  touched. It makes itself at startup (`RuntimeInitializeOnLoadMethod`, `DontDestroyOnLoad`), no scene change.
- The HUD's small line under the title said "3D · v..." left over from the old name; now just the version.
- Checked in Play mode (driving it by code, since the mouse can't be moved from here): two +RM 10,000 cheats
  took funds from 15,000 to 35,000, filling one need then all worked, promote went rank 1 to 2 with its
  bonus, skills reached level 10, frozen needs held at 100 over several seconds. Screenshots: the cheat menu
  on the mailbox (and the Needs page with all ten options fitting), the mailbox and then the sofa lit white and
  slightly bigger while their menus were open; the carried gnome's glow was at full strength while the
  mailbox's faded back out, and the gnome went back exactly where it was when the move was cancelled.
  **Not tested with a real mouse:** the right click on the mailbox itself and hovering by hand (both go
  through the same code that was tested, but deserve a quick try).

## Session 43: dark mode bubbles, tags behind the HUD, character plan (2026-09-28) · v0.36.1

- **Speech bubbles were white text on cream in dark mode**: `Ui.Bubble` had a fixed cream fill (`fffaf2`) but
  used `Ui.Ink`, which turns near white in dark mode. It now fills with `Ui.Card`, like every other panel.
  Scanned all UI code for the same mistake (fixed fills with theme text, fixed text on theme fills): the only
  other ones were the status window's negative feelings (dark red) and income lines (dark green), which now get
  lighter shades in dark mode.
- **Name tags and speech bubbles drew over the HUD** (a tag on top of the side panel): every HUD script draws at
  `GUI.depth` 0, so the order was left to chance. `CharacterHud` now draws at depth 10, behind all of them.
  Checked by parking James under the side panel with the game paused: neither his tag nor his bubble showed
  over it.
- **Rule 8** in `docs/RULES.md`: people are hyper realistic, inspired by inZOI, never copied.
- **`docs/CHARACTER_PLAN.md`**: the full plan for realistic characters and a character creator. It starts
  with a scan of the current characters (posed on the road in daylight with the AI off, four angle contact
  sheets of Stand, Crouch and Wave): skin pokes through Lily's tunic at the hip and lower back in a crouch, her
  13 bone skeleton has no fingers, James's torso bends like a rigid tube and his sleeve stays behind when he
  waves, and the "who you're playing" ring sits beside Lily's head. Licences were checked: MPFB2 (MakeHuman
  for Blender, CC0 assets, a rig that maps fully onto Unity's Humanoid) is the base; MetaHuman is now allowed
  in Unity but its raw files in a public repo are unclear and its face solver is Unreal only; Unity's Digital
  Human package (Unity Companion License) supplies skin, eye and hair shaders. Clothes follow body sliders by
  reusing MPFB2's per vertex clothes binding at run time, and hide the body under them (delete groups) so
  clipping cannot happen. Seven phases, a clipping scan tool, and three questions for Amir at the end.
- Nothing about the characters was changed yet: the plan waits for Amir's answers.

## Session 44: character plan phase 0, a realistic test person (2026-09-28) · v0.37.0

- **Decisions recorded** in `docs/CHARACTER_PLAN.md`: free assets only, adults only, start fresh (Lily and James are
  not remade; a new household comes from the creator in phase 5).
- **MPFB 2.0.17 installed** from extensions.blender.org (checksum checked) into a local extension repo on S:, with its
  user data on S: and MakeHuman's CC0 system asset pack loaded. It works on Blender 5.2.2, so no second Blender.
- **`tools/blender_mpfb_body.py`** builds `mpfb_test`: a 1.66 m adult on the 53 bone `game_engine` rig (fingers, which
  the old characters lacked), eyes, brows, lashes, teeth, a young adult skin, a casual outfit, shoes and long hair, with
  twelve slider shape keys on every mesh and the bone movement per slider. MakeHuman's height ends (about 1.2 m and
  2.3 m) are replaced by 1.50 m and 1.93 m, and age by 18 and 60. The body under the clothes is deleted for this test
  person; phase 3 hides it at run time instead so outfits can change.
- **`BodyShape`** (new) and **`CharacterRig`** `kind = "mpfb"`: see the architecture notes in `CLAUDE.md`.
- **Tested in Play mode:** spawned with the old AI it walked from the living room out to the garden by itself; a copy
  with weight, height and gender raised came out taller, heavier and male with its clothes and hair following and the
  skeleton moved to match (feet on the ground, joints in the right places); the crouch that pushed skin through Lily's
  tunic now shows no skin through the jeans or shirt, the torso leans forward and the hands reach out with fingers.
  At neutral the rebuilt bind poses match Unity's import to within 0.000001.
- **Not done yet, on purpose:** the test person is not in the scene (only spawned by test code), skin, eyes and hair
  still use MakeHuman's plain materials (phase 1), and the rig is still posed by code, not animation clips (phase 2).

## Session 45: character plan phase 1, realistic skin, eyes and hair (2026-09-28) · v0.38.0

- **Found in HDRP itself (free):** HDRP 17.6 ships Skin, Eye, Hair and Hair Physical shader graphs and Skin, Iris and
  Sclera diffusion profiles, so no extra package was needed. The Skin profile was already registered; `CharacterLook`
  adds Iris and Sclera to the default volume's Diffusion Profile List.
- **Skin:** HDRP's Skin shader (subsurface scattering) with a seamless 512 px pore map generated by code (9,000 soft
  pits over three octaves of fine grain, in HDRP's detail layout). MakeHuman skins only come with a colour map.
- **Eyes:** HDRP's Eye shader needs one object per eye in a set geometry (read from its EyeUtils.hlsl), MakeHuman has
  both eyes in one skinned mesh, so `Eyes` builds two eyeballs with a cornea bulge and hangs them on the head bone.
  First try looked bug eyed: MakeHuman's eye is only the front of a ball (31 mm wide, 23 mm deep), so finding the centre
  from its back put ours 10 mm too far forward; they are now placed by the front of the eye. Iris and sclera are cut
  from MakeHuman's eye texture (the iris edge found by walking out from the pupil), which works for all ten eye colours.
- **Hair:** HDRP's Hair shader. Bright streaks across the crown came from the shader's secondary highlight (0.5) on
  cards whose UVs do not follow the strands, plus MakeHuman's painted shine; now a strand map with the shine divided out
  and a soft sheen. Bare skin at the parting is gone: the export paints a hair coloured scalp into the skin texture
  wherever the hair lies within 2 cm of the head.
- **Tested in Play mode** with daylight close ups (the cinematic depth of field focused by setting the orbit camera's
  `distance`): the skin shades softly, the eyes sit inside the lids with a visible iris and wet cornea, the hair reads as
  dark brown with no streak and no bare parting. Cyan hair and eyes on the first frames were only the editor compiling
  the new shaders.
- **Still to improve:** a faint light line at the top of the parting in direct sun, brows are a little heavy, and the
  proper studio light belongs to the creator (phase 4). The test person is still not in the scene.

## Session 46: character plan phase 2, motion capture (2026-09-28) · v0.39.0

- **Clips:** the CMU library's index (2,548 described takes) was searched for every pose; 17 were downloaded (standing
  still, walk, sitting in chair, waving, pick up, very happy, eating, mixing batter, palm pilot for reading, weight
  lifting, typing, washing, swimming, sipping coffee, piano, salsa, conversation). Credits and the requested citation
  are in `Assets/Art/Animations/CMU/CREDITS.txt`. No guitar recording exists, so Guitar borrows the piano clip.
- **Three bugs found on the way:** (1) the downloaded FBX files say 0 fps, Unity assumed 1 fps and its Humanoid import
  kept three poses per clip (a walk stride took 2.3 s); re-saved at 30 fps through Blender. (2) Arms came out raised
  (a relaxed seated arm in the air): the T pose reference ignored the arm's roll, and the roll was then measured
  against the CMU root's "forward", which points up (the root is turned 270 degrees about X). Now the body's own axes
  are used; source and target arm angles agree within a few degrees in every clip. (3) Lying used the root's tipping
  and a floor lying clip at once, so lying uses the standing hold, which the tipped root turns into lying face up.
- **Fitting to furniture:** hips land on the `UseSpot` exactly and both feet are planted (under the knee, not below a
  foot the recording stretched forward). Measured on all ten seat types: 0.0 cm hip error, ankles 7 cm above the floor
  (the ankle height) on chairs, benches, sofas and the beanbag, on the foot ring on the bar stool. Loungers and hammocks
  raise the torso and thighs like before.
- **Tested in Play mode:** the test person walked with the AI (natural arm swing and stride), chose to pet Bedah on her
  own, sat on the sofa when told to (relaxed, hands on thighs); the beanbag (knees up, feet flat), the lounger (leaning
  back on the raised backrest) and a row of 14 copies each playing one action pose were checked by screenshot.
- **Not done yet:** hand IK so held props (book, mug, guitar) sit in the fingers instead of hanging off the forearm,
  foot IK on stairs, a proper guitar clip, and the crouch is a bend to pick up rather than a kneel to pat a pet.

## Session 47: character plan phase 3, clothes; quieter nights; feminine women (2026-09-28 to 29) · v0.40.0

- **Night crickets removed.** The night sound was a synthesized 4.3 kHz chirp in `GameAudio.cs`; it came across as a
  distracting beep, so it is gone (wind and daytime birds stay).
- **Women look feminine.** The test person had been shown at MakeHuman's neutral gender, halfway between male and
  female, which gave broad shoulders and a straight waist. Women are now made at the female end of the gender slider,
  and that end adds MakeHuman's CC0 measurement targets on top: narrower shoulders and shoulder caps, less V in the
  torso, a smaller waist and fuller hips, seat and bust. Checked in Blender side by side and in the game.
- **Clothes (phase 3).** MakeHuman's free clothes have no modest wear or sleepwear, so `tools/blender_mpfb_body.py`
  now makes our own from the body's surface: a long sleeved tunic with a high-low hem, wide trousers, a hijab with a
  smooth oval face opening that lies flat over the ears and drapes over the shoulders, and loose pyjamas. They carry
  every body slider, so they fit any body without a run time refit. With MakeHuman's casual set, shoes and two hair
  styles that makes the wardrobe in `Assets/Resources/Clothes` and three outfits: casual, modest and sleep.
- **Wardrobe.** New `Wardrobe` component: wears a whole outfit or single pieces while the game runs, binds each piece
  to the person's bones, hands it to `BodyShape` (new `AddSkin`) and hides only the skin under what is worn (the body
  is no longer cut; a second UV channel holds a cover bit per garment). A hijab also hides the hair. Garments get a
  double sided fabric with a woven detail map made in code (`CharacterLook.UpgradeWear`).
- **Clipping scan.** New menu **Dearlife > Scan characters** (Play mode): 3 outfits x 17 bodies x 6 poses = 306 cases,
  report in `Logs/character_scan.txt`. The first run passed 62. Each failure pointed at something real, which became a
  step of the tool: garments re-take the skin weights under them, inner layers are tucked inside outer ones and follow
  their weights, MakeHuman's clothes are lifted off and pushed out over visible skin, the hem curves up at the front
  clear of the hip crease, the neckline dips clear of the chin, the hijab has room under the chin, and stray shape key
  spikes are removed. The last run: **306 of 306 pass**, deepest point 4.9 mm (limit 8 mm). Body parts pressing
  together in a pose (arm on torso, belly on thighs) are listed as contact, not counted.
- **Cleanup:** the old per-person hair and clothes files (`mpfb_test_cut_long01`, `mpfb_test_female_casualsuit01`,
  `mpfb_test_shoes01`) are replaced by the wardrobe.
- **Not done yet:** tint masks so one garment comes in many colours, spring bones for loose drape, more garments
  (skirts, abaya, outerwear), and the character creator (phase 4) that uses all of this.

## Session 48: bathroom, TV, music corner and two pose fixes (2026-09-29) · v0.41.0

- **Lily pets the cat like James.** Her rig's spine bends the other way round (like James's, which was already
  handled), so in the crouch she leant back with her arms in the air. `CharacterRig.Set` now flips her spine and neck
  too; reading, talking and the rest look alike for both.
- **Reclining was upside down.** The lying pose bent the spine the wrong way for both of them, so they lay flat on a
  lounger's raised backrest and sank to the bottom of the bath. Fixed in `LieTargets`; checked on a lounger and in the
  bath.
- **Bath.** Taking a bath is now lying back in the tub (a new `UseSpot` in the tub, away from the tap, legs under the
  water) instead of standing beside it. `BathTub` fills the tub while it lasts: water rises under a heap of soft foam
  that covers the bather to the chest, a few soap bubbles float up and a little steam rises, then it all drains. Idle
  people never lie in the empty tub. Plays `bathtub.mp3`.
- **Shower.** The shower's hinged door never moved and people washed standing outside. `ShowerStall` turns it into a
  sliding door (along the outside of the fixed pane, hinges gone); a shower is now: the door slides open, the person
  steps in, the door closes, water pours from the rain head, steam fills the cube and soap bubbles float and slide down
  the body, then the water stops, the door opens and they step out. Plays `shower.mp3`. The effects are particles with
  materials made by **Dearlife > Make bathroom effects** (`Resources/Effects`); they render before refraction, or the
  refractive glass hid them.
- **Sink.** Washing up at the vanity plays `sink_water.mp3`.
- **TV.** The screen's texture coordinates were a box projection in metres, so only a sliver of the picture showed (a
  dot and a smear), and by day the screen looked black. `TvScreen` now stretches the picture over the screen's face and
  is much brighter by day (4000 nits, 45 at night).
- **Music corner.** A guitar on its stand and the synth keyboard now stand in the living room by the glass wall to the
  kitchen, in the house layout. New menu **Dearlife > Place missing house furniture** adds layout pieces the saved scene
  is short of without rebuilding the house (saves only list sold pieces, so old saves get them too).
- **Chats no longer interrupt what someone is doing.** Walking up to chat pulled the other person out of any activity
  without ending it; in the shower that left them stuck inside. Busy people are now left alone, and joining a chat
  ends an activity properly first.
- **Crash:** the editor ran out of graphics memory once (a D3D12 device error after many Play mode runs in one long
  session); restarting Unity cleared it. Nothing in the code.

## Session 49: character plan phase 4, the character creator (2026-09-29) · v0.42.0

- **New Game now opens "Create your household".** The people stand on the sunny deck in front of the living room in
  the game's own light; the camera looks from the garden with the house behind. A panel on the left has five tabs:
  **Body** (feminine to masculine, height, weight, muscle, proportions, age 18 to 60), **Face** (13 sliders), **Skin and
  hair** (a realistic skin tone range, long, short or no hair, six hair colours that brows and lashes follow),
  **Clothes** (T-shirt and jeans or tunic and trousers, an optional hijab, shoes, pyjamas, each piece in nine
  colours, with a sleepwear preview) and **About** (name, three traits, career). Drag beside the person to turn them
  round, **Face** zooms in, morning, noon, evening and night change the light. Up to four people; **Random** rolls a
  believable adult (names match the body), **Back** returns to the title screen.
- **Move in** saves the household in the slot (`PersonData`, `HouseholdData`, as JSON) and they take over the house:
  Lily and James step aside, the new people live, work and chat with their own traits and careers, and change into
  their pyjamas for bed. Load Game brings them back as made; older saves keep Lily and James. New Game wipes a created
  household and its careers.
- **Face sliders** come from MakeHuman's CC0 face targets, baked by `tools/blender_mpfb_body.py` like the body sliders,
  into every mesh and every garment, so the hijab and hair follow the face. The body now has 39 shape keys; the tool
  still runs in under two minutes.
- **People are made from `Resources/People/Person.prefab`** (**Dearlife > Make person prefab**), which also fixed a
  missing eye material (the eyes were plain white without it).
- Tested in Play mode on an empty slot: New Game, two people made, Move in, both living in the house, Load Game brought
  them back with their shapes, skin and careers.

## Session 50: character plan phase 5, a fresh default household (2026-09-29) · v0.43.0

- **Lily and James have moved out.** The default household is now **Aina** (a teacher: creative, cheerful, neat; tunic,
  wide trousers and hijab in sage, navy and rose) and **Danial** (an engineer: bookworm, foodie, handy; T-shirt and
  jeans), both made in the creator and kept as `Assets/Resources/People/DefaultHousehold.json`. Any save without its own
  created household (older saves too) gets them; New Game still opens the creator.
- **The old people are gone for good:** `lily.fbx`, `amir.fbx`, the glasses, their textures and materials, the
  procedural `person_*.fbx`, `tools/blender_rig_lily.py` and `tools/blender_symmetrize.py`, and the two scene objects.
  `tools/blender_characters.py` only makes the pets now; `SimData` no longer special cases the old names.
- **Hair under the hijab:** living people showed their hair through the hijab because the floor view switches every
  renderer back on. `Wardrobe` now switches the hair object off instead of its renderer.
- Tested in Play mode on an empty slot: Aina and Danial move in with their traits and careers and live in the house.

## Session 51: character plan phase 6, realistic pets with more to do (2026-09-29) · v0.44.0

- **Bedah and the dog are realistic now.** Bedah comes from "An Animated Cat" by Evil_Katz and the dog is a Shiba Inu
  from "Animated Dog Sits Rolls Over Shake Paw" by LasquetiSpice (both CC BY on Sketchfab, credited in
  `Characters/CREDITS.txt`, sources kept in `S:\Tools\pets`). `tools/blender_pets.py` flattens them (no parent
  empties, rest pose, scale 1), turns them to face -Y, sizes them (the cat 34 cm to the ear tips, the dog 55 cm), gives
  the bones clean names and exports them. The cat was a black cat: the tool repaints it as Bedah's calico by filling
  its UV layout with the 3D position of every texel and painting patches there (ragged orange and black patches on the
  back, a black tail and one black ear), keeping the fur strokes of the original. Its tufts and whiskers are pale cards
  on HDRP's hair shader; the dog keeps its own normal map.
- **`PetBody` poses them on their real skeletons**: turns in the pet's own axes added bone by bone from the body out,
  so the imported bone directions do not matter. A walk (feet down one after another) or a trot (diagonal pairs) with
  the paws lifting, standing with breathing, a looking head and twitching ears, sitting, lying like a sphinx, curled up
  asleep on one side, grooming (the cat licks a paw, the dog scratches an ear), eating, happy (the cat's tail up with a
  hooked tip and slow blinks, the dog wagging and panting) and a play bow. Every resting pose then settles onto the
  floor: the skin is baked once, its lowest point measured and the body raised or lowered to touch (kept per pose).
- **Pet corner**: a food and a water bowl on a silicone mat and a round pet bed (`tools/blender_petstuff.py`), in the
  house layout (the bed by the living room bookcase, the bowls in the kitchen corner) and in Buy mode under Pets.
- **More for the pets to do** (`Character.Pets.cs`): eat from the food bowl when hungry (a person feeding a pet now
  also fills the bowl, three servings), drink, nap in the pet bed (people leave a sleeping pet alone), keep a person
  company, a mad dash round the house, play together (cat and dog), sunbathe in the garden by day, sniff about, and
  ask for food with a meow or a woof. Pet status shows what they are doing (Sleeping, Grooming, Napping in the pet bed).
- The old stylised cat and dog (`cat.fbx`, `dog.fbx`, `tools/blender_characters.py`) and the jointed figure tables in
  `CharacterRig` are removed. The shop picture of the dog is the new Shiba.
- Tested in Play mode on an empty slot: Bedah walked to the bowl and ate, napped curled up in the bed, kept Aina
  company; the dog sniffed about, slept, scratched and played with Bedah.

## Session 52: creator details, relationships and sharing (2026-09-29) · v0.45.0

- **A Details tab in the creator**: eye colour (seven, from the natural brown to blue and grey), lipstick, blush and
  eyeshadow (a colour and an amount each), eyeliner with a small wing, freckles, facial hair (stubble, a goatee with a
  moustache, or a full beard, all in the hair colour) and three tattoos (a geometric band round the left forearm, a rose
  on the right shoulder, a small star inside the left wrist). Random people get some of these now and then.
- **How it is drawn**: `tools/blender_skin_layers.py` bakes masks on the MPFB2 body's UV layout by giving every texel its
  3D position and painting rules round landmarks (the eyes, the teeth, the arm bones), so lipstick follows the lips the
  skin texture already has and a beard stops under the jaw. At run time `PersonLook` blends the person's own colours
  onto their own copy of the skin texture on the graphics card (`Hidden/Dearlife/SkinLayers`), quick enough for every
  step of a slider. Found on the way: MakeHuman's inside of the mouth shares 3D positions with the lips, so the masks
  skip its deep red UV islands.
- **The scalp follows the hair colour.** The skin texture has short dark hair painted over the scalp, which showed as a
  dark band under blonde or grey hair; it is now masked and tinted with the person's hair colour.
- **Eye colour** recolours the iris texture into the person's own eye material, keeping its fibres.
- **Relationships**: on the About tab each person can be married to, partners with, a sibling or family of, a friend or
  just a housemate of each of the others. It sets how close they start and shows in their status. Aina and Danial are
  married. Partners chatting get "Time with my spouse" (or partner) instead of a plain nice chat.
- **Sharing**: Copy person or Copy household puts a short code on the clipboard (about 1 KB, the JSON zipped), Paste
  brings it into anyone's creator, and Save preset keeps people on this computer as a list to pick from.
- **Chats**: people used to always chat with the first person in the house; with more people they now pick whoever is
  free, the closest friend and nearest first, with some chance.

## Session 53: more clothes, and a full recheck of every interaction (2026-09-29) · v0.46.0

- **Seven new garments**, made from the body's surface by `tools/blender_mpfb_body.py` like the tunic: a fitted shirt
  and slim slacks (formal), a blazer with a V opening on a new `outer` layer (outerwear), a tank top with straps and
  shorts (sporty), and a one piece swimsuit and swim shorts (swimwear). The swimsuit's leg openings are slid onto a
  smooth leg line, like the hijab's face opening, so they do not follow the body's quads in steps.
- **In the creator** the Clothes tab offers four everyday looks (T-shirt and jeans, tunic and trousers, shirt and
  slacks, tank top and shorts), a blazer over the T-shirt, shirt or tank top, the hijab and shoes, and swimwear (a
  swimsuit, swim shorts or stay dressed) with a preview. People change into their swimwear for a swim and back after.
- **Layering only where pieces are worn together**: fitting every bottom inside every top squeezed the trousers out
  through the tunic; each bottom is now fitted only inside its own top, the blazer only over the tops it goes with,
  pyjamas only with pyjamas and swimwear with nothing.
- **Clipping scan**: 676 of 684 pass (7 outfits, 19 bodies; swimwear is checked standing, walking and swimming, the
  poses it is worn in). Left: the slacks show up to 13 mm through the shirt's front hem when crouching on some bodies,
  and the swimsuit's leg edge on the heaviest body is 1 mm over the limit.
- **Every interaction rechecked** with a new tool, **Dearlife > Check interactions**: one person is sent to every
  thing to do on every kind of item (68 in all, upstairs too), and the report notes whether they got there, the pose,
  where they ended up and whether it finished, with a picture of each. What it found and what changed:
  - Items on a counter, on a desk or on a wall (the coffee machine, the 3D printer, the chalkboard and whiteboard)
    had no place to stand: they are now used from the floor below them.
  - An item whose nearest side was cut off could not be used at all: every side is tried in turn now (the workbench),
    and the pool is reached from anywhere round its edge.
  - Meals and takeaway at the dining table or the kitchen island are eaten sitting on a chair or stool beside it, with
    a plate of food or a takeaway box on the table; desk work (work, freelance, plan a lesson, sketch) is done sitting
    at the desk, and office chairs swivel round to face it.
  - The toilet is sat on (it was used standing a metre in front of it).
  - The treadmill is walked on, on its belt; the spin bike is ridden (it was jumping jacks beside both).
  - "Sit by the fire" sat in mid air: it is now "Warm up by the fire", crouching; the laundry and filing crouch at the
    machine or drawer instead of typing in the air.
  - **The upstairs rooms could not be walked into.** People got up the stairs to the landing but no further: a potted
    plant in the middle of the tiny landing and a wardrobe half across the engineer's room door closed both doors, and
    in the engineer's room the robot arm, the beanbag, the bed and a plant walled off the middle of the room and the
    door to the gym. The plant on the landing is gone, the wardrobe, robot arm, beanbag and the other plant moved (in
    the house layout too); every item in the house is now reachable on foot from the ground floor (checked by path for
    all 70). The bathroom mirror no longer offers "Wash up" (the vanity under it does).
  - Watching TV from a seat that faces no TV stays offered as "(no TV)", as before.

## Session 54: polishing the clothes and beards (2026-09-29) · v0.47.0

- **Real beards.** A full beard and a goatee are now 3D hair instead of paint on the skin: six stacked shells over the
  beard area of the face, each a little further out and down, with short tapering strands in their alpha
  (`make_beard` in `tools/blender_mpfb_body.py`), on HDRP's hair shader and tinted with the person's hair colour. They
  follow every body and face slider like the clothes. Stubble stays painted, the painted beard shadow stays under the
  hair. New outfit "bearded" in `wardrobe.json` so the scan checks them.
- **A clean swimsuit leg line.** Sliding the edge vertices onto the line left folds; the swimsuit now reaches a little
  lower, faces wholly below the leg line are removed, the corners left below it are lifted to 3 mm under it, and the
  openings are cut by an alpha mask painted from its own surface (the body's UV layout overlaps itself round the
  crotch, so a mask painted from the body was ambiguous there). The edge is exact to a millimetre.
- **Layering fixes found on the way:** the fit of trouser hems round the shoes ran last and its smoothing undid tucks
  made earlier (a pyjama waist and the slacks showed through their tops); the hems are now fitted first, a vertex put
  back out is pulled in again, and a final sweep re-checks every pair. A gusset under the crotch was tried for the
  swimsuit and dropped: following the tight crease between the legs clipped more than the original line.
- **Shirt and slacks:** a high-low shirt hem clear of the hip crease, a little more room over the chest, and the slacks
  bridge the top of the seat like the shirt over them; the slacks no longer show through the shirt.
- **Clipping scan: 793 of 798 pass (8 outfits, 19 bodies).** It now skips fabric an alpha mask cuts away (it cannot be seen) and treats beards
  like hair (strands part against the skin, they do not clip like fabric). Left: the one piece swimsuit worn by
  masculine or older bodies, 0.2 to 5.8 mm over the limit at the crotch (random people only get it on women).
- Asset library keys (Sketchfab, Poly Pizza) are kept as gitignored text files in the project root.

## Session 55: walking, sitting, petting, the hijab and chairs at tables (2026-09-29) · v0.48.0

- **A calmer walk.** The old recording (CMU 02_01) bobbed the hips and planted the feet wide apart, which looked
  bouncy and straddled, worst on women. Measurements in Play mode (`Assets/Editor/WalkProbe.cs`:
  hips bob and sideways foot spacing, Logs/walk_probe.txt) picked two calm strides: 105_29 for men and 105_34 (a
  light, narrow walk) for women, blended by the body's gender through a new `feminine` parameter. On top, walk IK
  halves the hips' rise and fall round their running average and draws each foot in towards the line under the body
  (more for a feminine body). Aina: hips bob 4.5 to 2.9 cm, feet 9.6 to 3.5 cm apart. People walk a little slower
  (1.1 m/s).
- **Petting without bobbing.** The crouch looped the whole pick up (down and back up), so petting a cat bent over
  again and again. It now holds the still moment at the bottom (hands 33 cm off the floor).
- **Sitting at the dining table.** Each foot was put under the previous frame's knee, which fed back on itself: the
  legs slowly swung round and one splayed out sideways, under or through the table. Now the knee goes straight ahead of
  its hip joint at the thigh's length (with a knee hint) and the foot under it; legs are symmetric and the knees sit
  under the table with room to spare. Bar stools and sofas use the same placement.
- **Chairs snap to tables** in decorate mode, like The Sims 4 (`Assets/Scripts/House/SeatSnap.cs`): a dining chair
  near a dining table jumps to the nearest free place round it (spread evenly along each side, one at each end) and
  turns to face it; bar stools at the island and the barbecue counter, office chairs at desks. Ctrl places freely.
- **A hijab that drapes.** The drape followed the bust. It now falls like a curtain: round the body's upright axis,
  below the chin the fabric is never closer in than anywhere above it, worked on a blurred grid so it is smooth
  (`hang`), with soft pleats and a smooth throat (the step in the throat's room made ridges). The drape reaches lower
  in the middle, on a curved hem, and sweeps up at the sides clear of the arms (the tunic showed through there). Round
  the chin and throat the fabric is pushed out of the real body surface in the last tension passes: the drape pulled
  the fabric under a short body's chin into it.
- **Fitting follow ups:** a little more room in the shirt over the chest (bending over to pet pushes the chest
  forward) and over the top of the seat, and slacks tucked 4 cm inside a shirt instead of 3.
- **A rounder bust.** Feminine bodies get less pointed, slightly lifted breasts (`breast-point-decr`,
  `breast-volume-vert-up`), round under every top.
- **Clipping scan: 792 of 798 pass.** Left: the one piece swimsuit on masculine or older bodies (as before) and one
  shirt case in the deep crouch on the strongest body (8 mm).

