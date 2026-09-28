using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dearlife
{
    /// <summary>
    /// A little life in the streets: a handful of cars driving on the left (like at home) round the road grid, never bunched up,
    /// with their lights on at night, and now and then an aeroplane crossing the sky high above the city, with blinking lights.
    /// Everything is built from boxes at start, so there are no extra assets.
    /// </summary>
    public class CityTraffic : MonoBehaviour
    {
        public int cars = 14;
        public Vector2 planeEverySeconds = new Vector2(110f, 260f);

        // the road centre lines, the same as CityBuilder
        static readonly float[] RoadX = { -139.4f, -79.4f, -19.4f, 40.6f, 100.6f, 160.6f };
        static readonly float[] RoadZ = { -130f, -40f, 50f, 140f };
        const float Limit = 216f, Lane = 1.55f;

        class Car
        {
            public Transform t;
            public bool alongZ;
            public float dir, speed, fixedC, pos;
        }

        readonly List<Car> fleet = new List<Car>();
        Material glass, tyre, head, tail;
        Material[] paints;
        Material headCopy, tailCopy;
        static readonly int Emissive = Shader.PropertyToID("_EmissiveColor");

        // ---- the plane
        Transform plane;
        float nextPlane, planeT, planeLen;
        Vector3 planeFrom, planeTo;
        Renderer strobeA, strobeB;
        Material strobeMat;

        void Start()
        {
            MakeMaterials();
            var rnd = new System.Random(11);
            for (int i = 0; i < cars; i++) fleet.Add(MakeCar(rnd, i));
            MakePlane();
            nextPlane = Time.time + Random.Range(25f, 70f);
        }

        // ------------------------------------------------------------ materials

        static Material Lit(string name, Color c, float smooth, float metal, Color? glow = null)
        {
            var m = new Material(Shader.Find("HDRP/Lit")) { name = name };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            if (glow.HasValue) { m.SetFloat("_UseEmissiveIntensity", 0f); m.SetColor("_EmissiveColor", glow.Value); m.EnableKeyword("_EMISSIVE_COLOR_MAP"); }
            return m;
        }

        void MakeMaterials()
        {
            paints = new[]
            {
                Lit("Paint white", new Color(0.92f, 0.92f, 0.93f), 0.8f, 0.3f), Lit("Paint black", new Color(0.03f, 0.03f, 0.035f), 0.85f, 0.4f),
                Lit("Paint silver", new Color(0.62f, 0.64f, 0.67f), 0.8f, 0.7f), Lit("Paint red", new Color(0.55f, 0.04f, 0.05f), 0.85f, 0.3f),
                Lit("Paint blue", new Color(0.06f, 0.16f, 0.4f), 0.85f, 0.3f), Lit("Paint teal", new Color(0.08f, 0.35f, 0.34f), 0.8f, 0.3f),
            };
            glass = Lit("Car glass", new Color(0.03f, 0.05f, 0.07f), 0.95f, 0f);
            tyre = Lit("Tyre", new Color(0.02f, 0.02f, 0.02f), 0.3f, 0f);
            head = Lit("Headlight", new Color(0.9f, 0.92f, 0.95f), 0.7f, 0f, Color.black);
            tail = Lit("Taillight", new Color(0.6f, 0.02f, 0.02f), 0.6f, 0f, Color.black);
            headCopy = head; tailCopy = tail;
        }

        static void Part(Transform parent, string name, Vector3 centre, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.On;
            r.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
        }

        // ------------------------------------------------------------ cars

        Car MakeCar(System.Random rnd, int i)
        {
            var root = new GameObject("Car " + i).transform;
            root.SetParent(transform, false);
            var paint = paints[rnd.Next(paints.Length)];
            int kind = rnd.Next(3);                                   // saloon, hatchback, van
            float len = kind == 2 ? 5.2f : 4.4f, hgt = kind == 2 ? 1.95f : 1.42f;
            Part(root, "Body", new Vector3(0f, 0.62f, 0f), new Vector3(1.8f, 0.62f, len), paint);
            if (kind == 2) Part(root, "Cabin", new Vector3(0f, 1.32f, 0.4f), new Vector3(1.78f, 0.95f, len - 0.9f), paint);
            else Part(root, "Cabin", new Vector3(0f, 1.1f, kind == 0 ? -0.15f : -0.35f), new Vector3(1.55f, 0.5f, len * (kind == 0 ? 0.5f : 0.6f)), glass);
            Part(root, "Roof", new Vector3(0f, kind == 2 ? 1.83f : 1.37f, kind == 2 ? 0.4f : kind == 0 ? -0.15f : -0.35f), new Vector3(1.5f, 0.06f, kind == 2 ? len - 1.1f : len * 0.42f), paint);
            foreach (float x in new[] { -0.9f, 0.9f })
                foreach (float z in new[] { -len * 0.32f, len * 0.32f })
                    Part(root, "Wheel", new Vector3(x, 0.32f, z), new Vector3(0.22f, 0.64f, 0.64f), tyre);
            foreach (float x in new[] { -0.6f, 0.6f })
            {
                Part(root, "Headlight", new Vector3(x, 0.66f, len * 0.5f + 0.01f), new Vector3(0.38f, 0.14f, 0.05f), head);
                Part(root, "Taillight", new Vector3(x, 0.7f, -len * 0.5f - 0.01f), new Vector3(0.38f, 0.14f, 0.05f), tail);
            }
            var c = new Car { t = root, alongZ = rnd.NextDouble() < 0.55, speed = 7f + (float)rnd.NextDouble() * 6f };
            c.dir = rnd.NextDouble() < 0.5 ? 1f : -1f;
            float lane = c.alongZ ? RoadX[rnd.Next(RoadX.Length)] : RoadZ[rnd.Next(RoadZ.Length)];
            c.fixedC = lane;
            c.pos = -Limit + (float)rnd.NextDouble() * Limit * 2f;
            Place(c);
            return c;
        }

        void Place(Car c)
        {
            // driving on the left: heading +z the left side is -x, heading +x the left side is +z
            if (c.alongZ)
            {
                float x = c.fixedC + (c.dir > 0 ? -Lane : Lane);
                c.t.position = new Vector3(x, -0.3f, c.pos);
                c.t.rotation = Quaternion.Euler(0f, c.dir > 0 ? 0f : 180f, 0f);
            }
            else
            {
                float z = c.fixedC + (c.dir > 0 ? Lane : -Lane);
                c.t.position = new Vector3(c.pos, -0.3f, z);
                c.t.rotation = Quaternion.Euler(0f, c.dir > 0 ? 90f : -90f, 0f);
            }
        }

        // ------------------------------------------------------------ the plane

        void MakePlane()
        {
            var body = Lit("Plane body", new Color(0.92f, 0.93f, 0.95f), 0.7f, 0.2f);
            var accent = Lit("Plane tail", new Color(0.05f, 0.25f, 0.55f), 0.7f, 0.2f);
            var dark = Lit("Plane dark", new Color(0.08f, 0.09f, 0.1f), 0.5f, 0.4f);
            strobeMat = Lit("Plane strobe", Color.white, 0.5f, 0f, Color.black);
            var root = new GameObject("Aeroplane").transform;
            root.SetParent(transform, false);
            // the plane's nose points along +z
            var fus = GameObject.CreatePrimitive(PrimitiveType.Capsule); Destroy(fus.GetComponent<Collider>());
            fus.name = "Fuselage"; fus.transform.SetParent(root, false); fus.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); fus.transform.localScale = new Vector3(4.2f, 19f, 4.2f);
            fus.GetComponent<Renderer>().sharedMaterial = body;
            Part(root, "Wing", new Vector3(0f, -0.6f, -1f), new Vector3(34f, 0.35f, 6.4f), body);
            Part(root, "Tailplane", new Vector3(0f, 0.8f, -16.5f), new Vector3(11f, 0.25f, 3.2f), body);
            Part(root, "Fin", new Vector3(0f, 3.4f, -16f), new Vector3(0.3f, 5.6f, 4.4f), accent);
            foreach (float x in new[] { -6.5f, 6.5f }) Part(root, "Engine", new Vector3(x, -1.7f, 1.4f), new Vector3(1.7f, 1.7f, 4.4f), dark);
            Part(root, "Strobe L", new Vector3(-16.8f, -0.4f, -3.4f), new Vector3(1.6f, 1.6f, 1.6f), strobeMat);
            Part(root, "Strobe R", new Vector3(16.8f, -0.4f, -3.4f), new Vector3(1.6f, 1.6f, 1.6f), strobeMat);
            Part(root, "Beacon", new Vector3(0f, 2.3f, -3f), new Vector3(1.4f, 1f, 1.4f), strobeMat);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            root.localScale = Vector3.one * 1.35f;
            plane = root;
            plane.gameObject.SetActive(false);
        }

        void StartPlane()
        {
            float alt = Random.Range(260f, 340f);
            float side = Random.value < 0.5f ? -1f : 1f;
            float off = Random.Range(-120f, 120f);
            bool alongX = Random.value < 0.5f;
            planeFrom = alongX ? new Vector3(-700f * side, alt, off) : new Vector3(off, alt, -700f * side);
            planeTo = alongX ? new Vector3(700f * side, alt + Random.Range(-30f, 30f), off + Random.Range(-60f, 60f)) : new Vector3(off + Random.Range(-60f, 60f), alt + Random.Range(-30f, 30f), 700f * side);
            planeLen = Vector3.Distance(planeFrom, planeTo) / 95f;            // 95 m/s
            planeT = 0f;
            plane.position = planeFrom;
            plane.rotation = Quaternion.LookRotation((planeTo - planeFrom).normalized);
            plane.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------ every frame

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var c in fleet)
            {
                c.pos += c.dir * c.speed * dt;
                if (c.pos > Limit) c.pos = -Limit; else if (c.pos < -Limit) c.pos = Limit;
                Place(c);
            }
            // lights on at dusk (shared materials, copied once so the assets stay clean)
            var dn = DayNightCycle.Instance;
            float night = dn ? dn.Night01 : 0f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, night));
            head.SetColor(Emissive, new Color(1f, 0.95f, 0.85f) * (k * 25f));
            tail.SetColor(Emissive, new Color(1f, 0.05f, 0.03f) * (k * 14f + 3f));

            if (plane == null) return;
            if (!plane.gameObject.activeSelf)
            {
                if (Time.time >= nextPlane) StartPlane();
                return;
            }
            planeT += dt / Mathf.Max(planeLen, 1f);
            plane.position = Vector3.Lerp(planeFrom, planeTo, planeT);
            bool blink = Mathf.Repeat(Time.time, 1.2f) < 0.12f || Mathf.Repeat(Time.time, 1.2f) > 0.3f && Mathf.Repeat(Time.time, 1.2f) < 0.4f;
            strobeMat.SetColor(Emissive, blink ? Color.white * 90f : new Color(0.7f, 0.05f, 0.05f) * 4f);
            if (planeT >= 1f) { plane.gameObject.SetActive(false); nextPlane = Time.time + Random.Range(planeEverySeconds.x, planeEverySeconds.y); }
        }
    }
}
