using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Tracks which glossary entries have been discovered (codex mechanic).
    /// General-knowledge entries (no Topic) are always unlocked; station-tagged
    /// entries unlock the moment that station is reviewed. Persists across
    /// sessions via PlayerPrefs so the collection carries over between plays.
    /// </summary>
    public static class GlossaryProgress
    {
        private const string PrefKey = "cv_glossary_unlocked";
        private static HashSet<int> unlocked;

        private static void EnsureLoaded()
        {
            if (unlocked != null) return;
            unlocked = new HashSet<int>();

            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(raw))
                foreach (var part in raw.Split(','))
                    if (int.TryParse(part, out int idx)) unlocked.Add(idx);

            var entries = GlossaryContent.Entries;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].Topic == null) unlocked.Add(i);
        }

        public static bool IsUnlocked(int index)
        {
            EnsureLoaded();
            return unlocked.Contains(index);
        }

        public static int UnlockedCount { get { EnsureLoaded(); return unlocked.Count; } }
        public static int TotalCount => GlossaryContent.Entries.Length;

        /// <summary>Unlocks every entry tagged with this topic. Returns how many
        /// were newly discovered (0 if the player already knew them all).</summary>
        public static int UnlockTopic(StationSetup.Topic topic)
        {
            EnsureLoaded();
            var entries = GlossaryContent.Entries;
            int newlyUnlocked = 0;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].Topic == topic && unlocked.Add(i))
                    newlyUnlocked++;

            if (newlyUnlocked > 0)
            {
                PlayerPrefs.SetString(PrefKey, string.Join(",", unlocked.Select(i => i.ToString())));
                PlayerPrefs.Save();
            }
            return newlyUnlocked;
        }
    }
}
