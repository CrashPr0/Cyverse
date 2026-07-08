using UnityEngine;

namespace Cyverse.Core
{
    /// <summary>
    /// The player's chosen callsign, entered on the title screen and woven
    /// through the dialogue, badge, and results (endowment: it's *their*
    /// recruit, not a generic one). Persists between sessions; falls back to
    /// the script's canonical ID when unset.
    /// </summary>
    public static class PlayerIdentity
    {
        public const string DefaultCallsign = "CY95192";
        private const string PrefKey = "cv_callsign";

        private static string callsign;
        private static bool loaded;

        public static string Callsign
        {
            get
            {
                if (!loaded) { callsign = PlayerPrefs.GetString(PrefKey, ""); loaded = true; }
                return string.IsNullOrEmpty(callsign) ? DefaultCallsign : callsign;
            }
            set
            {
                callsign = Sanitize(value);
                loaded = true;
                PlayerPrefs.SetString(PrefKey, callsign);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Raw saved value (may be empty) for pre-filling the entry field.</summary>
        public static string SavedRaw => PlayerPrefs.GetString(PrefKey, "");

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                    sb.Append(char.ToUpperInvariant(c));
                if (sb.Length >= 12) break;
            }
            return sb.ToString();
        }
    }
}
