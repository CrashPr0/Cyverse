using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Cyverse.Tests
{
    /// <summary>The Hub's Hard wing and the Level 1 Hard copy.</summary>
    public sealed class HardModePlayModeTests
    {
        private readonly string[] progressKeys = { "cv_done_1", "cv_hard_done_1", "cv_hard_best_1" };
        private readonly bool[] hadKeys = new bool[3];
        private readonly int[] savedValues = new int[3];

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

        private static void SetHard(bool on)
        {
            Type hard = FindType("Cyverse.Core.HardMode");
            if (on) hard.GetMethod("Begin").Invoke(null, new object[] { 1 });
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
