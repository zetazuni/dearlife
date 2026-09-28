using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Realistic physics (rule 6): project settings, surface physics materials matched to the
    /// render material names, and colliders plus real masses for furniture.
    /// Furniture models come from Blender as a root with one child per part, and each part gets
    /// its own fitted box collider (or the exact mesh when it never moves), so the whole piece is an accurate compound collider.
    /// </summary>
    public static class PhysicsSetup
    {
        const string Dir = "Assets/Art/Physics";

        public struct FurnitureSpec
        {
            public float mass;        // kg
            public bool dynamic;      // false = never moves (rugs, built in pieces)
            public float dropHeight;  // placed this high and allowed to fall and settle (cushions)
        }

        static readonly Dictionary<string, FurnitureSpec> Specs = new Dictionary<string, FurnitureSpec>
        {
            { "sofa",        new FurnitureSpec { mass = 70f, dynamic = true } },
            { "marbletable", new FurnitureSpec { mass = 38f, dynamic = true } },
            { "geomrug",     new FurnitureSpec { mass = 6f,  dynamic = false } },
            { "kitchenrun",  new FurnitureSpec { mass = 400f, dynamic = false } },
            { "fridge",      new FurnitureSpec { mass = 90f,  dynamic = false } },
            { "kitchenisland", new FurnitureSpec { mass = 300f, dynamic = false } },
            { "barstool",    new FurnitureSpec { mass = 5f,   dynamic = true } },
            { "diningtable", new FurnitureSpec { mass = 35f,  dynamic = true } },
            { "diningchair", new FurnitureSpec { mass = 5.5f, dynamic = true } },
            { "pendant",     new FurnitureSpec { mass = 1f,   dynamic = false } },
            { "shower", new FurnitureSpec { mass = 200f, dynamic = false } },
            { "vanity", new FurnitureSpec { mass = 60f, dynamic = false } },
            { "bathmirror", new FurnitureSpec { mass = 8f, dynamic = false } },
            { "toilet", new FurnitureSpec { mass = 40f, dynamic = false } },
            { "bathtub", new FurnitureSpec { mass = 90f, dynamic = false } },
            { "towelrack", new FurnitureSpec { mass = 8f, dynamic = false } },
            { "bathmat", new FurnitureSpec { mass = 1f, dynamic = false } },
            { "washer", new FurnitureSpec { mass = 70f, dynamic = false } },
            { "dryer", new FurnitureSpec { mass = 45f, dynamic = false } },
            { "basket", new FurnitureSpec { mass = 2.5f, dynamic = true } },
            { "workbench", new FurnitureSpec { mass = 90f, dynamic = false } },
            { "garageshelf", new FurnitureSpec { mass = 45f, dynamic = false } },
            { "toolchest", new FurnitureSpec { mass = 60f, dynamic = true } },
            { "bicycle", new FurnitureSpec { mass = 12f, dynamic = false } },
            { "evcharger", new FurnitureSpec { mass = 6f, dynamic = false } },
            { "sedan", new FurnitureSpec { mass = 1300f, dynamic = false } },
            { "mpv", new FurnitureSpec { mass = 1250f, dynamic = false } },
            { "fruitbowl", new FurnitureSpec { mass = 0.9f, dynamic = true } },
            { "cuttingboard", new FurnitureSpec { mass = 1.2f, dynamic = true } },
            { "mug", new FurnitureSpec { mass = 0.3f, dynamic = true } },
            { "espresso", new FurnitureSpec { mass = 5f, dynamic = true } },
            { "utensils", new FurnitureSpec { mass = 0.8f, dynamic = true } },
            { "herbs", new FurnitureSpec { mass = 1.4f, dynamic = true } },
            { "towelstack", new FurnitureSpec { mass = 1.5f, dynamic = true } },
            { "candles", new FurnitureSpec { mass = 0.7f, dynamic = true } },
            { "cardboardboxes", new FurnitureSpec { mass = 9f, dynamic = true } },
            { "paintcans", new FurnitureSpec { mass = 8f, dynamic = true } },
            { "sparetyres", new FurnitureSpec { mass = 30f, dynamic = true } },
            { "platformbed", new FurnitureSpec { mass = 120f, dynamic = false } },
            { "platformbed_e", new FurnitureSpec { mass = 120f, dynamic = false } },
            { "nightstand", new FurnitureSpec { mass = 12f, dynamic = true } },
            { "tvunit", new FurnitureSpec { mass = 40f, dynamic = true } },
            { "bedlamp", new FurnitureSpec { mass = 1.2f, dynamic = true } },
            { "wardrobe", new FurnitureSpec { mass = 90f, dynamic = false } },
            { "bookcase", new FurnitureSpec { mass = 70f, dynamic = false } },
            { "teacherdesk", new FurnitureSpec { mass = 30f, dynamic = true } },
            { "officechair", new FurnitureSpec { mass = 9f, dynamic = true } },
            { "globe", new FurnitureSpec { mass = 1.5f, dynamic = true } },
            { "chalkboard", new FurnitureSpec { mass = 6f, dynamic = false } },
            { "worldmap", new FurnitureSpec { mass = 3f, dynamic = false } },
            { "officedesk", new FurnitureSpec { mass = 45f, dynamic = true } },
            { "filecabinet", new FurnitureSpec { mass = 35f, dynamic = true } },
            { "uplight", new FurnitureSpec { mass = 4f, dynamic = true } },
            { "whiteboard", new FurnitureSpec { mass = 5f, dynamic = false } },
            { "ebench", new FurnitureSpec { mass = 55f, dynamic = true } },
            { "robotarm", new FurnitureSpec { mass = 40f, dynamic = false } },
            { "printer3d", new FurnitureSpec { mass = 18f, dynamic = true } },
            { "beanbag", new FurnitureSpec { mass = 6f, dynamic = true } },
            { "treadmill", new FurnitureSpec { mass = 110f, dynamic = false } },
            { "dumbbells", new FurnitureSpec { mass = 90f, dynamic = false } },
            { "weightbench", new FurnitureSpec { mass = 45f, dynamic = true } },
            { "spinbike", new FurnitureSpec { mass = 35f, dynamic = true } },
            { "punchbag", new FurnitureSpec { mass = 30f, dynamic = false } },
            { "yogamat", new FurnitureSpec { mass = 2f, dynamic = true } },
            { "waterdispenser", new FurnitureSpec { mass = 18f, dynamic = true } },
            { "gymmirror", new FurnitureSpec { mass = 12f, dynamic = false } },
            { "planter", new FurnitureSpec { mass = 14f, dynamic = false } },
            { "lounger", new FurnitureSpec { mass = 12f, dynamic = true } },
            { "parasol", new FurnitureSpec { mass = 20f, dynamic = true } },
            { "bbqcounter", new FurnitureSpec { mass = 250f, dynamic = false } },
            { "bbq", new FurnitureSpec { mass = 35f, dynamic = true } },
            { "cooler", new FurnitureSpec { mass = 8f, dynamic = true } },
            { "telescope", new FurnitureSpec { mass = 5f, dynamic = true } },
            { "outdoorrug", new FurnitureSpec { mass = 5f, dynamic = false } },
            { "longdining", new FurnitureSpec { mass = 60f, dynamic = true } },
            { "outdoorsectional", new FurnitureSpec { mass = 90f, dynamic = true } },
            { "firepit", new FurnitureSpec { mass = 45f, dynamic = true } },
            { "lantern", new FurnitureSpec { mass = 2.5f, dynamic = true } },
            { "hammock", new FurnitureSpec { mass = 30f, dynamic = false } },
            { "gnome", new FurnitureSpec { mass = 4f, dynamic = true } },
            { "flamingo", new FurnitureSpec { mass = 1.5f, dynamic = true } },
            { "beachball", new FurnitureSpec { mass = 5f, dynamic = true } },
            { "fountain", new FurnitureSpec { mass = 400f, dynamic = false } },
            { "mailbox", new FurnitureSpec { mass = 10f, dynamic = false } },
            { "planterbox", new FurnitureSpec { mass = 25f, dynamic = true } },
            { "flowerbed", new FurnitureSpec { mass = 200f, dynamic = false } },
            { "gardenbench", new FurnitureSpec { mass = 20f, dynamic = true } },
            { "shed", new FurnitureSpec { mass = 900f, dynamic = false } },
            { "wheelbarrow", new FurnitureSpec { mass = 12f, dynamic = true } },
            { "toolrack", new FurnitureSpec { mass = 10f, dynamic = true } },
            { "logpile", new FurnitureSpec { mass = 60f, dynamic = true } },
            { "wateringcan", new FurnitureSpec { mass = 3.5f, dynamic = true } },
            { "hosereel", new FurnitureSpec { mass = 6f, dynamic = true } },
            { "cushion",     new FurnitureSpec { mass = 0.8f, dynamic = true } },
        };

        public static FurnitureSpec Spec(string id) =>
            Specs.TryGetValue(id, out var s) ? s : new FurnitureSpec { mass = 20f, dynamic = true };

        // ---------- project settings ----------

        public static void ProjectSettings()
        {
            Time.fixedDeltaTime = 1f / 90f;
            Physics.defaultSolverIterations = 12;
            Physics.defaultSolverVelocityIterations = 4;
            Physics.bounceThreshold = 1f;
            Physics.defaultContactOffset = 0.005f;
            Physics.sleepThreshold = 0.005f;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            AssetDatabase.SaveAssets();
        }

        // ---------- surfaces ----------

        static PhysicsMaterial Surface(string name, float dyn, float stat, float bounce,
            PhysicsMaterialCombine friction = PhysicsMaterialCombine.Average)
        {
            System.IO.Directory.CreateDirectory(Dir);
            string path = $"{Dir}/{name}.asset";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (!m) { m = new PhysicsMaterial(name); AssetDatabase.CreateAsset(m, path); }
            m.dynamicFriction = dyn;
            m.staticFriction = stat;
            m.bounciness = bounce;
            m.frictionCombine = friction;
            m.bounceCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Dictionary<string, PhysicsMaterial> byRenderMaterial;

        static PhysicsMaterial For(string renderMaterial)
        {
            if (byRenderMaterial == null)
            {
                var wood = Surface("Wood", 0.42f, 0.55f, 0.08f);
                var stone = Surface("Stone", 0.55f, 0.65f, 0.04f);
                var glassM = Surface("Glass", 0.25f, 0.35f, 0.06f);
                var grass = Surface("Grass", 0.7f, 0.85f, 0.02f);
                var rubber = Surface("Rubber", 0.9f, 1.0f, 0.35f, PhysicsMaterialCombine.Maximum);
                var metal = Surface("Metal", 0.35f, 0.45f, 0.1f);
                var fabric = Surface("Fabric", 0.75f, 0.9f, 0.02f, PhysicsMaterialCombine.Maximum);
                var foliage = Surface("Foliage", 0.6f, 0.7f, 0.15f);
                byRenderMaterial = new Dictionary<string, PhysicsMaterial>
                {
                    { "Oak", wood }, { "OakDark", wood }, { "Deck", wood }, { "Walnut", wood }, { "Trunk", wood },
                    { "WallWhite", stone }, { "Slab", stone }, { "Stone", stone }, { "GarageFloor", stone },
                    { "KitchenTile", stone }, { "BathTile", stone }, { "PoolTile", stone }, { "Marble_main", stone },
                    { "Glass", glassM }, { "PoolWater", glassM },
                    { "Lawn", grass }, { "LawnEdge", grass },
                    { "GymFloor", rubber },
                    { "Roof", metal }, { "BlackSteel", metal }, { "Mailbox", metal }, { "DarkCap", metal },
                    { "Fabric_main", fabric }, { "Cushion_main", fabric }, { "Rug_main", fabric }, { "RugBorder", fabric },
                    { "Leaves", foliage },
                };
            }
            return byRenderMaterial.TryGetValue(renderMaterial, out var p) ? p : null;
        }

        /// <summary>Gives every collider under root the physics material that matches what it looks like.</summary>
        public static void AssignSurfaces(Transform root)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                var r = c.GetComponent<Renderer>();
                if (!r || !r.sharedMaterial) continue;
                var pm = For(r.sharedMaterial.name);
                if (pm) c.sharedMaterial = pm;
            }
        }

        // ---------- furniture ----------

        /// <summary>
        /// Furniture stays upright and does not skate about: it can only turn on the spot, it stops within a few
        /// centimetres after a nudge, and it cannot be pushed over however many times it is poked.
        /// </summary>
        public static void Steady(Rigidbody rb)
        {
            rb.linearDamping = 4f;
            rb.angularDamping = 6f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        public static void MakeSolid(GameObject go, FurnitureSpec spec)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (spec.dynamic)
                {
                    // PhysX caps convex hulls at 256 polygons, far below our high poly parts, so moving
                    // pieces get one fitted box per part. The parts are close to boxes, so it stays accurate.
                    var bc = mf.gameObject.AddComponent<BoxCollider>();
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = mf.sharedMesh.bounds.size;
                }
                else
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>(); // static pieces can use the exact mesh
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
            AssignSurfaces(go.transform);
            if (!spec.dynamic) return;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = spec.mass;
            Steady(rb);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = spec.mass < 5f ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
            MakeSticky(rb, spec.mass);
        }

        /// <summary>Anything under 3 kg sits fast on its surface (see StickyProp): it only shifts a little and settles back in 200 ms.</summary>
        public static void MakeSticky(Rigidbody rb, float mass)
        {
            if (mass >= 3f) return;
            rb.isKinematic = true;
            if (!rb.GetComponent<StickyProp>()) rb.gameObject.AddComponent<StickyProp>();
        }
    }
}
