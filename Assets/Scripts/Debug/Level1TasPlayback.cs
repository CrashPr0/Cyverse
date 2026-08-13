#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cyverse.Core;
using Cyverse.Interaction;
using Cyverse.Level;
using Cyverse.Player;

namespace Cyverse.Testing
{
    /// <summary>
    /// Watchable, tool-assisted Level 1 replay. This is deliberately slower
    /// than the CI smoke run: the camera travels to each target and an overlay
    /// shows the virtual keys/actions that the deterministic runner executes.
    /// </summary>
    public sealed class Level1TasPlayback : MonoBehaviour
    {
        public bool Finished { get; private set; }
        public bool Passed { get; private set; }
        public string Failure { get; private set; }

        public float travelSpeed = 8f;
        public float actionPause = 0.65f;
        public bool restoreProgressOnFinish = true;

        private FirstPersonController playerController;
        private CharacterController characterController;
        private Transform player;
        private Camera viewCamera;
        private TasOverlay overlay;
        private bool hadCompletionKey;
        private int previousCompletion;

        private IEnumerator Start()
        {
            hadCompletionKey = PlayerPrefs.HasKey("cv_done_1");
            previousCompletion = PlayerPrefs.GetInt("cv_done_1", 0);
            FirstPersonController.LockCursor(false);

            yield return null;
            yield return null;
            playerController = FindObjectOfType<FirstPersonController>();
            viewCamera = Camera.main;
            player = playerController != null ? playerController.transform :
                (viewCamera != null ? viewCamera.transform.root : null);
            characterController = player != null ? player.GetComponent<CharacterController>() : null;
            // Disable only live input. Keep the CharacterController enabled so
            // the TAS travels through the same walls, doors, and floor
            // collision as a real player instead of teleporting through them.
            if (playerController != null) playerController.enabled = false;
            // A TAS supplies its own readable input overlay, so the passive
            // first-run controls card would only obscure the playback.
            foreach (var controls in FindObjectsOfType<Cyverse.UI.ControlsOverlay>())
                Destroy(controls);
            var controlsCard = GameObject.Find("ControlsOverlay");
            if (controlsCard != null) Destroy(controlsCard);
            overlay = TasOverlay.Create();

            Debug.Log("[TAS] Starting watchable Level 1 replay");
            yield return Show("TAS READY", "Deterministic Level 1 route", 1.2f);

            var manager = FindObjectOfType<Level1IamManager>();
            var briefing = FindObjectOfType<VideoStation>();
            var badge = FindObjectOfType<BadgeStation>();
            var mfa = FindObjectOfType<MfaGauntlet>();
            var sorting = FindObjectOfType<SortingStation>();
            var audit = FindObjectOfType<AuditStation>();
            var exam = FindObjectOfType<CertExamStation>();
            if (player == null || manager == null || briefing == null || badge == null ||
                mfa == null || sorting == null || audit == null || exam == null)
            {
                Fail("The visual-pass scene is missing the player or a required station.");
                yield break;
            }

            yield return TravelTo(briefing.transform, "W", "Walk to SECURITY BRIEFING", 3f);
            yield return Show("E", "Play security briefing", actionPause);
            yield return Show("→  (HOLD)", "Tool-assisted scrub to end", 1.2f);
            briefing.CompleteForAutomation();
            yield return Show("", "Briefing complete — task door unlocked", 0.9f);

            yield return TravelTo(badge.transform, "W", "Walk to ENROLLMENT", 2f);
            yield return Show("E", "Create ID badge", actionPause);
            badge.Interact(gameObject);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!badge.IsEnrolled && Time.realtimeSinceStartup < deadline) yield return null;
            if (!badge.IsEnrolled) { Fail("Badge enrollment timed out."); yield break; }

            Carryable token = FindCarryable("mfa_token");
            DropZone tokenSlot = FindZone("TOKEN SLOT");
            if (token == null || tokenSlot == null) { Fail("MFA token route is missing."); yield break; }
            yield return TravelTo(token.transform, "W", "Walk to SECURITY TOKEN", 1.7f);
            yield return Show("E", "Pick up security token", actionPause);
            token.Interact(gameObject);
            yield return TravelTo(tokenSlot.transform, "W", "Carry token to TOKEN SLOT", 1.8f);
            yield return Show("E", "Insert token — SOMETHING YOU HAVE", actionPause);
            tokenSlot.Interact(gameObject);

