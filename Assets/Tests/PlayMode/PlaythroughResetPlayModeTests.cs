using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Cyverse.Tests
{
    /// <summary>A reload wipes the last playthrough but keeps the player's settings.</summary>
    public sealed class PlaythroughResetPlayModeTests
    {
        private static readonly string[] ProgressKeys =
        {
            "cv_done_1", "cv_done_4", "cv_hard_done_2", "cv_hard_best_3", "cv_best",
            "cv_callsign", "cv_seen_controls", "cv_glossary_unlocked",
            "cv_soc_evidence_json", "cv_soc_playbook_solved", "cv_secplus_best_Investigation",
        };

        private static readonly string[] KeptKeys = { "cv_master", "cv_scenario_roster_index" };

        [TearDown]
        public void Cleanup()
        {
            // CI runs in a fresh container, so returning these keys to absent
            // loses nothing (the launch reset clears them anyway).
            foreach (string key in ProgressKeys) PlayerPrefs.DeleteKey(key);
            foreach (string key in KeptKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        [Test]
        public void ClearSavedPlaythrough_WipesProgressAndKeepsSettings()
        {
            foreach (string key in ProgressKeys)
            {
                if (key == "cv_callsign" || key == "cv_glossary_unlocked" || key == "cv_soc_evidence_json")
                    PlayerPrefs.SetString(key, "x");
                else
                    PlayerPrefs.SetInt(key, 1);
            }
            PlayerPrefs.SetFloat("cv_master", 0.4f);
            PlayerPrefs.SetInt("cv_scenario_roster_index", 2);
            PlayerPrefs.Save();

            Type reset = Type.GetType("Cyverse.Core.PlaythroughReset, Assembly-CSharp");
            Assert.That(reset, Is.Not.Null, "PlaythroughReset should exist.");
            MethodInfo clear = reset.GetMethod("ClearSavedPlaythrough", BindingFlags.Public | BindingFlags.Static);
            Assert.That(clear, Is.Not.Null);
            clear.Invoke(null, null);

            foreach (string key in ProgressKeys)
                Assert.That(PlayerPrefs.HasKey(key), Is.False, key + " should be cleared on reload.");
            Assert.That(PlayerPrefs.GetFloat("cv_master", -1f), Is.EqualTo(0.4f).Within(0.001f),
                "Settings should survive a reload.");
            Assert.That(PlayerPrefs.GetInt("cv_scenario_roster_index", -1), Is.EqualTo(2),
                "The roster index should survive so each fresh run rotates the culprit.");
        }

        [Test]
        public void ClearSavedPlaythrough_RunsOnEveryLaunch()
        {
            Type reset = Type.GetType("Cyverse.Core.PlaythroughReset, Assembly-CSharp");
            MethodInfo launch = reset.GetMethod("ClearOnLaunch", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(launch, Is.Not.Null);
            var attr = launch.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.That(attr, Is.Not.Null, "The reset must run automatically at startup.");
            Assert.That(attr.loadType, Is.EqualTo(RuntimeInitializeLoadType.SubsystemRegistration),
                "It must run before any scene, ahead of the roster's startup pick.");
        }
    }
}
