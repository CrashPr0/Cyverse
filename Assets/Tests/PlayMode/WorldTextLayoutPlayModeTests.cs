using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Cyverse.Tests
{
    public class WorldTextLayoutPlayModeTests
    {
        [UnityTest]
        [Timeout(30000)]
        public IEnumerator RuntimeBuiltCarryable_PickupIgnoresDeferredDestroyedColliders()
        {
            SceneManager.LoadScene("Level2_CyberDefense", LoadSceneMode.Single);
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

            Type intentType = FindType("Cyverse.Level.WorldTextLayoutIntent");
            Component itemLabel = card.GetComponentInChildren<TextMesh>(true);
            Component itemIntent = itemLabel.GetComponent(intentType);
            Assert.That(itemIntent, Is.Not.Null,
                "Interaction labels should declare their layout behavior at creation time.");
            Assert.That(intentType.GetProperty("LayoutMode").GetValue(itemIntent).ToString(),
                Is.EqualTo("InteractionCritical"));

            // StripCollider uses Destroy during play. Wait until Unity has
            // actually invalidated the decorative BoxCollider, then pick up.
            yield return null;

            Assert.That(GameplayActionTestDriver.Interact(card), Is.True);

            carryableType.GetMethod("ClearCarried", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, null);
            UnityEngine.Object.Destroy(card.gameObject);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator BootstrapScene_NormalizesDepthAndDeconflictsFloatingSigns()
        {
            SceneManager.LoadScene("Level2_CyberDefense", LoadSceneMode.Single);
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
            Vector3 edgePosition = camera.ViewportToWorldPoint(new Vector3(0.995f, 0.5f, 6f));
            object[] edgeArgs = { null, edgePosition, "CLIPPED EDGE LABEL", Color.cyan, 0.045f };
            var edge = (GameObject)makeSign.Invoke(null, edgeArgs);

            yield return null;
            yield return null;
            managerType.GetMethod("RefreshNow").Invoke(manager, null);
            managerType.GetMethod("EvaluateNow").Invoke(manager, null);

            Type tmpType = FindType("TMPro.TextMeshPro");
            Component firstText = first.GetComponent(tmpType);
            Component secondText = second.GetComponent(tmpType);
            Component edgeText = edge.GetComponent(tmpType);
            Assert.That(firstText, Is.Not.Null, "New floating signs should use TMP.");
            Assert.That(secondText, Is.Not.Null, "New floating signs should use TMP.");
            Assert.That(edgeText, Is.Not.Null, "The edge-clipping fixture should use TMP.");
            Type intentType = FindType("Cyverse.Level.WorldTextLayoutIntent");
            Component signIntent = first.GetComponent(intentType);
            Assert.That(signIntent, Is.Not.Null,
                "New signs should explicitly declare their layout behavior.");
            Assert.That(intentType.GetProperty("LayoutMode").GetValue(signIntent).ToString(),
                Is.EqualTo("Floating"));
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
            float edgeAlpha = ((Color)tmpType.GetProperty("color").GetValue(edgeText)).a;
            Assert.That(edgeAlpha, Is.LessThan(0.5f),
                "A floating sign clipped by the viewport edge should fade until fully readable.");
            foreach (Renderer child in edge.GetComponentsInChildren<Renderer>(true))
                if (child.gameObject != edge)
                    Assert.That(child.enabled, Is.False,
                        "A hidden edge sign must also hide its underline/halo chrome.");

            UnityEngine.Object.Destroy(first);
            UnityEngine.Object.Destroy(second);
            UnityEngine.Object.Destroy(edge);
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

            GameObject staleEndpointSign = GameObject.Find("Sign_ENDPOINTS");
            Assert.That(staleEndpointSign == null || !staleEndpointSign.activeInHierarchy,
                Is.True,
                "The saved visual pass must not leave its obsolete ENDPOINTS sign over the IR playbook.");

            Transform title = playbookTransform.Find("Sign_IR_PLAYBOOK");
            Assert.That(title, Is.Not.Null);
            Behaviour titleBillboard = title.GetComponent(FindType("Cyverse.Level.Billboard")) as Behaviour;
            Assert.That(titleBillboard, Is.Not.Null);
            Assert.That(titleBillboard.enabled, Is.False,
                "The playbook title should stay mounted to its board instead of drifting or fading.");

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
                    TextMesh label = ((Component)candidate).GetComponentInChildren<TextMesh>(true);
                    Assert.That(label, Is.Not.Null);
                    float labelWidth = ProjectedSize(label.GetComponent<Renderer>().bounds,
                        rack.transform.right);
                    Assert.That(labelWidth, Is.LessThanOrEqualTo(1.50f),
                        $"{id} label should fit inside its response-card column.");
                    if (id == "ir_detection")
                    {
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

        private static float ProjectedSize(Bounds bounds, Vector3 axis)
        {
            axis = new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
            return 2f * Vector3.Dot(bounds.extents, axis);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator ForensicsWorkflowLabels_AreMountedAndFinite()
        {
            SceneManager.LoadScene("Level3_Forensics", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            GameObject root = GameObject.Find("FORENSICS_LAB_POLISH");
            Assert.That(root, Is.Not.Null,
                "The Forensics Lab should install its workflow dressing at runtime.");

            string[] required =
            {
                "DF_WorkflowHeader", "DF_IntakeStatus", "DF_CorrelationLabel",
                "DF_ReportHeader", "DF_ReportStatus"
            };
            foreach (string name in required)
            {
                Transform label = root.transform.Find(name);
                Assert.That(label, Is.Not.Null, $"Missing Forensics workflow label: {name}");
                Assert.That(label.gameObject.activeInHierarchy, Is.True,
                    $"Forensics workflow label is hidden: {name}");

                Type tmpType = FindType("TMPro.TMP_Text");
                Component text = label.GetComponent(tmpType);
                Assert.That(text, Is.Not.Null, $"Forensics workflow label must use TMP: {name}");
                bool wrapping = (bool)tmpType.GetProperty("enableWordWrapping").GetValue(text);
                Assert.That(wrapping, Is.False,
                    $"Forensics workflow label must stay on one controlled line: {name}");
                object overflow = tmpType.GetProperty("overflowMode").GetValue(text);
                Assert.That(overflow.ToString(), Is.EqualTo("Ellipsis"),
                    $"Forensics workflow label must ellipsize instead of spilling into another mesh: {name}");
                Bounds bounds = label.GetComponent<Renderer>().bounds;
                Assert.That(float.IsNaN(bounds.center.x) || float.IsInfinity(bounds.center.x), Is.False,
                    $"Forensics workflow label has invalid X bounds: {name}");
                Assert.That(float.IsNaN(bounds.center.y) || float.IsInfinity(bounds.center.y), Is.False,
                    $"Forensics workflow label has invalid Y bounds: {name}");
                Assert.That(float.IsNaN(bounds.center.z) || float.IsInfinity(bounds.center.z), Is.False,
                    $"Forensics workflow label has invalid Z bounds: {name}");
            }

            Transform analysis = root.transform.Find("DF_AnalysisPad");
            Transform intake = root.transform.Find("DF_AcquisitionPad");
            Transform report = root.transform.Find("DF_ReportingPad");
            Assert.That(analysis, Is.Not.Null);
            Assert.That(intake, Is.Not.Null);
            Assert.That(report, Is.Not.Null);
            Assert.That(Vector3.Distance(intake.position, analysis.position), Is.GreaterThan(2.5f));
            Assert.That(Vector3.Distance(analysis.position, report.position), Is.GreaterThan(2.5f));
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator ForensicsDiegeticScreens_KeepTextInPositiveNonOverlappingRects()
        {
            SceneManager.LoadScene("Level3_Forensics", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Type screenType = FindType("Cyverse.Interaction.DiegeticScreen");
            UnityEngine.Object[] screens = UnityEngine.Object.FindObjectsOfType(screenType);
            // Custody intake, the centre terminal, and the console's left
            // (acquisition) and right (copied image) monitors.
            Assert.That(screens.Length, Is.EqualTo(4),
                "Level 3 should build one isolated diegetic screen per workflow display.");

            TMP_Text custodyStatus = GameObject.Find("CustodyStatus")?.GetComponent<TMP_Text>();
            Assert.That(custodyStatus, Is.Not.Null,
                "The evidence-intake screen should expose its custody progress row.");
            StringAssert.StartsWith("0/4", custodyStatus.text,
                "The intake screen must show the four required custody fields before the form is opened.");

            var canvasPositions = new System.Collections.Generic.List<Vector3>();
            foreach (UnityEngine.Object candidate in screens)
            {
                var screen = (Component)candidate;
                var canvas = (RectTransform)screenType.GetProperty("CanvasRoot").GetValue(screen);
                Assert.That(canvas, Is.Not.Null, $"{screen.name} should expose its offscreen canvas.");
                canvasPositions.Add(canvas.position);

                var surface = (Transform)screenType.GetProperty("ScreenTransform").GetValue(screen);
                var texture = (RenderTexture)screenType.GetProperty("Texture").GetValue(screen);
                Assert.That(surface, Is.Not.Null, $"{screen.name} should expose its physical display surface.");
                Assert.That(texture, Is.Not.Null, $"{screen.name} should expose its render texture.");
                float surfaceAspect = Mathf.Abs(surface.lossyScale.x / surface.lossyScale.y);
                float textureAspect = (float)texture.width / texture.height;
                Assert.That(textureAspect, Is.EqualTo(surfaceAspect).Within(0.05f),
                    $"{screen.name} render texture should match its physical aspect ratio so text is not stretched.");

                foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.ForceMeshUpdate();
                    Rect rect = text.rectTransform.rect;
                    StringAssert.DoesNotContain("✓", text.text,
                        $"{screen.name}/{text.name} uses a glyph missing from the runtime TMP font.");
                    Assert.That(rect.width, Is.GreaterThan(0f),
                        $"{screen.name}/{text.name} has a non-positive text width and will render as a vertical letter stack.");
                    Assert.That(rect.height, Is.GreaterThan(0f),
                        $"{screen.name}/{text.name} has a non-positive text height.");
                }

                foreach (Text text in canvas.GetComponentsInChildren<Text>(true))
                {
                    Rect rect = text.rectTransform.rect;
                    Assert.That(rect.width, Is.GreaterThan(0f),
                        $"{screen.name}/{text.name} has a non-positive text width.");
                    Assert.That(rect.height, Is.GreaterThan(0f),
                        $"{screen.name}/{text.name} has a non-positive text height.");
                }

                if (screen.name != "DF_UploadScreen") continue;
                RectTransform title = canvas.Find("UploadTitle") as RectTransform;
                RectTransform percent = canvas.Find("UploadPercent") as RectTransform;
                Assert.That(title, Is.Not.Null);
                Assert.That(percent, Is.Not.Null);
                Rect titleRect = RelativeRect(canvas, title);
                Rect percentRect = RelativeRect(canvas, percent);
                Assert.That(titleRect.Overlaps(percentRect), Is.False,
                    "The acquisition title and progress percentage need separate rows when upload text is populated.");
            }

            for (int i = 0; i < canvasPositions.Count; i++)
            for (int j = i + 1; j < canvasPositions.Count; j++)
                Assert.That(Vector3.Distance(canvasPositions[i], canvasPositions[j]), Is.GreaterThan(10f),
                    "Each diegetic render camera must frame only its own canvas; shared origins composite all station text.");

            // Evidence acquisition is the step after custody: it lives on the
            // Investigation Desk's LEFT monitor, not the report desk.
            GameObject console = GameObject.Find("ForensicsConsole");
            GameObject uploadScreen = GameObject.Find("DF_UploadScreen");
            Assert.That(console, Is.Not.Null);
            Assert.That(uploadScreen, Is.Not.Null);
            Assert.That(uploadScreen.transform.parent, Is.EqualTo(console.transform),
                "The acquisition screen should be mounted on the investigation console.");
            Assert.That(console.transform.InverseTransformPoint(uploadScreen.transform.position).x, Is.LessThan(-0.5f),
                "The acquisition screen should replace the console's LEFT monitor.");
            Assert.That(console.transform.Find("MonScreen_-1") == null ||
                !console.transform.Find("MonScreen_-1").GetComponent<Renderer>().enabled, Is.True,
                "The static left monitor quad must not z-fight the acquisition screen.");

            // The RIGHT monitor holds the working copy: blank until imaging ends.
            GameObject imageScreen = GameObject.Find("DF_ImageScreen");
            Assert.That(imageScreen, Is.Not.Null, "The console's right monitor should become the copied-image screen.");
            Assert.That(console.transform.InverseTransformPoint(imageScreen.transform.position).x, Is.GreaterThan(0.5f),
                "The copied-image screen should replace the console's RIGHT monitor.");
            GameObject imageTitle = GameObject.Find("ImageTitle");
            Assert.That(imageTitle, Is.Not.Null);
            Assert.That(imageTitle.GetComponent<TMP_Text>().text, Is.Empty,
                "The right monitor stays empty until the acquisition finishes.");

            Type plugInType = FindType("Cyverse.Interaction.PlugInStation");
            object plugIn = plugInType.GetProperty("Instance").GetValue(null);
            Assert.That(plugIn, Is.Not.Null);
            Assert.That(GameObject.Find("DF_EvidencePhone"), Is.Null,
                "The evidence phone should stay at intake until custody is accepted.");
            plugInType.GetMethod("CompleteNow").Invoke(plugIn, null);
            yield return null;
            Assert.That(imageTitle.GetComponent<TMP_Text>().text, Is.EqualTo("IMAGE COPIED"),
                "Once acquisition finishes the right monitor shows the copied image.");

            GameObject evidencePhone = GameObject.Find("DF_EvidencePhone");
            Assert.That(evidencePhone, Is.Not.Null, "Completing acquisition should leave the phone docked.");
            Bounds phoneBounds = default;
            bool hasPhoneBounds = false;
            foreach (Renderer part in evidencePhone.GetComponentsInChildren<Renderer>())
            {
                if (!hasPhoneBounds) { phoneBounds = part.bounds; hasPhoneBounds = true; }
                else phoneBounds.Encapsulate(part.bounds);
            }
            Renderer uploadSurface = uploadScreen.transform.Find("Screen").GetComponent<Renderer>();
            Assert.That(phoneBounds.max.y, Is.LessThan(uploadSurface.bounds.min.y),
                "The docked phone must sit below the acquisition screen instead of covering its rows.");
            float deskTop = console.transform.position.y + 1.0f;
            Assert.That(phoneBounds.min.y, Is.GreaterThanOrEqualTo(deskTop - 0.005f),
                "The docked phone should rest in its cradle on the desk, not sink into it.");
            TMP_Text uploadTitle = FindCanvasText(uploadScreen, screenType, "UploadTitle");
            Assert.That(uploadTitle, Is.Not.Null);
            StringAssert.Contains("COMPLETE", uploadTitle.text);

            Renderer custodySurface = GameObject.Find("CustodyReadoutScreen")
                .transform.Find("Screen").GetComponent<Renderer>();
            Renderer intakeCaption = GameObject.Find("DF_IntakeStatus").GetComponent<Renderer>();
            Assert.That(intakeCaption.bounds.max.y, Is.LessThan(custodySurface.bounds.min.y),
                "The legacy intake caption must stay below the diegetic custody screen instead of drawing through it.");

            // With the upload gone, the casework status is back on the report monitor.
            Renderer reportSurface = GameObject.Find("DF_ReportScreen").GetComponent<Renderer>();
            Renderer reportCaption = GameObject.Find("DF_ReportStatus").GetComponent<Renderer>();
            Assert.That(reportCaption.bounds.min.y, Is.GreaterThan(reportSurface.bounds.min.y),
                "The casework status should sit on the report screen.");
            Assert.That(reportCaption.bounds.max.y, Is.LessThan(reportSurface.bounds.max.y),
                "The casework status should sit on the report screen.");
            Assert.That(reportCaption.transform.position.z, Is.LessThan(reportSurface.bounds.min.z - 0.005f),
                "The casework status must sit in front of the report screen instead of depth-fighting through it.");
        }

        private static TMP_Text FindCanvasText(GameObject screenObject, Type screenType, string name)
        {
            Component screen = screenObject.GetComponent(screenType);
            var canvas = (RectTransform)screenType.GetProperty("CanvasRoot").GetValue(screen);
            Transform child = canvas != null ? canvas.Find(name) : null;
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static Rect RelativeRect(RectTransform root, RectTransform child)
        {
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, child);
            return Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
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

                TextMesh hostname = null;
                TextMesh activity = null;
                TextMesh status = null;
                TextMesh[] endpointLabels = endpoint.GetComponentsInChildren<TextMesh>(true);
                foreach (TextMesh label in endpointLabels)
                {
                    if (Mathf.Abs(label.transform.localPosition.y - 1.62f) < 0.02f) hostname = label;
                    else if (Mathf.Abs(label.transform.localPosition.y - 1.33f) < 0.02f) activity = label;
                    else if (Mathf.Abs(label.transform.localPosition.y - 1.05f) < 0.02f) status = label;
                }
                Assert.That(hostname, Is.Not.Null, $"{endpoint.name} should retain its hostname.");
                Assert.That(activity, Is.Not.Null,
                    $"{endpoint.name} should keep its current scenario text on the monitor. Found: " +
                    string.Join(" | ", System.Array.ConvertAll(endpointLabels,
                        label => $"{label.gameObject.name}='{label.text.Replace("\n", " / ")}'")));
                Assert.That(status, Is.Not.Null, $"{endpoint.name} should retain its status line.");
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

                Renderer screenRenderer = screen.GetComponent<Renderer>();
                AssertTextInsideScreen(endpoint, hostname, screenRenderer);
                AssertTextInsideScreen(endpoint, activity, screenRenderer);
                AssertTextInsideScreen(endpoint, status, screenRenderer);
                AssertSeparatedAlongAxis(endpoint, hostname, activity);
                AssertSeparatedAlongAxis(endpoint, activity, status);
            }

            for (int i = 1; i < endpoints.Count; i++)
                Assert.That(endpoints[i].transform.position.z - endpoints[i - 1].transform.position.z,
                    Is.GreaterThanOrEqualTo(2.9f));
        }

        private static void AssertTextInsideScreen(Component endpoint, TextMesh text,
            Renderer screen)
        {
            Vector3 up = endpoint.transform.up.normalized;
            Renderer textRenderer = text.GetComponent<Renderer>();
            float screenHalfHeight = ProjectedSize(screen.bounds, up) * 0.5f;
            float textHalfHeight = ProjectedSize(textRenderer.bounds, up) * 0.5f;
            float centerOffset = Mathf.Abs(Vector3.Dot(
                textRenderer.bounds.center - screen.bounds.center, up));
            Assert.That(centerOffset + textHalfHeight,
                Is.LessThanOrEqualTo(screenHalfHeight * 0.98f + 0.01f),
                $"{endpoint.name} {text.gameObject.name} should remain inside the monitor height.");
        }

        private static void AssertSeparatedAlongAxis(Component endpoint, TextMesh upper,
            TextMesh lower)
        {
            Vector3 up = endpoint.transform.up.normalized;
            Renderer upperRenderer = upper.GetComponent<Renderer>();
            Renderer lowerRenderer = lower.GetComponent<Renderer>();
            float centerDistance = Mathf.Abs(Vector3.Dot(
                upperRenderer.bounds.center - lowerRenderer.bounds.center, up));
            float requiredDistance =
                (ProjectedSize(upperRenderer.bounds, up) +
                 ProjectedSize(lowerRenderer.bounds, up)) * 0.5f;
            Assert.That(centerDistance, Is.GreaterThanOrEqualTo(requiredDistance - 0.005f),
                $"{endpoint.name} monitor rows {upper.gameObject.name} and " +
                $"{lower.gameObject.name} should not overlap vertically.");
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
