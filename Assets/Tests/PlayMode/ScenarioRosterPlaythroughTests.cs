using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Cyverse.Tests
{
    /// <summary>
    /// The bad actor must differ between playthroughs, stay put within one,
    /// and agree with the saved SOC handoff.  Drives ScenarioRoster through
    /// reflection (the test assembly cannot reference game code) against the
    /// real PlayerPrefs keys, restoring everything afterwards.
    /// </summary>
    public sealed class ScenarioRosterPlaythroughTests
    {
        private static readonly string[] Users = { "d.chen", "k.ramos", "j.okafor", "n.singh" };

        private static readonly string[] IntKeys =
        {
            "cv_scenario_roster_index", "cv_scenario_roster_finished",
            "cv_done_0", "cv_done_1", "cv_done_2", "cv_done_3", "cv_done_4",
            "cv_soc_compromised_computer", "cv_soc_chain_of_custody", "cv_soc_playbook_solved",
            "cv_soc_evidence_locked",
        };
        private const string EvidenceKey = "cv_soc_evidence_json";

        private Type rosterType;
        private FieldInfo initialized, sessionIndex, randomSource;
        private bool savedInitialized;
        private int savedIndex;
        private object savedSource;
        private bool[] hadInt;
        private int[] savedInt;
        private bool hadEvidence;
        private string savedEvidence;

        [SetUp]
        public void Snapshot()
        {
            rosterType = FindType("Cyverse.Level.ScenarioRoster");
            initialized = rosterType.GetField("sessionInitialized", BindingFlags.NonPublic | BindingFlags.Static);
            sessionIndex = rosterType.GetField("sessionIndex", BindingFlags.NonPublic | BindingFlags.Static);
            randomSource = rosterType.GetField("RandomIndexSource", BindingFlags.Public | BindingFlags.Static);
            savedInitialized = (bool)initialized.GetValue(null);
            savedIndex = (int)sessionIndex.GetValue(null);
            savedSource = randomSource.GetValue(null);

            hadInt = new bool[IntKeys.Length];
            savedInt = new int[IntKeys.Length];
            for (int i = 0; i < IntKeys.Length; i++)
            {
                hadInt[i] = PlayerPrefs.HasKey(IntKeys[i]);
                savedInt[i] = PlayerPrefs.GetInt(IntKeys[i], 0);
            }
            hadEvidence = PlayerPrefs.HasKey(EvidenceKey);
            savedEvidence = PlayerPrefs.GetString(EvidenceKey, "");
            ClearSave();
        }

        [TearDown]
        public void Restore()
        {
            for (int i = 0; i < IntKeys.Length; i++)
            {
                if (hadInt[i]) PlayerPrefs.SetInt(IntKeys[i], savedInt[i]);
                else PlayerPrefs.DeleteKey(IntKeys[i]);
            }
            if (hadEvidence) PlayerPrefs.SetString(EvidenceKey, savedEvidence);
            else PlayerPrefs.DeleteKey(EvidenceKey);
            PlayerPrefs.Save();
            randomSource.SetValue(null, savedSource);
            sessionIndex.SetValue(null, savedIndex);
            initialized.SetValue(null, savedInitialized);
        }

        [Test]
        public void EmptyStorage_PicksFromTheRandomSourceNotAlwaysTheFirstProfile()
        {
            for (int pick = 0; pick < Users.Length; pick++)
            {
                ClearSave();
                int chosen = pick;
                randomSource.SetValue(null, (Func<int, int>)(count => chosen));
                Assert.That(StartSession(), Is.EqualTo(Users[pick]),
                    "A first-ever visit (or private window) must follow the random pick.");
            }

            // And with the real RNG, a fresh start must be able to land
            // somewhere other than profile 0 (never-0 was the original bug).
            randomSource.SetValue(null, null);
            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 80 && seen.Count < 2; i++)
            {
                ClearSave();
                seen.Add(StartSession());
            }
            Assert.That(seen.Count, Is.GreaterThan(1), "Fresh starts always produced the same bad actor.");
        }

        [Test]
        public void MidPlaythroughReload_KeepsTheSameBadActor()
        {
            for (int stored = 0; stored < Users.Length; stored++)
            {
                ClearSave();
                PlayerPrefs.SetInt("cv_scenario_roster_index", stored);
                PlayerPrefs.SetInt("cv_scenario_roster_finished", 0);
                PlayerPrefs.SetInt("cv_done_1", 1);
                Assert.That(StartSession(), Is.EqualTo(Users[stored]));
                Assert.That(StartSession(), Is.EqualTo(Users[stored]), "A second startup (reload) must not rotate.");
            }
        }

        [Test]
        public void FinishedPlaythrough_AdvancesAndNeverRepeatsThePreviousName()
        {
            for (int stored = 0; stored < Users.Length; stored++)
            {
                ClearSave();
                PlayerPrefs.SetInt("cv_scenario_roster_index", stored);
                PlayerPrefs.SetInt("cv_done_1", 1);
                PlayerPrefs.SetInt("cv_done_2", 1);
                PlayerPrefs.SetInt("cv_done_3", 1);
                // Completing the last roster level is what ends a playthrough.
                FindType("Cyverse.Core.LevelProgress").GetMethod("MarkCompleted").Invoke(null, new object[] { 3 });
                string next = StartSession();
                Assert.That(next, Is.EqualTo(Users[(stored + 1) % Users.Length]));
                Assert.That(next, Is.Not.EqualTo(Users[stored]));

                // The permanent cv_done_* flags are still set; the new
                // playthrough must nevertheless hold its identity on reload.
                Assert.That(StartSession(), Is.EqualTo(next), "Replay must not rotate until it finishes.");
            }
        }

        [Test]
        public void CompletingEarlierLevels_DoesNotEndThePlaythrough()
        {
            PlayerPrefs.SetInt("cv_scenario_roster_finished", 0);
            Type progress = FindType("Cyverse.Core.LevelProgress");
            foreach (int level in new[] { 0, 1, 2 })
                progress.GetMethod("MarkCompleted").Invoke(null, new object[] { level });
            Assert.That(PlayerPrefs.GetInt("cv_scenario_roster_finished", -1), Is.EqualTo(0));
            progress.GetMethod("MarkCompleted").Invoke(null, new object[] { 3 });
            Assert.That(PlayerPrefs.GetInt("cv_scenario_roster_finished", -1), Is.EqualTo(1));
        }

        [Test]
        public void LegacySaveWithoutRosterKey_ContinuesAsTheOriginalCulprit()
        {
            PlayerPrefs.SetInt("cv_done_1", 1);
            Assert.That(StartSession(), Is.EqualTo("d.chen"),
                "Saves made before the roster existed were played as d.chen.");
        }

        [Test]
        public void NewPlaythrough_MovesTheSavedSocEvidenceToTheNewBadActor()
        {
            StoreEvidence("d.chen");
            PlayerPrefs.SetInt("cv_scenario_roster_index", 0);
            PlayerPrefs.SetInt("cv_scenario_roster_finished", 1);
            string user = StartSession();
            Assert.That(user, Is.EqualTo("k.ramos"));
            Assert.That(EvidenceUser(), Is.EqualTo(user),
                "EvidenceManifest and the rest of Level 3's logs must name the same person.");
        }

        [Test]
        public void InProgressSave_FollowsTheSocEvidenceItWasShown()
        {
            StoreEvidence("j.okafor");
            PlayerPrefs.SetInt("cv_scenario_roster_index", 0); // diverged/lost key
            PlayerPrefs.SetInt("cv_scenario_roster_finished", 0);
            Assert.That(StartSession(), Is.EqualTo("j.okafor"));
        }

        [Test]
        public void Level1AuditTrail_KeepsBystandersOutOfTheRosterPool()
        {
            Type content = FindType("Cyverse.Level.Level1IamContent");
            for (int pick = 0; pick < Users.Length; pick++)
            {
                ClearSave();
                int chosen = pick;
                randomSource.SetValue(null, (Func<int, int>)(count => chosen));
                string user = StartSession();
                Array rounds = (Array)content.GetMethod("AuditRounds").Invoke(null, null);
                foreach (object round in rounds)
                {
                    string[] lines = (string[])round.GetType().GetField("lines").GetValue(round);
                    foreach (string line in lines)
                        foreach (string other in Users)
                            if (other != user)
                                StringAssert.DoesNotContain(other, line,
                                    $"Round shows roster name {other} while the culprit is {user}.");
                }
            }
        }

        // ---- helpers ------------------------------------------------------

        [Test]
        public void CampaignReplay_RestoresRosterCompletionAndSavedEvidence()
        {
            PlayerPrefs.SetInt("cv_scenario_roster_index", 2);
            PlayerPrefs.SetInt("cv_scenario_roster_finished", 0);
            PlayerPrefs.SetInt("cv_done_1", 1);
            StoreEvidence("j.okafor");
            string before = PlayerPrefs.GetString(EvidenceKey);
            Type replayType = FindType("Cyverse.Testing.CampaignTasPlayback");
            GameObject host = new GameObject("RosterReplayPreservationTest");
            Component replay = host.AddComponent(replayType);
            Canvas overlay = null;
            try
            {
                // Advance only the normal replay setup, before its route begins.
                IEnumerator start = (IEnumerator)replayType.GetMethod("Start",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(replay, null);
                Assert.That(start.MoveNext(), Is.True);
                overlay = (Canvas)replayType.GetField("overlayCanvas",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(replay);

                FindType("Cyverse.Core.LevelProgress").GetMethod("MarkCompleted")
                    .Invoke(null, new object[] { 3 });
                PlayerPrefs.SetInt("cv_scenario_roster_index", 3);
                StoreEvidence("n.singh");
                replayType.GetMethod("RestoreProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(replay, null);

                Assert.That(PlayerPrefs.GetInt("cv_scenario_roster_index"), Is.EqualTo(2),
                    "A watchable replay must restore the player's selected culprit.");
                Assert.That(PlayerPrefs.GetInt("cv_scenario_roster_finished"), Is.Zero,
                    "Finishing the replay must not finish the player's real playthrough.");
                Assert.That(PlayerPrefs.GetString(EvidenceKey), Is.EqualTo(before));
            }
            finally
            {
                if (overlay != null) UnityEngine.Object.DestroyImmediate(overlay.gameObject);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ArchitectureRosterCheck_PreservesSavedEvidence()
        {
            StoreEvidence("d.chen");
            string before = PlayerPrefs.GetString(EvidenceKey);
            new ArchitectureSeamsPlayModeTests().ScenarioRoster_RotatesOneIdentityConsistentlyAcrossIamAndSoc();
            Assert.That(PlayerPrefs.GetString(EvidenceKey), Is.EqualTo(before),
                "Rotating test profiles must not retarget the player's saved SOC evidence.");
        }

        private string StartSession()
        {
            initialized.SetValue(null, false);
            rosterType.GetMethod("BeginStartupSession").Invoke(null, null);
            object profile = rosterType.GetProperty("Current").GetValue(null);
            return (string)profile.GetType().GetField("socUser").GetValue(profile);
        }

        private static void ClearSave()
        {
            foreach (string key in IntKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(EvidenceKey);
        }

        private static void StoreEvidence(string user)
        {
            Type evidenceType = FindType("Cyverse.Level.SocEvidenceRecord");
            object evidence = Activator.CreateInstance(evidenceType);
            evidenceType.GetField("computer").SetValue(evidence, "WS-03");
            evidenceType.GetField("user").SetValue(evidence, user);
            PlayerPrefs.SetString(EvidenceKey, JsonUtility.ToJson(evidence));
            PlayerPrefs.SetInt("cv_soc_chain_of_custody", 1);
        }

        private static string EvidenceUser()
        {
            Type progress = FindType("Cyverse.Level.SocProgress");
            object[] args = { null };
            Assert.That((bool)progress.GetMethod("TryGetEvidence").Invoke(null, args), Is.True);
            return (string)args[0].GetType().GetField("user").GetValue(args[0]);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            Assert.Fail("Type was not compiled: " + fullName);
            return null;
        }
    }
}
