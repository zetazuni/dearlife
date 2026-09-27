using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tiramisu.EditorTools
{
    /// <summary>
    /// Builds the greybox house from the 2D game's floor plan (1 tile = 1 m, garden toward +Z).
    /// Menu: Tiramisu > Build greybox house. It rebuilds Assets/Scenes/Main.unity from scratch,
    /// so tweak the numbers here and run it again rather than editing the scene by hand.
    /// </summary>
    public static class GreyboxBuilder
    {
        const float WX = 30f, WD = 8f, H = 3f, SLAB = 0.3f, UPY = 3.3f;
        const float OUT = 0.28f, PART = 0.14f, GLASS = 0.08f, DOOR_H = 2.3f;
        const string MatDir = "Assets/Art/Materials/Architecture";
        const float FLOOR_TOP = 0.02f; // room floor plates are 2 cm thick, furniture stands on top

        static Material oak, oakDark, wallWhite, cap, slab, tile, bathTile, garageFloor, gym, lawn, lawnDark,
            deck, water, poolTile, stone, glass, steel, roof, trunk, leaf, mailbox, wallBrown, lampGlow, poolGlow, poolMarble, dropMat, bubbleMat, ledWarm, ledCool, bulbGlow, doorWood, asphalt, apron, shutter, shutterDark, lineWhite, lineYellow, curtainFabric,
            pLiving, pKitchen, pHall, pBath, pGarage, pTeacher, pOffice, pLanding, pEngineer, pGym;
        static Material sideMinus, sidePlus;   // room paints for the two faces of an interior wall, set just before building it

        // ---------- render pipeline ----------

        [MenuItem("Tiramisu/Set up render pipeline")]
        public static void SetupPipeline()
        {
            CinematicSetup.Pipeline();
            PhysicsSetup.ProjectSettings();
            Debug.Log("Tiramisu: HDRP is set up with the film look settings, DirectX 12 and linear colour.");
        }

        // ---------- materials ----------

        static Material Tx(string name, string tex, MaterialLibrary.Mapping map, Vector2 smooth, float normal = 1f, float metal = 0f, Color? tint = null, float scale = 1f)
            => MaterialLibrary.Textured($"{MatDir}/{name}.mat", tex, map, tint ?? Color.white, smooth, metal, normal, scale);

        /// <summary>Neutral (greyscale detail) texture with the colour set here.</summary>
        static Material Tn(string name, string tex, MaterialLibrary.Mapping map, Color tint, Vector2 smooth, float normal = 1f, float metal = 0f, float scale = 1f)
            => MaterialLibrary.Textured($"{MatDir}/{name}.mat", tex, map, tint, smooth, metal, normal, scale, true);

        static Material Pl(string name, Color c, float smooth, float metal = 0f)
            => MaterialLibrary.Plain($"{MatDir}/{name}.mat", c, smooth, metal);

        static Material Paint(string name, Color c) => Tn(name, "white_plaster_02", MaterialLibrary.Mapping.Triplanar, c, new Vector2(0.02f, 0.22f), 0.3f);

        static Material Glowing(string name, Color c, Color emit)
        {
            var m = Pl(name, c, 0.6f);
            m.SetFloat("_UseEmissiveIntensity", 0f);
            m.SetColor("_EmissiveColor", emit);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void MakeMaterials()
        {
            var P = MaterialLibrary.Mapping.Planar;
            var T = MaterialLibrary.Mapping.Triplanar;
            oak = Tx("Oak", "herringbone_parquet", P, new Vector2(0.2f, 0.48f));
            oakDark = Tx("OakDark", "dark_wooden_planks", P, new Vector2(0.2f, 0.45f));
            wallWhite = Tn("WallWhite", "white_plaster_02", T, new Color(0.95f, 0.94f, 0.92f), new Vector2(0.02f, 0.22f), 0.3f);
            cap = Pl("DarkCap", new Color(0.05f, 0.05f, 0.055f), 0.5f);
            slab = Tn("Slab", "brushed_concrete", T, new Color(0.34f, 0.24f, 0.17f), new Vector2(0.1f, 0.4f));   // the edges of the floors: dark brown
            wallBrown = Tn("WallPastelBrown", "white_plaster_02", T, new Color(0.83f, 0.70f, 0.60f), new Vector2(0.02f, 0.22f), 0.3f);
            tile = Tn("KitchenTile", "marble_01", P, new Color(0.96f, 0.95f, 0.93f), new Vector2(0.55f, 0.85f), 0.6f);   // large format polished marble, soft warm white
            bathTile = Tx("BathTile", "large_grey_tiles", P, new Vector2(0.3f, 0.6f));
            garageFloor = Tn("GarageFloor", "concrete_floor", P, new Color(0.62f, 0.62f, 0.63f), new Vector2(0.2f, 0.5f));
            gym = Tn("GymFloor", "rubber_tiles", P, new Color(0.85f, 0.87f, 0.92f), new Vector2(0.05f, 0.32f));
            lawn = Tn("Lawn", "leafy_grass", P, new Color(0.40f, 0.58f, 0.24f), new Vector2(0f, 0.3f));
            lawnDark = Tn("LawnEdge", "leafy_grass", P, new Color(0.33f, 0.48f, 0.21f), new Vector2(0f, 0.25f));
            deck = Tn("Deck", "wood_floor_deck", P, new Color(0.64f, 0.47f, 0.33f), new Vector2(0.15f, 0.5f));
            water = MaterialLibrary.Water($"{MatDir}/PoolWater.mat");
            poolTile = Tn("PoolTile", "blue_floor_tiles_01", T, new Color(0.55f, 0.80f, 0.88f), new Vector2(0.5f, 0.9f));
            stone = Tn("Stone", "precast_stone_paving", T, new Color(0.80f, 0.78f, 0.74f), new Vector2(0.05f, 0.4f));
            glass = MaterialLibrary.Glass($"{MatDir}/Glass.mat", new Color(0.88f, 0.93f, 0.95f, 0.06f));
            steel = Pl("BlackSteel", new Color(0.04f, 0.04f, 0.045f), 0.78f, 1f);
            roof = Tn("Roof", "box_profile_metal_sheet", T, new Color(0.30f, 0.32f, 0.35f), new Vector2(0.35f, 0.7f), 1f, 1f);
            trunk = Tx("Trunk", "bark_brown_02", T, new Vector2(0f, 0.3f));
            leaf = Pl("Leaves", new Color(0.25f, 0.42f, 0.2f), 0.3f);
            mailbox = Pl("Mailbox", new Color(0.04f, 0.04f, 0.045f), 0.6f, 0.8f);
            asphalt = Tn("Asphalt", "concrete_floor", P, new Color(0.16f, 0.16f, 0.17f), new Vector2(0.1f, 0.3f));
            apron = Tn("DrivewayApron", "precast_stone_paving", P, new Color(0.62f, 0.62f, 0.62f), new Vector2(0.1f, 0.4f));
            shutter = Pl("GarageShutter", new Color(0.62f, 0.64f, 0.67f), 0.45f, 0.6f);
            shutterDark = Pl("GarageShutterDark", new Color(0.04f, 0.045f, 0.05f), 0.5f, 0.3f);
            lineWhite = Pl("RoadWhite", new Color(0.9f, 0.9f, 0.88f), 0.3f);
            lineYellow = Pl("RoadYellow", new Color(0.9f, 0.72f, 0.15f), 0.3f);
            curtainFabric = Tn("CurtainFabric", "rough_linen", T, new Color(0.97f, 0.94f, 0.88f), new Vector2(0f, 0.2f));
            pLiving = Paint("PaintLiving", new Color(0.90f, 0.83f, 0.72f));      // warm sand
            pKitchen = Paint("PaintKitchen", new Color(0.70f, 0.80f, 0.72f));    // sage green
            pHall = Paint("PaintHall", new Color(0.94f, 0.93f, 0.90f));          // soft white
            pBath = Paint("PaintBath", new Color(0.69f, 0.84f, 0.87f));          // aqua
            pGarage = Paint("PaintGarage", new Color(0.66f, 0.68f, 0.72f));      // cool grey
            pTeacher = Paint("PaintTeacher", new Color(0.93f, 0.78f, 0.79f));    // blush
            pOffice = Paint("PaintOffice", new Color(0.52f, 0.64f, 0.73f));      // blue grey
            pLanding = Paint("PaintLanding", new Color(0.95f, 0.94f, 0.92f));    // white
            pEngineer = Paint("PaintEngineer", new Color(0.48f, 0.53f, 0.59f));  // slate
            pGym = Paint("PaintGym", new Color(0.90f, 0.64f, 0.53f));            // coral
            doorWood = Tn("DoorWood", "american_walnut_veneer", T, new Color(0.62f, 0.42f, 0.27f), new Vector2(0.3f, 0.5f), 1f);
            lampGlow = Glowing("LampGlow", new Color(1f, 0.82f, 0.55f), new Color(1f, 0.7f, 0.35f) * 2.2f);
            ledWarm = Glowing("LedWarm", new Color(1f, 0.8f, 0.55f), new Color(1f, 0.72f, 0.4f) * 5f);
            ledCool = Glowing("LedCool", new Color(0.7f, 0.9f, 1f), new Color(0.55f, 0.82f, 1f) * 5f);
            bulbGlow = Glowing("StringBulb", new Color(1f, 0.9f, 0.7f), new Color(1f, 0.75f, 0.4f) * 6f);
            poolMarble = Tn("PoolMarble", "grey_cartago_03", T, new Color(0.55f, 0.55f, 0.6f), new Vector2(0.78f, 0.95f), 1f);   // near black marble with white veins
            dropMat = MaterialLibrary.Glass($"{MatDir}/WaterDrop.mat", new Color(0.85f, 0.95f, 1f, 0.45f));
            bubbleMat = MaterialLibrary.Glass($"{MatDir}/Bubble.mat", new Color(1f, 1f, 1f, 0.55f));
            poolGlow = Glowing("PoolGlow", new Color(0.5f, 0.9f, 1f), new Color(0.3f, 0.85f, 1f) * 2.5f);
        }

        // ---------- geometry helpers ----------

        /// <summary>A box from min corner to max corner in world metres.</summary>
        static GameObject Box(string name, Transform parent, Vector3 min, Vector3 max, Material m, bool shadows = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.position = (min + max) * 0.5f;
            g.transform.localScale = max - min;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        static Transform Group(string name, Transform parent)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g.transform;
        }

        /// <summary>
        /// A wall line with optional door gaps. axis X means the wall sits at x = at and runs along Z.
        /// Door gaps are [from, to] ranges along the run; each gets a lintel above DOOR_H.
        /// </summary>
        static void Wall(Transform parent, string name, WallCutaway.Axis axis, float at, float thick,
            float from, float to, float floorY, Material m, bool isGlass, params Vector2[] doors)
        {
            var line = Group(name, parent);
            var cut = line.gameObject.AddComponent<WallCutaway>();
            cut.axis = axis;
            cut.plane = at + thick * 0.5f;
            cut.floor = floorY > 0.1f ? 1 : 0;
            cut.floorY = floorY;
            cut.fullHeight = H;

            float cursor = from;
            int i = 0;
            System.Array.Sort(doors, (a, b) => a.x.CompareTo(b.x));
            foreach (var d in doors)
            {
                if (d.x > cursor) Segment(line, $"{name} {i++}", axis, at, thick, cursor, d.x, floorY, floorY + H, m, isGlass);
                Segment(line, $"{name} lintel {i++}", axis, at, thick, d.x, d.y, floorY + DOOR_H, floorY + H, m, isGlass);
                cursor = d.y;
            }
            if (to > cursor) Segment(line, $"{name} {i}", axis, at, thick, cursor, to, floorY, floorY + H, m, isGlass);
        }

        static WindowWall.Zone Zone(float from, float to, Material mat) => new WindowWall.Zone { from = from, to = to, mat = mat };

        static void Paint2(Material minus, Material plus) { sideMinus = minus; sidePlus = plus; }

        static WindowWall.Win Win(float center, float width, float sill, float top)
            => new WindowWall.Win { center = center, width = width, sill = sill, top = top, homeCenter = center, homeWidth = width };

        /// <summary>A concrete wall line whose windows can be moved and resized in decorate mode (see WindowWall).</summary>
        static void WindowedWall(Transform parent, string name, WallCutaway.Axis axis, float at, float thick,
            float from, float to, float floorY, Material m, WindowWall.Zone[] zones, params WindowWall.Win[] wins)
        {
            var line = Group(name, parent);
            var cut = line.gameObject.AddComponent<WallCutaway>();
            cut.axis = axis;
            cut.plane = at + thick * 0.5f;
            cut.floor = floorY > 0.1f ? 1 : 0;
            cut.floorY = floorY;
            cut.fullHeight = H;
            var ww = line.gameObject.AddComponent<WindowWall>();
            ww.axis = axis; ww.at = at; ww.thick = thick; ww.from = from; ww.to = to; ww.floorY = floorY;
            ww.wallHeight = H; ww.doorHeight = DOOR_H; ww.interiorSide = 1;
            ww.wallMat = m; ww.glassMat = glass; ww.frameMat = steel; ww.sillMat = oak;
            ww.curtainMat = curtainFabric;
            ww.zones.AddRange(zones);
            ww.windows.AddRange(wins);
            ww.Rebuild();
        }

        // ---------- sliding doors between the rooms ----------

        /// <summary>
        /// A modern sliding door hung on a black rail in front of the wall. Glass walls get a framed glass door,
        /// concrete walls a walnut door with fine grooves. Opens by hover and for anyone with a DoorOpener.
        /// </summary>
        static void SlidingDoorAt(Transform parent, string wallPath, string label, WallCutaway.Axis axis, float at, float thick,
            float a, float b, float floorY, bool glassDoor, int side, int slideDir)
        {
            var root = Group($"Sliding door: {label}", parent);
            var panel = Group("Panel", root);
            float t = glassDoor ? 0.03f : 0.045f;
            float d0 = side > 0 ? at + thick + 0.025f : at - 0.025f - t;
            float d1 = d0 + t;
            float front = side > 0 ? d1 : d0;          // the face people touch
            float w0 = a - 0.07f, w1 = b + 0.07f, h = DOOR_H + 0.03f;
            Vector3 P(float along, float y, float depth) => axis == WallCutaway.Axis.X ? new Vector3(depth, floorY + y, along) : new Vector3(along, floorY + y, depth);
            GameObject B(string nm, Transform par, float u0, float y0, float u1, float y1, float e0, float e1, Material mat, bool shadows = true)
            {
                var g = Box(nm, par, P(u0, y0, e0), P(u1, y1, e1), mat, shadows);
                return g;
            }

            float fd = side > 0 ? d1 : d0 - 0.02f, fe = side > 0 ? d1 + 0.02f : d0;   // handle depth range
            if (glassDoor)
            {
                const float f = 0.04f;
                B("Glass", panel, w0, 0.02f, w1, h, d0 + 0.008f, d1 - 0.008f, glass, false);
                B("Frame bottom", panel, w0, 0.02f, w1, 0.02f + f, d0, d1, steel);
                B("Frame top", panel, w0, h - f, w1, h, d0, d1, steel);
                B("Frame left", panel, w0, 0.02f, w0 + f, h, d0, d1, steel);
                B("Frame right", panel, w1 - f, 0.02f, w1, h, d0, d1, steel);
            }
            else
            {
                B("Slab", panel, w0, 0.02f, w1, h, d0, d1, doorWood);
                int grooves = Mathf.Max(3, Mathf.RoundToInt((w1 - w0) / 0.2f));
                for (int i = 1; i < grooves; i++)
                {
                    float u = w0 + (w1 - w0) * i / grooves;
                    B($"Groove {i}", panel, u - 0.004f, 0.1f, u + 0.004f, h - 0.1f, side > 0 ? d1 - 0.002f : d0 - 0.001f, side > 0 ? d1 + 0.001f : d0 + 0.002f, steel, false);
                }
            }
            // long black bar handle on the leading edge
            float hx = slideDir < 0 ? w1 - 0.16f : w0 + 0.16f;
            B("Handle", panel, hx - 0.015f, 0.75f, hx + 0.015f, 1.45f, fd + (side > 0 ? 0.0f : 0.0f), fe, steel);
            B("Handle stand a", panel, hx - 0.01f, 0.8f, hx + 0.01f, 0.83f, front - (side > 0 ? 0f : 0.03f), front + (side > 0 ? 0.03f : 0f), steel);
            B("Handle stand b", panel, hx - 0.01f, 1.37f, hx + 0.01f, 1.4f, front - (side > 0 ? 0f : 0.03f), front + (side > 0 ? 0.03f : 0f), steel);

            // the rail above, long enough for the door to slide open, with two hangers
            float dist = (b - a) + 0.14f;
            float r0 = Mathf.Min(w0, w0 + slideDir * dist) - 0.05f, r1 = Mathf.Max(w1, w1 + slideDir * dist) + 0.05f;
            float re0 = side > 0 ? d1 + 0.005f : d0 - 0.055f, re1 = re0 + 0.05f;
            B("Rail", root, r0, h + 0.03f, r1, h + 0.09f, re0, re1, steel);
            foreach (float u in new[] { w0 + 0.2f, w1 - 0.2f })
                B("Hanger", panel, u - 0.02f, h - 0.01f, u + 0.02f, h + 0.05f, Mathf.Min(d0, re0), Mathf.Max(d1, re1), steel);

            var door = root.gameObject.AddComponent<SlidingDoor>();
            door.panel = panel;
            door.slide = axis == WallCutaway.Axis.X ? new Vector3(0f, 0f, slideDir * dist) : new Vector3(slideDir * dist, 0f, 0f);
            float mid = at + thick * 0.5f;
            door.sensorCenter = P((a + b) * 0.5f, 1.1f, mid);
            door.sensorSize = axis == WallCutaway.Axis.X ? new Vector3(2.6f, 2.2f, (b - a) + 0.8f) : new Vector3((b - a) + 0.8f, 2.2f, 2.6f);
            AttachTo(wallPath, root.gameObject);   // hides with its wall when the wall is cut down
        }

        /// <summary>A pair of modern walnut doors on hinges in a slim black frame. Swings out to the garden.</summary>
        static void HingedDoubleDoor(Transform parent, string wallPath, string label, float a, float b)
        {
            var root = Group($"Hinged door: {label}", parent);
            float zc = WD + GLASS * 0.5f;
            const float f = 0.05f;
            Box("Frame left", root, new Vector3(a - f, 0.02f, zc - 0.05f), new Vector3(a, DOOR_H + f, zc + 0.05f), steel);
            Box("Frame right", root, new Vector3(b, 0.02f, zc - 0.05f), new Vector3(b + f, DOOR_H + f, zc + 0.05f), steel);
            Box("Frame head", root, new Vector3(a - f, DOOR_H, zc - 0.05f), new Vector3(b + f, DOOR_H + f, zc + 0.05f), steel);
            Box("Threshold", root, new Vector3(a, 0f, zc - 0.07f), new Vector3(b, 0.02f, zc + 0.07f), steel);
            float half = (b - a) * 0.5f;
            var leaves = new Transform[2];
            var angles = new float[2];
            for (int i = 0; i < 2; i++)
            {
                bool leftLeaf = i == 0;
                var hinge = Group(leftLeaf ? "Leaf left" : "Leaf right", root);
                float hx = leftLeaf ? a : b, dir = leftLeaf ? 1f : -1f, free = hx + dir * (half - 0.01f);
                float lo = Mathf.Min(hx, free), hi = Mathf.Max(hx, free);
                hinge.position = new Vector3(hx, 0f, zc);
                Box("Slab", hinge, new Vector3(lo, 0.02f, zc - 0.03f), new Vector3(hi, DOOR_H - 0.01f, zc + 0.03f), doorWood);
                for (int g = 1; g < 5; g++)
                {
                    float x = lo + (hi - lo) * g / 5f;
                    Box($"Groove {g}", hinge, new Vector3(x - 0.004f, 0.1f, zc + 0.029f), new Vector3(x + 0.004f, DOOR_H - 0.1f, zc + 0.032f), steel, false);
                }
                float hxp = free - dir * 0.1f;
                Box("Handle out", hinge, new Vector3(hxp - 0.015f, 0.8f, zc + 0.03f), new Vector3(hxp + 0.015f, 1.7f, zc + 0.065f), steel);
                Box("Handle in", hinge, new Vector3(hxp - 0.015f, 0.8f, zc - 0.065f), new Vector3(hxp + 0.015f, 1.7f, zc - 0.03f), steel);
                leaves[i] = hinge;
                angles[i] = leftLeaf ? -100f : 100f;   // both swing out towards the garden
            }
            var door = root.gameObject.AddComponent<HingedDoor>();
            door.leaves = leaves;
            door.angles = angles;
            door.openSeconds = 1.0f;
            door.sensorCenter = new Vector3((a + b) * 0.5f, 1.1f, zc + 0.2f);
            door.sensorSize = new Vector3((b - a) + 1.0f, 2.2f, 3.4f);
            AttachTo(wallPath, root.gameObject);
        }

        /// <summary>A roller shutter garage door in the end wall: slats that roll up into a housing above the opening.</summary>
        static void GarageShutter(Transform parent, string wallPath, float a, float b)
        {
            var root = Group("Garage shutter door", parent);
            float xd = WX + GLASS + 0.03f, t = 0.04f, h = DOOR_H;
            var panel = Group("Panel", root);
            panel.position = new Vector3(xd, h, 0f);   // the top edge, the slats hang below it and squash towards it when it opens
            Box("Backing", panel, new Vector3(xd - 0.012f, 0.02f, a - 0.02f), new Vector3(xd, h, b + 0.02f), shutterDark, false);
            const int n = 11;
            float sh = (h - 0.02f) / n;
            for (int i = 0; i < n; i++)
            {
                float top = h - i * sh;
                Box($"Slat {i + 1}", panel, new Vector3(xd, top - sh + 0.004f, a - 0.02f), new Vector3(xd + t, top - 0.004f, b + 0.02f), (i == 2 || i == 3) ? shutterDark : shutter);
            }
            Box("Housing", root, new Vector3(xd - 0.03f, h - 0.02f, a - 0.12f), new Vector3(xd + 0.3f, h + 0.34f, b + 0.12f), steel);
            Box("Guide rail a", root, new Vector3(xd - 0.03f, 0.02f, a - 0.08f), new Vector3(xd + 0.08f, h, a - 0.02f), steel);
            Box("Guide rail b", root, new Vector3(xd - 0.03f, 0.02f, b + 0.02f), new Vector3(xd + 0.08f, h, b + 0.08f), steel);
            var door = root.gameObject.AddComponent<ShutterDoor>();
            door.panel = panel;
            door.openScale = 0.06f;
            door.openSeconds = 1.6f;
            door.stayOpenSeconds = 2.5f;
            door.sensorCenter = new Vector3(xd + 0.3f, 1.1f, (a + b) * 0.5f);
            door.sensorSize = new Vector3(3.6f, 2.2f, (b - a) + 0.8f);
            AttachTo(wallPath, root.gameObject);
        }

        /// <summary>The road along the garage side, with a driveway apron and a ramp down from the garage floor.</summary>
        static void Road(Transform g, float gy)
        {
            var road = Group("Road", g);
            float rx0 = 37.4f, rx1 = 43.8f, top = gy + 0.02f, ax = 36.4f;
            Box("Driveway apron", road, new Vector3(WX + GLASS, gy, 1.6f), new Vector3(ax, -0.03f, 6.4f), apron);
            // public sidewalk between the hedges and the road
            Box("Sidewalk", road, new Vector3(35.5f, gy, -30f), new Vector3(rx0 - 0.18f, gy + 0.1f, 50f), stone);
            // ramp from the apron down to the road
            float dx = rx0 - ax, dy = top - (-0.03f), len = Mathf.Sqrt(dx * dx + dy * dy);
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Driveway ramp";
            ramp.transform.SetParent(road, false);
            ramp.transform.position = new Vector3(ax + dx * 0.5f, -0.03f + dy * 0.5f - 0.05f, 4f);
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            ramp.transform.localScale = new Vector3(len, 0.1f, 4.8f);
            ramp.GetComponent<Renderer>().sharedMaterial = apron;
            Box("Road", road, new Vector3(rx0, gy, -30f), new Vector3(rx1, top, 50f), asphalt);
            Box("Kerb near", road, new Vector3(rx0 - 0.18f, gy, -30f), new Vector3(rx0, gy + 0.16f, 50f), stone);
            Box("Kerb far", road, new Vector3(rx1, gy, -30f), new Vector3(rx1 + 0.18f, gy + 0.16f, 50f), stone);
            float cx = (rx0 + rx1) * 0.5f;
            for (float z = -28f; z < 48f; z += 4f)
                Box("Centre line", road, new Vector3(cx - 0.07f, top, z), new Vector3(cx + 0.07f, top + 0.005f, z + 2f), lineWhite, false);
            Box("Edge line near", road, new Vector3(rx0 + 0.3f, top, -30f), new Vector3(rx0 + 0.4f, top + 0.005f, 50f), lineWhite, false);
            Box("Edge line far", road, new Vector3(rx1 - 0.4f, top, -30f), new Vector3(rx1 - 0.3f, top + 0.005f, 50f), lineWhite, false);
        }

        static void BuildDoors(Transform house, Transform upper)
        {
            var g0 = Group("Doors ground floor", house);
            const string gw = "House/Ground floor/Walls/";
            // ordinary modern wooden doors in the garden facade, and the garage roller shutter to the road
            HingedDoubleDoor(g0, gw + "Front glass", "living room to the garden", 3f, 5f);
            HingedDoubleDoor(g0, gw + "Front glass", "kitchen to the garden", 10f, 12f);
            HingedDoubleDoor(g0, gw + "Front glass", "bathroom to the garden", 18f, 20f);
            GarageShutter(g0, gw + "Garage end glass", 2f, 6f);
            GarageFoldingDoor(g0, gw + "Front glass", 24f, 28f);
            SlidingDoorAt(g0, gw + "Living and kitchen glass", "living room and kitchen", WallCutaway.Axis.X, 8f - PART / 2, PART, 3f, 5f, 0f, true, 1, -1);
            SlidingDoorAt(g0, gw + "Kitchen and hall wall", "kitchen and stair hall", WallCutaway.Axis.X, 14f - PART / 2, PART, 6.2f, 7.8f, 0f, false, -1, -1);
            SlidingDoorAt(g0, gw + "Hall and bathroom wall", "stair hall and bathroom", WallCutaway.Axis.X, 16f - PART / 2, PART, 6.2f, 7.8f, 0f, false, 1, -1);
            SlidingDoorAt(g0, gw + "Bathroom and garage glass", "bathroom and garage", WallCutaway.Axis.X, 22f - PART / 2, PART, 3f, 5f, 0f, true, 1, -1);
            var g1 = Group("Doors upper floor", upper);   // inside the upper floor, so they hide with it
            const string uw = "House/Upper floor/Walls/";
            SlidingDoorAt(g1, uw + "Teacher and office wall", "teacher's room and office", WallCutaway.Axis.X, 8f - PART / 2, PART, 3f, 5f, UPY, false, 1, -1);
            SlidingDoorAt(g1, uw + "Office and landing wall", "office and landing", WallCutaway.Axis.X, 14f - PART / 2, PART, 0.4f, 1.8f, UPY, false, 1, 1);
            SlidingDoorAt(g1, uw + "Landing and engineer wall", "landing and engineer's room", WallCutaway.Axis.X, 16f - PART / 2, PART, 0.4f, 1.8f, UPY, false, 1, 1);
            SlidingDoorAt(g1, uw + "Engineer and gym glass", "engineer's room and gym", WallCutaway.Axis.X, 22f - PART / 2, PART, 3f, 5f, UPY, true, 1, -1);
        }

        static void Segment(Transform line, string name, WallCutaway.Axis axis, float at, float thick,
            float a, float b, float y0, float y1, Material m, bool isGlass)
        {
            Vector3 min = axis == WallCutaway.Axis.X ? new Vector3(at, y0, a) : new Vector3(a, y0, at);
            Vector3 max = axis == WallCutaway.Axis.X ? new Vector3(at + thick, y1, b) : new Vector3(b, y1, at + thick);
            if (!isGlass && sideMinus && sidePlus)
            {
                // an interior wall: each face in the paint of the room it looks into
                float mid = at + thick * 0.5f;
                Vector3 maxA = axis == WallCutaway.Axis.X ? new Vector3(mid, y1, b) : new Vector3(b, y1, mid);
                Vector3 minB = axis == WallCutaway.Axis.X ? new Vector3(mid, y0, a) : new Vector3(a, y0, mid);
                Box(name + " a", line, min, maxA, sideMinus, true);
                Box(name + " b", line, minB, max, sidePlus, true);
            }
            else Box(name, line, min, max, m, !isGlass);
            if (isGlass) Frames(line, name, axis, at, thick, a, b, y0, y1);
        }

        /// <summary>Slim black steel mullions (at most 1.5 m apart) and top and bottom rails on a glass panel.</summary>
        static void Frames(Transform line, string name, WallCutaway.Axis axis, float at, float thick, float a, float b, float y0, float y1)
        {
            const float w = 0.05f;
            float d0 = at - 0.015f, d1 = at + thick + 0.015f;
            Vector3 P(float along, float y, float depth) => axis == WallCutaway.Axis.X ? new Vector3(depth, y, along) : new Vector3(along, y, depth);
            int n = Mathf.Max(1, Mathf.CeilToInt((b - a) / 1.5f));
            for (int i = 0; i <= n; i++)
            {
                float u = Mathf.Clamp(a + (b - a) * i / n, a + w * 0.5f, b - w * 0.5f);
                Box($"{name} mullion {i}", line, P(u - w * 0.5f, y0, d0), P(u + w * 0.5f, y1, d1), steel);
            }
            Box($"{name} bottom rail", line, P(a, y0, d0), P(b, y0 + w, d1), steel);
            Box($"{name} top rail", line, P(a, y1 - w, d0), P(b, y1, d1), steel);
        }

        static void Room(Transform parent, string name, int floor, float order, float x0, float x1, float z0, float z1, float y, Material m)
        {
            if (m) Box($"{name} floor", parent, new Vector3(x0, y, z0), new Vector3(x1, y + 0.02f, z1), m);
            var mark = new GameObject($"Room: {name}");
            mark.transform.SetParent(parent, false);
            mark.transform.position = new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f);
            var rm = mark.AddComponent<RoomMarker>();
            rm.displayName = name;
            rm.floor = floor;
            rm.order = Mathf.RoundToInt(order);
            rm.size = new Vector2(x1 - x0, z1 - z0);

            if (m) // real rooms get a warm ceiling light and a reflection probe
            {
                bool crisp = name == "Garage" || name == "Gym" || name == "Bathroom" || name == "Office & Library";
                float boost = name == "Gym" ? 3.6f : name == "Garage" ? 1.3f : 1f;   // the gym floor and paint are darker, give it more light
                CinematicSetup.RoomLightAndProbe(parent, name, mark.transform.position, rm.size, H, crisp ? 5200f : 4300f, true, boost);
            }
        }

        // ---------- the house ----------

        [MenuItem("Tiramisu/Build greybox house")]
        public static void Build()
        {
            SetupPipeline();
            FurnitureImport.ImportAll();
            MakeMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var house = Group("House", null);

            BuildGround(Group("Ground floor", house));
            var upper = Group("Upper floor", house);
            BuildUpper(upper);
            var roofGroup = Group("Roof", house);
            BuildRoof(roofGroup);
            roofRoot = roofGroup; upperRoot = upper;
            BuildGarden(Group("Garden", null));
            CityBuilder.Build(asphalt, stone, lineWhite, lawnDark);
            BuildLots(Group("Lots", null));
            BuildDoors(house, upper);
            BuildBeams(house, upper);
            BuildWallLights(house, upper);
            Furnish(Group("Furniture", house), upper);
            var hv = BuildRig(upper.gameObject, roofGroup.gameObject);
            foreach (var door in Object.FindObjectsByType<AutoDoor>(FindObjectsInactive.Include))
                door.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;   // they open for whoever walks up
            MoveIn(Group("People and pets", null));
            ApplyDefaultLayout();
            PhysicsSetup.AssignSurfaces(house);
            PhysicsSetup.AssignSurfaces(GameObject.Find("Garden").transform);

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            const string scenePath = "Assets/Scenes/Main.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            CinematicSetup.Bake();
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Tiramisu: greybox house built and saved to " + scenePath);
        }

        /// <summary>The home lot and the empty lots in the city where the player builds a house: each is a Lot component (see Lot.cs), the ground is made when the game starts.</summary>
        static void BuildLots(Transform parent)
        {
            var home = Group("Home lot", parent);
            var hl = home.gameObject.AddComponent<Tiramisu.Lot>();
            hl.lotName = "Home"; hl.home = true; hl.order = 0;
            hl.blurb = "The house that came ready built. You can furnish it and paint it, but not build on it.";
            hl.min = new Vector2(-2.7f, -4.7f); hl.max = new Vector2(33.9f, 22.7f);
            hl.spawn = new Vector3(3.2f, 0f, 6.2f);

            string[] names = { "Gamuda Gardens", "Savannah Suites", "Kiara Greens" };
            string[] blurbs =
            {
                "A sunny corner plot across the road from home. Flat, open and ready.",
                "A quiet plot between two streets, a good place for a glass house like ours.",
                "A wide plot west of home, with room for a big garden and a pool.",
            };
            for (int i = 0; i < CityBuilder.BuildLots.Length; i++)
            {
                var r = CityBuilder.BuildLots[i];
                var g = Group("Lot " + names[i], parent);
                var lot = g.gameObject.AddComponent<Tiramisu.Lot>();
                lot.lotName = names[i]; lot.blurb = blurbs[i]; lot.home = false; lot.order = i + 1;
                lot.min = new Vector2(r.xMin + 1f, r.yMin + 1f); lot.max = new Vector2(r.xMax - 1f, r.yMax - 1f);
                lot.lawn = lawn; lot.body = slab; lot.poolFloor = poolMarble;
                lot.spawn = new Vector3((lot.min.x + lot.max.x) * 0.5f, 0f, lot.min.y + 2.5f);
                // a For sale sign at the front corner, two trees at the back
                var sign = Group("For sale sign", g);
                Box("Post", sign, new Vector3(lot.min.x + 1.6f, 0f, lot.min.y + 1.2f), new Vector3(lot.min.x + 1.72f, 1.5f, lot.min.y + 1.32f), steel);
                Box("Board", sign, new Vector3(lot.min.x + 1.0f, 1.0f, lot.min.y + 1.16f), new Vector3(lot.min.x + 2.32f, 1.6f, lot.min.y + 1.22f), Pl("ForSale", new Color(0.86f, 0.28f, 0.34f), 0.3f));
                Tree(g, new Vector3(lot.max.x - 3f, 0f, lot.max.y - 2.5f), 1.4f);
                Tree(g, new Vector3(lot.min.x + 3f, 0f, lot.max.y - 2.5f), 1.2f);
                if (i == 2) { Tree(g, new Vector3(lot.max.x - 6f, 0f, lot.max.y - 4f), 1.6f); Tree(g, new Vector3(lot.min.x + 8f, 0f, lot.max.y - 3f), 1.5f); }
            }
        }

        static void BuildGround(Transform g)
        {
            Box("Slab", g, new Vector3(-OUT, -SLAB, -OUT), new Vector3(WX + GLASS, 0f, WD + GLASS), slab);
            Box("Slab oak band", g, new Vector3(-OUT - 0.02f, -0.22f, WD + GLASS), new Vector3(WX + GLASS + 0.02f, -0.08f, WD + GLASS + 0.02f), oakDark);

            Room(g, "Living room", 0, 0, 0, 8, 0, WD, 0f, oak);
            Room(g, "Kitchen", 0, 1, 8, 14, 0, WD, 0f, tile);
            Room(g, "Stair hall", 0, 2, 14, 16, 0, WD, 0f, oak);
            Room(g, "Bathroom", 0, 3, 16, 22, 0, WD, 0f, bathTile);
            Room(g, "Garage", 0, 4, 22, 30, 0, WD, 0f, garageFloor);

            var walls = Group("Walls", g);
            WindowedWall(walls, "Back wall", WallCutaway.Axis.Z, -OUT, OUT, -OUT, WX + GLASS, 0f, wallBrown,
                new[] { Zone(-1f, 8f, pLiving), Zone(8f, 14f, pKitchen), Zone(14f, 16f, pHall), Zone(16f, 22f, pBath), Zone(22f, 31f, pGarage) },
                Win(1.9f, 1.2f, 1.0f, 2.3f), Win(6.2f, 1.2f, 1.0f, 2.3f), Win(15f, 0.8f, 0.9f, 2.5f), Win(20.4f, 1.4f, 1.9f, 2.5f), Win(27f, 1.2f, 1.2f, 2.3f));
            WindowedWall(walls, "Left wall", WallCutaway.Axis.X, -OUT, OUT, 0f, WD + GLASS, 0f, wallBrown, new[] { Zone(-1f, 9f, pLiving) }, Win(5.2f, 1.6f, 0.9f, 2.3f));
            Wall(walls, "Garage end glass", WallCutaway.Axis.X, WX, GLASS, 0f, WD, 0f, glass, true, new Vector2(2f, 6f));   // the shutter door to the road
            Wall(walls, "Front glass", WallCutaway.Axis.Z, WD, GLASS, 0f, WX + GLASS, 0f, glass, true,
                new Vector2(3, 5), new Vector2(10, 12), new Vector2(18, 20), new Vector2(24, 28));
            Wall(walls, "Living and kitchen glass", WallCutaway.Axis.X, 8f - PART / 2, PART, 0f, WD, 0f, glass, true, new Vector2(3, 5));
            Paint2(pKitchen, pHall);
            Wall(walls, "Kitchen and hall wall", WallCutaway.Axis.X, 14f - PART / 2, PART, 0f, WD, 0f, wallWhite, false, new Vector2(6.2f, 7.8f));
            Paint2(pHall, pBath);
            Wall(walls, "Hall and bathroom wall", WallCutaway.Axis.X, 16f - PART / 2, PART, 0f, WD, 0f, wallWhite, false, new Vector2(6.2f, 7.8f));
            Paint2(null, null);
            Wall(walls, "Bathroom and garage glass", WallCutaway.Axis.X, 22f - PART / 2, PART, 0f, WD, 0f, glass, true, new Vector2(3, 5));

            // floating oak stairs climbing from the front of the hall (z 6) up to the landing (z 2)
            var stairs = Group("Stairs", g);
            var stairArea = stairs.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
            stairArea.overrideArea = true;
            stairArea.area = TiramisuNav.StairsArea;
            const int steps = 14;
            float rise = UPY / (steps + 1), run = 4f / steps;
            for (int i = 0; i < steps; i++)
            {
                float top = (i + 1) * rise;
                float z1 = 6f - i * run, z0 = z1 - run;
                Box($"Step {i + 1}", stairs, new Vector3(14.2f, top - 0.07f, z0), new Vector3(15.8f, top, z1), oak);
                // slim steel support under each step, so the stairs float without a wall in the middle of the hall
                Box($"Step support {i + 1}", stairs, new Vector3(14.92f, top - 0.32f, z0), new Vector3(15.08f, top - 0.07f, z1), steel);
            }
        }

        static void BuildUpper(Transform g)
        {
            Box("Slab left", g, new Vector3(-OUT, UPY - SLAB, -OUT), new Vector3(14f, UPY, WD + GLASS), slab);
            Box("Slab right", g, new Vector3(16f, UPY - SLAB, -OUT), new Vector3(WX + GLASS, UPY, WD + GLASS), slab);
            Box("Slab landing", g, new Vector3(14f, UPY - SLAB, -OUT), new Vector3(16f, UPY, 2f), slab);

            Room(g, "Teacher's Room", 1, 10, 0, 8, 0, WD, UPY, oak);
            Room(g, "Office & Library", 1, 11, 8, 14, 0, WD, UPY, oakDark);
            Room(g, "Landing", 1, 12, 14, 16, 0, 2, UPY, oak);
            Room(g, "Engineer's Room", 1, 13, 16, 22, 0, WD, UPY, oak);
            Room(g, "Gym", 1, 14, 22, 30, 0, WD, UPY, gym);

            var walls = Group("Walls", g);
            WindowedWall(walls, "Back wall", WallCutaway.Axis.Z, -OUT, OUT, -OUT, WX + GLASS, UPY, wallBrown,
                new[] { Zone(-1f, 8f, pTeacher), Zone(8f, 14f, pOffice), Zone(14f, 16f, pLanding), Zone(16f, 22f, pEngineer), Zone(22f, 31f, pGym) },
                Win(7.3f, 0.9f, 1.0f, 2.2f), Win(15f, 0.8f, 1.0f, 2.4f), Win(29.2f, 1.2f, 1.2f, 2.4f));
            WindowedWall(walls, "Left wall", WallCutaway.Axis.X, -OUT, OUT, 0f, WD + GLASS, UPY, wallBrown, new[] { Zone(-1f, 9f, pTeacher) }, Win(1.65f, 1.0f, 1.0f, 2.2f));
            Wall(walls, "Gym end glass", WallCutaway.Axis.X, WX, GLASS, 0f, WD, UPY, glass, true);
            Wall(walls, "Front glass", WallCutaway.Axis.Z, WD, GLASS, 0f, WX + GLASS, UPY, glass, true);
            Paint2(pTeacher, pOffice);
            Wall(walls, "Teacher and office wall", WallCutaway.Axis.X, 8f - PART / 2, PART, 0f, WD, UPY, wallWhite, false, new Vector2(3, 5));
            Paint2(pOffice, pLanding);
            Wall(walls, "Office and landing wall", WallCutaway.Axis.X, 14f - PART / 2, PART, 0f, WD, UPY, wallWhite, false, new Vector2(0.4f, 1.8f));
            Paint2(pLanding, pEngineer);
            Wall(walls, "Landing and engineer wall", WallCutaway.Axis.X, 16f - PART / 2, PART, 0f, WD, UPY, wallWhite, false, new Vector2(0.4f, 1.8f));
            Paint2(null, null);
            Wall(walls, "Engineer and gym glass", WallCutaway.Axis.X, 22f - PART / 2, PART, 0f, WD, UPY, glass, true, new Vector2(3, 5));
        }

        /// <summary>A box slab with rectangular holes cut in it (x0, z0, x1, z1 in world metres), built as a grid of boxes.</summary>
        static void SlabWithHoles(string name, Transform parent, Vector3 lo, Vector3 hi, Material m, System.Collections.Generic.List<Vector4> holes)
        {
            var xs = new System.Collections.Generic.SortedSet<float> { lo.x, hi.x };
            var zs = new System.Collections.Generic.SortedSet<float> { lo.z, hi.z };
            foreach (var h in holes) { xs.Add(h.x); xs.Add(h.z); zs.Add(h.y); zs.Add(h.w); }
            var xa = new System.Collections.Generic.List<float>(xs);
            var za = new System.Collections.Generic.List<float>(zs);
            for (int i = 0; i + 1 < xa.Count; i++)
                for (int j = 0; j + 1 < za.Count; j++)
                {
                    float cx = (xa[i] + xa[i + 1]) * 0.5f, cz = (za[j] + za[j + 1]) * 0.5f;
                    bool inHole = false;
                    foreach (var h in holes) if (cx > h.x && cx < h.z && cz > h.y && cz < h.w) { inHole = true; break; }
                    if (inHole) continue;
                    Box($"{name} {i}.{j}", parent, new Vector3(xa[i], lo.y, za[j]), new Vector3(xa[i + 1], hi.y, za[j + 1]), m);
                }
        }

        static void BuildRoof(Transform g)
        {
            // flat modern roof: deep overhang over the deck, oak soffit underneath, black steel fascia, and skylights
            float y = UPY + H;
            Vector3 lo = new Vector3(-0.7f, y, -0.7f), hi = new Vector3(WX + 0.7f, y + 0.32f, WD + 1.6f);
            var holes = new System.Collections.Generic.List<Vector4>
            {
                new Vector4(14.15f, 2.3f, 15.85f, 5.7f),      // over the stairwell, light falls down into the stair hall
                new Vector4(3.4f, 2.6f, 5.6f, 4.6f),          // teacher's room
                new Vector4(9.9f, 2.6f, 12.1f, 4.6f),         // office
                new Vector4(18.9f, 2.6f, 21.1f, 4.6f),        // engineer's room
                new Vector4(24.9f, 2.6f, 27.1f, 4.6f),        // gym
            };
            SlabWithHoles("Roof", g, new Vector3(lo.x, y + 0.03f, lo.z), hi, roof, holes);
            SlabWithHoles("Roof soffit (oak)", g, lo, new Vector3(hi.x, y + 0.03f, hi.z), oakDark, holes);
            foreach (var h in holes)
            {
                // a low black steel curb round each opening and a pane of glass in it
                const float c = 0.06f;
                Box("Skylight curb a", g, new Vector3(h.x - c, y, h.y - c), new Vector3(h.z + c, y + 0.45f, h.y), steel);
                Box("Skylight curb b", g, new Vector3(h.x - c, y, h.w), new Vector3(h.z + c, y + 0.45f, h.w + c), steel);
                Box("Skylight curb c", g, new Vector3(h.x - c, y, h.y), new Vector3(h.x, y + 0.45f, h.w), steel);
                Box("Skylight curb d", g, new Vector3(h.z, y, h.y), new Vector3(h.z + c, y + 0.45f, h.w), steel);
                Box("Skylight glass", g, new Vector3(h.x, y + 0.2f, h.y), new Vector3(h.z, y + 0.22f, h.w), glass, false);
                Box("Skylight bar", g, new Vector3((h.x + h.z) * 0.5f - 0.02f, y + 0.19f, h.y), new Vector3((h.x + h.z) * 0.5f + 0.02f, y + 0.24f, h.w), steel);
            }
            const float f = 0.06f;
            Box("Fascia front", g, new Vector3(lo.x - f, y - 0.02f, hi.z), new Vector3(hi.x + f, hi.y + 0.08f, hi.z + f), steel);
            Box("Fascia back", g, new Vector3(lo.x - f, y - 0.02f, lo.z - f), new Vector3(hi.x + f, hi.y + 0.08f, lo.z), steel);
            Box("Fascia left", g, new Vector3(lo.x - f, y - 0.02f, lo.z), new Vector3(lo.x, hi.y + 0.08f, hi.z), steel);
            Box("Fascia right", g, new Vector3(hi.x, y - 0.02f, lo.z), new Vector3(hi.x + f, hi.y + 0.08f, hi.z), steel);
        }

        const float FX0 = -3f, FX1 = 34.25f, FZ0 = -5f, FZ1 = 23f;   // the plot the fence stands on

        static void BuildGarden(Transform g)
        {
            const float gy = -SLAB;
            // outer ground and lawn both leave a hole for the pool (x 18 to 26, z 12 to 16)
            float px0 = 18f, px1 = 26f, pz0 = 12f, pz1 = 16f;
            float gb = gy - 0.4f, gt = gy - 0.02f;
            Box("Ground front", g, new Vector3(-200f, gb, -200f), new Vector3(230f, gt, pz0), lawnDark);
            // the jacuzzi is attached to the far long edge of the pool (x 20.5 to 23.5, z 16 to 18)
            float jx0 = 20.5f, jx1 = 23.5f, jz1 = 18f;
            Box("Ground back a", g, new Vector3(-200f, gb, pz1), new Vector3(jx0, gt, 230f), lawnDark);
            Box("Ground back b", g, new Vector3(jx1, gb, pz1), new Vector3(230f, gt, 230f), lawnDark);
            Box("Ground back c", g, new Vector3(jx0, gb, jz1), new Vector3(jx1, gt, 230f), lawnDark);
            Box("Ground left", g, new Vector3(-200f, gb, pz0), new Vector3(px0, gt, pz1), lawnDark);
            Box("Ground right", g, new Vector3(px1, gb, pz0), new Vector3(230f, gt, pz1), lawnDark);

            // the whole plot (from the back of the house to the front fence) is lawn, with holes for the pool and the tub
            SlabWithHoles("Lawn", g, new Vector3(FX0, gy - 0.02f, FZ0), new Vector3(FX1, gy, FZ1), lawn,
                new System.Collections.Generic.List<Vector4> { new Vector4(px0, pz0, px1, pz1), new Vector4(jx0, pz1, jx1, jz1) });

            Box("Deck", g, new Vector3(0f, gy, WD + GLASS), new Vector3(WX + GLASS, -0.06f, 10.5f), deck);

            Road(g, gy);

            var pool = Group("Pool", g);
            Box("Pool basin", pool, new Vector3(px0, gy - 1.4f, pz0), new Vector3(px1, gy - 1.3f, pz1), poolMarble);
            Box("Pool wall near", pool, new Vector3(px0, gy - 1.4f, pz0), new Vector3(px1, gy, pz0 + 0.05f), poolMarble);
            Box("Pool wall far a", pool, new Vector3(px0, gy - 1.4f, pz1 - 0.05f), new Vector3(jx0, gy, pz1), poolMarble);
            Box("Pool wall far b", pool, new Vector3(jx1, gy - 1.4f, pz1 - 0.05f), new Vector3(px1, gy, pz1), poolMarble);
            Box("Pool wall left", pool, new Vector3(px0, gy - 1.4f, pz0), new Vector3(px0 + 0.05f, gy, pz1), poolMarble);
            Box("Pool wall right", pool, new Vector3(px1 - 0.05f, gy - 1.4f, pz0), new Vector3(px1, gy, pz1), poolMarble);
            // jacuzzi: a shallow rectangular tub open to the pool along its long edge
            Box("Jacuzzi basin", pool, new Vector3(jx0, gy - 0.95f, pz1), new Vector3(jx1, gy - 0.9f, jz1), poolMarble);
            Box("Jacuzzi wall left", pool, new Vector3(jx0, gy - 0.95f, pz1), new Vector3(jx0 + 0.05f, gy, jz1), poolMarble);
            Box("Jacuzzi wall right", pool, new Vector3(jx1 - 0.05f, gy - 0.95f, pz1), new Vector3(jx1, gy, jz1), poolMarble);
            Box("Jacuzzi wall far", pool, new Vector3(jx0, gy - 0.95f, jz1 - 0.05f), new Vector3(jx1, gy, jz1), poolMarble);
            Box("Jacuzzi bench", pool, new Vector3(jx0 + 0.05f, gy - 0.9f, jz1 - 0.5f), new Vector3(jx1 - 0.05f, gy - 0.5f, jz1 - 0.05f), poolMarble);
            // the surfaces are live wave meshes (ripples, splashes, buoyancy), see PoolRipples
            var ripples = pool.gameObject.AddComponent<PoolRipples>();
            ripples.min = new Vector2(px0 + 0.05f, pz0 + 0.05f);
            ripples.max = new Vector2(px1 - 0.05f, pz1 - 0.05f);
            ripples.surfaceY = gy - 0.12f;
            ripples.floorY = gy - 1.3f;
            ripples.material = water;
            var tub = Group("Jacuzzi water", pool);
            var tubRipples = tub.gameObject.AddComponent<PoolRipples>();
            tubRipples.min = new Vector2(jx0 + 0.05f, pz1 - 0.04f);
            tubRipples.max = new Vector2(jx1 - 0.05f, jz1 - 0.05f);
            tubRipples.surfaceY = gy - 0.12f;
            tubRipples.floorY = gy - 0.9f;
            tubRipples.material = water;
            tubRipples.breeze = 9f;            // the jets keep it churning
            tubRipples.damping = 0.975f;
            // two water jets from nozzles in the far wall shoot up and arc into the tub
            for (int j = 0; j < 2; j++)
            {
                float nx = (jx0 + jx1) * 0.5f + (j == 0 ? -0.6f : 0.6f);
                Box("Jet nozzle", tub, new Vector3(nx - 0.05f, gy - 0.16f, jz1 - 0.09f), new Vector3(nx + 0.05f, gy - 0.06f, jz1 - 0.05f), steel);
                var jet = tub.gameObject.AddComponent<WaterJet>();
                jet.start = new Vector3(nx, gy - 0.1f, jz1 - 0.12f);
                jet.velocity = new Vector3(j == 0 ? 0.2f : -0.2f, 2.3f, -2.6f);
                jet.material = dropMat;
                jet.target = tubRipples;
                jet.count = 46;
            }
            // cyan glow from under the tub bench
            Strip2(pool, "Jacuzzi glow", new Vector3(jx0 + 0.05f, gy - 0.55f, jz1 - 0.06f), new Vector3(jx1 - 0.05f, gy - 0.5f, jz1 - 0.05f), ledCool, new Color(0.5f, 0.85f, 1f) * 14f);
            AddNightLight(tub.gameObject, new Vector3((jx0 + jx1) * 0.5f, gy - 0.3f, 17.2f), new Color(0.3f, 0.85f, 1f), 420f, 4f, 0f, new Vector2(1.4f, 0.6f), 90f);
            // coping: around the pool, the tub cut out of it
            Box("Coping near", pool, new Vector3(px0 - 0.3f, gy, pz0 - 0.3f), new Vector3(px1 + 0.3f, gy + 0.05f, pz0), stone);
            Box("Coping far a", pool, new Vector3(px0 - 0.3f, gy, pz1), new Vector3(jx0 - 0.3f, gy + 0.05f, pz1 + 0.3f), stone);
            Box("Coping far b", pool, new Vector3(jx1 + 0.3f, gy, pz1), new Vector3(px1 + 0.3f, gy + 0.05f, pz1 + 0.3f), stone);
            Box("Coping left", pool, new Vector3(px0 - 0.3f, gy, pz0), new Vector3(px0, gy + 0.05f, pz1), stone);
            Box("Coping right", pool, new Vector3(px1, gy, pz0), new Vector3(px1 + 0.3f, gy + 0.05f, pz1), stone);
            Box("Tub coping left", pool, new Vector3(jx0 - 0.3f, gy, pz1), new Vector3(jx0, gy + 0.05f, jz1 + 0.3f), stone);
            Box("Tub coping right", pool, new Vector3(jx1, gy, pz1), new Vector3(jx1 + 0.3f, gy + 0.05f, jz1 + 0.3f), stone);
            Box("Tub coping far", pool, new Vector3(jx0 - 0.3f, gy, jz1), new Vector3(jx1 + 0.3f, gy + 0.05f, jz1 + 0.3f), stone);

            // a small wall fountain standing on the short east end of the pool, black marble with a spout over the water
            var wf = Group("Wall fountain", pool);
            Box("Wall", wf, new Vector3(26.06f, gy, 13.0f), new Vector3(26.36f, gy + 1.7f, 15.0f), poolMarble);
            Box("Wall cap", wf, new Vector3(26.02f, gy + 1.7f, 12.96f), new Vector3(26.4f, gy + 1.74f, 15.04f), steel);
            Box("Spout ledge", wf, new Vector3(25.6f, gy + 1.42f, 13.15f), new Vector3(26.06f, gy + 1.48f, 14.85f), steel);
            Strip2(wf, "Wall fountain glow", new Vector3(25.62f, gy + 1.38f, 13.2f), new Vector3(26.05f, gy + 1.42f, 14.8f), ledWarm, new Color(1f, 0.72f, 0.4f) * 18f);
            Strip2(wf, "Wall fountain edge a", new Vector3(26.02f, gy + 0.05f, 12.96f), new Vector3(26.07f, gy + 1.7f, 13.0f), ledCool, new Color(0.55f, 0.82f, 1f) * 18f);
            Strip2(wf, "Wall fountain edge b", new Vector3(26.02f, gy + 0.05f, 15.0f), new Vector3(26.07f, gy + 1.7f, 15.04f), ledCool, new Color(0.55f, 0.82f, 1f) * 18f);
            AddNightLight(wf.gameObject, new Vector3(25.85f, gy + 1.35f, 14f), new Color(1f, 0.76f, 0.5f), 420f, 4.5f, 0f, new Vector2(1.5f, 0.3f), 90f);
            var sheet = wf.gameObject.AddComponent<FallingWater>();
            sheet.start = new Vector3(25.75f, gy + 1.42f, 14f);
            sheet.lineAxis = Vector3.forward;
            sheet.lineLength = 1.6f;
            sheet.endY = gy - 0.12f;
            sheet.count = 70;
            sheet.fallSeconds = 0.55f;
            sheet.target = ripples;
            sheet.splash = 0.5f;
            sheet.material = dropMat;
            sheet.streak = new Vector2(0.022f, 0.16f);

            var path = Group("Stepping stones", g);
            for (int i = 0; i < 10; i++)
            {
                float z = 11f + i * 1.2f;
                Box($"Stone {i + 1}", path, new Vector3(3.5f, gy, z), new Vector3(4.5f, gy + 0.04f, z + 0.7f), stone);
            }

            foreach (var p in GardenProps) PropPlacer.Place(p, g);
            NightLights(g, gy, px0, px1, pz0, pz1);
            StringLights(g, gy);
            BuildFence(g, gy);
            Hedges(g, gy);
            LaneSlidingGate(g, gy);
            OuterSidewalk(g, gy);
            BackPath(g, gy);
            LedStrips(g, gy, px0, px1, pz0, pz1);

            Room(g, "Garden & pool", 0, 5, 0, WX, 10.5f, 22f, gy, null);
        }

        /// <summary>
        /// A light that fades on at night. With an area size it is a soft rectangle panel that faces
        /// (pitch, yaw) degrees (90 = down, -90 = up), otherwise a small point light.
        /// </summary>
        static void AddNightLight(GameObject host, Vector3 at, Color colour, float night, float range, float day = 0f,
            Vector2 area = default, float pitch = 90f, float yaw = 0f)
        {
            var lg = new GameObject("Night light");
            lg.transform.SetParent(host.transform, false);
            lg.transform.position = at;
            var l = lg.AddComponent<Light>();
            if (area != default)
            {
                l.type = LightType.Rectangle;
                l.areaSize = area;
                lg.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            else l.type = LightType.Point;
            lg.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
            l.lightUnit = LightUnit.Lumen;
            l.color = colour;
            l.range = range;
            l.intensity = day;
            l.shadows = LightShadows.None;
            var sw = lg.AddComponent<SwitchableLight>();
            sw.day = day;
            sw.night = night;
        }

        /// <summary>Lights that come on at night: path bollards, soffit downlights over the deck, glowing pool lamps.</summary>
        static void NightLights(Transform g, float gy, float px0, float px1, float pz0, float pz1)
        {
            var lights = Group("Night lights", g);
            var warm = new Color(1f, 0.76f, 0.45f);

            // low bollards along the edge of the deck and beside the stepping stones
            var bolls = new System.Collections.Generic.List<Vector2>();
            foreach (float x in new[] { 1.5f, 6.5f, 10.5f, 14.5f, 29f }) bolls.Add(new Vector2(x, 10.9f));
            foreach (float z in new[] { 12.5f, 15.5f, 18.5f }) bolls.Add(new Vector2(2.6f, z));
            foreach (float z in new[] { 12.5f, 15.5f }) bolls.Add(new Vector2(5.4f, z));
            foreach (var b in bolls)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Bollard";
                post.transform.SetParent(lights, false);
                post.transform.position = new Vector3(b.x, gy + 0.32f, b.y);
                post.transform.localScale = new Vector3(0.11f, 0.32f, 0.11f);
                post.GetComponent<Renderer>().sharedMaterial = steel;
                var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cap.name = "Bollard glow";
                cap.transform.SetParent(post.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.93f, 0f);
                cap.transform.localScale = new Vector3(0.9f, 0.08f, 0.9f);
                cap.GetComponent<Renderer>().sharedMaterial = lampGlow;
                Object.DestroyImmediate(cap.GetComponent<Collider>());
                Glow(cap, new Color(1f, 0.7f, 0.35f) * 2.2f);
                AddNightLight(post, new Vector3(b.x, gy + 0.72f, b.y), warm, 140f, 4f, 0f, new Vector2(0.3f, 0.3f), 90f);
            }

            // downlights in the roof overhang above the deck
            float ry = UPY + H - 0.02f;
            for (int i = 0; i < 6; i++)
            {
                float x = 2.5f + i * 5.2f;
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = "Soffit light";
                disc.transform.SetParent(lights, false);
                disc.transform.position = new Vector3(x, ry, WD + 1.1f);
                disc.transform.localScale = new Vector3(0.22f, 0.005f, 0.22f);
                disc.GetComponent<Renderer>().sharedMaterial = lampGlow;
                Object.DestroyImmediate(disc.GetComponent<Collider>());
                Glow(disc, new Color(1f, 0.7f, 0.35f) * 2.2f);
                AddNightLight(disc, new Vector3(x, ry - 0.05f, WD + 1.1f), warm, 380f, 6f, 0f, new Vector2(0.7f, 0.7f), 90f);
            }

            // glowing lamps in the pool wall and the water lit from below
            foreach (float x in new[] { 19.7f, 22f, 24.3f })
            {
                foreach (bool near in new[] { true, false })
                {
                    float z = near ? pz0 + 0.05f : pz1 - 0.05f;
                    float dz = near ? 0.02f : -0.02f;
                    var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    lamp.name = "Pool lamp";
                    lamp.transform.SetParent(lights, false);
                    lamp.transform.position = new Vector3(x, gy - 0.85f, z + dz);
                    lamp.transform.localScale = new Vector3(0.32f, 0.12f, 0.03f);
                    lamp.GetComponent<Renderer>().sharedMaterial = poolGlow;
                    Object.DestroyImmediate(lamp.GetComponent<Collider>());
                    Glow(lamp, new Color(0.3f, 0.85f, 1f) * 2.5f);
                    AddNightLight(lamp, new Vector3(x, gy - 0.85f, z + (near ? 0.12f : -0.12f)), new Color(0.3f, 0.85f, 1f), 260f, 6f, 0f, new Vector2(0.9f, 0.5f), 0f, near ? 0f : 180f);
                }
            }
        }

        static Transform roofRoot, upperRoot;   // things that belong to the roof or the upper floor hide with them

        /// <summary>A glowing strip (at least 5 cm thick) that only lights up at night.</summary>
        static void Strip2(Transform parent, string nm, Vector3 a, Vector3 b, Material m, Color emit)
        {
            Vector3 mid = (a + b) * 0.5f, size = b - a;
            for (int k = 0; k < 3; k++) if (size[k] < 0.04f) size[k] = 0.04f;
            var st = Box(nm, parent, mid - size * 0.5f, mid + size * 0.5f, m, false);
            Object.DestroyImmediate(st.GetComponent<Collider>());
            Glow(st, emit);
        }

        // ---------- fence and gates ----------

        /// <summary>
        /// Modern fence round the plot: black steel posts, walnut panels on a low concrete plinth, a steel cap, and a warm
        /// wall light on every post (a soft area light on every third). Each side is a WallCutaway, so the side between the
        /// camera and the house drops out of the way. Gaps: the front gate (aligned with the paving), the back gate and the
        /// open lane to the garage.
        /// </summary>
        static void BuildFence(Transform g, float gy)
        {
            var root = Group("Fence", g);
            // side, axis, fixed coordinate, from, to, gaps (from, to), inward sign, name
            FenceSide(root, gy, "Front fence", WallCutaway.Axis.Z, FZ1, FX0, FX1, new[] { new Vector2(3.2f, 4.8f) }, -1f);
            FenceSide(root, gy, "Back fence", WallCutaway.Axis.Z, FZ0, FX0, FX1, new[] { new Vector2(25.2f, 27.0f) }, +1f);
            FenceSide(root, gy, "West fence", WallCutaway.Axis.X, FX0, FZ0, FZ1, new Vector2[0], +1f);
            FenceSide(root, gy, "East fence", WallCutaway.Axis.X, FX1, FZ0, FZ1, new[] { new Vector2(1.3f, 6.9f) }, -1f);
        }

        static void FenceSide(Transform root, float gy, string name, WallCutaway.Axis axis, float fixedC, float a, float b, Vector2[] gaps, float inward)
        {
            var line = Group(name, root);
            var cut = line.gameObject.AddComponent<WallCutaway>();
            cut.axis = axis; cut.plane = fixedC; cut.floor = 0; cut.floorY = gy; cut.fullHeight = 2.4f;
            var lights = Group(name + " lights", root);
            cut.attachments.Add(lights.gameObject);
            Vector3 P(float along, float y, float depth) => axis == WallCutaway.Axis.Z ? new Vector3(along, y, depth) : new Vector3(depth, y, along);
            var segs = new System.Collections.Generic.List<Vector2>();
            float cur = a;
            System.Array.Sort(gaps, (p, q) => p.x.CompareTo(q.x));
            foreach (var gp in gaps) { segs.Add(new Vector2(cur, gp.x)); cur = gp.y; }
            segs.Add(new Vector2(cur, b));
            int postCount = 0;
            foreach (var sg in segs)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt((sg.y - sg.x) / 2.5f));
                float step = (sg.y - sg.x) / n;
                for (int i = 0; i <= n; i++)
                {
                    float u = sg.x + i * step;
                    bool end = i == 0 || i == n;
                    float h = end ? 2.15f : 1.95f;
                    Box("Post", line, P(u - 0.07f, gy, fixedC - 0.07f), P(u + 0.07f, gy + h, fixedC + 0.07f), steel);
                    // wall light on the inner face
                    float dInner = fixedC + inward * 0.075f;
                    var lamp = Box("Wall light", line, P(u - 0.06f, gy + 1.5f, Mathf.Min(dInner, dInner + inward * 0.06f)), P(u + 0.06f, gy + 1.58f, Mathf.Max(dInner, dInner + inward * 0.06f)), ledWarm, false);
                    Object.DestroyImmediate(lamp.GetComponent<Collider>());
                    Glow(lamp, new Color(1f, 0.72f, 0.4f) * 16f);
                    if (postCount++ % 3 == 0)
                        AddNightLight(lights.gameObject, P(u, gy + 1.45f, fixedC + inward * 0.35f), new Color(1f, 0.75f, 0.45f), 260f, 5f, 0f, new Vector2(0.3f, 0.3f), 90f);
                    if (i == n) break;
                    float u0 = u + 0.07f, u1 = u + step - 0.07f;
                    Box("Plinth", line, P(u0, gy, fixedC - 0.1f), P(u1, gy + 0.2f, fixedC + 0.1f), slab);
                    Box("Panel", line, P(u0, gy + 0.2f, fixedC - 0.03f), P(u1, gy + 1.9f, fixedC + 0.03f), doorWood);
                    Box("Rail low", line, P(u0, gy + 0.7f, fixedC - 0.045f), P(u1, gy + 0.73f, fixedC + 0.045f), steel);
                    Box("Rail high", line, P(u0, gy + 1.3f, fixedC - 0.045f), P(u1, gy + 1.33f, fixedC + 0.045f), steel);
                    Box("Cap", line, P(u0, gy + 1.9f, fixedC - 0.06f), P(u1, gy + 1.94f, fixedC + 0.06f), steel);
                }
            }
            // gates and the lane
            foreach (var gp in gaps)
            {
                bool lane = axis == WallCutaway.Axis.X && fixedC > 20f;
                if (lane) continue;   // open lane to the garage, the posts on both sides are the frame
                FenceGate(root, cut, name.Replace("fence", "gate"), axis, fixedC, gp.x, gp.y, gy, inward);
            }
        }

        /// <summary>A double swing gate in walnut and black steel, swinging inwards, that opens like the doors (hover, people, pets).</summary>
        static void FenceGate(Transform root, WallCutaway cut, string name, WallCutaway.Axis axis, float fixedC, float a, float b, float gy, float inward)
        {
            var gate = Group(name, root);
            Vector3 P(float along, float y, float depth) => axis == WallCutaway.Axis.Z ? new Vector3(along, y, depth) : new Vector3(depth, y, along);
            float half = (b - a) * 0.5f;
            var leaves = new Transform[2];
            var angles = new float[2];
            for (int i = 0; i < 2; i++)
            {
                bool first = i == 0;
                var hinge = Group(first ? "Leaf a" : "Leaf b", gate);
                float hu = first ? a : b, dir = first ? 1f : -1f;
                float lo = Mathf.Min(hu, hu + dir * (half - 0.01f)), hi = Mathf.Max(hu, hu + dir * (half - 0.01f));
                hinge.position = P(hu, gy, fixedC);
                Box("Slab", hinge, P(lo, gy + 0.12f, fixedC - 0.03f), P(hi, gy + 1.85f, fixedC + 0.03f), doorWood);
                Box("Frame bottom", hinge, P(lo, gy + 0.1f, fixedC - 0.05f), P(hi, gy + 0.16f, fixedC + 0.05f), steel);
                Box("Frame top", hinge, P(lo, gy + 1.82f, fixedC - 0.05f), P(hi, gy + 1.88f, fixedC + 0.05f), steel);
                Box("Frame near", hinge, P(lo, gy + 0.1f, fixedC - 0.05f), P(lo + 0.06f, gy + 1.88f, fixedC + 0.05f), steel);
                Box("Frame far", hinge, P(hi - 0.06f, gy + 0.1f, fixedC - 0.05f), P(hi, gy + 1.88f, fixedC + 0.05f), steel);
                float hx = hu + dir * (half - 0.2f);
                Box("Handle", hinge, P(hx - 0.015f, gy + 0.85f, fixedC - 0.07f), P(hx + 0.015f, gy + 1.35f, fixedC + 0.07f), steel);
                leaves[i] = hinge;
                // both leaves swing inwards (towards the plot)
                angles[i] = (first ? -1f : 1f) * inward * 100f;   // front gate (inward -1): left +100, right -100
            }
            // wider posts with a glowing cap either side
            foreach (float u in new[] { a - 0.16f, b + 0.16f })
            {
                Box("Gate post", gate, P(u - 0.13f, gy, fixedC - 0.13f), P(u + 0.13f, gy + 2.3f, fixedC + 0.13f), steel);
                var cap = Box("Gate lamp", gate, P(u - 0.1f, gy + 2.3f, fixedC - 0.1f), P(u + 0.1f, gy + 2.36f, fixedC + 0.1f), ledWarm, false);
                Object.DestroyImmediate(cap.GetComponent<Collider>());
                Glow(cap, new Color(1f, 0.72f, 0.4f) * 18f);
                AddNightLight(gate.gameObject, P(u, gy + 2.2f, fixedC + inward * 0.4f), new Color(1f, 0.75f, 0.45f), 320f, 5f, 0f, new Vector2(0.3f, 0.3f), 90f);
            }
            var door = gate.gameObject.AddComponent<HingedDoor>();
            door.leaves = leaves;
            door.angles = angles;
            door.openSeconds = 1.2f;
            door.stayOpenSeconds = 2.5f;
            door.sensorCenter = P((a + b) * 0.5f, gy + 1.1f, fixedC);
            door.sensorSize = axis == WallCutaway.Axis.Z ? new Vector3((b - a) + 1.2f, 2.2f, 5f) : new Vector3(5f, 2.2f, (b - a) + 1.2f);
            cut.attachments.Add(gate.gameObject);
        }

        /// <summary>Paving stones from the back gate to the house and along the back of it to the shed.</summary>
        /// <summary>A paved sidewalk round the house with strips that run out to the front gate and the back gate.</summary>
        static void HouseSidewalk(Transform g, float gy)
        {
            var w = Group("House sidewalk", g);
            float t = gy + 0.05f;
            Box("Sidewalk back", w, new Vector3(-1.5f, gy, -1.5f), new Vector3(31.3f, t, -OUT), stone);
            Box("Sidewalk left", w, new Vector3(-1.5f, gy, -OUT), new Vector3(-OUT, t, 10.5f), stone);
            Box("Sidewalk right a", w, new Vector3(WX + GLASS, gy, -OUT), new Vector3(31.3f, t, 1.6f), stone);
            Box("Sidewalk right b", w, new Vector3(WX + GLASS, gy, 6.4f), new Vector3(31.3f, t, 10.5f), stone);
            Box("Sidewalk to the back gate", w, new Vector3(25.2f, gy, -5f), new Vector3(27.0f, t, -1.5f), stone);
            Box("Sidewalk to the front gate", w, new Vector3(3.2f, gy, 10.5f), new Vector3(4.8f, t, FZ1), stone);
            Box("Front gate pad", w, new Vector3(2.7f, gy, 21.6f), new Vector3(5.3f, t, FZ1), stone);
        }

        /// <summary>A paved sidewalk right round the outside of the hedges, joined to the road's sidewalk, with strips through the hedge gaps to both gates.</summary>
        static void OuterSidewalk(Transform g, float gy)
        {
            var w = Group("Outer sidewalk", g);
            float t = gy + 0.06f, a = 1.05f, b = 2.35f;   // band from 1.05 to 2.35 m outside the fence
            float xr = 35.5f;                              // where the road's own sidewalk starts
            Box("Sidewalk front", w, new Vector3(FX0 - b, gy, FZ1 + a), new Vector3(xr, t, FZ1 + b), stone);
            Box("Sidewalk back", w, new Vector3(FX0 - b, gy, FZ0 - b), new Vector3(xr, t, FZ0 - a), stone);
            Box("Sidewalk west", w, new Vector3(FX0 - b, gy, FZ0 - a), new Vector3(FX0 - a, t, FZ1 + a), stone);
            Box("Sidewalk east a", w, new Vector3(FX1 + a, gy, FZ0 - a), new Vector3(xr, t, 1.3f), stone);
            Box("Sidewalk east b", w, new Vector3(FX1 + a, gy, 6.7f), new Vector3(xr, t, FZ1 + a), stone);
            Box("Front gate strip", w, new Vector3(3.2f, gy, FZ1), new Vector3(4.8f, t, FZ1 + a), stone);
            Box("Back gate strip", w, new Vector3(25.2f, gy, FZ0 - a), new Vector3(27.0f, t, FZ0), stone);
        }

        static void BackPath(Transform g, float gy)
        {
            var path = Group("Back path", g);
            float gx = 26.1f;
            for (float z = -4.9f; z < -1.0f; z += 1.05f)
                Box("Path stone", path, new Vector3(gx - 0.5f, gy, z), new Vector3(gx + 0.5f, gy + 0.04f, z + 0.72f), stone);
            for (float x = gx - 1.3f; x > 1.6f; x -= 1.3f)
                Box("Path stone", path, new Vector3(x - 1.0f, gy, -1.7f), new Vector3(x, gy + 0.04f, -1.0f), stone);
            Box("Gate pad", path, new Vector3(gx - 1.2f, gy, -5.2f), new Vector3(gx + 1.2f, gy + 0.03f, -4.8f), stone);
        }

        static int bulbCounter;

        /// <summary>Bushes all round the outside of the fence, left open at the gates and the garage lane.</summary>
        static void Hedges(Transform g, float gy)
        {
            var root = Group("Hedges", g);
            var rnd = new System.Random(17);
            var leafMat = Tn("Hedge", "leafy_grass", MaterialLibrary.Mapping.Triplanar, new Color(0.18f, 0.36f, 0.14f), new Vector2(0f, 0.3f), 1f, 0f, 0.5f);
            var flowers = new[] { Pl("HedgeFlowerPink", new Color(0.95f, 0.5f, 0.65f), 0.35f), Pl("HedgeFlowerWhite", new Color(0.96f, 0.96f, 0.93f), 0.35f) };
            // a clipped, rectangular hedge: one long box per run between the gaps, with a slightly narrower top layer
            void Run(bool alongX, float fixedC, float a, float b)
            {
                const float w = 0.75f, h = 0.9f;
                Vector3 P(float u, float y, float d) => alongX ? new Vector3(u, y, d) : new Vector3(d, y, u);
                var main = Box("Hedge", root, P(a, gy, fixedC - w * 0.5f), P(b, gy + h, fixedC + w * 0.5f), leafMat);
                Object.DestroyImmediate(main.GetComponent<Collider>());
                var top = Box("Hedge top", root, P(a + 0.05f, gy + h, fixedC - w * 0.5f + 0.06f), P(b - 0.05f, gy + h + 0.06f, fixedC + w * 0.5f - 0.06f), leafMat);
                Object.DestroyImmediate(top.GetComponent<Collider>());
                for (float u = a + 0.6f; u < b - 0.4f; u += 1.4f)
                    if (rnd.NextDouble() < 0.5)
                    {
                        var fl = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        fl.name = "Hedge flower";
                        fl.transform.SetParent(root, false);
                        fl.transform.position = alongX ? new Vector3(u, gy + h + 0.08f, fixedC) : new Vector3(fixedC, gy + h + 0.08f, u);
                        fl.transform.localScale = Vector3.one * 0.12f;
                        fl.GetComponent<Renderer>().sharedMaterial = flowers[rnd.Next(2)];
                        Object.DestroyImmediate(fl.GetComponent<Collider>());
                    }
            }
            void Row(bool alongX, float fixedC, float a, float b, Vector2[] gaps)
            {
                float cur = a;
                foreach (var gp in gaps) { if (gp.x > cur) Run(alongX, fixedC, cur, gp.x); cur = gp.y; }
                if (b > cur) Run(alongX, fixedC, cur, b);
            }
            Row(true, FZ1 + 0.55f, FX0 - 0.5f, FX1 + 0.6f, new[] { new Vector2(2.4f, 5.6f) });
            Row(true, FZ0 - 0.55f, FX0 - 0.5f, FX1 + 0.6f, new[] { new Vector2(24.5f, 27.7f) });
            Row(false, FX0 - 0.55f, FZ0 - 0.5f, FZ1 + 0.55f, new Vector2[0]);
            Row(false, FX1 + 0.55f, FZ0 - 0.5f, FZ1 + 0.55f, new[] { new Vector2(0.7f, 7.5f) });
        }

        /// <summary>A modern wall light: black plate with two slits that glow up and down, and a soft light. Hangs on the outer wall.</summary>
        static void WallSconce(Transform parent, string wallPath, WallCutaway.Axis axis, float faceC, float outward, float along, float y)
        {
            var gr = Group("Wall light", parent);
            Vector3 P(float a, float yy, float d) => axis == WallCutaway.Axis.Z ? new Vector3(a, yy, d) : new Vector3(d, yy, a);
            float d0 = faceC, d1 = faceC + outward * 0.09f;
            float lo = Mathf.Min(d0, d1), hi = Mathf.Max(d0, d1);
            Box("Plate", gr, P(along - 0.07f, y - 0.16f, lo), P(along + 0.07f, y + 0.16f, hi), steel);
            foreach (float dy in new[] { 0.135f, -0.155f })
            {
                float o1 = faceC + outward * 0.093f, lo2 = Mathf.Min(d0 + (o1 - d0) * 0.6f, o1), hi2 = Mathf.Max(d0 + (o1 - d0) * 0.6f, o1);
                var slit = Box("Glow", gr, P(along - 0.05f, y + dy, lo2), P(along + 0.05f, y + dy + 0.02f, hi2), ledWarm, false);
                Object.DestroyImmediate(slit.GetComponent<Collider>());
                Glow(slit, new Color(1f, 0.72f, 0.4f) * 18f);
            }
            AddNightLight(gr.gameObject, P(along, y - 0.2f, faceC + outward * 0.25f), new Color(1f, 0.75f, 0.45f), 240f, 4f, 0f, new Vector2(0.25f, 0.25f), 90f);
            AttachTo(wallPath, gr.gameObject);
        }

        static void BuildWallLights(Transform house, Transform upperFloor)
        {
            var gg = Group("Outer wall lights", house);
            var gu = Group("Outer wall lights upper", upperFloor);   // inside the upper floor, so they show with it
            const string gw = "House/Ground floor/Walls/", uw = "House/Upper floor/Walls/";
            foreach (float x in new[] { 0.7f, 7.6f, 14.0f, 17.2f, 22.0f, 25.6f, 29.0f })
                WallSconce(gg, gw + "Back wall", WallCutaway.Axis.Z, -OUT, -1f, x, 1.8f);
            foreach (float x in new[] { 0.7f, 5.8f, 8.2f, 13.6f, 16.5f, 21.9f, 27.8f })
                WallSconce(gu, uw + "Back wall", WallCutaway.Axis.Z, -OUT, -1f, x, UPY + 1.8f);
            foreach (float z in new[] { 4.0f, 7.2f })
                WallSconce(gg, gw + "Left wall", WallCutaway.Axis.X, -OUT, -1f, z, 1.8f);
            foreach (float z in new[] { 0.5f, 3.2f, 7.6f })
                WallSconce(gu, uw + "Left wall", WallCutaway.Axis.X, -OUT, -1f, z, UPY + 1.8f);
        }

        /// <summary>Bulb style vibe lights along the shed roof edges.</summary>
        static void ShedLights(Vector3 at)
        {
            var root = new GameObject("Shed string lights").transform;
            var furn = GameObject.Find("Furniture");
            if (furn) root.SetParent(furn.transform, false);
            float y = at.y + 2.4f, xl = at.x - 1.78f, xr = at.x + 1.78f, zf = at.z + 1.5f, zb = at.z - 1.35f;
            BulbLine(root, new Vector3(xl, y, zf), new Vector3(xr, y, zf), 0.14f, 5);
            BulbLine(root, new Vector3(xl, y, zb), new Vector3(xl, y, zf), 0.12f, 6);
            BulbLine(root, new Vector3(xr, y, zb), new Vector3(xr, y, zf), 0.12f, 6);
            BulbLine(root, new Vector3(xl, y, zb), new Vector3(xr, y, zb), 0.14f, 6);
        }

        /// <summary>A sagging cable of small warm bulbs; every lightEvery-th bulb also throws a little real light.</summary>
        static void BulbLine(Transform root, Vector3 a, Vector3 b, float sag, int lightEvery)
        {
            int n = Mathf.Max(6, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.3f));
            Vector3 prev = a;
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n;
                var pt = Vector3.Lerp(a, b, t);
                pt.y -= sag * 4f * t * (1f - t);
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seg.name = "Wire";
                seg.transform.SetParent(root, false);
                seg.transform.position = (prev + pt) * 0.5f;
                seg.transform.up = (pt - prev).normalized;
                seg.transform.localScale = new Vector3(0.01f, (pt - prev).magnitude * 0.5f, 0.01f);
                seg.GetComponent<Renderer>().sharedMaterial = steel;
                Object.DestroyImmediate(seg.GetComponent<Collider>());
                if (i < n)
                {
                    var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bulb.name = "Bulb";
                    bulb.transform.SetParent(root, false);
                    bulb.transform.position = pt + Vector3.down * 0.05f;
                    bulb.transform.localScale = Vector3.one * 0.07f;
                    bulb.GetComponent<Renderer>().sharedMaterial = bulbGlow;
                    Object.DestroyImmediate(bulb.GetComponent<Collider>());
                    Glow(bulb, new Color(1f, 0.75f, 0.4f) * 6f);
                    if (lightEvery > 0 && ++bulbCounter % lightEvery == 0) AddNightLight(bulb, bulb.transform.position, new Color(1f, 0.75f, 0.45f), 70f, 2.6f, 0f);
                }
                prev = pt;
            }
        }

        /// <summary>The shed's own door: one walnut leaf on hinges that swings out, same hover and pet behaviour as the other doors.</summary>
        static void ShedDoor(Vector3 at, Transform shed)
        {
            // the model's opening is x 0.35 to 1.35 (Blender), which arrives mirrored: local -1.35 to -0.35, front face at local z +1.2
            var root = Group("Shed door", shed.parent);
            float hx = at.x - 1.35f, w = 1.0f, fz = at.z + 1.2f, y0 = at.y + 0.12f, h = 2.03f;
            var hinge = Group("Leaf", root);
            hinge.position = new Vector3(hx, 0f, fz);
            Box("Slab", hinge, new Vector3(hx + 0.01f, y0, fz - 0.02f), new Vector3(hx + w - 0.01f, y0 + h, fz + 0.03f), doorWood);
            for (int i = 1; i < 5; i++)
                Box($"Groove {i}", hinge, new Vector3(hx + i * w / 5f - 0.004f, y0 + 0.1f, fz + 0.029f), new Vector3(hx + i * w / 5f + 0.004f, y0 + h - 0.1f, fz + 0.032f), steel, false);
            Box("Handle", hinge, new Vector3(hx + w - 0.15f, y0 + 0.9f, fz + 0.03f), new Vector3(hx + w - 0.12f, y0 + 1.4f, fz + 0.07f), steel);
            var door = root.gameObject.AddComponent<HingedDoor>();
            door.leaves = new[] { hinge };
            door.angles = new[] { -100f };
            door.openSeconds = 0.9f;
            door.sensorCenter = new Vector3(hx + w * 0.5f, at.y + 1.1f, fz + 0.4f);
            door.sensorSize = new Vector3(2.4f, 2.2f, 2.4f);
        }

        /// <summary>A modern sliding gate across the garage lane: walnut slats in a steel frame that runs along the inside of the fence.</summary>
        static void LaneSlidingGate(Transform parent, float gy)
        {
            var root = Group("Lane sliding gate", parent);
            var panel = Group("Panel", root);
            float x0 = FX1 - 0.34f, x1 = FX1 - 0.28f, z0 = 1.15f, z1 = 7.05f, h = 1.8f;
            Box("Frame bottom", panel, new Vector3(x0, gy + 0.12f, z0), new Vector3(x1 + 0.02f, gy + 0.2f, z1), steel);
            Box("Frame top", panel, new Vector3(x0, gy + h - 0.08f, z0), new Vector3(x1 + 0.02f, gy + h, z1), steel);
            Box("Frame front", panel, new Vector3(x0, gy + 0.12f, z0), new Vector3(x1 + 0.02f, gy + h, z0 + 0.08f), steel);
            Box("Frame back", panel, new Vector3(x0, gy + 0.12f, z1 - 0.08f), new Vector3(x1 + 0.02f, gy + h, z1), steel);
            int slats = 9;
            float sh = (h - 0.28f) / slats;
            for (int i = 0; i < slats; i++)
                Box($"Slat {i}", panel, new Vector3(x0 + 0.01f, gy + 0.2f + i * sh + 0.015f, z0 + 0.08f), new Vector3(x1, gy + 0.2f + (i + 1) * sh - 0.015f, z1 - 0.08f), doorWood);
            Box("Handle", panel, new Vector3(x0 - 0.05f, gy + 0.9f, z0 + 0.3f), new Vector3(x0 - 0.02f, gy + 1.4f, z0 + 0.34f), steel);
            Box("Track", root, new Vector3(x0 - 0.02f, gy, -4.6f), new Vector3(x1 + 0.04f, gy + 0.04f, z1), steel, false);
            var door = root.gameObject.AddComponent<SlidingDoor>();
            door.panel = panel;
            door.slide = new Vector3(0f, 0f, -5.9f);
            door.openSeconds = 2.2f;
            door.stayOpenSeconds = 3f;
            door.sensorCenter = new Vector3(FX1, gy + 1.1f, 4f);
            door.sensorSize = new Vector3(6f, 2.2f, 7.5f);
            AttachTo("Garden/Fence/East fence", root.gameObject);
        }

        /// <summary>Sets of vertical wooden beams a little off the outer wall (like the reference photo): black rails top and bottom.</summary>
        static void VerticalBeams(Transform parent, string wallPath, WallCutaway.Axis axis, float faceC, float outward, float a, float b, float y0, float y1)
        {
            var g = Group("Vertical beams", parent);
            Vector3 P(float along, float y, float depth) => axis == WallCutaway.Axis.Z ? new Vector3(along, y, depth) : new Vector3(depth, y, along);
            float off = 0.14f;
            float d0 = faceC + outward * off, d1 = faceC + outward * (off + 0.1f);
            int n = Mathf.Max(3, Mathf.RoundToInt((b - a) / 0.17f));
            float pitch = (b - a) / n;
            for (int i = 0; i < n; i++)
            {
                float u = a + i * pitch + (pitch - 0.07f) * 0.5f;
                Box($"Beam {i}", g, P(u, y0, Mathf.Min(d0, d1)), P(u + 0.07f, y1, Mathf.Max(d0, d1)), doorWood);
            }
            Box("Rail bottom", g, P(a - 0.03f, y0, Mathf.Min(faceC + outward * 0.02f, d0)), P(b, y0 + 0.05f, Mathf.Max(faceC + outward * 0.02f, d0)), steel);
            Box("Rail top", g, P(a - 0.03f, y1 - 0.05f, Mathf.Min(faceC + outward * 0.02f, d0)), P(b, y1, Mathf.Max(faceC + outward * 0.02f, d0)), steel);
            AttachTo(wallPath, g.gameObject);
        }

        static void BuildBeams(Transform house, Transform upperFloor)
        {
            var g = Group("Wall beams", house);
            var gu = Group("Wall beams upper", upperFloor);   // inside the upper floor, so they hide with it
            const string gw = "House/Ground floor/Walls/", uw = "House/Upper floor/Walls/";
            float face = -OUT, ul = UPY + 0.08f, uh = UPY + 2.95f;
            foreach (var r in new[] { new Vector2(3f, 5f), new Vector2(9f, 13f), new Vector2(22.2f, 25f) })
                VerticalBeams(g, gw + "Back wall", WallCutaway.Axis.Z, face, -1f, r.x, r.y, 0.08f, 2.95f);
            foreach (var r in new[] { new Vector2(2f, 5f), new Vector2(9f, 13f), new Vector2(17f, 21f), new Vector2(23f, 27f) })
                VerticalBeams(gu, uw + "Back wall", WallCutaway.Axis.Z, face, -1f, r.x, r.y, ul, uh);
            VerticalBeams(g, gw + "Left wall", WallCutaway.Axis.X, face, -1f, 0.8f, 3.6f, 0.08f, 2.95f);
            VerticalBeams(gu, uw + "Left wall", WallCutaway.Axis.X, face, -1f, 4f, 7.4f, ul, uh);
        }

        static void Glow(GameObject g, Color emission)
        {
            var ng = g.AddComponent<NightGlow>();
            ng.emission = emission;
        }

        /// <summary>Festoon string lights over the BBQ and dining corner: posts, sagging cables, warm bulbs, a few real lights.</summary>
        static void StringLights(Transform g, float gy)
        {
            var root = Group("String lights", g);
            float ph = 2.7f;
            var posts = new[] { new Vector3(27.2f, 0f, 12.6f), new Vector3(33.4f, 0f, 12.6f), new Vector3(33.4f, 0f, 21.2f), new Vector3(27.2f, 0f, 21.2f), new Vector3(27.2f, 0f, 17f), new Vector3(33.4f, 0f, 17f) };
            foreach (var p in posts)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "String light post";
                post.transform.SetParent(root, false);
                post.transform.position = new Vector3(p.x, gy + ph * 0.5f, p.z);
                post.transform.localScale = new Vector3(0.09f, ph * 0.5f, 0.09f);
                post.GetComponent<Renderer>().sharedMaterial = steel;
                var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = "Post cap";
                cap.transform.SetParent(root, false);
                cap.transform.position = new Vector3(p.x, gy + ph + 0.02f, p.z);
                cap.transform.localScale = Vector3.one * 0.11f;
                cap.GetComponent<Renderer>().sharedMaterial = steel;
                Object.DestroyImmediate(cap.GetComponent<Collider>());
            }
            int lit = 0;
            void Cable(Vector3 a, Vector3 b, float sag)
            {
                int n = Mathf.Max(6, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.42f));
                Vector3 prev = a;
                for (int i = 1; i <= n; i++)
                {
                    float t = i / (float)n;
                    var pt = Vector3.Lerp(a, b, t);
                    pt.y -= sag * 4f * t * (1f - t);
                    var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    seg.name = "Wire";
                    seg.transform.SetParent(root, false);
                    seg.transform.position = (prev + pt) * 0.5f;
                    seg.transform.up = (pt - prev).normalized;
                    seg.transform.localScale = new Vector3(0.012f, (pt - prev).magnitude * 0.5f, 0.012f);
                    seg.GetComponent<Renderer>().sharedMaterial = steel;
                    Object.DestroyImmediate(seg.GetComponent<Collider>());
                    if (i < n)
                    {
                        var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        bulb.name = "Bulb";
                        bulb.transform.SetParent(root, false);
                        bulb.transform.position = pt + Vector3.down * 0.06f;
                        bulb.transform.localScale = Vector3.one * 0.075f;
                        bulb.GetComponent<Renderer>().sharedMaterial = bulbGlow;
                        Object.DestroyImmediate(bulb.GetComponent<Collider>());
                        Glow(bulb, new Color(1f, 0.75f, 0.4f) * 6f);
                        if (++lit % 5 == 0) AddNightLight(bulb, bulb.transform.position, new Color(1f, 0.75f, 0.45f), 90f, 3.2f, 0f);
                    }
                    prev = pt;
                }
            }
            Vector3 Top(Vector3 p) => new Vector3(p.x, gy + ph, p.z);
            // a frame around the corner and a cross over the dining table, plus strings back to the roof edge
            Cable(Top(posts[0]), Top(posts[1]), 0.35f);
            Cable(Top(posts[3]), Top(posts[2]), 0.35f);
            Cable(Top(posts[0]), Top(posts[4]), 0.3f);
            Cable(Top(posts[4]), Top(posts[3]), 0.3f);
            Cable(Top(posts[1]), Top(posts[5]), 0.3f);
            Cable(Top(posts[5]), Top(posts[2]), 0.3f);
            Cable(Top(posts[4]), Top(posts[5]), 0.4f);
            float roofY = UPY + H - 0.05f;
            var groundRoot = root;
            root = Group("Strings to the roof edge", roofRoot);   // these hang from the roof, so they hide with it
            Cable(new Vector3(27.2f, roofY, WD + 1.55f), Top(posts[0]), 0.9f);
            Cable(new Vector3(WX + 0.55f, roofY, WD + 1.55f), Top(posts[1]), 0.9f);   // the roof ends at x 30.7, so this one angles to its corner
            root = groundRoot;
        }

        /// <summary>Modern LED strips that outline the house at night: roof edge, deck edge, upper slab, stairs, pool rim.</summary>
        static void LedStrips(Transform g, float gy, float px0, float px1, float pz0, float pz1)
        {
            var root = Group("LED strips", g);
            void Strip(string nm, Vector3 a, Vector3 b, Material m, Color emit, Transform into = null)
            {
                // strips are at least 5 cm thick so they read from a distance
                Vector3 mid = (a + b) * 0.5f, size = b - a;
                for (int k = 0; k < 3; k++) if (size[k] < 0.05f) size[k] = 0.05f;
                var s = Box(nm, into ? into : root, mid - size * 0.5f, mid + size * 0.5f, m, false);
                Object.DestroyImmediate(s.GetComponent<Collider>());
                Glow(s, emit);
            }
            var warm = new Color(1f, 0.72f, 0.4f) * 18f;
            var cool = new Color(0.55f, 0.82f, 1f) * 18f;
            float ry = UPY + H;
            // under the roof edge: front, and the two short sides
            Strip("Roof edge front", new Vector3(-0.6f, ry - 0.06f, WD + 1.5f), new Vector3(WX + 0.6f, ry - 0.03f, WD + 1.53f), ledWarm, warm, roofRoot);
            Strip("Roof edge left", new Vector3(-0.6f, ry - 0.06f, -0.6f), new Vector3(-0.57f, ry - 0.03f, WD + 1.5f), ledWarm, warm, roofRoot);
            Strip("Roof edge right", new Vector3(WX + 0.57f, ry - 0.06f, -0.6f), new Vector3(WX + 0.6f, ry - 0.03f, WD + 1.5f), ledWarm, warm, roofRoot);
            // under the lip of the upper floor slab, over the ground floor glass
            Strip("Upper slab", new Vector3(0f, UPY - SLAB - 0.03f, WD + GLASS + 0.005f), new Vector3(WX, UPY - SLAB, WD + GLASS + 0.035f), ledCool, cool, upperRoot);
            // under the deck edge, and along the slab base
            Strip("Deck edge", new Vector3(0f, -0.14f, 10.47f), new Vector3(WX + GLASS, -0.11f, 10.5f), ledWarm, warm);
            Strip("Slab base left", new Vector3(-OUT - 0.02f, -0.03f, -OUT), new Vector3(-OUT, 0f, WD + GLASS), ledCool, cool);
            // stair treads (the stairs are built in BuildGround, these follow them)
            const int steps = 14;
            float rise = UPY / (steps + 1), run = 4f / steps;
            for (int i = 0; i < steps; i++)
            {
                float top = (i + 1) * rise, z1 = 6f - i * run;
                Strip($"Step light {i + 1}", new Vector3(14.22f, top - 0.1f, z1 - 0.03f), new Vector3(15.78f, top - 0.07f, z1), ledWarm, warm);
            }
            // round the pool, just under the coping
            float py = gy - 0.16f;
            Strip("Pool rim near", new Vector3(px0, py, pz0), new Vector3(px1, py + 0.03f, pz0 + 0.02f), ledCool, cool);
            Strip("Pool rim far", new Vector3(px0, py, pz1 - 0.02f), new Vector3(px1, py + 0.03f, pz1), ledCool, cool);
            Strip("Pool rim left", new Vector3(px0, py, pz0), new Vector3(px0 + 0.02f, py + 0.03f, pz1), ledCool, cool);
            Strip("Pool rim right", new Vector3(px1 - 0.02f, py, pz0), new Vector3(px1, py + 0.03f, pz1), ledCool, cool);
            // a few soft area lights washing the deck from the roof edge
            for (int i = 0; i < 5; i++)
                AddNightLight(roofRoot.gameObject, new Vector3(3f + i * 6f, ry - 0.08f, WD + 1.4f), new Color(1f, 0.75f, 0.5f), 420f, 6f, 0f, new Vector2(1.2f, 0.25f), 90f);
        }

        /// <summary>A zig-zag (bi-fold) glass door in the garage's front glass wall.</summary>
        static void GarageFoldingDoor(Transform parent, string wallPath, float a, float b)
        {
            var root = Group("Garage folding glass door", parent);
            float zc = WD + GLASS * 0.5f;
            const int n = 6;
            float w = (b - a) / n;
            var panels = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                bool left = i < n / 2;
                var pnl = Group($"Panel {i + 1}", root);
                float x0 = left ? 0f : -w, x1 = left ? w : 0f;
                Vector3 P(float x, float y, float dz) => new Vector3(x, y, dz);
                Box("Glass", pnl, P(x0, 0.03f, -0.006f), P(x1, DOOR_H, 0.006f), glass, false);
                const float f = 0.04f;
                Box("Frame bottom", pnl, P(x0, 0.02f, -0.02f), P(x1, 0.02f + f, 0.02f), steel);
                Box("Frame top", pnl, P(x0, DOOR_H - f, -0.02f), P(x1, DOOR_H, 0.02f), steel);
                Box("Frame near", pnl, P(x0, 0.02f, -0.02f), P(x0 + f, DOOR_H, 0.02f), steel);
                Box("Frame far", pnl, P(x1 - f, 0.02f, -0.02f), P(x1, DOOR_H, 0.02f), steel);
                Box("Hinge stile", pnl, P(left ? x0 - 0.01f : x1 - 0.03f, 0.02f, -0.03f), P(left ? x0 + 0.03f : x1 + 0.01f, DOOR_H, 0.03f), steel);
                panels[i] = pnl;
            }
            Box("Header rail", root, new Vector3(a - 0.05f, DOOR_H, zc - 0.05f), new Vector3(b + 0.05f, DOOR_H + 0.06f, zc + 0.05f), steel);
            Box("Threshold", root, new Vector3(a - 0.05f, 0f, zc - 0.06f), new Vector3(b + 0.05f, 0.02f, zc + 0.06f), steel);
            var door = root.gameObject.AddComponent<FoldingDoor>();
            door.panels = panels;
            door.leftHinge = new Vector3(a, 0f, zc);
            door.rightHinge = new Vector3(b, 0f, zc);
            door.along = Vector3.right;
            door.panelWidth = w;
            door.leftCount = n / 2;
            door.openSeconds = 1.2f;
            door.stayOpenSeconds = 2.2f;
            door.sensorCenter = new Vector3((a + b) * 0.5f, 1.1f, zc + 0.3f);
            door.sensorSize = new Vector3((b - a) + 1.0f, 2.2f, 3.6f);
            door.Refresh();
            AttachTo(wallPath, root.gameObject);
        }

        static void Tree(Transform parent, Vector3 at, float size)
        {
            var t = Group("Tree", parent);
            var trunkGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunkGo.name = "Trunk";
            trunkGo.transform.SetParent(t, false);
            trunkGo.transform.position = at + Vector3.up * size;
            trunkGo.transform.localScale = new Vector3(0.25f * size, size, 0.25f * size);
            trunkGo.GetComponent<Renderer>().sharedMaterial = trunk;
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Leaves";
            crown.transform.SetParent(t, false);
            crown.transform.position = at + Vector3.up * size * 2.3f;
            crown.transform.localScale = Vector3.one * size * 2f;
            crown.GetComponent<Renderer>().sharedMaterial = leaf;
        }

        // ---------- furniture ----------

        /// <summary>
        /// Default furniture: model id (file name in Assets/Art/Models), centre x and z in metres,
        /// rotation in degrees (0 = the front faces the garden, +Z; 180 = faces the back wall) and floor.
        /// Blender models face -Y, which arrives in Unity facing +Z.
        /// </summary>
        static readonly (string id, float x, float z, float rot, int floor)[] Layout =
        {
            ("geomrug", 4f, 4.3f, 0f, 0),
            // living room, v0.22: a TV console facing the sofa, book cases along the west wall, floor lamps and a bean bag
            ("tvunit", 4.0f, 6.2f, 180f, 0),
            ("bookcase", 0.22f, 1.6f, 90f, 0),
            ("bookcase", 0.22f, 5.6f, 90f, 0),
            ("uplight", 2.1f, 2.2f, 0f, 0),
            ("uplight", 7.0f, 3.6f, 0f, 0),
            ("beanbag", 1.4f, 4.9f, 25f, 0),
            ("sofa", 4f, 3.1f, 0f, 0),
            ("marbletable", 4f, 4.6f, 0f, 0),
            // kitchen (x 8 to 14): counter run on the back wall, fridge beside it, island, stools, dining set
            ("kitchenrun", 11.62f, 0.34f, 0f, 0),
            ("fridge", 8.98f, 0.4f, 0f, 0),
            ("kitchenisland", 11.6f, 3.7f, 0f, 0),
            ("barstool", 10.6f, 4.55f, 20f, 0),
            ("barstool", 11.6f, 4.55f, -10f, 0),
            ("barstool", 12.6f, 4.55f, 5f, 0),
            ("diningtable", 10.9f, 6.5f, 0f, 0),
            ("diningchair", 10.3f, 5.75f, 0f, 0),
            ("diningchair", 11.5f, 5.75f, 0f, 0),
            ("diningchair", 10.3f, 7.25f, 180f, 0),
            ("diningchair", 11.5f, 7.25f, 180f, 0),
            ("pendant", 10.5f, 3.7f, 0f, 0),
            ("pendant", 11.6f, 3.7f, 0f, 0),
            ("pendant", 12.7f, 3.7f, 0f, 0),
            // bathroom (x 16 to 22): shower, vanity and toilet along the back wall, tub in the middle, laundry on the right
            ("shower", 16.72f, 0.62f, 0f, 0),
            ("vanity", 18.75f, 0.27f, 0f, 0),
            ("bathmirror", 18.75f, 0.04f, 0f, 0),
            ("toilet", 20.1f, 0.33f, 0f, 0),
            ("towelrack", 21.35f, 0.08f, 0f, 0),
            ("bathmat", 18.75f, 1.05f, 0f, 0),
            ("bathtub", 20.4f, 2.0f, 0f, 0),
            ("washer", 21.6f, 5.4f, 270f, 0),
            ("dryer", 21.6f, 6.1f, 270f, 0),
            ("basket", 20.8f, 6.9f, 0f, 0),
            // garage (x 22 to 30): two cars nose to the garden, tools along the back wall
            ("sedan", 26.2f, 3.05f, 90f, 0),
            ("mpv", 26.0f, 5.3f, 90f, 0),
            ("evcharger", 25.9f, 0.12f, 0f, 0),
            ("workbench", 28.9f, 0.34f, 0f, 0),
            ("garageshelf", 23.1f, 0.24f, 0f, 0),
            ("toolchest", 29.5f, 1.4f, 270f, 0),
            ("bicycle", 22.65f, 3.9f, 0f, 0),
            // planters under the money trees (the trees stand on their soil, 0.38 m up)
            ("planter", 0.75f, 0.8f, 0f, 0),
            ("planter", 16.75f, 5f, 0f, 0),
            ("planter", 7.3f, 7.2f, 0f, 1),
            ("planter", 21.3f, 2.6f, 0f, 1),
            ("planter", 15f, 0.7f, 0f, 1),
            // upper floor, Teacher's Room (x 0 to 8)
            ("platformbed", 5f, 1.17f, 0f, 1),
            ("nightstand", 3.9f, 0.22f, 0f, 1),
            ("nightstand", 6.1f, 0.22f, 0f, 1),
            ("wardrobe", 1f, 0.32f, 0f, 1),
            ("bookcase", 0.22f, 3.3f, 90f, 1),
            ("geomrug", 5f, 3.6f, 0f, 1),
            ("teacherdesk", 0.4f, 6f, 90f, 1),
            ("officechair", 1.2f, 6f, 270f, 1),
            // Office & Library (x 8 to 14)
            ("bookcase", 8.75f, 0.22f, 0f, 1),
            ("bookcase", 10f, 0.22f, 0f, 1),
            ("officedesk", 10.4f, 4.2f, 0f, 1),
            ("officechair", 10.4f, 5.05f, 180f, 1),
            ("officechair", 11.6f, 5f, 170f, 1),
            ("filecabinet", 13.55f, 2.2f, 270f, 1),
            ("uplight", 13.3f, 7.3f, 0f, 1),
            // Engineer's Room (x 16 to 22)
            ("platformbed_e", 20.2f, 1.17f, 0f, 1),
            ("nightstand", 18.9f, 0.22f, 0f, 1),
            ("wardrobe", 17.1f, 0.32f, 0f, 1),
            ("ebench", 16.45f, 5f, 90f, 1),
            ("officechair", 17.3f, 5f, 270f, 1),
            ("robotarm", 18.7f, 3.2f, 0f, 1),
            ("printer3d", 21f, 6.8f, 0f, 1),
            ("beanbag", 20.4f, 4.6f, 20f, 1),
            ("geomrug", 19.6f, 3.9f, 0f, 1),
            // Gym (x 22 to 30)
            ("treadmill", 23.3f, 0.93f, 0f, 1),
            ("treadmill", 25.2f, 0.93f, 0f, 1),
            ("dumbbells", 27.7f, 0.24f, 0f, 1),
            ("weightbench", 24f, 4.2f, 0f, 1),
            ("spinbike", 27.4f, 3.3f, 0f, 1),
            ("spinbike", 29f, 3.3f, 0f, 1),
            ("punchbag", 26.4f, 5.8f, 0f, 1),
            ("yogamat", 23.3f, 6.5f, 0f, 1),
            ("yogamat", 24.2f, 6.5f, 0f, 1),
            ("waterdispenser", 28.4f, 7.3f, 180f, 1),
        };

        /// <summary>Small things standing on surfaces: model id, x, y (height of the surface in metres), z and rotation.</summary>
        static readonly (string id, float x, float y, float z, float rot)[] Tabletop =
        {
            // kitchen island top is 0.94 m, counter top 0.92 m
            ("fruitbowl", 10.55f, 0.94f, 3.6f, 0f),
            ("cuttingboard", 12.4f, 0.94f, 3.55f, 12f),
            ("mug", 11.75f, 0.94f, 3.35f, 0f),
            ("mug", 11.95f, 0.94f, 3.5f, 70f),
            ("espresso", 13.35f, 0.92f, 0.33f, 0f),
            ("utensils", 11.45f, 0.92f, 0.3f, 0f),
            ("herbs", 11.8f, 0.92f, 0.32f, 20f),
            // bathroom: vanity top is 0.84 m
            ("towelstack", 21.6f, 0.87f, 6.1f, 90f),
            ("candles", 19.45f, 0.02f, 2.9f, 0f),
            ("candles", 2.45f, 0.47f, 2.95f, 0f),
            // garage
            ("cardboardboxes", 29.4f, 0.02f, 7.2f, 10f),
            ("paintcans", 23.15f, 0.02f, 1.0f, 0f),
            ("sparetyres", 22.6f, 0.02f, 6.9f, 0f),
            // upper floor surfaces (floor top is 3.32 m): nightstands 0.52, desks 0.75
            ("bedlamp", 3.9f, 3.84f, 0.22f, 0f),
            ("bedlamp", 6.1f, 3.84f, 0.22f, 0f),
            ("bedlamp", 18.9f, 3.84f, 0.22f, 0f),
            ("globe", 0.5f, 4.07f, 5.6f, 0f),
            ("mug", 10.9f, 4.07f, 4.3f, 0f),
            // front yard (garden ground is -0.3, the deck -0.06), like the yard of the 2D Tiramisu App
            ("fountain", 10.5f, -0.3f, 15.5f, 0f),
            ("gardenbench", 8.2f, -0.3f, 14.0f, 200f),
            ("gnome", 7.2f, -0.3f, 16.4f, 160f),
            ("flamingo", 12.6f, -0.3f, 13.0f, 200f),
            ("mailbox", 5.5f, -0.3f, 17.4f, 0f),
            ("planterbox", 2.2f, -0.3f, 11.3f, 0f),
            ("planterbox", 11.6f, -0.3f, 11.3f, 0f),
            ("planterbox", 19.5f, -0.3f, 11.0f, 0f),
            ("planterbox", 24.5f, -0.3f, 11.0f, 0f),
            ("flowerbed", 9.5f, -0.3f, 19.8f, 0f),
            ("flowerbed", 21.5f, -0.3f, 19.8f, 0f),
            ("flowerbed", 27.0f, -0.3f, 19.8f, 0f),
            ("hammock", 14.0f, -0.3f, 19.7f, 90f),
            ("lounger", 16.7f, -0.3f, 13.0f, 90f),
            ("lounger", 16.7f, -0.3f, 15.0f, 90f),
            ("parasol", 15.4f, -0.3f, 14.0f, 0f),
            ("lounger", 27.3f, -0.3f, 13.8f, 270f),
            ("beachball", 22.0f, -0.5f, 14.0f, 0f),
            // back yard: a shed with tools, a path from the back gate, planters, a bench and lanterns
            ("shed", 1.4f, -0.3f, -2.9f, 0f),
            ("wheelbarrow", 4.3f, -0.3f, -2.3f, 25f),
            ("toolrack", -0.5f, -0.3f, -2.9f, 270f),
            ("logpile", -2.5f, -0.3f, -3.0f, 90f),
            ("wateringcan", 2.4f, -0.3f, -1.15f, 30f),
            ("hosereel", 4.3f, -0.3f, -4.5f, 0f),
            ("planterbox", 8.0f, -0.3f, -4.5f, 0f),
            ("planterbox", 12.0f, -0.3f, -4.5f, 0f),
            ("planterbox", 16.0f, -0.3f, -4.5f, 0f),
            ("gardenbench", 10.0f, -0.3f, -3.6f, 0f),
            ("flowerbed", 15.0f, -0.3f, -2.8f, 0f),
            ("flowerbed", 21.0f, -0.3f, -2.8f, 0f),
            ("lantern", 23.5f, -0.3f, -1.5f, 0f),
            ("lantern", 6.5f, -0.3f, -1.5f, 0f),
            ("lantern", 27.4f, -0.3f, -3.5f, 0f),
            // BBQ corner
            ("bbqcounter", 30.2f, -0.3f, 11.5f, 0f),
            ("bbq", 32.6f, -0.3f, 11.5f, 0f),
            ("cooler", 28.6f, -0.3f, 11.5f, 0f),
            ("telescope", 33.1f, -0.3f, 14.0f, 90f),
            // long dining and the fire pit lounge
            ("outdoorrug", 30.2f, -0.28f, 14.6f, 0f),
            ("longdining", 30.2f, -0.3f, 14.6f, 0f),
            ("outdoorrug", 30.2f, -0.28f, 18.4f, 0f),
            ("firepit", 30.2f, -0.3f, 18.2f, 0f),
            ("outdoorsectional", 30.2f, -0.3f, 20.6f, 180f),
            ("lantern", 32.7f, -0.3f, 17.4f, 0f),
            ("lantern", 27.7f, -0.3f, 17.4f, 0f),
            // things hung on upper walls
            ("chalkboard", 0.04f, 4.57f, 6f, 90f),
            ("worldmap", 5f, 4.72f, 0.03f, 0f),
            ("whiteboard", 12.4f, 4.32f, 0.02f, 0f),
            ("gymmirror", 23.6f, 4.32f, 0.02f, 0f),
            ("gymmirror", 25.6f, 4.32f, 0.02f, 0f),
            ("gymmirror", 27.6f, 4.32f, 0.02f, 0f),
        };

        static PropPlacer.Prop Pr(string id, string variant, float x, float y, float z, float rot, float scale,
            PropPlacer.Body body, float mass = 0f, float colliderHeight = 0f)
            => new PropPlacer.Prop { id = id, variant = variant, at = new Vector3(x, y, z), rot = rot, scale = scale, body = body, mass = mass, colliderHeight = colliderHeight };

        const float TableTop = 0.4f + FLOOR_TOP; // marble coffee table
        const float SideTop = 0.45f + FLOOR_TOP; // side_table_01

        /// <summary>Photoscanned props from Poly Haven (tools/fetch_models.py) in the living room.</summary>
        static readonly PropPlacer.Prop[] LivingProps =
        {
            Pr("modern_arm_chair_01", null, 6.5f, FLOOR_TOP, 4.5f, 250f, 1f, PropPlacer.Body.Dynamic, 18f),
            Pr("side_table_01", null, 5.55f, FLOOR_TOP, 2.95f, 0f, 1f, PropPlacer.Body.Dynamic, 6f),
            Pr("side_table_01", null, 2.45f, FLOOR_TOP, 2.95f, 0f, 1f, PropPlacer.Body.Dynamic, 6f),
            Pr("desk_lamp_arm_01", null, 5.6f, SideTop + 0.002f, 2.9f, 200f, 1f, PropPlacer.Body.Dynamic, 2.5f),
            Pr("book_encyclopedia_set_01", null, 3.95f, TableTop + 0.004f, 4.6f, 0f, 1f, PropPlacer.Body.DynamicParts, 0.7f),
            Pr("ceramic_vase_03", null, 4.45f, TableTop + 0.002f, 4.62f, 0f, 1f, PropPlacer.Body.Dynamic, 1.2f),
            Pr("potted_plant_01", null, 7.3f, FLOOR_TOP, 0.75f, 30f, 1.35f, PropPlacer.Body.Static, 0f, 0.55f),
            Pr("pachira_aquatica_01", "_d", 0.75f, FLOOR_TOP + 0.38f, 0.8f, 0f, 1f, PropPlacer.Body.Static, 0f, 0.5f),
            Pr("pachira_aquatica_01", "_d", 16.75f, FLOOR_TOP + 0.38f, 5.0f, 30f, 0.95f, PropPlacer.Body.Static, 0f, 0.5f),
        };

        const float UpFloor = UPY + FLOOR_TOP;

        /// <summary>Photoscanned props on the upper floor.</summary>
        static readonly PropPlacer.Prop[] UpperProps =
        {
            Pr("modern_arm_chair_01", null, 6.8f, UpFloor, 5.8f, 220f, 1f, PropPlacer.Body.Dynamic, 18f),
            Pr("side_table_01", null, 7.5f, UpFloor, 6.6f, 0f, 1f, PropPlacer.Body.Dynamic, 6f),
            Pr("pachira_aquatica_01", "_d", 7.3f, UpFloor + 0.38f, 7.2f, 0f, 1f, PropPlacer.Body.Static, 0f, 0.5f),
            Pr("potted_plant_01", null, 8.6f, UpFloor, 7.3f, 30f, 1.35f, PropPlacer.Body.Static, 0f, 0.55f),
            Pr("modern_arm_chair_01", null, 12.5f, UpFloor, 6.5f, 200f, 1f, PropPlacer.Body.Dynamic, 18f),
            Pr("pachira_aquatica_01", "_c", 21.3f, UpFloor + 0.38f, 2.6f, 60f, 1.35f, PropPlacer.Body.Static, 0f, 0.5f),
            Pr("potted_plant_01", null, 29.3f, UpFloor, 7.2f, 0f, 1.35f, PropPlacer.Body.Static, 0f, 0.55f),
            Pr("pachira_aquatica_01", "_a", 15f, UpFloor + 0.38f, 0.7f, 120f, 1.3f, PropPlacer.Body.Static, 0f, 0.5f),
        };

        /// <summary>Real trees and shrubs for the garden (garden ground sits at -SLAB).</summary>
        static readonly PropPlacer.Prop[] GardenProps =
        {
            Pr("island_tree_02", null, 1.5f, -SLAB, 20f, 0f, 1.5f, PropPlacer.Body.Static, 0f, 2f),
            Pr("island_tree_02", null, 30.5f, -SLAB, 19.5f, 140f, 1.35f, PropPlacer.Body.Static, 0f, 2f),
            Pr("searsia_lucida", "_a_LOD0", 11f, -SLAB, 20.5f, 60f, 1.3f, PropPlacer.Body.Static, 0f, 1.5f),
            Pr("searsia_lucida", "_b_LOD0", 32.5f, -SLAB, 12f, 200f, 1.3f, PropPlacer.Body.Static, 0f, 1.5f),
            Pr("searsia_lucida", "_c_LOD0", -1.8f, -SLAB, 13f, 20f, 1.3f, PropPlacer.Body.Static, 0f, 1.2f),
            Pr("shrub_02", "_b", 7.5f, -SLAB - 0.16f, 11.6f, 0f, 0.55f, PropPlacer.Body.Static, 0f, 0.6f),
            Pr("shrub_02", "_d", 13.5f, -SLAB - 0.16f, 11.5f, 90f, 0.5f, PropPlacer.Body.Static, 0f, 0.6f),
            Pr("shrub_02", "_a", 28.5f, -SLAB - 0.16f, 11.4f, 45f, 0.55f, PropPlacer.Body.Static, 0f, 0.6f),
            Pr("shrub_02", "_c", 17f, -SLAB - 0.16f, 19.5f, 0f, 0.5f, PropPlacer.Body.Static, 0f, 0.6f),
        };

        static readonly System.Collections.Generic.Dictionary<string, int> keyCount = new System.Collections.Generic.Dictionary<string, int>();

        /// <summary>Built in fittings and things hung on walls or ceilings stay where they are.</summary>
        static readonly System.Collections.Generic.HashSet<string> Pinned = new System.Collections.Generic.HashSet<string>
        {
            "kitchenrun", "shower", "toilet", "bathtub", "vanity", "pendant", "punchbag", "evcharger",
            "bathmirror", "worldmap", "chalkboard", "whiteboard", "gymmirror",
            "fountain", "mailbox", "bbqcounter", "flowerbed", "hammock", "shed",
        };

        static readonly System.Collections.Generic.HashSet<string> SmallIds = new System.Collections.Generic.HashSet<string>
        {
            "cushion", "fruitbowl", "cuttingboard", "mug", "espresso", "utensils", "herbs", "towelstack", "candles", "bedlamp", "globe",
            "book_encyclopedia_set_01", "ceramic_vase_03", "desk_lamp_arm_01",
        };

        static void MakeMovable(GameObject go, bool pinned)
        {
            if (!go) return;
            string id = go.name;
            keyCount.TryGetValue(id, out int n);
            keyCount[id] = n + 1;
            var f = go.AddComponent<Furniture>();
            f.key = $"{id}#{n}";
            f.pinned = pinned || Pinned.Contains(id);
            f.small = SmallIds.Contains(id.Split(' ')[0]);
        }

        /// <summary>Round fountain with real moving water: ripple surfaces in the basin and the two bowls, a crown of spray and two overflows.</summary>
        static void SetupFountain(Vector3 at)
        {
            var root = new GameObject("Fountain water").transform;
            root.SetParent(GameObject.Find("Furniture") ? GameObject.Find("Furniture").transform : null, false);
            PoolRipples Surface(string nm, float r, float y, float floor, float breeze)
            {
                var go = new GameObject(nm);
                go.transform.SetParent(root, false);
                var pr = go.AddComponent<PoolRipples>();
                pr.min = new Vector2(at.x - r, at.z - r);
                pr.max = new Vector2(at.x + r, at.z + r);
                pr.round = true;
                pr.buoyancy = false;
                pr.cell = r < 0.6f ? 0.04f : 0.06f;
                pr.surfaceY = at.y + y;
                pr.floorY = at.y + floor;
                pr.material = water;
                pr.breeze = breeze;
                pr.damping = 0.98f;
                return pr;
            }
            var basin = Surface("Basin water", 0.96f, 0.5f, 0.3f, 1.2f);
            var mid = Surface("Middle bowl water", 0.5f, 1.17f, 1.1f, 2f);
            var top = Surface("Top bowl water", 0.27f, 1.55f, 1.5f, 2f);
            void Fall(string nm, float ring, float startY, float endY, float drift, int n, PoolRipples target, float fall)
            {
                var go = new GameObject(nm);
                go.transform.SetParent(root, false);
                var fw = go.AddComponent<FallingWater>();
                fw.start = new Vector3(at.x, at.y + startY, at.z);
                fw.ringRadius = ring;
                fw.drift = drift;
                fw.endY = at.y + endY;
                fw.count = n;
                fw.fallSeconds = fall;
                fw.target = target;
                fw.splash = 0.3f;
                fw.material = dropMat;
                fw.streak = new Vector2(0.02f, 0.09f);
            }
            // vibe lights: a ring of bulbs round the basin rim and strings up to the top of the fountain
            var bulbs = new GameObject("Fountain bulbs").transform;
            bulbs.SetParent(root, false);
            var apex = new Vector3(at.x, at.y + 1.95f, at.z);
            for (int i = 0; i < 10; i++)
            {
                float an = i / 10f * Mathf.PI * 2f;
                BulbLine(bulbs, apex, new Vector3(at.x + Mathf.Cos(an) * 1.14f, at.y + 0.72f, at.z + Mathf.Sin(an) * 1.14f), 0.16f, 7);
            }
            for (int i = 0; i < 30; i++)
            {
                float an = i / 30f * Mathf.PI * 2f;
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "Rim bulb";
                bulb.transform.SetParent(bulbs, false);
                bulb.transform.position = new Vector3(at.x + Mathf.Cos(an) * 1.14f, at.y + 0.68f, at.z + Mathf.Sin(an) * 1.14f);
                bulb.transform.localScale = Vector3.one * 0.09f;
                bulb.GetComponent<Renderer>().sharedMaterial = bulbGlow;
                Object.DestroyImmediate(bulb.GetComponent<Collider>());
                Glow(bulb, new Color(1f, 0.75f, 0.4f) * 8f);
            }
            AddNightLight(bulbs.gameObject, new Vector3(at.x, at.y + 1.75f, at.z), new Color(1f, 0.78f, 0.5f), 380f, 4f, 0f);
            Fall("Crown spray", 0.03f, 1.78f, 1.56f, 0.2f, 14, top, 0.42f);
            Fall("Top bowl overflow", 0.3f, 1.55f, 1.19f, 0.14f, 26, mid, 0.42f);
            Fall("Middle bowl overflow", 0.58f, 1.21f, 0.5f, 0.2f, 36, basin, 0.55f);
        }

        // ---------- people, pets and the places they use ----------

        static void MoveIn(Transform parent)
        {
            Person(parent, "athirah", "Lily", new Vector3(5.6f, 0.02f, 5.6f), 1f, 200f);
            Person(parent, "amir", "James", new Vector3(11.5f, 0.02f, 6.6f), 1f, 20f);
            Pet(parent, "bedah", "Bedah", new Vector3(3.2f, 0.02f, 6.3f));
        }

        static void Person(Transform parent, string model, string display, Vector3 at, float scale, float yaw)
        {
            var go = Spawn(parent, model, display, at, scale, yaw);
            if (!go) return;
            go.GetComponent<CharacterRig>().kind = model;   // "athirah" has our joint names, "amir" is mapped
            var ch = go.AddComponent<Character>();
            ch.displayName = display; ch.isPet = false; ch.scale = scale;
        }

        static void Pet(Transform parent, string model, string display, Vector3 at)
        {
            var go = Spawn(parent, model, model, at, 1f, 0f);
            if (!go) return;
            var pr = go.GetComponent<CharacterRig>();
            pr.pet = true;
            pr.kind = "cat";
            var ch = go.AddComponent<Character>();
            ch.displayName = display; ch.isPet = true; ch.scale = 1f;
        }

        static GameObject Spawn(Transform parent, string model, string objName, Vector3 at, float scale, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FurnitureImport.ModelDir}/Characters/{model}.fbx");
            if (!prefab) { Debug.LogWarning($"Tiramisu: character {model} not found."); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = objName;
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            var rig = go.AddComponent<CharacterRig>();
            if (model == "athirah") rig.hideMaterial = "Eyeglasses";   // she goes without glasses
            if (model == "amir") PutOnGlasses(go);
            go.AddComponent<UnityEngine.AI.NavMeshAgent>();
            go.AddComponent<DoorOpener>();
            return go;
        }

        /// <summary>The two green loose cushions that came with the sofa model are taken off.</summary>
        static void RemoveSofaCushions(GameObject sofa)
        {
            foreach (var t in sofa.GetComponentsInChildren<Transform>(true))
                if (t && t.name.StartsWith("seat cushion")) Object.DestroyImmediate(t.gameObject);
        }

        /// <summary>The see-through blue silhouette people and pets turn into while decorating.</summary>
        static Material GhostMaterial()
        {
            const string path = "Assets/Art/Materials/Ghost.mat";
            System.IO.Directory.CreateDirectory("Assets/Art/Materials");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("HDRP/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetFloat("_SurfaceType", 1f);
            m.SetFloat("_BlendMode", 0f);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_TransparentZWrite", 1f);   // one silhouette, not layers of limbs showing through each other
            m.SetFloat("_DoubleSidedEnable", 0f);
            m.SetColor("_UnlitColor", new Color(0.35f, 0.6f, 0.85f, 0.3f));
            m.SetColor("_EmissiveColor", Color.black);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Everything the life sim needs: money, needs and moods, things to do, sound, shop, build mode, hints.</summary>
        static void SimSystems(GameObject game)
        {
            game.AddComponent<Household>();
            game.AddComponent<LotManager>();
            game.AddComponent<PetShop>().dogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FurnitureImport.ModelDir}/dog.fbx");
            game.AddComponent<MapWindow>();
            game.AddComponent<GameAudio>();
            game.AddComponent<InteractionSetup>();
            game.AddComponent<SimUi>();
            game.AddComponent<Tutorial>();

            var cat = game.AddComponent<Catalog>();
            (string id, string name, string category)[] items =
            {
                ("sofa", "Sofa", "Living"), ("beanbag", "Bean bag", "Living"), ("marbletable", "Coffee table", "Living"), ("geomrug", "Rug", "Living"),
                ("tvunit", "TV and console", "Living"), ("uplight", "Floor lamp", "Living"), ("bookcase", "Bookcase", "Living"),
                ("modern_arm_chair_01", "Armchair", "Living"), ("side_table_01", "Side table", "Living"), ("potted_plant_01", "Potted plant", "Living"),
                // small things, each one on its own so they can be put anywhere
                ("candles", "Candles", "Decor"), ("globe", "Globe", "Decor"), ("fruitbowl", "Fruit bowl", "Decor"), ("mug", "Mug", "Decor"), ("cuttingboard", "Chopping board", "Decor"),
                ("utensils", "Utensils", "Decor"), ("herbs", "Herbs", "Decor"), ("towelstack", "Towels", "Decor"), ("book_encyclopedia_set_01", "Book set", "Decor"),
                ("ceramic_vase_03", "Vase", "Decor"), ("desk_lamp_arm_01", "Desk lamp", "Decor"), ("potted_plant_04", "Small plant", "Decor"), ("calathea_orbifolia_01", "Calathea", "Decor"),
                ("cardboardboxes", "Cardboard boxes", "Decor"), ("paintcans", "Paint cans", "Decor"), ("sparetyres", "Spare tyres", "Decor"),
                ("diningtable", "Dining table", "Kitchen"), ("diningchair", "Dining chair", "Kitchen"), ("barstool", "Bar stool", "Kitchen"), ("longdining", "Long table", "Kitchen"),
                ("fridge", "Fridge", "Kitchen"), ("espresso", "Coffee machine", "Kitchen"), ("waterdispenser", "Water dispenser", "Kitchen"),
                ("platformbed", "Bed", "Bedroom"), ("platformbed_e", "Bed (dark)", "Bedroom"), ("nightstand", "Nightstand", "Bedroom"), ("wardrobe", "Wardrobe", "Bedroom"), ("bedlamp", "Bedside lamp", "Bedroom"),
                ("bathtub", "Bathtub", "Bath"), ("vanity", "Vanity", "Bath"), ("towelrack", "Towel rack", "Bath"), ("washer", "Washing machine", "Bath"), ("dryer", "Dryer", "Bath"), ("basket", "Laundry basket", "Bath"),
                ("officedesk", "Office desk", "Study"), ("officechair", "Office chair", "Study"), ("teacherdesk", "Teacher's desk", "Study"), ("filecabinet", "File cabinet", "Study"),
                ("printer3d", "3D printer", "Study"), ("robotarm", "Robot arm", "Study"), ("telescope", "Telescope", "Study"),
                ("treadmill", "Treadmill", "Fitness"), ("weightbench", "Weight bench", "Fitness"), ("spinbike", "Spin bike", "Fitness"), ("punchbag", "Punch bag", "Fitness"), ("yogamat", "Yoga mat", "Fitness"), ("dumbbells", "Dumbbells", "Fitness"),
                ("gardenbench", "Garden bench", "Garden"), ("lounger", "Sun lounger", "Garden"), ("hammock", "Hammock", "Garden"), ("parasol", "Parasol", "Garden"), ("bbq", "Barbecue", "Garden"),
                ("bbqcounter", "BBQ counter", "Garden"), ("firepit", "Fire pit", "Garden"), ("lantern", "Lantern", "Garden"), ("outdoorsectional", "Outdoor sofa", "Garden"), ("cooler", "Cooler box", "Garden"),
                ("planter", "Planter", "Garden"), ("planterbox", "Planter box", "Garden"), ("flowerbed", "Flower bed", "Garden"), ("gnome", "Garden gnome", "Garden"), ("flamingo", "Flamingo", "Garden"),
                ("mailbox", "Mailbox", "Garden"), ("wheelbarrow", "Wheelbarrow", "Garden"), ("wateringcan", "Watering can", "Garden"), ("hosereel", "Hose reel", "Garden"),
                ("workbench", "Workbench", "Garage"), ("toolchest", "Tool chest", "Garage"), ("bicycle", "Bicycle", "Garage"), ("beachball", "Beach ball", "Garage"),
                ("guitar", "Acoustic guitar", "Music"), ("synth", "Synth keyboard", "Music"), ("adopt_dog", "Adopt a dog", "Pets"),
                ("avanza", "Toyota Avanza", "Cars"), ("mazda3", "Mazda 3", "Cars"), ("bmwm3", "BMW M3 Competition", "Cars"), ("porsche911", "Porsche 911 Turbo S", "Cars"),
            };
            foreach (var it in items)
            {
                // our own models are fbx files, the photoscanned ones are glTF folders, the cars are glb files
                string fileId = it.id.StartsWith("adopt_") ? it.id.Substring(6) : it.id;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FurnitureImport.ModelDir}/{fileId}.fbx");
                if (!prefab) prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FurnitureImport.ModelDir}/PolyHaven/{it.id}/{it.id}.gltf");
                if (!prefab) prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FurnitureImport.ModelDir}/Cars/{it.id}.glb");
                if (!prefab) continue;
                cat.entries.Add(new CatalogEntry { id = it.id, name = it.name, category = it.category, prefab = prefab, scale = it.id == "potted_plant_01" ? 1.35f : 1f });
            }
            game.AddComponent<BuyMode>();

            var bm = game.AddComponent<BuildMode>();
            bm.floorMaterials = new[] { oak, oakDark, tile, bathTile, garageFloor, gym, deck, stone, slab };
            bm.floorNames = new[] { "Oak", "Dark oak", "Marble", "Grey tile", "Concrete", "Rubber", "Deck", "Paving", "Slate" };
            bm.wallMaterials = new[] { wallWhite, wallBrown };
            bm.wallNames = new[] { "White", "Brown" };
            bm.previewMaterial = GhostMaterial();
            bm.glassMaterial = glass;
            bm.waterMaterial = water;
            bm.roofMaterial = roof;
            bm.stepMaterial = oak;
        }

        /// <summary>The soft round sprite and the four materials for petals, leaves, snow and fireflies.</summary>
        static void SeasonAssets(SeasonCycle sc)
        {
            System.IO.Directory.CreateDirectory("Assets/Art/Materials");
            const string tp = "Assets/Art/Textures/SoftDot.png";
            if (!System.IO.File.Exists(tp))
            {
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32f, 32f)) / 32f;
                        float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
                        t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                t.Apply();
                System.IO.File.WriteAllBytes(tp, t.EncodeToPNG());
                AssetDatabase.ImportAsset(tp);
                var imp = (TextureImporter)AssetImporter.GetAtPath(tp);
                imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.sRGBTexture = true;
                imp.SaveAndReimport();
            }
            var soft = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            Material Dot(string name, Color col, float glow)
            {
                string path = $"Assets/Art/Materials/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(Shader.Find("HDRP/Unlit")); AssetDatabase.CreateAsset(m, path); }
                m.SetFloat("_SurfaceType", 1f);
                m.SetFloat("_BlendMode", 0f);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_DoubleSidedEnable", 1f);
                m.SetTexture("_UnlitColorMap", soft);
                m.SetColor("_UnlitColor", col);
                m.SetColor("_EmissiveColor", col * glow);
                UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
                EditorUtility.SetDirty(m);
                return m;
            }
            // a leaf shaped sprite: a pointed ellipse with a centre vein and a short stem
            const string lp = "Assets/Art/Textures/LeafShape.png";
            if (!System.IO.File.Exists(lp))
            {
                const int N = 128;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;     // -1..1, the leaf runs along v
                        float w = 0.62f * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(v * 0.86f - 0.05f)), 0.75f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01((v + 0.85f) / 1.7f));
                        float a = Mathf.Abs(u) < w ? 1f : 0f;
                        if (Mathf.Abs(u) < 0.03f && v < -0.8f && v > -1f) a = 1f;             // stem
                        float vein = Mathf.Abs(u) < 0.035f || (Mathf.Abs(Mathf.Abs(u) - (v + 0.7f) * 0.28f) < 0.02f && v > -0.6f && v < 0.6f) ? 0.72f : 1f;
                        float edge = Mathf.Clamp01((w - Mathf.Abs(u)) * 30f);
                        t.SetPixel(x, y, new Color(vein, vein, vein, a > 0f ? Mathf.Max(edge, Mathf.Abs(u) < 0.03f ? 1f : 0f) : 0f));
                    }
                t.Apply();
                System.IO.File.WriteAllBytes(lp, t.EncodeToPNG());
                AssetDatabase.ImportAsset(lp);
                var li = (TextureImporter)AssetImporter.GetAtPath(lp);
                li.alphaIsTransparency = true; li.mipmapEnabled = true;
                li.SaveAndReimport();
            }
            var leafTex = AssetDatabase.LoadAssetAtPath<Texture2D>(lp);
            Material Leaf(string name, Color col)
            {
                string path = $"Assets/Art/Materials/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(Shader.Find("HDRP/Unlit")); AssetDatabase.CreateAsset(m, path); }
                m.SetFloat("_SurfaceType", 1f);
                m.SetFloat("_BlendMode", 0f);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_DoubleSidedEnable", 1f);
                m.SetFloat("_AlphaCutoffEnable", 1f);
                m.SetFloat("_AlphaCutoff", 0.35f);
                m.SetTexture("_UnlitColorMap", leafTex);
                m.SetColor("_UnlitColor", col);
                m.SetColor("_EmissiveColor", Color.black);
                UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
                EditorUtility.SetDirty(m);
                return m;
            }
            sc.soft = soft;
            // fallen leaves and petals for the roof: cut out sprites, each with its own threshold in the alpha so they appear a few at a time
            Texture2D Litter(string file, bool leaves)
            {
                string tp2 = $"Assets/Art/Textures/{file}.png";
                if (!System.IO.File.Exists(tp2))
                {
                    const int N = 1024;
                    var px = new Color[N * N];
                    var rnd = new System.Random(leaves ? 5 : 9);
                    int count = leaves ? 520 : 1400;
                    Color[] pal = leaves ? new[] { new Color(0.72f, 0.28f, 0.08f), new Color(0.92f, 0.66f, 0.12f), new Color(0.5f, 0.3f, 0.12f), new Color(0.8f, 0.4f, 0.1f) }
                                          : new[] { new Color(1f, 0.78f, 0.86f), new Color(1f, 0.92f, 0.95f), new Color(0.98f, 0.68f, 0.8f) };
                    for (int k = 0; k < count; k++)
                    {
                        float cx = (float)rnd.NextDouble() * N, cy = (float)rnd.NextDouble() * N, ang = (float)rnd.NextDouble() * Mathf.PI;
                        float len = leaves ? 22f + (float)rnd.NextDouble() * 16f : 7f + (float)rnd.NextDouble() * 5f, wid = leaves ? len * 0.42f : len * 0.6f;
                        var col = pal[rnd.Next(pal.Length)] * (0.85f + 0.3f * (float)rnd.NextDouble());
                        float a = 0.5f + 0.5f * (float)rnd.NextDouble();                    // the threshold: 0.5 shows first, 1.0 last
                        float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                        for (int dy = -(int)len; dy <= (int)len; dy++)
                            for (int dx = -(int)len; dx <= (int)len; dx++)
                            {
                                float u = dx * ca + dy * sa, v = -dx * sa + dy * ca;
                                float w = wid * Mathf.Sqrt(Mathf.Max(0f, 1f - (u / len) * (u / len)));
                                if (Mathf.Abs(v) > w) continue;
                                int x = ((int)cx + dx + N) % N, y = ((int)cy + dy + N) % N;
                                var c2 = col; if (leaves && Mathf.Abs(v) < 0.8f) c2 *= 0.75f;
                                px[y * N + x] = new Color(c2.r, c2.g, c2.b, a);
                            }
                    }
                    var t2 = new Texture2D(N, N, TextureFormat.RGBA32, true);
                    t2.SetPixels(px); t2.Apply();
                    System.IO.File.WriteAllBytes(tp2, t2.EncodeToPNG());
                    AssetDatabase.ImportAsset(tp2);
                    var ti = (TextureImporter)AssetImporter.GetAtPath(tp2);
                    ti.alphaIsTransparency = false; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 4;
                    ti.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Texture2D>(tp2);
            }
            Material Cover(string name, Texture2D tex, Color col, float smooth)
            {
                string path = $"Assets/Art/Materials/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(m, path); }
                m.SetColor("_BaseColor", col); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
                m.SetTexture("_BaseColorMap", tex);
                if (tex)
                {
                    m.SetFloat("_AlphaCutoffEnable", 1f); m.SetFloat("_AlphaCutoff", 1.01f); m.SetFloat("_DoubleSidedEnable", 1f);
                }
                UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
                EditorUtility.SetDirty(m);
                return m;
            }
            sc.snowCover = Cover("RoofSnow", null, new Color(0.96f, 0.97f, 1f), 0.25f);
            sc.leafLitter = Cover("RoofLeaves", Litter("RoofLeavesLitter", true), Color.white, 0.2f);
            sc.petalLitter = Cover("RoofPetals", Litter("RoofPetalsLitter", false), Color.white, 0.2f);
            sc.recolor = Shader.Find("Hidden/Tiramisu/Recolor");
            sc.petalMaterials = new[] { Dot("SeasonPetal", new Color(1f, 0.8f, 0.88f, 1f), 0.6f), Dot("SeasonPetalWhite", new Color(1f, 0.96f, 0.96f, 1f), 0.6f) };
            sc.petalMaterial = sc.petalMaterials[0];
            sc.leafMaterials = new[] { Leaf("SeasonLeafRust", new Color(0.72f, 0.28f, 0.08f, 1f)), Leaf("SeasonLeafGold", new Color(0.92f, 0.66f, 0.12f, 1f)), Leaf("SeasonLeafBrown", new Color(0.5f, 0.3f, 0.12f, 1f)) };
            sc.snowMaterial = Dot("SeasonSnow", new Color(1f, 1f, 1f, 1f), 0.8f);
            sc.fireflyMaterial = Dot("SeasonFirefly", new Color(1f, 0.95f, 0.5f, 1f), 6f);
        }

        /// <summary>The hexagon ring over the person you are playing: lit like everything else in the room (not a flat glowing icon), a pastel base colour with a soft gloss, and a touch of its own glow.</summary>
        static Material PlumbobMaterial()
        {
            const string path = "Assets/Art/Materials/Plumbob.mat";
            System.IO.Directory.CreateDirectory("Assets/Art/Materials");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var lit = Shader.Find("HDRP/Lit");
            if (!m) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != lit) m.shader = lit;      // migrates an older Unlit version of this asset
            m.SetColor("_BaseColor", new Color(0.85f, 0.75f, 0.95f, 1f));
            m.SetFloat("_Smoothness", 0.72f);
            m.SetFloat("_Metallic", 0.05f);
            m.SetColor("_EmissiveColor", new Color(0.85f, 0.75f, 0.95f, 1f) * 0.5f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        [System.Serializable] class DefEntry { public string key; public Vector3 pos; public float yaw; public string host; }
        [System.Serializable] class DefLayout { public System.Collections.Generic.List<DefEntry> items = new System.Collections.Generic.List<DefEntry>(); }

        /// <summary>
        /// The layout Amir left the house in (Assets/Editor/DefaultLayout.json, copied from the saved layout) becomes the starting layout.
        /// Small things that stood on a moved piece are carried along with it.
        /// </summary>
        static void ApplyDefaultLayout()
        {
            const string path = "Assets/Editor/DefaultLayout.json";
            if (!System.IO.File.Exists(path)) return;
            var layout = JsonUtility.FromJson<DefLayout>(System.IO.File.ReadAllText(path));
            if (layout == null) return;
            var all = Object.FindObjectsByType<Furniture>(FindObjectsInactive.Include);
            var byKey = new System.Collections.Generic.Dictionary<string, Furniture>();
            foreach (var f in all) byKey[f.key] = f;
            var placed = new System.Collections.Generic.HashSet<Furniture>();
            foreach (var e in layout.items)
                if (string.IsNullOrEmpty(e.host) && byKey.TryGetValue(e.key, out var f)) placed.Add(f);

            foreach (var e in layout.items)
            {
                if (!string.IsNullOrEmpty(e.host) || !byKey.TryGetValue(e.key, out var f) || f.pinned) continue;
                var oldPose = Matrix4x4.TRS(f.transform.position, f.transform.rotation, Vector3.one);
                var bounds = new Bounds(f.transform.position, Vector3.zero);
                foreach (var r in f.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                var riders = new System.Collections.Generic.List<Furniture>();
                foreach (var r in all)
                {
                    if (r == f || !r.small || placed.Contains(r)) continue;
                    var p = r.transform.position;
                    if (Mathf.Abs(p.x - bounds.center.x) <= bounds.extents.x && Mathf.Abs(p.z - bounds.center.z) <= bounds.extents.z
                        && p.y >= bounds.min.y + 0.05f && p.y <= bounds.max.y + 0.12f) riders.Add(r);
                }
                f.transform.SetPositionAndRotation(e.pos, Quaternion.Euler(0f, e.yaw, 0f));
                var delta = Matrix4x4.TRS(f.transform.position, f.transform.rotation, Vector3.one) * oldPose.inverse;
                foreach (var r in riders)
                    r.transform.SetPositionAndRotation(delta.MultiplyPoint3x4(r.transform.position), delta.rotation * r.transform.rotation);
            }
            foreach (var e in layout.items)
            {
                if (string.IsNullOrEmpty(e.host) || !byKey.TryGetValue(e.key, out var f) || !byKey.TryGetValue(e.host, out var h)) continue;
                f.transform.SetPositionAndRotation(h.transform.TransformPoint(e.pos), Quaternion.Euler(0f, h.transform.eulerAngles.y + e.yaw, 0f));
            }
            PlayerPrefs.DeleteKey("tiramisu.layout");   // the saved one is now the default
            Debug.Log($"Tiramisu: {layout.items.Count} pieces put in the layout Amir left them in.");
        }

        /// <summary>The working TV: a soft light in front of the screen (switched by TvScreen) and the screen itself.</summary>
        static void SetupTv(GameObject tv)
        {
            var lg = new GameObject("TV glow");
            lg.transform.SetParent(tv.transform, false);
            lg.transform.localPosition = new Vector3(0f, 1.0f, 0.7f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            lg.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
            l.lightUnit = LightUnit.Lumen;
            l.color = new Color(0.6f, 0.75f, 1f);
            l.range = 6f;
            l.intensity = 700f;
            l.shadows = LightShadows.None;
            l.enabled = false;
            tv.AddComponent<TvScreen>();
        }

        static void PutOnGlasses(GameObject person)
        {
            var glasses = AssetDatabase.LoadAssetAtPath<GameObject>($"{FurnitureImport.ModelDir}/Characters/glasses.fbx");
            Transform head = null;
            foreach (var t in person.GetComponentsInChildren<Transform>(true)) if (t.name == "Base HumanHead_056") head = t;
            if (!glasses || !head) { Debug.LogWarning("Tiramisu: glasses or head bone not found."); return; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(glasses);
            g.name = "Glasses";
            var up = person.transform.up; var fwd = person.transform.forward;
            g.transform.SetPositionAndRotation(head.position + up * GlassesUp + fwd * GlassesForward, person.transform.rotation);
            g.transform.localScale = Vector3.one * GlassesScale;
            g.transform.SetParent(head, true);
        }

        public static float GlassesUp = 0.1f, GlassesForward = 0.15f, GlassesScale = 1.2f;

        /// <summary>A seat or bed on a piece: where the pelvis goes (piece space), which way to face, and where to stand before getting on.</summary>
        static void Spot(GameObject piece, string label, CharacterRig.Pose pose, Vector3 pelvis, float yaw, Vector3 approach,
            float recline = 6f, float shin = -8f, float footY = 0f, float raise = 0f, float legRaise = 0f, float knee = 6f)
        {
            var g = new GameObject("Use: " + label);
            g.transform.SetParent(piece.transform, false);
            g.transform.localPosition = pelvis;
            var sp = g.AddComponent<UseSpot>();
            sp.label = label; sp.pose = pose; sp.yaw = yaw; sp.approachLocal = approach;
            sp.recline = recline; sp.shinAngle = shin; sp.footY = footY; sp.raise = raise; sp.legRaise = legRaise; sp.kneeBend = knee;
            sp.seconds = pose == CharacterRig.Pose.Lie ? new Vector2(40f, 90f) : new Vector2(20f, 60f);
        }

        static void AddSpots(GameObject go, string id)
        {
            var sit = CharacterRig.Pose.Sit; var lie = CharacterRig.Pose.Lie;
            switch (id)
            {
                case "sofa":
                    foreach (float x in new[] { -0.65f, 0f, 0.65f }) Spot(go, "sofa", sit, new Vector3(x, 0.62f, 0.02f), 0f, new Vector3(x, 0f, 1.1f), recline: 16f, shin: 12f);
                    break;
                case "modern_arm_chair_01": Spot(go, "armchair", sit, new Vector3(0f, 0.52f, 0.05f), 0f, new Vector3(0f, 0f, 0.95f), recline: 22f, shin: 10f); break;
                case "diningchair": Spot(go, "dining chair", sit, new Vector3(0f, 0.52f, 0f), 0f, new Vector3(0.75f, 0f, 0f), recline: 3f); break;
                case "barstool": Spot(go, "bar stool", sit, new Vector3(0f, 0.75f, 0f), 180f, new Vector3(0f, 0f, 0.8f), recline: 0f, shin: -5f, footY: 0.27f); break;
                case "officechair": Spot(go, "office chair", sit, new Vector3(0f, 0.53f, 0.02f), 0f, new Vector3(0.8f, 0f, 0.1f), recline: 8f); break;
                case "platformbed":
                case "platformbed_e": Spot(go, "bed", lie, new Vector3(0f, 0.7f, -0.25f), 0f, new Vector3(1.3f, 0f, 0f), knee: 5f); break;
                case "beanbag": Spot(go, "beanbag", sit, new Vector3(0f, 0.33f, 0f), 0f, new Vector3(0f, 0f, 0.95f), recline: 38f, shin: 22f); break;
                case "lounger": Spot(go, "lounger", lie, new Vector3(0f, 0.55f, -0.16f), 0f, new Vector3(0.9f, 0f, 0f), raise: 58f, knee: 4f); break;
                case "outdoorsectional":
                    foreach (float x in new[] { -0.8f, 0f, 0.8f }) Spot(go, "outdoor sofa", sit, new Vector3(x, 0.56f, 0.08f), 0f, new Vector3(x, 0f, 1.0f), recline: 12f, shin: 10f);
                    break;
                case "gardenbench":
                    foreach (float x in new[] { -0.4f, 0.4f }) Spot(go, "bench", sit, new Vector3(x, 0.53f, 0f), 0f, new Vector3(x, 0f, 0.8f), recline: 10f);
                    break;
                case "longdining":
                    foreach (float x in new[] { -0.8f, 0f, 0.8f })
                    {
                        Spot(go, "long table", sit, new Vector3(x, 0.53f, 0.72f), 180f, new Vector3(x, 0f, 1.4f), recline: 0f);
                        Spot(go, "long table", sit, new Vector3(x, 0.53f, -0.72f), 0f, new Vector3(x, 0f, -1.4f), recline: 0f);
                    }
                    break;
                case "hammock": Spot(go, "hammock", lie, new Vector3(0f, 0.85f, 0.2f), 0f, new Vector3(1.3f, 0f, 0f), raise: 14f, legRaise: 12f, knee: -14f); break;
            }
        }

        static void AttachTo(string wallPath, GameObject item)
        {
            var wall = GameObject.Find(wallPath);
            if (wall && wall.GetComponent<WallCutaway>()) wall.GetComponent<WallCutaway>().attachments.Add(item);
        }

        static void Furnish(Transform ground, Transform upper)
        {
            foreach (var p in LivingProps) { var lp = PropPlacer.Place(p, ground); MakeMovable(lp, false); if (lp) AddSpots(lp, p.id); }
            foreach (var p in UpperProps) { var up = PropPlacer.Place(p, upper); MakeMovable(up, false); if (up) AddSpots(up, p.id); }

            // a picture above the sofa, hung on the back wall (hidden while that wall is cut down)
            var pic = PropPlacer.Place(Pr("hanging_picture_frame_02", null, 4f, 1.35f, 0.01f, 0f, 1.4f, PropPlacer.Body.None), ground);
            var backWall = GameObject.Find("House/Ground floor/Walls/Back wall");
            if (pic && backWall) backWall.GetComponent<WallCutaway>().attachments.Add(pic);

            keyCount.Clear();
            var all = new System.Collections.Generic.List<(string id, float x, float y, float z, float rot, int floor)>();
            foreach (var l in Layout) all.Add((l.id, l.x, -999f, l.z, l.rot, l.floor));   // y -999 = on the floor
            foreach (var t in Tabletop) all.Add((t.id, t.x, t.y, t.z, t.rot, t.y > 2f ? 1 : 0));
            foreach (var f in all)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{FurnitureImport.ModelDir}/{f.id}.fbx");
                if (!model) { Debug.LogWarning($"Tiramisu: model {f.id} not found, skipped."); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, f.floor == 0 ? ground : upper);
                go.name = f.id;
                var spec = PhysicsSetup.Spec(f.id);
                go.transform.position = new Vector3(f.x, f.y > -900f ? f.y + spec.dropHeight : (f.floor == 0 ? 0f : UPY) + FLOOR_TOP + spec.dropHeight, f.z);
                go.transform.rotation = Quaternion.Euler(0f, f.rot, 0f);
                if (f.id == "sofa") RemoveSofaCushions(go);
                PhysicsSetup.MakeSolid(go, spec);
                MakeMovable(go, false);
                AddSpots(go, f.id);
                if (f.id == "bathmirror" && backWall) backWall.GetComponent<WallCutaway>().attachments.Add(go); // hangs on the back wall
                if (f.id == "worldmap" || f.id == "whiteboard" || f.id == "gymmirror") AttachTo("House/Upper floor/Walls/Back wall", go);
                if (f.id == "chalkboard") AttachTo("House/Upper floor/Walls/Left wall", go);
                if (f.id == "beachball")
                {
                    foreach (var bc in go.GetComponentsInChildren<BoxCollider>()) Object.DestroyImmediate(bc);
                    var mf = go.GetComponentInChildren<MeshFilter>();
                    var sc = mf.gameObject.AddComponent<SphereCollider>();
                    sc.center = mf.sharedMesh.bounds.center;
                    sc.radius = mf.sharedMesh.bounds.extents.x;
                    var rbb = go.GetComponent<Rigidbody>();
                    rbb.constraints = RigidbodyConstraints.None;   // it may roll
                    rbb.linearDamping = 0.5f;
                    rbb.angularDamping = 0.25f;
                    go.AddComponent<RollingBall>();
                }
                if (f.id == "fountain") SetupFountain(go.transform.position);
                if (f.id == "shed")
                {
                    // the model has a window and a pegboard with hanging tools that float outside the west wall (they were made for a wider
                    // shed): they looked like a random square and sticks, so they are taken off
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                        if (t && (t.name == "window frame" || t.name == "window glass" || t.name == "pegboard" || t.name.StartsWith("hung tool"))) Object.DestroyImmediate(t.gameObject);
                    ShedDoor(go.transform.position, go.transform); ShedLights(go.transform.position);
                }
                if (f.id == "tvunit") SetupTv(go);
                if (f.id == "firepit") // a flickering fire
                {
                    var fl = new GameObject("Fire light");
                    fl.transform.SetParent(go.transform, false);
                    fl.transform.position = go.transform.position + Vector3.up * 0.6f;
                    var l = fl.AddComponent<Light>();
                    l.type = LightType.Point;
                    fl.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                    l.lightUnit = LightUnit.Lumen;
                    l.color = new Color(1f, 0.55f, 0.2f);
                    l.range = 6f;
                    l.shadows = LightShadows.None;
                    var sw = fl.AddComponent<SwitchableLight>();
                    sw.day = 250f; sw.night = 900f;
                    fl.AddComponent<Flicker>();
                }
                if (f.id == "lantern") AddNightLight(go, go.transform.position + Vector3.up * 0.3f, new Color(1f, 0.7f, 0.4f), 150f, 3f, 0f, new Vector2(0.25f, 0.25f), 90f);
                if (f.id == "bedlamp") AddNightLight(go, go.transform.position + Vector3.up * 0.33f, new Color(1f, 0.78f, 0.5f), 110f, 3f, 0f, new Vector2(0.35f, 0.35f), 90f);
                if (f.id == "uplight") // washes the ceiling with a wide, soft glow
                    AddNightLight(go, go.transform.position + Vector3.up * 1.68f, new Color(1f, 0.8f, 0.55f), 380f, 5f, 0f, new Vector2(0.5f, 0.5f), -90f);
                if (f.id == "sedan" || f.id == "mpv") // red glow behind the tail lights
                {
                    float rear = f.id == "sedan" ? 2.3f : 2.08f; // metres behind the centre (Blender +Y is Unity -Z)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var lg = new GameObject("Tail light glow");
                        lg.transform.SetParent(go.transform, false);
                        lg.transform.position = go.transform.TransformPoint(new Vector3(side * 0.5f, 0.78f, -(rear + 0.12f)));
                        var l = lg.AddComponent<Light>();
                        l.type = LightType.Point;
                        lg.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                        l.lightUnit = LightUnit.Lumen;
                        l.intensity = 60f;
                        l.color = new Color(1f, 0.05f, 0.03f);
                        l.range = 2.5f;
                        l.shadows = LightShadows.None;
                    }
                }
                if (f.id == "pendant") // a soft downward glow from each shade
                    AddNightLight(go, go.transform.position + Vector3.up * 1.86f, new Color(1f, 0.78f, 0.5f), 300f, 4f, 60f, new Vector2(0.5f, 0.5f), 90f);
            }
        }

        // ---------- camera, light and view controller ----------

        static HouseView BuildRig(GameObject upper, GameObject roofGo)
        {
            var sunLight = CinematicSetup.Sun();
            var moonLight = CinematicSetup.Moon();
            var volume = CinematicSetup.PostVolume();
            RenderSettings.fog = false; // HDRP fog lives in the volume

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 2000f;
            camGo.AddComponent<AudioListener>();
            CinematicSetup.Camera(camGo, volume);
            camGo.AddComponent<PhysicsPoke>();
            var orbit = camGo.AddComponent<OrbitCamera>();
            // put the camera where the orbit will start, so the editor view matches play mode
            var rot = Quaternion.Euler(orbit.pitch, orbit.yaw, 0f);
            camGo.transform.SetPositionAndRotation(orbit.pivot + rot * new Vector3(0f, 0f, -orbit.distance), rot);

            var game = new GameObject("Game");
            var hv = game.AddComponent<HouseView>();
            hv.upperFloor = upper;
            hv.roof = roofGo;
            hv.upperFloorY = UPY;
            game.AddComponent<HouseHud>();
            game.AddComponent<DecorateMode>().ghostMaterial = GhostMaterial();
            game.AddComponent<LiveMode>().plumbobMaterial = PlumbobMaterial();
            game.AddComponent<CityTraffic>();
            var seasons = game.AddComponent<SeasonCycle>();
            SeasonAssets(seasons);
            game.AddComponent<TiramisuNav>();
            game.AddComponent<CharacterHud>();
            var dn = game.AddComponent<DayNightCycle>();
            dn.sun = sunLight;
            dn.moon = moonLight;
            dn.volume = volume;
            dn.hour = 15f;
            dn.auto = true;                        // time runs by default, like a life sim
            SimSystems(game);
            game.AddComponent<GraphicsModes>().volume = volume;
            game.AddComponent<FpsBenchmark>();
            return hv;
        }
    }
}
