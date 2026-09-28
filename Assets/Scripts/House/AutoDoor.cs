using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Base of every automatic door (sliding, hinged, garage shutter). It opens when the mouse hovers over it, or
    /// when anything carrying a <see cref="DoorOpener"/> (people, pets) is inside its sensor box, and closes a
    /// moment after the last one has gone. The subclass only decides how the door moves (<see cref="Apply"/>).
    /// </summary>
    public abstract class AutoDoor : MonoBehaviour
    {
        public static readonly List<AutoDoor> All = new List<AutoDoor>();

        [Tooltip("Zone that wakes the door for people and pets (world space centre and size).")]
        public Vector3 sensorCenter;
        public Vector3 sensorSize = new Vector3(2.6f, 2.2f, 2.6f);
        public float openSeconds = 0.9f;
        public float stayOpenSeconds = 1.2f;

        float openness, holdUntil, nextScan;
        static int hoverFrame = -1;
        static AutoDoor hovered;
        static readonly Collider[] buffer = new Collider[24];

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }

        /// <summary>Puts the door in its closed pose (used by the builder).</summary>
        public void Refresh() => Apply(0f);
        void OnDisable() => All.Remove(this);

        /// <summary>Opens the door and keeps it open for a moment (call again to keep it open).</summary>
        public void Open(float seconds = -1f) => holdUntil = Mathf.Max(holdUntil, Time.time + (seconds < 0f ? stayOpenSeconds : seconds));

        /// <summary>0 closed to 1 fully open, already eased.</summary>
        protected abstract void Apply(float eased);

        void Update()
        {
            // one mouse ray per frame for all doors
            if (hoverFrame != Time.frameCount)
            {
                hoverFrame = Time.frameCount;
                hovered = null;
                var cam = Camera.main;
                if (cam && !OrbitCamera.Blocked && Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit, 400f, ~0, QueryTriggerInteraction.Ignore))
                    hovered = hit.collider.GetComponentInParent<AutoDoor>();
            }
            if (hovered == this) Open();

            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 0.15f;
                var zone = new Bounds(sensorCenter, sensorSize);
                foreach (var o in DoorOpener.All)
                    if (zone.Contains(o.transform.position + Vector3.up * 0.5f)) { Open(); break; }
            }

            float target = Time.time < holdUntil ? 1f : 0f;
            openness = Mathf.MoveTowards(openness, target, Time.deltaTime / Mathf.Max(openSeconds, 0.05f));
            Apply(openness * openness * (3f - 2f * openness));   // smooth start and stop
        }
    }
}
