using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Cyverse.Tests
{
    public sealed class ArchitectureSeamsPlayModeTests
    {
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