            MfaFactor knowledge = null, biometric = null;
            foreach (var factor in FindObjectsOfType<MfaFactor>())
            {
                if (factor.kind == MfaFactor.Kind.Knowledge) knowledge = factor;
                else if (factor.kind == MfaFactor.Kind.Biometric) biometric = factor;
            }
            if (knowledge != null) yield return TravelTo(knowledge.transform, "W", "Walk to PASSCODE terminal", 1.8f);
            yield return Show("E  TYPE  ENTER", "Verify SOMETHING YOU KNOW", 1.1f);
            mfa.FactorCleared(0);
            if (biometric != null) yield return TravelTo(biometric.transform, "W", "Walk to BIOMETRIC pad", 1.8f);
            yield return Show("E", "Verify SOMETHING YOU ARE", 1.1f);
            mfa.FactorCleared(2);

            // Each crate still travels through its real acceptance predicate
            // and DropZone callback. The overlay makes the pickup/delivery
            // input sequence readable while the camera follows the route.
            var dataItems = FindObjectsOfType<Carryable>();
            foreach (var item in dataItems)
            {
                if (item == null || item.id == "mfa_token") continue;
                DropZone destination = FindAcceptingZone(item);
                if (destination == null) { Fail("No destination accepts " + item.id + "."); yield break; }
                yield return TravelTo(item.transform, "W", "Walk to " + item.itemName, 1.7f);
                yield return Show("E", "Pick up " + item.itemName, actionPause);
                item.Interact(gameObject);
                yield return TravelTo(destination.transform, "W", "Carry crate to " + destination.zoneName, 1.8f);
                yield return Show("E", "File under " + destination.zoneName, actionPause);
                destination.Interact(gameObject);
                yield return null;
            }
            if (!sorting.IsComplete) { Fail("Data Triage did not complete."); yield break; }

            yield return TravelTo(audit.transform, "W", "Walk to AUDIT HUNT", 2.4f);
            while (!audit.IsComplete)
            {
                int round = audit.Solved + 1;
                yield return Show("E", "Open audit round " + round, actionPause);
                yield return Show("↓  ↑", "Select anomalous log entry", 0.8f);
                yield return Show("E", "Flag highlighted anomaly", actionPause);
                audit.SolveCurrentRoundForAutomation();
                if (!audit.IsComplete) yield return new WaitForSecondsRealtime(1.3f);
            }

            deadline = Time.realtimeSinceStartup + 3f;
            while (manager.CurrentPhase != Level1IamManager.Phase.Exam &&
                   Time.realtimeSinceStartup < deadline) yield return null;
            if (manager.CurrentPhase != Level1IamManager.Phase.Exam)
            { Fail("Certification Exam did not unlock."); yield break; }

            yield return TravelTo(exam.transform, "W", "Walk to CERTIFICATION EXAM", 2f);
            yield return Show("E", "Start certification exam", 0.8f);
            yield return Show("1   2   3", "Answer certification questions", 1.5f);
            exam.CompleteForAutomation();
            yield return null;
            if (manager.CurrentPhase != Level1IamManager.Phase.Complete || !GameState.LevelComplete)
            { Fail("Level did not reach the results flow."); yield break; }

            Passed = true;
            yield return Show("TAS COMPLETE", "Level 1 end flow passed", 2f);
            Finished = true;
            Debug.Log("[TAS] PASS — watchable Level 1 replay completed");
            Restore();
        }

        private IEnumerator TravelTo(Transform target, string keys, string action, float stopDistance)
        {
            if (target == null) yield break;
            overlay.Set(keys, action);
            Vector3 targetFlat = target.position;
            targetFlat.y = player.position.y;
            Vector3 away = player.position - targetFlat;
            away.y = 0f;
            if (away.sqrMagnitude < 0.05f) away = -target.forward;
            Vector3 destination = targetFlat + away.normalized * stopDistance;
            float deadline = Time.realtimeSinceStartup + 12f;

            while ((player.position - destination).sqrMagnitude > 0.02f &&
                   Time.realtimeSinceStartup < deadline)
            {
                float step = travelSpeed * Time.unscaledDeltaTime;
                Vector3 toward = destination - player.position;
                toward.y = 0f;
                Vector3 displacement = toward.sqrMagnitude > 0.0001f
                    ? toward.normalized * Mathf.Min(step, toward.magnitude)
                    : Vector3.zero;

                // CharacterController.Move resolves the environment collision
                // and a small downward move keeps the actor grounded while the
                // normal FirstPersonController is temporarily disabled.
                if (characterController != null && characterController.enabled)
                    characterController.Move(displacement + Vector3.down * 2f * Time.unscaledDeltaTime);
                else
                    player.position += displacement;
                Face(target.position + Vector3.up * 1.4f);
                yield return null;
            }
            Face(target.position + Vector3.up * 1.4f);
            yield return new WaitForSecondsRealtime(0.2f);
        }

