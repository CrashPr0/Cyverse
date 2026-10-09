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
    /// The MFA briefing comes from an NPC the player talks to: the specialist
    /// stands by the vault, the vault's factors stay locked until she has been
    /// heard, and talking to her starts her recorded lines.
    /// </summary>
    public class MfaSpecialistPlayModeTests
    {
        [UnityTest]
        [Timeout(20000)]
        public IEnumerator Specialist_GatesTheVaultUntilThePlayerTalksToHer()
        {
            SceneManager.LoadScene("Level1_IAM", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Type specialistType = FindType("Cyverse.Interaction.MfaSpecialist");
            Type gauntletType = FindType("Cyverse.Interaction.MfaGauntlet");
            Type badgeType = FindType("Cyverse.Interaction.BadgeStation");
            Type dialogueType = FindType("Cyverse.Dialogue.DialogueManager");
            var specialist = (Component)UnityEngine.Object.FindObjectOfType(specialistType);
            var gauntlet = (Component)UnityEngine.Object.FindObjectOfType(gauntletType);
            var badge = (Component)UnityEngine.Object.FindObjectOfType(badgeType);
            Assert.That(specialist, Is.Not.Null, "Level 1 should have an MFA Specialist.");
            Assert.That(gauntlet, Is.Not.Null);
            Assert.That(badge, Is.Not.Null);

            // A person, standing on the way to the vault.
            Vector3 toVault = gauntlet.transform.position - specialist.transform.position;
            toVault.y = 0f;
            Assert.That(toVault.magnitude, Is.LessThan(10f), "She should stand in the MFA area, near the vault she explains.");
            Renderer[] parts = specialist.GetComponentsInChildren<Renderer>();
            Assert.That(parts.Length, Is.GreaterThan(40), "The specialist should be a modelled robot, not a few boxes.");
            float feet = float.MaxValue, top = float.MinValue;
            foreach (Renderer part in parts)
            {
                if (part.name.StartsWith("Sole")) feet = Mathf.Min(feet, part.bounds.min.y);
                if (part.name == "HeadShell") top = Mathf.Max(top, part.bounds.max.y);
            }
            float floor = specialist.transform.position.y;
            Assert.That(feet - floor, Is.EqualTo(0f).Within(0.02f), "Her feet should rest on the floor.");
            Assert.That(top - floor, Is.InRange(1.6f, 1.85f), "She should stand at an adult's height.");

            PropertyInfo briefedInScene = specialistType.GetProperty("BriefedInScene", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo briefed = specialistType.GetProperty("Briefed");
            Assert.That((bool)briefedInScene.GetValue(null), Is.False, "The vault must wait for the briefing.");
            Assert.That((string)specialistType.GetProperty("Prompt").GetValue(specialist),
                Does.Contain("MFA Specialist"));

            Assert.That(GameplayActionTestDriver.Interact(specialist), Is.True, "Talking to her is a normal interaction.");
            yield return null;
            Assert.That((bool)briefed.GetValue(specialist), Is.True);
            Assert.That((bool)briefedInScene.GetValue(null), Is.True);
            object dialogue = dialogueType.GetProperty("Instance").GetValue(null);
            Assert.That((bool)dialogueType.GetProperty("IsPlaying").GetValue(dialogue), Is.True,
                "Talking to her should start the recorded briefing.");
            Assert.That((bool)specialistType.GetProperty("Talking").GetValue(specialist), Is.True);

            dialogueType.GetMethod("Stop").Invoke(dialogue, null);
            yield return null;
            Assert.That((bool)specialistType.GetProperty("Talking").GetValue(specialist), Is.False);
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
