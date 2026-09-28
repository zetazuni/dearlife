using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// The glass shower: its front door slides open along the outside of the fixed pane (it used to be a hinged door that
    /// never moved), people step in and out through it, and while the shower runs water pours from the rain head, steam
    /// fills the cube and soap bubbles float round the bather. Put on the shower by InteractionSetup; Character drives it
    /// (<see cref="open"/>, <see cref="Running"/>, <see cref="Outside"/> and <see cref="Inside"/>).
    /// </summary>
    public class ShowerStall : MonoBehaviour
    {
        public bool open;

        Transform door;
        Vector3 closedLocal, openLocal;
        float slide;
        Vector3 doorCentre, insideLocal;       // piece space
        Vector3 facing;                        // out through the door, piece space
        ParticleSystem water, steam, floating, suds;
        Transform sudsOn;
        bool built;

        /// <summary>Where to stand in front of the door.</summary>
        public Vector3 Outside { get { Build(); return transform.TransformPoint(doorCentre + facing * 0.55f); } }
        /// <summary>The doorway itself, on the floor.</summary>
        public Vector3 Doorway { get { Build(); return transform.TransformPoint(doorCentre); } }
        /// <summary>Under the rain head, on the shower tray.</summary>
        public Vector3 Inside { get { Build(); return transform.TransformPoint(insideLocal); } }
        public Vector3 OutwardDir { get { Build(); return transform.TransformDirection(facing); } }
        public bool DoorOpen => slide > 0.95f;

        void Awake() => Build();

        void Build()
        {
            if (built) return;
            built = true;
            var parts = new List<Transform>();
            foreach (Transform c in transform)
            {
                if (c.name.StartsWith("front door") || c.name == "door handle") parts.Add(c);
                if (c.name.StartsWith("hinge")) c.gameObject.SetActive(false);        // a sliding door has no hinges
            }
            var b = new Bounds(); bool any = false;
            foreach (var p in parts)
            {
                var mf = p.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                var mb = mf.sharedMesh.bounds;
                var c = transform.InverseTransformPoint(p.TransformPoint(mb.center));
                if (!any) { b = new Bounds(c, Vector3.zero); any = true; } else b.Encapsulate(c);
                b.Encapsulate(c + Vector3.Scale(mb.extents, p.lossyScale));
                b.Encapsulate(c - Vector3.Scale(mb.extents, p.lossyScale));
            }
            if (!any) b = new Bounds(new Vector3(0.38f, 1.1f, 0.6f), new Vector3(0.64f, 2.15f, 0.02f));
            door = new GameObject("Sliding door").transform;
            door.SetParent(transform, false);
            foreach (var p in parts) p.SetParent(door, true);
            closedLocal = door.localPosition;
            // slide towards the middle of the front, just outside the fixed pane, so the whole doorway opens
            float dir = b.center.x > 0f ? -1f : 1f;
            facing = new Vector3(0f, 0f, Mathf.Sign(b.center.z));
            openLocal = closedLocal + new Vector3(dir * (b.size.x - 0.04f), 0f, 0f) + facing * 0.035f;
            doorCentre = new Vector3(b.center.x, 0f, b.center.z);
            var tray = transform.Find("tray");
            float floor = tray && tray.GetComponent<Renderer>() ? transform.InverseTransformPoint(tray.GetComponent<Renderer>().bounds.max).y : 0.05f;
            insideLocal = new Vector3(b.center.x * 0.25f, floor, -b.center.z * 0.05f);

            var head = transform.Find("rain head");
            var headLocal = head && head.GetComponent<Renderer>() ? transform.InverseTransformPoint(head.GetComponent<Renderer>().bounds.center) + Vector3.down * 0.03f : new Vector3(0f, 2.02f, -0.05f);
            water = WaterFx.Make(transform, "Shower water", headLocal, Quaternion.Euler(90f, 0f, 0f), new WaterFx.Spec
            {
                material = "drops", rate = 420f, life = 0.8f, speed = 2.6f, gravity = 1f, size = new Vector2(0.012f, 0.02f),
                coneRadius = 0.12f, coneAngle = 4f, stretch = true, max = 600,
            });
            float h = headLocal.y;
            var cube = new Vector3(Mathf.Abs(b.center.x) * 3.2f, 0.5f, Mathf.Abs(b.center.z) * 1.8f);
            steam = WaterFx.Make(transform, "Shower steam", new Vector3(0f, h * 0.55f, 0f), Quaternion.identity, new WaterFx.Spec
            {
                material = "steam", rate = 16f, life = 7f, speed = 0.02f, size = new Vector2(0.45f, 0.9f), grow = 1.8f,
                box = cube, drift = new Vector3(0.05f, 0.1f, 0.05f), noise = 0.1f, max = 160,
            });
            floating = WaterFx.Make(transform, "Shower bubbles", new Vector3(0f, h * 0.45f, 0f), Quaternion.identity, new WaterFx.Spec
            {
                material = "bubbles", rate = 30f, life = 5f, speed = 0.03f, gravity = -0.005f, size = new Vector2(0.02f, 0.06f),
                box = new Vector3(cube.x * 0.8f, h * 0.6f, cube.z * 0.8f), drift = new Vector3(0.05f, 0.05f, 0.05f), noise = 0.06f, max = 200,
            });
        }

        /// <summary>Water, steam and bubbles on or off; the bubbles follow this person while it runs.</summary>
        public void Running(bool on, Transform bather)
        {
            Build();
            WaterFx.Run(water, on);
            WaterFx.Run(steam, on);
            WaterFx.Run(floating, on);
            if (on && bather && (!suds || sudsOn != bather))
            {
                if (suds) Destroy(suds.gameObject);
                sudsOn = bather;
                // soap suds sliding down the body: a thin box round the torso and legs, bubbles falling slowly
                suds = WaterFx.Make(bather, "Soap suds", new Vector3(0f, 1.05f, 0f), Quaternion.identity, new WaterFx.Spec
                {
                    material = "bubbles", rate = 110f, life = 1.8f, speed = 0f, gravity = 0.05f, size = new Vector2(0.015f, 0.045f),
                    box = new Vector3(0.36f, 0.95f, 0.26f), world = true, max = 200,
                });
            }
            WaterFx.Run(suds, on);
        }

        void Update()
        {
            if (!door) return;
            float goal = open ? 1f : 0f;
            if (Mathf.Approximately(slide, goal)) return;
            slide = Mathf.MoveTowards(slide, goal, Time.deltaTime / 0.7f);
            door.localPosition = Vector3.Lerp(closedLocal, openLocal, Mathf.SmoothStep(0f, 1f, slide));
        }
    }
}
