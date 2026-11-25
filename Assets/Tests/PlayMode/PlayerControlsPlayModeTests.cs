using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Cyverse.Tests
{
    /// <summary>
    /// Player controls from playtest feedback: Shift sprints at twice the
    /// walking speed, and the keys have one job each: Tab closes a screen,
    /// Esc is Settings only.
    /// </summary>
    public sealed class PlayerControlsPlayModeTests
    {
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Sprint_IsTwiceWalkingSpeed_InHubAndGameRoom()
        {
            foreach (string scene in new[] { "Hub", "Level4_CyberAttack" })
            {
                SceneManager.LoadScene(scene, LoadSceneMode.Single);
                for (int i = 0; i < 4; i++) yield return null;

                Type controllerType = FindType("Cyverse.Player.FirstPersonController");
                Assert.That(controllerType, Is.Not.Null);
                Object player = Object.FindObjectOfType(controllerType);
                Assert.That(player, Is.Not.Null, $"{scene} should have a first-person player.");

                float walk = (float)controllerType.GetField("moveSpeed").GetValue(player);
                float sprint = (float)controllerType.GetProperty("SprintSpeed").GetValue(player);
                Assert.That(sprint, Is.EqualTo(walk * 2f).Within(0.01f),
                    $"Sprint in {scene} should be twice the walking speed.");
            }
        }

        [Test]
        public void KeyLabels_TabCloses_EscIsSettings()
        {
            Type input = FindType("Cyverse.Core.GameInput");
            Assert.That(input, Is.Not.Null);
            Assert.That(input.GetField("CancelLabel").GetRawConstantValue(), Is.EqualTo("TAB"));
            Assert.That(input.GetField("SettingsLabel").GetRawConstantValue(), Is.EqualTo("ESC"));
        }

        [Test]
        public void KeyRouting_EscOnlyOpensSettings_TabOnlyCloses()
        {
            Type settings = FindType("Cyverse.Settings.AccessibilitySettings");
            Assert.That(settings, Is.Not.Null);
            MethodInfo route = settings.GetMethod("RouteKeys", BindingFlags.Public | BindingFlags.Static);
            Assert.That(route, Is.Not.Null);
            string Route(bool esc, bool tab, bool settingsOpen, bool otherScreen,
                bool overlay = false, bool certificate = false, bool justPaused = false) =>
                route.Invoke(null, new object[] { esc, tab, settingsOpen, otherScreen, overlay, certificate, justPaused })
                    .ToString();

            // Free play.
            Assert.That(Route(esc: true, tab: false, settingsOpen: false, otherScreen: false), Is.EqualTo("OpenSettings"));
            Assert.That(Route(esc: false, tab: true, settingsOpen: false, otherScreen: false), Is.EqualTo("None"),
                "Tab must never open Settings.");
            // A station, form or arcade round is up: Tab is its close key, Esc only explains.
            Assert.That(Route(esc: true, tab: false, settingsOpen: false, otherScreen: true), Is.EqualTo("ShowCloseHint"),
                "Esc must not close or skip another screen.");
            Assert.That(Route(esc: false, tab: true, settingsOpen: false, otherScreen: true), Is.EqualTo("None"));
            // A quiz question or the results card: Settings may open over it.
            Assert.That(Route(esc: true, tab: false, settingsOpen: false, otherScreen: true, overlay: true), Is.EqualTo("OpenSettings"));
            Assert.That(Route(esc: true, tab: false, settingsOpen: false, otherScreen: true, overlay: true, certificate: true),
                Is.EqualTo("ShowCloseHint"));
            // Settings is up: either key resumes, except the Esc that just paused.
            Assert.That(Route(esc: true, tab: false, settingsOpen: true, otherScreen: false), Is.EqualTo("CloseSettings"));
            Assert.That(Route(esc: false, tab: true, settingsOpen: true, otherScreen: false), Is.EqualTo("CloseSettings"));
            Assert.That(Route(esc: true, tab: false, settingsOpen: true, otherScreen: false, justPaused: true), Is.EqualTo("None"));
            Assert.That(Route(esc: false, tab: false, settingsOpen: true, otherScreen: false), Is.EqualTo("None"));
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }
    }
}
