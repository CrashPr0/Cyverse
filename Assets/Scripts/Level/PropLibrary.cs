using System.Collections.Generic;
using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Art indirection layer: lets authored model prefabs replace the
    /// procedural primitive props WITHOUT freezing a scene.
    ///
    /// The problem this solves: when art is baked into a saved .unity file,
    /// it serves that one scene — future levels start ugly, and every
    /// gameplay change has to be re-applied into a huge scene by hand. When
    /// art lives in prefabs instead, the factory picks it up everywhere, in
    /// every level, including levels that don't exist yet.
    ///
    /// Contract: drop a prefab at `Assets/Resources/Props/&lt;Name&gt;.prefab`
    /// and the matching PropFactory builder uses it automatically. Miss, and
    /// the builder falls back to its primitive version — so this is safe to
    /// ship before a single prefab exists, and safe to migrate one prop at a
    /// time.
    ///
    /// Prefab authoring rules (see SETUP.md):
    ///  - pivot at floor level, centred on the footprint
    ///  - +Z is the prop's FRONT (the side a person faces / sits at)
    ///  - roughly match the primitive's footprint so layouts stay collision-free
    ///  - include colliders on anything solid; the factory does not add them
    /// </summary>
    public static class PropLibrary
    {
        public const string ResourcePath = "Props/";

        // Resources.Load returns null every call for a missing asset, so
        // remember misses too — these builders run dozens of times per scene.
        private static readonly Dictionary<string, GameObject> cache =
            new Dictionary<string, GameObject>();

        /// <summary>True if an art prefab exists for this prop name.</summary>
        public static bool Has(string propName) => Lookup(propName) != null;

        /// <summary>
        /// Instantiate the art prefab for <paramref name="propName"/>, or
        /// return null if none is installed (caller then builds primitives).
        /// </summary>
        public static GameObject TrySpawn(string propName, Transform parent,
            Vector3 localPos, float rotY, bool markStatic = true)
        {
            var prefab = Lookup(propName);
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab);
            go.name = propName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);

            if (markStatic)
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    t.gameObject.isStatic = true;

            return go;
        }

        private static GameObject Lookup(string propName)
        {
            if (cache.TryGetValue(propName, out var cached)) return cached;
            var loaded = Resources.Load<GameObject>(ResourcePath + propName);
            cache[propName] = loaded;
            return loaded;
        }

        /// <summary>Forget cached lookups — call after adding prefabs so the
        /// editor scene builders pick them up without a domain reload.</summary>
        public static void ClearCache() => cache.Clear();
    }
}
