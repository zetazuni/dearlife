using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// A jet of water that shoots out of a nozzle, arcs up and falls back into the tub (a real parabola under gravity).
    /// A steady stream of small streaks follows the arc, and every one that lands stirs the ripple surface.
    /// </summary>
    public class WaterJet : MonoBehaviour
    {
        public Vector3 start;              // nozzle, world space
        public Vector3 velocity = new Vector3(0f, 2.3f, -2.6f);
        public int count = 46;
        public Material material;
        public PoolRipples target;
        public float splash = 0.4f;
        public Vector2 streak = new Vector2(0.026f, 0.09f);

        Transform[] drops;
        float[] phase, last;
        float flight;

        void Start()
        {
            flight = 2f * Mathf.Max(velocity.y, 0.5f) / 9.81f;
            drops = new Transform[count];
            phase = new float[count];
            last = new float[count];
            var rnd = new System.Random((int)(start.x * 100f + start.z * 31f));
            for (int i = 0; i < count; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                g.name = "Jet drop";
                g.transform.SetParent(transform, false);
                Destroy(g.GetComponent<Collider>());
                if (material) g.GetComponent<Renderer>().sharedMaterial = material;
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                drops[i] = g.transform;
                phase[i] = (i + (float)rnd.NextDouble() * 0.5f) / count;
            }
        }

        void Update()
        {
            if (drops == null) return;
            for (int i = 0; i < count; i++)
            {
                float u = (Time.time / flight + phase[i]) % 1f;
                float t = u * flight;
                // a little spread so it reads as a jet and not a wire
                float spread = Mathf.Sin(phase[i] * 97f + i) * 0.03f * u;
                var p = start + velocity * t + Vector3.down * (0.5f * 9.81f * t * t) + new Vector3(spread, 0f, spread * 0.5f);
                drops[i].position = p;
                var vel = velocity + Vector3.down * (9.81f * t);
                drops[i].rotation = Quaternion.FromToRotation(Vector3.up, vel.normalized);
                drops[i].localScale = new Vector3(streak.x, streak.y, streak.x);
                if (u < last[i] && target)
                {
                    var land = start + velocity * flight;
                    target.Splash(new Vector3(land.x, target.surfaceY, land.z), splash, 0.16f);
                    target.Splash(new Vector3(start.x, target.surfaceY, start.z - 0.1f), splash * 0.4f, 0.1f);   // churn at the nozzle
                }
                last[i] = u;
            }
        }
    }
}
