using System.Runtime.InteropServices;
using UnityEngine;

namespace Cyverse.UI
{
    /// <summary>
    /// Clipboard access that also works in the browser.
    ///
    /// On desktop this is a thin wrapper over GUIUtility.systemCopyBuffer. On
    /// WebGL that buffer is engine-internal — it never sees the OS clipboard —
    /// and the browser consumes Ctrl/Cmd+V itself instead of forwarding it as
    /// key input, so a paste can only arrive as the page's own paste event.
    /// Reads there are therefore asynchronous: the text is delivered to
    /// <paramref name="methodName"/> on <paramref name="receiverName"/> rather
    /// than returned.
    /// </summary>
    public static class Clipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void Cyverse_ClipboardInit(string target, string method);
        [DllImport("__Internal")] private static extern void Cyverse_ClipboardRequestRead(string target, string method);
        [DllImport("__Internal")] private static extern void Cyverse_ClipboardWrite(string text);
#endif

        /// <summary>Start routing browser paste events to a GameObject method
        /// (the receiver must take a single string). No-op off WebGL.</summary>
        public static void ListenForPaste(string receiverName, string methodName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Cyverse_ClipboardInit(receiverName, methodName);
#endif
        }

        /// <summary>Clipboard contents, or null when they can only be delivered
        /// asynchronously — on WebGL the text arrives at the ListenForPaste
        /// receiver instead, and callers should simply wait for it.</summary>
        public static string Read(string receiverName, string methodName)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Cyverse_ClipboardRequestRead(receiverName, methodName);
            return null;
#else
            return GUIUtility.systemCopyBuffer;
#endif
        }

        public static void Write(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Cyverse_ClipboardWrite(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }
    }
}
