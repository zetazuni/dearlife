using UnityEngine;

namespace Dearlife
{
    /// <summary>Name tags over the people and pets, and a speech bubble while somebody is talking.</summary>
    public class CharacterHud : MonoBehaviour
    {
        void OnGUI()
        {
            var cam = Camera.main;
            if (!cam || Splash.Showing || SettingsWindow.Open) return;
            // the title screen's cinematic shots are meant to be anonymous, so names never show there regardless of the setting
            bool showNames = Ui.ShowNames && !MainMenu.Active;
            Ui.Begin();
            GUI.depth = 10;      // behind every HUD panel (they draw at 0): tags belong to the world, not on top of the interface
            foreach (var c in Character.All)
            {
                var r = c.GetComponentInChildren<Renderer>();
                if (r && !r.enabled) continue;
                var world = c.transform.position + Vector3.up * ((c.isPet ? 0.7f : 2.0f) * c.scale + 0.15f);
                var s = cam.WorldToScreenPoint(world);
                if (s.z < 0f) continue;
                float y = Screen.height - s.y;
                if (showNames && !string.IsNullOrEmpty(c.displayName)) Ui.Tag(new Vector2(s.x, y), c.displayName, 12f, c == LiveMode.Selected);
                if (c.Speaking && !string.IsNullOrEmpty(c.Bubble)) Ui.Bubble(new Vector2(s.x, y - 18f * Ui.Scale), c.Bubble);
            }
        }
    }
}
