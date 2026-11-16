using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Cyverse.Tests
{
    /// <summary>
    /// Unity's built-in font ships in WebGL builds as a small subset with no
    /// em dash, arrows, bullet or ellipsis, so HUD text and signs showed gaps
    /// in the browser. The game now loads its own font; these tests keep it
    /// in place and keep every 3D sign on it.
    /// </summary>
    public class GameFontPlayModeTests
    {
        // Em dash, en dash, ellipsis, bullet, middle dot, four arrows, angle
        // quotes, curly quotes, squares, triangles, circle, multiply, minus,
        // degree, e-acute: every non-ASCII character the game's text uses,
        // except the check mark and play triangle the code already avoids.
        private const string NeededCharacters =
            "—–…•·←↑→↓‹›" +
            "“”’■□▲▼●×−°é";

        [Test]
        public void GameFont_IsTheShippedAsset()
        {
            Font shipped = Resources.Load<Font>("Fonts/CyverseSans");
            Assert.That(shipped, Is.Not.Null, "Assets/Resources/Fonts/CyverseSans.ttf is missing.");
            Assert.That(shipped.dynamic, Is.True, "The game font must stay a dynamic font.");
            Assert.That(LoadGameFont(), Is.SameAs(shipped),
                "HudUI.LoadFont should return the game's own font, not Unity's built-in one.");
        }

        [Test]
        public void GameFont_HasEveryCharacterTheGameUses()
        {
            Font font = Resources.Load<Font>("Fonts/CyverseSans");
            Assert.That(font, Is.Not.Null);
            foreach (char c in NeededCharacters)
                Assert.That(font.HasCharacter(c), Is.True,
                    $"The game font has no glyph for U+{(int)c:X4}.");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator SavedSigns_AreMovedOntoTheGameFont()
        {
            // These scenes hold signs saved with Unity's built-in font.
            foreach (string scene in new[] { "Hub", "Level2_CyberDefense" })
            {
                SceneManager.LoadScene(scene, LoadSceneMode.Single);
                yield return null;
                yield return null;
                yield return null;

                Type managerType = FindType("Cyverse.Level.WorldTextLayoutManager");
                var manager = UnityEngine.Object.FindObjectOfType(managerType);
                Assert.That(manager, Is.Not.Null, scene + " has no WorldTextLayoutManager.");
                managerType.GetMethod("RefreshNow").Invoke(manager, null);

                Font font = LoadGameFont();
                int checkedSigns = 0;
                foreach (TextMesh sign in UnityEngine.Object.FindObjectsOfType<TextMesh>(true))
                {
                    if (!sign.gameObject.scene.IsValid()) continue;
                    checkedSigns++;
                    Assert.That(sign.font, Is.SameAs(font),
                        $"{scene}: sign '{sign.name}' ('{sign.text}') is not on the game font, " +
                        "so it would draw from the wrong glyph atlas.");
                }
                Assert.That(checkedSigns, Is.GreaterThan(0), scene + " should contain 3D signs.");
            }
        }

        private static Font LoadGameFont()
        {
            Type hud = FindType("Cyverse.UI.HudUI");
            return (Font)hud.GetMethod("LoadFont", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            Assert.Fail("Type not found: " + fullName);
            return null;
        }
    }
}
