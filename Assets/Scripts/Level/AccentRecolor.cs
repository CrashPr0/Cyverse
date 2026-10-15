using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cyverse.Level
{
    /// <summary>
    /// Carries an edited room accent (Palette.Accent) into what a scene file
    /// saved with the original colour: the Hub, the Orientation room and the
    /// Level 2 SOC keep their trim, signs, lights and floor lines in the scene.
    /// As a scene loads, before any prop is built, every glow, hologram, floor
    /// line, light and sign saved in it that reads as the room's accent is
    /// moved to the new colour (Palette.Tint), keeping its shade.
    ///
    /// Only objects loaded from the scene file are touched; props built in code
    /// already take their accent from the palette. Materials are copied, never
    /// edited, so other scenes and the project's files are unaffected. Nothing
    /// runs while the accent files keep their original colours.
    /// </summary>
    public static class AccentRecolor
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Runs after the scene's Awake and OnEnable calls, before any Start,
        // so props that read their colour from a saved object read the new one.
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            string room = Palette.RoomForScene(scene.name);
            if (room == null || !Palette.AccentChanged(room)) return;
            Apply(scene, room);
        }

        public static void Apply(Scene scene, string room)
        {
            var done = new Dictionary<Material, Material>();
            var doneShell = new Dictionary<Material, Material>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                // The player's own hands and HUD are not part of the room.
                if (root.name == "Player" || root.GetComponent<Canvas>() != null) continue;

                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!FromScene(r)) continue;
                    // The room shell's wall bands and floor trim are the accent by
                    // definition, even where a scene saved them in a paler tint.
                    bool shell = r.name.StartsWith("Strip_") || r.name.StartsWith("Trim_");
                    Dictionary<Material, Material> cache = shell ? doneShell : done;
                    Material[] mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material m = mats[i];
                        if (m == null) continue;
                        if (!cache.TryGetValue(m, out Material result))
                            cache[m] = result = Recolored(m, room, shell);
                        if (result == m) continue;
                        mats[i] = result;
                        changed = true;
                    }
                    if (changed) r.sharedMaterials = mats;
                }

                foreach (Light l in root.GetComponentsInChildren<Light>(true))
                    if (FromScene(l) && Palette.IsAccentShade(room, l.color))
                        l.color = Palette.Tint(room, l.color);

                foreach (TextMesh t in root.GetComponentsInChildren<TextMesh>(true))
                    if (FromScene(t) && Palette.IsAccentShade(room, t.color))
                        t.color = Palette.Tint(room, t.color);

                foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
                    if (FromScene(t) && Palette.IsAccentShade(room, t.color))
                        t.color = Palette.Tint(room, t.color);
            }
        }

        private static bool IsColoured(Color c)
        {
            Color.RGBToHSV(c, out _, out float s, out float v);
            return s >= 0.2f && v >= 0.03f;
        }

        /// <summary>Objects loaded from a scene file have positive instance
        /// ids; ones created at runtime have negative ones.</summary>
        private static bool FromScene(Object o) => o.GetInstanceID() > 0;

        /// <summary>A recoloured copy of <paramref name="m"/> when it is an
        /// accent glow, hologram or floor grid (or, on the room shell's trim,
        /// any coloured finish); otherwise <paramref name="m"/>.</summary>
        private static Material Recolored(Material m, string room, bool shell)
        {
            if (!FromScene(m) || Palette.Contains(m) || m.shader == null) return m;
            string shader = m.shader.name;
            if (shader.Contains("Text") || shader.Contains("Font")) return m;

            if (shader == "Cyverse/GridFloor")
            {
                if (!m.HasProperty("_LineColor") || !Palette.IsAccentShade(room, m.GetColor("_LineColor"))) return m;
                var floor = new Material(m);
                floor.SetColor("_LineColor", Palette.Tint(room, m.GetColor("_LineColor")));
                return floor;
            }

            if (!m.HasProperty("_Color")) return m;
            if (shell ? !IsColoured(m.GetColor("_Color")) : !Palette.IsAccentShade(room, m.GetColor("_Color"))) return m;
            var copy = new Material(m);
            copy.SetColor("_Color", Palette.Tint(room, m.GetColor("_Color")));
            if (m.HasProperty("_EmissionColor") && m.IsKeywordEnabled("_EMISSION"))
                copy.SetColor("_EmissionColor", Palette.Tint(room, m.GetColor("_EmissionColor")));
            return copy;
        }
    }
}
