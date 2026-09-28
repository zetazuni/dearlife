using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// A light that has one strength by day and another by night (room lights, pendants, lamps, garden and pool
    /// lights). DayNightCycle blends between them, and switches the light off completely when it is not needed.
    /// </summary>
    public class SwitchableLight : MonoBehaviour
    {
        public static readonly List<SwitchableLight> All = new List<SwitchableLight>();
        public float day = 300f;
        public float night = 1200f;
        Light l;
        public float Current { get; private set; }

        void OnEnable() { l = GetComponent<Light>(); if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public void Apply(float night01)
        {
            if (!l) return;
            float v = Mathf.Lerp(day, night, night01);
            Current = v;
            l.intensity = v;
            l.enabled = v > 0.5f;
        }
    }
}
