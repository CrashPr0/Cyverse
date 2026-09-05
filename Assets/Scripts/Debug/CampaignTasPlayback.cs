#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cyverse.Core;
using Cyverse.Forensics;
using Cyverse.Interaction;
using Cyverse.Level;
using Cyverse.Player;
using Cyverse.UI;

namespace Cyverse.Testing
{
    /// <summary>Watchable campaign route: password, Hub, and every completed
    /// story level through Digital Forensics.</summary>
    public sealed class CampaignTasPlayback : MonoBehaviour
    {
        public bool Finished { get; private set; }
        public bool Passed { get; private set; }
        public string Failure { get; private set; }

        private readonly bool[] hadKey = new bool[4];
        private readonly int[] savedProgress = new int[4];
        private readonly string[] socKeys =
        {
            SocProgress.CompromisedComputerKey,
            SocProgress.ChainOfCustodyKey,
            SocProgress.PlaybookKey,
        };
        private readonly bool[] hadSocKey = new bool[3];
        private readonly int[] savedSocProgress = new int[3];
        private bool hadEvidence;
        private string savedEvidence;
        private Canvas overlayCanvas;
        private TextMeshProUGUI keysText;
        private TextMeshProUGUI actionText;
        private bool lastMoveSucceeded;

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            for (int i = 0; i < 4; i++)
            {
                string key = "cv_done_" + i;
                hadKey[i] = PlayerPrefs.HasKey(key);
                savedProgress[i] = PlayerPrefs.GetInt(key, 0);
            }
            for (int i = 0; i < socKeys.Length; i++)
            {
                hadSocKey[i] = PlayerPrefs.HasKey(socKeys[i]);
                savedSocProgress[i] = PlayerPrefs.GetInt(socKeys[i], 0);
            }
            hadEvidence = PlayerPrefs.HasKey(SocProgress.EvidenceJsonKey);
            savedEvidence = PlayerPrefs.GetString(SocProgress.EvidenceJsonKey, "");
            SocProgress.ClearForAutomation();
            BuildOverlay();

            yield return WaitForScene("PasswordLock", 8f);
            var password = FindObjectOfType<PasswordLockController>();
            if (password == null) { Fail("Password terminal was not found."); yield break; }
            Show("TYPE  •••••••••••", "Enter configured access credential");
            yield return new WaitForSecondsRealtime(1.4f);
            Show("ENTER", "Submit password and open the Hub gate");
            password.SubmitForAutomation();
            yield return WaitForScene("Hub", 8f);

            yield return EnterLevelFromHub(1, "LEVEL 1 — I/AM");
            var level1 = gameObject.AddComponent<Level1TasPlayback>();
            level1.restoreProgressOnFinish = false;
            level1.travelSpeed = 11f;
            level1.actionPause = 0.45f;
            HideOverlay(true);
            while (!level1.Finished) yield return null;
            HideOverlay(false);
            if (!level1.Passed) { Fail(level1.Failure); yield break; }
            yield return ReturnToHub("H", "Return to Hub after Level 1");

            yield return EnterLevelFromHub(2, "LEVEL 2 — CYBER DEFENSE");
            yield return RunLevel2();
            if (!string.IsNullOrEmpty(Failure)) yield break;
            yield return ReturnToHub("H", "Return to Hub after Level 2");

            yield return EnterLevelFromHub(3, "LEVEL 3 — DIGITAL FORENSICS");
            yield return RunLevel3();
            if (!string.IsNullOrEmpty(Failure)) yield break;

            Passed = true;
            Show("CAMPAIGN TAS COMPLETE", "Password → Hub → Levels 1–3 passed");
            Debug.Log("[CAMPAIGN TAS] PASS — completed through Digital Forensics");
            yield return new WaitForSecondsRealtime(3f);
            RestoreProgress();
            Finished = true;
        }

        private IEnumerator EnterLevelFromHub(int level, string label)
        {
            yield return WaitForScene("Hub", 8f);
            HubDoor door = null;
            foreach (var candidate in FindObjectsOfType<HubDoor>())
                if (candidate.mode == HubDoor.Mode.LevelGate && candidate.levelIndex == level)
                { door = candidate; break; }
            if (door == null) { Fail("Hub door for level " + level + " was not found."); yield break; }
            yield return MovePlayerTo(door.transform, 2.2f, "W", "Walk through Hub to " + label);
            Show("E", "Enter " + label);
            yield return new WaitForSecondsRealtime(0.8f);
            string before = SceneManager.GetActiveScene().name;
            door.Interact(gameObject);
            yield return WaitForDifferentScene(before, 8f);
        }

