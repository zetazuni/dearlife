using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Makes a surface glow at night only (light strips, string light bulbs, lamp caps, pool lamps). It gets its own
    /// copy of the material and DayNightCycle scales the emission with the darkness.
    /// </summary>
    public class NightGlow : MonoBehaviour
    {
        public static readonly List<NightGlow> All = new List<NightGlow>();
        public Color emission = new Color(1f, 0.7f, 0.35f) * 4f;
        Renderer r;
        Material inst;

        void OnEnable()
        {
            r = GetComponent<Renderer>();
            if (r && !inst) inst = r.material;
            if (!All.Contains(this)) All.Add(this);
            Apply(DayNightCycle.Instance ? DayNightCycle.Instance.Night01 : 0f);
        }

        void OnDisable() => All.Remove(this);

        public void Apply(float night01)
        {
            if (!inst) return;
            inst.SetFloat("_UseEmissiveIntensity", 0f);
            inst.SetColor("_EmissiveColor", emission * night01);
        }
    }
}
