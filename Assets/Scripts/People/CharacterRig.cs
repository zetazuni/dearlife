using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Poses and animates people and pets. The realistic MPFB2 people play motion capture on a Humanoid Animator, with
    /// seats and feet fitted here; the pets are posed on their own skeletons by <see cref="PetBody"/>. Joint angles are
    /// given as "forward" or "bend" values and turned into rotations about the figure's own right and up axes, so it does
    /// not matter how the FBX importer oriented the joints.
    /// </summary>
    public class CharacterRig : MonoBehaviour
    {
        public enum Pose { Stand, Walk, Sit, Lie, Wave, Crouch, Sleep, Groom, Happy, Eat, Cook, Read, Exercise, Work, Wash, Swim, Drink, Guitar, Keys, Dance, Talk }

        public bool pet;
        [Tooltip("person, cat or dog: which skeleton this is")]
        public string kind = "person";
        public float RestHip { get; private set; } = 0.95f;   // pelvis height above the feet at rest, measured from the model
        [Tooltip("parts whose material name contains this are not drawn (once used to hide a model's glasses)")]
        public string hideMaterial = "";
        public Pose pose = Pose.Stand;
        [System.NonSerialized] public UseSpot seat;   // the piece being sat or lain on: the pose follows its shape
        public float walkSpeed = 1f;       // metres per second, drives the stride
        [System.NonSerialized] public float inPlaceSpeed;   // walking without moving (a treadmill): the stride speed to show

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
        PetBody quad;

        // people play motion capture through a Humanoid Animator (docs/CHARACTER_PLAN.md, phase 2)
        Animator anim;
        bool animated;
        // metres a second the walks cover at normal playback on a person whose hips rest 0.95 m high, measured in the game
        // from how fast a planted foot moves back under the body: the masculine one and the feminine one
        const float WalkClipSpeedM = 0.97f, WalkClipSpeedF = 1.21f;
        float feminine = -1f;
        float phase, clock, amp = 1f, speedSmooth;
        Vector3 lastPos;

        // our joint name -> Unity's humanoid bone: any rigged person works, whatever its own bones are called (v0.49.0)
        static readonly Dictionary<string, HumanBodyBones> HumanJoints = new Dictionary<string, HumanBodyBones>
        {
            { "pelvis", HumanBodyBones.Hips }, { "spine", HumanBodyBones.Chest }, { "neck", HumanBodyBones.Neck },
            { "arm.L", HumanBodyBones.LeftUpperArm }, { "forearm.L", HumanBodyBones.LeftLowerArm },
            { "arm.R", HumanBodyBones.RightUpperArm }, { "forearm.R", HumanBodyBones.RightLowerArm },
            { "leg.L", HumanBodyBones.LeftUpperLeg }, { "shin.L", HumanBodyBones.LeftLowerLeg },
            { "leg.R", HumanBodyBones.RightUpperLeg }, { "shin.R", HumanBodyBones.RightLowerLeg },
            { "foot.L", HumanBodyBones.LeftFoot }, { "foot.R", HumanBodyBones.RightFoot },
        };

        static readonly string[] PersonJoints = { "pelvis", "spine", "neck", "arm.L", "forearm.L", "arm.R", "forearm.R", "leg.L", "shin.L", "leg.R", "shin.R", "foot.L", "foot.R" };
        void Awake()
        {
            lastPos = transform.position;
            // the realistic pets (tools/blender_pets.py) are posed on their own skeletons
            if (pet) quad = PetBody.Make(transform, kind);
            if (quad != null)
            {
                foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
                return;
            }
            if (pet) return;   // a pet model without its skeleton
            anim = GetComponent<Animator>();
            bool human = anim && anim.avatar && anim.avatar.isHuman;
            foreach (var n in PersonJoints)
            {
                var t = human ? anim.GetBoneTransform(HumanJoints[n]) : FindDeep(transform, n);
                if (!t && human && n == "spine") t = anim.GetBoneTransform(HumanBodyBones.Spine);
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
            if (human)
            {
                LowerArms();
                if (!anim.runtimeAnimatorController) anim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animation/People");
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animated = anim.runtimeAnimatorController != null;
                FindHands();
                var foot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (foot) ankleHeight = Mathf.Max(0.03f, foot.position.y - transform.position.y);   // still at rest here
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
        /// Most skeletons rest in an "A" or a "T" pose (arms out). Swing each upper arm down to hang 10 degrees
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

        /// <summary>The bones were moved: take their new places as the rest positions.</summary>
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
                {
                    if (fr == null) { Destroy(holder); return; }
                    holder.transform.SetParent(fr.t, false);
                    holder.transform.localScale = Vector3.one * Inv(fr.t);
                    holder.transform.localPosition = fr.anchor;
                    holder.transform.localRotation = fr.frame;
                    // in the right hand itself where its fingers are known (v0.52.0): hung on the forearm it sat a hand's
                    // length from the hand of anyone whose arm is not the length it was made for
                    var grip = hands != null ? System.Array.Find(hands, x => x.right) : null;
                    var thumb = anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightThumbProximal) : null;
                    if (grip != null && thumb)
                    {
                        var thumbSide = Vector3.Cross(grip.fingers, grip.palm).normalized;         // across the hand: to the thumb or away from it
                        if (Vector3.Dot(thumbSide, grip.hand.InverseTransformPoint(thumb.position)) < 0f) thumbSide = -thumbSide;
                        float handScale = Inv(grip.hand);
                        holder.transform.SetParent(grip.hand, false);
                        holder.transform.localScale = Vector3.one * handScale;
                        holder.transform.localRotation = Quaternion.LookRotation(grip.fingers, thumbSide);   // the mug stands up along the thumb side
                        holder.transform.localPosition = (grip.fingers * 0.085f + grip.palm * 0.05f - thumbSide * 0.05f) * handScale;
                    }
                    Bit(holder.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.045f, 0f), new Vector3(0.075f, 0.045f, 0.075f), new Color(0.95f, 0.94f, 0.9f), null, 0.8f);
                    Bit(holder.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.085f, 0f), new Vector3(0.062f, 0.004f, 0.062f), new Color(0.32f, 0.18f, 0.1f), null, 0.9f);
                    break;
                }
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

        /// <summary>A pet's body centre (between hips and shoulders) in the current pose, or the figure's position.</summary>
        public Vector3 BodyCentre => quad != null ? quad.BodyCentre : transform.position;

        /// <summary>The top of the head, from the neck joint (works sitting and lying too).</summary>
        public Vector3 HeadTop
        {
            get
            {
                if (quad != null) return quad.HeadTop;
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
            if (quad != null) { quad.Tick(pose == Pose.Walk && speedSmooth < 0.05f ? Pose.Stand : pose, clock, speedSmooth, moved, Time.deltaTime); return; }
            if (animated)
            {
                squat = Mathf.MoveTowards(squat, pose == Pose.Crouch && !OnSeat ? 1f : 0f, Time.deltaTime / 0.5f);
                if (pose == Pose.Walk && speedSmooth > 0.3f) work = HandWork.None;   // walked off without saying so
                // standing still with a walk order would march on the spot: stand instead
                float shown = inPlaceSpeed > 0f ? inPlaceSpeed : speedSmooth;
                var p = pose == Pose.Walk && shown < 0.05f ? Pose.Stand : pose;
                anim.SetInteger("pose", (int)p);
                if (feminine < 0f || Time.frameCount % 30 == 0)
                {
                    // from the model: 1 walks the feminine walk, 0 the masculine one
                    var model = GetComponent<PersonModel>();
                    feminine = model ? Mathf.Clamp01(model.feminine) : 0.5f;
                    anim.SetFloat("feminine", feminine);
                }
                float clipSpeed = Mathf.Lerp(WalkClipSpeedM, WalkClipSpeedF, feminine) * (RestHip / 0.95f);   // longer legs, longer strides
                anim.SetFloat("walkSpeed", Mathf.Clamp(shown / (clipSpeed * Mathf.Max(transform.lossyScale.y, 0.01f)), 0.4f, 1.8f));
                return;
            }
            foreach (var jt in j.Values) { jt.tgt = 0f; jt.liftTgt = 0f; jt.yawTgt = 0f; }
            PersonTargets();

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

        float hipAverage = -1f;

        /// <summary>
        /// Walking (v0.50.0): the walks are hand made cycles with the feet already where they belong. The feet follow the
        /// cycle's own foot goals, which keeps the soles on the floor on a body with other proportions than the one the
        /// cycle was made on, and the rise and fall of the hips is eased a little round its running average.
        /// </summary>
        void WalkIK()
        {
            var body = anim.bodyPosition;
            float y = body.y - transform.position.y;
            hipAverage = hipAverage < 0f ? y : Mathf.Lerp(hipAverage, y, 1f - Mathf.Exp(-Time.deltaTime / 0.45f));
            body.y = transform.position.y + hipAverage + (y - hipAverage) * 0.8f;
            anim.bodyPosition = body;
            foreach (var goal in new[] { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot })
            {
                anim.SetIKPosition(goal, anim.GetIKPosition(goal));
                anim.SetIKPositionWeight(goal, 1f);
            }
        }

        /// <summary>
        /// Standing poses (v0.49.0): a recording retargeted onto a body with other proportions leaves the soles a few
        /// centimetres under or over the floor. Both feet are shifted together so the lower one rests on it at the ankle
        /// height the model has at rest (high heels included); a foot the recording lifts stays lifted.
        /// </summary>
        void GroundIK()
        {
            if (ankleHeight < 0f) return;
            var l = anim.GetIKPosition(AvatarIKGoal.LeftFoot);
            var r = anim.GetIKPosition(AvatarIKGoal.RightFoot);
            float dy = Mathf.Clamp(transform.position.y + ankleHeight - Mathf.Min(l.y, r.y), -0.08f, 0.08f);
            l.y += dy; r.y += dy;
            float down = Mathf.SmoothStep(0f, 1f, squat);
            // a narrow stance (v0.50.0): recordings of someone stirring or washing stand with the feet far apart. Each foot
            // stays within a hand's width of the line under the body, closer still for a feminine body; dancing and
            // exercise keep their own steps
            if (pose != Pose.Dance && pose != Pose.Exercise)
            {
                float widest = Mathf.Lerp(0.12f, 0.075f, feminine < 0f ? 0.5f : feminine) * transform.lossyScale.y * (1f + 0.6f * down);   // a squat stands wider
                var right = transform.right;
                var centre = anim.bodyPosition;
                float sl = Vector3.Dot(l - centre, right), sr = Vector3.Dot(r - centre, right);
                l += right * (Mathf.Clamp(sl, -widest, -0.03f) - sl);
                r += right * (Mathf.Clamp(sr, 0.03f, widest) - sr);
            }
            StraightenLegs();
            float floor = transform.position.y + ankleHeight;
            foreach (var (goal, hint, p) in new[] { (AvatarIKGoal.LeftFoot, AvatarIKHint.LeftKnee, l), (AvatarIKGoal.RightFoot, AvatarIKHint.RightKnee, r) })
            {
                anim.SetIKPosition(goal, p);
                anim.SetIKPositionWeight(goal, 1f);
                // squatting: the knees go forward and a little out
                anim.SetIKHintPosition(hint, p + transform.forward * 0.6f + Vector3.up * 0.4f + transform.right * (hint == AvatarIKHint.LeftKnee ? -0.12f : 0.12f));
                anim.SetIKHintPositionWeight(hint, down);
                // a foot on the floor lies flat on it (as it does at rest), turned the way the recording points it; one
                // that is lifted keeps the recording's tilt
                var fwd = Vector3.ProjectOnPlane(anim.GetIKRotation(goal) * Vector3.forward, Vector3.up);
                if (fwd.sqrMagnitude < 1e-4f) continue;
                anim.SetIKRotation(goal, Quaternion.LookRotation(fwd, Vector3.up));
                anim.SetIKRotationWeight(goal, Mathf.Clamp01(1f - (p.y - floor) / 0.06f));
            }
        }

        float squat, legLift, tableLean;

        /// <summary>
        /// Crouching (v0.52.0): the recording used before was the still moment of picking something up, a stiff bow with
        /// straight legs. Now the body of the standing clip is lowered and tipped forward a little while the feet stay
        /// on the floor (<see cref="GroundIK"/>), so the knees bend into a real squat. Eased, so getting down and up is
        /// a movement.
        /// </summary>
        void Squat()
        {
            if (squat <= 0.001f) return;
            float e = Mathf.SmoothStep(0f, 1f, squat), s = transform.lossyScale.y;
            var body = anim.bodyPosition;
            body.y -= e * 0.40f * (RestHip / 0.95f) * s;
            body -= transform.forward * (e * 0.10f * s);
            anim.bodyPosition = body;
            anim.bodyRotation = Quaternion.AngleAxis(e * 22f, transform.right) * anim.bodyRotation;
        }

        /// <summary>
        /// Standing up straight (v0.52.0): with the feet brought under the body and a recording made on other proportions
        /// the hips end up too low and the knees stay bent the whole time someone cooks or washes. After each frame the
        /// straighter leg is measured (<see cref="MeasureLegs"/>) and the body is raised a little more, or less, the next
        /// frame, until that leg is nearly straight. Not while squatting, dancing or exercising.
        /// </summary>
        void StraightenLegs()
        {
            if (Mathf.Abs(legLift) < 0.002f) return;
            var body = anim.bodyPosition; body.y += legLift; anim.bodyPosition = body;
        }

        bool Tall => animated && !OnSeat && squat <= 0.001f && (pose != Pose.Walk || speedSmooth <= 0.05f) && pose != Pose.Exercise && pose != Pose.Dance
                     && pose != Pose.Happy && pose != Pose.Crouch && pose != Pose.Swim && pose != Pose.Lie && pose != Pose.Sleep && pose != Pose.Sit;

        void MeasureLegs()
        {
            if (!animated) return;
            float s = transform.lossyScale.y;
            if (!Tall) { legLift = Mathf.MoveTowards(legLift, 0f, Time.deltaTime * 0.4f); return; }
            float straightest = 0f, length = 0f;
            foreach (var (up, lo, ft) in new[] { (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot) })
            {
                var a = anim.GetBoneTransform(up); var b = anim.GetBoneTransform(lo); var c = anim.GetBoneTransform(ft);
                if (!a || !b || !c) return;
                float len = Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position);
                if (len < 1e-4f) return;
                float ext = Vector3.Distance(a.position, c.position) / len;
                if (ext > straightest) { straightest = ext; length = len; }
            }
            legLift = Mathf.Clamp(legLift + (0.975f - straightest) * length * Mathf.Min(1f, Time.deltaTime * 12f), -0.03f * s, 0.16f * s);
        }

        void OnAnimatorIK(int layer)
        {
            LegsIK();
            HandsIK();
        }

        void LegsIK()
        {
            if (pose == Pose.Walk && speedSmooth > 0.05f && !OnSeat) { WalkIK(); return; }
            hipAverage = -1f;
            if (!OnSeat) { if (pose != Pose.Swim) { Squat(); GroundIK(); } return; }
            if (offsetFor != seat) { seatOffset = Vector3.zero; offsetFor = seat; kneeKnown = false; }
            anim.bodyPosition += transform.TransformVector(seatOffset);
            if (pose != Pose.Sit) return;
            // at a table or a desk the back is upright: the sitting clip leans forward, and the edge of the table would
            // go through the chest. Found a little at a time from how far the torso leant last frame
            var sp = anim.GetBoneTransform(HumanBodyBones.Spine); var nk = anim.GetBoneTransform(HumanBodyBones.Neck);
            if (sp && nk)
            {
                // (in the IK pass the bones show this frame's clip pose, before anything here is applied)
                var torso = nk.position - sp.position;
                float lean = Vector3.Angle(torso, Vector3.up) * Mathf.Sign(Vector3.Dot(torso, transform.forward));
                // (anywhere else the recording's slump forward is taken out too, down to the slant of the backrest)
                float allowed = work != HandWork.None ? 3f : Mathf.Max(-10f, 5f - seat.recline * 0.6f);
                tableLean = Mathf.MoveTowards(tableLean, Mathf.Clamp(lean - allowed, 0f, 45f), Time.deltaTime * 60f);
            }
            else tableLean = Mathf.MoveTowards(tableLean, 0f, Time.deltaTime * 40f);
            if (tableLean > 0.05f) anim.bodyRotation = Quaternion.AngleAxis(-tableLean, transform.right) * anim.bodyRotation;
            if (ankleHeight < 0f) ankleHeight = 0.075f * transform.lossyScale.y;
            float footY = seat.FloorY + seat.footY + ankleHeight;
            var goals = new[] { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
            var hints = new[] { AvatarIKHint.LeftKnee, AvatarIKHint.RightKnee };
            var fwd0 = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            for (int i = 0; i < 2; i++)
            {
                var goal = goals[i];
                var p = anim.GetIKPosition(goal);
                // the knee straight ahead of its hip joint at the thigh's length (a hair wider than the hips), the foot under
                // it with the shin leaning by the seat's shin angle. Placing the foot under the last frame's knee fed back
                // on itself: the legs drifted round and splayed to one side (v0.48.0)
                if (kneeKnown)
                {
                    var hip = kneeLocal[i + 2];
                    float thigh = Vector3.Distance(hip, kneeLocal[i]);
                    var kneeL = new Vector3(hip.x * 1.25f, Mathf.Min(hip.y, kneeLocal[i].y), hip.z + thigh * 0.97f);
                    var knee = transform.TransformPoint(kneeL);
                    p = new Vector3(knee.x, footY, knee.z) + fwd0 * (Mathf.Tan(seat.shinAngle * Mathf.Deg2Rad) * Mathf.Max(0f, knee.y - footY));
                    anim.SetIKHintPosition(hints[i], knee + fwd0 * 0.3f);
                    anim.SetIKHintPositionWeight(hints[i], 1f);
                }
                anim.SetIKPosition(goal, new Vector3(p.x, footY, p.z));
                anim.SetIKPositionWeight(goal, 1f);
                // flat on the floor, pointing where the knee points
                var fwd = Vector3.ProjectOnPlane(anim.GetIKRotation(goal) * Vector3.forward, Vector3.up);
                if (fwd.sqrMagnitude > 1e-4f) { anim.SetIKRotation(goal, Quaternion.LookRotation(fwd, Vector3.up)); anim.SetIKRotationWeight(goal, 0.7f); }
            }
        }

        // ------------------------------------------------------------ hands that do something

        /// <summary>What the hands are busy with (v0.52.0): the recordings were made in an empty room, so someone
        /// "cooking" stirred the air in front of their chest half a metre from the counter. <see cref="Character"/> finds
        /// the real surface and says what is done there; the hands are put on it and moved by a little.</summary>
        public enum HandWork
        {
            None,
            Surface,    // both hands just over a worktop, one stirring
            Type,       // both hands on a desk, fingers busy
            Eat,        // one hand on the table, the other between the plate and the mouth
            Board,      // the right hand writing on something upright
            Reach,      // both hands held out to a point: a drawer, a washing machine's door, a fire, a pet
            Punch,      // fists up, one then the other thrown at the point (the punch bag)
        }

        [System.NonSerialized] public HandWork work;
        [System.NonSerialized] public Vector3 workPoint;       // on the surface, in front of the middle of the body
        HandWork workShown;
        bool lapShown;
        float workBlend;
        readonly float[] clearW = new float[2];

        // where the shoulders, the head and the hips ended up last frame. In the IK pass the bones do not show that: they
        // show this frame's clip pose before the seat, the squat or anything else here has moved the body (on a bar stool
        // that is 80 cm higher), so what is measured from the body there uses these
        readonly Vector3[] shoulderSeen = new Vector3[2];
        readonly float[] armSeen = new float[2];
        Vector3 headSeen, hipsSeen;
        bool seen;

        void RememberPose()
        {
            if (!animated) return;
            var head = anim.GetBoneTransform(HumanBodyBones.Head); var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (!head || !hips) return;
            headSeen = head.position; hipsSeen = hips.position;
            for (int side = 0; side < 2; side++)
            {
                var a = anim.GetBoneTransform(ArmBones[side][0]); var b = anim.GetBoneTransform(ArmBones[side][1]); var c = anim.GetBoneTransform(ArmBones[side][2]);
                if (!a || !b || !c) return;
                shoulderSeen[side] = a.position;
                armSeen[side] = Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position);
            }
            seen = true;
        }

        public void Work(HandWork kind, Vector3 point) { work = kind; workPoint = point; }
        public void NoWork() { work = HandWork.None; }

        /// <summary>How far the body and its clothes reach forward of the hip bone at the chest and belly, in metres.</summary>
        public float FrontDepth
        {
            get
            {
                if (!model) model = GetComponent<PersonModel>();
                return (model ? model.FrontAbove(RestHip) : 0.15f) * transform.lossyScale.y;
            }
        }

        bool Upright => !OnSeat && pose != Pose.Sit && pose != Pose.Lie && pose != Pose.Sleep && pose != Pose.Swim && pose != Pose.Crouch && squat <= 0.001f;

        bool HandTarget(int side, out Vector3 t)
        {
            float s = transform.lossyScale.y, sign = side == 0 ? -1f : 1f, c = clock;
            var right = transform.right; var up = Vector3.up;
            var fwd = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            t = default;
            if (lapShown)
            {
                // sitting with nothing to do: the hands lie on the thighs, inside the arms of the chair
                t = transform.TransformPoint(Vector3.Lerp(kneeLocal[side + 2], kneeLocal[side], 0.55f)) + up * (0.105f * s);
                return true;
            }
            switch (workShown)
            {
                case HandWork.Surface:
                    t = workPoint + right * (sign * 0.16f * s) + up * (0.075f * s);
                    if (side == 1) t += (right * Mathf.Cos(c * 4.2f) + fwd * Mathf.Sin(c * 4.2f)) * (0.035f * s);
                    else t += fwd * (0.02f * s * Mathf.Sin(c * 1.3f));
                    return true;
                case HandWork.Type:
                    t = workPoint + right * ((sign * 0.12f + 0.03f * Mathf.Sin(c * 0.7f + side)) * s) + up * ((0.05f + 0.012f * Mathf.Max(0f, Mathf.Sin(c * 9f + side * 2.1f))) * s);
                    return true;
                case HandWork.Eat:
                {
                    var rest = workPoint + right * (sign * 0.17f * s) + up * (0.05f * s);
                    t = rest;
                    if (side == 0) return true;
                    float bite = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Sin(c * 1.4f) * 1.6f - 0.3f));
                    var mouth = seen ? headSeen + fwd * (0.16f * s) - up * (0.04f * s) + right * (0.03f * s) : rest + up * (0.45f * s);   // the wrist, under the mouth
                    t = Vector3.Lerp(rest, mouth, bite) + up * (Mathf.Sin(bite * Mathf.PI) * 0.05f * s);
                    return true;
                }
                case HandWork.Board:
                    if (side == 0) return false;
                    t = workPoint + right * (0.2f * s * Mathf.Sin(c * 0.9f)) + up * (0.11f * s * Mathf.Sin(c * 1.7f));
                    return true;
                case HandWork.Punch:
                {
                    // the guard in front of the chin, and a jab every other beat, left then right
                    var guard = (seen ? shoulderSeen[side] : workPoint) + fwd * (0.20f * s) + up * (0.07f * s) - right * (sign * 0.07f * s);
                    float jab = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Sin(c * 5.5f + side * Mathf.PI) * 2.2f - 1.1f));
                    t = Vector3.Lerp(guard, workPoint + right * (sign * 0.05f * s), jab);
                    return true;
                }
                case HandWork.Reach:
                    t = workPoint + right * (sign * 0.11f * s) + up * (0.02f * s * Mathf.Sin(c * 2.2f + side * 1.4f)) + fwd * (0.02f * s * Mathf.Sin(c * 1.5f + side));
                    return true;
            }
            return false;
        }

        /// <summary>A hand in front of the belly or the chest that would be inside the body (a mug lifted to drink on
        /// someone with a big chest): moved forward to just outside it.</summary>
        bool OutOfChest(ref Vector3 p)
        {
            if (!model) model = GetComponent<PersonModel>();
            if (!model || !seen) return false;
            var c = transform.InverseTransformPoint(hipsSeen);
            var l = transform.InverseTransformPoint(p);
            if (l.y < c.y) return false;
            if (!model.FrontEdge(l.y, l.x - c.x, heldProp ? 0.10f : 0.055f, out float edge)) return false;
            float z = l.z - c.z;
            if (z >= edge || z < -0.02f) return false;
            l.z = c.z + edge;
            p = transform.TransformPoint(l);
            return true;
        }

        void HandsIK()
        {
            bool lap = work == HandWork.None && OnSeat && pose == Pose.Sit && kneeKnown && !heldProp;
            bool any = work != HandWork.None || lap;
            if (any) { workShown = work; lapShown = lap; }
            workBlend = Mathf.MoveTowards(workBlend, any ? 1f : 0f, Time.deltaTime / 0.45f);
            float e = Mathf.SmoothStep(0f, 1f, workBlend), s = transform.lossyScale.y;
            bool upright = Upright;
            var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            for (int side = 0; side < 2; side++)
            {
                var goal = side == 0 ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
                var hint = side == 0 ? AvatarIKHint.LeftElbow : AvatarIKHint.RightElbow;
                var p = anim.GetIKPosition(goal);
                float w = 0f;
                Vector3 t = default;
                bool busy = e > 0f && HandTarget(side, out t);
                if (busy && seen)
                {
                    // never a stiff straight arm: what is too far away is reached for, not reached
                    float most = armSeen[side] * (lapShown || workShown == HandWork.Punch ? 0.97f : 0.86f);
                    var v = t - shoulderSeen[side];
                    if (v.magnitude > most) t = shoulderSeen[side] + v.normalized * most;
                }
                if (busy) { p = Vector3.Lerp(p, t, e); w = e; }
                bool pushed = upright && OutOfChest(ref p);
                clearW[side] = Mathf.MoveTowards(clearW[side], pushed ? 1f : 0f, Time.deltaTime / 0.2f);
                w = Mathf.Max(w, clearW[side]);
                anim.SetIKPosition(goal, p);
                anim.SetIKPositionWeight(goal, w);
                // the elbow hangs down beside the body, a little back
                if (busy && seen)
                {
                    anim.SetIKHintPosition(hint, shoulderSeen[side] + transform.right * ((side == 0 ? -0.22f : 0.22f) * s) - Vector3.up * (0.35f * s) - fwd * (0.12f * s));
                    anim.SetIKHintPositionWeight(hint, 0.6f * e);
                }
                else anim.SetIKHintPositionWeight(hint, 0f);
            }
        }

        // ------------------------------------------------------------ arms clear of the body

        PersonModel model;
        readonly float[] armOut = new float[2];
        static readonly HumanBodyBones[][] ArmBones =
        {
            new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleDistal },
            new[] { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleDistal },
        };

        /// <summary>
        /// The clips were made on slim bare bodies: on someone with broad hips, a flared skirt or a coat the hanging arm
        /// swings straight through the clothes (v0.52.0). The model knows its own outline (<see cref="PersonModel.BodyEdge"/>):
        /// whenever the forearm, the wrist or the fingers would be inside it, the whole arm is swung out from the shoulder
        /// by just enough, eased so it does not twitch.
        /// </summary>
        void ClearBody()
        {
            if (!animated) return;
            if (!model) model = GetComponent<PersonModel>();
            if (!model || model.bodyWide == null || model.bodyWide.Length == 0) return;
            bool upright = Upright;
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (!hips) return;
            var centre = transform.InverseTransformPoint(hips.position);
            for (int side = 0; side < 2; side++)
            {
                var upper = anim.GetBoneTransform(ArmBones[side][0]); var lower = anim.GetBoneTransform(ArmBones[side][1]); var hand = anim.GetBoneTransform(ArmBones[side][2]);
                if (!upper || !lower || !hand) continue;
                float sign = side == 0 ? -1f : 1f, need = 0f;
                if (upright)
                {
                    var shoulder = transform.InverseTransformPoint(upper.position);
                    var knuckle = anim.GetBoneTransform(ArmBones[side][3]); var tip = anim.GetBoneTransform(ArmBones[side][4]);
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 w; float thick;
                        if (i == 0) { w = Vector3.Lerp(lower.position, hand.position, 0.5f); thick = 0.05f; }
                        else if (i == 1) { w = hand.position; thick = 0.04f; }
                        else if (i == 2) { if (!knuckle) continue; w = knuckle.position; thick = 0.035f; }
                        else { if (!tip) continue; w = tip.position; thick = 0.025f; }
                        var l = transform.InverseTransformPoint(w);
                        if (l.y > shoulder.y - 0.12f) continue;          // a raised hand is not swung out sideways
                        if (!model.BodyEdge(l.y, l.z - centre.z, thick, out float edge)) continue;
                        float inside = edge - sign * (l.x - centre.x);
                        if (inside <= 0f) continue;
                        need = Mathf.Max(need, Mathf.Atan2(inside, Mathf.Max(0.15f, shoulder.y - l.y)) * Mathf.Rad2Deg);
                    }
                }
                armOut[side] = Mathf.Lerp(armOut[side], Mathf.Min(need, 34f), 1f - Mathf.Exp(-12f * Time.deltaTime));
                if (armOut[side] > 0.05f) upper.rotation = Quaternion.AngleAxis(armOut[side] * sign, transform.forward) * upper.rotation;
            }
        }

        // ------------------------------------------------------------ relaxed hands

        class HandRef { public Transform hand, forearm; public Vector3 fingers, palm; public bool right; }
        HandRef[] hands;
        float handRelax;

        // one joint of a finger: its rotation in the model's own rest pose, the axis it curls about (in its own space) and
        // how far it is curled already at rest
        class Knuckle { public Transform t; public Quaternion rest; public Vector3 axis, side; public float restCurl, restSpread; public int finger, joint; }
        Knuckle[] knuckles;
        float grip;

        // degrees of curl at the three joints of the index finger in a hand hanging loose and in one holding something;
        // each further finger closes a little more
        static readonly float[] LooseCurl = { 18f, 28f, 12f }, GripCurl = { 52f, 62f, 34f };

        static readonly HumanBodyBones[][] FingerBones =
        {
            new[] { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal },
            new[] { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
            new[] { HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
            new[] { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal },
            new[] { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
            new[] { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
            new[] { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
            new[] { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
        };
        static readonly HumanBodyBones[] ThumbBones =
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
        };

        /// <summary>Which way the fingers point and the palm faces, in each hand's own space, read from the finger bones.</summary>
        void FindHands()
        {
            var list = new List<HandRef>();
            foreach (var (h, f, index, middle, little, sign) in new[]
            {
                (HumanBodyBones.LeftHand, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftLittleProximal, 1f),
                (HumanBodyBones.RightHand, HumanBodyBones.RightLowerArm, HumanBodyBones.RightIndexProximal, HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightLittleProximal, -1f),
            })
            {
                var hand = anim.GetBoneTransform(h); var fore = anim.GetBoneTransform(f);
                var i = anim.GetBoneTransform(index); var m = anim.GetBoneTransform(middle); var l = anim.GetBoneTransform(little);
                if (!hand || !fore || !i || !m || !l) continue;
                var palm = Vector3.Cross(i.position - hand.position, l.position - hand.position) * sign;
                if (palm.sqrMagnitude < 1e-10f) continue;
                list.Add(new HandRef
                {
                    hand = hand, forearm = fore, right = sign < 0f,
                    fingers = hand.InverseTransformDirection((m.position - hand.position).normalized),
                    palm = hand.InverseTransformDirection(palm.normalized),
                });
            }
            hands = list.ToArray();
            FindKnuckles();
        }

        /// <summary>
        /// The fingers (v0.52.0). No clip moves them, and what Unity's humanoid system makes of an unanimated finger is a
        /// spread claw on every model (v0.50.0 and v0.51.0 tried to tame it with muscle values and did not). So the fingers
        /// are posed here on the model's own bones, from the model's own rest pose: the spread and the thumb stay as the
        /// artist made them, and each joint is curled about its own axis to the same few degrees on every model.
        /// </summary>
        void FindKnuckles()
        {
            var list = new List<Knuckle>();
            for (int f = 0; f < FingerBones.Length; f++)
            {
                var hand = anim.GetBoneTransform(f < 4 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                var href = System.Array.Find(hands, x => x.hand == hand);
                if (href == null) continue;
                var palm = hand.TransformDirection(href.palm);
                var before = hand.TransformDirection(href.fingers);      // the way the segment before this one points
                for (int k = 0; k < 3; k++)
                {
                    var t = anim.GetBoneTransform(FingerBones[f][k]);
                    if (!t) break;
                    var next = k < 2 ? anim.GetBoneTransform(FingerBones[f][k + 1]) : (t.childCount > 0 ? t.GetChild(0) : null);
                    // the last joint of most models has no bone after it, or an end bone that sits anywhere: it points
                    // on the way the one before it does
                    var dir = next && (next.position - t.position).sqrMagnitude > 1e-10f ? (next.position - t.position).normalized : before;
                    if (k == 2 && Vector3.Angle(dir, before) > 50f) dir = before;
                    var axis = Vector3.Cross(dir, palm);                 // turning about it brings the fingertip towards the palm
                    if (axis.sqrMagnitude < 1e-6f) break;
                    axis.Normalize();
                    list.Add(new Knuckle
                    {
                        t = t, rest = t.localRotation, axis = t.InverseTransformDirection(axis), side = t.InverseTransformDirection(palm), finger = f % 4, joint = k,
                        restCurl = Vector3.SignedAngle(Vector3.ProjectOnPlane(before, axis), Vector3.ProjectOnPlane(dir, axis), axis),
                        // models are made with the fingers fanned apart (easier to skin): how far this one is turned
                        // sideways from the line of the hand, at its first joint
                        restSpread = k == 0 ? Vector3.SignedAngle(Vector3.ProjectOnPlane(before, palm), Vector3.ProjectOnPlane(dir, palm), palm) : 0f,
                    });
                    before = dir;
                }
            }
            foreach (var b in ThumbBones)
            {
                var t = anim.GetBoneTransform(b);
                if (t) list.Add(new Knuckle { t = t, rest = t.localRotation, joint = -1 });
            }
            knuckles = list.ToArray();
        }

        /// <summary>How closed the hands are: loose most of the time, round whatever is held.</summary>
        float GripWanted()
        {
            if (heldProp) return 1f;
            switch (work)
            {
                case HandWork.Surface: return 0.55f;
                case HandWork.Type: return 0.2f;
                case HandWork.Eat: return 0.45f;
                case HandWork.Board: return 0.6f;
                case HandWork.Reach: return -0.3f;
                case HandWork.Punch: return 1f;
            }
            if (OnSeat && pose == Pose.Sit) return 0f;
            switch (pose)
            {
                case Pose.Cook: case Pose.Exercise: return 0.7f;        // a spoon, the bars of a machine
                case Pose.Wash: case Pose.Eat: case Pose.Drink: return 0.45f;
                case Pose.Work: case Pose.Keys: case Pose.Guitar: return 0.2f;   // fingers over the keys
                case Pose.Wave: case Pose.Swim: return -0.6f;           // an open hand
                default: return 0f;
            }
        }

        void PoseFingers()
        {
            if (!animated || knuckles == null) return;
            grip = Mathf.MoveTowards(grip, GripWanted(), Time.deltaTime / 0.35f);
            foreach (var k in knuckles)
            {
                if (!k.t) continue;
                if (k.joint < 0) { k.t.localRotation = k.rest; continue; }   // the thumb, as the model rests it
                float loose = LooseCurl[k.joint] + (k.joint == 2 ? 1.5f : 3f) * k.finger;
                float want = grip >= 0f ? Mathf.Lerp(loose, GripCurl[k.joint] + 2f * k.finger, grip) : Mathf.Lerp(loose, 3f, -grip);
                // the fan of the rest pose closed to a quarter (a hand at rest keeps its fingers together), opened again
                    // for an open hand
                float fan = Mathf.Clamp(k.restSpread * (grip < 0f ? Mathf.Lerp(0.25f, 0.8f, -grip) : 0.25f), -6f, 6f);
                k.t.localRotation = k.rest * Quaternion.AngleAxis(fan - k.restSpread, k.side) * Quaternion.AngleAxis(want - k.restCurl, k.axis);
            }
        }

        /// <summary>
        /// Standing and walking with nothing in the hands (v0.50.0): each hand hangs in line with its forearm, the palm
        /// turned to the hip, whatever the recording or the model's own rest pose does with the wrist. Eased in and out,
        /// so a hand that goes on to stir a pot or wave turns back smoothly.
        /// </summary>
        void RelaxHands()
        {
            if (!animated || hands == null || hands.Length == 0) return;
            bool want = (pose == Pose.Stand || pose == Pose.Walk) && !heldProp && !OnSeat && work == HandWork.None;
            handRelax = Mathf.MoveTowards(handRelax, want ? 1f : 0f, Time.deltaTime / 0.3f);
            float busy = Mathf.SmoothStep(0f, 1f, workBlend);
            if (handRelax <= 0f && busy <= 0f) return;
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (!hips) return;
            var fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            foreach (var h in hands)
            {
                var along = h.hand.position - h.forearm.position;
                if (along.sqrMagnitude < 1e-8f) continue;
                var own = Quaternion.LookRotation(h.fingers, h.palm);                // the hand's own fingers and palm
                if (busy > 0f)
                {
                    // a hand that works lies flat over what it works on (or against the board), fingers leading on from the forearm
                    Vector3 fingers, palm;
                    if (!lapShown && workShown == HandWork.Board) { if (!h.right) continue; fingers = Vector3.up + fwd * 0.6f; palm = fwd; }
                    else if (!lapShown && workShown == HandWork.Punch) continue;                 // a fist goes the way the arm goes
                    else if (!lapShown && workShown == HandWork.Eat && h.right) continue;      // the eating hand turns as the arm brings it
                    else { fingers = Vector3.ProjectOnPlane(along, Vector3.up).normalized + fwd * 0.7f; palm = Vector3.down; }
                    fingers = Vector3.ProjectOnPlane(fingers, palm);
                    if (fingers.sqrMagnitude > 1e-6f)
                        h.hand.rotation = Quaternion.Slerp(h.hand.rotation, Quaternion.LookRotation(fingers, palm) * Quaternion.Inverse(own), busy * 0.85f);
                    continue;
                }
                var inward = hips.position - h.hand.position; inward.y = 0f;
                inward = Vector3.ProjectOnPlane(inward, along);
                if (inward.sqrMagnitude < 1e-6f) continue;
                var goal = Quaternion.LookRotation(along, inward) * Quaternion.Inverse(own);
                h.hand.rotation = Quaternion.Slerp(h.hand.rotation, goal, handRelax * 0.9f);
            }
        }

        /// <summary>Whatever is left after the IK pass is corrected exactly, and fed back into the offset for the next frame.</summary>
        void LateUpdate()
        {
            ClearBody();
            RelaxHands();
            PoseFingers();
            MeasureLegs();
            if (!OnSeat) { RememberPose(); return; }
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (!hips) return;
            var err = seat.transform.position - hips.position;
            seatOffset += transform.InverseTransformVector(err);
            hips.position += err;
            var kl = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg); var kr = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            var hl = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg); var hr = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (kl && kr && hl && hr)
            {
                kneeLocal[0] = transform.InverseTransformPoint(kl.position); kneeLocal[1] = transform.InverseTransformPoint(kr.position);
                kneeLocal[2] = transform.InverseTransformPoint(hl.position); kneeLocal[3] = transform.InverseTransformPoint(hr.position);
                kneeKnown = true;
            }

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
                // the knees: bent down again after raised thighs (lying in a bath that is shorter than the legs), or the
                // lower legs raised further (the far end of a hammock). Small values were never shown and stay that way
                if (Mathf.Abs(seat.kneeBend) > 8f)
                    foreach (var (shin, foot) in new[] { (HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot), (HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot) })
                    {
                        var s = anim.GetBoneTransform(shin); var f = anim.GetBoneTransform(foot);
                        if (s && f) Lift(s, f.position - s.position, -seat.kneeBend);
                    }
            }
            RememberPose();
        }

        /// <summary>Turns a bone (and all below it) so the direction it points rises towards vertical by the given angle.</summary>
        static void Lift(Transform bone, Vector3 along, float degrees)
        {
            var axis = Vector3.Cross(along, Vector3.up);
            if (axis.sqrMagnitude < 1e-6f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis.normalized) * bone.rotation;
        }

        readonly Vector3[] kneeLocal = new Vector3[4];   // knees, then hip joints (left, right), last frame
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

        /// <summary>How high the pelvis (people) or body (pets) hangs above the figure's origin in each pose, unscaled.</summary>
        public float HipHeight => pose switch
        {
            Pose.Sit => 0.45f,
            Pose.Crouch => 0.53f,
            _ => 0.95f,
        };
    }
}
