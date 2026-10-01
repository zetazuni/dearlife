using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Dearlife
{
    /// <summary>
    /// A small picture of each person's face for the bar at the bottom of the screen (v0.51.0), taken in the game itself: a
    /// camera of its own stands in front of the head for a few frames and sees that person alone, nothing blurred. Taken
    /// once, a moment after the person appears, and again when asked (<see cref="Refresh"/>).
    /// </summary>
    public static class Portraits
    {
        class Shot { public RenderTexture tex; public int frames; public float due; }

        static readonly Dictionary<Character, Shot> shots = new Dictionary<Character, Shot>();
        static Camera cam;
        static CinematicFocus focus;
        const int Size = 256, Frames = 4;        // a few frames, so the exposure has settled on the face
        const int Layer = 31;                    // an unused layer the person is moved to for the instant of the picture

        /// <summary>The person's picture, or null while there is none yet (draw their initial instead).</summary>
        public static Texture Of(Character c)
        {
            if (!c) return null;
            if (!shots.TryGetValue(c, out var s)) shots[c] = s = new Shot { due = Time.unscaledTime + 1.5f };
            return s.frames >= Frames ? s.tex : null;
        }

        public static void Refresh(Character c) { if (c && shots.TryGetValue(c, out var s)) { s.frames = 0; s.due = Time.unscaledTime + 0.5f; } }

        /// <summary>Call once a frame (LiveMode does): takes at most one picture frame.</summary>
        public static void Tick()
        {
            Character gone = null;
            foreach (var kv in shots)
            {
                if (!kv.Key) { gone = kv.Key; continue; }
                var s = kv.Value;
                if (s.frames >= Frames || Time.unscaledTime < s.due || !kv.Key.gameObject.activeInHierarchy) continue;
                Take(kv.Key, s);
                return;
            }
            if (!ReferenceEquals(gone, null))
            {
                if (shots[gone].tex) { shots[gone].tex.Release(); Object.Destroy(shots[gone].tex); }
                shots.Remove(gone);
            }
        }

        static void Take(Character c, Shot s)
        {
            if (!cam)
            {
                var go = new GameObject("Portrait camera") { hideFlags = HideFlags.HideAndDontSave };
                cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.fieldOfView = 22f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 3f;
                cam.cullingMask = 1 << Layer;            // the person alone: no wall in the way, no room behind
                var hd = go.AddComponent<HDAdditionalCameraData>();
                hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
                hd.backgroundColorHDR = new Color(0.16f, 0.18f, 0.22f, 1f);
                hd.customRenderingSettings = true;
                foreach (var f in new[] { FrameSettingsField.DepthOfField, FrameSettingsField.MotionBlur, FrameSettingsField.Vignette, FrameSettingsField.FilmGrain, FrameSettingsField.ChromaticAberration })
                {
                    hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)f] = true;
                    hd.renderingPathCustomFrameSettings.SetEnabled(f, false);
                }
            }
            if (!s.tex)
            {
                s.tex = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = c.displayName + " portrait", antiAliasing = 1 };
                s.tex.Create();
            }
            var anim = c.GetComponentInChildren<Animator>();
            var head = anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            var at = head ? head.position + Vector3.up * 0.075f : c.transform.position + Vector3.up * 1.6f;
            var fwd = Vector3.ProjectOnPlane(head ? head.forward : c.transform.forward, Vector3.up);
            if (Vector3.Dot(fwd, c.transform.forward) < 0f || fwd.sqrMagnitude < 0.01f) fwd = c.transform.forward;   // head bones point every which way
            fwd = (fwd.normalized + c.transform.right * 0.25f).normalized;                                           // a little from the side
            cam.transform.position = at + fwd * 1.0f + Vector3.up * 0.03f;
            cam.transform.LookAt(at);
            cam.targetTexture = s.tex;
            // the game's depth of field is set for the far away play camera and would smear a face this close
            if (!focus) focus = Object.FindAnyObjectByType<CinematicFocus>();
            UnityEngine.Rendering.HighDefinition.DepthOfField dof = null;
            if (focus && focus.volume && focus.volume.profile) focus.volume.profile.TryGet(out dof);
            bool was = dof != null && dof.active;
            if (dof != null) dof.active = false;
            var parts = c.GetComponentsInChildren<Renderer>();
            var layers = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) { layers[i] = parts[i].gameObject.layer; parts[i].gameObject.layer = Layer; }
            cam.Render();
            for (int i = 0; i < parts.Length; i++) parts[i].gameObject.layer = layers[i];
            if (dof != null) dof.active = was;
            cam.targetTexture = null;
            s.frames++;
        }
    }
}
