using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Cyverse.Tests
{
    /// <summary>Acceptance coverage for the Level 4 controlled simulation.
    /// These tests verify authored state transitions and local-only choices;
    /// no exploit process or network action is ever started.</summary>
    public sealed class Level4CyberAttackAcceptanceTests
    {
        private readonly string[] progressKeys = { "cv_done_4", "cv_best" };
        private readonly bool[] hadKeys = new bool[2];
        private readonly int[] savedValues = new int[2];

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
            for (int i = 0; i < progressKeys.Length; i++)
            {
                if (hadKeys[i]) PlayerPrefs.SetInt(progressKeys[i], savedValues[i]);
                else PlayerPrefs.DeleteKey(progressKeys[i]);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void Content_DefinesFourSafeEscalatingStations()
        {
            Type contentType = FindType("Cyverse.Level.Level4CyberAttackContent");
            Array stations = (Array)contentType.GetMethod("Stations").Invoke(null, null);
            Assert.That(stations, Has.Length.EqualTo(4));

            var kinds = new HashSet<string>();
            for (int i = 0; i < stations.Length; i++)
            {
                object station = stations.GetValue(i);
                Type stationType = station.GetType();
                string kind = stationType.GetField("kind").GetValue(station).ToString();
                string title = (string)stationType.GetField("title").GetValue(station);
                Array options = (Array)stationType.GetField("options").GetValue(station);
                int correct = (int)stationType.GetField("correctOption").GetValue(station);
                int dataGain = (int)stationType.GetField("dataGain").GetValue(station);
                int detectionPenalty = (int)stationType.GetField("detectionPenalty").GetValue(station);
                kinds.Add(kind);
                Assert.That(options, Has.Length.EqualTo(3));
                Assert.That(correct, Is.InRange(0, options.Length - 1));
                Assert.That(dataGain, Is.GreaterThan(0));
                Assert.That(detectionPenalty, Is.GreaterThan(0));
                Assert.That(title, Does.Contain("//"),
                    "Station headings should show the defensive concept being inverted.");
            }

            Assert.That(kinds, Has.Count.EqualTo(4));
            Assert.That(GetStationTitle(stations, 0), Does.Contain("BYPASS MFA"));
            Assert.That(GetStationTitle(stations, 1), Does.Contain("ESCALATE PRIVILEGES"));
            Assert.That(GetStationTitle(stations, 2), Does.Contain("EXTRACT DATA"));
            Assert.That(GetStationTitle(stations, 3), Does.Contain("COVER TRACKS"));
        }

        [UnityTest]
        public IEnumerator Scene_BuildsFourStationsAndCompletesThroughManager()
        {
            SceneManager.LoadScene("Level4_CyberAttack", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Type managerType = FindType("Cyverse.Level.Level4CyberAttackManager");
            Type stationType = FindType("Cyverse.Interaction.CyberAttackStation");
            Type briefingType = FindType("Cyverse.Interaction.VideoStation");
            object manager = Object.FindObjectOfType(managerType);
            Object[] stations = Object.FindObjectsOfType(stationType);
            object briefing = Object.FindObjectOfType(briefingType);

            Assert.That(manager, Is.Not.Null);
            Assert.That(stations, Has.Length.EqualTo(4));
            Assert.That(briefing, Is.Not.Null);
            Assert.That(managerType.GetProperty("CurrentPhase").GetValue(manager).ToString(), Is.EqualTo("Briefing"));

            // The Level 4 HUD is procedural, so keep the layout contract in
            // the acceptance path: the checklist must be masked/bounded and
            // the scorecard rows must remain inside their own card at any
            // CanvasScaler resolution.
            Type taskListType = FindType("Cyverse.UI.TaskListPanel");
            Component taskList = Object.FindObjectOfType(taskListType) as Component;
            Assert.That(taskList, Is.Not.Null);
            GameObject taskPanel = taskListType.GetField("panel",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(taskList) as GameObject;
            TMP_Text taskBody = taskListType.GetField("bodyText",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(taskList) as TMP_Text;
            Assert.That(taskPanel, Is.Not.Null, "Level 4 should create a checklist card.");
            Assert.That(taskPanel.GetComponent<RectMask2D>(), Is.Not.Null,
                "Checklist text must be clipped by its card instead of escaping into the scene.");
            Assert.That(taskBody, Is.Not.Null);
            Assert.That(taskBody.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));

            // The SOC evidence handoff is meaningful in Levels 2 and 3 only.
            // Level 4 must not recreate that forensic inventory card simply
            // because a saved SOC record happens to exist.
            Type evidenceType = FindType("Cyverse.UI.EvidenceInventoryPanel");
            Assert.That(Object.FindObjectOfType(evidenceType), Is.Null,
                "The evidence inventory is scoped to the SOC and forensics levels.");

            MeshRenderer floor = GameObject.Find("Floor")?.GetComponent<MeshRenderer>();
            Assert.That(floor, Is.Not.Null, "Level 4 needs its shared floor collider and renderer.");
            Assert.That(floor.sharedMaterial.GetFloat("_PulseStrength"), Is.EqualTo(0f).Within(0.001f),
                "The Level 4 floor must stay static rather than competing with station UI.");
            Assert.That(GameObject.Find("L4_AisleSpine"), Is.Null,
                "The old full-room aisle rail should not be rebuilt over the floor.");
            Assert.That(GameObject.Find("AttackPath_0"), Is.Null,
                "The duplicate factory route rails should not be rebuilt over the floor.");

            GameObject ceiling = GameObject.Find("CeilingSlab");
            Assert.That(ceiling, Is.Not.Null);
            Assert.That(ceiling.transform.position.y, Is.GreaterThanOrEqualTo(7.3f),
                "Level 4's ceiling must clear the tall station boards.");
            GameObject northWall = GameObject.Find("Wall_North");
            Assert.That(northWall, Is.Not.Null);
            Assert.That(northWall.transform.localScale.y, Is.GreaterThanOrEqualTo(7.2f),
                "The raised ceiling needs a continuous wall shell beneath it.");
            GameObject centreHeaderColumn = FindIncludingInactive("Column_N0");
            Assert.That(centreHeaderColumn, Is.Not.Null);
            Assert.That(centreHeaderColumn.activeSelf, Is.False,
                "The north header bay must not be bisected by a structural column.");

            TMP_Text briefingTitle = briefingType.GetField("titleText").GetValue(briefing) as TMP_Text;
            TMP_Text briefingBody = briefingType.GetField("bodyText").GetValue(briefing) as TMP_Text;
            Assert.That(briefingTitle, Is.Not.Null);
            Assert.That(briefingBody, Is.Not.Null);
            float titleBottom = briefingTitle.transform.localPosition.y -
                briefingTitle.rectTransform.sizeDelta.y * briefingTitle.transform.localScale.y * 0.5f;
            float bodyTop = briefingBody.transform.localPosition.y +
                briefingBody.rectTransform.sizeDelta.y * briefingBody.transform.localScale.y * 0.5f;
            Assert.That(titleBottom, Is.GreaterThan(bodyTop),
                "Briefing title and body must occupy separate vertical bands.");

            Type meterType = FindType("Cyverse.Level.Level4ExfiltrationMeter");
            Component meterLayout = Object.FindObjectOfType(meterType) as Component;
            Assert.That(meterLayout, Is.Not.Null);
            RectTransform meterPanel = meterType.GetField("panelRect",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(meterLayout) as RectTransform;
            Assert.That(meterPanel, Is.Not.Null);
            AssertChildInside(meterPanel, meterType, meterLayout, "detectionLabel", "DetectionLabel");
            AssertChildInside(meterPanel, meterType, meterLayout, "detectionValue", "DetectionValue");
            AssertChildInside(meterPanel, meterType, meterLayout, "statusText", "Status");

            PropertyInfo stationIndexProperty = stationType.GetProperty("StationIndex");
            PropertyInfo stationKindProperty = stationType.GetProperty("Kind");
            string[] expectedKinds = { "BypassMfa", "EscalatePrivileges", "ExtractData", "CoverTracks" };
            foreach (Object station in stations)
            {
                Component stationComponent = (Component)station;
                BoxCollider aim = stationComponent.GetComponent<BoxCollider>();
                Assert.That(aim, Is.Not.Null);
                Assert.That(aim.isTrigger, Is.True);
                int stationIndex = (int)stationIndexProperty.GetValue(station);
                Assert.That(stationIndex, Is.InRange(0, expectedKinds.Length - 1));
                Assert.That(stationKindProperty.GetValue(station).ToString(), Is.EqualTo(expectedKinds[stationIndex]));

                Vector3 readableDirection = -stationComponent.transform.forward;
                readableDirection.y = 0f;
                Vector3 towardCentre = new Vector3(0f, 0f, 11f) - stationComponent.transform.position;
                towardCentre.y = 0f;
                Assert.That(Vector3.Dot(readableDirection.normalized, towardCentre.normalized),
                    Is.GreaterThan(0.995f), "Every Level 4 monitor must face the shared inward focal point.");
            }

            // Use the same deterministic hooks as the campaign TAS: the
            // briefing event starts the timer, then the manager submits each
            // authored correct choice in station order.
            briefingType.GetMethod("CompleteForAutomation").Invoke(briefing, null);
            yield return null;
            Assert.That(managerType.GetProperty("CurrentPhase").GetValue(manager).ToString(), Is.EqualTo("BypassMfa"));

            // Open the first choice screen and assert every control stays
            // inside the modal. This catches inverted top anchors, which once
            // placed the title above the panel and made the choices vanish.
            Component firstStation = null;
            foreach (Object station in stations)
            {
                if ((int)stationIndexProperty.GetValue(station) == 0)
                {
                    firstStation = station as Component;
                    break;
                }
            }
            Assert.That(firstStation, Is.Not.Null);
            stationType.GetMethod("Interact").Invoke(firstStation, new object[] { null });
            yield return null;
            GameObject choicePanel = stationType.GetField("panel",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(firstStation) as GameObject;
            Assert.That(choicePanel, Is.Not.Null);
            RectTransform choiceRect = choicePanel.GetComponent<RectTransform>();
            AssertChildInside(choiceRect, stationType, firstStation, "titleText", "Choice title");
            AssertChildInside(choiceRect, stationType, firstStation, "objectiveText", "Choice objective");
            AssertChildInside(choiceRect, stationType, firstStation, "situationText", "Choice situation");
            Button[] choices = stationType.GetField("optionButtons",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(firstStation) as Button[];
            Assert.That(choices, Has.Length.EqualTo(3));
            foreach (Button choice in choices)
                AssertRectInside(choiceRect, choice.GetComponent<RectTransform>(), "Choice option");
            stationType.GetMethod("CloseForManager").Invoke(firstStation, null);
            yield return null;
            Type hudType = FindType("Cyverse.UI.HudUI");
            Component hud = hudType.GetProperty("Instance").GetValue(null) as Component;
            Text objectiveText = hudType.GetField("objectiveText",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud) as Text;
            Assert.That(objectiveText, Is.Not.Null);
            Assert.That(objectiveText.text, Does.Not.Contain("EXFILTRATION"),
                "The top objective should not duplicate the dedicated scorecard telemetry.");
            managerType.GetMethod("CompleteForAutomation").Invoke(manager, null);
            yield return null;
            yield return null;

            Assert.That(managerType.GetProperty("CurrentPhase").GetValue(manager).ToString(), Is.EqualTo("Complete"));
            Assert.That((bool)managerType.GetProperty("IsLevelComplete").GetValue(manager), Is.True);
            Assert.That((int)managerType.GetProperty("ExfiltrationPercent").GetValue(manager), Is.EqualTo(100));
            Assert.That((int)managerType.GetProperty("DetectionPercent").GetValue(manager), Is.EqualTo(0));
            MethodInfo isStationComplete = managerType.GetMethod("IsStationComplete");
            foreach (Object station in stations)
                Assert.That((bool)isStationComplete.Invoke(manager, new object[] { station }), Is.True,
                    "Every station should retain its completed state after the final transition.");
            object meter = Object.FindObjectOfType(meterType);
            Assert.That(meter, Is.Not.Null, "Level 4 must expose the final exfiltration/detection meter.");
            Assert.That((float)meterType.GetProperty("DataProgress").GetValue(meter), Is.EqualTo(1f).Within(0.001f));
            Assert.That((float)meterType.GetProperty("DetectionProgress").GetValue(meter), Is.EqualTo(0f).Within(0.001f));

            // ResultsScreen receives the same final meter values as the HUD;
            // keep this visible in the acceptance path so a future UI change
            // cannot silently reduce completion to a generic score card.
            Type resultsType = FindType("Cyverse.UI.ResultsScreen");
            object results = resultsType.GetProperty("Instance").GetValue(null);
            Assert.That(results, Is.Not.Null);
            FieldInfo bodyField = resultsType.GetField("bodyText",
                BindingFlags.Instance | BindingFlags.NonPublic);
            object body = bodyField.GetValue(results);
            string resultsCopy = (string)body.GetType().GetProperty("text").GetValue(body);
            Assert.That(resultsCopy, Does.Contain("DATA STOLEN 100% / CAUGHT 0%"));

            Type progressType = FindType("Cyverse.Core.LevelProgress");
            Assert.That((bool)progressType.GetMethod("IsCompleted").Invoke(null, new object[] { 4 }), Is.True);
        }

        private static string GetStationTitle(Array stations, int index)
        {
            object station = stations.GetValue(index);
            return (string)station.GetType().GetField("title").GetValue(station);
        }

        private static GameObject FindIncludingInactive(string objectName)
        {
            foreach (Transform candidate in Object.FindObjectsOfType<Transform>(true))
                if (candidate != null && candidate.name == objectName)
                    return candidate.gameObject;
            return null;
        }

        private static void AssertChildInside(RectTransform panel, Type ownerType,
            Component owner, string fieldName, string label)
        {
            TMP_Text text = ownerType.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner) as TMP_Text;
            Assert.That(text, Is.Not.Null, "Missing Level 4 meter row: " + label);
            AssertRectInside(panel, text.rectTransform, label);
        }

        private static void AssertRectInside(RectTransform panel, RectTransform child, string label)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, child);
            Rect rect = panel.rect;
            const float tolerance = 1.5f;
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(rect.xMin - tolerance), label + " left escaped the card.");
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(rect.xMax + tolerance), label + " right escaped the card.");
            Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(rect.yMin - tolerance), label + " bottom escaped the card.");
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(rect.yMax + tolerance), label + " top escaped the card.");
        }

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
