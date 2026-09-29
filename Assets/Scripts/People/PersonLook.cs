using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Puts a <see cref="PersonData"/> on an MPFB2 person: body and face sliders (<see cref="BodyShape"/>), skin tone,
    /// hair style and colour (brows and lashes follow the hair), and clothes with their colours (<see cref="Wardrobe"/>).
    /// Everyday clothes by default; <see cref="Dress"/> switches to sleepwear for bed and back.
    /// </summary>
    public class PersonLook : MonoBehaviour
    {
        public PersonData data;
        public bool inSleepwear { get; private set; }

        /// <summary>Realistic skin tones as a multiplier on the base skin texture (a light to medium skin): fair to deep.</summary>
        static readonly Color[] Tones =
        {
            new Color(1.1f, 1.05f, 1.02f), new Color(1f, 1f, 1f), new Color(0.87f, 0.79f, 0.72f),
            new Color(0.71f, 0.59f, 0.5f), new Color(0.53f, 0.41f, 0.34f), new Color(0.37f, 0.27f, 0.22f),
        };

        public static Color SkinTint(float t)
        {
            t = Mathf.Clamp01(t) * (Tones.Length - 1);
            int i = Mathf.Min(Tones.Length - 2, (int)t);
            return Color.Lerp(Tones[i], Tones[i + 1], t - i);
        }

        /// <summary>The hair shader multiplies a grey strand map (about 0.72) by its tint, so the tint is lifted to match.</summary>
        public static Color HairTint(Color c) => new Color(Mathf.Clamp01(c.r / 0.72f), Mathf.Clamp01(c.g / 0.72f), Mathf.Clamp01(c.b / 0.72f));

        readonly Dictionary<Renderer, Material[]> own = new Dictionary<Renderer, Material[]>();

        Material[] Own(Renderer r)
        {
            if (!own.TryGetValue(r, out var m)) own[r] = m = r.materials;   // this person's copies, made once
            return m;
        }

        public void Apply(PersonData p)
        {
            data = p;
            ApplyShape();
            ApplyTints();
            Dress(inSleepwear);
        }

        /// <summary>Body and face sliders only (cheap enough for every frame of a slider drag).</summary>
        public void ApplyShape()
        {
            var shape = GetComponent<BodyShape>();
            if (!shape || data == null) return;
            shape.EnsureCaptured();
            shape.values.Clear();
            foreach (var v in data.sliders) shape.Set(v.name, v.value);
            shape.Apply();
        }

        static Material layers;
        RenderTexture skinRT;
        Texture skinBase;

        /// <summary>
        /// The skin with this person's details drawn on (Hidden/Dearlife/SkinLayers, masks from
        /// tools/blender_skin_layers.py): the tone, makeup, freckles, beard and tattoos. Done on the graphics card into a
        /// texture of its own, so it is quick enough for every step of a slider in the creator.
        /// </summary>
        void ComposeSkin(Material m)
        {
            if (!layers) layers = Resources.Load<Material>("People/SkinLayers");
            if (!layers || !m.HasProperty("_Base_Map")) { if (m.HasProperty("_Color")) m.SetColor("_Color", SkinTint(data.skin)); return; }
            if (!skinBase) skinBase = m.GetTexture("_Base_Map");
            if (!skinBase) return;
            if (!skinRT)
            {
                skinRT = new RenderTexture(skinBase.width, skinBase.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { useMipMap = true, autoGenerateMips = true, anisoLevel = 4, wrapMode = TextureWrapMode.Clamp, name = name + " skin" };
                skinRT.Create();
            }
            layers.SetColor("_Skin", SkinTint(data.skin));
            layers.SetColor("_Lip", A(data.lipColour, data.lipstick));
            layers.SetColor("_Blush", A(data.blushColour, data.blush));
            layers.SetColor("_Shadow", A(data.shadowColour, data.eyeshadow));
            layers.SetColor("_Liner", A(new Color(0.02f, 0.018f, 0.018f), data.liner));
            layers.SetColor("_Freckles", A(new Color(0.62f, 0.45f, 0.35f), data.freckles));
            layers.SetColor("_Beard", data.hairColour * 0.9f);
            layers.SetVector("_BeardStyle", new Vector4(data.beard == "full" ? 1f : 0f, data.beard == "goatee" ? 1f : 0f, data.beard == "stubble" ? 0.75f : 0f, 0f));
            layers.SetVector("_Tattoo", new Vector4(data.tattoos.Contains("band") ? 1f : 0f, data.tattoos.Contains("rose") ? 1f : 0f, data.tattoos.Contains("star") ? 1f : 0f, 0f));
            Graphics.Blit(skinBase, skinRT, layers, 0);
            m.SetTexture("_Base_Map", skinRT);
            m.SetColor("_Color", Color.white);    // the tone is in the texture now
        }

        static Color A(Color c, float a) { c.a = a; return c; }

        void OnDestroy()
        {
            if (skinRT) { skinRT.Release(); Destroy(skinRT); }
        }

        /// <summary>Skin tone and details, eye colour, hair, brow and lash colour, and the colours of the clothes.</summary>
        public void ApplyTints()
        {
            if (data == null) return;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name.EndsWith("_body"))
                    foreach (var m in Own(r)) ComposeSkin(m);
                else if (r.name.Contains("eyebrow") || r.name.Contains("eyelash"))
                    foreach (var m in Own(r)) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", HairTint(data.hairColour * 0.8f)); }
            }
            var eyes = GetComponent<Eyes>();
            if (eyes) eyes.SetIris(data.eyeColour);
            var w = GetComponent<Wardrobe>();
            if (!w) return;
            foreach (var t in data.colours) w.SetColour(t.id, t.colour);
            if (!string.IsNullOrEmpty(data.hair)) w.SetColour(data.hair, HairTint(data.hairColour));
        }

        /// <summary>Everyday clothes, or sleepwear (for bed and naps).</summary>
        public void Dress(bool sleepwear)
        {
            inSleepwear = sleepwear;
            Wear(sleepwear ? data?.sleep : data?.everyday, sleepwear);
        }

        /// <summary>Swimwear for the pool (someone who chose none swims in what they have on), or back to everyday clothes.</summary>
        public void DressForSwim(bool on)
        {
            if (data == null) return;
            if (on && data.swim != null && data.swim.Count > 0) Wear(data.swim, true);
            else if (!on) Dress(false);
        }

        void Wear(List<string> list, bool noHijab)
        {
            var w = GetComponent<Wardrobe>();
            if (!w || data == null || list == null) return;
            var items = new List<string>(list);
            if (!string.IsNullOrEmpty(data.hair)) items.Add(data.hair);
            if (noHijab) items.Remove("hijab");
            w.Wear(items);
        }
    }
}
