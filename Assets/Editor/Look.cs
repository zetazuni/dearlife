using System.IO;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Pictures of a person for checking by eye (v0.52.0): a camera of its own, nothing blurred, the same thing seen
    /// from several sides next to each other in one file. Used by <see cref="InteractionCheck"/>, and by hand from a
    /// script while looking for clipping.
    /// </summary>
    public static class Look
    {
        /// <summary>Views round a point, each "yaw" degrees round from the front of the given facing, into one PNG.</summary>
        public static void Save(string file, Vector3 at, Vector3 facing, float distance, float[] yaws, float pitch = 12f, int size = 512, float fov = 30f)
        {
            var go = new GameObject("Look camera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.enabled = false; cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 60f;
            var hd = go.AddComponent<HDAdditionalCameraData>();
            hd.customRenderingSettings = true;
            foreach (var f in new[] { FrameSettingsField.DepthOfField, FrameSettingsField.MotionBlur, FrameSettingsField.Vignette, FrameSettingsField.FilmGrain })
            {
                hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)f] = true;
                hd.renderingPathCustomFrameSettings.SetEnabled(f, false);
            }
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var tex = new Texture2D(size * yaws.Length, size, TextureFormat.RGB24, false);
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-4f) facing = Vector3.forward;
            cam.targetTexture = rt;
            for (int i = 0; i < yaws.Length; i++)
            {
                var dir = Quaternion.Euler(0f, yaws[i], 0f) * facing.normalized;
                dir = Quaternion.AngleAxis(-pitch, Vector3.Cross(Vector3.up, dir)) * dir;
                // a wall between the camera and the person: come in front of it
                float d = distance;
                if (Physics.Raycast(at + dir * 0.6f, dir, out var hit, distance - 0.6f, ~0, QueryTriggerInteraction.Ignore)) d = Mathf.Min(distance, Mathf.Max(1.1f, 0.6f + hit.distance - 0.1f));
                cam.transform.position = at + dir * d;
                cam.transform.LookAt(at);
                for (int k = 0; k < 3; k++) cam.Render();          // the exposure settles
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, size, size), i * size, 0);
            }
            tex.Apply();
            RenderTexture.active = null; cam.targetTexture = null;
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        }

        /// <summary>The whole person from the front left, the side and the back right.</summary>
        public static void Person(Character c, string file, float distance = 3.2f)
        {
            var rig = c.GetComponent<CharacterRig>();
            var anim = c.GetComponent<Animator>();
            var hips = anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Hips) : null;
            var at = hips ? hips.position + Vector3.up * 0.1f : c.transform.position + Vector3.up * 0.9f;
            // someone lying down has their "forward" pointing up: look from beside the body instead
            var facing = c.transform.forward;
            if (Mathf.Abs(facing.y) > 0.7f) facing = c.transform.up;
            Save(file, at, facing, distance, new[] { 35f, 100f, 215f });
        }

        /// <summary>One hand, close, from two sides.</summary>
        public static void Hand(Character c, string file, bool right = true)
        {
            var anim = c.GetComponent<Animator>();
            var hand = anim.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Save(file, hand.position - Vector3.up * 0.05f, c.transform.forward, 0.7f, new[] { right ? 90f : -90f, 0f, 180f }, 5f);
        }
    }
}
