using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.Settings;
using Cyverse.UI;
using static Cyverse.Level.BlockKit;

namespace Cyverse.Interaction
{
    /// <summary>
    /// Task 4 — ACCOUNTABILITY: the Audit Hunt. E at the access-log board
    /// opens the log full screen; the arrow keys move between entries and E
    /// or Enter flags the one you think is anomalous. Wrong flags buzz with a
    /// hint (no lockout); the right flag scores, explains why, and loads the
    /// next round. Esc closes the log and keeps your place. The in-world board
    /// mirrors the round for anyone watching.
    /// </summary>
    public class AuditStation : MonoBehaviour, IInteractable, IGameplayActionTarget
    {
        public event Action Completed;
        public bool IsComplete { get; private set; }

        public int pointsPerRound = 80;
        public Func<bool> gate;
        public string gateMessage = "You can't use this yet.";

        private const int RowCount = 6;

        private Level1IamContent.LogRound[] rounds;
        private int round, cursor;

        /// <summary>Rounds solved / rounds total (for the HUD checklist).</summary>
        public int Solved => round;
        public int Rounds => rounds != null ? rounds.Length : 0;
        public bool IsAwaitingSelection => panelOpen && !transitioning && !IsComplete;
        public int SelectedRowIndex => cursor;
        private bool active, transitioning, panelOpen;

        private TextMesh headerText, hintText;
        private TextMesh[] rowTexts;
        private Transform highlight;
        private AuditLogPanel panel;
        private ModalSession.Lease modal;
        private Color accent = BuildKit.AccentCyan;

        private static readonly Color RowColor = new Color(0.80f, 0.90f, 1f);
        private static readonly Color GoodColor = new Color(0.30f, 1f, 0.45f);
        private static readonly Color BadColor = new Color(1f, 0.45f, 0.35f);

        public bool CanInteract => !IsComplete && rounds != null && rounds.Length > 0;
        public string Prompt => active ? "Open the access audit log (resume)" : "Open the access audit log";

        void Awake()
        {
            // The saved board's surface is a collider-free Quad and its frame
            // is easy to miss at eye level. A trigger target makes the board
            // reliably selectable without adding a movement obstacle.
            BuildKit.EnsureAimCollider(gameObject, height: 3.4f, width: 4.8f);
        }

        public void Interact(GameObject interactor)
        {
            if (IsComplete || transitioning || rounds == null || rounds.Length == 0) return;
            if (gate != null && !gate())
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast(gateMessage, new Color(1f, 0.55f, 0.4f));
                return;
            }

            if (!panelOpen) OpenLog();
            else Flag();
        }

        /// <summary>Opens the full-screen log, starting the hunt the first time
        /// and resuming the current round after that.</summary>
        private void OpenLog()
        {
            if (!ModalSession.TryOpen(this, ModalSession.Channel.Quiz, out modal)) return;
            if (panel == null) panel = new AuditLogPanel(accent);
            panelOpen = true;
            panel.Show();
            if (!active)
            {
                active = true;
                LoadRound();
                panel.SetFeedback("");
            }
            else
            {
                ShowRoundOnPanel();
            }
            if (hintText != null) hintText.text = "LOG OPEN ON SCREEN";
        }

        private void CloseLog()
        {
            panelOpen = false;
            if (panel != null) panel.Hide();
            modal?.Close();
            modal = null;
            if (hintText != null && !IsComplete) hintText.text = "PRESS [E] TO OPEN THE FULL LOG";
        }

