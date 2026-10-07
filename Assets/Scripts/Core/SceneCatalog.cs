namespace Cyverse.Core
{
    /// <summary>
    /// Maps every current or legacy level name to its canonical production
    /// scene.
    ///
    /// Older builds shipped separate "visual pass" scenes. Those copies are no
    /// longer authoritative, but their names remain here as aliases so saved
    /// Hub doors and old links self-heal to the maintained scene.
    ///
    /// Resolution happens at RUNTIME rather than being baked into scene files,
    /// which matters because doors are serialized inside hand-authored scenes:
    /// a door saved pointing at "Level1_IAM_VisualPass" is downgraded safely to
    /// "Level1_IAM" without requiring a player save reset.
    /// </summary>
    public static class SceneCatalog
    {
        /// <summary>Canonical scene first, followed by recognized legacy aliases.</summary>
        private static readonly string[][] Variants =
        {
            new[] { "Level0", "Level0 Visual Pass" },
            new[] { "Level1_IAM", "Level1_IAM_VisualPass" },
            // The Hub's saved Level 2 door points at "Level1" (named before
            // the renumbering); always route it to the maintained scene.
            new[] { "Level2_CyberDefense", "Level2_CyberDefense_VisualPass", "Level1" },
            new[] { "Level3_Forensics", "Level3_Forensics_VisualPass" },
            new[] { "Level4_CyberAttack", "Level4_CyberAttack_VisualPass" },
            new[] { "Hub", "Hub_VisualPass" },
        };

        /// <summary>Canonical production scene for the given name. Accepts a
        /// canonical name or legacy alias, so it is safe to call repeatedly.</summary>
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

                return chain[0];
            }
            return sceneName; // not a level we track
        }
    }
}
