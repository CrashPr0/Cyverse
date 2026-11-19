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
    /// Orientation (Level 0) asks for the callsign only when the Hub title
    /// card has not already taken one this playthrough.
    /// </summary>
    public sealed class Level0CallsignPlayModeTests
    {
        private const string Key = "cv_callsign";

        [TearDown]
        public void Cleanup()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            Identity().GetMethod("ForgetCached").Invoke(null, null);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Level0_SkipsTheTitle_WhenTheCallsignIsAlreadyChosen()
        {
            PlayerPrefs.SetString(Key, "TESTER");
            PlayerPrefs.Save();
            Identity().GetMethod("ForgetCached").Invoke(null, null);

            SceneManager.LoadScene("Level0", LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;

            Assert.That(TitleActive(), Is.False,
                "Orientation should not ask for the callsign again after the Hub title.");
            Assert.That((string)Identity().GetProperty("Callsign").GetValue(null), Is.EqualTo("TESTER"));
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator Level0_ShowsTheTitle_WhenNoCallsignWasChosen()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            Identity().GetMethod("ForgetCached").Invoke(null, null);

            SceneManager.LoadScene("Level0", LoadSceneMode.Single);
            for (int i = 0; i < 8; i++) yield return null;

            Assert.That(TitleActive(), Is.True,
                "Entering Orientation without a callsign (no Hub title) should still ask for one.");

            // Close it the way Enter does, so the title's modal is released.
            Type menu = FindType("Cyverse.UI.MainMenu");
            object instance = menu.GetProperty("Instance").GetValue(null);
            menu.GetMethod("Dismiss", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, null);
            yield return null;
            Assert.That(TitleActive(), Is.False);
        }

        private static bool TitleActive() =>
            (bool)FindType("Cyverse.UI.MainMenu").GetProperty("Active").GetValue(null);

        private static Type Identity()
        {
            Type t = FindType("Cyverse.Core.PlayerIdentity");
            Assert.That(t, Is.Not.Null);
            return t;
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
