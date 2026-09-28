using UnityEngine;

namespace Dearlife
{
    /// <summary>Adds Interactable to every piece of furniture that has something to do, and to the pool.</summary>
    public class InteractionSetup : MonoBehaviour
    {
        void Start() { Refresh(); }

        /// <summary>A pool the player dug: people can swim in it.</summary>
        public static void AddPool(PoolRipples pr, Transform parent, float groundY)
        {
            var go = new GameObject("Pool interaction (built)");
            go.transform.SetParent(parent, true);
            var c = new Vector3((pr.min.x + pr.max.x) * 0.5f, pr.surfaceY, (pr.min.y + pr.max.y) * 0.5f);
            go.transform.position = c;
            var it = Interactable.Attach(go, "pool");
            if (it == null) return;
            it.hasCustomStand = true;
            it.poolHalf = new Vector2((pr.max.x - pr.min.x) * 0.5f, (pr.max.y - pr.min.y) * 0.5f);
            it.customStand = new Vector3(c.x, groundY, pr.min.y - 0.9f);
            it.customFace = c;
        }

        public static void Refresh()
        {
            foreach (var f in Object.FindObjectsByType<Furniture>(FindObjectsInactive.Include))
            {
                if (f && f.GetComponent<Interactable>() == null) Interactable.Attach(f.gameObject, f.name);
                // pieces saved in the scene before they had a place to sit or lie (the bath) get it now
                if (f && f.GetComponentInChildren<UseSpot>(true) == null) SeatSpots.Add(f.gameObject, InteractionTable.BaseId(f.name));
                // the pets' bowls and bed
                string bid = f ? InteractionTable.BaseId(f.name) : "";
                if ((bid == "petbowls" || bid == "petbed") && f.GetComponent<PetThing>() == null)
                    f.gameObject.AddComponent<PetThing>().kind = bid == "petbed" ? PetThing.Kind.Bed : PetThing.Kind.Bowls;
            }
            if (!GameObject.Find("Pool interaction"))
            {
                PoolRipples big = null; float area = 0f;
                foreach (var pr in Object.FindObjectsByType<PoolRipples>(FindObjectsInactive.Include))
                {
                    if (pr.round) continue;                                                // the fountain
                    float a = (pr.max.x - pr.min.x) * (pr.max.y - pr.min.y);
                    if (a > area) { area = a; big = pr; }                                  // the pool, not the tub
                }
                if (big != null)
                {
                    var go = new GameObject("Pool interaction");
                    go.transform.SetParent(big.transform.parent, true);
                    var c = new Vector3((big.min.x + big.max.x) * 0.5f, big.surfaceY, (big.min.y + big.max.y) * 0.5f);
                    go.transform.position = c;
                    var it = Interactable.Attach(go, "pool");
                    if (it != null)
                    {
                        it.hasCustomStand = true;
                        it.customStand = new Vector3(c.x - 2.5f, -0.3f, big.min.y - 0.9f);
                        it.customFace = c;
                        it.poolHalf = new Vector2((big.max.x - big.min.x) * 0.5f, (big.max.y - big.min.y) * 0.5f);
                    }
                }
            }
        }
    }
}
