using System.Runtime.InteropServices;

namespace Cyverse.Audio
{
    /// <summary>
    /// Text-to-speech via the browser's Web Speech API (WebGL builds only).
    /// Free, key-less, and offline-capable — it uses the player's local OS
    /// voices. On other platforms (including the editor) it reports
    /// unavailable and no-ops, so captions remain the guaranteed channel.
    /// </summary>
    public static class Speech
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int Cyverse_SpeechAvailable();
        [DllImport("__Internal")] private static extern void Cyverse_Speak(string text, float rate, float pitch, float volume);
        [DllImport("__Internal")] private static extern void Cyverse_CancelSpeech();

        public static bool Available => Cyverse_SpeechAvailable() == 1;
        public static void Speak(string text, float rate, float pitch, float volume)
            => Cyverse_Speak(text, rate, pitch, volume);
        public static void Cancel() => Cyverse_CancelSpeech();
#else
        public static bool Available => false;
        public static void Speak(string text, float rate, float pitch, float volume) { }
        public static void Cancel() { }
#endif
    }
}
