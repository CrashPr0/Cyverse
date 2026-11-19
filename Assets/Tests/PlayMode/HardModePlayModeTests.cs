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
    /// <summary>The Hub's Hard wing and the Level 1 and Level 2 Hard copies.</summary>
    public sealed class HardModePlayModeTests
    {
        private readonly string[] progressKeys =
        {
            "cv_done_1", "cv_hard_done_1", "cv_hard_best_1",
            "cv_done_2", "cv_hard_done_2", "cv_hard_best_2",
        };
        private readonly bool[] hadKeys = new bool[6];
        private readonly int[] savedValues = new int[6];

        [SetUp]
        public void PreserveProgress()
        {
            for (int i = 0; i < progressKeys.Length; i++)
            {
                hadKeys[i] = PlayerPrefs.HasKey(progressKeys[i]);
                savedValues[i] = PlayerPrefs.GetInt(progressKeys[i], 0);
                PlayerPrefs.DeleteKey(progressKeys[i]);
            }
            PlayerPrefs.Save();
        }

        [TearDown]
        public void RestoreProgress()
        {
            SetHard(false);
            for (int i = 0; i < progressKeys.Length; i++)
            {
                if (hadKeys[i]) PlayerPrefs.SetInt(progressKeys[i], savedValues[i]);
                else PlayerPrefs.DeleteKey(progressKeys[i]);
            }
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator Hub_HardWingIsReachableAndLevel1HardUnlocksAfterLevel1()
        {
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Assert.That(GameObject.Find("HardWing"), Is.Not.Null, "The Hub should build its Hard wing.");
            // Nothing solid between the Hub floor and the wing's far wall at walking height.
            Assert.That(Physics.Raycast(new Vector3(-15f, 1.5f, -4f), Vector3.left, out RaycastHit hit, 30f), Is.True);
            Assert.That(hit.point.x, Is.LessThan(-28.5f),
                "The west wall should be open into the Hard wing, but the walk is blocked by " + hit.collider.name);

            object hardDoor = FindHardDoor(1);
            Assert.That(hardDoor, Is.Not.Null, "The Hard wing needs a Level 1 Hard door.");
            Type doorType = hardDoor.GetType();
            Assert.That((string)doorType.GetField("sceneName").GetValue(hardDoor), Is.EqualTo("Level1_IAM"));
            Assert.That((int)doorType.GetField("levelIndex").GetValue(hardDoor), Is.EqualTo(-1),
                "Hard doors keep levelIndex -1 so lookups by level find the Normal door.");
            Assert.That((bool)doorType.GetProperty("IsOpen").GetValue(hardDoor), Is.False,
                "Level 1 Hard stays locked until Level 1 is complete.");

            PlayerPrefs.SetInt("cv_done_1", 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            hardDoor = FindHardDoor(1);
            Assert.That((bool)doorType.GetProperty("IsOpen").GetValue(hardDoor), Is.True,
                "Completing Level 1 opens its Hard copy.");
        }

        [UnityTest]
        public IEnumerator Level1Hard_HidesGuidanceAndMovesToken()
        {
            SetHard(true);
            SceneManager.LoadScene("Level1_IAM", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Type beaconType = FindType("Cyverse.Level.ObjectiveBeacon");
            object beacon = beaconType.GetProperty("Instance").GetValue(null);
            if (beacon != null)
                Assert.That((bool)beaconType.GetProperty("MarkerShown").GetValue(beacon), Is.False,
                    "Hard runs have no objective marker.");

            GameObject rack = GameObject.Find("TokenRack");
            Assert.That(rack, Is.Not.Null);
            Assert.That(rack.transform.position.x, Is.GreaterThan(0f),
                "On Hard the token charges across the room from the vault.");

        }

        [UnityTest]
        public IEnumerator Hub_Level2HardDoorOpensAfterLevel2()
        {
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            object hardDoor = FindHardDoor(2);
            Assert.That(hardDoor, Is.Not.Null, "The Hard wing needs a Level 2 Hard door.");
            Type doorType = hardDoor.GetType();
            Assert.That((string)doorType.GetField("sceneName").GetValue(hardDoor), Is.EqualTo("Level2_CyberDefense"));
            Assert.That((int)doorType.GetField("levelIndex").GetValue(hardDoor), Is.EqualTo(-1));
            Assert.That((bool)doorType.GetProperty("IsOpen").GetValue(hardDoor), Is.False,
                "Level 2 Hard stays locked until Level 2 is complete.");

            PlayerPrefs.SetInt("cv_done_2", 1);
            PlayerPrefs.Save();
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
            yield return null;
            yield return null;

            hardDoor = FindHardDoor(2);
            Assert.That((bool)doorType.GetProperty("IsOpen").GetValue(hardDoor), Is.True,
                "Completing Level 2 opens its Hard copy.");
        }

        [UnityTest]
        public IEnumerator Level2Hard_AlertClockAndPlaybookPenalty()
        {
            SetHard(true, 2);
            SceneManager.LoadScene("Level2_CyberDefense", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Type managerType = FindType("Cyverse.Level.Level2Manager");
            object manager = managerType.GetProperty("Instance").GetValue(null);
            Assert.That(manager, Is.Not.Null);
            Assert.That((bool)managerType.GetProperty("IsHard").GetValue(manager), Is.True);

            Type beaconType = FindType("Cyverse.Level.ObjectiveBeacon");
            object beacon = beaconType.GetProperty("Instance").GetValue(null);
            if (beacon != null)
                Assert.That((bool)beaconType.GetProperty("MarkerShown").GetValue(beacon), Is.False,
                    "Hard runs have no objective marker.");

            // The alert clock starts when the briefing ends.
            Type siemType = FindType("Cyverse.Interaction.SiemConsole");
            Object siem = Object.FindObjectOfType(siemType);
            Assert.That(siem, Is.Not.Null);
            Assert.That((bool)siemType.GetProperty("AlertClockRunning").GetValue(siem), Is.False);
            managerType.GetMethod("OnBriefingCompleted", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, null);
            yield return null;
            Assert.That((bool)siemType.GetProperty("AlertClockRunning").GetValue(siem), Is.True,
                "On Hard the SIEM alerts run on a response clock.");
            float left = (float)siemType.GetProperty("AlertSecondsLeft").GetValue(siem);
            Assert.That(left, Is.GreaterThan(50f).And.LessThanOrEqualTo(60f));

            // A wrong playbook card costs points and goes back to the rack.
            Type cardType = FindType("Cyverse.Interaction.Carryable");
            Type playbookType = FindType("Cyverse.Interaction.PlaybookStation");
            Object playbook = Object.FindObjectOfType(playbookType);
            Assert.That(playbook, Is.Not.Null);
            MethodInfo isExpected = playbookType.GetMethod("IsExpectedCard");
            Component wrong = null;
            foreach (Object c in Object.FindObjectsOfType(cardType))
            {
                if (!((string)cardType.GetField("id").GetValue(c)).StartsWith("ir_")) continue;
                if ((bool)isExpected.Invoke(playbook, new object[] { c })) continue;
                wrong = (Component)c;
                break;
            }
            Assert.That(wrong, Is.Not.Null, "Expected a playbook card that isn't step 1.");
            Vector3 home = wrong.transform.position;
            cardType.GetField("gate").SetValue(wrong, null);
            cardType.GetMethod("Interact").Invoke(wrong, new object[] { null });
            Assert.That(cardType.GetProperty("Carried").GetValue(null), Is.SameAs(wrong),
                "The test needs to be carrying the wrong card.");

            Type zoneType = FindType("Cyverse.Interaction.DropZone");
            Object step1 = null;
            foreach (Object z in Object.FindObjectsOfType(zoneType))
                if ((string)zoneType.GetField("zoneName").GetValue(z) == "STEP 1") step1 = z;
            Assert.That(step1, Is.Not.Null);

            Type scoreType = FindType("Cyverse.Core.ScoreSystem");
            int before = (int)scoreType.GetProperty("Score").GetValue(null);
            zoneType.GetMethod("Interact").Invoke(step1, new object[] { null });

            Assert.That((int)scoreType.GetProperty("Score").GetValue(null), Is.EqualTo(before - 30),
                "A wrong step costs 30 on Hard.");
            Assert.That(cardType.GetProperty("Carried").GetValue(null), Is.Null);
            Assert.That(Vector3.Distance(wrong.transform.position, home), Is.LessThan(0.01f),
                "The wrong card goes back to its spot on the rack.");
        }

        [UnityTest]
        public IEnumerator ContainSpread_IsolatingEveryInfectionWins()
        {
            Type gameType = FindType("Cyverse.UI.ContainSpread");
            bool won = false, quit = false;
            object game = gameType.GetMethod("Play").Invoke(null, new object[]
            {
                (Action)(() => won = true), (Action)(() => quit = true),
            });
            yield return null;

            var startInfected = (int[])gameType.GetField("StartInfected").GetValue(null);
            int fileServer = (int)gameType.GetField("FileServer").GetValue(null);
            MethodInfo isInfected = gameType.GetMethod("IsInfected");
            MethodInfo isolate = gameType.GetMethod("Isolate");
            foreach (int i in startInfected)
                Assert.That((bool)isInfected.Invoke(game, new object[] { i }), Is.True);
            Assert.That((bool)isInfected.Invoke(game, new object[] { fileServer }), Is.False);

            // The file server can't be taken offline: it has to be defended.
            isolate.Invoke(game, new object[] { fileServer });
            Assert.That((bool)gameType.GetMethod("IsIsolated").Invoke(game, new object[] { fileServer }), Is.False);

            // A hop only reaches a neighbour of an infected machine.
            int reached = (int)gameType.GetMethod("Hop").Invoke(game, null);
            Assert.That(reached, Is.GreaterThanOrEqualTo(0));
            bool nextToInfection = false;
            foreach (int i in startInfected)
                if ((bool)gameType.GetMethod("AreNeighbours").Invoke(null, new object[] { i, reached }))
                    nextToInfection = true;
            Assert.That(nextToInfection, Is.True, "Malware spreads along network links only.");

            for (int i = 0; i < 9; i++)
                if ((bool)isInfected.Invoke(game, new object[] { i }))
                    isolate.Invoke(game, new object[] { i });

            Assert.That((bool)gameType.GetProperty("Won").GetValue(game), Is.True);
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(won, Is.True, "Containing it hands control back as a win.");
            Assert.That(quit, Is.False);
        }

        private static void SetHard(bool on, int level = 1)
        {
            Type hard = FindType("Cyverse.Core.HardMode");
            if (on) hard.GetMethod("Begin").Invoke(null, new object[] { level });
            else hard.GetMethod("End").Invoke(null, null);
        }

        private static object FindHardDoor(int level)
        {
            Type doorType = FindType("Cyverse.Interaction.HubDoor");
            foreach (Object candidate in Object.FindObjectsOfType(doorType))
                if ((int)doorType.GetField("hardLevel").GetValue(candidate) == level)
                    return candidate;
            return null;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            throw new InvalidOperationException("Type not found: " + fullName);
        }
    }
}
