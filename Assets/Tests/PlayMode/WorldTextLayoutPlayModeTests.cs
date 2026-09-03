using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Cyverse.Tests
{
    public class WorldTextLayoutPlayModeTests
    {
        [UnityTest]
        [Timeout(30000)]
        public IEnumerator RuntimeBuiltCarryable_PickupIgnoresDeferredDestroyedColliders()
        {
            SceneManager.LoadScene("Level2_CyberDefense_VisualPass", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Type carryableType = FindType("Cyverse.Interaction.Carryable");
            MethodInfo build = carryableType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static);
            var card = (Component)build.Invoke(null, new object[]
            {
                new Vector3(0f, 1f, -12f), "EDITOR PICKUP TEST",
                "editor_pickup_test", Color.cyan, false
            });

            // StripCollider uses Destroy during play. Wait until Unity has
            // actually invalidated the decorative BoxCollider, then pick up.
            yield return null;

            var interactor = new GameObject("PickupTestInteractor");
            Assert.DoesNotThrow(() => carryableType.GetMethod("Interact").Invoke(card,
                new object[] { interactor }));

            carryableType.GetMethod("ClearCarried", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            UnityEngine.Object.Destroy(card.gameObject);
            UnityEngine.Object.Destroy(interactor);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator VisualPass_NormalizesDepthAndDeconflictsFloatingSigns()
        {
            SceneManager.LoadScene("Level2_CyberDefense_VisualPass", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Type managerType = FindType("Cyverse.Level.WorldTextLayoutManager");
            object manager = UnityEngine.Object.FindObjectOfType(managerType);
            Assert.That(manager, Is.Not.Null, "VisualDirector should install the shared world-text manager.");
            managerType.GetMethod("RefreshNow").Invoke(manager, null);

            AssertPlaybookLayoutIsReadable();
            AssertSocWorkstationsStayOnTheirMonitorsAndClearTheAisle();
            AssertSocPolishBuildsColliderFreeTaskZones();
            AssertNextTaskCalloutIsProminent();

            Type signFxType = FindType("Cyverse.Level.SignFX");
            foreach (TextMesh text in UnityEngine.Object.FindObjectsOfType<TextMesh>(true))
            {
                if (!text.gameObject.scene.IsValid()) continue;
                Renderer renderer = text.GetComponent<Renderer>();
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Cyverse/WorldText"),
                    $"Legacy world text was not depth-normalized: {text.name}");

                bool floating = text.GetComponent(FindType("Cyverse.Level.Billboard")) != null ||
                                text.gameObject.name.StartsWith("Sign_");
                Behaviour fx = text.GetComponent(signFxType) as Behaviour;
                if (!floating && fx != null)
                    Assert.That(fx.enabled, Is.False,
                        $"Surface readout must not bob into its mesh: {text.name}");
            }

            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null);
            // Isolate the synthetic overlap pair from authored station labels.
            // The playbook guide intentionally reserves the center of the real
            // gameplay view, so test the manager in empty world space.
            camera.transform.SetPositionAndRotation(new Vector3(0f, 10f, -30f), Quaternion.identity);
            Vector3 position = camera.transform.position + camera.transform.forward * 6f;
            Type buildKitType = FindType("Cyverse.Level.BuildKit");
            MethodInfo makeSign = buildKitType.GetMethod("MakeSign");
            object[] firstArgs = { null, position, "OVERLAP PRIORITY", Color.cyan, 0.045f };
            object[] secondArgs = { null, position, "OVERLAP SECONDARY", Color.white, 0.045f };
            var first = (GameObject)makeSign.Invoke(null, firstArgs);
            var second = (GameObject)makeSign.Invoke(null, secondArgs);

            yield return null;
            yield return null;
            managerType.GetMethod("RefreshNow").Invoke(manager, null);
            managerType.GetMethod("EvaluateNow").Invoke(manager, null);

            Type tmpType = FindType("TMPro.TextMeshPro");
            Component firstText = first.GetComponent(tmpType);
            Component secondText = second.GetComponent(tmpType);
            Assert.That(firstText, Is.Not.Null, "New floating signs should use TMP.");
            Assert.That(secondText, Is.Not.Null, "New floating signs should use TMP.");
            float firstAlpha = ((Color)tmpType.GetProperty("color").GetValue(firstText)).a;
            float secondAlpha = ((Color)tmpType.GetProperty("color").GetValue(secondText)).a;
            int overlapCount = (int)managerType.GetProperty("LastOverlapCount").GetValue(manager);
            Vector2 firstVisibility = (Vector2)managerType.GetMethod("DebugVisibility").Invoke(manager,
                new object[] { firstText });
            Vector2 secondVisibility = (Vector2)managerType.GetMethod("DebugVisibility").Invoke(manager,
                new object[] { secondText });
            Debug.Log($"[WORLD TEXT TEST] overlaps={overlapCount} alpha={firstAlpha:F2}/{secondAlpha:F2} " +
                      $"visibility={firstVisibility}/{secondVisibility} " +
                      $"screen={Screen.width}x{Screen.height} " +
                      $"bounds={first.GetComponent<Renderer>().bounds}/{second.GetComponent<Renderer>().bounds} " +
                      $"viewport={camera.WorldToViewportPoint(first.transform.position)}/" +
                      $"{camera.WorldToViewportPoint(second.transform.position)}");
            Assert.That(Mathf.Min(firstAlpha, secondAlpha), Is.LessThan(0.5f),
                "One overlapping floating label should fade.");
            Assert.That(Mathf.Max(firstAlpha, secondAlpha), Is.GreaterThan(0.75f),
                "The higher-priority/nearer label should remain readable.");
            Assert.That(overlapCount, Is.GreaterThan(0));

            UnityEngine.Object.Destroy(first);
            UnityEngine.Object.Destroy(second);
        }

        private static void AssertPlaybookLayoutIsReadable()
        {
            Type playbookType = FindType("Cyverse.Interaction.PlaybookStation");
            object playbook = UnityEngine.Object.FindObjectOfType(playbookType);
            Assert.That(playbook, Is.Not.Null);
            playbookType.GetMethod("Configure").Invoke(playbook, null);
            Transform playbookTransform = ((Component)playbook).transform;

            Assert.That(playbookTransform.position.x, Is.EqualTo(17f).Within(0.05f),
                "The IR playbook should be mounted on the SOC room's right/east wall.");
            Assert.That(Vector3.Dot(playbookTransform.TransformDirection(Vector3.back), Vector3.left),
                Is.GreaterThan(0.95f),
                "The east-wall playbook should face west into the playable room.");

            Assert.That(GameObject.Find("PlaybookSequenceGuide"), Is.Not.Null,
                "The IR playbook should show the six-step sequence in-world.");

            Type dropZoneType = FindType("Cyverse.Interaction.DropZone");
            var slotXs = new System.Collections.Generic.List<float>();
            foreach (UnityEngine.Object candidate in UnityEngine.Object.FindObjectsOfType(dropZoneType))
            {
                string zoneName = (string)dropZoneType.GetField("zoneName").GetValue(candidate);
                if (zoneName.StartsWith("STEP "))
                {
                    Transform slot = ((Component)candidate).transform;
                    Vector3 local = playbookTransform.InverseTransformPoint(slot.position);
                    slotXs.Add(local.x);
                    Assert.That(local.z, Is.EqualTo(0f).Within(0.05f),
                        "Every playbook slot should stay aligned to its rotated board.");
                    Assert.That(Quaternion.Angle(slot.rotation, playbookTransform.rotation),
                        Is.LessThan(0.5f));
                }
            }
            slotXs.Sort();
            Assert.That(slotXs.Count, Is.EqualTo(6));
            for (int i = 1; i < slotXs.Count; i++)
                Assert.That(slotXs[i] - slotXs[i - 1], Is.GreaterThanOrEqualTo(2.2f),
                    "IR playbook slots should not be packed together.");

            Type carryableType = FindType("Cyverse.Interaction.Carryable");
            Transform rack = GameObject.Find("PlaybookRack").transform;
            Assert.That(Vector3.Distance(rack.position,
                playbookTransform.TransformPoint(new Vector3(0f, 0f, -5f))), Is.LessThan(0.05f));
            Assert.That(Quaternion.Angle(rack.rotation, playbookTransform.rotation), Is.LessThan(0.5f));
            var cardXs = new System.Collections.Generic.List<float>();
            foreach (UnityEngine.Object candidate in UnityEngine.Object.FindObjectsOfType(carryableType))
            {
                string id = (string)carryableType.GetField("id").GetValue(candidate);
                if (id.StartsWith("ir_"))
                {
                    Transform card = ((Component)candidate).transform;
                    Vector3 local = rack.InverseTransformPoint(card.position);
                    cardXs.Add(local.x);
                    Assert.That(local.z, Is.EqualTo(0f).Within(0.05f),
                        "Every shuffled card should stay aligned over the rotated rack.");
                    Assert.That(Quaternion.Angle(card.rotation, rack.rotation), Is.LessThan(0.5f));
                    if (id == "ir_detection")
                    {
                        TextMesh label = ((Component)candidate).GetComponentInChildren<TextMesh>(true);
                        Assert.That(label, Is.Not.Null);
                        Assert.That(label.transform.localPosition.x, Is.EqualTo(0f).Within(0.001f),
                            "The Detection label should remain centered over its card.");
                    }
                }
            }
            cardXs.Sort();
            Assert.That(cardXs.Count, Is.EqualTo(6));
            for (int i = 1; i < cardXs.Count; i++)
                Assert.That(cardXs[i] - cardXs[i - 1], Is.GreaterThanOrEqualTo(1.8f),
                    "IR response cards should be individually readable and selectable.");
        }

        private static void AssertNextTaskCalloutIsProminent()
        {
            GameObject panel = GameObject.Find("NextTaskPanel");
            Assert.That(panel, Is.Not.Null, "The HUD should give the current action a dedicated callout.");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Assert.That(panelRect, Is.Not.Null);
            Assert.That(panelRect.sizeDelta.x, Is.GreaterThanOrEqualTo(1000f));
            Assert.That(panelRect.sizeDelta.y, Is.GreaterThanOrEqualTo(80f));

            GameObject kicker = GameObject.Find("NextTaskLabel");
            Assert.That(kicker, Is.Not.Null);
            Text kickerText = kicker.GetComponent<Text>();
            Assert.That(kickerText, Is.Not.Null);
            StringAssert.Contains("NEXT TASK", kickerText.text);

            Text objective = GameObject.Find("Objective").GetComponent<Text>();
            Assert.That(objective.fontStyle, Is.EqualTo(FontStyle.Bold));
            Assert.That(objective.alignment, Is.EqualTo(TextAnchor.UpperLeft));
            Assert.That(objective.text.StartsWith("Objective:"), Is.False,
                "The callout label supplies the hierarchy, so the action should not repeat 'Objective'.");
        }

        private static void AssertSocWorkstationsStayOnTheirMonitorsAndClearTheAisle()
        {
            Type endpointType = FindType("Cyverse.Interaction.EndpointStation");
            var endpoints = new System.Collections.Generic.List<Component>();
            foreach (UnityEngine.Object candidate in UnityEngine.Object.FindObjectsOfType(endpointType))
            {
                var endpoint = (Component)candidate;
                if (endpoint.gameObject.activeInHierarchy) endpoints.Add(endpoint);
            }
            endpoints.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
            Assert.That(endpoints.Count, Is.EqualTo(4));

            foreach (Component endpoint in endpoints)
            {
                Assert.That(endpoint.transform.position.x, Is.EqualTo(-17f).Within(0.05f),
                    "SOC workstations should sit against the west wall, not block the center aisle.");
                Assert.That(Vector3.Dot(endpoint.transform.TransformDirection(Vector3.back), Vector3.right),
                    Is.GreaterThan(0.95f),
                    $"{endpoint.name} monitor should face into the room, not into the west wall.");

                TextMesh activity = null;
                TextMesh[] endpointLabels = endpoint.GetComponentsInChildren<TextMesh>(true);
                foreach (TextMesh label in endpointLabels)
                    if (Mathf.Abs(label.transform.localPosition.y - 1.33f) < 0.02f) activity = label;
                Assert.That(activity, Is.Not.Null,
                    $"{endpoint.name} should keep its current scenario text on the monitor. Found: " +
                    string.Join(" | ", System.Array.ConvertAll(endpointLabels,
                        label => $"{label.gameObject.name}='{label.text.Replace("\n", " / ")}'")));
                Assert.That(activity.text, Is.Not.Empty,
                    $"{endpoint.name} should show the current SOC scenario details.");

                Transform screen = endpoint.transform.Find("Screen");
                Assert.That(screen, Is.Not.Null);
                Assert.That(screen.localScale.x, Is.GreaterThanOrEqualTo(1.54f),
                    $"{endpoint.name} should use the enlarged SOC monitor.");
                Assert.That(screen.localScale.y, Is.GreaterThanOrEqualTo(0.85f),
                    $"{endpoint.name} should provide enough vertical space for three readable rows.");
                float allowedWidth = Mathf.Abs(screen.lossyScale.x) * 0.93f;
                Renderer renderer = activity.GetComponent<Renderer>();
                Vector3 axis = endpoint.transform.right;
                axis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
                float width = 2f * Vector3.Dot(renderer.bounds.extents, axis);
                Assert.That(width, Is.LessThanOrEqualTo(allowedWidth + 0.01f),
                    $"{endpoint.name} activity text should remain inside the monitor width.");
            }

            for (int i = 1; i < endpoints.Count; i++)
                Assert.That(endpoints[i].transform.position.z - endpoints[i - 1].transform.position.z,
                    Is.GreaterThanOrEqualTo(2.9f));
        }

        private static void AssertSocPolishBuildsColliderFreeTaskZones()
        {
            GameObject root = GameObject.Find("SOC_Polish");
            Assert.That(root, Is.Not.Null, "The Cyber Defense visual pass should install the SOC finish layer.");
            Assert.That(root.transform.Find("SOC_AisleInset"), Is.Not.Null,
                "The center aisle should have a restrained navigation inset.");

            for (int i = 1; i <= 4; i++)
            {
                Transform bay = root.transform.Find("SOC_WorkstationBay_" + i);
                Assert.That(bay, Is.Not.Null, $"Workstation bay {i} should have a finished wall surround.");
            }

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                Assert.That(collider == null || !collider.enabled, Is.True,
                    $"Decorative SOC polish must not add collision: {collider?.name}");
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
