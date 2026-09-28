using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// All the sound of the game. It is the sound of the 2D Tiramisu App brought over: the seasonal lofi music (jazzy seventh chords, swung
    /// drums, a little hook per season, vinyl crackle), its coin, pop, buy, love, level and error jingles, and the real recordings of Bedah
    /// (meow and purr). What the 2D game never had is made in code here: sizzling food, running water, splashes, birds and wind (the night crickets were removed in v0.40.0: their synthesized beep was distracting).
    /// F2 mutes everything, N turns the music off.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public enum Sfx { Click, Chime, No, Buy, Sell, Alert, Place, Splash, Ding, Level }

        public static GameAudio Instance { get; private set; }
        public static bool Muted { get; private set; }
        public static bool MusicOn { get; private set; } = true;

        // the four sliders of the settings panel (0 to 1), saved
        public static float VolMaster { get; private set; } = 1f;
        public static float VolMusic { get; private set; } = 0.6f;
        public static float VolSfx { get; private set; } = 1f;
        public static float VolPet { get; private set; } = 0.5f;

        const int Rate = 22050;
        enum Wave { Sine, Triangle, Square, Saw }

        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        readonly AudioClip[] tracks = new AudioClip[4];
        readonly Task<float[]>[] rendering = new Task<float[]>[4];
        readonly Dictionary<string, AudioClip> recorded = new Dictionary<string, AudioClip>();
        AudioSource sfx, music, birds, wind;
        AudioClip birdChirp, sizzle, water;
        float nextChirp, nextMeow;
        int playing = -1;

        void Awake()
        {
            Instance = this;
            if (PlayerPrefs.GetInt("dearlife.muted", 0) == 1) Muted = true;
            if (PlayerPrefs.GetInt("dearlife.music", 1) == 0) MusicOn = false;
            VolMaster = PlayerPrefs.GetFloat("dearlife.volMaster", 1f);
            VolMusic = PlayerPrefs.GetFloat("dearlife.volMusic", 0.6f);
            VolSfx = PlayerPrefs.GetFloat("dearlife.volSfx", 1f);
            VolPet = PlayerPrefs.GetFloat("dearlife.volPet", 0.5f);
            var go = gameObject;
            sfx = Source(go, false, 0.55f);
            music = Source(go, true, 0f);
            wind = Source(go, true, 0f);
            birds = Source(go, false, 0.35f);
            Build();
        }

        static AudioSource Source(GameObject go, bool loop, float vol)
        {
            var s = go.AddComponent<AudioSource>();
            s.spatialBlend = 0f; s.loop = loop; s.volume = vol; s.playOnAwake = false;
            return s;
        }

        public static void Play(Sfx s)
        {
            if (Instance == null || Muted) return;
            if (Instance.clips.TryGetValue(s, out var c)) Instance.sfx.PlayOneShot(c, VolSfx);
        }

        public static void ToggleMute() { Muted = !Muted; PlayerPrefs.SetInt("dearlife.muted", Muted ? 1 : 0); }
        public static void ToggleMusic() { MusicOn = !MusicOn; PlayerPrefs.SetInt("dearlife.music", MusicOn ? 1 : 0); }

        /// <summary>0 master, 1 music, 2 sound effects, 3 pets.</summary>
        public static void SetVolume(int which, float v)
        {
            v = Mathf.Clamp01(v);
            switch (which)
            {
                case 0: VolMaster = v; PlayerPrefs.SetFloat("dearlife.volMaster", v); break;
                case 1: VolMusic = v; PlayerPrefs.SetFloat("dearlife.volMusic", v); break;
                case 2: VolSfx = v; PlayerPrefs.SetFloat("dearlife.volSfx", v); break;
                default: VolPet = v; PlayerPrefs.SetFloat("dearlife.volPet", v); break;
            }
        }

        // ------------------------------------------------------------ the real recordings and what is made from them

        static AudioClip Recording(string name)
        {
            if (Instance == null) return null;
            if (!Instance.recorded.TryGetValue(name, out var c)) Instance.recorded[name] = c = Resources.Load<AudioClip>("Audio/" + name);
            return c;
        }

        /// <summary>Plays one of the recorded clips (cat_meow, cat_purr, dog_bark, guitar, synth ...) in the room, at a place.</summary>
        public static AudioSource PlayAt(string name, Vector3 pos, float volume, float pitch = 1f, float seconds = 0f, bool pet = true)
        {
            if (Instance == null || Muted) return null;
            var c = Recording(name);
            return c ? Spawn(c, pos, volume * (pet ? VolPet * 2f : VolSfx), pitch, seconds, false) : null;
        }

        static AudioSource Spawn(AudioClip c, Vector3 pos, float volume, float pitch, float seconds, bool loop)
        {
            var go = new GameObject("sound " + c.name);
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = c; s.volume = Mathf.Clamp01(volume); s.pitch = pitch; s.loop = loop || seconds > c.length;
            s.spatialBlend = 0.65f; s.minDistance = 3f; s.maxDistance = 30f; s.rolloffMode = AudioRolloffMode.Linear;
            s.Play();
            Destroy(go, (seconds > 0f ? seconds : c.length / Mathf.Max(0.1f, pitch)) + 0.1f);
            return s;
        }

        /// <summary>The sound of an activity: sizzling for cooking, running water for washing and swimming. Stop it with <see cref="StopAct"/>.</summary>
        public static AudioSource PlayAct(string id, float seconds, Vector3 pos)
        {
            if (Instance == null || Muted) return null;
            AudioClip c = null; float vol = 0.5f;
            switch (id)
            {
                case "cook": case "grill": case "takeaway": c = Instance.sizzle; vol = 0.5f; break;
                case "laundry": case "swim": c = Instance.water; vol = 0.55f; break;
                case "guitar": case "synth": case "shower": case "bath": case "washface":
                {
                    // real recordings (the 2D game's music, and the bathroom's water), looped for as long as it lasts,
                    // fading in and out
                    var rec = Recording(id == "bath" ? "bathtub" : id == "washface" ? "sink_water" : id);
                    if (rec == null) return null;
                    var src = Spawn(rec, pos, 0.9f * VolSfx, 1f, seconds + 0.5f, true);
                    src.gameObject.name = "act sound";
                    src.gameObject.AddComponent<AudioFade>().Begin(src, seconds + 0.5f, 0.9f * VolSfx);
                    return src;
                }
            }
            if (c == null) return null;
            var s = Spawn(c, pos, vol * VolSfx, 1f, seconds + 0.5f, true);
            s.gameObject.name = "act sound";
            return s;
        }

        public static void StopAct(AudioSource s)
        {
            if (s) Destroy(s.gameObject);
        }

        // ------------------------------------------------------------ the small software synth (the same recipes as the WebAudio version)

        static float Osc(Wave w, float phase)
        {
            phase -= Mathf.Floor(phase);
            switch (w)
            {
                case Wave.Triangle: return 4f * Mathf.Abs(phase - 0.5f) - 1f;
                case Wave.Square: return phase < 0.5f ? 1f : -1f;
                case Wave.Saw: return 2f * phase - 1f;
                default: return Mathf.Sin(2f * Mathf.PI * phase);
            }
        }

        static float MidiHz(float m) => 440f * Mathf.Pow(2f, (m - 69f) / 12f);

        /// <summary>One note: a short fade in, then a fall to silence, like the 2D game's tone(). It wraps round the end of the loop.</summary>
        static void Tone(float[] mix, float[] send, float startSec, float hz, float dur, float vol, Wave wave, float sendAmount, System.Random r, float attack = 0.02f)
        {
            int i0 = Mathf.RoundToInt(startSec * Rate), n = Mathf.CeilToInt((dur + 0.05f) * Rate);
            float cents = ((float)r.NextDouble() * 2f - 1f) * 7f;
            float f = hz * Mathf.Pow(2f, cents / 1200f), ph = 0f, dt = f / Rate;
            float k = Mathf.Log(0.0001f / Mathf.Max(vol, 0.0001f)) / Mathf.Max(dur - attack, 0.01f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = t < attack ? vol * t / attack : vol * Mathf.Exp(k * (t - attack));
                if (t > dur) env *= Mathf.Clamp01(1f - (t - dur) / 0.05f);
                float s = Osc(wave, ph) * env; ph += dt;
                int idx = (i0 + i) % mix.Length;
                mix[idx] += s;
                if (sendAmount > 0f) send[idx] += s * sendAmount;
            }
        }

        /// <summary>A puff of filtered noise (hats, snares, crackle): 'h' high pass, 'l' low pass, 'b' band pass.</summary>
        static void Noise(float[] mix, float startSec, float dur, float vol, char kind, float freq, float q, System.Random r)
        {
            int i0 = Mathf.RoundToInt(startSec * Rate), n = Mathf.CeilToInt((dur + 0.02f) * Rate);
            float w0 = 2f * Mathf.PI * Mathf.Min(freq, Rate * 0.45f) / Rate, cs = Mathf.Cos(w0), sn = Mathf.Sin(w0), al = sn / (2f * Mathf.Max(q, 0.1f));
            float b0, b1, b2, a0 = 1f + al, a1 = -2f * cs, a2 = 1f - al;
            if (kind == 'h') { b0 = (1f + cs) / 2f; b1 = -(1f + cs); b2 = (1f + cs) / 2f; }
            else if (kind == 'l') { b0 = (1f - cs) / 2f; b1 = 1f - cs; b2 = (1f - cs) / 2f; }
            else { b0 = al; b1 = 0f; b2 = -al; }
            b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
            float x1 = 0, x2 = 0, y1 = 0, y2 = 0, k = Mathf.Log(0.0001f / Mathf.Max(vol, 0.0001f)) / Mathf.Max(dur, 0.005f);
            for (int i = 0; i < n; i++)
            {
                float x = (float)r.NextDouble() * 2f - 1f;
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                float t = i / (float)Rate;
                if (t > dur) break;
                mix[(i0 + i) % mix.Length] += y * vol * Mathf.Exp(k * t);
            }
        }

        static void Kick(float[] mix, float startSec)
        {
            int i0 = Mathf.RoundToInt(startSec * Rate), n = Mathf.CeilToInt(0.3f * Rate);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float hz = t < 0.14f ? 115f * Mathf.Pow(42f / 115f, t / 0.14f) : 42f;
                ph += hz / Rate;
                mix[(i0 + i) % mix.Length] += Mathf.Sin(2f * Mathf.PI * ph) * 0.5f * Mathf.Exp(Mathf.Log(0.0001f / 0.5f) * t / 0.28f);
            }
        }

        /// <summary>The music bus's delay: a feedback echo, softened, like the 2D game's. It runs on a loop so the tail wraps round.</summary>
        static void Echo(float[] mix, float[] send, float seconds, float feedback, float lowpassHz)
        {
            int n = mix.Length, d = Mathf.RoundToInt(seconds * Rate);
            float a = Mathf.Exp(-2f * Mathf.PI * lowpassHz / Rate);
            var y = new float[n]; var wet = new float[n];
            for (int pass = 0; pass < 4; pass++)
            {
                float lp = 0f;
                for (int i = 0; i < n; i++)
                {
                    lp = lp * a + y[((i - d) % n + n) % n] * (1f - a);
                    wet[i] = lp;
                    y[i] = send[i] + feedback * lp;
                }
            }
            for (int i = 0; i < n; i++) mix[i] += wet[i];
        }

        static void LowPass(float[] x, float hz)
        {
            float a = Mathf.Exp(-2f * Mathf.PI * hz / Rate);
            for (int p = 0; p < 2; p++) { float y = 0f; for (int i = 0; i < x.Length; i++) { y = y * a + x[i] * (1f - a); x[i] = y; } }
        }

        // ------------------------------------------------------------ the seasonal lofi music

        static readonly int[][] Chords = { new[] { 0, 3, 7, 10 }, new[] { 0, 3, 7, 10, 14 }, new[] { 0, 4, 7, 11 }, new[] { 0, 4, 7, 11, 14 }, new[] { 0, 4, 7, 10 } };
        const int M7 = 0, M9 = 1, Maj7 = 2, Maj9 = 3, Dom7 = 4;
        static readonly (int root, int type)[][] Progs =
        {
            new[] { (2, M9), (7, Dom7), (0, Maj9), (9, M7) },       // spring
            new[] { (5, Maj9), (4, M7), (2, M9), (0, Maj7) },       // summer
            new[] { (9, M9), (2, M7), (7, Dom7), (0, Maj7) },       // autumn
            new[] { (0, Maj9), (9, M7), (2, M9), (7, Dom7) },       // winter
        };
        static readonly float[] Bpm = { 76f, 82f, 70f, 66f };
        // step in the bar, scale degree, wave: a small hook that makes each season different
        static readonly (int step, int deg, Wave wave)[][] Hooks =
        {
            new[] { (0, 0, Wave.Triangle), (4, 4, Wave.Triangle), (8, 7, Wave.Triangle), (12, 4, Wave.Triangle) },                       // spring: bright arpeggio
            new[] { (0, 0, Wave.Square), (3, 7, Wave.Square), (6, 4, Wave.Square), (9, 9, Wave.Square), (12, 7, Wave.Square) },         // summer: bouncy
            new[] { (0, 7, Wave.Sine), (6, 4, Wave.Sine), (10, 0, Wave.Sine), (14, -3, Wave.Sine) },                                    // autumn: wistful descent
            new[] { (0, 12, Wave.Sine), (8, 7, Wave.Sine) },                                                                            // winter: sparse glockenspiel
        };

        /// <summary>Four bars of one season, made in the background (about a second) so the game does not stutter.</summary>
        static float[] RenderTrack(int se)
        {
            var r = new System.Random(100 + se);
            float spd = 60f / Bpm[se] / 4f;
            int n = Mathf.RoundToInt(64 * spd * Rate);
            var mix = new float[n]; var send = new float[n];
            for (int st = 0; st < 64; st++)
            {
                int bar = st >> 4, s = st & 15;
                var (root, ty) = Progs[se][bar];
                var chord = Chords[ty];
                float t = st * spd + ((s & 1) == 1 ? spd * 0.2f : 0f);          // swing
                var pad = new float[chord.Length];
                for (int i = 0; i < chord.Length; i++) pad[i] = 48 + root + chord[i];
                if (s == 0) for (int i = 0; i < pad.Length; i++) Tone(mix, send, t + i * 0.014f, MidiHz(pad[i]), spd * 8f, 0.05f, Wave.Triangle, 0.3f, r);
                if (s == 7 || (s == 10 && r.NextDouble() < 0.7)) for (int i = 1; i < pad.Length; i++) Tone(mix, send, t, MidiHz(pad[i]), spd * 1.8f, 0.03f, Wave.Sine, 0.2f, r);
                if (s == 0) Tone(mix, send, t, MidiHz(36 + root), spd * 4f, 0.2f, Wave.Sine, 0f, r);
                if (s == 8 && r.NextDouble() < 0.6) Tone(mix, send, t, MidiHz(36 + root + 7), spd * 2.5f, 0.14f, Wave.Sine, 0f, r);
                if (s == 11 && r.NextDouble() < 0.5) Tone(mix, send, t, MidiHz(48 + root), spd * 1.5f, 0.09f, Wave.Sine, 0f, r);
                if (s == 0 || (s == 10 && r.NextDouble() < 0.8) || (s == 7 && r.NextDouble() < 0.25)) Kick(mix, t);
                if (s == 4 || s == 12) Noise(mix, t, 0.16f, 0.14f, 'b', 1900f, 0.8f, r);
                if (s % 2 == 0) Noise(mix, t, 0.045f, s % 4 == 0 ? 0.05f : 0.03f, 'h', 7500f, 0.7f, r);
                if (s % 2 == 0 && r.NextDouble() < 0.3 && bar % 4 != 3) Tone(mix, send, t, MidiHz(pad[r.Next(pad.Length)] + 12), spd * (2f + (float)r.NextDouble() * 3f), 0.05f, Wave.Sine, 0.55f, r);
                if (r.NextDouble() < 0.25) Noise(mix, t + (float)r.NextDouble() * spd, 0.012f, 0.06f, 'h', 3500f, 0.7f, r);   // vinyl crackle
                foreach (var h in Hooks[se])
                    if (h.step == s) Tone(mix, send, t, MidiHz(60 + root + h.deg), spd * (se == 3 ? 5f : 2.2f), se == 3 ? 0.07f : 0.045f, h.wave, 0.4f, r);
            }
            Echo(mix, send, 0.37f, 0.36f, 1600f);
            // a constant soft hiss under it all
            float hiss = 0f, a = Mathf.Exp(-2f * Mathf.PI * 1800f / Rate);
            for (int i = 0; i < n; i++) { float x = (float)r.NextDouble() * 2f - 1f; hiss = a * hiss + (1f - a) * x; mix[i] += (x - hiss) * 0.012f; }
            LowPass(mix, 3000f);
            float peak = 0.001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(mix[i]));
            float g = 0.85f / peak;
            for (int i = 0; i < n; i++) mix[i] *= g;
            int f = Mathf.RoundToInt(0.004f * Rate);       // a very short fade at the seam so the loop never clicks
            for (int i = 0; i < f; i++) { float k = i / (float)f; mix[i] *= k; mix[n - 1 - i] *= k; }
            return mix;
        }

        static int SeasonIndex() { var sc = SeasonCycle.Instance; return sc ? (int)sc.season : 0; }

        /// <summary>The music of the current season has been made (it is built in the background when the game opens).</summary>
        public bool MusicReady { get { int se = SeasonIndex(); return tracks[se] != null || (rendering[se] != null && rendering[se].IsCompleted); } }

        void StartTrack(int se)
        {
            if (tracks[se] == null)
            {
                if (rendering[se] == null) rendering[se] = Task.Run(() => RenderTrack(se));
                if (!rendering[se].IsCompleted) return;
                var data = rendering[se].Result;
                var c = AudioClip.Create("lofi " + (Season)se, data.Length, 1, Rate, false);
                c.SetData(data, 0);
                tracks[se] = c;
            }
            music.clip = tracks[se];
            music.Play();
            playing = se;
        }

        // ------------------------------------------------------------ the small sounds

        static AudioClip Make(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        /// <summary>A jingle from a list of notes (hz, length, wave, volume, start), each one falling away like the 2D game's snd().</summary>
        static AudioClip Jingle(string name, params (float hz, float len, Wave wave, float vol, float at)[] notes)
        {
            float total = 0f;
            foreach (var n in notes) total = Mathf.Max(total, n.at + n.len + 0.05f);
            return Make(name, total, t =>
            {
                float v = 0f;
                foreach (var n in notes)
                {
                    float u = t - n.at;
                    if (u < 0f || u > n.len) continue;
                    float vol = n.vol * 6f;      // the browser plays these very quietly, the mixer here wants more
                    float e = vol * Mathf.Exp(Mathf.Log(0.0001f / vol) * u / n.len) * Mathf.Clamp01(u / 0.003f);
                    v += Osc(n.wave, n.hz * u) * e;
                }
                return v;
            });
        }

        static float Env(float t, float attack, float length) => Mathf.Clamp01(t / attack) * Mathf.Exp(-t * (4f / length));
        static float Sine(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);

        void Build()
        {
            // the 2D game's own jingles
            clips[Sfx.Click] = Jingle("pop", (520f, 0.08f, Wave.Sine, 0.05f, 0f));
            clips[Sfx.Ding] = Jingle("coin", (880f, 0.1f, Wave.Triangle, 0.05f, 0f), (1320f, 0.2f, Wave.Triangle, 0.05f, 0.08f));
            clips[Sfx.Sell] = clips[Sfx.Ding];
            clips[Sfx.Buy] = Jingle("buy", (440f, 0.1f, Wave.Sine, 0.05f, 0f), (660f, 0.16f, Wave.Sine, 0.05f, 0.09f));
            clips[Sfx.No] = Jingle("err", (170f, 0.16f, Wave.Square, 0.03f, 0f));
            clips[Sfx.Chime] = Jingle("love", (660f, 0.1f, Wave.Sine, 0.04f, 0f), (880f, 0.14f, Wave.Sine, 0.04f, 0.07f));
            clips[Sfx.Level] = Jingle("lvl", (523f, 0.25f, Wave.Triangle, 0.05f, 0f), (659f, 0.25f, Wave.Triangle, 0.05f, 0.11f), (784f, 0.25f, Wave.Triangle, 0.05f, 0.22f), (1047f, 0.25f, Wave.Triangle, 0.05f, 0.33f));
            // new for the 3D house
            clips[Sfx.Alert] = Jingle("alert", (523f, 0.15f, Wave.Sine, 0.05f, 0f), (392f, 0.2f, Wave.Sine, 0.05f, 0.2f));
            clips[Sfx.Place] = Make("place", 0.16f, t => Sine(150f + 60f * Mathf.Exp(-t * 30f), t) * Env(t, 0.002f, 0.12f) * 0.7f);
            var rnd = new System.Random(3);
            clips[Sfx.Splash] = Make("splash", 0.6f, t => ((float)rnd.NextDouble() * 2f - 1f) * Env(t, 0.02f, 0.5f) * 0.25f);

            var r2 = new System.Random(8); float lp = 0f;
            wind.clip = Make("wind", 6f, t => { lp = lp * 0.985f + ((float)r2.NextDouble() * 2f - 1f) * 0.015f; return lp * 9f * (0.6f + 0.4f * Sine(0.2f, t)); });
            // the bird of the 2D game: a quick rising chirp
            birdChirp = Make("bird", 0.25f, t => Sine(3200f + 1100f * Mathf.Clamp01(t / 0.09f) - 700f * Mathf.Clamp01((t - 0.09f) / 0.16f), t) * Env(t, 0.008f, 0.16f) * 0.35f);
            // sizzling food: bright noise with crackles
            var r3 = new System.Random(11); float hp = 0f; float ha = Mathf.Exp(-2f * Mathf.PI * 5000f / Rate);
            sizzle = Make("sizzle", 3f, t => { float x = (float)r3.NextDouble() * 2f - 1f; hp = ha * hp + (1f - ha) * x; return (x - hp) * 0.07f * (0.7f + 0.3f * Sine(3.1f, t)) + (r3.NextDouble() < 0.0015 ? 0.2f : 0f); });
            // running water: a soft stream with little bubbles
            var r4 = new System.Random(19); float wl = 0f, wl2 = 0f; float bub = 0f, bubHz = 500f;
            water = Make("water", 3f, t =>
            {
                float x = (float)r4.NextDouble() * 2f - 1f; wl = wl * 0.7f + x * 0.3f; wl2 = wl2 * 0.97f + wl * 0.03f;
                if (r4.NextDouble() < 0.0006) { bub = 1f; bubHz = 300f + (float)r4.NextDouble() * 500f; }
                bub *= 0.9992f;
                return (wl - wl2) * 0.09f * (0.7f + 0.3f * Sine(0.6f, t)) + Sine(bubHz * (1f + (1f - bub) * 1.4f), t) * bub * 0.05f;
            });
            wind.Play();
        }

        // ------------------------------------------------------------ every frame

        void Update()
        {
            AudioListener.volume = VolMaster;
            var dn = DayNightCycle.Instance;
            float night = dn ? dn.Night01 : 0f;
            var sea = SeasonCycle.Instance;
            float winter = sea && sea.season == Season.Winter ? 1f : 0f;
            float k = 1f - Mathf.Exp(-2f * Time.unscaledDeltaTime);
            float mute = Muted ? 0f : 1f;

            // the lofi track of the season: made once in the background, and it swaps quietly when the season changes
            int se = SeasonIndex();
            if (playing != se)
            {
                if (playing >= 0 && music.volume > 0.01f) music.volume = Mathf.Lerp(music.volume, 0f, k * 2f);
                else StartTrack(se);
            }
            else music.volume = Mathf.Lerp(music.volume, MusicOn ? 0.32f * VolMusic * mute : 0f, k);

            wind.volume = Mathf.Lerp(wind.volume, (0.02f + 0.06f * winter) * mute, k);
            if (Time.time > nextChirp)
            {
                nextChirp = Time.time + Random.Range(2.5f, 9f);
                if (night < 0.4f && winter < 0.5f && !Muted) { birds.pitch = Random.Range(0.85f, 1.3f); birds.PlayOneShot(birdChirp, 0.5f * VolSfx); if (Random.value < 0.5f) StartCoroutine(Twice()); }
            }
            // now and then Bedah calls out, with the real recording
            if (Time.time > nextMeow)
            {
                nextMeow = Time.time + Random.Range(35f, 90f);
                var pets = new List<Character>(); foreach (var c in Character.All) if (c && c.isPet && c.isActiveAndEnabled) pets.Add(c);
                if (pets.Count > 0) { var p = pets[Random.Range(0, pets.Count)]; PlayAt(p.name.StartsWith("dog") ? "dog_bark" : "cat_meow", p.transform.position + Vector3.up * 0.3f, 0.7f, Random.Range(0.92f, 1.1f)); }
            }
            if (Input.GetKeyDown(KeyCode.N)) ToggleMusic();
            if (Input.GetKeyDown(KeyCode.F2)) ToggleMute();
        }

        System.Collections.IEnumerator Twice() { yield return new WaitForSeconds(0.28f); if (!Muted) birds.PlayOneShot(birdChirp, 0.4f * VolSfx); }
    }

    /// <summary>Fades a looped sound in at the start and out at the end.</summary>
    public class AudioFade : MonoBehaviour
    {
        AudioSource src; float length, volume, t;

        public void Begin(AudioSource s, float seconds, float vol) { src = s; length = seconds; volume = vol; s.volume = 0f; }

        void Update()
        {
            if (!src) return;
            t += Time.deltaTime;
            src.volume = volume * Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((length - t) / 0.7f);
        }
    }
}
