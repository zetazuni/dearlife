using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Poses and animates a jointed figure from tools/blender_characters.py (people and pets) by swinging its joints.
    /// Angles are given as "forward" or "bend" values and turned into rotations about the figure's own right and up axes,
    /// so it does not matter how the FBX importer oriented the joints.
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        public enum Pose { Stand, Walk, Sit, Lie, Wave, Crouch, Sleep, Groom, Happy, Eat, Cook, Read, Exercise, Work, Wash, Swim, Drink, Guitar, Keys, Dance, Talk }

        public bool pet;
        [Tooltip("person, cat or dog: which joint table builds the skeleton")]
        public string kind = "person";
        public float RestHip { get; private set; } = 0.95f;   // pelvis height above the feet at rest, measured from the model
        [Tooltip("parts whose material name contains this are not drawn (once used to hide a model's glasses)")]
        public string hideMaterial = "";
        public Pose pose = Pose.Stand;
        [System.NonSerialized] public UseSpot seat;   // the piece being sat or lain on: the pose follows its shape
        public float walkSpeed = 1f;       // metres per second, drives the stride

        class Joint
        {
            public Transform t;
            public Quaternion rest;
            public Vector3 restPos;
            public Vector3 rightLocal, upLocal, upInParent;
            public float ang, tgt, lift, liftTgt, yaw, yawTgt;
            public Vector3 anchor;          // where a hand (forearms) or the chest (spine) is, in this joint's own space, measured at rest
            public Quaternion frame;        // turns the figure's own axes into this joint's space, at rest
        }

        readonly Dictionary<string, Joint> j = new Dictionary<string, Joint>();

        // realistic (MPFB2) people play motion capture through a Humanoid Animator instead (docs/CHARACTER_PLAN.md, phase 2)
        Animator anim;
        bool animated;
        const float WalkClipSpeed = 1.35f;   // metres a second the CMU walk covers at normal playback
        float phase, clock, amp = 1f, speedSmooth;
        Vector3 lastPos;

        // some downloaded rigs name their joints differently: our joint name -> the model's bone name
        static readonly Dictionary<string, string> AmirBones = new Dictionary<string, string>
        {
            { "pelvis", "Base HumanPelvis_01" }, { "spine", "Base HumanSpine1_011" }, { "neck", "Base HumanNeck1_054" },
            { "arm.L", "Base HumanLUpperarm_017" }, { "forearm.L", "Base HumanLForearm_018" },
            { "arm.R", "Base HumanRUpperarm_036" }, { "forearm.R", "Base HumanRForearm_037" },
            { "leg.L", "Base HumanLThigh_02" }, { "shin.L", "Base HumanLCalf_00" },
            { "leg.R", "Base HumanRThigh_06" }, { "shin.R", "Base HumanRCalf_07" },
        };

        // the MPFB2 "game engine" skeleton (tools/blender_mpfb_body.py)
        static readonly Dictionary<string, string> MpfbBones = new Dictionary<string, string>
        {
            { "pelvis", "pelvis" }, { "spine", "spine_02" }, { "neck", "neck_01" },
            { "arm.L", "upperarm_l" }, { "forearm.L", "lowerarm_l" }, { "arm.R", "upperarm_r" }, { "forearm.R", "lowerarm_r" },
            { "leg.L", "thigh_l" }, { "shin.L", "calf_l" }, { "leg.R", "thigh_r" }, { "shin.R", "calf_r" },
            { "foot.L", "foot_l" }, { "foot.R", "foot_r" },
        };

        static readonly Dictionary<string, Dictionary<string, string>> Aliases = new Dictionary<string, Dictionary<string, string>>
        {
            { "amir", AmirBones }, { "mpfb", MpfbBones },
        };

        static readonly string[] PersonJoints = { "pelvis", "spine", "neck", "arm.L", "forearm.L", "arm.R", "forearm.R", "leg.L", "shin.L", "leg.R", "shin.R", "foot.L", "foot.R" };
        static readonly string[] PetJoints = { "body", "head", "tail", "legFL", "legFR", "legBL", "legBR" };

        // Joint positions in Blender space (x, y, z), as in tools/blender_characters.py. The FBX is flat: every part is named
        // "joint|part", and the skeleton is built here at runtime and the parts are hung on it.
        static readonly (string name, string parent, Vector3 pos)[] PersonTable =
        {
            ("pelvis", null, new Vector3(0, 0, 0.95f)), ("spine", "pelvis", new Vector3(0, 0, 0.95f)), ("neck", "spine", new Vector3(0, 0, 1.44f)),
            ("arm.L", "spine", new Vector3(0.2f, 0, 1.4f)), ("forearm.L", "arm.L", new Vector3(0.2f, 0, 1.13f)),
            ("arm.R", "spine", new Vector3(-0.2f, 0, 1.4f)), ("forearm.R", "arm.R", new Vector3(-0.2f, 0, 1.13f)),
            ("leg.L", "pelvis", new Vector3(0.085f, 0, 0.93f)), ("shin.L", "leg.L", new Vector3(0.085f, 0, 0.5f)),
            ("leg.R", "pelvis", new Vector3(-0.085f, 0, 0.93f)), ("shin.R", "leg.R", new Vector3(-0.085f, 0, 0.5f)),
        };
        static readonly (string name, string parent, Vector3 pos)[] CatTable =
        {
            ("body", null, new Vector3(0, 0, 0.2f)), ("head", "body", new Vector3(0, -0.27f, 0.3f)), ("tail", "body", new Vector3(0, 0.3f, 0.26f)),
            ("legFL", "body", new Vector3(0.07f, -0.16f, 0.16f)), ("legFR", "body", new Vector3(-0.07f, -0.16f, 0.16f)),
            ("legBL", "body", new Vector3(0.08f, 0.16f, 0.16f)), ("legBR", "body", new Vector3(-0.08f, 0.16f, 0.16f)),
        };
        static readonly (string name, string parent, Vector3 pos)[] DogTable =
        {
            ("body", null, new Vector3(0, 0, 0.36f)), ("head", "body", new Vector3(0, -0.36f, 0.5f)), ("tail", "body", new Vector3(0, 0.38f, 0.44f)),
            ("legFL", "body", new Vector3(0.09f, -0.24f, 0.3f)), ("legFR", "body", new Vector3(-0.09f, -0.24f, 0.3f)),
            ("legBL", "body", new Vector3(0.1f, 0.24f, 0.3f)), ("legBR", "body", new Vector3(-0.1f, 0.24f, 0.3f)),
        };

        void BuildSkeleton()
        {
            if (FindDeep(transform, pet ? "body" : "pelvis")) return;   // already jointed
            var table = kind == "cat" ? CatTable : kind == "dog" ? DogTable : PersonTable;
            var made = new Dictionary<string, Transform>();
            foreach (var (n, par, pos) in table)
            {
                var g = new GameObject(n).transform;
                g.SetParent(transform, false);
                g.localPosition = new Vector3(-pos.x, pos.z, -pos.y);    // Blender to Unity
                if (par != null) g.SetParent(made[par], true);
                made[n] = g;
            }
            var parts = new List<Transform>();
            foreach (Transform c in transform) if (c.name.Contains("|")) parts.Add(c);
            foreach (var c in parts)
                if (made.TryGetValue(c.name.Split('|')[0], out var joint)) c.SetParent(joint, true);
        }

        void Awake()
        {
            lastPos = transform.position;
            Aliases.TryGetValue(kind, out var aliases);
            if (aliases == null) BuildSkeleton();
            foreach (var n in pet ? PetJoints : PersonJoints)
            {
                var t = FindDeep(transform, aliases != null && aliases.TryGetValue(n, out var alias) ? alias : n);
                if (!t) continue;
                j[n] = new Joint
                {
                    t = t,
                    rest = t.localRotation,
                    restPos = t.localPosition,
                    rightLocal = Quaternion.Inverse(t.rotation) * transform.right,
                    upLocal = Quaternion.Inverse(t.rotation) * transform.up,
                    upInParent = t.parent ? Quaternion.Inverse(t.parent.rotation) * transform.up : Vector3.up,
                };
            }
            if (kind == "mpfb")
            {
                LowerArms();
                anim = GetComponent<Animator>();
                if (anim && anim.avatar && anim.avatar.isHuman)
                {
                    if (!anim.runtimeAnimatorController) anim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animation/People");
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animated = anim.runtimeAnimatorController != null;
                    var foot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                    if (foot) ankleHeight = Mathf.Max(0.03f, foot.position.y - transform.position.y);   // still at rest here
                }
            }
            if (!pet)
            {
                // where the hands and the chest are, so things can be put in them whatever the pose
                float sy = Mathf.Max(transform.lossyScale.y, 0.01f);
                foreach (var hn in new[] { "forearm.L", "forearm.R" })
                    if (j.TryGetValue(hn, out var fj)) { fj.anchor = fj.t.InverseTransformPoint(fj.t.position - transform.up * 0.27f * sy); fj.frame = Quaternion.Inverse(fj.t.rotation) * transform.rotation; }
                if (j.TryGetValue("spine", out var sj)) { sj.anchor = sj.t.InverseTransformPoint(transform.position + transform.up * 1.12f * sy + transform.forward * 0.16f * sy); sj.frame = Quaternion.Inverse(sj.t.rotation) * transform.rotation; }
            }
            if (!pet && j.TryGetValue("pelvis", out var pv))
                RestHip = Mathf.Max(0.4f, (pv.t.position.y - transform.position.y) / Mathf.Max(transform.lossyScale.y, 0.01f));
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                r.updateWhenOffscreen = true;
                if (string.IsNullOrEmpty(hideMaterial) || !r.sharedMesh) continue;
                var mats = r.sharedMaterials;
                Mesh copy = null;
                for (int m = 0; m < mats.Length && m < r.sharedMesh.subMeshCount; m++)
                {
                    if (!mats[m] || mats[m].name.IndexOf(hideMaterial, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!copy) { copy = Instantiate(r.sharedMesh); r.sharedMesh = copy; }
                    copy.SetIndices(new int[0], MeshTopology.Triangles, m);
                }
            }   // the bounds of a posed skin change
        }


        /// <summary>
        /// MPFB skeletons rest in an "A" pose (arms out at about 45 degrees). Swing each upper arm down to hang 10 degrees
        /// off the body and make that the rest, so every pose starts from relaxed arms.
        /// </summary>
        void LowerArms()
        {
            foreach (var side in new[] { "L", "R" })
            {
                if (!j.TryGetValue("arm." + side, out var arm) || !j.TryGetValue("forearm." + side, out var fore)) continue;
                var dir = (fore.t.position - arm.t.position).normalized;
                float outward = Mathf.Sign(Vector3.Dot(dir, transform.right));
                var want = Quaternion.AngleAxis(10f * outward, transform.forward) * -transform.up;
                arm.t.rotation = Quaternion.FromToRotation(dir, want) * arm.t.rotation;
            }
            foreach (var jt in j.Values)
            {
                jt.rest = jt.t.localRotation;
                jt.rightLocal = Quaternion.Inverse(jt.t.rotation) * transform.right;
                jt.upLocal = Quaternion.Inverse(jt.t.rotation) * transform.up;
                jt.upInParent = jt.t.parent ? Quaternion.Inverse(jt.t.parent.rotation) * transform.up : Vector3.up;
            }
        }

        /// <summary>The bones were moved (a body slider changed, see <see cref="BodyShape"/>): take their new places as the rest positions.</summary>
        public void RefreshRest(float restHip)
        {
            foreach (var jt in j.Values) if (jt.t) jt.restPos = jt.t.localPosition;
            if (!pet && restHip > 0.4f) RestHip = restHip;
        }

        // ------------------------------------------------------------ things held in the hands

        GameObject heldProp;
        static readonly Dictionary<Color, Material> propMats = new Dictionary<Color, Material>();

        static Material PropMat(Color c, float smooth = 0.45f, float metal = 0f)
        {
            if (propMats.TryGetValue(c, out var m) && m) return m;
            m = new Material(Shader.Find("HDRP/Lit")) { name = "Held prop" };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            propMats[c] = m;
            return m;
        }

        static GameObject Bit(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color c, Quaternion? rot = null, float smooth = 0.45f, float metal = 0f)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            if (rot.HasValue) g.transform.localRotation = rot.Value;
            g.GetComponent<Renderer>().sharedMaterial = PropMat(c, smooth, metal);
            return g;
        }

        /// <summary>Puts something in the hands or against the chest: "book", "mug", "dumbbell" or "guitar" (pass a copy of the guitar of the piece).</summary>
        public void Hold(string kind, GameObject copy = null)
        {
            Release();
            if (pet) return;
            var sp = j.ContainsKey("spine") ? j["spine"] : null;
            var fr = j.ContainsKey("forearm.R") ? j["forearm.R"] : null;
            var fl = j.ContainsKey("forearm.L") ? j["forearm.L"] : null;
            var holder = new GameObject("Held " + kind);
            switch (kind)
            {
                case "book":
                    if (sp == null) { Destroy(holder); return; }
                    holder.transform.SetParent(sp.t, false);
                    holder.transform.localScale = Vector3.one * Inv(sp.t);
                    holder.transform.localPosition = sp.anchor + sp.frame * (new Vector3(0f, 0.02f, 0.05f) * Inv(sp.t));
                    holder.transform.localRotation = sp.frame * Quaternion.Euler(-62f, 0f, 0f);
                    Bit(holder.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.19f, 0.032f, 0.26f), new Color(0.62f, 0.12f, 0.14f));
                    Bit(holder.transform, PrimitiveType.Cube, new Vector3(0f, 0.002f, 0.004f), new Vector3(0.17f, 0.03f, 0.245f), new Color(0.93f, 0.9f, 0.82f));
                    break;
                case "mug":
                    if (fr == null) { Destroy(holder); return; }
                    holder.transform.SetParent(fr.t, false);
                    holder.transform.localScale = Vector3.one * Inv(fr.t);
                    holder.transform.localPosition = fr.anchor;
                    holder.transform.localRotation = fr.frame;
                    Bit(holder.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.045f, 0f), new Vector3(0.075f, 0.045f, 0.075f), new Color(0.95f, 0.94f, 0.9f), null, 0.8f);
                    Bit(holder.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.085f, 0f), new Vector3(0.062f, 0.004f, 0.062f), new Color(0.32f, 0.18f, 0.1f), null, 0.9f);
                    break;
                case "dumbbell":
                    if (fr == null || fl == null) { Destroy(holder); return; }
                    // the two weights ride on the two forearms
                    Destroy(holder); holder = new GameObject("Held dumbbells");
                    foreach (var f in new[] { fr, fl })
                    {
                        var h = new GameObject("Dumbbell").transform;
                        h.SetParent(f.t, false); h.localScale = Vector3.one * Inv(f.t); h.localPosition = f.anchor; h.localRotation = f.frame;
                        Bit(h, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.022f, 0.10f, 0.022f), new Color(0.6f, 0.6f, 0.63f), Quaternion.Euler(0f, 0f, 90f), 0.8f, 1f);
                        foreach (float x in new[] { -0.11f, 0.11f }) Bit(h, PrimitiveType.Cylinder, new Vector3(x, 0f, 0f), new Vector3(0.12f, 0.02f, 0.12f), new Color(0.06f, 0.06f, 0.07f), Quaternion.Euler(0f, 0f, 90f), 0.5f, 0.6f);
                        heldExtra.Add(h.gameObject);
                    }
                    holder.name = "Held dumbbells";
                    break;
                case "guitar":
                    if (sp == null || copy == null) { Destroy(holder); return; }
                    Destroy(holder); holder = copy;
                    holder.SetActive(true);
                    foreach (var mb in holder.GetComponentsInChildren<Collider>()) Destroy(mb);
                    holder.transform.SetParent(sp.t, false);
                    float inv = Inv(sp.t);
                    holder.transform.localScale = Vector3.one * inv;
                    var glr = sp.frame * Quaternion.Euler(-8f, -14f, 78f);
                    // the body of the guitar (0.62 m up its own length, a little behind its plate) is what sits against the chest
                    holder.transform.localRotation = glr;
                    holder.transform.localPosition = sp.anchor + (sp.frame * new Vector3(-0.10f, -0.14f, 0.05f) - glr * new Vector3(0f, 0.62f, -0.10f)) * inv;
                    break;
                default: Destroy(holder); return;
            }
            heldProp = holder;
        }

        readonly List<GameObject> heldExtra = new List<GameObject>();

        /// <summary>Some downloaded skeletons are in centimetres: what is hung on a bone must be scaled back to metres.</summary>
        float Inv(Transform bone) => transform.lossyScale.y / Mathf.Max(bone.lossyScale.y, 1e-6f);

        public void Release()
        {
            if (heldProp) Destroy(heldProp);
            heldProp = null;
            foreach (var g in heldExtra) if (g) Destroy(g);
            heldExtra.Clear();
        }

        /// <summary>The top of the head, from the neck joint (works sitting and lying too).</summary>
        public Vector3 HeadTop
        {
            get
            {
                if (j.TryGetValue("neck", out var n) && n.t) return n.t.position + Vector3.up * (0.33f * transform.lossyScale.y);
                return transform.position + Vector3.up * (1.75f * transform.lossyScale.y);
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform c in root)
            {
                if (c.name == name || (c.name.StartsWith(name + ".") && c.name.Length > name.Length + 1 && char.IsDigit(c.name[name.Length + 1]))) return c;   // Blender numbers repeated names (pelvis.001)
                var r = FindDeep(c, name);
                if (r) return r;
            }
            return null;
        }

        void Set(string n, float forward = 0f, float lift = 0f, float yaw = 0f)
        {
            if (!j.TryGetValue(n, out var jt)) return;
            // these rigs' spine bones are turned the other way round (Lily's too: without this she leant back instead of
            // over the cat when petting it, arms in the air)
            if ((kind == "amir" || kind == "lily" || kind == "mpfb") && (n == "spine" || n == "neck")) forward = -forward;
            jt.tgt = forward; jt.liftTgt = lift; jt.yawTgt = yaw;
        }

        void Update()
        {
            clock += Time.deltaTime;
            float stride = Mathf.Clamp(walkSpeed, 0f, 3f);
            // the stride follows the ground actually covered, so the feet do not slide
            var here = transform.position;
            float moved = Time.deltaTime > 0f ? new Vector2(here.x - lastPos.x, here.z - lastPos.z).magnitude : 0f;
            lastPos = here;
            float speed = Time.deltaTime > 0f ? moved / Time.deltaTime : 0f;
            speedSmooth = Mathf.Lerp(speedSmooth, Mathf.Min(speed, 3f), 1f - Mathf.Exp(-10f * Time.deltaTime));
            phase += moved / Mathf.Max(transform.lossyScale.y, 0.01f) * (pet ? 7.5f : 4.3f);
            amp = Mathf.Clamp01(speedSmooth / (pet ? 0.9f : 1.2f)) * 0.5f + 0.5f;
            if (pose == Pose.Walk && speedSmooth < 0.05f) amp = 0.35f;
            if (animated)
            {
                // standing still with a walk order would march on the spot: stand instead
                var p = pose == Pose.Walk && speedSmooth < 0.05f ? Pose.Stand : pose;
                anim.SetInteger("pose", (int)p);
                anim.SetFloat("walkSpeed", Mathf.Clamp(speedSmooth / (WalkClipSpeed * Mathf.Max(transform.lossyScale.y, 0.01f)), 0.4f, 1.8f));
                return;
            }
            foreach (var jt in j.Values) { jt.tgt = 0f; jt.liftTgt = 0f; jt.yawTgt = 0f; }
            if (pet) PetTargets(); else PersonTargets();

            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            foreach (var kv in j)
            {
                var jt = kv.Value;
                if (!jt.t) continue;
                jt.ang = Mathf.Lerp(jt.ang, jt.tgt, k);
                jt.lift = Mathf.Lerp(jt.lift, jt.liftTgt, k);
                jt.yaw = Mathf.Lerp(jt.yaw, jt.yawTgt, k);
                // positive "forward" swings the joint towards the front of the figure, that is a negative turn about its right axis
                var q = jt.rest * Quaternion.AngleAxis(-jt.ang, jt.rightLocal) * Quaternion.AngleAxis(jt.yaw, jt.upLocal);
                jt.t.localRotation = q;
                jt.t.localPosition = jt.restPos;
                if (Mathf.Abs(jt.lift) > 1e-4f) jt.t.position += transform.up * (jt.lift * transform.lossyScale.y);   // world metres, whatever unit the imported skeleton uses
            }
        }

        // ---- fitting motion capture to our furniture

        Vector3 seatOffset;          // body offset (in the figure's space) that puts the pelvis on the seat, learnt frame by frame
        UseSpot offsetFor;
        float ankleHeight = -1f;     // foot bone above the sole, measured at rest

        bool OnSeat => animated && seat && (pose == Pose.Sit || pose == Pose.Lie || pose == Pose.Sleep);

        /// <summary>
        /// Motion capture does not know our furniture. In the IK pass the body is moved so the pelvis lands on the seat point
        /// (the offset is learnt from the previous frames), then each foot is planted on the floor, or on the foot ring of a bar
        /// stool, with the knees bending to suit the seat height (a beanbag, a bar stool).
        /// </summary>
        void OnAnimatorIK(int layer)
        {
            if (!OnSeat) return;
            if (offsetFor != seat) { seatOffset = Vector3.zero; offsetFor = seat; kneeKnown = false; }
            anim.bodyPosition += transform.TransformVector(seatOffset);
            if (pose != Pose.Sit) return;
            if (ankleHeight < 0f) ankleHeight = 0.075f * transform.lossyScale.y;
            float footY = seat.FloorY + seat.footY + ankleHeight;
            var goals = new[] { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
            for (int i = 0; i < 2; i++)
            {
                var goal = goals[i];
                var p = anim.GetIKPosition(goal);
                // under the knee (last frame's, the pose is steady), the shin leaning by the seat's shin angle: a straight
                // down drop from a foot that the recording stretched forward is out of the leg's reach
                if (kneeKnown)
                {
                    var knee = transform.TransformPoint(kneeLocal[i]);
                    var fwd0 = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                    p = new Vector3(knee.x, footY, knee.z) + fwd0 * (Mathf.Tan(seat.shinAngle * Mathf.Deg2Rad) * Mathf.Max(0f, knee.y - footY));
                }
                anim.SetIKPosition(goal, new Vector3(p.x, footY, p.z));
                anim.SetIKPositionWeight(goal, 1f);
                // flat on the floor, pointing where the knee points
                var fwd = Vector3.ProjectOnPlane(anim.GetIKRotation(goal) * Vector3.forward, Vector3.up);
                if (fwd.sqrMagnitude > 1e-4f) { anim.SetIKRotation(goal, Quaternion.LookRotation(fwd, Vector3.up)); anim.SetIKRotationWeight(goal, 0.7f); }
            }
        }

        /// <summary>Whatever is left after the IK pass is corrected exactly, and fed back into the offset for the next frame.</summary>
        void LateUpdate()
        {
            if (!OnSeat) return;
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (!hips) return;
            var err = seat.transform.position - hips.position;
            seatOffset += transform.InverseTransformVector(err);
            hips.position += err;
            var kl = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg); var kr = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            if (kl && kr) { kneeLocal[0] = transform.InverseTransformPoint(kl.position); kneeLocal[1] = transform.InverseTransformPoint(kr.position); kneeKnown = true; }

            // lying on a lounger or in a hammock: the torso rises with the backrest and the thighs with the ends, like the
            // code posing did (seat.raise, seat.legRaise), bending at the spine and the hips towards straight up
            if (pose != Pose.Sit)
            {
                var spine = anim.GetBoneTransform(HumanBodyBones.Spine); var head = anim.GetBoneTransform(HumanBodyBones.Head);
                if (spine && head && seat.raise > 0.5f) Lift(spine, head.position - spine.position, seat.raise);
                if (seat.legRaise > 0.5f)
                    foreach (var (thigh, shin) in new[] { (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg) })
                    {
                        var t = anim.GetBoneTransform(thigh); var s = anim.GetBoneTransform(shin);
                        if (t && s) Lift(t, s.position - t.position, seat.legRaise);
                    }
            }
        }

        /// <summary>Turns a bone (and all below it) so the direction it points rises towards vertical by the given angle.</summary>
        static void Lift(Transform bone, Vector3 along, float degrees)
        {
            var axis = Vector3.Cross(along, Vector3.up);
            if (axis.sqrMagnitude < 1e-6f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis.normalized) * bone.rotation;
        }

        readonly Vector3[] kneeLocal = new Vector3[2];
        bool kneeKnown;

        void PersonTargets()
        {
            float s = Mathf.Sin(phase), breathe = Mathf.Sin(clock * 1.6f);
            switch (pose)
            {
                case Pose.Walk:
                {
                    float sl = Mathf.Sin(phase), sr = -sl;
                    float cl = Mathf.Cos(phase), cr = -cl;
                    // thigh swings about 28 degrees, the knee bends most while the leg swings forward (foot leaves the ground)
                    Set("leg.L", 28f * amp * sl + 3f); Set("leg.R", 28f * amp * sr + 3f);
                    Set("shin.L", -(6f + Mathf.Max(0f, cl) * 50f * amp)); Set("shin.R", -(6f + Mathf.Max(0f, cr) * 50f * amp));
                    // arms swing against the legs, elbows soft
                    Set("arm.L", -22f * amp * sl, 0f, 0f); Set("arm.R", -22f * amp * sr);
                    Set("forearm.L", 14f + 12f * amp * Mathf.Max(0f, -sl)); Set("forearm.R", 14f + 12f * amp * Mathf.Max(0f, -sr));
                    Set("spine", 3f, 0f, 5f * amp * sl);
                    // the hips rise and fall twice per stride, lowest when both feet are down
                    Set("pelvis", 0f, -0.018f * amp + Mathf.Abs(Mathf.Cos(phase)) * 0.02f * amp, -4f * amp * sl);
                    break;
                }
                case Pose.Sit: SitTargets(breathe); break;
                case Pose.Lie: LieTargets(breathe); break;
                case Pose.Wave:
                    Set("arm.R", 165f); Set("forearm.R", 30f + 25f * Mathf.Sin(clock * 9f));
                    Set("spine", breathe);
                    break;
                case Pose.Crouch: CrouchTargets(); break;
                case Pose.Eat: case Pose.Drink:
                {
                    float bite = Mathf.Max(0f, Mathf.Sin(clock * 2.4f));
                    Set("arm.R", 20f + 60f * bite); Set("forearm.R", 40f + 95f * bite);
                    Set("arm.L", 12f); Set("forearm.L", 30f);
                    Set("spine", 3f); Set("neck", -6f * bite);
                    break;
                }
                case Pose.Cook:
                {
                    float st = Mathf.Sin(clock * 5f);
                    Set("arm.R", 42f + 6f * st); Set("forearm.R", 62f + 14f * st);
                    Set("arm.L", 34f); Set("forearm.L", 70f);
                    Set("spine", 9f); Set("neck", -12f);
                    break;
                }
                case Pose.Read:
                    Set("arm.L", 36f); Set("arm.R", 36f); Set("forearm.L", 98f); Set("forearm.R", 98f);
                    Set("spine", 4f); Set("neck", -20f + 2f * Mathf.Sin(clock * 0.8f));
                    break;
                case Pose.Work:
                {
                    float ty = Mathf.Sin(clock * 9f);
                    Set("arm.L", 40f); Set("arm.R", 40f); Set("forearm.L", 72f + 5f * ty); Set("forearm.R", 72f - 5f * ty);
                    Set("spine", 7f); Set("neck", -14f);
                    break;
                }
                case Pose.Exercise:
                {
                    float b = Mathf.Abs(Mathf.Sin(clock * 3.2f));
                    Set("pelvis", 0f, -0.18f * b);
                    Set("leg.L", 38f * b); Set("leg.R", 38f * b); Set("shin.L", -55f * b); Set("shin.R", -55f * b);
                    Set("arm.L", 20f + 130f * b); Set("arm.R", 20f + 130f * b); Set("forearm.L", 10f); Set("forearm.R", 10f);
                    Set("spine", 8f * b);
                    break;
                }
                case Pose.Wash:
                {
                    float sc = Mathf.Sin(clock * 7f);
                    Set("arm.L", 100f); Set("arm.R", 100f); Set("forearm.L", 95f + 12f * sc); Set("forearm.R", 95f - 12f * sc);
                    Set("spine", 3f); Set("neck", -4f);
                    break;
                }
                case Pose.Guitar:
                {
                    float st = Mathf.Sin(clock * 7.5f), sway = Mathf.Sin(clock * 1.5f);
                    Set("arm.R", 40f + 9f * st); Set("forearm.R", 66f + 22f * st);
                    Set("arm.L", 62f); Set("forearm.L", 30f + 4f * Mathf.Sin(clock * 0.9f));
                    Set("spine", 4f, 0f, 3f * sway); Set("neck", -12f + 3f * sway);
                    Set("pelvis", 0f, 0f, 2f * sway);
                    break;
                }
                case Pose.Keys:
                {
                    float a = Mathf.Sin(clock * 5.2f), b = Mathf.Sin(clock * 4.3f + 2f), sway = Mathf.Sin(clock * 1.2f);
                    Set("arm.L", 46f + 7f * a); Set("forearm.L", 60f + 10f * Mathf.Max(0f, a));
                    Set("arm.R", 46f + 7f * b); Set("forearm.R", 60f + 10f * Mathf.Max(0f, b));
                    Set("spine", 5f, 0f, 3f * sway); Set("neck", -11f + 4f * sway);
                    break;
                }
                case Pose.Dance:
                {
                    float beat = Mathf.Sin(clock * 4.2f), half = Mathf.Sin(clock * 2.1f);
                    Set("pelvis", 0f, -0.03f * Mathf.Abs(beat), 9f * half);
                    Set("arm.L", 105f + 45f * beat); Set("arm.R", 105f - 45f * beat);
                    Set("forearm.L", 35f + 25f * Mathf.Abs(beat)); Set("forearm.R", 35f + 25f * Mathf.Abs(beat));
                    Set("leg.L", 10f * beat); Set("leg.R", -10f * beat); Set("shin.L", -8f * Mathf.Abs(beat)); Set("shin.R", -8f * Mathf.Abs(beat));
                    Set("spine", 2f, 0f, -8f * half); Set("neck", 0f, 0f, 6f * half);
                    break;
                }
                case Pose.Talk:
                {
                    float g1 = Mathf.Max(0f, Mathf.Sin(clock * 2.3f)), g2 = Mathf.Max(0f, Mathf.Sin(clock * 1.7f + 1.5f));
                    Set("arm.R", 26f + 40f * g1); Set("forearm.R", 52f + 34f * g1 - 12f * g2);
                    Set("arm.L", 14f + 28f * g2); Set("forearm.L", 40f + 30f * g2);
                    Set("spine", breathe * 1.2f, 0f, 4f * Mathf.Sin(clock * 1.1f)); Set("neck", 2f * Mathf.Sin(clock * 1.9f), 0f, 5f * Mathf.Sin(clock * 1.3f));
                    break;
                }
                case Pose.Swim:
                {
                    float sw = Mathf.Sin(clock * 3.2f);
                    Set("arm.L", 85f + 85f * sw); Set("arm.R", 85f - 85f * sw); Set("forearm.L", 10f); Set("forearm.R", 10f);
                    Set("leg.L", 14f * sw); Set("leg.R", -14f * sw); Set("neck", 55f);
                    break;
                }
                default:
                    Set("spine", breathe * 1.2f);
                    Set("arm.L", 2f); Set("arm.R", 2f);
                    break;
            }
            FeetTargets(s);
        }

        float Target(string n) => j.TryGetValue(n, out var x) ? x.tgt : 0f;

        /// <summary>The feet stay flat whatever the leg does (the thigh and shin angles are taken back off), with a little toe-off and heel strike when walking.</summary>
        void FeetTargets(float s)
        {
            float toeL = 0f, toeR = 0f;
            if (pose == Pose.Walk)
            {
                toeL = -18f * amp * Mathf.Max(0f, -s) + 8f * amp * Mathf.Max(0f, s);
                toeR = -18f * amp * Mathf.Max(0f, s) + 8f * amp * Mathf.Max(0f, -s);
            }
            Set("foot.L", -(Target("leg.L") + Target("shin.L")) + toeL);
            Set("foot.R", -(Target("leg.R") + Target("shin.R")) + toeR);
        }

        /// <summary>
        /// Sitting follows the piece: the pelvis is on the seat, the torso leans back like the backrest, and the legs are
        /// solved so the feet reach the floor (or the foot ring) whatever the seat height is: knees high on a beanbag,
        /// thighs sloping down on a bar stool.
        /// </summary>
        void SitTargets(float breathe)
        {
            float rec = seat ? seat.recline : 6f;
            float s0 = seat ? seat.shinAngle : -8f;
            float footY = seat ? seat.footY : 0f;
            float hip = seat ? (seat.transform.position.y - seat.FloorY) / Mathf.Max(transform.lossyScale.y, 0.01f) : 0.5f;
            float L = Mathf.Max(0.3f, (RestHip - 0.06f) * 0.5f);
            float drop = Mathf.Max(0.05f, hip - footY - 0.06f);
            float cosA = Mathf.Clamp(drop / L - Mathf.Cos(s0 * Mathf.Deg2Rad), -1f, 1f);
            float a = Mathf.Acos(cosA) * Mathf.Rad2Deg;                 // thigh, forward from straight down
            Set("pelvis", 0f, -(RestHip - 0.45f));
            Set("leg.L", a); Set("leg.R", a);
            Set("shin.L", s0 - a); Set("shin.R", s0 - a);               // lower leg relative to the thigh
            Set("spine", -rec + breathe);
            Set("neck", rec * 0.6f);                                    // the head stays up
            float lap = Mathf.Lerp(24f, 14f, Mathf.Clamp01(rec / 25f));
            Set("arm.L", lap); Set("arm.R", lap); Set("forearm.L", 62f); Set("forearm.R", 62f);
        }

        /// <summary>
        /// Crouching down to a pet: the pelvis drops to knee height and the legs are solved so the feet stay on the floor
        /// (the old pose left the knees bent but the body hanging in the air), the back leans forward and one hand reaches out.
        /// </summary>
        void CrouchTargets()
        {
            float hip = 0.5f;
            float L = Mathf.Max(0.3f, (RestHip - 0.06f) * 0.5f);
            float s0 = -32f;
            float cosA = Mathf.Clamp((hip - 0.06f) / L - Mathf.Cos(s0 * Mathf.Deg2Rad), -1f, 1f);
            float a = Mathf.Acos(cosA) * Mathf.Rad2Deg;
            Set("pelvis", 0f, -(RestHip - hip));
            Set("leg.L", a); Set("leg.R", a);
            Set("shin.L", s0 - a); Set("shin.R", s0 - a);
            Set("spine", 24f, 0f, 0f);
            Set("neck", -10f);
            float stroke = Mathf.Sin(clock * 3.2f);
            Set("arm.L", 62f + 8f * stroke); Set("forearm.L", 30f + 12f * stroke);      // the petting hand
            Set("arm.R", 22f); Set("forearm.R", 50f);                                    // the other rests on the knee
        }

        /// <summary>Lying follows the piece: a flat bed, a lounger with its backrest raised, a hammock that curves up at both ends.</summary>
        void LieTargets(float breathe)
        {
            float raise = seat ? seat.raise : 0f, legs = seat ? seat.legRaise : 0f, knee = seat ? seat.kneeBend : 6f;
            // positive bends towards the chest (as in the crouch), which lying face up lifts the torso; it was negative,
            // which bent Lily and James down through a lounger's backrest and to the bottom of the bath
            Set("spine", raise + breathe * 1.5f);
            Set("neck", Mathf.Min(raise * 0.4f, 22f));
            Set("leg.L", legs); Set("leg.R", legs + 1.5f);
            Set("shin.L", -knee); Set("shin.R", -knee - 2f);
            float rest = raise > 20f ? 30f : 10f;                       // hands lie on the belly when the body is propped up
            Set("arm.L", 6f); Set("arm.R", 6f); Set("forearm.L", rest + 12f); Set("forearm.R", rest + 12f);
        }

        void PetTargets()
        {
            float s = Mathf.Sin(phase), tailSway = Mathf.Sin(clock * 3.5f) * 12f;
            switch (pose)
            {
                case Pose.Walk:
                    float sw = 34f * s;
                    Set("legFL", sw); Set("legBR", sw); Set("legFR", -sw); Set("legBL", -sw);
                    Set("body", 0f, Mathf.Abs(s) * 0.012f);
                    Set("head", -4f * s);
                    Set("tail", 0f, 0f, 18f * Mathf.Sin(clock * 5f));
                    break;
                case Pose.Sit:
                case Pose.Groom:
                case Pose.Happy:
                    Set("body", 32f, -0.07f);
                    Set("legBL", 88f); Set("legBR", 88f);
                    Set("head", pose == Pose.Groom ? -30f + 10f * Mathf.Sin(clock * 6f) : -8f);
                    Set("tail", 0f, 0f, pose == Pose.Happy ? 38f * Mathf.Sin(clock * 14f) : tailSway);
                    break;
                case Pose.Sleep:
                    Set("body", 0f, -0.09f);
                    Set("legFL", 80f); Set("legFR", 80f); Set("legBL", 80f); Set("legBR", 80f);
                    Set("head", -30f + 2f * Mathf.Sin(clock * 1.4f));
                    Set("tail", 0f, 0f, 55f);
                    break;
                default:
                    Set("body", 0f, Mathf.Sin(clock * 1.8f) * 0.004f);
                    Set("tail", 0f, 0f, tailSway);
                    break;
            }
        }

        /// <summary>How high the pelvis (people) or body (pets) hangs above the figure's origin in each pose, unscaled.</summary>
        public float HipHeight => pose switch
        {
            Pose.Sit => 0.45f,
            Pose.Crouch => 0.53f,
            _ => 0.95f,
        };
    }
}
