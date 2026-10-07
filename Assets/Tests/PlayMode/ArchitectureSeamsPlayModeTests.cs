using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Cyverse.Tests
{
    public sealed class ArchitectureSeamsPlayModeTests
    {
        [Test]
        public void RichInteractions_ShareGameplayActionsWithoutAutomationHooks()
        {
            Type targetInterface = FindType("Cyverse.Interaction.IGameplayActionTarget");
            string[] actionTargets =
            {
                "Cyverse.Interaction.VideoStation",
                "Cyverse.Interaction.AuditStation",
                "Cyverse.Interaction.SiemConsole",
                "Cyverse.Interaction.CyberAttackStation",
                "Cyverse.Player.FirstPersonController",
                "Cyverse.Quiz.QuizSystem",
                "Cyverse.Forensics.ChainOfCustodyForm",
                "Cyverse.Forensics.QueryTerminal",
                "Cyverse.UI.PasswordLockController",
                "Cyverse.UI.TypingChallenge",
            };
            foreach (string name in actionTargets)
                Assert.That(targetInterface.IsAssignableFrom(FindType(name)), Is.True,
                    name + " must accept the same action vocabulary as live input and TAS playback.");

            string[] gameplayTypes =
            {
                "Cyverse.Interaction.VideoStation",
                "Cyverse.Interaction.AuditStation",
                "Cyverse.Interaction.CertExamStation",
                "Cyverse.Interaction.SiemConsole",
                "Cyverse.Interaction.ForensicsConsole",
                "Cyverse.Forensics.ChainOfCustodyForm",
                "Cyverse.Level.Level4CyberAttackManager",
                "Cyverse.Level.SocProgress",
                "Cyverse.Level.ScenarioRoster",
                "Cyverse.UI.PasswordLockController",
                "Cyverse.UI.TypingChallenge",
            };
            foreach (string name in gameplayTypes)
            foreach (MethodInfo method in FindType(name).GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                Assert.That(method.Name, Does.Not.Contain("ForAutomation"),
                    name + " leaked an automation-only state transition into gameplay code.");
        }

        [UnityTest]
        public IEnumerator PlayerMotor_AcceptsTheSameMovementActionAsTasPlayback()
        {
            Type motorType = FindType("Cyverse.Player.FirstPersonController");
            var actor = new GameObject("GameplayActionMotorTest");
            // This test may follow a scene-loading acceptance test. Keep its
            // synthetic motor clear of whatever floor or props that scene owns.
            actor.transform.position = new Vector3(0f, 20f, 0f);
            Component motor = actor.AddComponent(motorType);
            motor.GetType().GetProperty("enabled").SetValue(motor, false);
            Vector3 before = actor.transform.position;

            Assert.That(GameplayActionTestDriver.Move(motor, Vector3.forward * 4f, 0.25f), Is.True);
            Assert.That(actor.transform.position.z, Is.GreaterThan(before.z + 0.9f),
                "The shared motor action must move through CharacterController rather than a TAS teleport.");

            UnityEngine.Object.Destroy(actor);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ModalSession_EnforcesExclusiveOwnershipAndRestoresResources()
        {
            Type gameStateType = FindType("Cyverse.Core.GameState");
            Type modalType = FindType("Cyverse.Core.ModalSession");
            Type channelType = modalType.GetNestedType("Channel", BindingFlags.Public);
            MethodInfo tryOpen = modalType.GetMethod("TryOpen", BindingFlags.Public | BindingFlags.Static);
            MethodInfo reset = gameStateType.GetMethod("Reset", BindingFlags.Public | BindingFlags.Static);

            reset.Invoke(null, null);
            Time.timeScale = 1f;

            var firstOwner = new GameObject("FirstModalOwner");
            var secondOwner = new GameObject("SecondModalOwner");
            object quiz = Enum.Parse(channelType, "Quiz");
            object settings = Enum.Parse(channelType, "Settings");

            object[] firstArgs = { firstOwner, quiz, null, true, true };
            Assert.That((bool)tryOpen.Invoke(null, firstArgs), Is.True);
            object firstLease = firstArgs[2];
            Assert.That(firstLease, Is.Not.Null);
            Assert.That((bool)gameStateType.GetField("QuizActive").GetValue(null), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            object[] competingArgs = { secondOwner, settings, null, true, true };
            Assert.That((bool)tryOpen.Invoke(null, competingArgs), Is.False,
                "A second screen must not stack over the current modal owner.");

            object[] wrongChannelArgs = { firstOwner, settings, null, true, true };
            Assert.That((bool)tryOpen.Invoke(null, wrongChannelArgs), Is.False,
                "An owner cannot silently reinterpret its active lease as another channel.");

            firstLease.GetType().GetMethod("Close").Invoke(firstLease, null);
            Assert.That((bool)gameStateType.GetField("QuizActive").GetValue(null), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            Time.timeScale = 0.65f;
            object[] resetArgs = { secondOwner, settings, null, true, true };
            Assert.That((bool)tryOpen.Invoke(null, resetArgs), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            reset.Invoke(null, null);
            Assert.That((bool)gameStateType.GetField("MenuOpen").GetValue(null), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(0.65f),
                "Global reset must restore the clock value held before the modal opened.");
            Time.timeScale = 1f;

            UnityEngine.Object.Destroy(firstOwner);
            UnityEngine.Object.Destroy(secondOwner);
            yield return null;
        }

        [Test]
        public void ScenarioRoster_RotatesOneIdentityConsistentlyAcrossIamAndSoc()
        {
            Type rosterType = FindType("Cyverse.Level.ScenarioRoster");
            FieldInfo initialized = rosterType.GetField("sessionInitialized",
                BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo index = rosterType.GetField("sessionIndex",
                BindingFlags.NonPublic | BindingFlags.Static);
            MethodInfo begin = rosterType.GetMethod("BeginStartupSession");
            PropertyInfo current = rosterType.GetProperty("Current");
            string rotationKey = (string)rosterType.GetField("RotationKey").GetValue(null);
            bool hadRotation = PlayerPrefs.HasKey(rotationKey);
            int savedRotation = PlayerPrefs.GetInt(rotationKey, 0);
            bool hadCompletion = PlayerPrefs.HasKey("cv_done_3");
            int savedCompletion = PlayerPrefs.GetInt("cv_done_3", 0);
            string finishedKey = (string)rosterType.GetField("FinishedKey").GetValue(null);
            bool hadFinished = PlayerPrefs.HasKey(finishedKey);
            int savedFinished = PlayerPrefs.GetInt(finishedKey, 0);
            const string evidenceKey = "cv_soc_evidence_json";
            bool hadEvidence = PlayerPrefs.HasKey(evidenceKey);
            string savedEvidence = PlayerPrefs.GetString(evidenceKey, "");
            bool savedInitialized = (bool)initialized.GetValue(null);
            int savedIndex = (int)index.GetValue(null);

            string[] expectedUsers = { "d.chen", "k.ramos", "j.okafor", "n.singh" };
            try
            {
                // Completing the campaign makes each startup advance exactly
                // once instead of preserving an in-progress investigation.
                PlayerPrefs.SetInt("cv_done_3", 1);
                for (int i = 0; i < expectedUsers.Length; i++)
                {
                    // cv_done_3 is permanent; the roster's own finished flag
                    // is what marks "the previous playthrough just ended".
                    PlayerPrefs.SetInt(finishedKey, 1);
                    PlayerPrefs.SetInt(rotationKey, (i + expectedUsers.Length - 1) % expectedUsers.Length);
                    initialized.SetValue(null, false);
                    begin.Invoke(null, null);

                    object profile = current.GetValue(null);
                    string user = (string)profile.GetType().GetField("socUser").GetValue(profile);
                    Assert.That(user, Is.EqualTo(expectedUsers[i]));

                    Array rounds = (Array)FindType("Cyverse.Level.Level1IamContent")
                        .GetMethod("AuditRounds").Invoke(null, null);
                    object impossibleTravel = rounds.GetValue(1);
                    string[] auditLines = (string[])impossibleTravel.GetType()
                        .GetField("lines").GetValue(impossibleTravel);
                    int matchingAuditLines = 0;
                    foreach (string line in auditLines)
                        if (line.Contains(user)) matchingAuditLines++;
                    Assert.That(matchingAuditLines, Is.EqualTo(3),
                        "Level 1 should use the active roster identity for the complete impossible-travel trail.");

                    Array scenarios = (Array)FindType("Cyverse.Level.Level2Content")
                        .GetMethod("SocScenarios").Invoke(null, null);
                    object incident = scenarios.GetValue(2);
                    int trigger = (int)incident.GetType().GetField("triggerRowIndex").GetValue(incident);
                    Array rows = (Array)incident.GetType().GetField("rows").GetValue(incident);
                    string socUser = (string)rows.GetValue(trigger).GetType()
                        .GetField("user").GetValue(rows.GetValue(trigger));
                    Assert.That(socUser, Is.EqualTo(user),
                        "Level 2 must continue the identity selected in Level 1.");
                }
            }
            finally
            {
                if (hadRotation) PlayerPrefs.SetInt(rotationKey, savedRotation);
                else PlayerPrefs.DeleteKey(rotationKey);
                if (hadCompletion) PlayerPrefs.SetInt("cv_done_3", savedCompletion);
                else PlayerPrefs.DeleteKey("cv_done_3");
                if (hadFinished) PlayerPrefs.SetInt(finishedKey, savedFinished);
                else PlayerPrefs.DeleteKey(finishedKey);
                if (hadEvidence) PlayerPrefs.SetString(evidenceKey, savedEvidence);
                else PlayerPrefs.DeleteKey(evidenceKey);
                PlayerPrefs.Save();
                index.SetValue(null, savedIndex);
                initialized.SetValue(null, savedInitialized);
            }
        }

        [UnityTest]
        public IEnumerator HubDoors_UseCanonicalScenesInsteadOfLegacyVisualPasses()
        {
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            string[] expectedScenes =
            {
                "Level0",
                "Level1_IAM",
                "Level2_CyberDefense",
                "Level3_Forensics",
                "Level4_CyberAttack",
            };
            Type doorType = FindType("Cyverse.Interaction.HubDoor");
            int checkedDoors = 0;
            foreach (UnityEngine.Object candidate in UnityEngine.Object.FindObjectsOfType(doorType))
            {
                int level = (int)doorType.GetField("levelIndex").GetValue(candidate);
                if (level < 0 || level >= expectedScenes.Length) continue;
                string scene = (string)doorType.GetField("sceneName").GetValue(candidate);
                Assert.That(scene, Is.EqualTo(expectedScenes[level]),
                    $"Hub level {level} must route to the current canonical scene.");
                StringAssert.DoesNotContain("VisualPass", scene);
                StringAssert.DoesNotContain("Visual Pass", scene);
                checkedDoors++;
            }
            Assert.That(checkedDoors, Is.GreaterThanOrEqualTo(5),
                "The Hub should expose every canonical level destination.");
        }

        [UnityTest]
        public IEnumerator HudToasts_UseDedicatedRowsBelowObjectiveBanner()
        {
            SceneManager.LoadScene("Level1_IAM", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Type hudType = FindType("Cyverse.UI.HudUI");
            Component hud = hudType.GetProperty("Instance").GetValue(null) as Component;
            Assert.That(hud, Is.Not.Null);

            hudType.GetMethod("ShowObjective").Invoke(hud,
                new object[] { "Objective: Complete the current task" });
            MethodInfo showToast = hudType.GetMethod("ShowToast");
            showToast.Invoke(hud, new object[] { "TASK COMPLETE", Color.green });
            showToast.Invoke(hud, new object[] {
                "GLOSSARY UPDATED — additional task-completion context remains readable without covering another notification",
                Color.cyan
            });
            Canvas.ForceUpdateCanvases();

            GameObject objective = hudType.GetField("objectivePanel",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud) as GameObject;
            Assert.That(objective, Is.Not.Null);
            Rect objectiveRect = WorldRect(objective.GetComponent<RectTransform>());

            var toastRects = new List<Rect>();
            Canvas hudCanvas = hudType.GetProperty("Canvas").GetValue(hud) as Canvas;
            Assert.That(hudCanvas, Is.Not.Null);
            foreach (Text text in hudCanvas.GetComponentsInChildren<Text>(true))
                if (text.name == "Toast") toastRects.Add(WorldRect(text.rectTransform));

            Assert.That(toastRects, Has.Count.EqualTo(2));
            foreach (Rect toastRect in toastRects)
                Assert.That(toastRect.Overlaps(objectiveRect), Is.False,
                    "Task notifications must not cover the persistent objective banner.");
            Assert.That(toastRects[0].Overlaps(toastRects[1]), Is.False,
                "Back-to-back task notifications need separate rows.");
        }

        private static Rect WorldRect(RectTransform transform)
        {
            var corners = new Vector3[4];
            transform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y,
                corners[2].x, corners[2].y);
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
