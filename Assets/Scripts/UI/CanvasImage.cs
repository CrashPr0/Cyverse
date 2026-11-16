using System.Runtime.InteropServices;

namespace Cyverse.UI
{
    /// <summary>
    /// Saves the game view as a PNG through the browser (WebGL builds only).
    /// Call <see cref="Save"/> at the end of a frame, after rendering, or the
    /// picture comes out blank. Elsewhere it reports unsupported and no-ops.
    /// </summary>
    public static class CanvasImage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int Cyverse_SaveCanvasImage(string fileName);

        public static bool Supported => true;
        public static bool Save(string fileName) => Cyverse_SaveCanvasImage(fileName) == 1;
#else
        public static bool Supported => false;
        public static bool Save(string fileName) => false;
#endif
    }
}
