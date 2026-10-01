using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Where a frame's time goes, without opening the Profiler window (v0.53.0): <see cref="Begin"/> in Play mode, wait a
    /// few seconds, then <see cref="Report"/> gives the average milliseconds of the parts that matter (the whole frame,
    /// scripts, physics, animation, drawing) and what is being drawn (batches, triangles, shadow casters).
    /// </summary>
    public static class Perf
    {
        static readonly string[] Wanted =
        {
            "Main Thread", "PlayerLoop", "BehaviourUpdate", "LateBehaviourUpdate", "FixedBehaviourUpdate", "FixedUpdate.PhysicsFixedUpdate",
            "Physics.Simulate", "Animators.Update", "MeshSkinning.Update", "NavMeshManager", "GUI.Repaint", "Gfx.WaitForPresentOnGfxThread",
            "Gfx.WaitForGfxCommandsFromMainThread", "Batches Count", "Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Vertices Count",
            "Shadow Casters Count", "Visible Skinned Meshes Count", "GPU Frame Time", "CPU Main Thread Frame Time", "CPU Render Thread Frame Time", "CPU Total Frame Time",
        };

        static readonly List<(string name, ProfilerRecorder rec, bool time)> recs = new List<(string, ProfilerRecorder, bool)>();

        public static void Begin(int frames = 120)
        {
            foreach (var r in recs) r.rec.Dispose();
            recs.Clear();
            var all = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(all);
            var done = new HashSet<string>();
            foreach (var h in all)
            {
                var d = ProfilerRecorderHandle.GetDescription(h);
                if (System.Array.IndexOf(Wanted, d.Name) < 0 || !done.Add(d.Name)) continue;
                var rec = new ProfilerRecorder(h, frames, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.SumAllSamplesInFrame);
                rec.Start();
                recs.Add((d.Name, rec, d.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds));
            }
        }

        public static string Report()
        {
            var sb = new StringBuilder();
            sb.Append($"frame {Time.smoothDeltaTime * 1000f:0.0} ms ({1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f):0} fps)\n");
            foreach (var (name, rec, time) in recs)
            {
                if (!rec.Valid || rec.Count == 0) continue;
                double sum = 0;
                for (int k = 0; k < rec.Count; k++) sum += rec.GetSample(k).Value;
                double avg = sum / rec.Count;
                if (time) { if (avg / 1e6 >= 0.05) sb.Append($"  {name}: {avg / 1e6:0.00} ms\n"); }
                else sb.Append($"  {name}: {avg:0}\n");
            }
            return sb.ToString();
        }
    }
}
