using UnityEngine;

namespace Dearlife
{
    /// <summary>Makes a light flicker a little like a fire. Works with SwitchableLight (it scales whatever intensity that set).</summary>
    [RequireComponent(typeof(Light))]
    public class Flicker : MonoBehaviour
    {
        public float amount = 0.18f, speed = 7f;
        Light l;
        SwitchableLight sw;
        float seed;

        void Awake() { l = GetComponent<Light>(); sw = GetComponent<SwitchableLight>(); seed = Random.value * 100f; }

        void LateUpdate()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed) * 2f - 1f;
            if (sw && sw.Current > 0.5f) l.intensity = sw.Current * (1f + n * amount);   // scales the base, never compounds
        }
    }
}
