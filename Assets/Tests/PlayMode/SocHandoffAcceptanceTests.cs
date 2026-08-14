using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Cyverse.Tests
{
    public class SocHandoffAcceptanceTests
    {
        private readonly string[] intKeys =
        {
            "cv_done_2",
            "cv_soc_compromised_computer",
            "cv_soc_chain_of_custody",
            "cv_soc_playbook_solved",
        };

        private bool[] hadInt;
        private int[] savedInt;
        private bool hadEvidence;
        private string savedEvidence;

        [SetUp]
        public void PreserveProgress()
        {
            hadInt = new bool[intKeys.Length];
            savedInt = new int[intKeys.Length];
            for (int i = 0; i < intKeys.Length; i++)
            {
                hadInt[i] = PlayerPrefs.HasKey(intKeys[i]);
                savedInt[i] = PlayerPrefs.GetInt(intKeys[i], 0);
                PlayerPrefs.DeleteKey(intKeys[i]);
            }
            hadEvidence = PlayerPrefs.HasKey("cv_soc_evidence_json");
            savedEvidence = PlayerPrefs.GetString("cv_soc_evidence_json", "");
            PlayerPrefs.DeleteKey("cv_soc_evidence_json");
        }

        [TearDown]
        public void RestoreProgress()
        {
            for (int i = 0; i < intKeys.Length; i++)
            {
                if (hadInt[i]) PlayerPrefs.SetInt(intKeys[i], savedInt[i]);
                else PlayerPrefs.DeleteKey(intKeys[i]);
            }
            if (hadEvidence) PlayerPrefs.SetString("cv_soc_evidence_json", savedEvidence);
            else PlayerPrefs.DeleteKey("cv_soc_evidence_json");
            PlayerPrefs.Save();
        }

        [Test]
        public void ScenarioData_HasThreeSolvableFourByFourCases()
        {
            Type content = FindType("Cyverse.Level.Level2Content");
            Array scenarios = (Array)content.GetMethod("SocScenarios").Invoke(null, null);
            Assert.That(scenarios.Length, Is.EqualTo(3));

            for (int i = 0; i < scenarios.Length; i++)
            {
                object scenario = scenarios.GetValue(i);
                Type scenarioType = scenario.GetType();
                Array rows = (Array)scenarioType.GetField("rows").GetValue(scenario);
                int trigger = (int)scenarioType.GetField("triggerRowIndex").GetValue(scenario);
                Assert.That(rows.Length, Is.EqualTo(4), $"Scenario {i + 1} must have four rows.");
                Assert.That(trigger, Is.InRange(0, 3), $"Scenario {i + 1} trigger must identify one row.");

                var computers = new HashSet<string>();
                foreach (object row in rows)
                    computers.Add((string)row.GetType().GetField("computer").GetValue(row));
                Assert.That(computers.SetEquals(new[] { "WS-01", "WS-02", "WS-03", "WS-04" }), Is.True,
                    $"Scenario {i + 1} must map one row to every workstation.");
            }

            object finalScenario = scenarios.GetValue(2);
            Type finalType = finalScenario.GetType();
            Array finalRows = (Array)finalType.GetField("rows").GetValue(finalScenario);
            int finalTrigger = (int)finalType.GetField("triggerRowIndex").GetValue(finalScenario);
            object finalRow = finalRows.GetValue(finalTrigger);
            Assert.That((string)finalRow.GetType().GetField("computer").GetValue(finalRow), Is.EqualTo("WS-03"));
            Assert.That((string)finalRow.GetType().GetField("time").GetValue(finalRow), Is.EqualTo("02:13"));
        }

        [Test]
        public void DigitalForensicsGate_RequiresAllThreeKeysAndLevelCompletion()
        {
            Type progress = FindType("Cyverse.Core.LevelProgress");
            MethodInfo unlocked = progress.GetMethod("IsUnlocked");

            for (int mask = 0; mask < 8; mask++)
            {
                PlayerPrefs.SetInt("cv_done_2", 1);
                PlayerPrefs.SetInt("cv_soc_compromised_computer", (mask & 1) != 0 ? 1 : 0);
                PlayerPrefs.SetInt("cv_soc_chain_of_custody", (mask & 2) != 0 ? 1 : 0);
                PlayerPrefs.SetInt("cv_soc_playbook_solved", (mask & 4) != 0 ? 1 : 0);
                bool result = (bool)unlocked.Invoke(null, new object[] { 3 });
                Assert.That(result, Is.EqualTo(mask == 7), $"Unexpected DF gate result for key mask {mask}.");
            }

            PlayerPrefs.SetInt("cv_done_2", 0);
            PlayerPrefs.SetInt("cv_soc_compromised_computer", 1);
            PlayerPrefs.SetInt("cv_soc_chain_of_custody", 1);
            PlayerPrefs.SetInt("cv_soc_playbook_solved", 1);
            Assert.That((bool)unlocked.Invoke(null, new object[] { 3 }), Is.False,
                "The SOC level itself must also be complete.");
        }

        [Test]
        public void EvidenceRecord_RoundTripsAsStructuredJson()
        {
            Type evidenceType = FindType("Cyverse.Level.SocEvidenceRecord");
            Type progressType = FindType("Cyverse.Level.SocProgress");
            object evidence = Activator.CreateInstance(evidenceType);
            Set(evidence, "alertTitle", "Suspicious Account Discovery Commands");
            Set(evidence, "time", "02:13");
            Set(evidence, "computer", "WS-03");
            Set(evidence, "user", "d.chen");
            Set(evidence, "activity", "net user /domain");
            Set(evidence, "verificationResult", "machine locked/idle — activity unexplained");
            Set(evidence, "collectedAtUtc", "2026-08-14 17:00 UTC");
            Set(evidence, "analystName", "SOC Analyst");
            Set(evidence, "inventoryItem", "Evidence: WS-03 disk image + chain-of-custody record");

            progressType.GetMethod("StoreEvidence").Invoke(null, new[] { evidence });
            object[] args = { null };
            bool found = (bool)progressType.GetMethod("TryGetEvidence").Invoke(null, args);
            Assert.That(found, Is.True);
            Assert.That(Get(args[0], "computer"), Is.EqualTo("WS-03"));
            Assert.That(Get(args[0], "activity"), Is.EqualTo("net user /domain"));
            Assert.That(PlayerPrefs.GetInt("cv_soc_compromised_computer"), Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt("cv_soc_chain_of_custody"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator VisualPass_RewiresFourWorkstationsAndBuildsAlertBoard()
        {
            SceneManager.LoadScene("Level2_CyberDefense_VisualPass", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Type endpointType = FindType("Cyverse.Interaction.EndpointStation");
            UnityEngine.Object[] endpoints = UnityEngine.Object.FindObjectsOfType(endpointType);
            Assert.That(endpoints.Length, Is.EqualTo(4),
                "The saved five-desk EDR row should become exactly four active SOC workstations.");

            var names = new HashSet<string>();
            foreach (object endpoint in endpoints)
            {
                object definition = endpointType.GetField("def").GetValue(endpoint);
                names.Add((string)definition.GetType().GetField("hostname").GetValue(definition));
            }
            Assert.That(names.SetEquals(new[] { "WS-01", "WS-02", "WS-03", "WS-04" }), Is.True);

            Type siemType = FindType("Cyverse.Interaction.SiemConsole");
            object siem = UnityEngine.Object.FindObjectOfType(siemType);
            Assert.That(siem, Is.Not.Null);

            var siemComponent = (Component)siem;
            var aimCollider = siemComponent.GetComponent<BoxCollider>();
            Assert.That(aimCollider, Is.Not.Null,
                "The Alert Board needs an eye-level target collider in the saved visual-pass scene.");
            Assert.That(aimCollider.isTrigger, Is.True,
                "The enlarged Alert Board target must not block player movement.");

            Physics.SyncTransforms();
            var eyeLevelRay = new Ray(
                siemComponent.transform.TransformPoint(0f, 2.5f, -3f),
                siemComponent.transform.forward);
            Assert.That(Physics.Raycast(eyeLevelRay, out RaycastHit hit, 6f,
                ~0, QueryTriggerInteraction.Collide), Is.True,
                "An eye-level interaction ray should hit the Alert Board.");
            Assert.That(hit.collider.GetComponentInParent(siemType), Is.SameAs(siem),
                "The visible Alert Board must resolve to the SiemConsole interaction.");

            siemType.GetMethod("Interact").Invoke(siem, new object[] { null });
            yield return null;

            Type gameState = FindType("Cyverse.Core.GameState");
            Assert.That((bool)gameState.GetField("SocInvestigationOpen").GetValue(null), Is.True);
            Assert.That(GameObject.Find("SocInvestigationPanel"), Is.Not.Null,
                "Opening the Alert Board should construct its TMP comparison UI.");

            siemType.GetMethod("ClosePanel", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(siem, null);
        }

        private static void Set(object instance, string field, string value) =>
            instance.GetType().GetField(field).SetValue(instance, value);

        private static string Get(object instance, string field) =>
            (string)instance.GetType().GetField(field).GetValue(instance);

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
