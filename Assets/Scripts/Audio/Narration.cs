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
        private static readonly Dictionary<string, byte[]> envelopes = new Dictionary<string, byte[]>();

        public static AudioClip Clip(string name)
        {
            if (cache.TryGetValue(name, out AudioClip clip) && clip != null) return clip;
            clip = Resources.Load<AudioClip>(Folder + name);
            cache[name] = clip;
            return clip;
        }

        /// <summary>The clip's loudness at 30 frames per second, 0-255 per frame
        /// (<c>name_env.bytes</c>, precomputed offline because WebGL cannot read
        /// audio samples at runtime). Null when the clip has none.</summary>
        public static byte[] Envelope(string name)
        {
            if (envelopes.TryGetValue(name, out byte[] data)) return data;
            var asset = Resources.Load<TextAsset>(Folder + name + "_env");
            data = asset != null ? asset.bytes : null;
            envelopes[name] = data;
            return data;
        }
    }
}
