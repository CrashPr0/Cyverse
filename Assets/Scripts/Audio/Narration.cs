using System.Collections.Generic;
using UnityEngine;

namespace Cyverse.Audio
{
    /// <summary>
    /// Recorded voice-over clips in Resources/Audio/Narration. A missing clip
    /// returns null, which DialogueManager treats as "no recording" and falls
    /// back to browser text-to-speech, so content never depends on the files.
    /// </summary>
    public static class Narration
    {
        private const string Folder = "Audio/Narration/";
        private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static AudioClip Clip(string name)
        {
            if (cache.TryGetValue(name, out AudioClip clip) && clip != null) return clip;
            clip = Resources.Load<AudioClip>(Folder + name);
            cache[name] = clip;
            return clip;
        }
    }
}
