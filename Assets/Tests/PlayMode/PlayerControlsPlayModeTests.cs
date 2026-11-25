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
    /// walking speed in every level.
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
