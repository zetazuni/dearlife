using UnityEngine;

namespace Dearlife
{
    /// <summary>Marks a piece of a window (glass, frame or sill) so decorate mode can pick the whole window up.</summary>
    public class WallWindowPart : MonoBehaviour
    {
        public WindowWall wall;
        public int index;
    }
}