        private IEnumerator ReturnToHub(string keys, string action)
        {
            Show(keys, action);
            yield return new WaitForSecondsRealtime(1f);
            HubDoor exit = null;
            foreach (var door in FindObjectsOfType<HubDoor>())
                if (door.sceneName == "Hub" || SceneCatalog.Preferred(door.sceneName) == "Hub")
                { exit = door; break; }
            if (exit == null) { Fail("Return-to-Hub door was not found."); yield break; }
            string before = SceneManager.GetActiveScene().name;
            exit.SetUnlocked(true);
            exit.Interact(gameObject);
            yield return WaitForDifferentScene(before, 8f);
        }

        private IEnumerator RunLevel2()
        {
            yield return null; yield return null;
            var manager = FindObjectOfType<Level2Manager>();
            var briefing = FindObjectOfType<VideoStation>();
            var siem = FindObjectOfType<SiemConsole>();
            var playbook = FindObjectOfType<PlaybookStation>();
            var exam = FindObjectOfType<CertExamStation>();
            if (manager == null || briefing == null || siem == null || playbook == null || exam == null)
            { Fail("Level 2 is missing a required station."); yield break; }

            yield return MovePlayerTo(briefing.transform, 3f, "W", "Walk to Cyber Defense briefing");
            Show("E  → (HOLD)", "Play and scrub defense briefing");
            yield return new WaitForSecondsRealtime(1.1f);
            briefing.CompleteForAutomation();

            float playerY = CurrentPlayerY();
            yield return MovePlayerToPoint(new Vector3(4f, playerY, -8.5f),
                new Vector3(4f, 1.4f, 0f), "D  W", "Step around the briefing screen");
            if (!lastMoveSucceeded) yield break;
            yield return MovePlayerToPoint(new Vector3(0f, playerY, 0f),
                new Vector3(0f, 1.4f, 2f), "W", "Approach the unlocked SOC doorway");
            if (!lastMoveSucceeded) yield break;
            yield return MovePlayerToPoint(new Vector3(0f, playerY, 4.5f),
                new Vector3(0f, 1.4f, 8f), "W", "Walk through the SOC doorway");
            if (!lastMoveSucceeded) yield break;

            yield return MovePlayerTo(siem.transform, 2.6f, "W", "Walk to the SOC Alert Board");
            if (!lastMoveSucceeded) yield break;
            Show("E  ↑  ↓  1  2", "Flag rows and verify all three SOC scenarios");
            yield return new WaitForSecondsRealtime(1.2f);
            siem.CompleteForAutomation();
            if (!SocProgress.HasCompromisedComputer || !SocProgress.HasChainOfCustody ||
                !SocProgress.TryGetEvidence(out var evidence) || evidence.computer != "WS-03")
            { Fail("SOC investigation did not produce the structured WS-03 evidence handoff."); yield break; }

            while (!playbook.IsComplete)
            {
                Carryable chosen = null;
                DropZone slot = null;
                foreach (var item in FindObjectsOfType<Carryable>())
                {
                    if (!item.id.StartsWith("ir_")) continue;
                    foreach (var zone in FindObjectsOfType<DropZone>())
                        if (zone.accepts != null && zone.accepts(item)) { chosen = item; slot = zone; break; }
                    if (chosen != null) break;
                }
                if (chosen == null || slot == null) { Fail("IR playbook wiring stalled."); yield break; }
                float routeY = CurrentPlayerY();
                yield return MovePlayerToPoint(new Vector3(6f, routeY, 14f),
                    new Vector3(6f, 1.4f, 10f), "D  S", "Return to the playbook aisle");
                if (!lastMoveSucceeded) yield break;
                yield return MovePlayerToPoint(new Vector3(6f, routeY, 9.8f),
                    new Vector3(-1f, 1.4f, 10f), "S", "Walk around the response-card rack");
                if (!lastMoveSucceeded) yield break;
                yield return MovePlayerToPoint(new Vector3(chosen.transform.position.x, routeY, 9.8f),
                    chosen.transform.position + Vector3.up, "W  A", "Approach the response-card rack from the aisle");
                if (!lastMoveSucceeded) yield break;
                yield return MovePlayerTo(chosen.transform, 1.6f, "W", "Walk to response card");
                if (!lastMoveSucceeded) yield break;
                Show("E", "Pick up " + chosen.itemName);
                yield return new WaitForSecondsRealtime(0.4f);
                chosen.Interact(gameObject);
                yield return MovePlayerToPoint(new Vector3(6f, routeY, 9.8f),
                    new Vector3(6f, 1.4f, 14f), "D", "Carry card around the rack");
                if (!lastMoveSucceeded) yield break;
                yield return MovePlayerToPoint(new Vector3(6f, routeY, 14f),
                    slot.transform.position + Vector3.up, "W", "Enter the playbook aisle");
                if (!lastMoveSucceeded) yield break;
                yield return MovePlayerTo(slot.transform, 1.6f, "W", "Carry card to next playbook slot");
                if (!lastMoveSucceeded) yield break;
                Show("E", "Place next response step");
                yield return new WaitForSecondsRealtime(0.4f);
                slot.Interact(gameObject);
                yield return null;
            }

            float deadline = Time.realtimeSinceStartup + 3f;
            while (manager.CurrentPhase != Level2Manager.Phase.Exam && Time.realtimeSinceStartup < deadline) yield return null;
            yield return MovePlayerTo(exam.transform, 2f, "W", "Walk to Level 2 certification exam");
            Show("E  1  2  3", "Complete certification questions");
            yield return new WaitForSecondsRealtime(1.2f);
            exam.CompleteForAutomation();
            yield return null;
            if (manager.CurrentPhase != Level2Manager.Phase.Complete) Fail("Level 2 did not complete.");
        }

