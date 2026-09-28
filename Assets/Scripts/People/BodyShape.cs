using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Body sliders for the realistic MPFB2 people (docs/CHARACTER_PLAN.md). Each slider mixes two shape keys baked by
    /// tools/blender_mpfb_body.py (-1 = the low end, 0 = neutral, +1 = the high end) on every skinned mesh (body, eyes,
    /// brows, lashes, teeth), moves the bones by what the .bodyshape.json says so joints stay inside the reshaped body,
    /// and rebuilds the skin binding (bind poses) to match. Change the sliders, then call <see cref="Apply"/>.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class BodyShape : MonoBehaviour
    {
        [System.Serializable] public class Move { public string key; public float[] d; }
        [System.Serializable] public class BoneData { public string name; public float[] head; public Move[] moves; }
        [System.Serializable] public class SliderData { public string name, low, high; }
        [System.Serializable] public class Data { public SliderData[] sliders; public BoneData[] bones; }

        public TextAsset shapeData;
        [Range(-1f, 1f)] public float gender, weight, muscle, height, proportions, age;

        Data data;
        Transform root;
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Matrix4x4> restRoot = new Dictionary<Transform, Matrix4x4>();   // bone rest pose in root space
        readonly List<Transform> order = new List<Transform>();                                         // parents before children
        readonly List<SkinnedMeshRenderer> skins = new List<SkinnedMeshRenderer>();
        bool captured;

        public float Get(string slider)
        {
            switch (slider)
            {
                case "gender": return gender; case "weight": return weight; case "muscle": return muscle;
                case "height": return height; case "proportions": return proportions; case "age": return age;
            }
            return 0f;
        }

        public void Set(string slider, float v)
        {
            v = Mathf.Clamp(v, -1f, 1f);
            switch (slider)
            {
                case "gender": gender = v; break; case "weight": weight = v; break; case "muscle": muscle = v; break;
                case "height": height = v; break; case "proportions": proportions = v; break; case "age": age = v; break;
            }
        }

        void Awake() { Capture(); if (shapeData) Apply(); }

        /// <summary>Remembers the rest pose; must run before anything poses the skeleton (hence the early execution order).</summary>
        void Capture()
        {
            if (captured) return;
            captured = true;
            root = transform;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!r.sharedMesh) continue;
                r.sharedMesh = Instantiate(r.sharedMesh);        // own bind poses per person
                skins.Add(r);
                foreach (var b in r.bones) if (b && !restRoot.ContainsKey(b)) restRoot[b] = root.worldToLocalMatrix * b.localToWorldMatrix;
            }
            foreach (var b in restRoot.Keys) bones[b.name] = b;
            foreach (var b in root.GetComponentsInChildren<Transform>(true)) if (restRoot.ContainsKey(b)) order.Add(b);   // hierarchy order
        }

        static Vector3 BlenderToUnity(float[] d) => new Vector3(-d[0], d[2], -d[1]);   // same axes as FBX export in the tools

        public void Apply()
        {
            Capture();
            if (data == null && shapeData) data = JsonUtility.FromJson<Data>(shapeData.text);
            if (data == null) return;

            // shape keys: low end below zero, high end above
            var weights = new Dictionary<string, float>();
            foreach (var s in data.sliders)
            {
                float v = Get(s.name);
                weights[s.low] = Mathf.Max(0f, -v);
                weights[s.high] = Mathf.Max(0f, v);
            }
            foreach (var r in skins)
            {
                var m = r.sharedMesh;
                foreach (var kv in weights)
                {
                    int i = m.GetBlendShapeIndex(kv.Key);
                    if (i >= 0) r.SetBlendShapeWeight(i, kv.Value * 100f);
                }
            }

            // new rest positions of the bones, in root space
            var newPos = new Dictionary<Transform, Vector3>();
            foreach (var b in order) newPos[b] = (Vector3)restRoot[b].GetColumn(3);
            foreach (var bd in data.bones)
            {
                if (!bones.TryGetValue(bd.name, out var b) || bd.moves == null) continue;
                var off = Vector3.zero;
                foreach (var mv in bd.moves)
                    if (weights.TryGetValue(mv.key, out float w) && w > 0f) off += BlenderToUnity(mv.d) * w;
                newPos[b] += off;
            }

            // move the bones (positions only: rotations belong to the animation) and rebuild the bind poses
            var newRest = new Dictionary<Transform, Matrix4x4>();
            foreach (var b in order)
            {
                var m = restRoot[b];
                m.SetColumn(3, new Vector4(newPos[b].x, newPos[b].y, newPos[b].z, 1f));
                newRest[b] = m;
                var parentRest = b.parent && newRest.TryGetValue(b.parent, out var pm) ? pm : (b.parent ? root.worldToLocalMatrix * b.parent.localToWorldMatrix : Matrix4x4.identity);
                b.localPosition = parentRest.inverse.MultiplyPoint3x4(newPos[b]);
            }
            foreach (var r in skins)
            {
                var m = r.sharedMesh;
                var bind = m.bindposes;
                var smrToRoot = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int i = 0; i < r.bones.Length && i < bind.Length; i++)
                    if (r.bones[i] && newRest.TryGetValue(r.bones[i], out var br)) bind[i] = br.inverse * smrToRoot;
                m.bindposes = bind;
            }

            var rig = GetComponent<CharacterRig>();
            if (rig && bones.TryGetValue("pelvis", out var pelvis)) rig.RefreshRest(newPos[pelvis].y);
        }
    }
}
