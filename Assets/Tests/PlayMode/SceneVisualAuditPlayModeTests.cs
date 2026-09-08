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
    /// Cheap scene-wide visual smoke checks for the hand-authored visual-pass
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
        public IEnumerator Level0VisualPass_HasSaneBoundsAndInteractionTargets()
        {
            yield return AuditScene("Level0 Visual Pass");

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
        public IEnumerator Level1IamVisualPass_HasSaneBoundsAndInteractionTargets()
        {
            yield return AuditScene("Level1_IAM_VisualPass");

            Assert.That(GameObject.Find("BriefingScreen"), Is.Not.Null);
            Assert.That(GameObject.Find("BadgeStation"), Is.Not.Null);
            Assert.That(GameObject.Find("MfaVault"), Is.Not.Null);
            Assert.That(GameObject.Find("SortingStation"), Is.Not.Null);
            Assert.That(GameObject.Find("AuditStation"), Is.Not.Null);
            Assert.That(GameObject.Find("CertExamStation"), Is.Not.Null);

            // These are the player-facing targets, not decorative screen
            // meshes. Their presence is what keeps the visual pass playable.
            AssertTriggerAimVolume("BriefingScreen", 3.4f, 4.6f);
            AssertTriggerAimVolume("BadgeStation", 3.0f, 1.8f);
            AssertTriggerAimVolume("AuditStation", 3.4f, 4.8f);
            AssertTriggerAimVolume("CertExamStation", 3.0f, 2.6f);

            // These are task containers rather than IInteractable targets;
            // their child factors/drop zones own the actual interaction
            // colliders. Keep the audit honest about that wiring too.
            Assert.That(GameObject.Find("DropZone_TOKEN_SLOT"), Is.Not.Null);
            Assert.That(GameObject.Find("DropZone_INTERN"), Is.Not.Null);
            Assert.That(GameObject.Find("DropZone_HR_MANAGER"), Is.Not.Null);
            Assert.That(GameObject.Find("DropZone_SYSADMIN"), Is.Not.Null);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator Level2CyberDefenseVisualPass_HasSaneBoundsAndTaskTargets()
        {
            yield return AuditScene("Level2_CyberDefense_VisualPass");

            Assert.That(GameObject.Find("SiemConsole"), Is.Not.Null);
            Assert.That(GameObject.Find("PlaybookStation"), Is.Not.Null);
            Assert.That(GameObject.Find("CertExamStation"), Is.Not.Null);
            Assert.That(GameObject.Find("SOC_Polish"), Is.Not.Null);

            Type endpointType = FindType("Cyverse.Interaction.EndpointStation");
            Assert.That(endpointType, Is.Not.Null);
            UnityEngine.Object[] endpoints = UnityEngine.Object.FindObjectsOfType(endpointType);
            Assert.That(endpoints.Length, Is.EqualTo(4),
                "The visual pass should expose exactly four active SOC workstations.");
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
                "Legacy ENDPOINTS signage must not float above the playbook after the visual pass is rewired.");
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

        private static bool IsAtmosphericRenderer(Renderer renderer)
        {
            // These are intentionally allowed to project beyond the room
            // shell: their bounds describe a glow/dust volume rather than a
            // solid prop that can visibly float through a wall.
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
                   name.StartsWith("PLANE ");
        }

    }
}
