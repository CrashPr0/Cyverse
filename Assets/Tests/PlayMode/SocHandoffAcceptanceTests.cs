using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Cyverse.Tests
{
    public class SocHandoffAcceptanceTests
    {
        private readonly string[] intKeys =
        {
            "cv_done_2",
            "cv_done_3",
            "cv_soc_compromised_computer",
            "cv_soc_chain_of_custody",
            "cv_soc_playbook_solved",
            "cv_soc_evidence_locked",
            "cv_scenario_roster_index",
            "cv_scenario_roster_finished",
        };

        private bool[] hadInt;
        private int[] savedInt;
        private bool hadEvidence;
        private string savedEvidence;
        private bool savedRosterInitialized;
        private int savedRosterIndex;

        [SetUp]
        public void PreserveProgress()
        {
            Type roster = FindType("Cyverse.Level.ScenarioRoster");
            savedRosterInitialized = (bool)roster.GetField("sessionInitialized",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            savedRosterIndex = (int)roster.GetField("sessionIndex",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
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
            Type roster = FindType("Cyverse.Level.ScenarioRoster");
            roster.GetField("sessionInitialized", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, savedRosterInitialized);
            roster.GetField("sessionIndex", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, savedRosterIndex);
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

        [Test]
        public void EvidenceLockedKey_IsSetByDepositAndClearedByFreshEvidence()
        {
            Type progressType = FindType("Cyverse.Level.SocProgress");
            Assert.That(PlayerPrefs.GetInt("cv_soc_evidence_locked", 0), Is.EqualTo(0));

            progressType.GetMethod("MarkEvidenceLocked").Invoke(null, null);
            Assert.That(PlayerPrefs.GetInt("cv_soc_evidence_locked"), Is.EqualTo(1));
            Assert.That((bool)progressType.GetProperty("HasEvidenceLocked").GetValue(null), Is.True);

            // A device seized after the deposit is a new device: it has not been locked up yet.
            StoreTestEvidence();
            Assert.That(PlayerPrefs.GetInt("cv_soc_evidence_locked"), Is.EqualTo(0));
            Assert.That((bool)progressType.GetProperty("HasEvidenceLocked").GetValue(null), Is.False);
        }

        [Test]
        public void DigitalForensicsGate_DoesNotNeedTheLockerDeposit()
        {
            // Players who return to the Hub (or never used the locker) must still be able to
            // enter the lab: the locker holds a training device for them.
            PlayerPrefs.SetInt("cv_done_2", 1);
            PlayerPrefs.SetInt("cv_soc_compromised_computer", 1);
            PlayerPrefs.SetInt("cv_soc_chain_of_custody", 1);
            PlayerPrefs.SetInt("cv_soc_playbook_solved", 1);
            PlayerPrefs.SetInt("cv_soc_evidence_locked", 0);
            Type progress = FindType("Cyverse.Core.LevelProgress");
            Assert.That((bool)progress.GetMethod("IsUnlocked").Invoke(null, new object[] { 3 }), Is.True);
        }

        [UnityTest]
        public IEnumerator AlertBoard_UsesPortableSelectionMarkers()
        {
            SceneManager.LoadScene("Level2_CyberDefense", LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
            Type siemType = FindType("Cyverse.Interaction.SiemConsole");
            object siem = UnityEngine.Object.FindObjectOfType(siemType);
            Assert.That(GameplayActionTestDriver.Interact(siem), Is.True);
            yield return null;
            try
            {
                siemType.GetField("flaggedRow", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(siem, 0);
                siemType.GetMethod("RenderAlertBoard", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(siem, new object[] { "" });
                var body = (TMP_Text)siemType.GetField("bodyText",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(siem);
                var controls = (TMP_Text)siemType.GetField("controlsText",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(siem);
                StringAssert.Contains("<color=#E5A823>></color>", body.text);
                StringAssert.Contains("<color=#FF8A78>*</color>", body.text);
                StringAssert.DoesNotContain("\u25B6", body.text);
                StringAssert.DoesNotContain("\u2691", body.text);
                StringAssert.Contains("UP / DOWN SELECT ROW", controls.text);
            }
            finally
            {
                GameplayActionTestDriver.Cancel(siem);
            }
        }

        [UnityTest]
        public IEnumerator BootstrapScene_RewiresFourWorkstationsAndBuildsAlertBoard()
        {
            SceneManager.LoadScene("Level2_CyberDefense", LoadSceneMode.Single);
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
                "The Alert Board needs an eye-level target collider in the bootstrap scene.");
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

            Assert.That(GameplayActionTestDriver.Interact(siem), Is.True);
            yield return null;

            Type gameState = FindType("Cyverse.Core.GameState");
            Assert.That((bool)gameState.GetField("SocInvestigationOpen").GetValue(null), Is.True);
            Assert.That(GameObject.Find("SocInvestigationPanel"), Is.Not.Null,
                "Opening the Alert Board should construct its TMP comparison UI.");

            Assert.That(GameplayActionTestDriver.Cancel(siem), Is.True);
            Assert.That(GameplayActionTestDriver.RunDeterministicAction(
                "CompleteSocInvestigation", siem), Is.True,
                "The deterministic adapter must finish the SOC route only through player actions.");
            Assert.That((bool)siemType.GetProperty("IsComplete").GetValue(siem), Is.True);
            Assert.That(PlayerPrefs.GetInt("cv_soc_chain_of_custody", 0), Is.EqualTo(1));

            // The resolved alert yields a physical device that has to be sealed in the
            // north-wall locker; the direct door to the lab stays shut until then (and
            // until the rest of the SOC mission is done).
            Type lockerType = FindType("Cyverse.Interaction.EvidenceLocker");
            Type carryableType = FindType("Cyverse.Interaction.Carryable");
            Type doorType = FindType("Cyverse.Interaction.HubDoor");
            object locker = UnityEngine.Object.FindObjectOfType(lockerType);
            Assert.That(locker, Is.Not.Null,
                "The saved SOC scene gets its evidence locker from the manager at startup.");
            Assert.That(((Component)locker).transform.position.z, Is.GreaterThan(18.5f),
                "The SOC locker belongs in the north wall, the wall the player exits through.");

            object forensicsDoor = null;
            foreach (UnityEngine.Object candidate in UnityEngine.Object.FindObjectsOfType(doorType))
                if ((string)doorType.GetField("sceneName").GetValue(candidate) == "Level3_Forensics")
                    forensicsDoor = candidate;
            Assert.That(forensicsDoor, Is.Not.Null, "The SOC needs a direct door to the Forensics Lab.");
            Assert.That((bool)doorType.GetProperty("IsOpen").GetValue(forensicsDoor), Is.False);

            Component seized = null;
            foreach (UnityEngine.Object candidate in UnityEngine.Object.FindObjectsOfType(carryableType))
                if ((string)carryableType.GetField("id").GetValue(candidate) == "seized_device")
                    seized = (Component)candidate;
            Assert.That(seized, Is.Not.Null, "Resolving the alert must hand the player the seized device.");
            Assert.That(PlayerPrefs.GetInt("cv_soc_evidence_locked", 0), Is.EqualTo(0));

            // An empty-handed player cannot seal anything.
            Assert.That(GameplayActionTestDriver.Interact(locker), Is.True);
            Assert.That((bool)lockerType.GetProperty("IsSealed").GetValue(locker), Is.False);

            Assert.That(GameplayActionTestDriver.RunDeterministicAction("SecureSeizedDevice", locker), Is.True,
                "The seized device must be carried to the locker through the normal pickup/place actions.");
            Assert.That((bool)lockerType.GetProperty("IsSealed").GetValue(locker), Is.True);
            Assert.That(PlayerPrefs.GetInt("cv_soc_evidence_locked", 0), Is.EqualTo(1));
            Assert.That(carryableType.GetProperty("Carried").GetValue(null), Is.Null,
                "The device leaves the player's hands when it is locked away.");
            Assert.That((bool)doorType.GetProperty("IsOpen").GetValue(forensicsDoor), Is.False,
                "The lab door also waits for the playbook and the certification exam.");
        }

        [UnityTest]
        public IEnumerator ForensicsIntake_RequiresAndCompletesClickableCustodyForm()
        {
            Type evidenceType = FindType("Cyverse.Level.SocEvidenceRecord");
            Type progressType = FindType("Cyverse.Level.SocProgress");
            object evidence = Activator.CreateInstance(evidenceType);
            Set(evidence, "alertTitle", "Suspicious Account Discovery Commands");
            Set(evidence, "computer", "WS-03");
            Set(evidence, "user", "d.chen");
            Set(evidence, "activity", "net user /domain");
            Set(evidence, "verificationResult", "machine locked/idle — activity unexplained");
            Set(evidence, "collectedAtUtc", "2026-08-14 17:00 UTC");
            Set(evidence, "analystName", "SOC Analyst");
            Set(evidence, "inventoryItem", "Evidence: WS-03 disk image + chain-of-custody record");
            progressType.GetMethod("StoreEvidence").Invoke(null, new[] { evidence });

            SceneManager.LoadScene("Level3_Forensics", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Type stationType = FindType("Cyverse.Interaction.ChainOfCustodyStation");
            Type formType = FindType("Cyverse.Forensics.ChainOfCustodyForm");
            Type consoleType = FindType("Cyverse.Interaction.ForensicsConsole");
            object station = UnityEngine.Object.FindObjectOfType(stationType);
            object form = UnityEngine.Object.FindObjectOfType(formType);
            object console = UnityEngine.Object.FindObjectOfType(consoleType);
            Assert.That(station, Is.Not.Null);
            Assert.That(form, Is.Not.Null);
            Assert.That(console, Is.Not.Null);

            // The first step in the lab is taking the device out of the evidence locker,
            // and Evidence Intake refuses to open until that has been done.
            Type lockerType = FindType("Cyverse.Interaction.EvidenceLocker");
            Type carryableType = FindType("Cyverse.Interaction.Carryable");
            object locker = UnityEngine.Object.FindObjectOfType(lockerType);
            Assert.That(locker, Is.Not.Null, "The lab must hold the handed-off device in an evidence locker.");
            Assert.That(((Component)locker).transform.position.z, Is.LessThan(-18.5f),
                "The lab locker belongs in the south wall the player arrives through.");
            Assert.That((bool)lockerType.GetProperty("DeviceRetrieved").GetValue(locker), Is.False);
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            Assert.That((bool)formType.GetProperty("IsOpen").GetValue(form), Is.False,
                "Evidence Intake must not open before the device is retrieved.");

            Assert.That(GameplayActionTestDriver.RunDeterministicAction("RetrieveEvidenceDevice", locker), Is.True);
            Assert.That((bool)lockerType.GetProperty("DeviceRetrieved").GetValue(locker), Is.True);
            Assert.That(carryableType.GetProperty("Carried").GetValue(null), Is.Not.Null,
                "The retrieved device should be in the player's hands.");

            // Opening intake constructs four mouse-clickable blanks.
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            // The LEFT rework removed the evidence-download phone: the form is
            // fillable as soon as it opens, so there is no download gate here.
            yield return null;
            int blanks = 0;
            foreach (Button button in UnityEngine.Object.FindObjectsOfType<Button>(true))
                if (button.name.StartsWith("Blank_")) blanks++;
            Assert.That(blanks, Is.EqualTo(4));
            Assert.That(GameObject.Find("ChainOfCustodyForm"), Is.Not.Null);

            for (int field = 0; field < 4; field++)
                Assert.That(GameplayActionTestDriver.Select(form, field, GameplayActionTestDriver.CustodyAnswer(field)), Is.True);
            Assert.That(GameplayActionTestDriver.Submit(form), Is.True);
            Assert.That(GameplayActionTestDriver.Cancel(form), Is.True);
            yield return null;
            Assert.That((bool)formType.GetProperty("IsComplete").GetValue(form), Is.True);

            // First E docks the evidence device and starts the upload; the
            // second opens the terminal (finishing the upload instantly).
            Type uploadType = FindType("Cyverse.Interaction.PlugInStation");
            object upload = uploadType.GetProperty("Instance").GetValue(null);
            Assert.That(upload, Is.Not.Null, "The investigation desk should host the evidence upload.");
            Assert.That(GameplayActionTestDriver.Interact(console), Is.True);
            yield return null;
            Assert.That((bool)uploadType.GetProperty("UploadStarted").GetValue(upload), Is.True,
                "The first E at the desk after custody should start the evidence upload.");
            Type gameState = FindType("Cyverse.Core.GameState");
            Assert.That((bool)gameState.GetField("QuizActive").GetValue(null), Is.False,
                "Starting the upload must not also open the terminal.");
            Assert.That(carryableType.GetProperty("Carried").GetValue(null), Is.Null,
                "The device leaves the player's hands when it docks in the cradle.");
            Assert.That(GameplayActionTestDriver.Interact(console), Is.True);
            yield return null;
            Assert.That((bool)uploadType.GetProperty("UploadComplete").GetValue(upload), Is.True);
            Assert.That((bool)gameState.GetField("QuizActive").GetValue(null), Is.True,
                "Completing custody should unlock the forensic query terminal.");

            Type terminalType = FindType("Cyverse.Forensics.QueryTerminal");
            object terminal = UnityEngine.Object.FindObjectOfType(terminalType);
            Assert.That(terminalType.GetProperty("TutorialActive").GetValue(terminal), Is.True,
                "The first forensic investigation should open with a guided start.");
            Assert.That(terminalType.GetProperty("TutorialStep").GetValue(terminal), Is.EqualTo(0));

            Assert.That(GameplayActionTestDriver.ClearText(terminal), Is.True);
            Assert.That(GameplayActionTestDriver.Append(terminal, "tables"), Is.True);
            Assert.That(GameplayActionTestDriver.Submit(terminal), Is.True);
            Assert.That(terminalType.GetProperty("TutorialStep").GetValue(terminal), Is.EqualTo(1));

            Assert.That(GameplayActionTestDriver.ClearText(terminal), Is.True);
            Assert.That(GameplayActionTestDriver.Append(terminal,
                "EvidenceManifest | project computer"), Is.True);
            Assert.That(GameplayActionTestDriver.Submit(terminal), Is.True);
            Assert.That(terminalType.GetProperty("TutorialStep").GetValue(terminal), Is.EqualTo(2));

            Assert.That(GameplayActionTestDriver.ClearText(terminal), Is.True);
            Assert.That(GameplayActionTestDriver.Append(terminal, "answer WS-03"), Is.True);
            Assert.That(GameplayActionTestDriver.Submit(terminal), Is.True);
            Assert.That(terminalType.GetProperty("TutorialActive").GetValue(terminal), Is.False,
                "Submitting the guided finding should hand control back to the normal case flow.");
            Assert.That(terminalType.GetProperty("TutorialStep").GetValue(terminal), Is.EqualTo(3));
            Assert.That(GameplayActionTestDriver.Cancel(terminal), Is.True);
        }

        [UnityTest]
        public IEnumerator ForensicsEndFlow_RequiresExplicitReportSubmission()
        {
            StoreTestEvidence();
            SceneManager.LoadScene("Level3_Forensics", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Type managerType = FindType("Cyverse.Level.Level3ForensicsManager");
            Type briefingType = FindType("Cyverse.Interaction.VideoStation");
            Type custodyStationType = FindType("Cyverse.Interaction.ChainOfCustodyStation");
            Type formType = FindType("Cyverse.Forensics.ChainOfCustodyForm");
            Type consoleType = FindType("Cyverse.Interaction.ForensicsConsole");
            Type reportType = FindType("Cyverse.Interaction.ForensicsReportStation");
            Type gameState = FindType("Cyverse.Core.GameState");

            object manager = UnityEngine.Object.FindObjectOfType(managerType);
            object briefing = UnityEngine.Object.FindObjectOfType(briefingType);
            object custodyStation = UnityEngine.Object.FindObjectOfType(custodyStationType);
            object form = UnityEngine.Object.FindObjectOfType(formType);
            object console = UnityEngine.Object.FindObjectOfType(consoleType);
            object report = UnityEngine.Object.FindObjectOfType(reportType);
            Assert.That(manager, Is.Not.Null);
            Assert.That(briefing, Is.Not.Null);
            Assert.That(custodyStation, Is.Not.Null);
            Assert.That(form, Is.Not.Null);
            Assert.That(console, Is.Not.Null);
            Assert.That(report, Is.Not.Null, "The visible report desk must be mechanically usable.");

            Component reportComponent = (Component)report;
            BoxCollider aim = reportComponent.GetComponent<BoxCollider>();
            Assert.That(aim, Is.Not.Null);
            Assert.That(aim.isTrigger, Is.True, "The report aim target must not block player movement.");

            Assert.That(GameplayActionTestDriver.Scrub(briefing, float.MaxValue), Is.True);
            object locker = UnityEngine.Object.FindObjectOfType(FindType("Cyverse.Interaction.EvidenceLocker"));
            Assert.That(locker, Is.Not.Null);
            Assert.That(GameplayActionTestDriver.RunDeterministicAction("RetrieveEvidenceDevice", locker), Is.True);
            Assert.That(GameplayActionTestDriver.Interact(custodyStation), Is.True);
            // No evidence-download step in the LEFT rework; proceed to fill.
            yield return null;
            for (int field = 0; field < 4; field++)
                Assert.That(GameplayActionTestDriver.Select(form, field, GameplayActionTestDriver.CustodyAnswer(field)), Is.True);
            Assert.That(GameplayActionTestDriver.Submit(form), Is.True);
            Assert.That(GameplayActionTestDriver.Cancel(form), Is.True);
            yield return GameplayActionTestDriver.RunDeterministic("CompleteForensics", console);
            yield return null;

            TMP_Text consoleReadout = (TMP_Text)consoleType
                .GetField("readoutText", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(console);
            Assert.That(consoleReadout, Is.Not.Null);
            StringAssert.DoesNotContain("\u2713", consoleReadout.text,
                "The completed terminal uses a glyph missing from the runtime TMP font.");
            StringAssert.Contains("[OK]", consoleReadout.text,
                "The completed terminal should expose an ASCII-safe completion marker.");

            Assert.That(managerType.GetProperty("CurrentPhase").GetValue(manager).ToString(),
                Is.EqualTo("Report"));
            Assert.That((bool)managerType.GetProperty("ReportReady").GetValue(manager), Is.True);
            Assert.That((bool)managerType.GetProperty("ReportSubmitted").GetValue(manager), Is.False);
            Assert.That((bool)gameState.GetField("LevelComplete").GetValue(null), Is.False,
                "Closing the terminal cases must not bypass the report step.");
            Assert.That(PlayerPrefs.GetInt("cv_done_3", 0), Is.EqualTo(0));

            Assert.That(GameplayActionTestDriver.Interact(report), Is.True);
            yield return null;
            yield return null;

            Assert.That(managerType.GetProperty("CurrentPhase").GetValue(manager).ToString(),
                Is.EqualTo("Complete"));
            Assert.That((bool)managerType.GetProperty("ReportSubmitted").GetValue(manager), Is.True);
            Assert.That((bool)gameState.GetField("LevelComplete").GetValue(null), Is.True);
            Assert.That(PlayerPrefs.GetInt("cv_done_3", 0), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ForensicsLocker_HoldsATrainingDeviceWhenNothingCameFromTheSoc()
        {
            // Arriving from the Hub with no SOC deposit must never soft-lock the lab.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DF HANDOFF\]"));
            SceneManager.LoadScene("Level3_Forensics", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Type lockerType = FindType("Cyverse.Interaction.EvidenceLocker");
            Type stationType = FindType("Cyverse.Interaction.ChainOfCustodyStation");
            Type formType = FindType("Cyverse.Forensics.ChainOfCustodyForm");
            object locker = UnityEngine.Object.FindObjectOfType(lockerType);
            object station = UnityEngine.Object.FindObjectOfType(stationType);
            object form = UnityEngine.Object.FindObjectOfType(formType);
            Assert.That(locker, Is.Not.Null);
            Assert.That(station, Is.Not.Null);
            Assert.That(GameplayActionTestDriver.RunDeterministicAction("RetrieveEvidenceDevice", locker), Is.True,
                "The locker still releases a (training) device without a SOC deposit.");
            Assert.That(GameplayActionTestDriver.Interact(station), Is.True);
            yield return null;
            Assert.That((bool)formType.GetProperty("IsOpen").GetValue(form), Is.True,
                "With the device in hand, Evidence Intake opens as normal.");
            GameplayActionTestDriver.Cancel(form);
        }

        [Test]
        public void ForensicsDatasets_KeepEveryRotatingAnswerSolvable()
        {
            Type databaseType = FindType("Cyverse.Forensics.LogDatabase");
            Type caseType = FindType("Cyverse.Forensics.InvestigationCase");
            Type queryType = FindType("Cyverse.Forensics.MiniKql");
            object database = databaseType.GetMethod("Build").Invoke(null, null);
            object[] cases =
            {
                caseType.GetMethod("SpartanGold").Invoke(null, null),
                caseType.GetMethod("MidnightExfil").Invoke(null, null),
            };

            foreach (object investigation in cases)
            {
                Array questions = (Array)caseType.GetField("questions").GetValue(investigation);
                foreach (object question in questions)
                {
                    string example = (string)question.GetType().GetField("exampleQuery").GetValue(question);
                    object result = queryType.GetMethod("Run").Invoke(null, new[] { database, example });
                    string error = (string)result.GetType().GetField("error").GetValue(result);
                    Assert.That(error, Is.Null, $"Broken example query: {example}");
                }
            }
        }

        private static void StoreTestEvidence()
        {
            Type evidenceType = FindType("Cyverse.Level.SocEvidenceRecord");
            Type progressType = FindType("Cyverse.Level.SocProgress");
            object evidence = Activator.CreateInstance(evidenceType);
            Set(evidence, "alertTitle", "Suspicious Account Discovery Commands");
            Set(evidence, "computer", "WS-03");
            Set(evidence, "user", "d.chen");
            Set(evidence, "activity", "net user /domain");
            Set(evidence, "verificationResult", "machine locked/idle — activity unexplained");
            Set(evidence, "collectedAtUtc", "2026-08-14 17:00 UTC");
            Set(evidence, "analystName", "SOC Analyst");
            Set(evidence, "inventoryItem", "Evidence: WS-03 disk image + chain-of-custody record");
            progressType.GetMethod("StoreEvidence").Invoke(null, new[] { evidence });
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
