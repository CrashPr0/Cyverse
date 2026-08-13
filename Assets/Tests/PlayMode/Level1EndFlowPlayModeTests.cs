using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Cyverse.Tests
{
    public class Level1EndFlowPlayModeTests
    {
        [UnityTest]
        [Timeout(30000)]
        public IEnumerator VisualPass_CompletesEndFlowAndPersistsProgress()
        {
            SceneManager.LoadScene("Level1_IAM_VisualPass", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Type driverType = FindType("Cyverse.Testing.Level1AutomatedPlaythrough");
            Assert.That(driverType, Is.Not.Null, "Automation driver was not compiled.");
            var go = new GameObject("Level1 End Flow Test Driver");
            Component driver = go.AddComponent(driverType);
            PropertyInfo finished = driverType.GetProperty("Finished");
            PropertyInfo passed = driverType.GetProperty("Passed");
            PropertyInfo failure = driverType.GetProperty("Failure");

            float deadline = Time.realtimeSinceStartup + 25f;
            while (!(bool)finished.GetValue(driver) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That((bool)finished.GetValue(driver), Is.True, "Playthrough timed out.");
            Assert.That((bool)passed.GetValue(driver), Is.True, (string)failure.GetValue(driver));
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
