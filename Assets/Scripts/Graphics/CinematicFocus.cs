using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Dearlife
{
    /// <summary>
    /// Keeps the depth of field focused on whatever the orbit camera is looking at,
    /// like a camera operator pulling focus. Things much nearer or further than the subject soften a little.
    /// </summary>
    public class CinematicFocus : MonoBehaviour
    {
        public Volume volume;
        DepthOfField dof;

        void Start()
        {
            if (volume && volume.profile) volume.profile.TryGet(out dof);
        }

        void LateUpdate()
        {
            var cam = OrbitCamera.Instance;
            if (dof == null || !cam) return;
            float d = cam.distance;
            dof.nearFocusStart.value = 0f;
            dof.nearFocusEnd.value = d * 0.35f;
            // the sky counts as infinitely far, so its far blur would smear the stars and moon into blobs: no far blur at night
            bool night = DayNightCycle.Instance && DayNightCycle.Instance.Night01 > 0.5f;
            dof.farFocusStart.value = night ? 100000f : d * 1.6f;
            dof.farFocusEnd.value = night ? 200000f : d * 5f;
        }
    }
}
