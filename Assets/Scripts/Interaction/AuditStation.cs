using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// Task 4 — ACCOUNTABILITY: the Audit Hunt. An in-world access-log board:
    /// E opens the log, ↑/↓ move the highlight, E flags the entry you think is
    /// anomalous. Wrong flags buzz with a hint (no lockout); the right flag
    /// scores and loads the next round. Playable entirely in-world — no modal,
    /// so it feels like working a real SOC screen.
    /// </summary>
    public class AuditStation : MonoBehaviour, IInteractable
    {
        public event Action Completed;
        public bool IsComplete { get; private set; }

        public int pointsPerRound = 80;
        public float controlRange = 6f;
        public Func<bool> gate;
        public string gateMessage = "You can't use this yet.";

        private const int RowCount = 6;

        private Level1IamContent.LogRound[] rounds;
        private int round, cursor;

        /// <summary>Rounds solved / rounds total (for the HUD checklist).</summary>
        public int Solved => round;
        public int Rounds => rounds != null ? rounds.Length : 0;
        private bool active, transitioning;

        private TextMesh headerText, hintText;
        private TextMesh[] rowTexts;
        private Transform highlight;

        private static readonly Color RowColor = new Color(0.80f, 0.90f, 1f);
        private static readonly Color GoodColor = new Color(0.30f, 1f, 0.45f);
        private static readonly Color BadColor = new Color(1f, 0.45f, 0.35f);

        public bool CanInteract => !IsComplete && rounds != null && rounds.Length > 0;
        public string Prompt => active ? "Flag highlighted entry" : "Open the access audit log";

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

            if (!active)
            {
                active = true;
                LoadRound();
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("Find the anomaly — ↑/↓ select, E flag", new Color(0.70f, 0.85f, 1f));
            }
            else
            {
                Flag();
            }
        }

        void Update()
        {
            if (!active || IsComplete || transitioning || GameState.Busy) return;

            var cam = Camera.main;
            if (cam == null || (cam.transform.position - transform.position).sqrMagnitude > controlRange * controlRange)
                return;

            int move = 0;
            if (Input.GetKeyDown(KeyCode.UpArrow)) move = -1;
            else if (Input.GetKeyDown(KeyCode.DownArrow)) move = 1;
            if (move == 0) return;

            // Only rows that actually carry an entry are selectable — a round
            // with fewer lines than the board has rows would otherwise let the
            // highlight park on blank space.
            int count = Mathf.Clamp(rounds[round].lines.Length, 1, rowTexts.Length);
            cursor = (cursor + move + count) % count;
            PositionHighlight();
            if (Sfx.Instance != null) Sfx.Instance.PlayClick();
        }

        private void Flag()
        {
            var r = rounds[round];
            if (cursor == r.anomaly)
            {
                rowTexts[cursor].color = GoodColor;
                ScoreSystem.Add(pointsPerRound);
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                BurstFX.SpawnAbove(transform, GoodColor, 22, minimumHeight: 2f);
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast($"ANOMALY FLAGGED  +{pointsPerRound}  ·  {r.why}", GoodColor);

                round++;
                if (round >= rounds.Length)
                {
                    IsComplete = true;
                    headerText.text = "AUDIT CLEAR ✓";
                    headerText.color = new Color(0.90f, 0.66f, 0.14f);
                    hintText.text = "All anomalies accounted for.";
                    if (highlight != null) highlight.gameObject.SetActive(false);
                    Completed?.Invoke();
                }
                else
                {
                    StartCoroutine(NextRoundSoon());
                }
            }
            else
            {
                StartCoroutine(FlashRow(cursor));
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast($"That entry checks out. Hint: {r.hint}", new Color(1f, 0.75f, 0.45f));
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Selects and flags the correct row through the normal flag
        /// path. The runner calls this once per round and still waits for the
        /// station's real transition delay between rounds.</summary>
        public void SolveCurrentRoundForAutomation()
        {
            if (IsComplete || transitioning || rounds == null || rounds.Length == 0) return;
            if (!active)
            {
                active = true;
                LoadRound();
            }
            cursor = rounds[round].anomaly;
            PositionHighlight();
            Flag();
        }
#endif

        private IEnumerator NextRoundSoon()
        {
            transitioning = true;
            yield return new WaitForSeconds(1.2f);
            LoadRound();
            transitioning = false;
        }

        private IEnumerator FlashRow(int row)
        {
            rowTexts[row].color = BadColor;
            yield return new WaitForSeconds(0.5f);
            if (rowTexts[row].color == BadColor) rowTexts[row].color = RowColor;
        }

        private void LoadRound()
        {
            var r = rounds[round];
            headerText.text = $"ACCESS AUDIT — ROUND {round + 1}/{rounds.Length}";
            for (int i = 0; i < rowTexts.Length; i++)
            {
                rowTexts[i].text = i < r.lines.Length ? r.lines[i] : "";
                rowTexts[i].color = RowColor;
            }
            cursor = 0;
            PositionHighlight();
        }

        private void PositionHighlight()
        {
            if (highlight == null) return;
            highlight.localPosition = new Vector3(0f, RowY(cursor), 0.01f);
        }

        private static float RowY(int i) => 2.42f - i * 0.30f;

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
            this.gate = gate;
            this.gateMessage = gateMessage;
            if (!ScreenIsWired() && !TryAdoptScreen()) BuildScreen(accent);
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
                "↑ / ↓ select   ·   E flag the anomaly", new Color(0.55f, 0.65f, 0.78f), 0.018f,
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

            var frameMat = BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.55f, 0.35f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Post_L", root.transform,
                new Vector3(-1.85f, 1.6f, 0.08f), Vector3.zero, new Vector3(0.12f, 3.2f, 0.12f), frameMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Post_R", root.transform,
                new Vector3(1.85f, 1.6f, 0.08f), Vector3.zero, new Vector3(0.12f, 3.2f, 0.12f), frameMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Panel", root.transform,
                new Vector3(0f, 1.95f, 0.08f), Vector3.zero, new Vector3(3.9f, 2.3f, 0.08f), frameMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Quad, "Surface", root.transform,
                new Vector3(0f, 1.95f, 0.02f), Vector3.zero, new Vector3(3.75f, 2.15f, 1f),
                BuildKit.MakeHologram(new Color(0.10f, 0.16f, 0.24f)), collider: false);

            var station = root.AddComponent<AuditStation>();
            station.Configure(rounds, accent, gate, gateMessage);

            BuildKit.MakeSign(root.transform, pos + new Vector3(0f, 3.6f, 0f), "AUDIT", accent, 0.032f);

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
