using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>The list of everything that can be bought (filled in by the builder).</summary>
    public class Catalog : MonoBehaviour
    {
        public static Catalog Instance { get; private set; }
        public List<CatalogEntry> entries = new List<CatalogEntry>();

        void Awake() { Instance = this; }

        public CatalogEntry Find(string id) { foreach (var e in entries) if (e.id == id) return e; return null; }
        public IEnumerable<string> Categories()
        {
            var seen = new List<string>();
            foreach (var e in entries) if (!seen.Contains(e.category)) seen.Add(e.category);
            return seen;
        }

        void Start() { PurchaseSave.Restore(this); }
    }
}
