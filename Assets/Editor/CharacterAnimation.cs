using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Phase 2 of docs/CHARACTER_PLAN.md: motion capture from the CMU library (Assets/Art/Animations/CMU, free to use and
    /// share, not to resell) retargeted onto the people through Unity's Humanoid system. Every skeleton gets an explicit
    /// bone map (CMU uses Daz style names; each person's map comes with the model, see PersonImport) and a T pose reference,
    /// because Humanoid treats the rest pose as zero: most models rest in an A pose, so without it every arm would come out
    /// 45 degrees off.
    /// </summary>
    public static class CharacterAnimation
    {
        public const string ClipDir = "Assets/Art/Animations/CMU";
        /// <summary>Walks and the idle (v0.50.0): baked onto one standard skeleton by tools/blender_anim.py, whose bones are
        /// named like Unity's humanoid bones, so the bone map is each name onto itself. The whole clip is used and loops.</summary>
        public const string BakedDir = "Assets/Art/Animations/BlendSwap";

        static readonly string[] StandardBones =
        {
            "Hips", "Spine", "Chest", "Neck", "Head",
            "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes", "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes",
        };

        static readonly Dictionary<string, string> CmuMap = new Dictionary<string, string>
        {
            { "Hips", "hip" }, { "Spine", "abdomen" }, { "Chest", "chest" }, { "Neck", "neck" }, { "Head", "head" },
            { "LeftShoulder", "lCollar" }, { "LeftUpperArm", "lShldr" }, { "LeftLowerArm", "lForeArm" }, { "LeftHand", "lHand" },
            { "RightShoulder", "rCollar" }, { "RightUpperArm", "rShldr" }, { "RightLowerArm", "rForeArm" }, { "RightHand", "rHand" },
            { "LeftUpperLeg", "lThigh" }, { "LeftLowerLeg", "lShin" }, { "LeftFoot", "lFoot" },
            { "RightUpperLeg", "rThigh" }, { "RightLowerLeg", "rShin" }, { "RightFoot", "rFoot" },
            { "LeftEye", "leftEye" }, { "RightEye", "rightEye" },
            { "Left Thumb Proximal", "lThumb1" }, { "Left Thumb Intermediate", "lThumb2" },
            { "Left Index Proximal", "lIndex1" }, { "Left Index Intermediate", "lIndex2" },
            { "Left Middle Proximal", "lMid1" }, { "Left Middle Intermediate", "lMid2" },
            { "Left Ring Proximal", "lRing1" }, { "Left Ring Intermediate", "lRing2" },
            { "Left Little Proximal", "lPinky1" }, { "Left Little Intermediate", "lPinky2" },
            { "Right Thumb Proximal", "rThumb1" }, { "Right Thumb Intermediate", "rThumb2" },
            { "Right Index Proximal", "rIndex1" }, { "Right Index Intermediate", "rIndex2" },
            { "Right Middle Proximal", "rMid1" }, { "Right Middle Intermediate", "rMid2" },
            { "Right Ring Proximal", "rRing1" }, { "Right Ring Intermediate", "rRing2" },
            { "Right Little Proximal", "rPinky1" }, { "Right Little Intermediate", "rPinky2" },
        };

        /// <summary>Makes a model Humanoid with the given bone map and a T pose (arms level, straight) as its reference.</summary>
        public static bool MakeHumanoid(string path, Dictionary<string, string> map, out string report)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.SaveAndReimport();                          // first pass fills in the skeleton list

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var inst = Object.Instantiate(prefab);
            var bones = new Dictionary<string, Transform>();
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            TPose(inst.transform, bones, map);

            var desc = imp.humanDescription;
            var human = new List<HumanBone>();
            foreach (var kv in map)
                if (bones.ContainsKey(kv.Value)) human.Add(new HumanBone { humanName = kv.Key, boneName = kv.Value, limit = new HumanLimit { useDefaultValues = true } });
            desc.human = human.ToArray();
            var skel = new List<SkeletonBone>();
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                string name = t == inst.transform ? prefab.name : t.name;
                skel.Add(new SkeletonBone { name = name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
            }
            desc.skeleton = skel.ToArray();
            Object.DestroyImmediate(inst);
            imp.humanDescription = desc;
            imp.SaveAndReimport();

            Avatar avatar = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Avatar a) avatar = a;
            report = avatar ? $"{path}: avatar valid={avatar.isValid} human={avatar.isHuman} bones={human.Count}" : $"{path}: no avatar";
            return avatar && avatar.isValid && avatar.isHuman;
        }

        /// <summary>
        /// Upper arms and forearms level and pointing straight out sideways, palms down, like Unity's "Enforce T-Pose". The
        /// body's own axes are used, not the root's: the CMU files' root is turned 270 degrees about X by the Z up conversion,
        /// so its "forward" points up.
        /// </summary>
        static void TPose(Transform root, Dictionary<string, Transform> bones, Dictionary<string, string> map)
        {
            Transform B(string human) => map.TryGetValue(human, out var n) && bones.TryGetValue(n, out var t) ? t : null;
            var hips = B("Hips"); var head = B("Head"); var lArm = B("LeftUpperArm"); var rArm = B("RightUpperArm");
            if (!hips || !head || !lArm || !rArm) return;
            var up = (head.position - hips.position).normalized;
            var right = Vector3.ProjectOnPlane(rArm.position - lArm.position, up).normalized;   // towards the character's right
            var forward = Vector3.Cross(right, up);
            foreach (var (side, sign) in new[] { ("Left", -1f), ("Right", 1f) })
            {
                if (!map.TryGetValue(side + "UpperArm", out var ua) || !bones.TryGetValue(ua, out var upper)) continue;
                if (!map.TryGetValue(side + "LowerArm", out var la) || !bones.TryGetValue(la, out var lower)) continue;
                if (!map.TryGetValue(side + "Hand", out var ha) || !bones.TryGetValue(ha, out var hand)) continue;
                var outward = right * sign;
                upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, outward) * upper.rotation;
                lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, outward) * lower.rotation;
                // the hand in line with the arm too: a model that rests with drooping wrists would otherwise carry that
                // droop into every pose (hands flared away from the body)
                if (map.TryGetValue(side + " Middle Proximal", out var mi) && bones.TryGetValue(mi, out var middle))
                    hand.rotation = Quaternion.FromToRotation(middle.position - hand.position, outward) * hand.rotation;
                // and the roll: palms down, thumbs forward. With the arm rolled 90 degrees every elbow bend of the recording
                // swings up instead of forward (a relaxed seated arm ended up in the air)
                if (map.TryGetValue(side + " Thumb Proximal", out var th) && bones.TryGetValue(th, out var thumb))
                {
                    var axis = outward.normalized;
                    var t = Vector3.ProjectOnPlane(thumb.position - hand.position, axis);
                    var want = Vector3.ProjectOnPlane(forward, axis);
                    if (t.sqrMagnitude > 1e-8f && want.sqrMagnitude > 1e-8f)
                        upper.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(t, want, axis), axis) * upper.rotation;
                }
            }
        }

        // ---------------------------------------------------------------- the clips and the controller

        public const string ControllerPath = "Assets/Resources/Animation/People.controller";

        /// <summary>
        /// Which part of each CMU recording is used (seconds, the files are 30 fps), found by charting hip height and how
        /// much the joints move over time: calm stretches for holds, one full stride for the walk (the left thigh leads at
        /// 1.2 s and again at 2.33 s). Frame 0 of every recording is a reference pose and is never used.
        /// </summary>
        static readonly (string clip, float from, float to)[] Segments =
        {
            // walking and standing are not CMU recordings any more (v0.50.0): see BakedDir
            ("sit", 6.0f, 10.4f), ("wave", 0.1f, 3.0f),
            // crouch (petting a pet, the laundry): the still moment of the pick up with the hands held low, 33 cm off the
            // floor (v0.48.0); the old 0.4 to 1.6 s went down and came back up, so the loop bent over again and again
            ("crouch", 1.4f, 1.8f), ("happy", 1.0f, 4.0f), ("eat", 0.1f, 8.4f), ("cook", 0.1f, 5.4f), ("read", 2.0f, 6.9f),
            ("exercise", 2.0f, 8.0f), ("work", 0.3f, 7.0f), ("wash", 1.0f, 10.0f), ("swim", 1.0f, 6.3f), ("drink", 1.0f, 9.0f),
            ("keys", 0.3f, 5.6f), ("dance", 7.5f, 18.5f), ("talk", 3.0f, 13.5f),
        };

        /// <summary>
        /// Every CharacterRig.Pose and the clip it plays (poses without a recording of their own borrow the closest one).
        /// Crouching is the standing clip: CharacterRig lowers the body into a squat itself (v0.52.0). Lying
        /// uses the calm standing clip: UseSpot.RootFor already tips the whole figure onto its back, so a standing hold becomes
        /// lying face up with the arms at the sides (a floor lying recording would be turned over twice).
        /// </summary>
        public static readonly (string pose, string clip)[] PoseClips =
        {
            ("Stand", "stand"), ("Walk", "walk"), ("Sit", "sit"), ("Lie", "stand"), ("Wave", "wave"), ("Crouch", "stand"),
            ("Sleep", "stand"), ("Groom", "stand"), ("Happy", "happy"), ("Eat", "eat"), ("Cook", "cook"), ("Read", "read"),
            ("Exercise", "exercise"), ("Work", "work"), ("Wash", "wash"), ("Swim", "swim"), ("Drink", "drink"),
            ("Guitar", "keys"), ("Keys", "keys"), ("Dance", "dance"), ("Talk", "talk"),
        };

        static void ConfigureClip(string path)
        {
            string key = System.IO.Path.GetFileName(path).Split('_')[0];
            var seg = System.Array.Find(Segments, s => s.clip == key);
            if (seg.clip == null) return;
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            var clip = new ModelImporterClipAnimation
            {
                name = key, takeName = imp.defaultClipAnimations.Length > 0 ? imp.defaultClipAnimations[0].takeName : key,
                firstFrame = seg.from * 30f, lastFrame = seg.to * 30f,
                loopTime = true, loopPose = key.StartsWith("walk"),
                // the body faces the character's forward, stays centred over it, and keeps its real height (sitting, lying)
                lockRootRotation = true, keepOriginalOrientation = false,
                lockRootHeightY = true, keepOriginalPositionY = true, heightFromFeet = false,
                lockRootPositionXZ = true, keepOriginalPositionXZ = false,
            };
            imp.clipAnimations = new[] { clip };
            imp.SaveAndReimport();
        }

        /// <summary>A baked clip (BakedDir): all of it but its first frame, looping, the body kept over the character like the recordings.</summary>
        static void ConfigureBaked(string path)
        {
            string key = System.IO.Path.GetFileName(path).Split('_')[0];
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            if (imp.defaultClipAnimations.Length == 0) return;
            var take = imp.defaultClipAnimations[0];
            var clip = new ModelImporterClipAnimation
            {
                // the file's first frame is the rest pose (the avatar's reference, see tools/blender_anim.py): not part of the clip
                name = key, takeName = take.takeName, firstFrame = take.firstFrame + 1f, lastFrame = take.lastFrame,
                loopTime = true, loopPose = true,
                lockRootRotation = true, keepOriginalOrientation = false,
                lockRootHeightY = true, keepOriginalPositionY = true, heightFromFeet = false,
                lockRootPositionXZ = true, keepOriginalPositionXZ = false,
            };
            imp.clipAnimations = new[] { clip };
            imp.SaveAndReimport();
        }

        static AnimationClip LoadClip(string key)
        {
            foreach (var guid in AssetDatabase.FindAssets(key + "_ t:Model", new[] { BakedDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileName(p).Split('_')[0] != key) continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                    if (o is AnimationClip c && c.name == key) return c;
            }
            foreach (var guid in AssetDatabase.FindAssets(key + "_cmu t:Model", new[] { ClipDir }))
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (o is AnimationClip c && c.name == key) return c;
            return null;
        }

        /// <summary>One state per pose, reached from anywhere when the "pose" parameter changes, with a short cross fade.</summary>
        static void BuildController()
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ControllerPath));
            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("pose", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("walkSpeed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("feminine", AnimatorControllerParameterType.Float);   // 0 a masculine walk, 1 a feminine one
            var sm = ctrl.layers[0].stateMachine;
            var poseNames = System.Enum.GetNames(typeof(CharacterRig.Pose));
            for (int i = 0; i < poseNames.Length; i++)
            {
                var pc = System.Array.Find(PoseClips, x => x.pose == poseNames[i]);
                var clip = LoadClip(pc.clip ?? "stand");
                var st = sm.AddState(poseNames[i], new Vector3(300f, 60f * i, 0f));
                st.motion = clip;
                if (poseNames[i] == "Walk")
                {
                    // the walk is a blend of the two walks by how feminine the body is
                    var tree = new UnityEditor.Animations.BlendTree { name = "Walk", blendType = UnityEditor.Animations.BlendTreeType.Simple1D, blendParameter = "feminine", useAutomaticThresholds = false };
                    AssetDatabase.AddObjectToAsset(tree, ctrl);
                    tree.AddChild(LoadClip("walk"), 0f);
                    tree.AddChild(LoadClip("walkf"), 1f);
                    st.motion = tree;
                }
                if (poseNames[i] == "Walk") { st.speedParameterActive = true; st.speedParameter = "walkSpeed"; }
                if (i == 0) sm.defaultState = st;
                var tr = sm.AddAnyStateTransition(st);
                tr.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, i, "pose");
                tr.duration = 0.25f; tr.hasExitTime = false; tr.canTransitionToSelf = false;
            }
            var layers = ctrl.layers;
            layers[0].iKPass = true;        // CharacterRig.OnAnimatorIK plants the feet and fits the pelvis to seats
            ctrl.layers = layers;
            // the fingers are not in the controller: CharacterRig poses them on the model's own bones (v0.52.0)
            AssetDatabase.DeleteAsset("Assets/Resources/Animation/RelaxedHands.anim");
            AssetDatabase.DeleteAsset("Assets/Resources/Animation/Fingers.mask");
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Dearlife/Set up character animation")]
        public static void SetUp()
        {
            var log = new System.Text.StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ClipDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                MakeHumanoid(p, CmuMap, out var r); log.AppendLine(r);
                ConfigureClip(p);
            }
            var same = new Dictionary<string, string>();
            foreach (var b in StandardBones) same[b] = b;
            if (AssetDatabase.IsValidFolder(BakedDir))
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { BakedDir }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    MakeHumanoid(p, same, out var r); log.AppendLine(r);
                    ConfigureBaked(p);
                }
            BuildController();
            Debug.Log("Dearlife: character animation set up.\n" + log);
        }
    }
}
