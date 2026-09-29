using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Dearlife > Check interactions (in Play mode): sends one person to every thing to do on every kind of item in the
    /// house, one after the other, at triple speed. For each it notes whether they got there, how long it took, the pose,
    /// how far from the item they ended up, whether it finished, and saves a picture of them doing it. The report is
    /// Logs/interaction_check.txt, the pictures Logs/interaction_check/. Use an empty save slot.
    /// </summary>
    public static class InteractionCheck
    {
        class Case { public Interactable it; public InteractionDef d; }

        static readonly List<Case> cases = new List<Case>();
        static readonly StringBuilder log = new StringBuilder();
        static int index, stage, passed;
        static float t0, started, shotAt;
        static Character who;
        static string shotDir;
        static string only;

        [MenuItem("Dearlife/Check interactions")]
        static void Start() => Begin(null);

        /// <summary>Checks only the items whose id contains this (for a second look at a few).</summary>
        public static void Begin(string filter)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("Dearlife: enter Play mode first (on an empty save slot)."); return; }
            only = filter;
            cases.Clear(); log.Clear(); index = 0; stage = 0; passed = 0;
            var seen = new HashSet<string>();
            // the upper floor must be shown, or its pieces are switched off
            var view = HouseView.Instance;
            if (view && view.upperFloor) view.upperFloor.SetActive(true);
            foreach (var it in Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            {
                if (!it.isActiveAndEnabled || it.defs == null || !seen.Add(it.id)) continue;   // one of each kind of item
                if (!string.IsNullOrEmpty(only) && !it.id.Contains(only)) continue;
                foreach (var d in it.defs) cases.Add(new Case { it = it, d = d });
            }
            cases.Sort((a, b) => string.Compare(a.it.id + a.d.id, b.it.id + b.d.id, System.StringComparison.Ordinal));
            foreach (var c in Character.All) if (!c.isPet) { who = c; break; }
            if (!who) { Debug.LogWarning("Dearlife: nobody to send."); return; }
            shotDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "interaction_check");
            Directory.CreateDirectory(shotDir);
            foreach (var f in Directory.GetFiles(shotDir, "*.png")) File.Delete(f);
            var hh = Household.Instance;
            if (hh) Household.Earn(100000, "interaction check");
            Time.timeScale = 3f;
            log.AppendLine($"Dearlife interaction check, {System.DateTime.Now:yyyy-MM-dd HH:mm}, v{GameInfo.Version}, {cases.Count} cases with {who.displayName}");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Fill()
        {
            if (who.sim) for (int i = 0; i < who.sim.needs.Length; i++) who.sim.needs[i] = 85f;   // no need drags them off
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            if (index >= cases.Count) { Finish(); return; }
            var c = cases[index];
            float now = Time.time;
            switch (stage)
            {
                case 0:
                {
                    if (!DearlifeNav.Ready || who.Activity == "Just arrived") break;   // the house is still being set up
                    who.CancelOrders();
                    Fill();
                    // start a couple of steps from where the item is used, on the same floor, so the walk is short but real
                    if (!c.it.StandPoint(c.it.Centre, out var stand)) stand = c.it.Centre;
                    var from = stand;
                    var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = DearlifeNav.AgentType, areaMask = ~(1 << DearlifeNav.StairsArea) };
                    for (int k = 0; k < 8; k++)
                    {
                        var d = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * 1.6f;
                        if (UnityEngine.AI.NavMesh.SamplePosition(stand + d, out var hit, 0.3f, filter) && Mathf.Abs(hit.position.y - stand.y) < 0.3f) { from = hit.position; break; }
                    }
                    who.TeleportTo(from);
                    t0 = now; stage = 1;
                    break;
                }
                case 1:
                    if (now - t0 < 0.8f) break;
                    who.GiveOrder(new Character.Order { kind = Character.Order.Kind.Interact, thing = c.it, def = c.d, label = "Check: " + c.d.label });
                    t0 = now; stage = 2;
                    break;
                case 2:
                    Fill();
                    if (who.ActiveLabel == c.d.label) { started = now - t0; shotAt = now + 1.6f; stage = 3; Frame(c); break; }
                    if (now - t0 > 40f || (now - t0 > 2f && !who.OnOrder && who.Queued == 0 && who.ActiveLabel != c.d.label && who.Activity != "Check: " + c.d.label))
                    {
                        // a TV seat that faces no TV is offered as "(no TV)" and cannot be ordered: not a fault
                        bool noTv = c.d.needsTv && !FacesTv(c.it);
                        Note(c, noTv, noTv ? "no seat here faces a TV (offered as \"(no TV)\")" : $"never started (was: {who.Activity}, said: {who.Bubble})");
                        if (noTv) passed++;
                        Next();
                    }
                    break;
                case 3:
                    Frame(c);
                    if (now >= shotAt)
                    {
                        string file = Path.Combine(shotDir, $"{index:00}_{c.it.id}_{c.d.id}.png");
                        ScreenCapture.CaptureScreenshot(file);
                        Measure(c);
                        stage = 4;
                    }
                    break;
                case 4:
                    Fill();
                    if (who.ActiveLabel != c.d.label)
                    {
                        log.AppendLine($"    finished after {now - t0:0.0} s, then: {who.Activity}");
                        passed++;
                        Next();
                    }
                    else if (now - t0 > started + c.d.seconds + 30f) { log.AppendLine("    did not finish in time"); Next(); }
                    break;
            }
        }

        static void Frame(Case c)
        {
            var cam = OrbitCamera.Instance;
            if (!cam) return;
            cam.ExternalControl = true;
            var fwd = who.transform.forward; fwd.y = 0f;
            float yaw = Quaternion.LookRotation(fwd.sqrMagnitude > 0.01f ? -fwd : Vector3.forward).eulerAngles.y + 35f;   // in front, a little to the side
            cam.SetImmediate(who.transform.position + Vector3.up * 0.8f, yaw + 180f, 42f, 3.0f);
        }

        static void Measure(Case c)
        {
            var rig = who.GetComponent<CharacterRig>();
            var p = who.transform.position;
            var centre = c.it.Centre;
            float flat = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(centre.x, centre.z));
            var toItem = centre - p; toItem.y = 0f;
            float facing = toItem.sqrMagnitude > 0.01f ? Vector3.Angle(who.transform.forward, toItem) : 0f;
            string pose = rig ? rig.pose.ToString() : "?";
            log.AppendLine($"{c.it.id,-22} {c.d.id,-10} started after {started:0.0} s, pose {pose}, {flat:0.00} m from the item's centre, facing it within {facing:0} degrees, y {p.y:0.00}");
        }

        static void Note(Case c, bool ok, string text) => log.AppendLine($"{c.it.id,-22} {c.d.id,-10} {(ok ? "" : "PROBLEM: ")}{text}");

        static bool FacesTv(Interactable it)
        {
            foreach (var sp in it.GetComponentsInChildren<UseSpot>()) if (TvScreen.Facing(sp) != null) return true;
            return false;
        }

        static void Next() { index++; stage = 0; }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Time.timeScale = 1f;
            var cam = OrbitCamera.Instance; if (cam) cam.ExternalControl = false;
            log.Insert(0, $"{passed} of {cases.Count} finished\n");
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "Logs", "interaction_check.txt"), log.ToString());
            Debug.Log($"Dearlife: interaction check done, {passed} of {cases.Count} finished (Logs/interaction_check.txt).");
        }
    }
}
