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

        /// <summary>Skin tone, hair, brow and lash colour, and the colours of the clothes.</summary>
        public void ApplyTints()
        {
            if (data == null) return;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name.EndsWith("_body"))
                    foreach (var m in Own(r)) { if (m.HasProperty("_Color")) m.SetColor("_Color", SkinTint(data.skin)); }
                else if (r.name.Contains("eyebrow") || r.name.Contains("eyelash"))
                    foreach (var m in Own(r)) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", HairTint(data.hairColour * 0.8f)); }
            }
            var w = GetComponent<Wardrobe>();
            if (!w) return;
            foreach (var t in data.colours) w.SetColour(t.id, t.colour);
            if (!string.IsNullOrEmpty(data.hair)) w.SetColour(data.hair, HairTint(data.hairColour));
        }

        /// <summary>Everyday clothes, or sleepwear (for bed and naps).</summary>
        public void Dress(bool sleepwear)
        {
            inSleepwear = sleepwear;
            var w = GetComponent<Wardrobe>();
            if (!w || data == null) return;
            var items = new List<string>(sleepwear ? data.sleep : data.everyday);
            if (!string.IsNullOrEmpty(data.hair)) items.Add(data.hair);
            if (sleepwear) items.Remove("hijab");
            w.Wear(items);
        }
    }
}