        void Update()
        {
            if (!panelOpen || Time.frameCount == GameState.MenuTransitionFrame) return;

            if (GameInput.CancelDown)
            {
                if (!IsComplete) CloseLog();
                return;
            }
            if (IsComplete || transitioning) return;

            int move = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow)) move = -1;
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow)) move = 1;
            if (move != 0)
            {
                GameplayActions.TryApply(this, GameplayAction.Navigate(move));
                return;
            }

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                GameplayActions.TryApply(this, GameplayAction.Interact());
        }

        private void OnDestroy()
        {
            modal?.Close();
            if (panel != null) panel.Destroy();
        }

        public bool TryApply(GameplayAction action, GameObject actor)
        {
            if (action.Kind != GameplayActionKind.Navigate || !IsAwaitingSelection ||
                rounds == null || rowTexts == null)
                return false;

            // Only rows that actually carry an entry are selectable — a round
            // with fewer lines than the board has rows would otherwise let the
            // highlight park on blank space.
            int count = Mathf.Clamp(rounds[round].lines.Length, 1, rowTexts.Length);
            int delta = action.Index % count;
            cursor = (cursor + delta + count) % count;
            PositionHighlight();
            if (panel != null) panel.SetCursor(cursor);
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
            return true;
        }

        private void Flag()
        {
            var r = rounds[round];
            // Feedback goes on the log itself, not in toasts that would land
            // on top of it.
            if (cursor == r.anomaly)
            {
                rowTexts[cursor].color = GoodColor;
                if (panel != null)
                {
                    panel.SetRowState(cursor, AuditLogPanel.RowState.Correct);
                    panel.SetFeedback($"<color=#4CE087><b>ANOMALY FLAGGED  +{pointsPerRound}</b></color>   {r.why}");
                }
                ScoreSystem.Add(pointsPerRound);
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                BurstFX.SpawnAbove(transform, GoodColor, 22, minimumHeight: 2f);

                round++;
                if (round >= rounds.Length)
                {
                    IsComplete = true;
                    headerText.text = "AUDIT CLEAR ✓";
                    headerText.color = new Color(0.90f, 0.66f, 0.14f);
                    hintText.text = "All anomalies accounted for.";
                    if (highlight != null) highlight.gameObject.SetActive(false);
                    StartCoroutine(FinishSoon());
                }
                else
                {
                    StartCoroutine(NextRoundSoon(r.why));
                }
            }
            else
            {
                StartCoroutine(FlashRow(cursor));
                if (panel != null)
                    panel.SetFeedback($"<color=#FFB070><b>THAT ENTRY CHECKS OUT.</b></color>   Hint: {r.hint}");
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
            }
        }

        // The next round loads after a beat; the reason the last anomaly
        // stood out stays on screen while the player reads the new log.
        private IEnumerator NextRoundSoon(string why)
        {
            transitioning = true;
            yield return new WaitForSeconds(1.2f);
            LoadRound();
            if (panel != null)
                panel.SetFeedback($"<color=#4CE087>Round {round} cleared:</color> {why}");
            transitioning = false;
        }

        // The cleared log stays up long enough to read, then closes itself.
        private IEnumerator FinishSoon()
        {
            if (panel != null) panel.SetTitleDone();
            yield return new WaitForSecondsRealtime(AccessibilitySettings.ReduceMotion ? 0.6f : 1.8f);
            if (panelOpen) CloseLog();
            Completed?.Invoke();
        }

        private IEnumerator FlashRow(int row)
        {
            rowTexts[row].color = BadColor;
            if (panel != null) panel.SetRowState(row, AuditLogPanel.RowState.Wrong);
            yield return new WaitForSeconds(0.5f);
            if (rowTexts[row].color == BadColor) rowTexts[row].color = RowColor;
            if (panel != null) panel.SetRowState(row, AuditLogPanel.RowState.Normal);
        }

        private void LoadRound()
        {
            var r = rounds[round];
            headerText.text = $"ACCESS AUDIT — ROUND {round + 1}/{rounds.Length}";
            if (highlight != null) highlight.gameObject.SetActive(true);
            SetIdleRows(false);
            for (int i = 0; i < rowTexts.Length; i++)
            {
                rowTexts[i].text = i < r.lines.Length ? r.lines[i] : "";
                rowTexts[i].color = RowColor;
            }
            cursor = 0;
            PositionHighlight();
            ShowRoundOnPanel();
        }

        private void ShowRoundOnPanel()
        {
            if (panel == null || rounds == null || round >= rounds.Length) return;
            panel.SetRound(round + 1, rounds.Length, rounds[round].lines);
            panel.SetCursor(cursor);
        }

        private void PositionHighlight()
        {
            if (highlight == null) return;
            // Follow the row itself, so an adopted (saved) screen lines up too.
            float y = rowTexts != null && cursor < rowTexts.Length && rowTexts[cursor] != null
                ? rowTexts[cursor].transform.localPosition.y : RowY(cursor);
            highlight.localPosition = new Vector3(0f, y, 0.01f);
        }

        // Six rows between the header (2.78) and the hint (0.98), with clear
        // space above the hint under the last row.
        private static float RowY(int i) => 2.5f - i * 0.25f;

        // ---- Wiring ----------------------------------------------------------

        /// <summary>Installs the runtime-only state: the log rounds, the gate,
        /// and the screen's TextMesh references. None of that survives being
        /// saved into a scene file (delegates and non-serialized fields are
        /// dropped), so a visual-pass scene comes back with a dead board unless
        /// this runs. Called by Build and again by the level factory on load;
        /// safe either way.</summary>
        public void Configure(Level1IamContent.LogRound[] logRounds, Color accent,
            Func<bool> gate, string gateMessage)
        {
            rounds = logRounds;
            this.accent = accent;
            this.gate = gate;
            this.gateMessage = gateMessage;
            if (!ScreenIsWired() && !TryAdoptScreen()) BuildScreen(accent);
            EnsureIdleRows();
            if (!active && !IsComplete) ShowIdleScreen();
        }

        private void ShowIdleScreen()
        {
            if (!ScreenIsWired()) return;
            headerText.text = "ACCESS AUDIT // STANDBY";
            hintText.text = "PRESS [E] TO OPEN ACCESS LOG";
            for (int i = 0; i < rowTexts.Length; i++) rowTexts[i].text = "";
            rowTexts[0].text = "REVIEW IDENTITY  ·  PRIVILEGE  ·  TIME  ·  LOCATION";
            rowTexts[0].color = new Color(0.62f, 0.74f, 0.88f);
            if (highlight != null) highlight.gameObject.SetActive(false);
            SetIdleRows(true);
        }

        /// <summary>Greyed placeholder entries under the idle board's legend,
        /// so at rest it reads as a log waiting to be opened rather than an
        /// empty screen. Hidden as soon as a round loads.</summary>
        private void EnsureIdleRows()
        {
            if (transform.Find("IdleRows") != null) return;
            var rng = new System.Random(7);
            float[] columnX = { -1.75f, -1.2f, -0.35f, 0.35f };
            float[] minWidth = { 0.30f, 0.40f, 0.30f, 0.55f };
            var bars = new List<CombineInstance>();
            for (int row = 1; row < RowCount; row++)
                for (int c = 0; c < columnX.Length; c++)
                {
                    float w = minWidth[c] + (float)rng.NextDouble() * 0.25f;
                    bars.Add(Piece(QuadMesh, new Vector3(columnX[c] + w * 0.5f, RowY(row), -0.03f), Vector3.zero,
                        new Vector3(w, 0.045f, 1f)));
                }
            Batch("IdleRows", transform, BuildKit.MakeEmissive(new Color(0.30f, 0.42f, 0.55f), 0.45f), bars);
        }

        private void SetIdleRows(bool visible)
        {
            Transform idle = transform.Find("IdleRows");
            if (idle != null) idle.gameObject.SetActive(visible);
        }

        private bool ScreenIsWired()
        {
            if (headerText == null || hintText == null || highlight == null) return false;
            if (rowTexts == null || rowTexts.Length != RowCount) return false;
            foreach (var row in rowTexts) if (row == null) return false;
            return true;
        }

        /// <summary>Re-attaches to a screen that already exists in the scene —
        /// the case for a saved visual pass, where the objects survived but the
        /// references didn't. Preferred over rebuilding, since it keeps whatever
        /// layout the scene was authored with.</summary>
        private bool TryAdoptScreen()
        {
            TextMesh header = null, hint = null;
            Transform hl = null;
            var rows = new List<TextMesh>();

            foreach (Transform child in transform)
            {
                if (child.name == "Highlight") { hl = child; continue; }
                var label = child.GetComponent<TextMesh>();
                if (label == null || !child.name.StartsWith("Label_")) continue;

                // The rows are built with empty text, so they carry the bare
                // "Label_" name; the header and hint carry their own text.
                if (child.name == "Label_") rows.Add(label);
                else if (child.name == "Label_ACCESS_AUDIT") header = label;
                else hint = label;
            }

            if (header == null || hint == null || hl == null || rows.Count != RowCount) return false;

            rows.Sort((a, b) => b.transform.localPosition.y.CompareTo(a.transform.localPosition.y));
            headerText = header;
            hintText = hint;
            highlight = hl;
            rowTexts = rows.ToArray();
            return true;
        }

        /// <summary>(Re)creates the board's text and highlight. Anything a
        /// previous build left behind is cleared first, so re-wiring a saved
        /// scene doesn't double up the labels.</summary>
        private void BuildScreen(Color accent)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                // Exactly what MakeLabel and the highlight quad are named. The
                // frame, the surface and the "Sign_AUDIT" board sign all have
                // other names and are left alone.
                if (child.name.StartsWith("Label_") || child.name == "Highlight")
                    SafeDestroy(child.gameObject);
            }

            headerText = BuildKit.MakeLabel(transform, new Vector3(0f, 2.78f, -0.04f),
                "ACCESS AUDIT", accent, 0.026f);
            hintText = BuildKit.MakeLabel(transform, new Vector3(0f, 0.98f, -0.04f),
                "PRESS [E] TO OPEN THE FULL LOG", new Color(0.55f, 0.65f, 0.78f), 0.018f,
                billboard: false, anchor: TextAnchor.MiddleCenter, style: FontStyle.Normal);

            rowTexts = new TextMesh[RowCount];
            for (int i = 0; i < RowCount; i++)
            {
                rowTexts[i] = BuildKit.MakeLabel(transform,
                    new Vector3(-1.75f, RowY(i), -0.04f), "", RowColor, 0.019f,
                    billboard: false, anchor: TextAnchor.MiddleLeft, style: FontStyle.Normal);
            }

            var hl = BuildKit.SpawnLocal(PrimitiveType.Quad, "Highlight", transform,
                new Vector3(0f, RowY(0), 0.01f), Vector3.zero, new Vector3(3.6f, 0.27f, 1f),
                BuildKit.MakeHologram(accent), collider: false);
            highlight = hl.transform;
        }

        /// <summary>The board's housing, in the chamfered style of the Level 1
        /// stations: a panel with a bezel round the screen and a lit top edge,
        /// standing on two short legs with feet. Front toward -Z.</summary>
        private static void BuildFrame(Transform root, Color accent)
        {
            Material body = Mat(new Color(0.075f, 0.085f, 0.115f), 0.45f, 0.4f);
            Material trim = Mat(new Color(0.17f, 0.19f, 0.25f), 0.55f, 0.6f);
            Slab("Panel", root, new Vector3(0f, 1.95f, 0.1f), new Vector3(3.9f, 2.3f, 0.1f), 0.03f, body, solid: true);
            Batch("Bezel", root, trim, new List<CombineInstance>
            {
                Piece(CubeMesh, new Vector3(-1.9f, 1.95f, 0.025f), Vector3.zero, new Vector3(0.1f, 2.3f, 0.03f)),
                Piece(CubeMesh, new Vector3(1.9f, 1.95f, 0.025f), Vector3.zero, new Vector3(0.1f, 2.3f, 0.03f)),
                Piece(CubeMesh, new Vector3(0f, 3.05f, 0.025f), Vector3.zero, new Vector3(3.7f, 0.1f, 0.03f)),
                Piece(CubeMesh, new Vector3(0f, 0.85f, 0.025f), Vector3.zero, new Vector3(3.7f, 0.1f, 0.03f)),
            });
            Box("TopGlow", root, new Vector3(0f, 3.05f, 0.007f), new Vector3(3.2f, 0.012f, 0.006f),
                BuildKit.MakeEmissive(accent, 1.1f));
            for (int side = -1; side <= 1; side += 2)
            {
                string s = side < 0 ? "L" : "R";
                Block("Post_" + s, root, new Vector3(side * 1.55f, 0f, 0.1f), Vector3.zero, 0.05f, 0.9f,
                    new Vector2(0.07f, 0.07f), new Vector2(0.06f, 0.06f), 0.02f, body, solid: true);
                Slab("Foot_" + s, root, new Vector3(side * 1.55f, 0.025f, 0.1f), new Vector3(0.26f, 0.05f, 0.5f),
                    0.015f, trim, solid: true);
            }
        }

        private static void SafeDestroy(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        // ---- Construction ----------------------------------------------------

        public static AuditStation Build(Vector3 pos, float rotY,
            Level1IamContent.LogRound[] rounds, Color accent, Func<bool> gate, string gateMessage)
        {
            var root = new GameObject("AuditStation");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            BuildFrame(root.transform, accent);
            BuildKit.SpawnLocal(PrimitiveType.Quad, "Surface", root.transform,
                new Vector3(0f, 1.95f, 0.02f), Vector3.zero, new Vector3(3.75f, 2.15f, 1f),
                BuildKit.MakeHologram(new Color(0.10f, 0.16f, 0.24f)), collider: false);

            var station = root.AddComponent<AuditStation>();
            station.Configure(rounds, accent, gate, gateMessage);
            // No floating "AUDIT" sign: the board's own header names it, and
            // the sign only ever showed as a faded copy behind the board.

            var glow = new GameObject("AuditLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.2f, -1.4f);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = accent;
            l.range = 5f;
            l.intensity = 1.5f;

            return station;
        }
    }
}
