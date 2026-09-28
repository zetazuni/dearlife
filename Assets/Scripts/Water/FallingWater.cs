using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Falling water made of many small streaks that drop from an emitter (a line or a ring) to a lower level, speeding
    /// up as they fall. When a streak lands it splashes into the target pool surface, so the ripples follow the water.
    /// Used by the round fountain (crown, bowl overflows) and the wall fountain.
    /// </summary>
    public class FallingWater : MonoBehaviour
    {
        public Vector3 start;              // world position of the emitter centre
        public float endY;                 // world height where the water lands
        public Vector3 lineAxis = Vector3.right;
        public float lineLength;           // > 0: emit along a line of this length, centred on start
        public float ringRadius;           // > 0: emit from a ring of this radius instead
        public float drift;                // how far outward (ring) the water travels while falling
        public int count = 40;
        public float fallSeconds = 0.6f;
        public PoolRipples target;
        public float splash = 0.35f;
        public Material material;
        public Vector2 streak = new Vector2(0.018f, 0.1f);

        Transform[] drops;
        Vector3[] origin, dir;
        float[] phase, last;

        void Start()
        {
            drops = new Transform[count];
            origin = new Vector3[count];
            dir = new Vector3[count];
            phase = new float[count];
            last = new float[count];
            var rnd = new System.Random((int)(start.x * 100f + start.z * 37f + start.y * 13f));
            for (int i = 0; i < count; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                g.name = "Water streak";
                g.transform.SetParent(transform, false);
                Destroy(g.GetComponent<Collider>());
                if (material) g.GetComponent<Renderer>().sharedMaterial = material;
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.transform.localScale = new Vector3(streak.x, streak.y, streak.x);
                drops[i] = g.transform;
                float r = (float)rnd.NextDouble();
                if (ringRadius > 0f)
                {
                    float a = (i + (float)rnd.NextDouble() * 0.4f) / count * Mathf.PI * 2f;
                    dir[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    origin[i] = start + dir[i] * ringRadius;
                }
                else
                {
                    origin[i] = start + lineAxis.normalized * ((i + (float)rnd.NextDouble() * 0.5f) / count - 0.5f) * lineLength;
                    dir[i] = Vector3.zero;
                }
                phase[i] = r;
            }
        }

        void Update()
        {
            if (drops == null) return;
            float h = start.y - endY;
            for (int i = 0; i < count; i++)
            {
                float u = (Time.time / Mathf.Max(fallSeconds, 0.1f) + phase[i]) % 1f;
                var p = origin[i] + dir[i] * (drift * u);
                p.y = start.y - h * u * u;
                drops[i].position = p;
                // stretch along the fall
                float speed = 2f * u * h / fallSeconds;
                float len = Mathf.Clamp(streak.y + speed * 0.02f, streak.y, 0.45f);
                drops[i].localScale = new Vector3(streak.x, len, streak.x);
                if (u < last[i] && target)   // it wrapped: the last streak just landed
                {
                    var land = origin[i] + dir[i] * drift;
                    target.Splash(new Vector3(land.x, target.surfaceY, land.z), splash, 0.09f);
                }
                last[i] = u;
            }
        }
    }
}
