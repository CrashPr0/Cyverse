using System.Collections.Generic;
using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// The shared finishes of the props built in code (stations, kiosks,
    /// cabinets, furniture, ceiling fixtures), kept as material files in
    /// Resources/Palette so an artist can recolour them without touching
    /// code: edit a file in the Project window, or press Play, click a prop and
    /// edit its material (edits made in Play mode stay, since it is the file
    /// itself). See Resources/Palette/README.md for what each one colours.
    ///
    /// A missing file falls back to the colour the code asks for, so nothing
    /// breaks if one is deleted.
    /// </summary>
    public static class Palette
    {
        private const string Folder = "Palette/";

        private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        private static readonly HashSet<Material> files = new HashSet<Material>();
        private static readonly HashSet<string> missing = new HashSet<string>();

        /// <summary>Finishes picked by the colour a builder asks BlockKit.Mat
        /// for, so every prop that uses the house body, trim, recess or chrome
        /// colour shares the one file.</summary>
        private static readonly (Color colour, string name)[] Finishes =
        {
            (new Color(0.075f, 0.085f, 0.115f), "Body"),
            (new Color(0.17f, 0.19f, 0.25f), "Trim"),
            (new Color(0.035f, 0.04f, 0.06f), "Recess"),
            (new Color(0.02f, 0.025f, 0.04f), "RecessDeep"),
            (new Color(0.58f, 0.61f, 0.68f), "Chrome"),
            (new Color(0.13f, 0.14f, 0.19f), "CaseBody"),
        };

        /// <summary>The finish for a colour BlockKit.Mat is asked for, or null.</summary>
        public static string FinishFor(Color colour)
        {
            foreach ((Color c, string name) in Finishes)
                if (c == colour) return name;
            return null;
        }

        /// <summary>The palette material <paramref name="name"/>; without its
        /// file, a material from the given values (made once and shared).</summary>
        public static Material Get(string name, Color colour, float smoothness, float metallic) =>
            Load(name) ?? Fallback(name, () => BuildKit.MakeStandard(colour, smoothness, metallic));

        /// <summary>A glowing palette material (lamps, LEDs); without its file,
        /// BuildKit.MakeEmissive with the given values.</summary>
        public static Material Glow(string name, Color colour, float strength) =>
            Load(name) ?? Fallback(name, () => BuildKit.MakeEmissive(colour, strength));

        /// <summary>True for a material loaded from the palette folder: load-time
        /// passes leave these alone, so the file is what you see.</summary>
        public static bool Contains(Material material) => material != null && files.Contains(material);

        private static Material Load(string name)
        {
            if (cache.TryGetValue(name, out Material m) && m != null) return m;
            if (missing.Contains(name)) return null;
            m = Resources.Load<Material>(Folder + name);
            if (m == null) { missing.Add(name); return null; }
            cache[name] = m;
            files.Add(m);
            return m;
        }

        private static Material Fallback(string name, System.Func<Material> make)
        {
            string key = "fallback:" + name;
            if (cache.TryGetValue(key, out Material m) && m != null) return m;
            return cache[key] = make();
        }
    }
}
