using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Poses the realistic pets (tools/blender_pets.py: Bedah the cat and the Shiba dog) on their own skeletons: a walk or
    /// trot with every leg in step, standing with breathing and a looking head, sitting, lying, curled up asleep, grooming,
    /// eating, happy (a raised tail for the cat, a wag and panting for the dog) and a play bow.
    ///
    /// Every pose is a set of turns in the pet's own axes, added bone by bone from the body outwards, so it does not matter
    /// how the imported bones are oriented. "pitch" lifts the tip of a bone up or forward (a leg swings forward, the head
    /// looks up; a tail goes down), "yaw" turns it to the pet's right, "roll" tips its top to the pet's right.
    /// </summary>
    public class PetBody
    {
        class Bone
        {
            public Transform t;
            public Quaternion rest;
            public Vector3 restPos;
            public int depth;
            public Vector3 now, want;    // pitch, yaw, roll in degrees
        }

        readonly Transform fig;
        readonly Dictionary<string, Bone> b = new Dictionary<string, Bone>();
        readonly List<Bone> order = new List<Bone>();
        readonly bool dog;
        readonly float hipH;            // height of the hips at rest, in the pet's own (unscaled) metres
        readonly string[] spine, tail;
        float drop, dropWant, shift, shiftWant, gait, blinkAt, blink, wagSeed;
        Vector3 body, bodyWant;         // the whole body turned about the hips

        // resting on the floor: once a pose has settled, the skin is baked once and the lowest point measured, and the body
        // is raised or lowered so it just touches (kept per pose, so the next time it starts right)
        readonly Dictionary<CharacterRig.Pose, float> floorFix = new Dictionary<CharacterRig.Pose, float>();
        readonly SkinnedMeshRenderer[] skins;
        Mesh baked;
        CharacterRig.Pose last = (CharacterRig.Pose)(-1);
        float settled, fix, fixWant;
        bool measured;

        static readonly Dictionary<string, string> Cat = new Dictionary<string, string>
        {
            { "root", "j_body" }, { "hips", "j_hips" }, { "spine1", "j_spine_1" }, { "spine2", "j_spine_2" }, { "spine3", "j_spine_3" }, { "spine4", "j_spine_4" },
            { "neck1", "j_neck_base" }, { "neck2", "j_neck_1" }, { "head", "j_head" }, { "jaw", "j_jaw" },
            { "earL", "j_l_ear" }, { "earR", "j_r_ear" }, { "lidL", "j_l_upper_eyelid" }, { "lidR", "j_r_upper_eyelid" },
            { "tail1", "j_tail_1" }, { "tail2", "j_tail_2" }, { "tail3", "j_tail_3" }, { "tail4", "j_tail_4" }, { "tail5", "j_tail_5" }, { "tail6", "j_tail_6" },
            { "armL", "j_l_humerous" }, { "elbowL", "j_l_elbow" }, { "pawL", "j_l_wrist" },
            { "armR", "j_r_humerous" }, { "elbowR", "j_r_elbow" }, { "pawR", "j_r_wrist" },
            { "thighL", "j_l_femur" }, { "kneeL", "j_l_knee" }, { "hockL", "j_l_ankle" }, { "footL", "j_l_ball" },
            { "thighR", "j_r_femur" }, { "kneeR", "j_r_knee" }, { "hockR", "j_r_ankle" }, { "footR", "j_r_ball" },
        };

        static readonly Dictionary<string, string> Dog = new Dictionary<string, string>
        {
            { "root", "COG_jnt" }, { "hips", "pelvis_jnt" }, { "spine1", "spine_1_jnt" }, { "spine2", "spine_2_jnt" }, { "spine3", "chest_jnt" },
            { "neck1", "neck_base_jnt" }, { "neck2", "neck_mid_jnt" }, { "head", "head_jnt" }, { "jaw", "jaw_jnt" },
            { "earL", "L_ear_base_jnt" }, { "earR", "R_ear_base_jnt" }, { "lidL", "L_upper_lid_jnt" }, { "lidR", "R_upper_lid_jnt" },
            { "tail1", "tail_1_jnt" }, { "tail2", "tail_2_jnt" }, { "tail3", "tail_3_jnt" }, { "tail4", "tail_4_jnt" }, { "tail5", "tail_5_jnt" }, { "tail6", "tail_6_jnt" }, { "tail7", "tail_7_jnt" },
            { "armL", "L_shoulder_jnt" }, { "elbowL", "L_elbow_jnt" }, { "pawL", "L_wrist_jnt" },
            { "armR", "R_shoulder_jnt" }, { "elbowR", "R_elbow_jnt" }, { "pawR", "R_wrist_jnt" },
            { "thighL", "L_hip_jnt" }, { "kneeL", "L_knee_jnt" }, { "hockL", "L_ankle_jnt" }, { "footL", "L_foot_jnt" },
            { "thighR", "R_hip_jnt" }, { "kneeR", "R_knee_jnt" }, { "hockR", "R_ankle_jnt" }, { "footR", "R_foot_jnt" },
        };

        /// <summary>The pet's bones, or null when this model does not have them (an old jointed figure).</summary>
        public static PetBody Make(Transform figure, string kind)
        {
            var map = kind == "dog" ? Dog : Cat;
            if (!Find(figure, map["root"]) || !Find(figure, map["head"])) return null;
            return new PetBody(figure, map, kind == "dog");
        }

        PetBody(Transform figure, Dictionary<string, string> map, bool isDog)
        {
            fig = figure; dog = isDog;
            foreach (var kv in map)
            {
                var t = Find(figure, kv.Value);
                if (!t) continue;
                int d = 0;
                for (var p = t; p && p != figure; p = p.parent) d++;
                var bone = new Bone { t = t, rest = t.localRotation, restPos = t.localPosition, depth = d };
                b[kv.Key] = bone;
                order.Add(bone);
            }
            order.Sort((x, y) => x.depth.CompareTo(y.depth));
            float s = Mathf.Max(fig.lossyScale.y, 1e-4f);
            hipH = b.ContainsKey("hips") ? (b["hips"].t.position.y - fig.position.y) / s : 0.25f;
            var sp = new List<string>(); foreach (var n in new[] { "spine1", "spine2", "spine3", "spine4" }) if (b.ContainsKey(n)) sp.Add(n);
            var tl = new List<string>(); for (int i = 1; i <= 7; i++) if (b.ContainsKey("tail" + i)) tl.Add("tail" + i);
            spine = sp.ToArray(); tail = tl.ToArray();
            var sk = new List<SkinnedMeshRenderer>();
            foreach (var r in figure.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMaterial && !r.sharedMaterial.name.Contains("Cards")) sk.Add(r);   // tufts and whiskers do not stand on anything
            skins = sk.ToArray();
            wagSeed = Random.value * 10f;
            blinkAt = Random.Range(2f, 5f);
        }

        static Transform Find(Transform root, string name)
        {
            foreach (Transform c in root)
            {
                if (c.name == name) return c;
                var r = Find(c, name);
                if (r) return r;
            }
            return null;
        }

        void Set(string n, float pitch, float yaw = 0f, float roll = 0f)
        {
            if (b.TryGetValue(n, out var x)) x.want += new Vector3(pitch, yaw, roll);
        }

        void Spine(float pitch, float yaw = 0f, float roll = 0f)
        {
            foreach (var n in spine) Set(n, pitch / spine.Length, yaw / spine.Length, roll / spine.Length);
        }

        /// <summary>Spread over the tail, more towards the tip when curl is above 1.</summary>
        void Tail(float pitch, float yaw, float curl = 1f)
        {
            float sum = 0f;
            for (int i = 0; i < tail.Length; i++) sum += Mathf.Pow(i + 1, curl - 1f);
            for (int i = 0; i < tail.Length; i++)
            {
                float w = Mathf.Pow(i + 1, curl - 1f) / sum;
                Set(tail[i], pitch * w, yaw * w);
            }
        }

        /// <summary>One leg: which, how far it swings (forward is positive) and how much it folds.</summary>
        void FrontLeg(string side, float swing, float fold)
        {
            Set("arm" + side, swing);
            Set("elbow" + side, -fold);
            Set("paw" + side, fold * 0.8f);
        }

        void HindLeg(string side, float swing, float fold)
        {
            Set("thigh" + side, swing);
            Set("knee" + side, -fold);
            Set("hock" + side, fold * 1.1f);
            Set("foot" + side, -fold * 0.3f);
        }

        public void Tick(CharacterRig.Pose pose, float clock, float speed, float moved, float dt)
        {
            foreach (var x in order) x.want = Vector3.zero;
            bodyWant = Vector3.zero; dropWant = 0f; shiftWant = 0f;
            float s = Mathf.Max(fig.lossyScale.y, 1e-4f);
            float breathe = Mathf.Sin(clock * (pose == CharacterRig.Pose.Sleep ? 1.3f : 2.2f));
            float fast = 18f;   // how quickly the body follows the pose
            float jawOpen = 0f;

            switch (pose)
            {
                case CharacterRig.Pose.Walk:
                {
                    fast = 22f;
                    float stride = (dog ? 0.62f : 0.42f);
                    gait += moved / s / stride;
                    bool trot = speed / s > (dog ? 1.1f : 0.75f);
                    float a = (trot ? 30f : 22f) * Mathf.Clamp01(speed / s / 0.5f + 0.25f);
                    // a walk puts the feet down one after another (left hind, left fore, right hind, right fore), a trot in diagonal pairs
                    Leg("L", false, gait + 0f, a);
                    Leg("L", true, gait + (trot ? 0.5f : 0.25f), a);
                    Leg("R", false, gait + 0.5f, a);
                    Leg("R", true, gait + (trot ? 0f : 0.75f), a);
                    float bob = Mathf.Sin(gait * Mathf.PI * 4f);
                    dropWant = 0.006f * (bob + 1f) * (dog ? 1.4f : 1f);
                    Spine(0f, 4f * Mathf.Sin(gait * Mathf.PI * 2f));
                    Set("neck1", -4f); Set("head", 3f * bob);
                    if (dog) Tail(-70f, 10f * Mathf.Sin(clock * 6f), 1.8f);
                    else Tail(-25f, 12f * Mathf.Sin(clock * 3f + 1f), 1.4f);
                    break;
                }
                case CharacterRig.Pose.Sit:
                case CharacterRig.Pose.Groom:
                {
                    Sit();
                    if (pose == CharacterRig.Pose.Groom)
                    {
                        if (dog)
                        {
                            // a scratch behind the ear with a hind leg
                            float sc = Mathf.Sin(clock * 22f);
                            HindLeg("R", 30f + 8f * sc, 30f);
                            Set("neck1", 0f, 0f, 14f); Set("head", -10f, 18f, 18f);
                        }
                        else
                        {
                            // licks a raised front paw, then washes the face with it
                            float lick = Mathf.Sin(clock * 7f);
                            FrontLeg("L", 55f, 95f);
                            Set("neck1", -18f, -10f); Set("head", -28f + 6f * lick, -14f);
                            jawOpen = 6f + 4f * lick;
                        }
                    }
                    break;
                }
                case CharacterRig.Pose.Lie:
                case CharacterRig.Pose.Crouch:
                    Loaf();
                    break;
                case CharacterRig.Pose.Sleep:
                    Curl(breathe);
                    break;
                case CharacterRig.Pose.Eat:
                case CharacterRig.Pose.Drink:
                {
                    bodyWant = new Vector3(-8f, 0f, 0f);
                    FrontLeg("L", 6f, 16f); FrontLeg("R", 6f, 16f);
                    Set("neck1", -38f); Set("neck2", -22f); Set("head", -12f);
                    jawOpen = 8f + 8f * Mathf.Max(0f, Mathf.Sin(clock * 9f));
                    Tail(dog ? -40f : -10f, 8f * Mathf.Sin(clock * 2f), 1.5f);
                    break;
                }
                case CharacterRig.Pose.Happy:
                {
                    Stand(clock, breathe);
                    if (dog)
                    {
                        float wag = Mathf.Sin(clock * 15f + wagSeed);
                        Tail(-95f, 42f * wag, 2f);
                        Spine(0f, 5f * wag);
                        jawOpen = 14f + 4f * Mathf.Sin(clock * 9f);   // panting
                        dropWant = 0.01f * Mathf.Abs(Mathf.Sin(clock * 5f));
                        Set("earL", -20f); Set("earR", -20f);
                    }
                    else
                    {
                        // tail straight up with a hooked tip, the head rubbing against a hand, slow blinks
                        Tail(-95f, 6f * Mathf.Sin(clock * 1.5f), 0.9f);
                        if (tail.Length >= 2) { Set(tail[tail.Length - 1], 35f); Set(tail[tail.Length - 2], 20f); }
                        Set("head", 10f, 0f, 16f * Mathf.Sin(clock * 1.6f));
                        blink = Mathf.Max(blink, 0.55f + 0.45f * Mathf.Sin(clock * 0.9f));
                    }
                    break;
                }
                case CharacterRig.Pose.Exercise:
                case CharacterRig.Pose.Dance:
                {
                    // a play bow: chest down, bottom up, tail going
                    bodyWant = new Vector3(-22f, 0f, 0f);
                    foreach (var side in new[] { "L", "R" }) { Set("arm" + side, 25f); Set("elbow" + side, 70f); Set("paw" + side, -15f); }
                    HindLeg("L", -4f, 0f); HindLeg("R", -4f, 0f);
                    Set("neck1", 26f); Set("head", 14f, 0f, 10f * Mathf.Sin(clock * 3f));
                    Tail(dog ? -90f : -50f, 38f * Mathf.Sin(clock * (dog ? 14f : 5f)), 1.6f);
                    jawOpen = dog ? 12f : 0f;
                    break;
                }
                default:
                    Stand(clock, breathe);
                    break;
            }

            // blinking, and the ears turning now and then
            blinkAt -= dt;
            if (blinkAt <= 0f) { blinkAt = Random.Range(2.5f, 6f); blink = 1f; }
            blink = Mathf.MoveTowards(blink, 0f, dt * 7f);
            float shut = pose == CharacterRig.Pose.Sleep ? 1f : Mathf.Clamp01(blink);
            Set("lidL", -32f * shut); Set("lidR", -32f * shut);
            Set("jaw", -jawOpen);
            Set("spine2", 0.8f * breathe);

            // settle onto the floor
            bool grounded = pose != CharacterRig.Pose.Walk && pose != CharacterRig.Pose.Stand && pose != CharacterRig.Pose.Happy;
            if (pose != last)
            {
                last = pose; settled = 0f; measured = false;
                fixWant = grounded && floorFix.TryGetValue(pose, out var known) ? known : 0f;
            }
            settled += dt;
            if (grounded && !measured && settled > 1.2f) { measured = true; Measure(s); floorFix[pose] = fixWant; }
            fix = Mathf.MoveTowards(fix, fixWant, dt * 0.25f);

            // follow the wanted pose smoothly
            float k = 1f - Mathf.Exp(-fast * dt);
            foreach (var x in order) x.now = Vector3.Lerp(x.now, x.want, k);
            body = Vector3.Lerp(body, bodyWant, k);
            drop = Mathf.Lerp(drop, dropWant, k);
            shift = Mathf.Lerp(shift, shiftWant, k);
            Apply(s);
        }

        void Leg(string side, bool front, float g, float a)
        {
            float p = Mathf.Repeat(g, 1f);
            const float stance = 0.62f;
            float swing, lift;
            if (p < stance) { swing = Mathf.Lerp(a, -a, p / stance); lift = 0f; }
            else
            {
                float q = (p - stance) / (1f - stance);
                swing = Mathf.Lerp(-a, a, 0.5f - 0.5f * Mathf.Cos(q * Mathf.PI));
                lift = Mathf.Sin(q * Mathf.PI);
            }
            if (front) FrontLeg(side, swing * 0.9f, 55f * lift);
            else HindLeg(side, swing, 45f * lift);
        }

        void Stand(float clock, float breathe)
        {
            // looks about now and then, the tail swaying
            float look = Mathf.Sin(clock * 0.37f + wagSeed) * Mathf.Sin(clock * 0.11f);
            Set("neck1", 0f, 16f * look); Set("head", 3f * Mathf.Sin(clock * 0.5f), 14f * look);
            Set("earL", 10f * Mathf.Max(0f, Mathf.Sin(clock * 0.8f + 1f)) - 5f); Set("earR", 10f * Mathf.Max(0f, Mathf.Sin(clock * 0.7f + 3f)) - 5f);
            if (dog) Tail(-70f, 8f * Mathf.Sin(clock * 2.5f), 1.8f);
            else Tail(10f, 14f * Mathf.Sin(clock * 1.2f), 1.6f);
        }

        void Sit()
        {
            bodyWant = new Vector3(dog ? 34f : 40f, 0f, 0f);
            dropWant = -hipH * (dog ? 0.58f : 0.62f);
            shiftWant = 0f;
            float up = bodyWant.x;
            // front legs straight down again, hind legs folded under the body
            FrontLeg("L", -up + 6f, 0f); FrontLeg("R", -up + 6f, 0f);
            Set("pawL", 8f); Set("pawR", 8f);
            HindLeg("L", 42f, 120f); HindLeg("R", 42f, 120f);
            Set("hockL", -40f); Set("hockR", -40f);
            Set("neck1", -up * 0.55f); Set("head", -up * 0.35f);
            if (dog) Tail(-20f, 45f, 1.4f); else Tail(-35f, 80f, 1.2f);
        }

        void Loaf()
        {
            dropWant = -hipH * (dog ? 0.66f : 0.7f);
            // like a sphinx: elbows under the chest, forearms laid forward on the floor, hind legs folded under the hips
            foreach (var side in new[] { "L", "R" })
            {
                Set("arm" + side, -50f); Set("elbow" + side, 140f); Set("paw" + side, -10f);
                Set("thigh" + side, 75f); Set("knee" + side, -145f); Set("hock" + side, 70f);
            }
            Set("neck1", -6f); Set("head", -2f);
            Tail(20f, 50f, 1.2f);
        }

        void Curl(float breathe)
        {
            // curled up on one side, nose to tail
            bodyWant = new Vector3(0f, 0f, 72f);
            dropWant = -hipH * 0.74f;
            // lying on its side the curl is a bend of the back (nose towards the belly), in the body's own axes
            Spine(-68f + 1.5f * breathe);
            Set("neck1", -26f); Set("neck2", -20f); Set("head", -26f);
            FrontLeg("L", 25f, 50f); FrontLeg("R", 35f, 60f);
            HindLeg("L", 45f, 60f); HindLeg("R", 55f, 70f);
            Tail(150f, 0f, 1.1f);
            Set("earL", -10f); Set("earR", -10f);
        }

        void Apply(float s)
        {
            foreach (var x in order) { x.t.localRotation = x.rest; x.t.localPosition = x.restPos; }
            var right = fig.right; var up = fig.up; var fwd = fig.forward;
            if (b.TryGetValue("root", out var root))
            {
                var pivot = b.TryGetValue("hips", out var h) ? h.t.position : root.t.position;
                var q = Quaternion.AngleAxis(body.y, up) * Quaternion.AngleAxis(-body.x, right) * Quaternion.AngleAxis(body.z, fwd);
                root.t.rotation = q * root.t.rotation;
                root.t.position = pivot + q * (root.t.position - pivot) + up * ((drop + fix) * s) + fwd * (shift * s);
                // the rest of the pose is in the body's own axes (a pet curled on its side curls about its own back)
                right = q * right; up = q * up; fwd = q * fwd;
            }
            foreach (var x in order)
            {
                var a = x.now;
                if (a.sqrMagnitude < 1e-6f) continue;
                var q = Quaternion.AngleAxis(a.y, up) * Quaternion.AngleAxis(-a.x, right) * Quaternion.AngleAxis(a.z, fwd);
                x.t.rotation = q * x.t.rotation;
            }
        }

        void Measure(float s)
        {
            if (skins.Length == 0) return;
            if (!baked) baked = new Mesh();
            float lowest = float.MaxValue;
            foreach (var r in skins)
            {
                if (!r) continue;
                r.BakeMesh(baked, true);
                var m = r.transform.localToWorldMatrix;
                var vs = baked.vertices;
                for (int i = 0; i < vs.Length; i += 3) lowest = Mathf.Min(lowest, m.MultiplyPoint3x4(vs[i]).y);
            }
            if (lowest == float.MaxValue) return;
            fixWant = Mathf.Clamp(fix + (fig.position.y - lowest) / s, -0.12f, 0.12f);
        }

        /// <summary>The middle of the body, between the hips and the shoulders, wherever the pose has put them.</summary>
        public Vector3 BodyCentre => b.TryGetValue("hips", out var h) && b.TryGetValue("neck1", out var n) ? (h.t.position + n.t.position) * 0.5f : fig.position;

        /// <summary>The top of the head, for the name tag and the speech bubble.</summary>
        public Vector3 HeadTop => b.TryGetValue("head", out var h) ? h.t.position + fig.up * (0.08f * fig.lossyScale.y) : fig.position;
    }
}
