using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// The bath fills while somebody bathes: water rises in the tub under a thick layer of foam (a heap of soft white
    /// blobs that covers the bather to the chest), a few soap bubbles float up and a little steam rises; when the bath is
    /// over it all sinks away again. Put on the bathtub by InteractionSetup; Character calls <see cref="Fill"/>.
    /// </summary>
    public class BathTub : MonoBehaviour
    {
        Transform water, foam;
        ParticleSystem bubbles, steam;
        float level, target;
        float bottom, top;                  // inside of the tub, piece space

        public void Fill(bool on)
        {
            Build();
            target = on ? 1f : 0f;
            WaterFx.Run(bubbles, on);
            WaterFx.Run(steam, on);
        }

        void Build()
        {
            if (water) return;
            // the tub's inside, from the tub shell's size (piece space)
            var shell = transform.Find("tub");
            var b = new Bounds(new Vector3(0f, 0.34f, 0f), new Vector3(1.7f, 0.55f, 0.86f));
            if (shell && shell.GetComponent<MeshFilter>())
            {
                var mb = shell.GetComponent<MeshFilter>().sharedMesh.bounds;
                b = new Bounds(transform.InverseTransformPoint(shell.TransformPoint(mb.center)), Vector3.Scale(mb.size, shell.lossyScale));
            }
            bottom = b.min.y + 0.07f;
            top = b.max.y;
            float hx = b.extents.x - 0.1f, hz = b.extents.z - 0.08f;
            var centre = new Vector3(b.center.x, 0f, b.center.z);

            var root = new GameObject("Bath water").transform;
            root.SetParent(transform, false);
            water = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            Destroy(water.GetComponent<Collider>());
            water.name = "Water";
            water.SetParent(root, false);
            water.localPosition = centre;
            water.localScale = new Vector3(hx * 2f, 0.005f, hz * 2f);   // an oval, like the tub
            var wr = water.GetComponent<Renderer>();
            wr.sharedMaterial = WaterFx.Mat("bathwater");
            wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            foam = new GameObject("Foam").transform;
            foam.SetParent(root, false);
            foam.localPosition = centre;
            foam.gameObject.AddComponent<MeshFilter>().sharedMesh = FoamMesh(hx, hz);
            var fr = foam.gameObject.AddComponent<MeshRenderer>();
            fr.sharedMaterial = WaterFx.Mat("foam");
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            float surface = Mathf.Lerp(bottom, top, 0.68f);
            bubbles = WaterFx.Make(root, "Soap bubbles", centre + Vector3.up * (surface + 0.08f), Quaternion.identity, new WaterFx.Spec
            {
                material = "bubbles", rate = 7f, life = 3.5f, speed = 0.05f, gravity = -0.01f, size = new Vector2(0.015f, 0.04f),
                box = new Vector3(hx * 1.6f, 0.05f, hz * 1.6f), drift = new Vector3(0.04f, 0.08f, 0.04f), noise = 0.05f, max = 60,
            });
            steam = WaterFx.Make(root, "Steam", centre + Vector3.up * (surface + 0.15f), Quaternion.identity, new WaterFx.Spec
            {
                material = "steam", rate = 2.5f, life = 6f, speed = 0.05f, size = new Vector2(0.35f, 0.7f), grow = 2f,
                box = new Vector3(hx * 1.5f, 0.05f, hz * 1.5f), drift = new Vector3(0.03f, 0.12f, 0.03f), noise = 0.08f, max = 40,
            });
            Apply();
        }

        /// <summary>A heap of flattened blobs over an oval, denser and higher in the middle, as one mesh.</summary>
        static Mesh FoamMesh(float hx, float hz)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            var rnd = new System.Random(7);
            var parts = new List<CombineInstance>();
            // many small, overlapping, flattened blobs read as foam (a few big ones read as pebbles)
            for (int i = 0; i < 900; i++)
            {
                // a point in the oval, a little more often towards the middle
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rnd.NextDouble()) * 0.97f;
                float x = Mathf.Cos(a) * r * hx, z = Mathf.Sin(a) * r * hz;
                float size = Mathf.Lerp(0.035f, 0.075f, (float)rnd.NextDouble()) * (1.15f - 0.35f * r);
                float y = (float)rnd.NextDouble() * 0.035f + (1f - r * r) * 0.045f;
                parts.Add(new CombineInstance
                {
                    mesh = sphere,
                    transform = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f), new Vector3(size, size * 0.4f, size)),
                });
            }
            var m = new Mesh { name = "Bath foam", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.CombineMeshes(parts.ToArray(), true, true);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        void Update()
        {
            if (!water) return;
            if (Mathf.Approximately(level, target)) return;
            level = Mathf.MoveTowards(level, target, Time.deltaTime / 3.5f);   // three and a half seconds to fill or drain
            Apply();
        }

        void Apply()
        {
            float e = Mathf.SmoothStep(0f, 1f, level);
            float surface = Mathf.Lerp(bottom, top, 0.68f);
            float y = Mathf.Lerp(bottom, surface, e);
            water.gameObject.SetActive(level > 0.01f);
            foam.gameObject.SetActive(level > 0.01f);
            var p = water.localPosition; p.y = y; water.localPosition = p;
            p = foam.localPosition; p.y = y - 0.01f; foam.localPosition = p;
            foam.localScale = new Vector3(0.6f + 0.4f * e, 0.2f + 0.8f * e, 0.6f + 0.4f * e);
        }
    }
}
