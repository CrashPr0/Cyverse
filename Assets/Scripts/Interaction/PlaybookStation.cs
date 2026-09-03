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

        private const float SlotSpacing = 2.25f;
        private const float CardSpacing = 1.85f;

        public int Placed => placed;
        public int Total => steps != null ? steps.Length : 0;
        public string NextStep => steps != null && placed < steps.Length ? steps[placed] : string.Empty;
        public int NextSlot => Mathf.Min(placed + 1, Total);

        public bool IsExpectedCard(Carryable card) =>
            card != null && !string.IsNullOrEmpty(NextStep) && card.id == PlaybookId(NextStep);

        /// <summary>Moves the complete playbook interaction as one assembly.
        /// The board, slots, rack, cards, labels, and detection geometry all
        /// derive their placement from this transform.</summary>
        public void Place(Vector3 boardPosition, float yawDegrees)
        {
            if (steps == null) steps = Level2Content.PlaybookSteps();
            if (why == null) why = Level2Content.PlaybookWhy();
            transform.SetPositionAndRotation(boardPosition, Quaternion.Euler(0f, yawDegrees, 0f));
            ApplyReadableLayout();
        }

        /// <summary>Restores the private step arrays and DropZone delegates
        /// that Unity cannot serialize into a saved visual-pass scene.</summary>
        public void Configure()
        {
            steps = Level2Content.PlaybookSteps();
            why = Level2Content.PlaybookWhy();
            ApplyReadableLayout();
            foreach (var zone in FindObjectsOfType<DropZone>())
            {
                if (!zone.zoneName.StartsWith("STEP ")) continue;
                if (!int.TryParse(zone.zoneName.Substring(5), out int number)) continue;
                int slot = number - 1;
                if (slot < 0 || slot >= steps.Length) continue;
                var wiredZone = zone;
                zone.accepts = item => slot == placed && item.id == PlaybookId(steps[slot]);
                zone.onAccepted = item => OnAccepted(wiredZone, item, slot);
                zone.onRejected = item => OnRejected(wiredZone, item, slot);
            }
        }

        private void OnAccepted(DropZone zone, Carryable card, int slot)
        {
            card.Consume();
            placed++;

            ScoreSystem.Add(pointsPerStep);
            if (Sfx.Instance != null) Sfx.Instance.PlayStreak(placed);
            BurstFX.SpawnAbove(zone.transform, new Color(0.30f, 1f, 0.45f), 18, minimumHeight: 1.3f);

            // Replace the placeholder instead of stacking another label on
            // top of it. The completed sequence stays readable at a glance.
            SetSlotLabel(zone, $"{slot + 1}. {steps[slot]}",
                new Color(0.30f, 1f, 0.55f), 0.018f);

            if (HudUI.Instance != null)
                HudUI.Instance.ShowToast($"STEP {slot + 1}: {steps[slot]}  +{pointsPerStep}  ·  {why[slot]}",
                    new Color(0.30f, 1f, 0.45f));

            if (placed >= steps.Length && !IsComplete)
            {
                IsComplete = true;
                SocProgress.MarkPlaybookSolved();
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
                    ? $"Fill STEP {placed + 1} with {NextStep} first — work left to right."
                    : $"STEP {slot + 1} needs {steps[slot]}, not {card.itemName}.",
                new Color(1f, 0.55f, 0.4f));
        }

        private static void SetSlotLabel(DropZone zone, string text, Color color, float size)
        {
            foreach (TextMesh label in zone.GetComponentsInChildren<TextMesh>(true))
            {
                if (!label.text.StartsWith("STEP ")) continue;
                label.text = text;
                label.color = color;
                label.characterSize = size;
                return;
            }

            BuildKit.MakeLabel(zone.transform, new Vector3(0f, 1.55f, 0f),
                text, color, size, billboard: true);
        }

        // ---- Construction ----------------------------------------------------

        /// <summary>Board of numbered slots plus a shuffled card rack.</summary>
        public static PlaybookStation Build(Vector3 boardPos, Vector3 rackPos, Color accent,
            Func<bool> gate, string gateMessage, float yawDegrees = 0f)
        {
            var root = new GameObject("PlaybookStation");
            root.transform.SetPositionAndRotation(boardPos, Quaternion.Euler(0f, yawDegrees, 0f));

            var station = root.AddComponent<PlaybookStation>();
            station.steps = Level2Content.PlaybookSteps();
            station.why = Level2Content.PlaybookWhy();

            var frameMat = BuildKit.MakeStandard(new Color(0.08f, 0.09f, 0.13f), 0.55f, 0.35f);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Backboard", root.transform,
                new Vector3(0f, 2.1f, 0.6f), Vector3.zero, new Vector3(14f, 0.12f, 0.5f), frameMat, collider: false);
            BuildKit.MakeSign(root.transform, root.transform.TransformPoint(new Vector3(0f, 3.1f, 0f)),
                "IR PLAYBOOK", accent, 0.032f);
            BuildKit.MakeLabel(root.transform, new Vector3(0f, 2.5f, 0.3f),
                "TAKE A CARD FROM THE RACK, THEN PLACE IT ON THE NEXT NUMBERED SLOT",
                new Color(0.60f, 0.72f, 0.85f), 0.017f, billboard: false);
            var guide = BuildKit.MakeLabel(root.transform, new Vector3(0f, 2.18f, 0.3f),
                "1  PREPARATION   >   2  DETECTION   >   3  CONTAINMENT\n" +
                "4  ERADICATION   >   5  RECOVERY   >   6  LESSONS LEARNED",
                new Color(0.90f, 0.95f, 1f), 0.015f, billboard: false);
            guide.gameObject.name = "PlaybookSequenceGuide";

            // Numbered slots run along the board's local X axis, regardless
            // of which wall the complete assembly is mounted on.
            for (int i = 0; i < station.steps.Length; i++)
            {
                int slot = i; // capture per iteration
                Vector3 p = root.transform.TransformPoint(
                    new Vector3(CenteredOffset(i, station.steps.Length, SlotSpacing), 0f, 0f));
                var zone = DropZone.Build(p, $"STEP {i + 1}", accent);
                zone.transform.rotation = root.transform.rotation;
                zone.accepts = item =>
                    slot == station.placed && item.id == PlaybookId(station.steps[slot]);
                zone.onAccepted = item => station.OnAccepted(zone, item, slot);
                zone.onRejected = item => station.OnRejected(zone, item, slot);
            }

            // Card rack — deliberately NOT in playbook order.
            int[] shuffled = { 2, 5, 0, 4, 1, 3 };
            var rack = new GameObject("PlaybookRack");
            rack.transform.SetPositionAndRotation(rackPos, root.transform.rotation);
            BuildKit.SpawnLocal(PrimitiveType.Cube, "Table", rack.transform,
                new Vector3(0f, 0.45f, 0f), Vector3.zero, new Vector3(10.8f, 0.9f, 1.4f),
                BuildKit.MakeStandard(new Color(0.10f, 0.11f, 0.16f), 0.55f, 0.4f), collider: true);
            BuildKit.MakeLabel(rack.transform, new Vector3(0f, 1.8f, 0f),
                "SHUFFLED CARDS  —  E TO PICK UP", accent, 0.013f, billboard: false);

            for (int i = 0; i < shuffled.Length; i++)
            {
                string step = station.steps[shuffled[i]];
                var card = Carryable.Build(
                    rack.transform.TransformPoint(
                        new Vector3(CenteredOffset(i, shuffled.Length, CardSpacing), 0.9f, 0f)),
                    step, PlaybookId(step), accent);
                card.transform.rotation = rack.transform.rotation;
                foreach (TextMesh label in card.GetComponentsInChildren<TextMesh>(true))
                {
                    label.characterSize = 0.018f;
                    label.transform.localPosition = new Vector3(
                        0f, 0.68f, label.transform.localPosition.z);
                }
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

            station.ApplyReadableLayout();

            return station;
        }

        /// <summary>Upgrades serialized visual-pass scenes as well as fresh
        /// procedural builds. Older scenes retain the original cramped world
        /// positions, so Configure applies the current spacing at runtime.</summary>
        private void ApplyReadableLayout()
        {
            Transform backboard = transform.Find("Backboard");
            if (backboard != null)
                backboard.localScale = new Vector3(14f, backboard.localScale.y, backboard.localScale.z);

            TextMesh instruction = null;
            foreach (TextMesh label in GetComponentsInChildren<TextMesh>(true))
            {
                if (!label.text.Contains("response steps") && label.gameObject.name != "PlaybookInstruction")
                    continue;
                instruction = label;
                break;
            }
            if (instruction != null)
            {
                instruction.gameObject.name = "PlaybookInstruction";
                instruction.text = "TAKE A CARD FROM THE RACK, THEN PLACE IT ON THE NEXT NUMBERED SLOT";
                instruction.characterSize = 0.017f;
                instruction.transform.localPosition = new Vector3(0f, 2.5f, 0.3f);
                Billboard billboard = instruction.GetComponent<Billboard>();
                if (billboard != null) billboard.enabled = false;
            }

            if (transform.Find("PlaybookSequenceGuide") == null)
            {
                var guide = BuildKit.MakeLabel(transform, new Vector3(0f, 2.18f, 0.3f),
                    "1  PREPARATION   >   2  DETECTION   >   3  CONTAINMENT\n" +
                    "4  ERADICATION   >   5  RECOVERY   >   6  LESSONS LEARNED",
                    new Color(0.90f, 0.95f, 1f), 0.015f, billboard: false);
                guide.gameObject.name = "PlaybookSequenceGuide";
            }

            var zones = new System.Collections.Generic.List<DropZone>();
            foreach (DropZone zone in FindObjectsOfType<DropZone>())
                if (zone.zoneName.StartsWith("STEP ")) zones.Add(zone);
            zones.Sort((a, b) => StepNumber(a).CompareTo(StepNumber(b)));
            for (int i = 0; i < zones.Count; i++)
            {
                zones[i].transform.SetPositionAndRotation(
                    transform.TransformPoint(
                        new Vector3(CenteredOffset(i, zones.Count, SlotSpacing), 0f, 0f)),
                    transform.rotation);
            }

            GameObject rack = GameObject.Find("PlaybookRack");
            if (rack == null) return;
            // Keep the shuffled cards five metres in front of the board on
            // its readable side, even when the assembly changes walls.
            rack.transform.SetPositionAndRotation(
                transform.TransformPoint(new Vector3(0f, 0f, -5f)),
                transform.rotation);
            Transform table = rack.transform.Find("Table");
            if (table != null)
                table.localScale = new Vector3(10.8f, table.localScale.y, 1.4f);

            foreach (TextMesh label in rack.GetComponentsInChildren<TextMesh>(true))
            {
                if (!label.text.Contains("RESPONSE CARDS")) continue;
                label.text = "SHUFFLED CARDS  —  E TO PICK UP";
                label.characterSize = 0.013f;
                label.transform.localPosition = new Vector3(0f, 2.35f, 0f);
                Billboard billboard = label.GetComponent<Billboard>();
                if (billboard != null) billboard.enabled = false;
            }

            int[] shuffled = { 2, 5, 0, 4, 1, 3 };
            Carryable[] cards = FindObjectsOfType<Carryable>();
            for (int i = 0; i < shuffled.Length; i++)
            {
                string id = PlaybookId(steps[shuffled[i]]);
                foreach (Carryable card in cards)
                {
                    if (card.id != id || card.transform.parent != null) continue;
                    card.transform.SetPositionAndRotation(
                        rack.transform.TransformPoint(
                            new Vector3(CenteredOffset(i, shuffled.Length, CardSpacing), 0.9f, 0f)),
                        rack.transform.rotation);
                    card.transform.localScale = Vector3.one;
                    foreach (TextMesh label in card.GetComponentsInChildren<TextMesh>(true))
                    {
                        label.characterSize = 0.018f;
                        label.transform.localPosition = new Vector3(
                            0f, 0.68f, label.transform.localPosition.z);
                    }
                    break;
                }
            }
        }

        private static int StepNumber(DropZone zone) =>
            int.TryParse(zone.zoneName.Substring(5), out int number) ? number : int.MaxValue;

        private static float CenteredOffset(int index, int count, float spacing) =>
            (index - (count - 1) * 0.5f) * spacing;

        /// <summary>Carryable ids must be stable and space-free.</summary>
        private static string PlaybookId(string step) =>
            "ir_" + step.ToLowerInvariant().Replace(' ', '_');
    }
}
