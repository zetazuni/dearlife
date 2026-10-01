using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Dearlife
{
    /// <summary>
    /// The grid of move mode (v0.54.0), switched with G. While it is on, a piece that is moved stands square to it (turned
    /// in quarter turns) and the edges of its box lie on the grid's lines; holding Alt places it freely. This class holds
    /// the arithmetic of that snap and draws the grid: fine lines every <see cref="Cell"/>, stronger ones every metre,
    /// over the plot, at the height of the floor that is being worked on.
    /// </summary>
    public class MoveGrid : MonoBehaviour
    {
        public const float Cell = 0.25f;
        const string Pref = "dearlife.grid";

        static bool? on;
        /// <summary>Whether the grid is switched on (remembered between sessions, the same for every save).</summary>
        public static bool On
        {
            get { if (!on.HasValue) on = PlayerPrefs.GetInt(Pref, 1) == 1; return on.Value; }
            set { on = value; PlayerPrefs.SetInt(Pref, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>The nearest quarter turn.</summary>
        public static Quaternion Square(Quaternion rot) => Quaternion.Euler(0f, Mathf.Round(rot.eulerAngles.y / 90f) * 90f, 0f);

        /// <summary>
        /// Where a piece standing square goes so that its box lies on the grid: on each axis the nearer of its two
        /// edges is brought onto a line, so it can stand flush against a wall on either side whatever its size.
        /// </summary>
        public static void Snap(Bounds local, Quaternion rot, ref float x, ref float z)
        {
            var off = rot * new Vector3(local.center.x, 0f, local.center.z);
            var right = rot * Vector3.right; var fwd = rot * Vector3.forward;
            float hx = Mathf.Abs(right.x) * local.extents.x + Mathf.Abs(fwd.x) * local.extents.z;
            float hz = Mathf.Abs(right.z) * local.extents.x + Mathf.Abs(fwd.z) * local.extents.z;
            x += Nearest(x + off.x - hx, x + off.x + hx);
            z += Nearest(z + off.z - hz, z + off.z + hz);
        }

        static float Nearest(float min, float max)
        {
            float a = Mathf.Round(min / Cell) * Cell - min, b = Mathf.Round(max / Cell) * Cell - max;
            return Mathf.Abs(a) <= Mathf.Abs(b) ? a : b;
        }

        // ------------------------------------------------------------------ the lines

        static MoveGrid instance;
        MeshRenderer fine, strong;
        Rect drawn;
        float height;

        /// <summary>Shows the grid at this height, or hides it.</summary>
        public static void Show(bool visible, float y)
        {
            if (!instance)
            {
                if (!visible) return;
                var go = new GameObject("Move grid");
                instance = go.AddComponent<MoveGrid>();
            }
            instance.Set(visible, y);
        }

        void Set(bool visible, float y)
        {
            if (!visible) { if (gameObject.activeSelf) gameObject.SetActive(false); return; }
            var b = LotManager.Bounds;
            if (!fine || b != drawn) Build(b);
            height = Mathf.Lerp(height, y, Mathf.Abs(height - y) > 1f ? 1f : 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            transform.position = new Vector3(0f, height + 0.006f, 0f);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        void Build(Rect b)
        {
            drawn = b;
            foreach (Transform c in transform) Destroy(c.gameObject);
            // whole cells, a little beyond the plot
            float x0 = Mathf.Floor(b.xMin / Cell) * Cell, x1 = Mathf.Ceil(b.xMax / Cell) * Cell;
            float z0 = Mathf.Floor(b.yMin / Cell) * Cell, z1 = Mathf.Ceil(b.yMax / Cell) * Cell;
            fine = Lines("Fine lines", x0, x1, z0, z1, false, new Color(1f, 1f, 1f, 0.10f));
            strong = Lines("Metre lines", x0, x1, z0, z1, true, new Color(1f, 1f, 1f, 0.30f));
        }

        MeshRenderer Lines(string name, float x0, float x1, float z0, float z1, bool metres, Color colour)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            int nx = Mathf.RoundToInt((x1 - x0) / Cell), nz = Mathf.RoundToInt((z1 - z0) / Cell);
            for (int i = 0; i <= nx; i++)
            {
                float x = x0 + i * Cell;
                if (IsMetre(x) != metres) continue;
                verts.Add(new Vector3(x, 0f, z0)); verts.Add(new Vector3(x, 0f, z1));
            }
            for (int i = 0; i <= nz; i++)
            {
                float z = z0 + i * Cell;
                if (IsMetre(z) != metres) continue;
                verts.Add(new Vector3(x0, 0f, z)); verts.Add(new Vector3(x1, 0f, z));
            }
            var idx = new int[verts.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetIndices(idx, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var m = new Material(Shader.Find("HDRP/Unlit")) { name = name };
            HDMaterial.SetSurfaceType(m, true);
            m.SetColor("_UnlitColor", colour);
            HDMaterial.ValidateMaterial(m);
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        static bool IsMetre(float v) => Mathf.Abs(v - Mathf.Round(v)) < 0.01f;
    }
}
