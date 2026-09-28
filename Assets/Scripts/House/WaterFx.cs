using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Small particle systems for the bathroom (steam, falling water, soap bubbles), with the materials made by
    /// Dearlife > Make bathroom effects (Resources/Effects). HDRP's unlit shader has no vertex colours, so particles fade
    /// by shrinking at the end of their life instead of turning see-through.
    /// </summary>
    public static class WaterFx
    {
        public static Material Mat(string name) => Resources.Load<Material>("Effects/" + name);

        public struct Spec
        {
            public string material;
            public float rate, life, speed, gravity;
            public Vector2 size;
            public Vector3 box;                  // emitter box size (local); zero x = a cone
            public float coneRadius, coneAngle;
            public bool stretch, world;
            public float grow;                   // size at the end of life, as a multiple (particles still shrink to nothing at the very end)
            public Vector3 drift;                // extra velocity, local
            public float noise;
            public int max;
        }

        public static ParticleSystem Make(Transform parent, string name, Vector3 localPos, Quaternion localRot, Spec s)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.life * 0.7f, s.life);
            main.startSpeed = s.speed;
            main.startSize = new ParticleSystem.MinMaxCurve(s.size.x, s.size.y);
            main.gravityModifier = s.gravity;
            main.maxParticles = s.max > 0 ? s.max : 500;
            main.simulationSpace = s.world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var em = ps.emission;
            em.rateOverTime = s.rate;
            var sh = ps.shape;
            if (s.box.x > 0f) { sh.shapeType = ParticleSystemShapeType.Box; sh.scale = s.box; }
            else { sh.shapeType = ParticleSystemShapeType.Cone; sh.radius = s.coneRadius; sh.angle = s.coneAngle; }
            if (s.drift != Vector3.zero)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.x = new ParticleSystem.MinMaxCurve(-Mathf.Abs(s.drift.x), Mathf.Abs(s.drift.x));
                vel.y = new ParticleSystem.MinMaxCurve(s.drift.y * 0.5f, s.drift.y);
                vel.z = new ParticleSystem.MinMaxCurve(-Mathf.Abs(s.drift.z), Mathf.Abs(s.drift.z));
            }
            if (s.noise > 0f)
            {
                var n = ps.noise;
                n.enabled = true;
                n.strength = s.noise;
                n.frequency = 0.6f;
            }
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            float g = s.grow > 0f ? s.grow : 1f;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.3f), new Keyframe(0.15f, 1f), new Keyframe(0.8f, g), new Keyframe(1f, 0f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat(s.material);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (s.stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 3f; }
            return ps;
        }

        /// <summary>Starts or stops emitting (what is already in the air finishes its life).</summary>
        public static void Run(ParticleSystem ps, bool on)
        {
            if (!ps) return;
            if (on && !ps.isEmitting) ps.Play(true);
            else if (!on && ps.isEmitting) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
