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