        private IEnumerator RunLevel3()
        {
            yield return null; yield return null;
            var manager = FindObjectOfType<Level3ForensicsManager>();
            var briefing = FindObjectOfType<VideoStation>();
            var console = FindObjectOfType<ForensicsConsole>();
            var custody = FindObjectOfType<ChainOfCustodyStation>();
            var custodyForm = FindObjectOfType<ChainOfCustodyForm>();
            if (manager == null || briefing == null || console == null || custody == null || custodyForm == null)
            { Fail("Level 3 is missing its briefing, custody intake, or investigation desk."); yield break; }

            yield return MovePlayerTo(briefing.transform, 3f, "W", "Walk to analyst briefing");
            Show("E  → (HOLD)", "Play and scrub analyst briefing");
            yield return new WaitForSecondsRealtime(1.1f);
            briefing.CompleteForAutomation();
            yield return new WaitForSecondsRealtime(0.4f);

            // The desk is diagonally across the task room. A straight line
            // intersects the solid half of the divider, so route through the
            // actual three-metre doorway before approaching the console.
            float playerY = CurrentPlayerY();
            yield return MovePlayerToPoint(new Vector3(4f, playerY, -8.5f),
                new Vector3(4f, 1.4f, 0f), "D  W", "Step around the briefing screen");
            if (!lastMoveSucceeded) yield break;
            yield return MovePlayerToPoint(new Vector3(0f, playerY, 0f),
                new Vector3(0f, 1.4f, 2f), "W", "Approach the unlocked SOC doorway");
            if (!lastMoveSucceeded) yield break;
            yield return MovePlayerToPoint(new Vector3(0f, playerY, 4.5f),
                new Vector3(0f, 1.4f, 8f), "W", "Walk through the SOC doorway");
            if (!lastMoveSucceeded) yield break;
            yield return MovePlayerTo(custody.transform, 2.2f, "A  W", "Walk to Evidence Intake");
            if (!lastMoveSucceeded) yield break;
            Show("E  CLICK ×4", "Complete chain-of-custody form");
            yield return new WaitForSecondsRealtime(0.8f);
            custodyForm.CompleteForAutomation();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return MovePlayerTo(console.transform, 2.2f, "W", "Walk to Investigation Desk");
            if (!lastMoveSucceeded) yield break;
            Show("E", "Open forensic query terminal");
            yield return new WaitForSecondsRealtime(0.8f);
            Show("TYPE QUERY  •  ENTER", "Solve both Digital Forensics cases");
            yield return new WaitForSecondsRealtime(1.8f);
            console.CompleteForAutomation();
            yield return null; yield return null;
            if (manager.CurrentPhase != Level3ForensicsManager.Phase.Complete)
                Fail("Digital Forensics did not reach completion.");
        }

        private IEnumerator MovePlayerTo(Transform target, float stopDistance, string keys, string action)
        {
            var controller = FindObjectOfType<FirstPersonController>();
            var body = controller != null ? controller.transform : null;
            if (body == null)
            {
                lastMoveSucceeded = false;
                Fail("No first-person player was available while trying to " + action + ".");
                yield break;
            }
            Vector3 away = body.position - target.position; away.y = 0f;
            if (away.sqrMagnitude < 0.1f) away = -target.forward;
            Vector3 destination = target.position + away.normalized * stopDistance;
            destination.y = body.position.y;
            yield return MovePlayerToPoint(destination, target.position + Vector3.up * 1.4f,
                keys, action);
        }

