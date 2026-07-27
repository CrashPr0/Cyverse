using System;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.Settings;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// Task 1 — SIEM. Alerts arrive one at a time on a countdown; the player
    /// triages each with [1] ESCALATE or [2] DISMISS before the timer runs out.
    ///
    /// The time pressure is the lesson. Reading a static list of alerts teaches
    /// nothing about a SOC; being made to judge under a clock, with noise
    /// outnumbering real incidents, is what "alert fatigue" actually feels
    /// like. Letting one expire counts as a miss — the flood does not wait.
    /// </summary>
    public class SiemConsole : MonoBehaviour, IInteractable
    {
        public event Action Completed;
        public bool IsComplete { get; private set; }

        public int pointsPerAlert = 40;
        public float secondsPerAlert = 9f;
        public float controlRange = 7f;

        private Level2Content.Alert[] alerts;
        private int index = -1;
        private int correct;
        private bool running;
        private float timeLeft;

        private TextMesh headerText, alertText, hintText;
        private Transform timerFill;
        private Renderer screenRenderer;

        public bool CanInteract => !IsComplete && !running;
        public string Prompt => running ? "Triaging…" : "Start the alert shift";

        public int Correct => correct;
        public int Total => alerts != null ? alerts.Length : 0;
        public int Handled => Mathf.Max(0, index);

        public void Interact(GameObject interactor)
        {
            if (IsComplete || running) return;
            running = true;
            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast("SHIFT START — [1] ESCALATE   [2] DISMISS", new Color(0.55f, 0.85f, 1f));
            NextAlert();
        }

        private void NextAlert()
        {
            index++;
            if (index >= alerts.Length) { Finish(); return; }

            timeLeft = secondsPerAlert;
            var a = alerts[index];
            if (headerText != null)
                headerText.text = $"ALERT {index + 1}/{alerts.Length}   ·   source: {a.source}";
            if (alertText != null) alertText.text = Wrap(a.text, 34);
            if (hintText != null) hintText.text = "[1] ESCALATE        [2] DISMISS";
        }

        void Update()
        {
            if (!running || IsComplete) return;

            // Countdown only advances while the player is actually at the desk,
            // so stepping away to think never punishes them.
            var cam = Camera.main;
            bool near = cam != null &&
                (cam.transform.position - transform.position).sqrMagnitude < controlRange * controlRange;
            if (!near || GameState.AnyMenuOpen) return;

            timeLeft -= Time.deltaTime;
            if (timerFill != null)
            {
                var s = timerFill.localScale;
                s.x = Mathf.Clamp01(timeLeft / secondsPerAlert);
                timerFill.localScale = s;
            }

            if (timeLeft <= 0f) { Resolve(-1); return; }

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Resolve(1);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Resolve(0);
        }

        /// <summary>choice: 1 escalate, 0 dismiss, -1 timed out.</summary>
        private void Resolve(int choice)
        {
            var a = alerts[index];
            bool right = choice >= 0 && (choice == 1) == a.escalate;

            if (right)
            {
                correct++;
                ScoreSystem.Add(pointsPerAlert);
                if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast(
                        (a.escalate ? "ESCALATED  +" : "DISMISSED  +") + pointsPerAlert + "  ·  " + a.why,
                        new Color(0.30f, 1f, 0.45f));
            }
            else
            {
                if (Sfx.Instance != null) Sfx.Instance.PlayDeny();
                string lead = choice < 0
                    ? "MISSED — the queue moved on."
                    : a.escalate ? "That was REAL." : "That was noise.";
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast($"{lead}  {a.why}", new Color(1f, 0.55f, 0.4f));
            }

            NextAlert();
        }

        private void Finish()
        {
            running = false;
            IsComplete = true;
            if (headerText != null) headerText.text = "SHIFT COMPLETE";
            if (alertText != null) alertText.text = Wrap($"Triaged {correct} of {alerts.Length} correctly.", 34);
            if (hintText != null) hintText.text = "";
            if (timerFill != null) timerFill.localScale = new Vector3(0f, 1f, 1f);
            if (screenRenderer != null)
                screenRenderer.sharedMaterial = BuildKit.MakeHologram(new Color(0.30f, 1f, 0.45f));
            BurstFX.Spawn(transform.position + Vector3.up * 2.2f, new Color(0.30f, 1f, 0.45f), 30);
            Completed?.Invoke();
        }

        private static string Wrap(string text, int maxLine)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder();
            int len = 0;
            foreach (string w in text.Split(' '))
            {
                if (len > 0 && len + w.Length + 1 > maxLine) { sb.Append('\n'); len = 0; }
                else if (len > 0) { sb.Append(' '); len++; }
                sb.Append(w);
                len += w.Length;
            }
            return sb.ToString();
        }

        // ---- Construction ----------------------------------------------------

        public static SiemConsole Build(Vector3 pos, float rotY, Level2Content.Alert[] alerts, Color accent)
        {
            var root = new GameObject("SiemConsole");
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            var bodyMat = BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Desk", root.transform,
                new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(3.0f, 1.0f, 1.0f), bodyMat, collider: true);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Posts", root.transform,
                new Vector3(0f, 1.6f, 0.35f), Vector3.zero, new Vector3(0.1f, 1.2f, 0.1f), bodyMat, collider: false);

            // Big wall-style triage screen above the desk.
            BuildKit.SpawnLocal(PrimitiveType.Cube, "ScreenBody", root.transform,
                new Vector3(0f, 2.5f, 0.2f), Vector3.zero, new Vector3(3.6f, 2.1f, 0.1f), bodyMat, collider: false);
            var screen = BuildKit.SpawnLocal(PrimitiveType.Quad, "Screen", root.transform,
                new Vector3(0f, 2.5f, 0.14f), Vector3.zero, new Vector3(3.45f, 1.95f, 1f),
                BuildKit.MakeHologram(accent), collider: false);

            var console = root.AddComponent<SiemConsole>();
            console.alerts = alerts;
            console.screenRenderer = screen.GetComponent<Renderer>();

            console.headerText = BuildKit.MakeLabel(root.transform, new Vector3(0f, 3.25f, 0.1f),
                "SIEM — ALERT QUEUE", accent, 0.024f);
            console.alertText = BuildKit.MakeLabel(root.transform, new Vector3(0f, 2.55f, 0.1f),
                "Press E to start the shift", new Color(0.95f, 0.98f, 1f), 0.030f);
            console.hintText = BuildKit.MakeLabel(root.transform, new Vector3(0f, 1.85f, 0.1f),
                "", new Color(0.60f, 0.72f, 0.85f), 0.020f);

            // Countdown bar, left-pivoted so localScale.x is the time left.
            BuildKit.SpawnLocal(PrimitiveType.Cube, "TimerTrack", root.transform,
                new Vector3(0f, 1.62f, 0.1f), Vector3.zero, new Vector3(3.3f, 0.06f, 0.02f),
                BuildKit.MakeStandard(new Color(0.15f, 0.17f, 0.22f), 0.3f, 0f), collider: false);
            // The pivot sits at the bar's LEFT edge and stays unit-scaled; the
            // bar itself is offset half a width along +X so scaling the pivot's
            // x drains the bar leftward. (Scaling the pivot to the bar's size
            // instead would make Update's `scale.x = fraction` collapse it.)
            var pivot = new GameObject("TimerPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(-1.65f, 1.62f, 0.09f);
            pivot.localScale = Vector3.one;
            BuildKit.SpawnLocal(PrimitiveType.Cube, "TimerFill", pivot,
                new Vector3(1.65f, 0f, 0f), Vector3.zero, new Vector3(3.3f, 0.05f, 0.02f),
                BuildKit.MakeEmissive(accent, 2f), collider: false);
            console.timerFill = pivot;

            BuildKit.MakeSign(root.transform, pos + new Vector3(0f, 4.1f, 0f), "SIEM", accent, 0.032f);

            var glow = new GameObject("SiemLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.6f, -1.6f);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = accent;
            l.range = 9f;
            l.intensity = 1.8f;

            return console;
        }
    }
}
