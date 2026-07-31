using System;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Core;
using Cyverse.Level;
using Cyverse.UI;

namespace Cyverse.Interaction
{
    /// <summary>
    /// Task 3 — INCIDENT RESPONSE. Six playbook cards must be placed on the
    /// sequence board in the correct order, using the same carry mechanic as
    /// Level 1's data crates.
    ///
    /// Slots fill strictly left to right: a slot refuses anything until every
    /// slot before it is filled, and only accepts the step that genuinely
    /// comes next. That constraint is the teaching — in real IR, doing
    /// Eradication before Containment reinfects the network and doing
    /// Recovery before Eradication restores the malware with the backup.
    /// </summary>
    public class PlaybookStation : MonoBehaviour
    {
        public event Action Completed;
        public bool IsComplete { get; private set; }

        public int pointsPerStep = 50;

        private string[] steps;
        private string[] why;
        private int placed;

        public int Placed => placed;
        public int Total => steps != null ? steps.Length : 0;

        /// <summary>
        /// Attach the sequence slots' accept/place hooks and restore the step
        /// list. Build() routes through this, but so must the level manager on
        /// Start: the hooks are Func/Action and `steps` is a private array, so a
        /// scene SAVED with the board already built loaded with everything null
        /// — the slots accepted no cards and the checklist read 0/0.
        /// Idempotent: slots that already carry hooks are left alone.
        /// </summary>
        public void Rebind()
        {
            if (steps == null || steps.Length == 0) steps = Level2Content.PlaybookSteps();
            if (why == null || why.Length == 0) why = Level2Content.PlaybookWhy();

            foreach (var zone in FindObjectsOfType<DropZone>())
            {
                if (zone.accepts != null) continue;
                int slot = SlotIndex(zone.zoneName);
                if (slot < 0 || slot >= steps.Length) continue;

                var z = zone;
                z.accepts = item => slot == placed && item.id == PlaybookId(steps[slot]);
                z.onAccepted = item => OnAccepted(z, item, slot);
                z.onRejected = item => OnRejected(z, item, slot);
            }
        }

        /// <summary>"STEP 3" → 2. Anything else (a Level 1 role pedestal, the
        /// MFA token slot) returns -1 and is skipped.</summary>
        private static int SlotIndex(string zoneName)
        {
            if (string.IsNullOrEmpty(zoneName) || !zoneName.StartsWith("STEP ")) return -1;
            return int.TryParse(zoneName.Substring(5), out int n) ? n - 1 : -1;
        }

        private void OnAccepted(DropZone zone, Carryable card, int slot)
        {
            card.Consume();
            placed++;

            ScoreSystem.Add(pointsPerStep);
            if (Sfx.Instance != null) Sfx.Instance.PlayStreak(placed);
            BurstFX.SpawnAbove(zone.transform, new Color(0.30f, 1f, 0.45f), 18, minimumHeight: 1.3f);

            // Leave the step's name standing on the slot as a record.
            BuildKit.MakeLabel(zone.transform, new Vector3(0f, 1.25f, 0f),
                $"{slot + 1}. {steps[slot]}", new Color(0.30f, 1f, 0.55f), 0.020f, billboard: true);

            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast($"STEP {slot + 1}: {steps[slot]}  +{pointsPerStep}  ·  {why[slot]}",
                    new Color(0.30f, 1f, 0.45f));

            if (placed >= steps.Length && !IsComplete)
            {
                IsComplete = true;
                if (HudUI.Instance != null)
                    HudUI.Instance.ShowToast("PLAYBOOK COMPLETE — response sequence locked in",
                        new Color(0.90f, 0.66f, 0.14f));
                Completed?.Invoke();
            }
        }

        private void OnRejected(DropZone zone, Carryable card, int slot)
        {
            // DropZone already played the deny sound.
            if (HudUI.Instance == null) return;
            HudUI.Instance.ShowToast(
                slot > placed
                    ? $"Fill step {placed + 1} first — the playbook runs in order."
                    : $"That isn't step {slot + 1}. Think about what has to happen before anything else.",
                new Color(1f, 0.55f, 0.4f));
        }

        // ---- Construction ----------------------------------------------------

        /// <summary>Board of numbered slots plus a shuffled card rack.</summary>
        public static PlaybookStation Build(Vector3 boardPos, Vector3 rackPos, Color accent,
            Func<bool> gate, string gateMessage)
        {
            var root = new GameObject("PlaybookStation");
            root.transform.position = boardPos;

            var station = root.AddComponent<PlaybookStation>();
            station.steps = Level2Content.PlaybookSteps();
            station.why = Level2Content.PlaybookWhy();

            var frameMat = BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.55f, 0.35f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Backboard", root.transform,
                new Vector3(0f, 2.1f, 0.6f), Vector3.zero, new Vector3(11.5f, 0.12f, 0.5f), frameMat, collider: false);
            BuildKit.MakeSign(root.transform, boardPos + new Vector3(0f, 3.1f, 0f),
                "IR PLAYBOOK", accent, 0.032f);
            BuildKit.MakeLabel(root.transform, new Vector3(0f, 2.5f, 0.3f),
                "Place the response steps in order  →", new Color(0.60f, 0.72f, 0.85f), 0.020f, billboard: true);

            // Numbered slots, west to east.
            for (int i = 0; i < station.steps.Length; i++)
                DropZone.Build(boardPos + new Vector3(-5f + i * 2f, 0f, 0f), $"STEP {i + 1}", accent);
            station.Rebind();

            // Card rack — deliberately NOT in playbook order.
            int[] shuffled = { 2, 5, 0, 4, 1, 3 };
            var rack = new GameObject("PlaybookRack");
            rack.transform.position = rackPos;
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Table", rack.transform,
                new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(5.0f, 0.9f, 1.0f),
                BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f), collider: true);
            BuildKit.MakeLabel(rack.transform, new Vector3(0f, 1.8f, 0f),
                "RESPONSE CARDS", accent, 0.024f, billboard: true);

            for (int i = 0; i < shuffled.Length; i++)
            {
                string step = station.steps[shuffled[i]];
                var card = Carryable.Build(
                    rackPos + new Vector3(-2f + i * 0.8f, 0.9f, 0f),
                    step, PlaybookId(step), accent);
                card.gate = gate;
                card.gateMessage = gateMessage;
            }

            var glow = new GameObject("PlaybookLight");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 3f, -1.5f);
            var l = glow.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = accent;
            l.range = 12f;
            l.intensity = 1.7f;

            return station;
        }

        /// <summary>Carryable ids must be stable and space-free.</summary>
        private static string PlaybookId(string step) =>
            "ir_" + step.ToLowerInvariant().Replace(' ', '_');
    }
}