        private IEnumerator MovePlayerToPoint(Vector3 destination, Vector3 lookAt,
            string keys, string action)
        {
            lastMoveSucceeded = false;
            Show(keys, action);
            foreach (var controls in FindObjectsOfType<ControlsOverlay>()) Destroy(controls);
            var controlsCard = GameObject.Find("ControlsOverlay");
            if (controlsCard != null) Destroy(controlsCard);
            var controller = FindObjectOfType<FirstPersonController>();
            var body = controller != null ? controller.transform : null;
            var cc = body != null ? body.GetComponent<CharacterController>() : null;
            if (body == null)
            {
                Fail("No first-person player was available while trying to " + action + ".");
                yield break;
            }
            controller.enabled = false;
            destination.y = body.position.y;
            float deadline = Time.realtimeSinceStartup + 14f;
            while (Vector3.ProjectOnPlane(destination - body.position, Vector3.up).sqrMagnitude > 0.04f &&
                   Time.realtimeSinceStartup < deadline)
            {
                Vector3 delta = destination - body.position; delta.y = 0f;
                // Batch-mode Play Mode can report a zero frame delta even
                // while realtime advances. Keep the TAS deterministic there
                // so the same route can run locally and in CI.
                float stepDelta = Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);
                delta = Vector3.ClampMagnitude(delta, 7f * stepDelta);
                if (cc != null && cc.enabled) cc.Move(delta + Vector3.down * 2f * stepDelta);
                else body.position += delta;
                Vector3 look = lookAt - body.position; look.y = 0f;
                if (look.sqrMagnitude > 0.01f) body.rotation = Quaternion.LookRotation(look);
                yield return null;
            }
            float remaining = Vector3.ProjectOnPlane(destination - body.position, Vector3.up).magnitude;
            if (remaining > 0.45f)
            {
                Fail($"Collision blocked the TAS while trying to {action} ({remaining:0.0}m short).");
                yield break;
            }
            lastMoveSucceeded = true;
        }

        private static float CurrentPlayerY()
        {
            var controller = FindObjectOfType<FirstPersonController>();
            return controller != null ? controller.transform.position.y : 0f;
        }

        private IEnumerator WaitForScene(string contains, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!SceneManager.GetActiveScene().name.Contains(contains) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!SceneManager.GetActiveScene().name.Contains(contains)) Fail("Timed out loading " + contains + ".");
            yield return null; yield return null;
        }

        private IEnumerator WaitForDifferentScene(string before, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (SceneManager.GetActiveScene().name == before && Time.realtimeSinceStartup < deadline) yield return null;
            if (SceneManager.GetActiveScene().name == before) Fail("Scene transition timed out from " + before + ".");
            yield return null; yield return null;
        }

        private void Show(string keys, string action)
        {
            keysText.text = "[ " + keys + " ]";
            actionText.text = "CAMPAIGN TAS  •  " + action;
        }

        private void HideOverlay(bool hidden) => overlayCanvas.gameObject.SetActive(!hidden);

        private void Fail(string message)
        {
            if (!string.IsNullOrEmpty(Failure)) return;
            Failure = message;
            Debug.LogError("[CAMPAIGN TAS] FAIL — " + message);
            Show("FAILED", message);
            RestoreProgress();
            Finished = true;
        }

        private void RestoreProgress()
        {
            for (int i = 0; i < 4; i++)
            {
                string key = "cv_done_" + i;
                if (hadKey[i]) PlayerPrefs.SetInt(key, savedProgress[i]); else PlayerPrefs.DeleteKey(key);
            }
            for (int i = 0; i < socKeys.Length; i++)
            {
                if (hadSocKey[i]) PlayerPrefs.SetInt(socKeys[i], savedSocProgress[i]);
                else PlayerPrefs.DeleteKey(socKeys[i]);
            }
            if (hadEvidence) PlayerPrefs.SetString(SocProgress.EvidenceJsonKey, savedEvidence);
            else PlayerPrefs.DeleteKey(SocProgress.EvidenceJsonKey);
            PlayerPrefs.Save();
        }

        private void BuildOverlay()
        {
            var root = new GameObject("Campaign TAS Input Visualizer", typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(root);
            overlayCanvas = root.GetComponent<Canvas>(); overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay; overlayCanvas.sortingOrder = 600;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(root.transform, false);
            var rt = panel.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(.5f, 0f); rt.pivot = new Vector2(.5f, 0f); rt.anchoredPosition = new Vector2(0, 38); rt.sizeDelta = new Vector2(780, 150);
            HudUI.StylePanel(panel, new Color(.015f, .025f, .05f, .94f), HudUI.Accent);
            keysText = MakeText(panel.transform, 42, new Vector2(0, 28), new Color(.9f, .66f, .14f));
            actionText = MakeText(panel.transform, 24, new Vector2(0, -34), Color.white);
        }

        private static TextMeshProUGUI MakeText(Transform parent, float size, Vector2 pos, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>(); text.fontSize = size; text.color = color; text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(740, 60); text.rectTransform.anchoredPosition = pos;
            return text;
        }
    }
}
#endif
