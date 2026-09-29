using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Measures a person's walk in Play mode: sends them along a straight line and samples the hips and feet every frame.
    /// Reports how far the hips bob up and down, how far apart the feet are sideways, and the stride, in centimetres
    /// (Logs/walk_probe.txt). Used to tune the walk (docs/devlog.md, session 55).
    /// </summary>
    public static class WalkProbe
    {
        static Character who;
        static Animator anim;
        static readonly List<(float hip, float side)> samples = new List<(float, float)>();
        static float until, startAt;
        static Vector3 dir;
        static string label;
        static bool shots;
        static float nextShot;
        static int shotIndex;

        public static void Begin(string person, Vector3 from, Vector3 to) => Begin(person, from, to, false);

        /// <summary>With pictures: a camera follows them and saves a front and a side view every 0.4 s (Logs/walk_*.png).</summary>
        public static void Begin(string person, Vector3 from, Vector3 to, bool pictures)
        {
            shots = pictures; shotIndex = 0; nextShot = 0f;
            foreach (var c in Character.All) if (c.displayName == person) who = c;
            if (!who) { Debug.LogWarning("WalkProbe: no " + person); return; }
            anim = who.GetComponent<Animator>();
            label = person;
            samples.Clear();
            who.TeleportTo(from);
            dir = (to - from); dir.y = 0f; dir.Normalize();
            who.GiveOrder(new Character.Order { kind = Character.Order.Kind.Go, point = to, label = "probe" });
            startAt = Time.time + 1.5f;                          // up to walking speed first
            until = Time.time + Vector3.Distance(from, to) / 1.2f;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || !who) { EditorApplication.update -= Tick; return; }
            if (Time.time < startAt) return;
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            var side = Vector3.Cross(Vector3.up, dir);
            samples.Add((hips.position.y - who.transform.position.y, Mathf.Abs(Vector3.Dot(lf.position - rf.position, side))));
            if (shots && Time.time >= nextShot && OrbitCamera.Instance)
            {
                nextShot = Time.time + 0.4f;
                var cam = OrbitCamera.Instance;
                cam.ExternalControl = true;
                bool front = shotIndex % 2 == 0;
                float yaw = Quaternion.LookRotation(front ? -dir : Vector3.Cross(dir, Vector3.up)).eulerAngles.y;
                cam.SetImmediate(who.transform.position + Vector3.up * 0.75f + dir * (front ? 0.4f : 0f), yaw + 180f, 6f, 2.6f);
                ScreenCapture.CaptureScreenshot($"Logs/walk_{label}_{shotIndex:00}_{(front ? "front" : "side")}.png");
                shotIndex++;
            }
            if (Time.time < until) return;
            EditorApplication.update -= Tick;
            float lo = float.MaxValue, hi = float.MinValue, sep = 0f;
            var seps = new List<float>();
            foreach (var s in samples) { lo = Mathf.Min(lo, s.hip); hi = Mathf.Max(hi, s.hip); seps.Add(s.side); }
            seps.Sort();
            sep = seps.Count > 0 ? seps[seps.Count / 2] : 0f;
            string line = $"{label}: hips bob {(hi - lo) * 100f:0.0} cm, feet {sep * 100f:0.0} cm apart sideways (median), {samples.Count} frames";
            System.IO.File.AppendAllText("Logs/walk_probe.txt", line + "\n");
            Debug.Log("WalkProbe " + line);
        }
    }
}
