using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Dearlife
{
    /// <summary>
    /// Builds the walkable surface for people and pets from the house's colliders. It bakes once shortly after the start
    /// (all floors switched on and all walls up for that moment) and again a second after furniture was moved. Doors and
    /// gates are left out of the bake, they open for whoever walks up to them.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class DearlifeNav : MonoBehaviour
    {
        public static DearlifeNav Instance { get; private set; }
        public static bool Ready { get; private set; }
        public static int AgentType { get; private set; }
        public const int StairsArea = 3;

        NavMeshSurface surface;
        float rebuildAt;

        void Awake()
        {
            Instance = this;
            Ready = false;       // (with domain reload switched off in the editor, this would still be true from the last time)
            var s = NavMesh.CreateSettings();
            s.agentRadius = 0.24f;
            s.agentHeight = 1.7f;
            s.agentClimb = 0.4f;
            s.agentSlope = 48f;
            s.overrideVoxelSize = true;
            s.voxelSize = 0.05f;
            AgentType = s.agentTypeID;
            surface = gameObject.AddComponent<NavMeshSurface>();
            surface.agentTypeID = AgentType;
            // only the home plot: the empty lots in the city each have their own surface (see Lot)
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(18f, 3f, 9f);
            surface.size = new Vector3(64f, 22f, 48f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
        }

        IEnumerator Start()
        {
            yield return new WaitForSeconds(1.6f);   // the furniture has settled by now
            Build();
            Ready = true;
        }

        public void RequestRebuild() => rebuildAt = Time.time + 1.2f;

        void Update()
        {
            if (rebuildAt > 0f && Time.time > rebuildAt && DecorateMode.Instance != null && !DecorateMode.Instance.Holding)
            {
                rebuildAt = 0f;
                Build();
                var lot = LotManager.Current;
                if (lot != null && !lot.home) lot.RebuildNav();
            }
        }

        void Build()
        {
            var view = HouseView.Instance;
            bool upperWas = view && view.upperFloor && view.upperFloor.activeSelf;
            bool roofWas = view && view.roof && view.roof.activeSelf;
            if (view && view.upperFloor) view.upperFloor.SetActive(true);
            if (view && view.roof) view.roof.SetActive(false);
            foreach (var w in WallCutaway.All) w.BakeMode(true);
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            foreach (var w in WallCutaway.All) w.BakeMode(false);
            if (view && view.upperFloor) view.upperFloor.SetActive(upperWas);
            if (view && view.roof) view.roof.SetActive(roofWas);
        }
    }
}
