using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// The windows of the city light up at dusk and the street lamps come on. It works on copies of the materials (never the
    /// assets), and the season changes how much the windows glow (more lights on in the long winter evenings).
    /// </summary>
    public class CityNight : MonoBehaviour
    {
        public List<Material> lit = new List<Material>();
        public Material lamp;

        readonly List<Material> copies = new List<Material>();
        Material lampCopy;
        static readonly int Emissive = Shader.PropertyToID("_EmissiveColor");

        void Start()
        {
            var map = new Dictionary<Material, Material>();
            foreach (var m in lit) if (m) { var c = new Material(m); map[m] = c; copies.Add(c); }
            if (lamp) { lampCopy = new Material(lamp); map[lamp] = lampCopy; }
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var shared = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++) if (shared[i] && map.TryGetValue(shared[i], out var c)) { shared[i] = c; changed = true; }
                if (changed) r.sharedMaterials = shared;
            }
        }

        void Update()
        {
            var dn = DayNightCycle.Instance;
            float night = dn ? dn.Night01 : 0f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.85f, night));
            foreach (var m in copies) m.SetColor(Emissive, Color.white * (k * 14f));
            if (lampCopy) lampCopy.SetColor(Emissive, new Color(1f, 0.82f, 0.5f) * (k * 30f));
        }
    }
}
