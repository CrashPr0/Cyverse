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
    /// <summary>
    /// Cheap scene-wide visual smoke checks for the production bootstrap
    /// scenes. These do not try to judge art direction; they catch the class
    /// of regressions that makes a scene look AI/placeholder-generated: NaN or
    /// degenerate render bounds, props/text outside the room shell, missing
    /// cameras, and a station whose interaction target was lost during an
    /// editor pass. The same checks can run in CI without opening every scene
    /// by hand, while the per-scene logs make a failure actionable.
    /// </summary>
    public sealed class SceneVisualAuditPlayModeTests
    {
        private const float RoomLimit = 22f;
        private const float FloorLimit = -0.35f;
        private const float CeilingLimit = 6.25f;

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Level0Bootstrap_HasSaneBoundsAndInteractionTargets()
        {
            yield return AuditScene("Level0");

            Assert.That(GameObject.Find("Station_iam"), Is.Not.Null);
            Assert.That(GameObject.Find("Station_cia"), Is.Not.Null);
            Assert.That(GameObject.Find("Station_nice"), Is.Not.Null);
            Assert.That(GameObject.Find("SecurityScanner"), Is.Not.Null);

            AssertTriggerAimVolume("Station_iam", 3.0f, 1.8f);
            AssertTriggerAimVolume("Station_cia", 3.0f, 1.8f);
            AssertTriggerAimVolume("Station_nice", 3.0f, 1.8f);
            AssertTriggerAimVolume("SecurityScanner", 3.4f, 1.8f);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Level1IamBootstrap_HasSaneBoundsAndInteractionTargets()
        {
            yield return AuditScene("Level1_IAM");

            Assert.That(GameObject.Find("BriefingScreen"), Is.Not.Null);
            Assert.That(GameObject.Find("BadgeStation"), Is.Not.Null);
            Assert.That(GameObject.Find("MfaVault"), Is.Not.Null);
            Assert.That(GameObject.Find("SortingStation"), Is.Not.Null);
            Assert.That(GameObject.Find("AuditStation"), Is.Not.Null);
            Assert.That(GameObject.Find("CertExamStation"), Is.Not.Null);
            AssertQuietGridFloor("Level 1");

            // These are the player-facing targets, not decorative screen
            // meshes. Their presence is what keeps the bootstrap scene playable.
            AssertTriggerAimVolume("BriefingScreen", 3.4f, 4.6f);
            AssertTriggerAimVolume("BadgeStation", 3.0f, 1.8f);
            AssertTriggerAimVolume("AuditStation", 3.4f, 4.8f);
            AssertTriggerAimVolume("CertExamStation", 3.0f, 2.6f);

            Transform scanBar = GameObject.Find("BadgeStation").transform
                .Find("PanelScreen/ScanBar");
            Assert.That(scanBar, Is.Not.Null);
            Assert.That(scanBar.gameObject.activeSelf, Is.False,
                "The enrollment scan bar should appear only during biometric capture, not cover ENROLL at rest.");

            // These are task containers rather than IInteractable targets;
            // their child factors/drop zones own the actual interaction
            // colliders. Keep the audit honest about that wiring too.
            Assert.That(GameObject.Find("DropZone_TOKEN_SLOT"), Is.Not.Null);
            Assert.That(GameObject.Find("DropZone_INTERN"), Is.Not.Null);
            Assert.That(GameObject.Find("DropZone_HR_MANAGER"), Is.Not.Null);
            Assert.That(GameObject.Find("DropZone_SYSADMIN"), Is.Not.Null);

            GameObject authenticator = GameObject.Find("Label_AUTHENTICATOR");
            GameObject passcode = GameObject.Find("Label_PASSCODE");
            Assert.That(authenticator, Is.Not.Null);
            Assert.That(passcode, Is.Not.Null);
            Vector2 authenticatorXZ = new Vector2(authenticator.transform.position.x,
                authenticator.transform.position.z);
            Vector2 passcodeXZ = new Vector2(passcode.transform.position.x,
                passcode.transform.position.z);
            Assert.That(Vector2.Distance(authenticatorXZ, passcodeXZ), Is.GreaterThanOrEqualTo(0.55f),
                "The authenticator title must remain spatially distinct from the mounted passcode screen.");

            Assert.That(GameObject.Find("Sign_DATA_TRIAGE").transform.position.y,
                Is.GreaterThanOrEqualTo(3.6f),
                "The authorization header should clear its crate and role labels.");
            Assert.That(GameObject.Find("Sign_CERTIFICATION").transform.position.y,
                Is.GreaterThanOrEqualTo(3.6f),
                "The certification header should clear the horizontal wall-light strip.");

            var exits = new List<Transform>();
            foreach (Transform candidate in UnityEngine.Object.FindObjectsOfType<Transform>())
                if (candidate.parent == null && candidate.name.StartsWith("HubDoor_", StringComparison.Ordinal))
                    exits.Add(candidate);
            Assert.That(exits.Count, Is.GreaterThanOrEqualTo(2),
                "Both Level 1 rooms should retain a return portal.");
            foreach (Transform exit in exits)
            {
                Transform portal = exit.Find("Portal");
                Transform backing = exit.Find("PortalBacking");
                Assert.That(portal, Is.Not.Null, exit.name + " is missing its portal surface.");
                Assert.That(backing, Is.Not.Null,
                    exit.name + " needs an opaque recess so wall trim cannot show through the portal.");
                Assert.That(portal.localPosition.z, Is.LessThan(backing.localPosition.z),
                    exit.name + " portal glass must sit in front of its opaque recess.");
                Assert.That(exit.Find("InnerTrim_Top"), Is.Not.Null,
                    exit.name + " needs a readable inner doorway silhouette.");
            }

            Transform taskDoor = GameObject.Find("LockedDoor_TASK_ROOM").transform;
            Assert.That(taskDoor.Find("Panel/PanelGlow"), Is.Not.Null,
                "The task-room door should read as a solid sliding door with a separate luminous face.");

            Transform knowledge = GameObject.Find("MfaFactor_Knowledge").transform;
            Transform knowledgeBody = knowledge.Find("ScreenBody");
            Transform knowledgeLabel = knowledge.Find("Label_PASSCODE");
            Assert.That(Array.Exists(knowledgeLabel.GetComponents<MonoBehaviour>(), component =>
                    component != null && component.GetType().FullName == "Cyverse.Level.Billboard"), Is.False,
                "PASSCODE should be mounted on its terminal, not float independently in world space.");
            Assert.That(Quaternion.Angle(knowledgeBody.localRotation, knowledgeLabel.localRotation),
                Is.LessThan(0.1f), "PASSCODE should follow the terminal screen pitch.");

            TextMesh[] auditLabels = GameObject.Find("AuditStation")
                .GetComponentsInChildren<TextMesh>(true);
            Assert.That(Array.Exists(auditLabels, label => label.text.Contains("PRESS [E]")), Is.True,
                "The idle audit display should explain how to open the otherwise-empty board.");
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Level2CyberDefenseBootstrap_HasSaneBoundsAndTaskTargets()
        {
            yield return AuditScene("Level2_CyberDefense");

            Assert.That(GameObject.Find("SiemConsole"), Is.Not.Null);
            Assert.That(GameObject.Find("PlaybookStation"), Is.Not.Null);
            Assert.That(GameObject.Find("CertExamStation"), Is.Not.Null);
            Assert.That(GameObject.Find("SOC_Polish"), Is.Not.Null);

            Type endpointType = FindType("Cyverse.Interaction.EndpointStation");
            Assert.That(endpointType, Is.Not.Null);
            UnityEngine.Object[] endpoints = UnityEngine.Object.FindObjectsOfType(endpointType);
            Assert.That(endpoints.Length, Is.EqualTo(4),
                "The bootstrap scene should expose exactly four active SOC workstations.");
            foreach (UnityEngine.Object endpoint in endpoints)
            {
                Component component = endpoint as Component;
                Assert.That(component, Is.Not.Null);
                Assert.That(component.gameObject.activeInHierarchy, Is.True);
                Assert.That(component.transform.position.x, Is.EqualTo(-17f).Within(0.1f));
                Assert.That(component.GetComponentInChildren<Renderer>(true), Is.Not.Null);
            }

            // The board and the IR playbook are both tall enough to target at
            // eye level; the board's trigger volume must remain non-blocking.
            AssertTriggerAimVolume("SiemConsole", 2.4f, 4.0f);
            GameObject staleEndpointSign = GameObject.Find("Sign_ENDPOINTS");
            Assert.That(staleEndpointSign == null || !staleEndpointSign.activeInHierarchy, Is.True,
                "Legacy ENDPOINTS signage must not float above the playbook after the bootstrap is rewired.");
            AssertQuietGridFloor("Level 2");

            GameObject siem = GameObject.Find("SiemConsole");
            GameObject alertHeaderObject = GameObject.Find("Label_ALERT_BOARD");
            GameObject alertBodyObject = GameObject.Find("Label_Active_SOC_scenario");
            GameObject playbookInstructionObject = GameObject.Find("PlaybookInstruction");
            GameObject playbookGuideObject = GameObject.Find("PlaybookSequenceGuide");
            GameObject certificationSignObject = GameObject.Find("Sign_CERTIFICATION");
            Assert.That(alertHeaderObject, Is.Not.Null, "Missing Label_ALERT_BOARD.");
            Assert.That(alertBodyObject, Is.Not.Null, "Missing Label_Active_SOC_scenario.");
            Assert.That(playbookInstructionObject, Is.Not.Null, "Missing normalized PlaybookInstruction.");
            Assert.That(playbookGuideObject, Is.Not.Null, "Missing PlaybookSequenceGuide.");
            Assert.That(certificationSignObject, Is.Not.Null, "Missing Sign_CERTIFICATION.");

            TextMesh alertHeader = alertHeaderObject.GetComponent<TextMesh>();
            TextMesh alertBody = alertBodyObject.GetComponent<TextMesh>();
            int activeTitleCopies = 0;
            foreach (TextMesh label in siem.GetComponentsInChildren<TextMesh>(true))
                if (label.text == alertBody.text) activeTitleCopies++;

            TextMesh playbookInstruction = playbookInstructionObject.GetComponent<TextMesh>();
            TextMesh playbookGuide = playbookGuideObject.GetComponent<TextMesh>();
            Transform certificationSign = certificationSignObject.transform;

            Assert.That(alertHeader.text, Does.StartWith("ALERT BOARD"),
                "The Alert Board heading must not be overwritten by the active scenario title.");
            Assert.That(activeTitleCopies, Is.EqualTo(1),
                "The active scenario title should appear once on the Alert Board body.");
            Assert.That(certificationSign.position.y, Is.GreaterThanOrEqualTo(3.6f),
                "The certification header should clear the horizontal wall-light strip.");
            Assert.That(playbookInstruction.characterSize, Is.GreaterThanOrEqualTo(0.030f),
                "The playbook instruction row is too small to read at its interaction distance.");
            Assert.That(playbookGuide.characterSize, Is.GreaterThanOrEqualTo(0.023f),
                "The playbook sequence guide is too small to read at its interaction distance.");
            Assert.That(Mathf.Abs(playbookInstruction.transform.position.y - playbookGuide.transform.position.y),
                Is.GreaterThanOrEqualTo(0.45f),
                "The playbook instruction and sequence guide need visibly separate rows.");
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Level3Forensics_HasSaneBoundsAndTaskTargets()
        {
            yield return AuditScene("Level3_Forensics");

            Assert.That(GameObject.Find("ForensicsConsole"), Is.Not.Null);
            Assert.That(GameObject.Find("EvidenceBoard"), Is.Not.Null);
            Assert.That(GameObject.Find("ChainOfCustodyStation"), Is.Not.Null);
            Assert.That(GameObject.Find("FORENSICS_LAB_POLISH"), Is.Not.Null);

            AssertTriggerAimVolume("ChainOfCustodyStation", 2.4f, 2.8f);
            GameObject console = GameObject.Find("ForensicsConsole");
            Assert.That(console.GetComponentInChildren<Renderer>(true), Is.Not.Null);
            GameObject reportMonitor = GameObject.Find("DF_ReportMonitor");
            Assert.That(reportMonitor, Is.Not.Null);
            Assert.That(reportMonitor.GetComponent<Renderer>(), Is.Not.Null);
            AssertQuietGridFloor("Level 3");

            Transform polish = GameObject.Find("FORENSICS_LAB_POLISH").transform;
            var workflowPaths = new List<Transform>();
            foreach (Transform child in polish)
                if (child.name.StartsWith("DF_Path_", StringComparison.Ordinal))
                    workflowPaths.Add(child);
            Assert.That(workflowPaths.Count, Is.GreaterThanOrEqualTo(3),
                "The forensics workflow should retain visible floor routing between stations.");
            foreach (Transform path in workflowPaths)
            {
                Vector3 direction = path.forward.normalized;
                float axisAlignment = Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.z));
                Assert.That(axisAlignment, Is.GreaterThanOrEqualTo(0.999f),
                    path.name + " cuts diagonally across the orthogonal floor grid.");
            }
        }

        private static IEnumerator AuditScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            // Allow Awake/Start self-healing passes, TMP generation, and one
            // world-text evaluation to settle before taking measurements.
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null, sceneName + " should have a main camera.");
            Assert.That(IsFinite(camera.transform.position), Is.True,
                sceneName + " camera position contains NaN/Infinity.");
            Assert.That(Mathf.Abs(camera.transform.position.x), Is.LessThan(RoomLimit));
            Assert.That(Mathf.Abs(camera.transform.position.z), Is.LessThan(RoomLimit));
            Assert.That(camera.transform.position.y, Is.InRange(0.5f, CeilingLimit));

            var findings = new List<string>();
            int rendererCount = 0;
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsOfType<Renderer>(true))
            {
                if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
                if (IsAtmosphericRenderer(renderer)) continue;
                rendererCount++;
                Bounds bounds = renderer.bounds;
                if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
                {
                    findings.Add(renderer.name + " has a non-finite render bound.");
                    continue;
                }

                // These are intentionally oversized visual helpers, not
                // gameplay props: the room-wide dust volume, billboarded
                // light glows, and ceiling fixture meshes extend beyond the
                // 40x40 shell by design. Keep the audit focused on accidental
                // authored transforms and interaction surfaces.
                if (IsIntentionalVisualHelper(renderer)) continue;

                // World-space props should live inside the shared 40x40 room
                // shell. A small margin allows wall thickness and glow quads,
                // but catches accidental parent transforms and floating props.
                if (bounds.min.x < -RoomLimit || bounds.max.x > RoomLimit ||
                    bounds.min.z < -RoomLimit || bounds.max.z > RoomLimit ||
                    bounds.min.y < FloorLimit || bounds.max.y > CeilingLimit)
                {
                    findings.Add($"{renderer.name} bounds {bounds} escape the room shell.");
                }
            }

            Assert.That(rendererCount, Is.GreaterThan(40),
                sceneName + " rendered too little geometry; a visual root may be missing.");
            Assert.That(findings, Is.Empty, sceneName + " visual audit findings:\n" +
                string.Join("\n", findings));

            Type managerType = FindType("Cyverse.Level.WorldTextLayoutManager");
            UnityEngine.Object manager = UnityEngine.Object.FindObjectOfType(managerType);
            if (manager != null)
            {
                managerType.GetMethod("RefreshNow").Invoke(manager, null);
                managerType.GetMethod("EvaluateNow").Invoke(manager, null);
                int overlapCount = (int)managerType.GetProperty("LastOverlapCount").GetValue(manager);
                Debug.Log($"[VISUAL AUDIT] {sceneName}: renderers={rendererCount}, " +
                    $"floating-text-overlaps={overlapCount}, camera={camera.transform.position}");
            }
        }

        private static void AssertTriggerAimVolume(string objectName, float expectedHeight,
            float expectedWidth)
        {
            GameObject target = GameObject.Find(objectName);
            Assert.That(target, Is.Not.Null);

            BoxCollider aim = target.GetComponent<BoxCollider>();
            Assert.That(aim, Is.Not.Null, objectName + " lost its eye-level aim volume.");
            Assert.That(aim.isTrigger, Is.True, objectName + " aim volume must not block movement.");
            Assert.That(aim.size.y, Is.GreaterThanOrEqualTo(expectedHeight - 0.05f));
            Assert.That(aim.size.x, Is.GreaterThanOrEqualTo(expectedWidth - 0.05f));
        }

        private static void AssertQuietGridFloor(string sceneLabel)
        {
            Renderer floor = GameObject.Find("Floor")?.GetComponent<Renderer>();
            Assert.That(floor, Is.Not.Null, sceneLabel + " should expose its grid floor renderer.");
            Material material = floor.sharedMaterial;
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Cyverse/GridFloor"));
            Assert.That(material.GetFloat("_PulseStrength"), Is.EqualTo(0f).Within(0.001f),
                sceneLabel + " floor pulse creates broad moving bands across the room.");
            Assert.That(material.GetFloat("_LineWidth"), Is.LessThanOrEqualTo(0.015f),
                sceneLabel + " major grid lines are too wide at an oblique camera angle.");
            Assert.That(material.GetFloat("_MinorEmission"), Is.LessThanOrEqualTo(0.08f),
                sceneLabel + " minor grid competes with the task stations and navigation rails.");
            Assert.That(material.GetFloat("_Emission"), Is.LessThanOrEqualTo(0.55f),
                sceneLabel + " grid emission is bright enough to resemble malformed floor geometry.");
        }

        private static bool IsAtmosphericRenderer(Renderer renderer)
        {
            // These are intentionally allowed to project beyond the room
            // shell: their bounds describe a glow/dust volume rather than a
            // solid prop that can visibly float through a wall.
            // The evidence locker's mirror room is a copy of the neighbouring
            // level built deliberately beyond the shared wall.
            if (renderer.transform.root.name == "LockerMirrorRoom" || renderer.name == "SightlineStandIn") return true;
            string name = renderer.name;
            if (name == "DustMotes" || name == "LightGlow" || name == "Glow" ||
                name.StartsWith("Lamp_Ceiling_", StringComparison.Ordinal))
                return true;

            // The visual-pass room shell is a parented blockout whose child
            // Plane bounds include the full wall thickness. It is the shell
            // used to contain the props, not a misplaced prop itself.
            return renderer.transform.root != null && renderer.transform.root.name == "Room";
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

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsIntentionalVisualHelper(Renderer renderer)
        {
            string name = renderer.name.ToUpperInvariant();
            return name.Contains("DUSTMOTE") || name.Contains("LIGHTGLOW") ||
                   name == "GLOW" || name.StartsWith("GLOW ") ||
                   name.Contains("LAMP_CEILING") || name == "PLANE" ||
                   name.StartsWith("PLANE ") || name == "CEILINGSLAB" ||
                   name.StartsWith("CEILINGPANEL_") || name.StartsWith("WALL_");
        }

    }
}