        private void Face(Vector3 worldPoint)
        {
            Vector3 flat = worldPoint - player.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f)
                player.rotation = Quaternion.Slerp(player.rotation,
                    Quaternion.LookRotation(flat), 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            if (viewCamera != null)
            {
                float vertical = worldPoint.y - viewCamera.transform.position.y;
                float horizontal = Mathf.Max(0.01f,
                    Vector3.ProjectOnPlane(worldPoint - viewCamera.transform.position, Vector3.up).magnitude);
                float pitch = -Mathf.Atan2(vertical, horizontal) * Mathf.Rad2Deg;
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private IEnumerator Show(string keys, string action, float seconds)
        {
            if (overlay != null) overlay.Set(keys, action);
            yield return new WaitForSecondsRealtime(seconds);
        }

        private static Carryable FindCarryable(string id)
        {
            foreach (var item in FindObjectsOfType<Carryable>()) if (item.id == id) return item;
            return null;
        }

        private static DropZone FindZone(string name)
        {
            foreach (var zone in FindObjectsOfType<DropZone>()) if (zone.zoneName == name) return zone;
            return null;
        }

        private static DropZone FindAcceptingZone(Carryable item)
        {
            foreach (var zone in FindObjectsOfType<DropZone>())
                if (zone.accepts != null && zone.accepts(item)) return zone;
            return null;
        }

        private void Fail(string message)
        {
            Failure = message;
            Finished = true;
            Debug.LogError("[TAS] FAIL — " + message);
            if (overlay != null) overlay.Set("TAS FAILED", message);
            Restore();
        }

        private void Restore()
        {
            if (restoreProgressOnFinish)
            {
                if (hadCompletionKey) PlayerPrefs.SetInt("cv_done_1", previousCompletion);
                else PlayerPrefs.DeleteKey("cv_done_1");
                PlayerPrefs.Save();
            }
            if (playerController != null) playerController.enabled = true;
        }

        private sealed class TasOverlay
        {
            private TextMeshProUGUI keyText;
            private TextMeshProUGUI actionText;

            public static TasOverlay Create()
            {
                var root = new GameObject("TAS Input Visualizer", typeof(Canvas), typeof(CanvasScaler));
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);

                var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(root.transform, false);
                var rect = panel.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 42f);
                rect.sizeDelta = new Vector2(720f, 150f);
                panel.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.05f, 0.94f);
                Cyverse.UI.HudUI.StylePanel(panel, panel.GetComponent<Image>().color,
                    new Color(0.35f, 0.85f, 1f));

                var result = new TasOverlay();
                result.keyText = MakeText(panel.transform, "Keys", 42f, new Vector2(0f, 28f),
                    new Color(0.90f, 0.66f, 0.14f), FontStyles.Bold);
                result.actionText = MakeText(panel.transform, "Action", 25f, new Vector2(0f, -34f),
                    Color.white, FontStyles.Normal);
                result.Set("TAS", "Preparing route…");
                return result;
            }

            public void Set(string keys, string action)
            {
                keyText.text = string.IsNullOrEmpty(keys) ? "<color=#5BD9FF>●</color>" : "[ " + keys + " ]";
                actionText.text = "TAS INPUT  •  " + action;
            }

            private static TextMeshProUGUI MakeText(Transform parent, string name, float size,
                Vector2 position, Color color, FontStyles style)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                var text = go.GetComponent<TextMeshProUGUI>();
                text.fontSize = size;
                text.color = color;
                text.fontStyle = style;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                var rect = text.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(680f, 62f);
                rect.anchoredPosition = position;
                return text;
            }
        }
    }
}
#endif
