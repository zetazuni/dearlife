using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>Marks a room so the camera can jump to it. Size is the floor area in metres.</summary>
    public class RoomMarker : MonoBehaviour
    {
        public static readonly List<RoomMarker> All = new List<RoomMarker>();

        public string displayName;
        public int floor;
        public Vector2 size = new Vector2(8f, 8f);
        public int order;

        void OnEnable() { All.Add(this); All.Sort((a, b) => a.order.CompareTo(b.order)); }
        void OnDisable() => All.Remove(this);

        public float ViewDistance => Mathf.Max(size.x, size.y) * 1.7f + 4f;
    }
}
