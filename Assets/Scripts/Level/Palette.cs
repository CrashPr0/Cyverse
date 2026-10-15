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
    ///
    /// Each room's accent (the colour of its trim, floor grid, signs and
    /// station glows) is a palette file too, Accent&lt;Room&gt;; see Accent,
    /// Tint and AccentRecolor.
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

        // ---- Room accents ----------------------------------------------------

        /// <summary>Each room's accent as the code was written with it. The
        /// AccentHub ... AccentLevel4 files start at these values.</summary>
        private static readonly Dictionary<string, Color> AccentDefaults = new Dictionary<string, Color>
        {
            { "Hub", new Color(0.90f, 0.66f, 0.14f) },
            { "Orientation", new Color(0.25f, 0.80f, 1.00f) },
            { "Level1", new Color(0.25f, 0.65f, 1.00f) },
            { "Level2", new Color(0.95f, 0.42f, 0.32f) },
            { "Level3", new Color(0.30f, 1.00f, 0.55f) },
            { "Level4", new Color(1.00f, 0.42f, 0.25f) },
        };

        /// <summary>A room's accent: the Albedo colour of its Accent file
        /// (Hub, Orientation, Level1 ... Level4).</summary>
        public static Color Accent(string room)
        {
            Color fallback = AccentDefaults.TryGetValue(room, out Color d) ? d : BuildKit.AccentCyan;
            Material file = Load("Accent" + room);
            if (file == null || !file.HasProperty("_Color")) return fallback;
            Color c = file.color;
            c.a = 1f;
            return c;
        }

        /// <summary>True when the room's accent file differs from the colour the
        /// code was written with (so its props need shifting).</summary>
        public static bool AccentChanged(string room)
        {
            if (!AccentDefaults.TryGetValue(room, out Color d)) return false;
            Color c = Accent(room);
            return Mathf.Abs(c.r - d.r) + Mathf.Abs(c.g - d.g) + Mathf.Abs(c.b - d.b) > 0.004f;
        }

        /// <summary>A colour written for the room's original accent (a dimmer
        /// floor-line shade, a nearby glow), moved the way the accent moved: its
        /// hue turns by the same amount, and its saturation and brightness scale
        /// the same way, so shades keep their relation to the accent. Unchanged
        /// while the accent file is at its original colour.</summary>
        public static Color Tint(string room, Color c)
        {
            if (!AccentDefaults.TryGetValue(room, out Color d) || !AccentChanged(room)) return c;
            Color.RGBToHSV(d, out float dh, out float ds, out float dv);
            Color.RGBToHSV(Accent(room), out float nh, out float ns, out float nv);
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(nh + (h - dh), 1f);
            s = Mathf.Clamp01(ds > 0.001f ? s * ns / ds : ns);
            v = dv > 0.001f ? v * nv / dv : v;
            Color result = Color.HSVToRGB(h, s, v, true);
            result.a = c.a;
            return result;
        }

        /// <summary>True for a colour that reads as the room's original accent or
        /// a shade of it: within 6 degrees of its hue and similar saturation.</summary>
        public static bool IsAccentShade(string room, Color c)
        {
            if (!AccentDefaults.TryGetValue(room, out Color d)) return false;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            if (v < 0.03f || s < 0.3f) return false;
            Color.RGBToHSV(d, out float dh, out float ds, out _);
            float hueGap = Mathf.Abs(Mathf.DeltaAngle(h * 360f, dh * 360f));
            return hueGap <= 6f && Mathf.Abs(s - ds) <= 0.25f;
        }

        /// <summary>The accent room a scene belongs to, or null.</summary>
        public static string RoomForScene(string sceneName)
        {
            switch (Core.SceneCatalog.Preferred(sceneName))
            {
                case "Hub": return "Hub";
                case "Level0": return "Orientation";
                case "Level1_IAM": return "Level1";
                case "Level2_CyberDefense": return "Level2";
                case "Level3_Forensics": return "Level3";
                case "Level4_CyberAttack": return "Level4";
                default: return null;
            }
        }

        // ---- Finishes ----------------------------------------------------------

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
