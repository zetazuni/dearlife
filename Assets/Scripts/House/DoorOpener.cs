using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>Put this on a person or a pet and the doors and gates open when it walks up to them (no collider needed).</summary>
    public class DoorOpener : MonoBehaviour
    {
        public static readonly List<DoorOpener> All = new List<DoorOpener>();
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);
    }
}
