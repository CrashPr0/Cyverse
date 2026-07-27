using UnityEngine;

namespace Cyverse.Core
{
    /// <summary>
    /// Maps a level's base scene name to the best version actually available
    /// in this build.
    ///
    /// Each level can exist twice: a procedural scene (a lone bootstrap that
    /// generates the room at Play time) and an artist-authored "visual pass"
    /// with real models and hand-placed lighting. The visual pass is always
    /// preferred when it is present in Build Settings; otherwise we silently
    /// fall back, so a build that ships only the procedural scenes still runs.
    ///
    /// Resolution happens at RUNTIME rather than being baked into scene files,
    /// which matters because doors are serialized inside hand-authored scenes:
    /// a door saved pointing at "Level1_IAM" upgrades itself to the visual
    /// pass without anyone re-editing the Hub.
    /// </summary>
    public static class SceneCatalog
    {
        /// <summary>base scene name → richer variants, best first.</summary>
        private static readonly string[][] Variants =
        {
            new[] { "Level0 Visual Pass", "Level0" },
            new[] { "Level1_IAM_VisualPass", "Level1_IAM" },
            new[] { "Level1_VisualPass", "Level1" },
            new[] { "Level3_Forensics_VisualPass", "Level3_Forensics" },
            new[] { "Hub_VisualPass", "Hub" },
        };

        /// <summary>Best available scene for the given name. Accepts either a
        /// base name or a variant name, so it is safe to call repeatedly.</summary>
        public static string Preferred(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return sceneName;

            foreach (var chain in Variants)
            {
                bool inChain = false;
                foreach (var name in chain)
                    if (string.Equals(name, sceneName, System.StringComparison.OrdinalIgnoreCase))
                    { inChain = true; break; }
                if (!inChain) continue;

                foreach (var candidate in chain)
                    if (Application.CanStreamedLevelBeLoaded(candidate)) return candidate;

                return chain[chain.Length - 1]; // none loadable: keep the base name
            }
            return sceneName; // not a level we track
        }
    }
}
